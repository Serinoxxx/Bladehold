using System;
using System.IO;
using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using static System.FormattableString;

/// <summary>
///     Balance telemetry: writes one CSV per run under <c>persistentDataPath/Telemetry/</c> so easy and
///     sluggish stretches show up in data instead of vibes. Self-bootstrapping (spawned once on play via
///     <see cref="RuntimeInitializeOnLoadMethod" />, surviving scene reloads) — nothing to add to the scene.
///
///     Pure listener, per the Health-is-the-hub convention: it subscribes to existing events
///     (<see cref="GameLoopManager" /> wave events, the player's <see cref="Health.OnDamaged" />/<see cref="Health.OnDied" />,
///     the sword <see cref="DamageTrigger.OnHit" />, <see cref="InputReader.onAttackDeactivated" /> for swing
///     counting) and never changes gameplay. Sprint time is
///     polled from the vendored controller's private <c>_isSprinting</c> field by reflection (the
///     <see cref="PlayerMoveSpeedBinder" /> precedent). Missing pieces log one warning and are skipped, so
///     telemetry can never break the game.
///
///     Row types (superset columns; blank = not applicable):
///     <list type="bullet">
///         <item><c>run_start</c> — starting wave and saved-progress context in <c>detail</c>.</item>
///         <item><c>wave_clear</c> — one per cleared wave: clear time, kills, gold, damage in/out, crits,
///         quick vs charged swings, sprint seconds. Intermission activity (accumulators reset when the
///         next wave starts) is excluded from wave rows but still lands in the run totals.</item>
///         <item><c>death</c> — the fatal wave's partial stats; <c>run_summary</c> — whole-run totals.</item>
///     </list>
/// </summary>
public class RunTelemetry : MonoBehaviour
{
    private const string Header = "event,wave,run_seconds,wave_seconds,kills,gold_earned,damage_taken,hits_taken,damage_dealt,hits_dealt,crits,quick_attacks,charged_attacks,sprint_seconds,cost,detail";

    public struct WaveStats
    {
        public float damageTaken;
        public int hitsTaken;
        public float damageDealt;
        public int hitsDealt;
        public int crits;
        public int quickAttacks;
        public int chargedAttacks;
        public float sprintSeconds;
    }

    private static RunTelemetry instance;
    public static RunTelemetry Instance => instance;

    public event Action<RunTelemetryData> OnRunEnded;

    // Bound scene objects (re-bound every scene load; scene reload = new run).
    private GameLoopManager gameLoop;
    private Health playerHealth;
    private GameStats gameStats;
    private PlayerAttack playerAttack;
    private InputReader inputReader;
    private SamplePlayerAnimationController controller;
    private DamageTrigger swordTrigger;
    private PlayerThrownAxe thrownAxe;
    private PlayerWand wand;
    private FieldInfo isSprintingField;
    
    private PlayerDodge playerDodge;
    private PlayerMount playerMount;

    private string filePath;
    private float runStartTime;
    private float waveStartTime;
    private bool playerDead;
    private string fatalEnemy = "";
    private string gateDestroyerEnemy = "";
    private int gateDestroyedWave = 0;
    private string lastDamagerName = "";

    // Per-wave accumulators, reset on WaveStarted.
    private int killsAtWaveStart;
    private int goldAtWaveStart;
    private float damageTaken;
    private int hitsTaken;
    private float damageDealt;
    private int hitsDealt;
    private int crits;
    private int quickAttacks;
    private int chargedAttacks;
    private float sprintSeconds;

    // Whole-run totals.
    private float totalDamageTaken;
    private int totalHitsTaken;
    private float totalDamageDealt;
    private float totalMeleeDamageDealt;
    private float totalRangedDamageDealt;
    private int totalHitsDealt;
    private int totalCrits;
    private int totalQuickAttacks;
    private int totalChargedAttacks;
    private float totalSprintSeconds;
    private int totalDodges;
    private float totalMountSeconds;
    private int totalChestsDestroyed;

    // Gate repair (plan 15). Repairs happen in the prep phase *before* a wave, so this is not reset on
    // WaveStarted: it accumulates through prep + the wave and is cleared after that wave's wave_clear row,
    // i.e. each wave row counts the supply spent repairing in the prep that led into it.
    private int gateRepairSupplyThisWave;
    private int totalGateRepairSupply;

    /// <summary>Supply spent repairing the gate since the last wave_clear row (the prep leading into the current wave).</summary>
    public int GateRepairSupplyThisWave => gateRepairSupplyThisWave;
    /// <summary>Supply spent repairing the gate this scene run.</summary>
    public int TotalGateRepairSupply => totalGateRepairSupply;

    /// <summary>Called by <see cref="GateRepairStation" /> each time it spends supply on the gate. No-op without telemetry.</summary>
    public static void RecordGateRepair(int supply)
    {
        if (instance == null || supply <= 0) return;
        instance.gateRepairSupplyThisWave += supply;
        instance.totalGateRepairSupply += supply;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }
        GameObject go = new GameObject("RunTelemetry");
        DontDestroyOnLoad(go);
        go.AddComponent<RunTelemetry>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        isSprintingField = typeof(SamplePlayerAnimationController).GetField("_isSprinting", BindingFlags.Instance | BindingFlags.NonPublic);
        if (isSprintingField == null)
        {
            Debug.LogWarning("RunTelemetry could not find the controller's '_isSprinting' field; sprint time won't be tracked.");
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // Bootstrap runs after the initial scene's Awakes, so the scene singletons already exist; scene
        // reloads go through HandleSceneLoaded instead.
        BeginRun();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Unbind();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            BeginRun();
        }
    }

    private void Update()
    {
        if (playerDead || controller == null || isSprintingField == null)
        {
            return;
        }
        if ((bool)isSprintingField.GetValue(controller))
        {
            sprintSeconds += Time.deltaTime;
            totalSprintSeconds += Time.deltaTime;
        }
        if (playerMount != null && playerMount.IsMounted)
        {
            totalMountSeconds += Time.deltaTime;
        }
    }

    // ---- run lifecycle ----

    private void BeginRun()
    {
        Unbind();

        Player player = Player.Instance;
        if (player == null)
        {
            Debug.LogWarning("RunTelemetry: no Player.Instance in this scene; telemetry disabled for this run.");
            return;
        }

        playerHealth = player.Health;
        gameStats = GameStats.Instance;
        gameLoop = GameLoopManager.Instance;
        playerAttack = player.GetComponentInChildren<PlayerAttack>(true);
        inputReader = player.GetComponentInChildren<InputReader>(true);
        controller = player.GetComponentInChildren<SamplePlayerAnimationController>(true);
        playerDodge = player.GetComponentInChildren<PlayerDodge>(true);
        playerMount = player.GetComponentInChildren<PlayerMount>(true);

        swordTrigger = null;
        // Exclude inactive children: with several melee weapons on the rig, an unequipped one still carries
        // a ReadsPlayerStats trigger — bind the live one only. (Still binds "first found" if a third
        // active ReadsPlayerStats trigger ever appears.)
        foreach (DamageTrigger trigger in player.GetComponentsInChildren<DamageTrigger>(false))
        {
            if (trigger.ReadsPlayerStats)
            {
                swordTrigger = trigger;
                break;
            }
        }

        if (gameLoop == null) Debug.LogWarning("RunTelemetry: no GameLoopManager; wave rows won't be recorded.");
        if (swordTrigger == null) Debug.LogWarning("RunTelemetry: no player-stats DamageTrigger found under the player; damage dealt won't be recorded.");
        if (inputReader == null) Debug.LogWarning("RunTelemetry: no InputReader found under the player; swing counts won't be recorded.");

        if (gameLoop != null)
        {
            gameLoop.OnWaveStarted += HandleWaveStarted;
            gameLoop.OnWaveCleared += HandleWaveCleared;
            gameLoop.OnWaveCardPicked += HandleWaveCardPicked;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDamaged += HandlePlayerDamaged;
            playerHealth.OnDied += HandlePlayerDied;
        }
        if (swordTrigger != null)
        {
            swordTrigger.OnHit += HandleSwordHit;
        }
        // The thrown axe feeds the same damage-dealt accumulators (note: bow damage is
        // not telemetered today — the axe is included deliberately, since face-tank balance can't
        // be read without its share of the damage).
        thrownAxe = player.GetComponentInChildren<PlayerThrownAxe>(true);
        if (thrownAxe != null)
        {
            thrownAxe.OnHit += HandleSwordHit;
        }
        // The Mage's wand likewise (its elemental riders/zones/chains are not telemetered — the
        // chain-lightning precedent; direct weapon damage is the balance signal).
        wand = player.GetComponentInChildren<PlayerWand>(true);
        if (wand != null)
        {
            wand.OnHit += HandleSwordHit;
        }
        if (inputReader != null)
        {
            inputReader.onAttackDeactivated += HandleAttackReleased;
        }
        
        if (playerDodge != null) playerDodge.OnDodgeStarted += HandleDodgeStarted;
        Gate.OnAnyGateDestroyed += HandleGateDestroyed;
        Chest.OnAnyChestDestroyed += HandleChestDestroyed;

        playerDead = false;
        runStartTime = Time.time;
        waveStartTime = Time.time;
        ResetWaveAccumulators();
        totalDamageTaken = 0f; totalHitsTaken = 0;
        totalDamageDealt = 0f; totalMeleeDamageDealt = 0f; totalRangedDamageDealt = 0f; totalHitsDealt = 0; totalCrits = 0;
        totalQuickAttacks = 0; totalChargedAttacks = 0;
        totalSprintSeconds = 0f;
        gateRepairSupplyThisWave = 0; totalGateRepairSupply = 0;
        totalDodges = 0; totalMountSeconds = 0f; totalChestsDestroyed = 0;
        fatalEnemy = ""; gateDestroyerEnemy = ""; gateDestroyedWave = 0; lastDamagerName = "";

        OpenRunFile();

        // The context row waits a frame so every scene singleton's Start (save load) has run.
        StartCoroutine(WriteRunStartNextFrame());
    }

    private System.Collections.IEnumerator WriteRunStartNextFrame()
    {
        yield return null;

        string classId = SaveSystem.Load().equippedArmourSet;
        string detail = Invariant($"startWave={RunSession.CurrentWave};class={classId};gold={RunSession.InRunGold};scene={SceneManager.GetActiveScene().name};mode={RunMode}");
        AppendRow("run_start", wave: Invariant($"{RunSession.CurrentWave}"), runSeconds: "0", detail: detail);
    }

    /// <summary>
    ///     tutorial / campaign / standalone (scene played from the Editor). Tells the Valley Stronghold's
    ///     tutorial runs (T3) from its campaign-node runs (plan 21).
    /// </summary>
    private static string RunMode => TutorialRun.Active ? "tutorial" : RunSession.IsCampaignRun ? "campaign" : "standalone";

    private void Unbind()
    {
        if (gameLoop != null)
        {
            gameLoop.OnWaveStarted -= HandleWaveStarted;
            gameLoop.OnWaveCleared -= HandleWaveCleared;
            gameLoop.OnWaveCardPicked -= HandleWaveCardPicked;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDamaged -= HandlePlayerDamaged;
            playerHealth.OnDied -= HandlePlayerDied;
        }
        if (swordTrigger != null)
        {
            swordTrigger.OnHit -= HandleSwordHit;
        }
        if (thrownAxe != null)
        {
            thrownAxe.OnHit -= HandleSwordHit;
        }
        if (wand != null)
        {
            wand.OnHit -= HandleSwordHit;
        }
        if (inputReader != null)
        {
            inputReader.onAttackDeactivated -= HandleAttackReleased;
        }
        
        if (playerDodge != null) playerDodge.OnDodgeStarted -= HandleDodgeStarted;
        Gate.OnAnyGateDestroyed -= HandleGateDestroyed;
        Chest.OnAnyChestDestroyed -= HandleChestDestroyed;
        gameLoop = null;
        playerHealth = null;
        gameStats = null;
        playerAttack = null;
        inputReader = null;
        controller = null;
        swordTrigger = null;
    }

    // ---- event handlers ----

    private void HandleWaveStarted(int wave)
    {
        waveStartTime = Time.time;
        ResetWaveAccumulators();
    }

    private void HandleWaveCleared(int wave, string _)
    {
        WriteWaveRow("wave_clear", wave, WaveOutcomeDetail());
        gateRepairSupplyThisWave = 0;
    }

    // Plan 15: outcome, card and reward of the wave just resolved, plus supply spent repairing the gate before it.
    private string WaveOutcomeDetail()
    {
        if (gameLoop == null) return Invariant($"gate_repair_supply={gateRepairSupplyThisWave}");
        WaveCard card = gameLoop.CurrentWaveCard;
        string cardText = card != null ? Invariant($"{card.ObjectiveId}/{card.stance}/{card.skulls}") : "none";
        string reward = string.IsNullOrEmpty(gameLoop.LastRewardDescription) ? "none" : gameLoop.LastRewardDescription;
        return Invariant($"outcome={(gameLoop.LastWaveSucceeded ? "success" : "fail")}; card={cardText}; reward={reward}; gate_repair_supply={gateRepairSupplyThisWave}");
    }

    // Plan 15: one row per wave draft: the three offers, the pick, decision time, player and gate HP at pick time.
    private void HandleWaveCardPicked(System.Collections.Generic.IReadOnlyList<WaveCard> offered, WaveCard picked, float seconds)
    {
        System.Collections.Generic.List<string> offers = new System.Collections.Generic.List<string>();
        if (offered != null)
        {
            foreach (WaveCard c in offered)
            {
                if (c != null) offers.Add(Invariant($"{c.ObjectiveId}/{c.stance}/{c.skulls}"));
            }
        }
        string pick = picked != null ? Invariant($"{picked.ObjectiveId}/{picked.stance}/{picked.skulls}") : "none";
        float playerPct = playerHealth != null && playerHealth.MaxHealth > 0f ? playerHealth.CurrentHealth / playerHealth.MaxHealth * 100f : -1f;
        float gatePct = RunSession.FortressGateMaxHealth > 0f ? RunSession.FortressGateCurrentHealth / RunSession.FortressGateMaxHealth * 100f : -1f;
        int wave = gameLoop != null ? gameLoop.UpcomingWave : 0;
        AppendRow("wave_choice",
            wave: Invariant($"{wave}"),
            runSeconds: Invariant($"{RunSeconds():F1}"),
            detail: Invariant($"offered={string.Join("|", offers)}; pick={pick}; decide_s={seconds:F1}; player_hp_pct={playerPct:F0}; gate_hp_pct={gatePct:F0}"));
    }

    private void HandlePlayerDamaged(Damage damage)
    {
        damageTaken += damage.value;
        hitsTaken++;
        totalDamageTaken += damage.value;
        totalHitsTaken++;
        
        if (damage.source != null)
        {
            var component = damage.source as Component;
            if (component != null) lastDamagerName = component.gameObject.name.Replace("(Clone)", "").Trim();
        }
    }

    private void HandlePlayerDied()
    {
        Debug.Log($"[RunTelemetry] HandlePlayerDied triggered! Fatal enemy: '{lastDamagerName}'. Invoking OnRunEnded...");
        playerDead = true;
        fatalEnemy = lastDamagerName;

        int wave = gameLoop != null ? gameLoop.CurrentWave : 0;
        WriteWaveRow("death", wave, "died mid-wave");

        int kills = gameStats != null ? gameStats.GoblinsKilled : 0;
        int gold = gameStats != null ? gameStats.GoldEarnedThisRun : 0;
        AppendRow("run_summary",
            wave: Invariant($"{wave}"),
            runSeconds: Invariant($"{RunSeconds():F1}"),
            kills: Invariant($"{kills}"),
            goldEarned: Invariant($"{gold}"),
            damageTakenField: Invariant($"{totalDamageTaken:F1}"),
            hitsTakenField: Invariant($"{totalHitsTaken}"),
            damageDealtField: Invariant($"{totalDamageDealt:F1}"),
            hitsDealtField: Invariant($"{totalHitsDealt}"),
            critsField: Invariant($"{totalCrits}"),
            quick: Invariant($"{totalQuickAttacks}"),
            charged: Invariant($"{totalChargedAttacks}"),
            sprint: Invariant($"{totalSprintSeconds:F1}"),
            detail: "run totals");

        string classId = SaveSystem.Load().equippedArmourSet;

        RunTelemetryData data = new RunTelemetryData
        {
            playtestVersion = Application.version,
            classId = classId,
            runMode = RunMode,
            startingWave = RunSession.CurrentWave,
            maxWaveReached = wave,
            totalRunTimeSeconds = RunSeconds(),
            fatalEnemy = fatalEnemy,
            gateDestroyerEnemy = gateDestroyerEnemy,
            gateDestroyedWave = gateDestroyedWave,
            meleeDamageDealt = totalMeleeDamageDealt,
            rangedDamageDealt = totalRangedDamageDealt,
            timesDodged = totalDodges,
            mountTimeSeconds = totalMountSeconds,
            chestsDestroyed = totalChestsDestroyed
        };
        OnRunEnded?.Invoke(data);
    }

    private void HandleSwordHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        damageDealt += damage.value;
        hitsDealt++;
        totalDamageDealt += damage.value;
        totalHitsDealt++;
        
        if (damage.isProjectile) 
            totalRangedDamageDealt += damage.value;
        else 
            totalMeleeDamageDealt += damage.value;

        if (damage.isCritical)
        {
            crits++;
            totalCrits++;
        }
    }

    private void HandleAttackReleased()
    {
        if (playerDead)
        {
            return;
        }
        // ChargeLevel resets on press and is kept live during the hold, so at release it describes this swing.
        if (playerAttack != null && playerAttack.ChargeLevel >= 1)
        {
            chargedAttacks++;
            totalChargedAttacks++;
        }
        else
        {
            quickAttacks++;
            totalQuickAttacks++;
        }
    }

    private void HandleDodgeStarted() => totalDodges++;
    private void HandleChestDestroyed() => totalChestsDestroyed++;
    private void HandleGateDestroyed(Gate gate)
    {
        gateDestroyedWave = gameLoop != null ? gameLoop.CurrentWave : 0;
        // The gate doesn't have an OnDamaged hook giving the last attacker out of the box, 
        // but we can at least log the wave it died on.
        gateDestroyerEnemy = "Enemy";
        
        // Also trigger the run end if this gate fell ends the run!
        HandlePlayerDied(); // reuse the death logic for telemetry broadcast
    }

    // ---- row writing ----

    private void ResetWaveAccumulators()
    {
        killsAtWaveStart = gameStats != null ? gameStats.GoblinsKilled : 0;
        goldAtWaveStart = gameStats != null ? gameStats.GoldEarnedThisRun : 0;
        damageTaken = 0f; hitsTaken = 0;
        damageDealt = 0f; hitsDealt = 0; crits = 0;
        quickAttacks = 0; chargedAttacks = 0;
        sprintSeconds = 0f;
    }

    private void WriteWaveRow(string eventName, int wave, string detail)
    {
        float waveSeconds = Time.time - waveStartTime;
        int kills = gameStats != null ? gameStats.GoblinsKilled - killsAtWaveStart : 0;
        int gold = gameStats != null ? gameStats.GoldEarnedThisRun - goldAtWaveStart : 0;
        AppendRow(eventName,
            wave: Invariant($"{wave}"),
            runSeconds: Invariant($"{RunSeconds():F1}"),
            waveSeconds: Invariant($"{waveSeconds:F1}"),
            kills: Invariant($"{kills}"),
            goldEarned: Invariant($"{gold}"),
            damageTakenField: Invariant($"{damageTaken:F1}"),
            hitsTakenField: Invariant($"{hitsTaken}"),
            damageDealtField: Invariant($"{damageDealt:F1}"),
            hitsDealtField: Invariant($"{hitsDealt}"),
            critsField: Invariant($"{crits}"),
            quick: Invariant($"{quickAttacks}"),
            charged: Invariant($"{chargedAttacks}"),
            sprint: Invariant($"{sprintSeconds:F1}"),
            detail: detail);
    }

    private float RunSeconds() => Time.time - runStartTime;

    public WaveStats GetCurrentWaveStats()
    {
        return new WaveStats
        {
            damageTaken = damageTaken,
            hitsTaken = hitsTaken,
            damageDealt = damageDealt,
            hitsDealt = hitsDealt,
            crits = crits,
            quickAttacks = quickAttacks,
            chargedAttacks = chargedAttacks,
            sprintSeconds = sprintSeconds
        };
    }

    /// <summary>
    ///     Totals since this scene loaded (one sector), for the end screen. Covers the same damage sources
    ///     as the CSV: melee weapon, thrown axe and wand hits (bow and ultimate/elemental riders aren't counted).
    /// </summary>
    public WaveStats GetRunTotals(out float seconds)
    {
        seconds = RunSeconds();
        return new WaveStats
        {
            damageTaken = totalDamageTaken,
            hitsTaken = totalHitsTaken,
            damageDealt = totalDamageDealt,
            hitsDealt = totalHitsDealt,
            crits = totalCrits,
            quickAttacks = totalQuickAttacks,
            chargedAttacks = totalChargedAttacks,
            sprintSeconds = totalSprintSeconds
        };
    }

    /// <summary>Builds a row with one named argument per header column, so columns can never drift.</summary>
    private void AppendRow(string eventName, string wave = "", string runSeconds = "", string waveSeconds = "",
        string kills = "", string goldEarned = "", string damageTakenField = "", string hitsTakenField = "",
        string damageDealtField = "", string hitsDealtField = "", string critsField = "", string quick = "",
        string charged = "", string sprint = "", string cost = "", string detail = "")
    {
        string[] fields =
        {
            eventName, wave, runSeconds, waveSeconds, kills, goldEarned, damageTakenField, hitsTakenField,
            damageDealtField, hitsDealtField, critsField, quick, charged, sprint, cost, Sanitize(detail),
        };
        Append(string.Join(",", fields));
    }

    /// <summary>Keeps free-text safe inside a comma-separated row.</summary>
    private static string Sanitize(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace(',', ';').Replace('\n', ' ').Replace('\r', ' ');

    private void OpenRunFile()
    {
        string directory = Path.Combine(Application.persistentDataPath, "Telemetry");
        Directory.CreateDirectory(directory);

        string baseName = $"run_{DateTime.Now:yyyyMMdd_HHmmss}";
        filePath = Path.Combine(directory, baseName + ".csv");
        for (int suffix = 2; File.Exists(filePath); suffix++)
        {
            filePath = Path.Combine(directory, $"{baseName}_{suffix}.csv");
        }

        File.WriteAllText(filePath, Header + "\n");
        Debug.Log($"RunTelemetry: logging this run to {filePath}");
    }

    /// <summary>Appends one row immediately, so data survives quitting play mode mid-run.</summary>
    private void Append(string row)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        try
        {
            File.AppendAllText(filePath, row + "\n");
        }
        catch (IOException e)
        {
            Debug.LogWarning($"RunTelemetry: failed to write telemetry row: {e.Message}");
        }
    }
}
