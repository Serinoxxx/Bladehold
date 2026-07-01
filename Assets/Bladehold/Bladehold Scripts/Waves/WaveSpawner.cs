using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Drives wave-based goblin spawning. Each wave has a total number of goblins to kill (growing with
///     the wave number, per <see cref="WaveConfigSO" />); at most <see cref="WaveConfigSO.maxConcurrent" />
///     are alive at once, and as they die replacements trickle in until the wave total has been killed.
///     Clearing a wave starts an intermission countdown, then the next, larger wave.
///
///     A scene singleton like <see cref="GameStats" /> so the <see cref="DeathScreen" /> can read the
///     current wave. It tracks goblin deaths through each spawned goblin's <see cref="Health.OnDied" />
///     (goblins become corpses on death rather than being destroyed, so death — not destruction — is the
///     signal), and stops spawning when the player dies. UI reacts via the events below; the spawner stays
///     unaware of what listens.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance;

    [Header("What to spawn")]
    [Tooltip("The goblin enemy prefab. Must have a Health component (death is tracked via Health.OnDied).")]
    [SerializeField] private GameObject goblinPrefab;
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

    private int waveGoblinTotal;   // goblins that must die to clear the current wave
    private int killedThisWave;    // goblins killed so far this wave
    private int remainingToSpawn;  // goblins not yet spawned this wave
    private int aliveCount;        // goblins currently alive

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
        if (goblinPrefab == null)
        {
            Debug.LogError("Goblin prefab is not assigned in the inspector.");
            anyError = true;
        }
        else if (goblinPrefab.GetComponent<Health>() == null)
        {
            Debug.LogError("Goblin prefab has no Health component; wave clearing is tracked via Health.OnDied.");
            anyError = true;
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
        }

        StartCoroutine(RunWaves());
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

        WaveStarted?.Invoke(CurrentWave);

        StartCoroutine(SpawnLoop());
    }

    /// <summary>
    ///     Trickles goblins in over the wave: spawns one whenever there's room under the concurrent cap and
    ///     goblins are still owed, waiting <see cref="WaveConfigSO.spawnInterval" /> between spawns. Because a
    ///     goblin death frees a slot, this same loop spawns the replacements until the wave total is met.
    /// </summary>
    private IEnumerator SpawnLoop()
    {
        while (remainingToSpawn > 0 && !playerDead)
        {
            if (aliveCount < config.maxConcurrent)
            {
                SpawnGoblin();
                yield return new WaitForSeconds(config.spawnInterval);
            }
            else
            {
                yield return null;
            }
        }
    }

    private void SpawnGoblin()
    {
        remainingToSpawn--;
        aliveCount++;

        Vector3 position = ResolveSpawnPosition();
        GameObject goblin = Instantiate(goblinPrefab, position, Quaternion.identity);

        // Rolled before Start runs on the goblin, so GoldenGoblin.Start sees the flag and applies its visual.
        float goldenChance = stats != null ? stats.GetValue(StatType.GoldenGoblinChance) : 0f;
        if (goldenChance > 0f && UnityEngine.Random.value < goldenChance)
        {
            goblin.GetComponent<GoldenGoblin>()?.MarkGolden();
        }

        Health health = goblin.GetComponent<Health>();
        if (health == null)
        {
            // Validated in Start, but guard anyway: count it dead so the wave can't stall.
            HandleGoblinDied();
            return;
        }

        // Self-unsubscribing handler: each goblin reports its own death exactly once, and Health stays
        // unaware of the spawner.
        Action handler = null;
        handler = () =>
        {
            health.OnDied -= handler;
            HandleGoblinDied();
        };
        health.OnDied += handler;
    }

    private void HandleGoblinDied()
    {
        aliveCount--;
        killedThisWave++;
        // SpawnLoop sees the freed slot and spawns a replacement if any goblins are still owed; RunWaves
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
