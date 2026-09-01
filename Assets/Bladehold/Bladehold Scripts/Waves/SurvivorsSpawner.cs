using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Continuous enemy spawner for Survivors Mode. Spawns enemies around the player in off-screen rings
///     at a configurable, time-ramping spawn rate while capping maximum concurrent alive enemies.
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

    [Header("Spawn Rate Settings")]
    [Tooltip("Initial delay in seconds between enemy spawns (e.g. 1.0 = 1 enemy/sec).")]
    [SerializeField] private float baseSpawnInterval = 1.0f;

    [Tooltip("Minimum spawn interval floor in seconds (fastest possible spawn rate).")]
    [SerializeField] private float minSpawnInterval = 0.15f;

    [Tooltip("Seconds reduced from spawn interval per minute of play (e.g. 0.05 = 0.05s faster spawn rate every minute).")]
    [SerializeField] private float spawnIntervalDecreasePerMinute = 0.05f;

    [Tooltip("Maximum alive enemies permitted on field simultaneously.")]
    [SerializeField] private int maxConcurrentEnemies = 100;

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

    private readonly List<SpawnType> spawnTypes = new List<SpawnType>();
    private readonly HashSet<Health> aliveEnemies = new HashSet<Health>();
    private PlayerStats stats;
    private float spawnTimer;
    private int aliveCount;
    private bool anyError = false;

    public int AliveCount => aliveCount;
    public int MaxConcurrentEnemies => maxConcurrentEnemies;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (roster == null || prefabMap == null)
        {
            Debug.LogError("[SurvivorsSpawner] Roster or PrefabMap SO is not assigned!");
            anyError = true;
            return;
        }

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

    private void Update()
    {
        if (anyError) return;

        // Pause spawning if SurvivorsGameManager is not active or paused
        if (SurvivorsGameManager.Instance != null && !SurvivorsGameManager.Instance.IsGameActive)
        {
            return;
        }

        if (aliveCount >= maxConcurrentEnemies)
        {
            return;
        }

        float currentRunTime = SurvivorsGameManager.Instance != null ? SurvivorsGameManager.Instance.RunTimer : Time.time;
        float currentInterval = CalculateCurrentSpawnInterval(currentRunTime);

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy(currentRunTime);
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
    ///     Calculates spawn interval based on elapsed run time.
    /// </summary>
    public float CalculateCurrentSpawnInterval(float runTimeSeconds)
    {
        float elapsedMinutes = runTimeSeconds / 60f;
        float calculated = baseSpawnInterval - (elapsedMinutes * spawnIntervalDecreasePerMinute);
        return Mathf.Max(minSpawnInterval, calculated);
    }

    private void SpawnEnemy(float runTimeSeconds)
    {
        SpawnType selectedType = SelectSpawnTypeForTime(runTimeSeconds);

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

        // Extra safeguard: if a shielder was somehow selected but is already at cap, swap to non-shielder
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
        GameObject enemy = Instantiate(selectedType.prefab, spawnPos, Quaternion.identity);

        // Apply roster stat overrides
        WaveSpawner.ApplyDefinition(enemy, selectedType.def);

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
            aliveCount++;
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
            };
            health.OnDied += handler;
        }
    }

    [Tooltip("Whether to ignore per-row maxConcurrent limits in CSV to allow continuous horde spawning up to maxConcurrentEnemies.")]
    [SerializeField] private bool ignoreRowMaxConcurrent = true;

    [Header("Special Enemy Caps")]
    [Tooltip("Maximum concurrent shielders (bubblers) allowed simultaneously in Survivors Mode. Overrides ignoreRowMaxConcurrent.")]
    [SerializeField] private int maxConcurrentShielders = 2;

    public int MaxConcurrentShielders => maxConcurrentShielders;

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

    private SpawnType SelectSpawnTypeForTime(float runTimeSeconds)
    {
        // Simulated wave level: 1 + elapsed minutes
        int effectiveWave = Mathf.FloorToInt(runTimeSeconds / 60f) + 1;

        List<SpawnType> eligible = new List<SpawnType>();

        foreach (SpawnType type in spawnTypes)
        {
            if (type.def.enabled && effectiveWave >= type.def.unlockWave)
            {
                int currentAlive = type.AliveCount;

                // Shielders (bubblers) are strictly capped at maxConcurrentShielders (default 2)
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
        float runTime = SurvivorsGameManager.Instance != null ? SurvivorsGameManager.Instance.RunTimer : Time.time;
        for (int i = 0; i < count; i++)
        {
            SpawnEnemy(runTime);
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
