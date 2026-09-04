using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Wave-based enemy spawner for Survivors Mode. Spawns enemies in configurable periodic group batches
///     matching the wave-spawner approach, tracks current wave, and supports cleanly pausing spawning and
///     despawning remaining enemies during post-objective cleanup.
/// </summary>
public class SurvivorsSpawner : MonoBehaviour
{
    public static SurvivorsSpawner Instance { get; private set; }

    private class SpawnType
    {
        public EnemyDefinition def;
        public GameObject prefab;
        public int alive;
        public bool isShielder;
        public readonly HashSet<Health> aliveInstances = new HashSet<Health>();

        public int AliveCount
        {
            get
            {
                aliveInstances.RemoveWhere(h => h == null || h.IsDead);
                return aliveInstances.Count;
            }
        }
    }

    [Header("Enemy Roster & Assets")]
    [Tooltip("CSV-driven enemy roster.")]
    [SerializeField] private EnemyRosterSO roster;

    [Tooltip("Enemy prefab map asset.")]
    [SerializeField] private EnemyPrefabMapSO prefabMap;

    [Header("Wave Spawner Settings")]
    [Tooltip("Round pacing config asset containing round rosters, 20 max concurrent limit, 3s indicators, and drop weights.")]
    [SerializeField] private RoundPacingConfigSO pacingConfig;

    [Tooltip("Prefab spawned as the ground telegraph indicator.")]
    [SerializeField] private GameObject spawnIndicatorPrefab;

    [Tooltip("Optional shared WaveConfigSO asset. If assigned, overrides inspector batch settings.")]
    [SerializeField] private WaveConfigSO config;

    [Tooltip("Maximum enemies spawned in a single periodic group batch (capped by maxConcurrentEnemies).")]
    [SerializeField] private int spawnBatchSize = 10;

    [Tooltip("Seconds between periodic group spawns.")]
    [SerializeField] private float spawnBatchInterval = 8.0f;

    [Tooltip("Seconds between individual enemy spawns within a group burst.")]
    [SerializeField] private float spawnInterval = 0.2f;

    [Tooltip("Maximum alive enemies permitted on field simultaneously.")]
    [SerializeField] private int maxConcurrentEnemies = 20;

    [Header("Audio")]
    [Tooltip("Battle horn sound played when a periodic group wave batch spawns.")]
    [SerializeField] private AudioClip groupSpawnHornSound;

    [Tooltip("Volume multiplier for the group spawn horn sound.")]
    [Range(0f, 1f)] [SerializeField] private float hornVolume = 1f;

    [Header("Spawn Positioning Points")]
    [Tooltip("Scene spawn points. Goblins spawn at a random spawnpoint. If empty, auto-discovers scene spawnpoints or falls back to ring around player.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Random scatter radius in meters around the chosen spawn point.")]
    [SerializeField] private float spawnRadiusAroundPoint = 2f;

    [Header("Spawn Positioning around Player (Fallback)")]
    [Tooltip("Minimum distance from player for fallback spawn positions.")]
    [SerializeField] private float minPlayerDistance = 14f;

    [Tooltip("Maximum distance from player for fallback spawn positions.")]
    [SerializeField] private float maxPlayerDistance = 24f;

    [Tooltip("Distance threshold for snapping spawn points to NavMesh.")]
    [SerializeField] private float navMeshSampleDistance = 5f;

    [Header("Special Enemy Caps")]
    [Tooltip("Maximum concurrent shielders (bubblers) allowed simultaneously in Survivors Mode.")]
    [SerializeField] private int maxConcurrentShielders = 2;

    [Tooltip("Whether to ignore per-row maxConcurrent limits in CSV to allow horde spawning up to maxConcurrentEnemies.")]
    [SerializeField] private bool ignoreRowMaxConcurrent = true;

    private readonly List<SpawnType> spawnTypes = new List<SpawnType>();
    private readonly HashSet<Health> aliveEnemies = new HashSet<Health>();
    private PlayerStats stats;
    private int aliveCount;
    private int currentWave = 1;
    private int totalToSpawnThisWave;
    private int remainingToSpawn;
    private bool isSpawningActive = false;
    private Coroutine spawnLoopCoroutine;
    private bool isInitialized = false;
    private bool anyError = false;

    public event Action OnWaveWiped;

    public int AliveCount => aliveCount;
    public int CurrentWave => currentWave;
    public int RemainingToSpawn => remainingToSpawn;
    public int TotalToSpawnThisWave => totalToSpawnThisWave;
    public int MaxConcurrentEnemies => pacingConfig != null && pacingConfig.maxConcurrentEnemies > 0 ? pacingConfig.maxConcurrentEnemies : (config != null && config.maxConcurrent > 0 ? config.maxConcurrent : (maxConcurrentEnemies > 0 ? maxConcurrentEnemies : 20));
    public bool IsSpawningActive => isSpawningActive;
    public int MaxConcurrentShielders => maxConcurrentShielders;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeIfNeeded();
    }

    private void InitializeIfNeeded()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (roster == null || prefabMap == null)
        {
            Debug.LogError("[SurvivorsSpawner] Roster or PrefabMap SO is not assigned!");
            anyError = true;
            return;
        }

        spawnTypes.Clear();

        // Initialize spawn types from roster & prefab map
        foreach (EnemyDefinition def in roster.Enemies)
        {
            if (def == null || !def.enabled) continue;

            GameObject prefab = prefabMap.FindPrefab(def.id);
            if (prefab == null)
            {
                Debug.LogWarning($"[SurvivorsSpawner] Prefab map missing prefab for roster id '{def.id}'. Skipping.");
                continue;
            }

            bool isShielder = CheckIfShielder(def, prefab);

            spawnTypes.Add(new SpawnType
            {
                def = def,
                prefab = prefab,
                alive = 0,
                isShielder = isShielder
            });
        }

        if (spawnTypes.Count == 0)
        {
            Debug.LogError("[SurvivorsSpawner] No valid spawn types loaded from roster and prefab map!");
            anyError = true;
            return;
        }

        if (Player.Instance != null)
        {
            stats = Player.Instance.Stats;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            GameObject parentObj = GameObject.Find("Spawnpoints");
            if (parentObj != null)
            {
                Transform[] childTransforms = parentObj.GetComponentsInChildren<Transform>();
                List<Transform> list = new List<Transform>();
                foreach (Transform t in childTransforms)
                {
                    if (t != parentObj.transform) list.Add(t);
                }
                if (list.Count > 0)
                {
                    spawnPoints = list.ToArray();
                }
            }
        }
    }

    private void Start()
    {
        InitializeIfNeeded();

        // If not already active (e.g. if no GameLoopManager exists to drive waves), start wave 1
        if (!isSpawningActive && GameLoopManager.Instance == null)
        {
            StartWave(1);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    ///     Begins wave spawning for the given wave number with an optional enemy quota.
    /// </summary>
    public void StartWave(int waveNumber, int enemyCount = 0)
    {
        InitializeIfNeeded();
        if (anyError) return;

        currentWave = Mathf.Max(1, waveNumber);

        if (enemyCount > 0)
        {
            totalToSpawnThisWave = enemyCount;
        }
        else if (pacingConfig != null)
        {
            int round = RunSession.CurrentRound > 0 ? RunSession.CurrentRound : 1;
            RoundPacingConfigSO.RoundDefinition roundDef = pacingConfig.GetRound(round);
            totalToSpawnThisWave = roundDef != null ? roundDef.requiredKillsPerWave : (15 + (round - 1) * 5);
        }
        else
        {
            totalToSpawnThisWave = 15 + (currentWave - 1) * 5;
        }

        remainingToSpawn = totalToSpawnThisWave;
        isSpawningActive = true;

        if (spawnLoopCoroutine != null)
        {
            StopCoroutine(spawnLoopCoroutine);
        }
        spawnLoopCoroutine = StartCoroutine(SpawnLoop());
        Debug.Log($"[SurvivorsSpawner] StartWave {currentWave} started. (quota={totalToSpawnThisWave}, spawnBatchSize={spawnBatchSize}, maxConcurrent={MaxConcurrentEnemies})");
    }

    /// <summary>
    ///     Stops active enemy spawning (e.g. during cleanup or intermission).
    /// </summary>
    public void StopSpawning()
    {
        isSpawningActive = false;
        if (spawnLoopCoroutine != null)
        {
            StopCoroutine(spawnLoopCoroutine);
            spawnLoopCoroutine = null;
        }
    }

    /// <summary>
    ///     Cleanly despawns all currently alive enemies from the scene.
    /// </summary>
    public void DespawnAllAliveEnemies()
    {
        List<Health> toDespawn = new List<Health>(aliveEnemies);
        aliveEnemies.Clear();
        aliveCount = 0;

        foreach (SpawnType type in spawnTypes)
        {
            type.alive = 0;
            type.aliveInstances.Clear();
        }

        foreach (Health health in toDespawn)
        {
            if (health != null && !health.IsDead && health.gameObject != null)
            {
                Destroy(health.gameObject);
            }
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (isSpawningActive && remainingToSpawn > 0)
        {
            if (SurvivorsGameManager.Instance != null && !SurvivorsGameManager.Instance.IsGameActive)
            {
                yield return null;
                continue;
            }

            int effectiveBatchSize = config != null && config.spawnBatchSize > 0 ? config.spawnBatchSize : (spawnBatchSize > 0 ? spawnBatchSize : 10);
            float effectiveBatchInterval = config != null && config.spawnBatchInterval > 0f ? config.spawnBatchInterval : (spawnBatchInterval > 0f ? spawnBatchInterval : 8.0f);
            float effectiveSpawnInterval = config != null && config.spawnInterval > 0f ? config.spawnInterval : (spawnInterval > 0f ? spawnInterval : 0.2f);
            int effectiveMaxConcurrent = MaxConcurrentEnemies;

            if (aliveCount < effectiveMaxConcurrent && remainingToSpawn > 0)
            {
                int batchTarget = Mathf.Min(effectiveBatchSize, effectiveMaxConcurrent - aliveCount);
                batchTarget = Mathf.Min(batchTarget, remainingToSpawn);

                if (batchTarget > 0)
                {
                    PlayGroupSpawnHorn();

                    for (int i = 0; i < batchTarget && isSpawningActive && remainingToSpawn > 0; i++)
                    {
                        while (SurvivorsGameManager.Instance != null && !SurvivorsGameManager.Instance.IsGameActive)
                        {
                            yield return null;
                        }

                        if (!isSpawningActive || remainingToSpawn <= 0) break;

                        remainingToSpawn--;
                        SpawnEnemyForWave(currentWave);

                        if (effectiveSpawnInterval > 0f && i < batchTarget - 1)
                        {
                            yield return new WaitForSeconds(effectiveSpawnInterval);
                        }
                    }
                }

                if (!isSpawningActive || remainingToSpawn <= 0) yield break;

                // Wait for batch interval
                float timer = 0f;
                while (timer < effectiveBatchInterval && isSpawningActive && remainingToSpawn > 0)
                {
                    if (SurvivorsGameManager.Instance == null || SurvivorsGameManager.Instance.IsGameActive)
                    {
                        timer += Time.deltaTime;
                    }

                    if (aliveCount <= 0 && timer >= 1.0f)
                    {
                        break; // Trigger next batch early when all enemies are cleared
                    }

                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private void PlayGroupSpawnHorn()
    {
        AudioClip clip = config != null && config.groupSpawnHornSound != null ? config.groupSpawnHornSound : groupSpawnHornSound;
        float volume = config != null ? config.hornVolume : hornVolume;

        if (clip == null) return;

        if (TryGetComponent(out AudioSource audioSource))
        {
            audioSource.PlayOneShot(clip, volume);
        }
        else
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(clip, pos, volume);
        }
    }

    private void SpawnEnemyForWave(int waveNumber)
    {
        SpawnType selectedType = SelectSpawnTypeForWave(waveNumber);

        if (stats == null && Player.Instance != null)
        {
            stats = Player.Instance.Stats;
        }

        if (stats != null)
        {
            float goldenChance = stats.GetValue(StatType.GoldenGoblinChance);
            if (goldenChance > 0f && UnityEngine.Random.value < goldenChance)
            {
                SpawnType goldenType = spawnTypes.Find(t => t.def != null && t.def.id == "golden_goblin");
                if (goldenType != null && goldenType.prefab != null)
                {
                    selectedType = goldenType;
                }
            }
        }

        if (selectedType == null || selectedType.prefab == null) return;

        // Extra safeguard: if a shielder was selected but is already at cap, swap to non-shielder
        if (selectedType.isShielder && selectedType.AliveCount >= maxConcurrentShielders)
        {
            SpawnType fallback = spawnTypes.Find(t => !t.isShielder) ?? spawnTypes[0];
            if (fallback != null && fallback.prefab != null && (!fallback.isShielder || fallback.AliveCount < maxConcurrentShielders))
            {
                selectedType = fallback;
            }
            else
            {
                return;
            }
        }

        Vector3 spawnPos = ResolveSpawnPosition();
        float indicatorDuration = pacingConfig != null ? pacingConfig.spawnTelegraphDuration : 3.0f;
        GameObject indPrefab = (pacingConfig != null && pacingConfig.indicatorPrefab != null) ? pacingConfig.indicatorPrefab : spawnIndicatorPrefab;

        aliveCount++; // Reserve slot during telegraph
        SpawnIndicator.Create(spawnPos, indicatorDuration, indPrefab, () =>
        {
            if (!isSpawningActive || this == null)
            {
                aliveCount = Mathf.Max(0, aliveCount - 1);
                return;
            }

            GameObject enemy = Instantiate(selectedType.prefab, spawnPos, Quaternion.identity);

            WaveSpawner.ApplyDefinition(enemy, selectedType.def);

            if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentWaveBuff != BannerBuffType.None)
            {
                EnemyBuffController buffController = enemy.AddComponent<EnemyBuffController>();
                buffController.Initialize(GameLoopManager.Instance.CurrentWaveBuff);
            }

            if (stats != null)
            {
                float impulseChance = stats.GetValue(StatType.ImpulseGoblinChance);
                if (impulseChance > 0f && UnityEngine.Random.value < impulseChance)
                {
                    enemy.GetComponent<ImpulseGoblin>()?.MarkImpulse();
                }
            }

            Health health = enemy.GetComponent<Health>();
            if (health != null)
            {
                selectedType.alive++;
                selectedType.aliveInstances.Add(health);
                aliveEnemies.Add(health);

                Action handler = null;
                handler = () =>
                {
                    health.OnDied -= handler;
                    aliveEnemies.Remove(health);
                    selectedType.aliveInstances.Remove(health);
                    selectedType.alive = Mathf.Max(0, selectedType.alive - 1);
                    aliveCount = Mathf.Max(0, aliveCount - 1);

                    GameLoopManager.Instance?.OnEnemyKilled(health);

                    if (remainingToSpawn <= 0 && aliveCount <= 0 && isSpawningActive)
                    {
                        StopSpawning();
                        OnWaveWiped?.Invoke();
                    }
                };
                health.OnDied += handler;
            }
            else
            {
                aliveCount = Mathf.Max(0, aliveCount - 1);
                if (remainingToSpawn <= 0 && aliveCount <= 0 && isSpawningActive)
                {
                    StopSpawning();
                    OnWaveWiped?.Invoke();
                }
            }
        });
    }

    private static bool CheckIfShielder(EnemyDefinition def, GameObject prefab)
    {
        if (def != null && !string.IsNullOrEmpty(def.id))
        {
            if (string.Equals(def.id, "bubbler", StringComparison.OrdinalIgnoreCase) ||
                def.id.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                def.id.IndexOf("bubbler", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        if (prefab != null)
        {
            if (prefab.GetComponent<BubblerCaster>() != null ||
                prefab.GetComponent<BubbleShield>() != null ||
                prefab.name.IndexOf("bubbler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefab.name.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private SpawnType SelectSpawnTypeForWave(int waveNumber)
    {
        List<SpawnType> eligible = new List<SpawnType>();

        int currentRound = RunSession.CurrentRound;
        RoundPacingConfigSO.RoundDefinition roundDef = pacingConfig != null ? pacingConfig.GetRound(currentRound) : null;
        string[] allowed = roundDef != null ? roundDef.allowedEnemyIds : null;

        foreach (SpawnType type in spawnTypes)
        {
            if (type.def.enabled && waveNumber >= type.def.unlockWave)
            {
                if (allowed != null && allowed.Length > 0 && Array.IndexOf(allowed, type.def.id) < 0)
                {
                    continue;
                }

                int currentAlive = type.AliveCount;

                // Shielders (bubblers) are strictly capped at maxConcurrentShielders
                if (type.isShielder)
                {
                    int cap = maxConcurrentShielders;
                    if (type.def.maxConcurrent > 0 && type.def.maxConcurrent < cap)
                    {
                        cap = type.def.maxConcurrent;
                    }
                    if (currentAlive >= cap)
                    {
                        continue;
                    }
                }
                else if (!ignoreRowMaxConcurrent && type.def.maxConcurrent > 0 && currentAlive >= type.def.maxConcurrent)
                {
                    continue;
                }

                eligible.Add(type);
            }
        }

        if (eligible.Count == 0)
        {
            SpawnType fallback = spawnTypes.Find(t => !t.isShielder) ?? spawnTypes[0];
            return fallback;
        }

        // Weighted roll based on spawnChance
        float totalWeight = 0f;
        foreach (SpawnType type in eligible)
        {
            totalWeight += type.def.spawnChance;
        }

        if (totalWeight <= 0f)
        {
            return eligible[UnityEngine.Random.Range(0, eligible.Count)];
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (SpawnType type in eligible)
        {
            cumulative += type.def.spawnChance;
            if (roll <= cumulative)
            {
                return type;
            }
        }

        return eligible[0];
    }

    private Vector3 ResolveSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int randomIndex = UnityEngine.Random.Range(0, spawnPoints.Length);
                Transform pt = spawnPoints[randomIndex];
                if (pt == null) continue;

                Vector3 candidate = pt.position;
                if (spawnRadiusAroundPoint > 0f)
                {
                    Vector2 jitter = UnityEngine.Random.insideUnitCircle * spawnRadiusAroundPoint;
                    candidate += new Vector3(jitter.x, 0f, jitter.y);
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    return navHit.position;
                }
            }
        }

        Vector3 center = Player.Instance != null ? Player.Instance.transform.position : transform.position;
        NavMeshPath path = new NavMeshPath();
        Vector3 fallbackPos = center + new Vector3(minPlayerDistance, 0f, 0f);
        bool hasFallback = false;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            float distance = UnityEngine.Random.Range(minPlayerDistance, maxPlayerDistance);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

            // Raycast downward to verify candidate lands directly on Terrain (skipping rock props and ledges)
            Ray ray = new Ray(candidate + Vector3.up * 50f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit rayHit, 100f))
            {
                bool isTerrain = rayHit.collider is TerrainCollider ||
                                 rayHit.collider.GetComponent<Terrain>() != null ||
                                 rayHit.collider.gameObject.name.ToLower().Contains("terrain");

                if (isTerrain)
                {
                    Vector3 terrainPoint = rayHit.point;
                    if (NavMesh.SamplePosition(terrainPoint, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                    {
                        // Verify complete NavMesh path to player so enemies are never trapped on rocks/isolated islands
                        if (NavMesh.CalculatePath(navHit.position, center, NavMesh.AllAreas, path))
                        {
                            if (path.status == NavMeshPathStatus.PathComplete)
                            {
                                return navHit.position;
                            }
                            else if (!hasFallback)
                            {
                                fallbackPos = navHit.position;
                                hasFallback = true;
                            }
                        }
                    }
                }
            }
        }

        return fallbackPos;
    }

    /// <summary>Debug helper for DevConsole: list of loaded spawnable enemy definitions.</summary>
    public IReadOnlyList<EnemyDefinition> DebugSpawnableTypes
    {
        get
        {
            List<EnemyDefinition> list = new List<EnemyDefinition>();
            foreach (SpawnType type in spawnTypes)
            {
                if (type.def != null) list.Add(type.def);
            }
            return list;
        }
    }

    /// <summary>Debug helper for DevConsole: spawns one enemy of the specified type id.</summary>
    public void DebugSpawnEnemyType(string id)
    {
        SpawnType type = spawnTypes.Find(t => t.def != null && t.def.id == id);
        if (type == null)
        {
            type = spawnTypes.Count > 0 ? spawnTypes[0] : null;
        }

        if (type != null)
        {
            SpawnEnemyForType(type);
        }
    }

    /// <summary>Debug helper for DevConsole: burst spawns N enemies immediately.</summary>
    public void DebugSpawnBurst(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnEnemyForWave(currentWave);
        }
    }

    private void SpawnEnemyForType(SpawnType type)
    {
        if (type == null || type.prefab == null) return;

        Vector3 spawnPos = ResolveSpawnPosition();
        GameObject enemy = Instantiate(type.prefab, spawnPos, Quaternion.identity);
        WaveSpawner.ApplyDefinition(enemy, type.def);

        Health health = enemy.GetComponent<Health>();
        if (health != null)
        {
            aliveCount++;
            type.alive++;
            type.aliveInstances.Add(health);
            aliveEnemies.Add(health);

            Action handler = null;
            handler = () =>
            {
                health.OnDied -= handler;
                aliveEnemies.Remove(health);
                type.aliveInstances.Remove(health);
                type.alive = Mathf.Max(0, type.alive - 1);
                aliveCount = Mathf.Max(0, aliveCount - 1);
            };
            health.OnDied += handler;
        }
    }
}
