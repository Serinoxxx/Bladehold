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
///     type still under its per-wave budget (<c>minSpawn</c>, ramping +1 per wave since unlock and capped
///     by <c>maxConcurrent</c> — see <see cref="PerWaveBudget" />), then rolls the non-fallback types in
///     CSV order — the first type that is unlocked (<c>unlockWave</c>), under its own concurrent cap
///     (<c>maxConcurrent</c>) and per-wave budget, and wins its <c>spawnChance</c> roll (a percent:
///     10 = 10%) is spawned; otherwise the fallback (first) row spawns. Spawn positions keep at least
///     <see cref="minPlayerDistance" /> from the player. The row's stat overrides (health/damage/gold/speed/
///     scale) are applied to the instance right after Instantiate, before its Start runs, so the shared
///     ScriptableObjects are never mutated.
///
///     A scene singleton like <see cref="GameStats" /> so the <see cref="DeathScreen" /> can read the
///     current wave. It tracks enemy deaths through each spawned enemy's <see cref="Health.OnDied" />
///     (enemies become corpses on death rather than being destroyed, so death — not destruction — is the
///     signal), and stops spawning when the player dies. UI reacts via the events below; the spawner stays
///     unaware of what listens.
/// </summary>
/// <summary>The player's between-wave choice, awaited by <see cref="WaveSpawner" /> after each clear.</summary>
public enum IntermissionChoice
{
    Pending,
    Recover,
    HoldTheLine,
}

public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance;

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
    [Tooltip("The shared id → prefab map asset (also used by the EnemyZoo gallery). Roster rows without a mapping there are skipped (with a warning) until one is added.")]
    [SerializeField] private EnemyPrefabMapSO prefabMap;
    [SerializeField] private WaveConfigSO config;

    [Header("Where to spawn")]
    [Tooltip("Spawn points. Goblins spawn at a random one each time. If empty, they spawn around this object within Spawn Radius.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Fallback spawn radius around this object, used only when no spawn points are assigned.")]
    [SerializeField] private float spawnRadius = 8f;
    [Tooltip("Spawn positions are snapped to the nearest NavMesh point within this distance, so goblins land on walkable ground.")]
    [SerializeField] private float navMeshSampleDistance = 3f;
    [Tooltip("Enemies never spawn closer to the player than this. Too-close candidates are re-rolled a few times; if every roll fails, the farthest candidate is used.")]
    [SerializeField] private float minPlayerDistance = 8f;

    /// <summary>Seconds remaining in the pre-wave countdown, fired once per second during the intermission.</summary>
    public event Action<int> CountdownTick;

    /// <summary>Raised when a wave begins, carrying the (1-based) wave number.</summary>
    public event Action<int> WaveStarted;

    /// <summary>Raised when every goblin in a wave has been killed, carrying the cleared wave number.</summary>
    public event Action<int> WaveCleared;

    /// <summary>
    ///     Raised after a wave clears and the between-wave choice opens (Recover vs Hold the Line),
    ///     carrying the wave that just cleared. A UI (<see cref="WaveIntermissionUI" />) subscribes,
    ///     shows the stats screen, and calls <see cref="ChooseRecover" />/<see cref="ChooseHoldTheLine" />.
    ///     With no subscriber the spawner proceeds straight to the next countdown, so the game stays
    ///     playable when the intermission UI isn't wired.
    /// </summary>
    public event Action<int> IntermissionStarted;

    /// <summary>The wave currently in progress (or about to start), 1-based.</summary>
    public int CurrentWave { get; private set; }

    private int waveGoblinTotal;   // enemies that must die to clear the current wave
    private int killedThisWave;    // enemies killed so far this wave
    private int remainingToSpawn;  // enemies not yet spawned this wave
    private int aliveCount;        // enemies currently alive
    private bool waveInProgress;   // true between BeginWave and the wave clearing (gates the dev cheats)
    private readonly HashSet<Health> aliveEnemies = new HashSet<Health>(); // so DebugWipeWave can kill them
    private readonly List<SpawnType> spawnTypes = new List<SpawnType>();   // roster rows with a valid prefab; [0] = fallback

    private Health playerHealth;
    private PlayerStats stats;
    private bool runOver = false;   // the run has ended — the player died or a gate fell
    private bool anyError = false;
    private int? nextWaveOverride;  // dev-console override applied when the current wave clears
    private bool spawningPaused;    // dev-console: pauses SpawnLoop's automatic trickle-spawn
    private IntermissionChoice pendingChoice; // the between-wave choice being awaited (see WaitForPlayerChoice)
    private bool skipNextCountdown; // set by a Hold-the-Line choice so the next wave begins immediately

    /// <summary>The wave that will begin next: the dev-console override if one is set, otherwise the
    /// natural successor mid-wave, or the wave the intermission is counting down to.</summary>
    public int NextWave => waveInProgress ? nextWaveOverride ?? CurrentWave + 1 : CurrentWave;

    /// <summary>True while <see cref="DebugSetSpawningPaused" /> has paused automatic spawning.</summary>
    public bool IsSpawningPaused => spawningPaused;

    /// <summary>Ids/display-names of every spawnable enemy type (roster rows with a valid prefab
    /// mapping — see <see cref="BuildSpawnTypes" />), for dev-console tooling.</summary>
    public IReadOnlyList<EnemyDefinition> DebugSpawnableTypes
    {
        get
        {
            var defs = new List<EnemyDefinition>(spawnTypes.Count);
            foreach (SpawnType type in spawnTypes)
            {
                defs.Add(type.def);
            }
            return defs;
        }
    }

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
        Gate.OnAnyGateDestroyed -= HandleGateDestroyed;
    }

    private void Start()
    {
        if (roster == null)
        {
            Debug.LogError("EnemyRosterSO is not assigned in the inspector.");
            anyError = true;
        }
        else if (prefabMap == null)
        {
            Debug.LogError("EnemyPrefabMapSO is not assigned in the inspector.");
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

        // Stop spawning once the run ends — player death, or any gate falling (gate defense).
        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
        Gate.OnAnyGateDestroyed += HandleGateDestroyed;

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
            // Impulse Goblin is gold-tree-granted: base 0 = never spawns until the 'Impulse' node.
            stats.SetBase(StatType.ImpulseGoblinChance, 0f);
        }

        StartCoroutine(RunWaves());
    }

    /// <summary>
    ///     Pairs each roster row with its prefab from the shared map asset. Rows without a prefab (or
    ///     with an invalid one) are skipped with a warning, so designers can author CSV rows ahead of
    ///     the prefab arriving. No fallback row surviving is a hard error — there'd be nothing to spawn.
    /// </summary>
    private void BuildSpawnTypes()
    {
        foreach (EnemyDefinition def in roster.Enemies)
        {
            GameObject prefab = prefabMap.FindPrefab(def.id);
            if (prefab == null)
            {
                Debug.LogWarning($"Enemy roster row '{def.id}' has no entry in the enemy prefab map; that type won't spawn.");
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

    private void HandlePlayerDied()
    {
        runOver = true;
        // RunWaves / SpawnLoop both watch runOver and exit on their own.
    }

    private void HandleGateDestroyed(Gate gate)
    {
        runOver = true;
    }

    private IEnumerator RunWaves()
    {
        // Let every listener (WaveUI, …) finish subscribing in its own Start before the first event fires,
        // since script execution order between this and the UI isn't guaranteed.
        yield return null;

        while (!runOver)
        {
            // Remember the wave we're on so a death mid-wave can restart from it.
            RunState.StartingWave = CurrentWave;

            // "Recover and Upgrade" skips the pre-wave countdown — the player spent the (frozen)
            // intermission upgrading. "Hold the Line" keeps it as a loot window on the cleared field.
            if (!skipNextCountdown)
            {
                yield return StartCoroutine(Countdown());
            }
            skipNextCountdown = false;
            if (runOver)
            {
                yield break;
            }

            BeginWave();

            // Wait until every goblin this wave has been killed (DebugWipeWave kills them all at once,
            // so it clears the wave through this same accounting).
            while (killedThisWave < waveGoblinTotal && !runOver)
            {
                yield return null;
            }
            if (runOver)
            {
                yield break;
            }

            waveInProgress = false;
            int clearedWave = CurrentWave;
            WaveCleared?.Invoke(clearedWave);
            CurrentWave = nextWaveOverride ?? CurrentWave + 1;
            nextWaveOverride = null;

            // Open the between-wave choice (stats screen + Recover/Hold the Line). Parks the loop until
            // the UI answers; a no-op when no intermission UI is listening.
            yield return StartCoroutine(WaitForPlayerChoice(clearedWave));
            if (runOver)
            {
                yield break;
            }
        }
    }

    /// <summary>
    ///     After a wave clears, opens the between-wave choice: fires <see cref="IntermissionStarted" />
    ///     and waits for the UI to call <see cref="ChooseRecover" /> or <see cref="ChooseHoldTheLine" />.
    ///     Recover sets <see cref="skipNextCountdown" /> so the next wave begins immediately (the player
    ///     upgraded during the frozen intermission); Hold the Line keeps the countdown as a loot window.
    ///     With no <see cref="IntermissionStarted" /> subscriber (no UI wired) it returns at once, so the
    ///     loop runs its normal countdown next iteration — the pre-intermission behaviour.
    /// </summary>
    private IEnumerator WaitForPlayerChoice(int clearedWave)
    {
        if (IntermissionStarted == null)
        {
            yield break;
        }

        pendingChoice = IntermissionChoice.Pending;
        IntermissionStarted.Invoke(clearedWave);

        while (pendingChoice == IntermissionChoice.Pending && !runOver)
        {
            yield return null;
        }

        if (pendingChoice == IntermissionChoice.Recover)
        {
            skipNextCountdown = true;
        }
    }

    /// <summary>UI entry point: recover and upgrade — the next wave begins immediately (the player
    /// upgraded during the frozen intermission, so there's no loot window to earn).</summary>
    public void ChooseRecover()
    {
        if (pendingChoice == IntermissionChoice.Pending)
        {
            pendingChoice = IntermissionChoice.Recover;
        }
    }

    /// <summary>UI entry point: hold the line — the next wave runs its normal pre-wave countdown, which
    /// doubles as a window to loot the cleared field before the assault resumes.</summary>
    public void ChooseHoldTheLine()
    {
        if (pendingChoice == IntermissionChoice.Pending)
        {
            pendingChoice = IntermissionChoice.HoldTheLine;
        }
    }

    private IEnumerator Countdown()
    {
        for (int remaining = config.timeBetweenWaves; remaining > 0 && !runOver; remaining--)
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
        waveInProgress = true;

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
        while (remainingToSpawn > 0 && !runOver)
        {
            if (!spawningPaused && aliveCount < config.maxConcurrent)
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
    ///     Picks the type for one spawn slot. First pass: any non-fallback type still under its per-wave
    ///     budget (unlocked and under its own concurrent cap) spawns immediately, in CSV order. Second
    ///     pass: the remaining rows are checked in CSV order, and the first to win its spawnChance roll
    ///     is chosen. When none wins (or none is eligible), the fallback (first) row spawns.
    /// </summary>
    private SpawnType SelectSpawnType()
    {
        for (int i = 1; i < spawnTypes.Count; i++)
        {
            SpawnType type = spawnTypes[i];
            if (IsEligible(type) && type.spawnedThisWave < PerWaveBudget(type))
            {
                return type;
            }
        }

        for (int i = 1; i < spawnTypes.Count; i++)
        {
            SpawnType type = spawnTypes[i];
            if (IsEligible(type)
                && type.spawnedThisWave < PerWaveBudget(type)
                && UnityEngine.Random.value < type.def.spawnChance)
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

    /// <summary>
    ///     How many of this type may spawn this wave. Types with a minSpawn ramp up: minSpawn on their
    ///     unlock wave, one more each wave after, capped at maxConcurrent when that is set (e.g. brutes
    ///     with minSpawn 1 / maxConcurrent 3 go 1, 2, 3, 3, ... per wave). The budget is both a guarantee
    ///     (first pass fills it) and a cap (chance rolls can't exceed it). Types without a minSpawn are
    ///     chance-only and per-wave-unlimited, as before.
    /// </summary>
    private int PerWaveBudget(SpawnType type)
    {
        if (type.def.minSpawn <= 0)
        {
            return int.MaxValue;
        }
        int budget = type.def.minSpawn + Mathf.Max(0, CurrentWave - type.def.unlockWave);
        return type.def.maxConcurrent > 0 ? Mathf.Min(budget, type.def.maxConcurrent) : budget;
    }

    /// <summary>Spawns one enemy, either picked normally (<see cref="SelectSpawnType" />) or, when
    /// <paramref name="forcedType" /> is given (the dev-console spawn-specific-type cheat), that exact type.</summary>
    private void SpawnEnemy(SpawnType forcedType = null)
    {
        remainingToSpawn--;
        aliveCount++;

        SpawnType type = forcedType ?? SelectSpawnType();
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

        // Independent of the golden roll — a goblin can be both golden and impulse (the impulse aura
        // wins the visual; see GoldenGoblin.ApplyGoldenVisual).
        float impulseChance = stats != null ? stats.GetValue(StatType.ImpulseGoblinChance) : 0f;
        if (impulseChance > 0f && UnityEngine.Random.value < impulseChance)
        {
            enemy.GetComponent<ImpulseGoblin>()?.MarkImpulse();
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
    /// leave the prefab's own ScriptableObject values in effect; the shared SOs are never mutated.
    /// Public so config/test harnesses (e.g. <c>EnemyZoo</c>) can spawn roster-faithful instances
    /// through the same single source of truth.</summary>
    public static void ApplyDefinition(GameObject enemy, EnemyDefinition def)
    {
        if (def.health.HasValue)
        {
            enemy.GetComponent<Health>()?.SetMaxHealth(def.health.Value);
        }
        if (def.damage.HasValue)
        {
            enemy.GetComponent<AIAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<LightningBallAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<HomingOrbAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<RadialBurstAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<LightningStormAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<TrollSlamAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<BomberAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<MountedKnightBrain>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<HookProjectileAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<WhirlwindAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<SlayerDashAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<LeapSlamAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<PinballCharge>()?.SetDamage(def.damage.Value);
        }
        if (def.minGold.HasValue)
        {
            enemy.GetComponent<CoinDropper>()?.SetCoinDrop(def.minGold.Value, def.maxGold.Value);
        }
        if (def.speed.HasValue)
        {
            enemy.GetComponent<AIMovement>()?.SetSpeed(def.speed.Value);
        }
        if (def.impulseResistance.HasValue)
        {
            enemy.GetComponent<ImpulseReceiver>()?.SetResistance(def.impulseResistance.Value);
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
    ///     Dev-console cheat: instantly clears the wave in progress (see
    ///     <see cref="ClearCurrentWaveInstantly" />), sending the run into the next intermission.
    ///     A no-op during the intermission — there's nothing to wipe.
    /// </summary>
    public void DebugWipeWave()
    {
        if (anyError || runOver || !waveInProgress)
        {
            return;
        }
        ClearCurrentWaveInstantly();
    }

    /// <summary>
    ///     Dev-console cheat: sets the wave that begins next (clamped to 1). Mid-wave it's stored and
    ///     applied when the current wave clears; during the intermission it retargets the wave the
    ///     countdown is about to start.
    /// </summary>
    public void DebugSetNextWave(int wave)
    {
        if (anyError || runOver)
        {
            return;
        }
        wave = Mathf.Max(1, wave);
        if (waveInProgress)
        {
            nextWaveOverride = wave;
        }
        else
        {
            CurrentWave = wave;
            // Keep the death-restart wave in sync — RunWaves already stamped the old value this iteration.
            RunState.StartingWave = wave;
        }
    }

    /// <summary>
    ///     Dev-console cheat: instantly spawns one enemy of the given roster id (see
    ///     <see cref="DebugSpawnableTypes" />) at a random spawn point, bypassing normal type selection
    ///     (<see cref="SelectSpawnType" />) and the concurrent-spawn cap — it always spawns immediately.
    ///     Grows the wave total by one so kill/wave-clear accounting stays consistent, the same trick
    ///     <see cref="DebugSpawnBurst" /> uses. A no-op outside an active wave (nothing to grow) or for
    ///     an unknown id.
    /// </summary>
    public void DebugSpawnEnemyType(string id)
    {
        if (anyError || runOver || !waveInProgress || string.IsNullOrEmpty(id))
        {
            return;
        }

        SpawnType type = null;
        foreach (SpawnType candidate in spawnTypes)
        {
            if (candidate.def.id == id)
            {
                type = candidate;
                break;
            }
        }
        if (type == null)
        {
            return;
        }

        waveGoblinTotal += 1;
        remainingToSpawn += 1;
        SpawnEnemy(type);
    }

    /// <summary>
    ///     Dev-console cheat: pauses/resumes <see cref="SpawnLoop" />'s automatic trickle-spawn, without
    ///     touching the countdown or wave-clear accounting. Lets a wave be parked mid-spawn while enemies
    ///     are placed manually via <see cref="DebugSpawnEnemyType" />.
    /// </summary>
    public void DebugSetSpawningPaused(bool paused)
    {
        spawningPaused = paused;
    }

    /// <summary>
    ///     Instantly clears the wave in progress. Enemies not yet spawned are cancelled, and every live
    ///     enemy is killed through the normal <see cref="Health" /> damage flow so all death listeners
    ///     (spawner accounting, coin drops, kill stats, corpse handling) stay consistent.
    /// </summary>
    private void ClearCurrentWaveInstantly()
    {
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

    /// <summary>
    ///     Dev-console stress test: spawns <paramref name="count" /> extra enemies into the current
    ///     wave, ignoring the concurrent cap and spawn pacing (sliced across frames to avoid a hitch).
    ///     The wave total grows to match, so the wave still clears through the normal kill accounting,
    ///     and <see cref="SpawnLoop" /> simply idles until deaths bring aliveCount back under the cap.
    ///     A no-op during the intermission — a burst there would be orphaned by the next
    ///     <see cref="BeginWave" />'s counter reset.
    /// </summary>
    public void DebugSpawnBurst(int count)
    {
        if (anyError || runOver || !waveInProgress || count <= 0)
        {
            return;
        }

        waveGoblinTotal += count;
        remainingToSpawn += count;
        StartCoroutine(SpawnBurst(count));
    }

    private IEnumerator SpawnBurst(int count)
    {
        const int spawnsPerFrame = 25;
        // remainingToSpawn can hit zero mid-burst if DebugWipeWave cancels the wave under us.
        for (int i = 0; i < count && !runOver && remainingToSpawn > 0; i++)
        {
            SpawnEnemy();
            if ((i + 1) % spawnsPerFrame == 0)
            {
                yield return null;
            }
        }
    }

    private void HandleEnemyDied()
    {
        aliveCount--;
        killedThisWave++;
        // SpawnLoop sees the freed slot and spawns a replacement if any enemies are still owed; RunWaves
        // sees killedThisWave reach the total and clears the wave.
    }

    /// <summary>
    ///     Adopts an enemy spawned by something other than this spawner (the Fort Golem's
    ///     <see cref="MinionSpawner" />) into the current wave's accounting: the wave total and alive
    ///     set both grow by one, so the wave clears only once the extra enemy is dead too.
    ///     <c>remainingToSpawn</c> is deliberately untouched — external enemies never route through
    ///     <c>SpawnEnemy</c> (the debug-cheat precedent), so the trickle-spawner's budget is not
    ///     theirs to consume. Returns false (and adopts nothing) outside a live wave — external
    ///     enemies still work standalone, since kill credit/coins/corpses are all
    ///     <see cref="Health" />-event-driven.
    /// </summary>
    public bool RegisterExternalEnemy(GameObject enemy)
    {
        if (anyError || runOver || !waveInProgress || enemy == null)
        {
            return false;
        }

        Health health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead)
        {
            return false;
        }

        waveGoblinTotal++;
        aliveCount++;

        // The same self-unsubscribing death handler every spawned enemy gets.
        aliveEnemies.Add(health);
        Action handler = null;
        handler = () =>
        {
            health.OnDied -= handler;
            aliveEnemies.Remove(health);
            HandleEnemyDied();
        };
        health.OnDied += handler;
        return true;
    }

    /// <summary>
    ///     Picks a spawn position at least <see cref="minPlayerDistance" /> from the player, re-rolling a
    ///     handful of candidates if needed. If every candidate is too close (small arena, unlucky rolls),
    ///     the farthest one is used rather than stalling the spawn.
    /// </summary>
    private Vector3 ResolveSpawnPosition()
    {
        const int attempts = 8;
        Vector3 playerPos = Player.Instance != null ? Player.Instance.transform.position : transform.position;
        bool checkDistance = Player.Instance != null && minPlayerDistance > 0f;

        Vector3 best = transform.position;
        float bestDistance = -1f;
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = RollSpawnCandidate();

            // Snap onto the NavMesh so spawned goblins can immediately pathfind.
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                candidate = hit.position;
            }

            if (!checkDistance)
            {
                return candidate;
            }

            float distance = Vector3.Distance(candidate, playerPos);
            if (distance >= minPlayerDistance)
            {
                return candidate;
            }
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
        return best;
    }

    private Vector3 RollSpawnCandidate()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            return point != null ? point.position : transform.position;
        }
        Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(offset.x, 0f, offset.y);
    }
}
