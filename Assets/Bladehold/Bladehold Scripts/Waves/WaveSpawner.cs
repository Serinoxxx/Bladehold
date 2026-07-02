using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Drives wave-based enemy spawning. Each wave has a total number of enemies to kill (growing with
///     the wave number, per <see cref="WaveConfigSO" />); at most <see cref="WaveConfigSO.maxConcurrent" />
///     are alive at once, and as they die replacements trickle in until the wave total has been killed.
///     Clearing a wave starts an intermission countdown, then the next, larger wave.
///
///     What spawns is driven by the <see cref="EnemyRosterSO" /> CSV: each spawn slot first fills any
///     type still owed its per-wave <c>minSpawn</c> guarantee, then rolls the non-fallback types in CSV
///     order — the first type that is unlocked (<c>unlockWave</c>), under its own concurrent cap
///     (<c>maxConcurrent</c>), and wins its <c>spawnChance</c> roll (a percent: 10 = 10%) is spawned;
///     otherwise the fallback (first) row spawns. The row's stat overrides (health/damage/gold/speed/
///     scale) are applied to the instance right after Instantiate, before its Start runs, so the shared
///     ScriptableObjects are never mutated.
///
///     A scene singleton like <see cref="GameStats" /> so the <see cref="DeathScreen" /> can read the
///     current wave. It tracks enemy deaths through each spawned enemy's <see cref="Health.OnDied" />
///     (enemies become corpses on death rather than being destroyed, so death — not destruction — is the
///     signal), and stops spawning when the player dies. UI reacts via the events below; the spawner stays
///     unaware of what listens.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance;

    /// <summary>Inspector mapping from a roster CSV id to the prefab that spawns for it.</summary>
    [Serializable]
    private class EnemyPrefabEntry
    {
        [Tooltip("Must match an id in the roster CSV.")]
        public string id;
        [Tooltip("The enemy prefab. Must have a Health component (death is tracked via Health.OnDied).")]
        public GameObject prefab;
    }

    /// <summary>A roster row paired with its prefab, plus this type's live-count (for its concurrent
    /// cap) and per-wave spawn count (for its minSpawn guarantee).</summary>
    private class SpawnType
    {
        public EnemyDefinition def;
        public GameObject prefab;
        public int alive;
        public int spawnedThisWave;
    }

    [Header("What to spawn")]
    [Tooltip("CSV-driven enemy roster. The first row is the unlimited fallback type; later rows roll their spawnChance per spawn.")]
    [SerializeField] private EnemyRosterSO roster;
    [Tooltip("Maps each roster CSV id to its prefab. Roster rows without a mapping here are skipped (with a warning) until one is added.")]
    [SerializeField] private EnemyPrefabEntry[] enemyPrefabs;
    [SerializeField] private WaveConfigSO config;

    [Header("Where to spawn")]
    [Tooltip("Spawn points. Goblins spawn at a random one each time. If empty, they spawn around this object within Spawn Radius.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Fallback spawn radius around this object, used only when no spawn points are assigned.")]
    [SerializeField] private float spawnRadius = 8f;
    [Tooltip("Spawn positions are snapped to the nearest NavMesh point within this distance, so goblins land on walkable ground.")]
    [SerializeField] private float navMeshSampleDistance = 3f;

    /// <summary>Seconds remaining in the pre-wave countdown, fired once per second during the intermission.</summary>
    public event Action<int> CountdownTick;

    /// <summary>Raised when a wave begins, carrying the (1-based) wave number.</summary>
    public event Action<int> WaveStarted;

    /// <summary>Raised when every goblin in a wave has been killed, carrying the cleared wave number.</summary>
    public event Action<int> WaveCleared;

    /// <summary>The wave currently in progress (or about to start), 1-based.</summary>
    public int CurrentWave { get; private set; }

    private int waveGoblinTotal;   // enemies that must die to clear the current wave
    private int killedThisWave;    // enemies killed so far this wave
    private int remainingToSpawn;  // enemies not yet spawned this wave
    private int aliveCount;        // enemies currently alive
    private readonly HashSet<Health> aliveEnemies = new HashSet<Health>(); // so DebugAdvanceWave can kill them
    private readonly List<SpawnType> spawnTypes = new List<SpawnType>();   // roster rows with a valid prefab; [0] = fallback

    private Health playerHealth;
    private PlayerStats stats;
    private bool playerDead = false;
    private bool anyError = false;

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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void Start()
    {
        if (roster == null)
        {
            Debug.LogError("EnemyRosterSO is not assigned in the inspector.");
            anyError = true;
        }
        else
        {
            BuildSpawnTypes();
        }
        if (config == null)
        {
            Debug.LogError("WaveConfigSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Resume from the wave a previous run set (1 by default). Clamp so a stale value can't start below 1.
        CurrentWave = Mathf.Max(1, RunState.StartingWave);

        // Stop spawning once the player dies.
        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        // Golden Goblin is entirely Reincarnate-upgrade-granted, so the bases start at 0 (no chance, no bonus)
        // until a node raises them. Optional: the game still works with no PlayerStats, golden goblins just
        // never spawn.
        stats = player != null ? player.Stats : null;
        if (stats != null)
        {
            stats.SetBase(StatType.GoldenGoblinChance, 0f);
            stats.SetBase(StatType.GoldenGoblinGoldBonusPercent, 0f);
            // Multiplier stat (base 1.0): consumed by CoinDropper, registered here alongside the
            // other enemy-economy bases (same split as GoldenGoblinGoldBonusPercent above).
            stats.SetBase(StatType.GoldDropMultiplier, 1f);
        }

        StartCoroutine(RunWaves());
    }

    /// <summary>
    ///     Pairs each roster row with its inspector-mapped prefab. Rows without a prefab (or with an
    ///     invalid one) are skipped with a warning, so designers can author CSV rows ahead of the
    ///     prefab arriving. No fallback row surviving is a hard error — there'd be nothing to spawn.
    /// </summary>
    private void BuildSpawnTypes()
    {
        foreach (EnemyDefinition def in roster.Enemies)
        {
            GameObject prefab = FindPrefab(def.id);
            if (prefab == null)
            {
                Debug.LogWarning($"Enemy roster row '{def.id}' has no prefab entry on the WaveSpawner; that type won't spawn.");
                continue;
            }
            if (prefab.GetComponent<Health>() == null)
            {
                Debug.LogError($"Enemy prefab for '{def.id}' has no Health component; wave clearing is tracked via Health.OnDied. Skipping that type.");
                continue;
            }
            spawnTypes.Add(new SpawnType { def = def, prefab = prefab });
        }

        if (spawnTypes.Count == 0)
        {
            Debug.LogError("No spawnable enemy types: the roster is empty or no row has a valid prefab entry.");
            anyError = true;
        }
    }

    private GameObject FindPrefab(string id)
    {
        if (enemyPrefabs == null)
        {
            return null;
        }
        foreach (EnemyPrefabEntry entry in enemyPrefabs)
        {
            if (entry != null && entry.id == id)
            {
                return entry.prefab;
            }
        }
        return null;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        // RunWaves / SpawnLoop both watch playerDead and exit on their own.
    }

    private IEnumerator RunWaves()
    {
        // Let every listener (WaveUI, …) finish subscribing in its own Start before the first event fires,
        // since script execution order between this and the UI isn't guaranteed.
        yield return null;

        while (!playerDead)
        {
            // Remember the wave we're on so a death mid-wave can restart from it.
            RunState.StartingWave = CurrentWave;

            yield return StartCoroutine(Countdown());
            if (playerDead)
            {
                yield break;
            }

            BeginWave();

            // Wait until every goblin this wave has been killed.
            while (killedThisWave < waveGoblinTotal && !playerDead)
            {
                yield return null;
            }
            if (playerDead)
            {
                yield break;
            }

            WaveCleared?.Invoke(CurrentWave);
            CurrentWave++;
        }
    }

    private IEnumerator Countdown()
    {
        for (int remaining = config.timeBetweenWaves; remaining > 0 && !playerDead; remaining--)
        {
            CountdownTick?.Invoke(remaining);
            yield return new WaitForSeconds(1f);
        }
    }

    private void BeginWave()
    {
        waveGoblinTotal = config.GoblinsForWave(CurrentWave);
        killedThisWave = 0;
        aliveCount = 0;
        remainingToSpawn = waveGoblinTotal;
        foreach (SpawnType type in spawnTypes)
        {
            type.spawnedThisWave = 0;
        }

        WaveStarted?.Invoke(CurrentWave);

        StartCoroutine(SpawnLoop());
    }

    /// <summary>
    ///     Trickles enemies in over the wave: spawns one whenever there's room under the concurrent cap and
    ///     enemies are still owed, waiting <see cref="WaveConfigSO.spawnInterval" /> between spawns. Because an
    ///     enemy death frees a slot, this same loop spawns the replacements until the wave total is met.
    /// </summary>
    private IEnumerator SpawnLoop()
    {
        while (remainingToSpawn > 0 && !playerDead)
        {
            if (aliveCount < config.maxConcurrent)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(config.spawnInterval);
            }
            else
            {
                yield return null;
            }
        }
    }

    /// <summary>
    ///     Picks the type for one spawn slot. First pass: any non-fallback type still owed its per-wave
    ///     minSpawn guarantee (unlocked and under its own concurrent cap) spawns immediately, in CSV
    ///     order. Second pass: the remaining rows are checked in CSV order, and the first to win its
    ///     spawnChance roll is chosen. When none wins (or none is eligible), the fallback (first) row
    ///     spawns.
    /// </summary>
    private SpawnType SelectSpawnType()
    {
        for (int i = 1; i < spawnTypes.Count; i++)
        {
            SpawnType type = spawnTypes[i];
            if (IsEligible(type) && type.spawnedThisWave < type.def.minSpawn)
            {
                return type;
            }
        }

        for (int i = 1; i < spawnTypes.Count; i++)
        {
            SpawnType type = spawnTypes[i];
            if (IsEligible(type) && UnityEngine.Random.value < type.def.spawnChance)
            {
                return type;
            }
        }
        return spawnTypes[0];
    }

    private bool IsEligible(SpawnType type)
    {
        return CurrentWave >= type.def.unlockWave
            && (type.def.maxConcurrent <= 0 || type.alive < type.def.maxConcurrent);
    }

    private void SpawnEnemy()
    {
        remainingToSpawn--;
        aliveCount++;

        SpawnType type = SelectSpawnType();
        type.spawnedThisWave++;
        Vector3 position = ResolveSpawnPosition();
        GameObject enemy = Instantiate(type.prefab, position, Quaternion.identity);

        // CSV overrides go on before the instance's Start runs (the MarkGolden timing trick), so each
        // component sees its override when it initializes.
        ApplyDefinition(enemy, type.def);

        // Rolled before Start runs on the enemy, so GoldenGoblin.Start sees the flag and applies its visual.
        float goldenChance = stats != null ? stats.GetValue(StatType.GoldenGoblinChance) : 0f;
        if (goldenChance > 0f && UnityEngine.Random.value < goldenChance)
        {
            enemy.GetComponent<GoldenGoblin>()?.MarkGolden();
        }

        Health health = enemy.GetComponent<Health>();
        if (health == null)
        {
            // Validated in Start, but guard anyway: count it dead so the wave can't stall.
            HandleEnemyDied();
            return;
        }
        type.alive++;

        // Self-unsubscribing handler: each enemy reports its own death exactly once, and Health stays
        // unaware of the spawner.
        aliveEnemies.Add(health);
        Action handler = null;
        handler = () =>
        {
            health.OnDied -= handler;
            aliveEnemies.Remove(health);
            type.alive--;
            HandleEnemyDied();
        };
        health.OnDied += handler;
    }

    /// <summary>Applies a roster row's stat overrides to a freshly spawned instance. Blank CSV cells
    /// leave the prefab's own ScriptableObject values in effect; the shared SOs are never mutated.</summary>
    private static void ApplyDefinition(GameObject enemy, EnemyDefinition def)
    {
        if (def.health.HasValue)
        {
            enemy.GetComponent<Health>()?.SetMaxHealth(def.health.Value);
        }
        if (def.damage.HasValue)
        {
            enemy.GetComponent<AIAttack>()?.SetDamage(def.damage.Value);
        }
        if (def.minGold.HasValue)
        {
            enemy.GetComponent<CoinDropper>()?.SetCoinDrop(def.minGold.Value, def.maxGold.Value);
        }
        if (def.speed.HasValue)
        {
            enemy.GetComponent<AIMovement>()?.SetSpeed(def.speed.Value);
        }
        if (!Mathf.Approximately(def.scale, 1f))
        {
            enemy.transform.localScale *= def.scale;
            // Agent dimensions don't follow transform scale, so scale them to match the visual.
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius *= def.scale;
                agent.height *= def.scale;
            }
        }
    }

    /// <summary>
    ///     Dev-console cheat: instantly clears the wave in progress. Enemies not yet spawned are
    ///     cancelled, and every live enemy is killed through the normal <see cref="Health" /> damage
    ///     flow so all death listeners (spawner accounting, coin drops, kill stats, corpse handling)
    ///     stay consistent. A no-op during the intermission countdown.
    /// </summary>
    public void DebugAdvanceWave()
    {
        if (anyError || playerDead)
        {
            return;
        }

        // Cancel enemies that haven't spawned yet; SpawnLoop exits on its own.
        waveGoblinTotal -= remainingToSpawn;
        remainingToSpawn = 0;

        // Copy first: each death handler removes its enemy from the set as it runs.
        foreach (Health enemy in new List<Health>(aliveEnemies))
        {
            enemy.ReceiveDamage(new Damage { value = 999999f, type = DamageType.blunt });
        }
        // killedThisWave now equals waveGoblinTotal, so RunWaves clears the wave on its next frame.
    }

    private void HandleEnemyDied()
    {
        aliveCount--;
        killedThisWave++;
        // SpawnLoop sees the freed slot and spawns a replacement if any enemies are still owed; RunWaves
        // sees killedThisWave reach the total and clears the wave.
    }

    private Vector3 ResolveSpawnPosition()
    {
        Vector3 candidate;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            candidate = point != null ? point.position : transform.position;
        }
        else
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
            candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
        }

        // Snap onto the NavMesh so spawned goblins can immediately pathfind.
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return candidate;
    }
}
