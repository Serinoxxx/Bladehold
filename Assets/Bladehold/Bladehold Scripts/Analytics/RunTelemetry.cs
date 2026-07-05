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
///     (<see cref="WaveSpawner" /> wave events, the player's <see cref="Health.OnDamaged" />/<see cref="Health.OnDied" />,
///     the sword <see cref="DamageTrigger.OnHit" />, <see cref="InputReader.onAttackDeactivated" /> for swing
///     counting, and both tree services' <c>OnNodePurchased</c>) and never changes gameplay. Sprint time is
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
///         <item><c>purchase</c> — one per skill purchase (either tree): node id/name in <c>detail</c>,
///         price paid in <c>cost</c>. Death-screen shopping lands after the death row, which is correct —
///         it is part of that run's story.</item>
///         <item><c>death</c> — the fatal wave's partial stats; <c>run_summary</c> — whole-run totals.</item>
///     </list>
/// </summary>
public class RunTelemetry : MonoBehaviour
{
    private const string Header = "event,wave,run_seconds,wave_seconds,kills,gold_earned,damage_taken,hits_taken,damage_dealt,hits_dealt,crits,quick_attacks,charged_attacks,sprint_seconds,cost,detail";

    private static RunTelemetry instance;

    // Bound scene objects (re-bound every scene load; scene reload = new run).
    private WaveSpawner waveSpawner;
    private Health playerHealth;
    private GameStats gameStats;
    private PlayerAttack playerAttack;
    private InputReader inputReader;
    private SamplePlayerAnimationController controller;
    private DamageTrigger swordTrigger;
    private SkillTreeService goldTree;
    private ReincarnateService reincarnateTree;
    private FieldInfo isSprintingField;

    private string filePath;
    private float runStartTime;
    private float waveStartTime;
    private bool playerDead;

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
    private int totalHitsDealt;
    private int totalCrits;
    private int totalQuickAttacks;
    private int totalChargedAttacks;
    private float totalSprintSeconds;

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
        waveSpawner = WaveSpawner.Instance;
        goldTree = SkillTreeService.Instance;
        reincarnateTree = ReincarnateService.Instance;
        playerAttack = player.GetComponentInChildren<PlayerAttack>(true);
        inputReader = player.GetComponentInChildren<InputReader>(true);
        controller = player.GetComponentInChildren<SamplePlayerAnimationController>(true);

        swordTrigger = null;
        foreach (DamageTrigger trigger in player.GetComponentsInChildren<DamageTrigger>(true))
        {
            if (trigger.ReadsPlayerStats)
            {
                swordTrigger = trigger;
                break;
            }
        }

        if (waveSpawner == null) Debug.LogWarning("RunTelemetry: no WaveSpawner; wave rows won't be recorded.");
        if (swordTrigger == null) Debug.LogWarning("RunTelemetry: no player-stats DamageTrigger found under the player; damage dealt won't be recorded.");
        if (inputReader == null) Debug.LogWarning("RunTelemetry: no InputReader found under the player; swing counts won't be recorded.");

        if (waveSpawner != null)
        {
            waveSpawner.WaveStarted += HandleWaveStarted;
            waveSpawner.WaveCleared += HandleWaveCleared;
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
        if (inputReader != null)
        {
            inputReader.onAttackDeactivated += HandleAttackReleased;
        }
        if (goldTree != null)
        {
            goldTree.OnNodePurchased += HandleGoldPurchase;
        }
        if (reincarnateTree != null)
        {
            reincarnateTree.OnNodePurchased += HandleReincarnatePurchase;
        }

        playerDead = false;
        runStartTime = Time.time;
        waveStartTime = Time.time;
        ResetWaveAccumulators();
        totalDamageTaken = 0f; totalHitsTaken = 0;
        totalDamageDealt = 0f; totalHitsDealt = 0; totalCrits = 0;
        totalQuickAttacks = 0; totalChargedAttacks = 0;
        totalSprintSeconds = 0f;

        OpenRunFile();

        // The context row waits a frame so every scene singleton's Start (save load, purchased-node
        // re-apply) has run and the counts are real.
        StartCoroutine(WriteRunStartNextFrame());
    }

    private System.Collections.IEnumerator WriteRunStartNextFrame()
    {
        yield return null;

        Wallet wallet = Player.Instance != null ? Player.Instance.Wallet : null;
        string detail = Invariant($"startWave={RunState.StartingWave};gold={(wallet != null ? wallet.Coins : 0)};goldNodes={CountOwned(goldTree)};reincNodes={CountOwned(reincarnateTree)}");
        AppendRow("run_start", wave: Invariant($"{RunState.StartingWave}"), runSeconds: "0", detail: detail);
    }

    private static int CountOwned(ISkillTreeService service)
    {
        if (service == null || service.Tree == null)
        {
            return 0;
        }
        int owned = 0;
        foreach (SkillNode node in service.Tree.Nodes)
        {
            if (service.IsPurchased(node.id))
            {
                owned++;
            }
        }
        return owned;
    }

    private void Unbind()
    {
        if (waveSpawner != null)
        {
            waveSpawner.WaveStarted -= HandleWaveStarted;
            waveSpawner.WaveCleared -= HandleWaveCleared;
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
        if (inputReader != null)
        {
            inputReader.onAttackDeactivated -= HandleAttackReleased;
        }
        if (goldTree != null)
        {
            goldTree.OnNodePurchased -= HandleGoldPurchase;
        }
        if (reincarnateTree != null)
        {
            reincarnateTree.OnNodePurchased -= HandleReincarnatePurchase;
        }
        waveSpawner = null;
        playerHealth = null;
        gameStats = null;
        playerAttack = null;
        inputReader = null;
        controller = null;
        swordTrigger = null;
        goldTree = null;
        reincarnateTree = null;
    }

    // ---- event handlers ----

    private void HandleWaveStarted(int wave)
    {
        waveStartTime = Time.time;
        ResetWaveAccumulators();
    }

    private void HandleWaveCleared(int wave)
    {
        WriteWaveRow("wave_clear", wave, "");
    }

    private void HandlePlayerDamaged(Damage damage)
    {
        damageTaken += damage.value;
        hitsTaken++;
        totalDamageTaken += damage.value;
        totalHitsTaken++;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;

        int wave = waveSpawner != null ? waveSpawner.CurrentWave : 0;
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
    }

    private void HandleSwordHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        damageDealt += damage.value;
        hitsDealt++;
        totalDamageDealt += damage.value;
        totalHitsDealt++;
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

    private void HandleGoldPurchase(SkillNode node, int price) => WritePurchaseRow("gold", node, price);

    private void HandleReincarnatePurchase(SkillNode node, int price) => WritePurchaseRow("reinc", node, price);

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

    private void WritePurchaseRow(string tree, SkillNode node, int price)
    {
        int wave = waveSpawner != null ? waveSpawner.CurrentWave : 0;
        AppendRow("purchase",
            wave: Invariant($"{wave}"),
            runSeconds: Invariant($"{RunSeconds():F1}"),
            cost: Invariant($"{price}"),
            detail: $"{tree}:{node.id}:{node.displayName}");
    }

    private float RunSeconds() => Time.time - runStartTime;

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
