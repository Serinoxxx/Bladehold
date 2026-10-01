using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using TMPro;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
///     Master controller for a combat sector's 5-wave loop (plan 15): wave 1 is a fixed Hold the Gate,
///     waves 2-4 are picked from a 3-card wave draft (objective + stance + skulls + clan modifier +
///     reward bundle), wave 5 is the fixed Captain Assault. Between waves: reward popup → wave draft →
///     prep (build towers, objective preview) → hold Ready → 3-2-1 → wave. When the objective resolves
///     the stragglers rout, the card reward is granted on success, and clearing wave 5 wins the sector.
/// </summary>
public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    public const string CaptainObjectiveId = "defeat_captain";

    [Header("Configurations")]
    [Tooltip("Round pacing config asset containing round rosters, 20 max concurrent limit, 3s indicators, and drop weights.")]
    [SerializeField] private RoundPacingConfigSO pacingConfig;

    [SerializeField] private bool enableGameLoopManageStateDebug = false;

    [Header("Ready")]
    [Tooltip("Seconds the Start Wave input ([T] / D-pad Down) must be held to leave prep.")]
    [SerializeField] private float readyHoldSeconds = 1f;

    [Header("Captain Settings")]
    [Tooltip("Captain Fraglob prefab. If null, an error is logged and Captain Kombusta spawns instead.")]
    [SerializeField] private GameObject captainPrefab;
    [Tooltip("Optional dedicated prefab for Captain Kombusta. If null, spawner will attempt to spawn 'captain_kombusta'.")]
    [SerializeField] private GameObject captainKombustaPrefab;
    [SerializeField] private Transform captainSpawnPoint;

    [Header("Cinematic Intermission")]
    [SerializeField] private CinemachineCamera intermissionVirtualCamera;
    [SerializeField] private GameObject intermissionStatsPanel;

    [Header("UI Dependencies")]
    [SerializeField] private SurvivorsSpawner spawner;
    [SerializeField] private SurvivorsObjectiveManager objectiveManager;
    [SerializeField] private Transform bossSpawnPoint;

    [Header("UI References (Optional / Fallback)")]
    [SerializeField] private TMP_Text waveAnnouncementText;
    [SerializeField] private TMP_Text intermissionTimerText;
    [SerializeField] private TMP_Text rewardNotificationText;
    [SerializeField] private GameObject intermissionBanner;
    [SerializeField] private GameObject victoryScreen;

    private int killsThisWave = 0;
    private int targetKillsThisWave = 15;
    private ISurvivorsObjective currentObjective;
    private bool isWaveActive = false;
    private bool isIntermission = false;
    private bool isPrep = false;
    private bool isChoosingCard = false;
    private bool isAwaitingReady = false;
    private bool isResolving = false;
    private bool isRouting = false;
    private readonly List<Health> routStragglers = new List<Health>();
    private float readyHoldTimer = 0f;
    private float resolveWatchdog = 0f;
    private int upcomingWave = 1;
    private string lastObjectiveId = "";
    private float cardChoiceOpenedTime;
    private GameObject activeCaptain;
    private InputReader inputReader;
    private System.Random cardRng;
    private WaveChoiceConfigSO waveChoiceConfig;

    [Header("Resource Reward Feedback (Auto-Wired)")]
    public DamageNumbersPro.DamageNumber goldPopupPrefab;
    public DamageNumbersPro.DamageNumber metalPopupPrefab;
    public DamageNumbersPro.DamageNumber bloodPopupPrefab;
    [Tooltip("Played above the player when a wave card's reward pays out (coin sound, UI track).")]
    public MMF_Player rewardFeedback;

    /// <summary>The card the current (or upcoming, during prep) wave is played with. Null outside a card flow.</summary>
    public WaveCard CurrentWaveCard { get; private set; }

    /// <summary>The clan modifier of the current card, or None.</summary>
    public BannerBuffType CurrentWaveBuff => CurrentWaveCard != null ? CurrentWaveCard.BuffType : BannerBuffType.None;

    public int CurrentWave => RunSession.CurrentWave;
    public int CurrentRound => RunSession.CurrentRound;
    public int UpcomingWave => upcomingWave;
    public int KillsThisWave => killsThisWave;
    public int TargetKillsThisWave => targetKillsThisWave;
    public SurvivorsObjectiveManager ObjectiveManager => objectiveManager;
    public ISurvivorsObjective CurrentObjective => objectiveManager != null ? objectiveManager.CurrentObjective : currentObjective;
    public bool IsWaveActive => isWaveActive;
    public bool IsIntermission => isIntermission;
    /// <summary>True from the end of a wave until Ready is held: the only window where towers and gate repair work.</summary>
    public bool IsPrepPhase => isPrep;
    /// <summary>True during prep once a card is picked and the game is waiting for the Ready hold.</summary>
    public bool IsAwaitingReady => isPrep && isAwaitingReady && !isChoosingCard;

    /// <summary>True while the wave choice cards are open.</summary>
    public bool IsChoosingCard => isChoosingCard;
    public float ReadyHoldProgress => readyHoldSeconds > 0f ? Mathf.Clamp01(readyHoldTimer / readyHoldSeconds) : 0f;
    public bool IsRouting => isRouting;
    /// <summary>The enemies still fleeing during the rout (dead ones included until it ends). Empty otherwise.</summary>
    public IReadOnlyList<Health> RoutStragglers => routStragglers;
    /// <summary>Seconds left to hunt the stragglers down before they escape; 0 when not routing.</summary>
    public float RoutSecondsLeft { get; private set; }
    /// <summary>3, 2, 1 during the pre-wave countdown; 0 otherwise.</summary>
    public int CountdownSeconds { get; private set; }
    /// <summary>The upcoming wave's card objective title during prep, or blank.</summary>
    public string UpcomingObjectiveTitle => isPrep && CurrentWaveCard != null && CurrentWaveCard.objective != null ? CurrentWaveCard.objective.TitleText : "";
    /// <summary>One-line HUD status for the between-wave phases (prompt, countdown, rout); empty during a wave.</summary>
    public string StatusText { get; private set; } = "";

    /// <summary>What this sector paid and who fell, for the victory screen. Reset on scene load.</summary>
    public SectorSummary Summary { get; } = new SectorSummary();

    public event Action<int> OnWaveStarted;
    public event Action<int, string> OnWaveCleared;
    public event Action OnVictory;
    public event Action<Health> OnEnemyKilledEvent;
    /// <summary>Fired when a wave's objective resolves (after the rout): wave, success, the card it was played with (may be null).</summary>
    public event Action<int, bool, WaveCard> OnWaveResolved;
    /// <summary>Fired when a card reward is paid: the card and the one-line description shown in the popup.</summary>
    public event Action<WaveCard, string> OnWaveRewardGranted;
    /// <summary>Fired when the player picks from a wave draft: offered cards, the pick, seconds taken to decide.</summary>
    public event Action<IReadOnlyList<WaveCard>, WaveCard, float> OnWaveCardPicked;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }

        if (spawner == null) spawner = FindAnyObjectByType<SurvivorsSpawner>();
        if (objectiveManager == null) objectiveManager = SurvivorsObjectiveManager.Instance ?? FindAnyObjectByType<SurvivorsObjectiveManager>();
    }

    private void Start()
    {
        if (rewardFeedback == null) Debug.LogError("[GameLoopManager] rewardFeedback is not assigned.", this);

        waveChoiceConfig = WaveChoiceConfigSO.Load();
        cardRng = new System.Random(Environment.TickCount);

        if (Player.Instance != null)
        {
            inputReader = Player.Instance.GetComponentInChildren<InputReader>();
            if (Player.Instance.Health != null) Player.Instance.Health.OnDied += HandlePlayerDied;
        }
        if (inputReader == null) Debug.LogError("[GameLoopManager] No InputReader on the player; the Ready hold can't be read.", this);

        // The castle scenes' gates were authored with an Interactable from the old rest-gate flow;
        // nothing handles it now, so keep it from showing a dead [E] prompt.
        foreach (Gate gate in Gate.All)
        {
            Interactable gateInteractable = gate != null ? gate.GetComponent<Interactable>() : null;
            if (gateInteractable != null)
            {
                gateInteractable.CanInteract = false;
            }
        }

        if (objectiveManager == null) objectiveManager = SurvivorsObjectiveManager.Instance ?? FindAnyObjectByType<SurvivorsObjectiveManager>();
        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted += HandleObjectiveCompleted;
            objectiveManager.OnObjectiveFailed += HandleObjectiveFailed;
        }

        int initialWave = RunSession.CurrentWave > 0 ? RunSession.CurrentWave : 1;
        RunSession.CurrentWave = initialWave;

        CampaignNodeSO node = CurrentCampaignNode;
        if (node != null)
        {
            Debug.Log($"[GameLoopManager] Campaign Active: Sector '{node.nodeTitle}' (threat {SectorThreat.Current}, clan {(node.clanBuff != null ? node.clanBuff.clanName : "any")}).");
        }

        BeginIntermission(initialWave);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied -= HandlePlayerDied;
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
            objectiveManager.OnObjectiveFailed -= HandleObjectiveFailed;
        }
    }

    private static CampaignNodeSO CurrentCampaignNode =>
        CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive ? CampaignManager.Instance.CurrentNode : null;

    private int TotalWaves => pacingConfig != null ? pacingConfig.wavesPerRound : 5;

    public void OnEnemyKilled(Health enemyHealth)
    {
        if (!isWaveActive) return;

        killsThisWave++;

        // Roll gold drop into in-run purse
        RunSession.AddInRunGold(UnityEngine.Random.Range(2, 6));

        // Defense Supply roll
        int supplyDrop = UnityEngine.Random.value < 0.35f ? UnityEngine.Random.Range(1, 4) : 0;
        if (enemyHealth != null && enemyHealth.MaxHealth >= 150f)
        {
            supplyDrop += UnityEngine.Random.Range(4, 8); // Brute / elite bonus supply
        }
        if (supplyDrop > 0)
        {
            RunSession.AddInRunSupply(supplyDrop);
        }

        OnEnemyKilledEvent?.Invoke(enemyHealth);
    }

    // ---- Intermission: reward → wave draft → prep → Ready ----

    private void BeginIntermission(int nextWave)
    {
        upcomingWave = nextWave;
        if (nextWave > TotalWaves) return;

        isIntermission = true;
        isPrep = true;
        isAwaitingReady = false;
        readyHoldTimer = 0f;
        CurrentWaveCard = null;
        if (intermissionBanner != null) intermissionBanner.SetActive(true);
        if (objectiveManager != null) objectiveManager.SetPhase(SurvivorsObjectivePhase.Intermission, 0f);

        WaveCardRollContext ctx = BuildRollContext(nextWave);
        if (waveChoiceConfig == null)
        {
            EnterAwaitReady();
            return;
        }

        bool drafting = pacingConfig == null || pacingConfig.draftWaveCards;
        if (drafting && waveChoiceConfig.IsDraftedWave(nextWave) && SurvivorsCardSelectUI.Instance != null)
        {
            List<WaveCard> cards = WaveCardGenerator.Roll(waveChoiceConfig, ctx, cardRng);
            if (cards.Count > 0)
            {
                isChoosingCard = true;
                cardChoiceOpenedTime = Time.unscaledTime;
                SetStatus(Loc.Get("wave.status.choose", "Choose your next battle"));
                SurvivorsCardSelectUI.Instance.OpenWaveChoice(cards, picked => HandleWaveCardPicked(cards, picked));
                return;
            }
            Debug.LogError($"[GameLoopManager] Wave draft for wave {nextWave} rolled no cards; playing a fixed wave instead.", this);
        }

        FixedWaveDefinition fixedWave = drafting && nextWave >= waveChoiceConfig.finalWaveNumber ? waveChoiceConfig.finalWave : waveChoiceConfig.firstWave;
        CurrentWaveCard = WaveCardGenerator.BuildFixed(waveChoiceConfig, fixedWave, ctx, cardRng);
        if (CurrentWaveCard != null && pacingConfig != null && !pacingConfig.rollWaveClans)
        {
            CurrentWaveCard.clan = null;
        }
        EnterAwaitReady();
    }

    private WaveCardRollContext BuildRollContext(int wave)
    {
        CampaignNodeSO node = CurrentCampaignNode;
        return new WaveCardRollContext
        {
            wave = wave,
            threat = SectorThreat.Current,
            lastObjectiveId = lastObjectiveId,
            nodeClan = node != null ? node.clanBuff : null,
            captainName = node != null ? node.captainName : null,
            isDemo = DemoConfigSO.IsDemo
        };
    }

    private void HandleWaveCardPicked(IReadOnlyList<WaveCard> offered, WaveCard picked)
    {
        isChoosingCard = false;
        CurrentWaveCard = picked;
        float seconds = Time.unscaledTime - cardChoiceOpenedTime;
        OnWaveCardPicked?.Invoke(offered, picked, seconds);
        if (picked != null)
        {
            Debug.Log($"[GameLoopManager] Wave {upcomingWave} card: {picked.ObjectiveId} ({picked.stance}, {picked.skulls} skulls, {picked.ClanName}) → {picked.gold}g {picked.supply}s {picked.bonusType} x{picked.bonusAmount}");
        }
        EnterAwaitReady();
    }

    private void EnterAwaitReady()
    {
        isPrep = true;
        isAwaitingReady = true;
        readyHoldTimer = 0f;
        string title = CurrentWaveCard != null && CurrentWaveCard.objective != null ? CurrentWaveCard.objective.TitleText : "";
        if (waveAnnouncementText != null)
        {
            waveAnnouncementText.text = string.IsNullOrEmpty(title)
                ? $"WAVE {upcomingWave} / {TotalWaves}"
                : $"WAVE {upcomingWave} / {TotalWaves}: {title.ToUpperInvariant()}";
        }
        SetStatus(ReadyPrompt());
    }

    private string ReadyPrompt() => Loc.Get("wave.status.ready", "Build your defences, then start the wave");

    private void Update()
    {
        if (IsAwaitingReady)
        {
            TickReadyHold();
        }

        if (isResolving)
        {
            // Backstop: if the rout ever stalls (stuck agents), finish it off.
            resolveWatchdog += Time.deltaTime;
            if (resolveWatchdog >= 45f && isWaveActive)
            {
                resolveWatchdog = 0f;
                Debug.LogWarning("[GameLoopManager] Rout backstop expired! Striking stragglers with lightning.");
                if (spawner != null) spawner.StrikeAllAliveWithLightning(9999f);
            }
        }
    }

    private void TickReadyHold()
    {
        bool held = inputReader != null && inputReader.StartWaveHeld;
        if (!held || Time.timeScale <= 0f)
        {
            if (readyHoldTimer > 0f)
            {
                readyHoldTimer = 0f;
                SetStatus(ReadyPrompt());
            }
            return;
        }

        readyHoldTimer += Time.unscaledDeltaTime;
        if (readyHoldTimer >= readyHoldSeconds)
        {
            readyHoldTimer = 0f;
            isPrep = false;
            isAwaitingReady = false;
            StartCoroutine(StartNextWaveAfterDelay());
        }
    }

    /// <summary>Debug / DevConsole: skips the Ready hold.</summary>
    public void DebugPressReady()
    {
        if (!IsAwaitingReady) return;
        readyHoldTimer = readyHoldSeconds;
        isPrep = false;
        isAwaitingReady = false;
        StartCoroutine(StartNextWaveAfterDelay());
    }

    /// <summary>
    ///     DevConsole: replaces the upcoming wave's card (e.g. a forced <c>wavecard &lt;id&gt; &lt;skulls&gt;</c>).
    ///     Only during prep; the card applies when Ready is held.
    /// </summary>
    public bool DebugSetUpcomingCard(WaveCard card)
    {
        if (!IsAwaitingReady || card == null) return false;
        CurrentWaveCard = card;
        EnterAwaitReady();
        return true;
    }

    /// <summary>DevConsole: rerolls and reopens the wave draft for the upcoming wave (prep only).</summary>
    public bool DebugRerollWaveDraft()
    {
        if (!IsAwaitingReady || waveChoiceConfig == null || SurvivorsCardSelectUI.Instance == null) return false;
        List<WaveCard> cards = WaveCardGenerator.Roll(waveChoiceConfig, BuildRollContext(Mathf.Clamp(upcomingWave, waveChoiceConfig.firstDraftedWave, waveChoiceConfig.finalWaveNumber - 1)), cardRng);
        if (cards.Count == 0) return false;
        isChoosingCard = true;
        cardChoiceOpenedTime = Time.unscaledTime;
        SurvivorsCardSelectUI.Instance.OpenWaveChoice(cards, picked => HandleWaveCardPicked(cards, picked));
        return true;
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        for (int sec = 3; sec > 0; sec--)
        {
            CountdownSeconds = sec;
            string countdown = Loc.Get("wave.status.countdown", "Wave starts in {0}...").Replace("{0}", sec.ToString(CultureInfo.InvariantCulture));
            SetStatus(countdown);
            if (intermissionTimerText != null) intermissionTimerText.text = countdown;
            yield return new WaitForSeconds(1.0f);
        }

        CountdownSeconds = 0;
        isIntermission = false;
        if (intermissionBanner != null) intermissionBanner.SetActive(false);

        StartWave(upcomingWave);
    }

    private void SetStatus(string text)
    {
        StatusText = text ?? "";
        if (intermissionTimerText != null && !isWaveActive) intermissionTimerText.text = StatusText;
    }

    // ---- The wave ----

    public void StartWave(int waveNumber)
    {
        RunSession.CurrentWave = waveNumber;
        upcomingWave = waveNumber;

        WaveCard card = CurrentWaveCard;
        int baseQuota = pacingConfig != null
            ? pacingConfig.GetKillQuota(waveNumber, SectorThreat.Current)
            : 15 + (waveNumber - 1) * 5;
        float quotaMultiplier = card != null ? card.killQuotaMultiplier : 1f;
        targetKillsThisWave = Mathf.Max(1, Mathf.RoundToInt(baseQuota * quotaMultiplier));
        killsThisWave = 0;
        isWaveActive = true;
        isIntermission = false;
        isPrep = false;
        isResolving = false;
        isAwaitingReady = false;
        isRouting = false;
        resolveWatchdog = 0f;
        activeCaptain = null;
        SetStatus("");

        if (intermissionBanner != null) intermissionBanner.SetActive(false);

        StartWaveObjective(waveNumber, card);

        if (currentObjective is GoblinRushObjective)
        {
            targetKillsThisWave = 999999;
        }
        else if (currentObjective is KillEnemiesObjective holdTheGate)
        {
            // Hold the Gate's kill target is the wave quota, so it scales with threat and skulls.
            holdTheGate.SetRequiredKills(targetKillsThisWave);
        }

        if (spawner != null)
        {
            spawner.StartWave(waveNumber, targetKillsThisWave);
            // Golden Goblin: the objective is the only thing on the field.
            if (currentObjective is ISuppressRegularSpawns) spawner.StopSpawning();
        }

        // A 3-skull card brings a captain (the Captain Assault objective spawns its own).
        bool objectiveSpawnsCaptain = currentObjective != null && currentObjective.ObjectiveId == CaptainObjectiveId;
        if (!objectiveSpawnsCaptain && card != null && card.hasCaptain)
        {
            activeCaptain = SpawnCaptainForWave(card.CaptainTier, CaptainNameFor(card));
        }

        if (waveAnnouncementText != null)
        {
            waveAnnouncementText.text = $"WAVE {waveNumber} / {TotalWaves}";
        }

        OnWaveStarted?.Invoke(waveNumber);
        Debug.Log($"[GameLoopManager] Started Wave {waveNumber} / {TotalWaves}. Objective {currentObjective?.ObjectiveId ?? "none"}, target kills {targetKillsThisWave}, skulls {card?.skulls ?? 1}.");
    }

    private void StartWaveObjective(int waveNumber, WaveCard card)
    {
        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindAnyObjectByType<SurvivorsObjectiveManager>();
            if (objectiveManager != null)
            {
                objectiveManager.OnObjectiveCompleted += HandleObjectiveCompleted;
                objectiveManager.OnObjectiveFailed += HandleObjectiveFailed;
            }
        }

        if (objectiveManager == null)
        {
            currentObjective = null;
            return;
        }

        bool started = card != null && objectiveManager.StartObjective(card.ObjectiveId, waveNumber);
        if (!started)
        {
            if (card != null)
            {
                Debug.LogError($"[GameLoopManager] Wave card objective '{card.ObjectiveId}' isn't in this scene; falling back to a random objective.", this);
            }
            objectiveManager.StartWaveObjective(waveNumber);

            // Captain Assault is missing from the scene: keep wave 5's captain the old way.
            if (card != null && card.ObjectiveId == CaptainObjectiveId)
            {
                activeCaptain = SpawnCaptainForWave(CaptainTierForThreat(SectorThreat.Current), CaptainNameFor(card));
            }
        }

        currentObjective = objectiveManager.CurrentObjective;
        if (currentObjective != null) lastObjectiveId = currentObjective.ObjectiveId;
        Debug.Log($"[GameLoopManager] Wave {waveNumber} Objective: {currentObjective?.Title ?? "None"}");
    }

    /// <summary>The captain a wave card named (it may have rolled a wandering captain), else the campaign node's.</summary>
    public string CaptainNameFor(WaveCard card)
    {
        if (card != null && !string.IsNullOrEmpty(card.captainName)) return card.captainName;
        return CurrentCampaignNode != null ? CurrentCampaignNode.captainName : null;
    }

    /// <summary>The captain tier the final wave uses at a sector threat (mirrors DefeatCaptainObjective).</summary>
    public static BannerDifficultyTier CaptainTierForThreat(int threat) => DefeatCaptainObjective.TierForThreat(threat);

    /// <summary>
    ///     Applies the current card's enemy modifiers to a freshly spawned wave enemy: the skull HP
    ///     multiplier, then the clan buff at the card's scaled magnitude. Called by the spawner.
    /// </summary>
    public void ApplyWaveModifiers(GameObject enemy)
    {
        WaveCard card = CurrentWaveCard;
        if (enemy == null || card == null || !isWaveActive) return;

        Health health = enemy.GetComponent<Health>();
        if (health != null && !Mathf.Approximately(card.enemyHealthMultiplier, 1f))
        {
            health.SetMaxHealth(health.MaxHealth * card.enemyHealthMultiplier);
        }

        if (card.HasClan)
        {
            EnemyBuffController buff = enemy.GetComponent<EnemyBuffController>() ?? enemy.AddComponent<EnemyBuffController>();
            buff.Initialize(card.BuffType, card.modifierMagnitude);
        }
    }

    private void HandleObjectiveCompleted(ISurvivorsObjective obj)
    {
        if (obj is KillRemainingEnemiesObjective) return; // legacy cleanup objective; the rout replaces it
        if (!isWaveActive || isResolving) return;
        Debug.Log($"[GameLoopManager] Objective Completed: {obj?.Title}");
        ResolveWave(true);
    }

    private void HandleObjectiveFailed(ISurvivorsObjective obj)
    {
        if (!isWaveActive || isResolving) return;
        Debug.Log($"[GameLoopManager] Objective Failed: {obj?.Title}. No card reward.");
        ResolveWave(false);
    }

    /// <summary>Debug method: completes the active objective, which resolves the wave.</summary>
    public void DebugCompleteObjective()
    {
        if (objectiveManager != null && objectiveManager.CurrentObjective != null && !objectiveManager.CurrentObjective.IsComplete)
        {
            objectiveManager.DebugCompleteCurrentObjective();
        }
        if (isWaveActive && !isResolving)
        {
            ResolveWave(true);
        }
    }

    // ---- Resolution: rout → clear → reward → next intermission ----

    private void ResolveWave(bool success)
    {
        isResolving = true;
        resolveWatchdog = 0f;
        if (spawner != null) spawner.StopSpawning();
        StartCoroutine(RoutRoutine(success));
    }

    private IEnumerator RoutRoutine(bool success)
    {
        List<Health> stragglers = new List<Health>();
        if (spawner != null) spawner.GetAliveEnemies(stragglers);
        if (activeCaptain != null)
        {
            Health captainHealth = activeCaptain.GetComponent<Health>();
            if (captainHealth != null && !captainHealth.IsDead && !stragglers.Contains(captainHealth)) stragglers.Add(captainHealth);
        }
        stragglers.RemoveAll(h => h == null || h.IsDead);

        if (stragglers.Count > 0)
        {
            isRouting = true;
            routStragglers.Clear();
            routStragglers.AddRange(stragglers);
            float duration = waveChoiceConfig != null ? waveChoiceConfig.routDurationSeconds : 20f;
            foreach (Health h in stragglers)
            {
                Vector3 fleeTo = spawner != null ? spawner.NearestSpawnPoint(h.transform.position) : h.transform.position - h.transform.forward * 30f;
                EnemyRout.Begin(h.gameObject, fleeTo);
            }

            float t = 0f;
            int shownSeconds = -1;
            while (t < duration && stragglers.Exists(h => h != null && !h.IsDead))
            {
                RoutSecondsLeft = duration - t;
                int seconds = Mathf.CeilToInt(RoutSecondsLeft);
                if (seconds != shownSeconds)
                {
                    shownSeconds = seconds;
                    SetStatus(string.Format(Loc.Get("wave.status.rout_hunt", "The goblins are fleeing! Hunt them down: {0}s"), seconds));
                }
                t += Time.deltaTime;
                yield return null;
            }
            RoutSecondsLeft = 0f;
            routStragglers.Clear();

            if (spawner != null) spawner.DespawnAllAliveEnemies();
            foreach (Health h in stragglers)
            {
                if (h != null && !h.IsDead) Destroy(h.gameObject);
            }
            isRouting = false;
        }

        ClearActiveWave(success);
    }

    private void ClearActiveWave(bool success)
    {
        if (!isWaveActive) return;
        isWaveActive = false;
        isResolving = false;
        int clearedWave = CurrentWave;
        WaveCard card = CurrentWaveCard;

        Debug.Log($"[GameLoopManager] Wave {clearedWave} / {TotalWaves} {(success ? "cleared" : "failed")}.");

        if (objectiveManager != null) objectiveManager.StopActiveObjective();

        // Decrement temporary buff durations from rest shop
        RunSession.OnWaveCompleted();

        // Kill gold/supply and the flat wave-clear supply are always kept.
        RunSession.AddInRunSupply(30);

        // Wave-clear heal perks apply win or lose.
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            if (RunSession.HasMetaPerk("regeneration")) Player.Instance.Health.Heal(RunSession.GetMetaPerkValue("regeneration", 5f));
            if (RunSession.SpecialHerbsWavesRemaining > 0) Player.Instance.Health.Heal(5f);
        }

        if (success) Summary.wavesWon++; else Summary.wavesFailed++;
        LastWaveSucceeded = success;
        LastRewardDescription = "";

        if (clearedWave >= TotalWaves)
        {
            if (spawner != null)
            {
                spawner.StopSpawning();
                spawner.DespawnAllAliveEnemies();
            }

            OnWaveResolved?.Invoke(clearedWave, success, card);
            OnWaveCleared?.Invoke(clearedWave, "Sector Defended");
            TriggerVictory();
            return;
        }

        isPrep = true; // building opens as soon as the field is clear
        OnWaveResolved?.Invoke(clearedWave, success, card);

        if (success && card != null)
        {
            GrantCardReward(card, () =>
            {
                OnWaveCleared?.Invoke(clearedWave, "Wave Cleared");
                StartCoroutine(NextIntermissionAfterPopup(clearedWave + 1));
            });
        }
        else
        {
            OnWaveCleared?.Invoke(clearedWave, success ? "Wave Cleared" : "Objective Failed");
            StartCoroutine(NextIntermissionAfterPopup(clearedWave + 1));
        }
    }

    /// <summary>Whether the last resolved wave's objective succeeded (telemetry reads this on OnWaveCleared).</summary>
    public bool LastWaveSucceeded { get; private set; }

    /// <summary>What the last card reward paid, e.g. "+180 gold, +90 supply, 2 Goblin Blood" (blank on failure).</summary>
    public string LastRewardDescription { get; private set; } = "";

    private IEnumerator NextIntermissionAfterPopup(int nextWave)
    {
        yield return new WaitForSecondsRealtime(waveChoiceConfig != null ? waveChoiceConfig.cardsDelaySeconds : 4f);
        BeginIntermission(nextWave);
    }

    /// <summary>Pays a card's bundle: gold, supply, then the bonus (a draft pick opens the skill-card modal, chained).</summary>
    private void GrantCardReward(WaveCard card, Action onComplete)
    {
        List<string> parts = new List<string>();
        if (card.gold > 0)
        {
            RunSession.AddInRunGold(card.gold);
            parts.Add(Loc.Get("wave.reward.gold", "+{0} gold").Replace("{0}", card.gold.ToString(CultureInfo.InvariantCulture)));
        }
        if (card.supply > 0)
        {
            RunSession.AddInRunSupply(card.supply);
            parts.Add(Loc.Get("wave.reward.supply", "+{0} supply").Replace("{0}", card.supply.ToString(CultureInfo.InvariantCulture)));
        }

        int draftPicks = 0;
        switch (card.bonusType)
        {
            case WaveBonusType.DraftPick:
                draftPicks = Mathf.Max(1, card.bonusAmount);
                break;
            case WaveBonusType.GoblinBlood:
                RunSession.AddGoblinBlood(card.bonusAmount);
                break;
            case WaveBonusType.OrcishMetal:
                RunSession.AddOrcishMetal(card.bonusAmount);
                break;
            case WaveBonusType.TrollHeart:
                float bonusHp = card.bonusAmount;
                RunSession.PlayerBonusMaxHealth += bonusHp;
                if (Player.Instance != null && Player.Instance.Health != null)
                {
                    float current = Player.Instance.Health.CurrentHealth;
                    Player.Instance.Health.SetMaxHealth(Player.Instance.Health.MaxHealth + bonusHp);
                    Player.Instance.Health.SetCurrentHealth(current + bonusHp);
                }
                break;
        }
        if (card.bonusType != WaveBonusType.None) parts.Add(card.BonusText);

        Summary.RecordReward(card, draftPicks);
        string desc = string.Join(", ", parts);
        LastRewardDescription = desc;
        if (rewardNotificationText != null)
        {
            rewardNotificationText.text = $"{Loc.Get("wave.reward.header", "Wave Reward")}: {desc}";
        }
        PlayRewardFeedback(card);
        OnWaveRewardGranted?.Invoke(card, desc);
        Debug.Log($"[GameLoopManager] Wave card reward: {desc}");

        OpenDraftPicks(draftPicks, onComplete);
    }

    private void OpenDraftPicks(int remaining, Action onComplete)
    {
        if (remaining <= 0 || SurvivorsCardSelectUI.Instance == null)
        {
            onComplete?.Invoke();
            return;
        }
        SurvivorsCardSelectUI.Instance.OpenDraft(null, () => OpenDraftPicks(remaining - 1, onComplete));
    }

    private void PlayRewardFeedback(WaveCard card)
    {
        if (Player.Instance == null) return;
        Vector3 pos = Player.Instance.transform.position + new Vector3(0, 2f, 0);

        if (rewardFeedback != null)
        {
            rewardFeedback.PlayFeedbacks(pos);
        }

        if (goldPopupPrefab != null && card.gold > 0) goldPopupPrefab.Spawn(pos, card.gold);
        if (card.bonusType == WaveBonusType.OrcishMetal && metalPopupPrefab != null) metalPopupPrefab.Spawn(pos + Vector3.up * 0.5f, card.bonusAmount);
        if (card.bonusType == WaveBonusType.GoblinBlood && bloodPopupPrefab != null) bloodPopupPrefab.Spawn(pos + Vector3.up * 0.5f, card.bonusAmount);
    }

    /// <summary>
    ///     Spawns a Clan Captain, plays the cinematic EnemyIntroUI with difficulty skulls, and initializes its
    ///     <see cref="ICaptain" /> controller. <paramref name="preferredCaptainName" /> is matched through
    ///     <see cref="CaptainRegistry" />; blank or unknown picks a random spawnable captain.
    /// </summary>
    public GameObject SpawnCaptainForWave(BannerDifficultyTier tier, string preferredCaptainName = null)
    {
        Vector3 spawnPos = captainSpawnPoint != null ? captainSpawnPoint.position :
                           (bossSpawnPoint != null ? bossSpawnPoint.position : (transform.position + new Vector3(0f, 0f, 25f)));
        Quaternion spawnRot = captainSpawnPoint != null ? captainSpawnPoint.rotation :
                             (bossSpawnPoint != null ? bossSpawnPoint.rotation : Quaternion.identity);

        CaptainRegistry.CaptainEntry entry = CaptainRegistry.Find(preferredCaptainName);
        if (entry == null)
        {
            List<CaptainRegistry.CaptainEntry> spawnable = new List<CaptainRegistry.CaptainEntry>();
            foreach (CaptainRegistry.CaptainEntry candidate in CaptainRegistry.All)
            {
                if (CanSpawnCaptain(candidate)) spawnable.Add(candidate);
            }
            entry = spawnable.Count > 0 ? spawnable[UnityEngine.Random.Range(0, spawnable.Count)] : CaptainRegistry.Kombusta;
        }

        if (!CanSpawnCaptain(entry))
        {
            Debug.LogError($"[GameLoopManager] {entry.displayName} can't spawn (no prefab slot or '{entry.rosterId}' roster row). Spawning Captain Kombusta instead. Needs Lance in the Editor.");
            entry = CaptainRegistry.Kombusta;
        }

        GameObject captainGo = SpawnCaptainObject(entry, spawnPos, spawnRot);
        if (captainGo == null)
        {
            Debug.LogError($"[GameLoopManager] {entry.displayName} has no prefab (no prefab slot and no '{entry.rosterId}' roster row). No captain spawned.");
            return null;
        }

        string captainName = entry.displayName;
        ICaptain captain = captainGo.GetComponent<ICaptain>();
        if (captain == null)
        {
            Debug.LogError($"[GameLoopManager] Captain prefab '{captainGo.name}' has no ICaptain controller.");
            return captainGo;
        }
        captain.Initialize(tier, captainName);

        Health captainHealth = captainGo.GetComponent<Health>();
        if (captainHealth != null)
        {
            string fallenName = captainName;
            BannerDifficultyTier fallenTier = tier;
            Action onCaptainDied = null;
            onCaptainDied = () =>
            {
                captainHealth.OnDied -= onCaptainDied;
                Summary.captainsDefeated.Add($"{fallenName} ({BannerDifficultyHelper.GetTierName(fallenTier)})");
            };
            captainHealth.OnDied += onCaptainDied;
        }

        // Play the enemy intro announcement with difficulty skulls
        if (EnemyIntroUI.Instance != null)
        {
            string subtitle = $"{BannerDifficultyHelper.GetTierName(tier).ToUpper()} - {BannerDifficultyHelper.GetRewardMultiplier(tier)}X REWARDS";
            EnemyIntroUI.Instance.ShowIntro($"{captainName} has arrived!", (int)tier, subtitle, 3.5f);
        }

        Debug.Log($"[GameLoopManager] Spawned {captainName} at Tier {tier} ({BannerDifficultyHelper.GetRewardMultiplier(tier)}x rewards)!");
        return captainGo;
    }

    private bool CanSpawnCaptain(CaptainRegistry.CaptainEntry entry)
    {
        if (entry == CaptainRegistry.Fraglob) return captainPrefab != null;
        if (entry == CaptainRegistry.Kombusta && captainKombustaPrefab != null) return true;
        return !string.IsNullOrEmpty(entry.rosterId) && spawner != null && spawner.Roster != null && spawner.Roster.Find(entry.rosterId) != null;
    }

    /// <summary>
    ///     Roster captains spawn through the spawner, so the CSV row applies and the rout/cleanup track them
    ///     (they don't count toward the kill quota). Prefab slots are the direct-Instantiate path.
    /// </summary>
    private GameObject SpawnCaptainObject(CaptainRegistry.CaptainEntry entry, Vector3 spawnPos, Quaternion spawnRot)
    {
        if (entry == CaptainRegistry.Fraglob)
        {
            return captainPrefab != null ? Instantiate(captainPrefab, spawnPos, spawnRot) : null;
        }
        if (entry == CaptainRegistry.Kombusta && captainKombustaPrefab != null)
        {
            return Instantiate(captainKombustaPrefab, spawnPos, spawnRot);
        }
        if (string.IsNullOrEmpty(entry.rosterId) || spawner == null || spawner.Roster == null || spawner.Roster.Find(entry.rosterId) == null)
        {
            return null;
        }
        return spawner.DebugSpawnEnemyType(entry.rosterId);
    }

    private void TriggerVictory()
    {
        Debug.Log($"[GameLoopManager] DEFENSE VICTORY! All {TotalWaves} waves cleared!");
        isWaveActive = false;
        isIntermission = false;
        isPrep = false;
        SetStatus("");

        // Preserve player health ratio so it carries over to the next node
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            RunSession.PlayerHealthRatio = Mathf.Clamp01(Player.Instance.Health.CurrentHealth / Player.Instance.Health.MaxHealth);
        }

        // Towers don't follow the player to the next sector: dismantle them for supply.
        int towerRefund = TowerPlotManager.Instance != null ? TowerPlotManager.Instance.DismantleAllForRefund() : 0;
        Summary.towerRefund = towerRefund;

        // Trigger DeathScreen in victory mode (No procedural fallback UI)
        DeathScreen endScreen = DeathScreen.Instance != null ? DeathScreen.Instance : FindAnyObjectByType<DeathScreen>();
        if (endScreen != null)
        {
            endScreen.ShowVictory("VICTORY!", towerRefund);
        }
        else if (victoryScreen != null)
        {
            victoryScreen.SetActive(true);
            CursorLockManager.SetUnlock("VictoryScreen", true);
        }

        OnVictory?.Invoke();
    }

    private void HandlePlayerDied()
    {
        // Second Wind is handled by RunSession's Health.TryPreventDeath hook, before death is final.
        Debug.Log("[GameLoopManager] Player died. DeathScreen handles run conclusion and transition to Meta Area.");
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        if (!enableGameLoopManageStateDebug) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 250), GUI.skin.box);
        GUILayout.Label("<b>GameLoopManager State</b>");
        GUILayout.Label($"Round: {CurrentRound} | Wave: {CurrentWave} | Upcoming: {upcomingWave}");
        GUILayout.Label($"Wave Active: {isWaveActive} | Prep: {isPrep} | Choosing: {isChoosingCard} | Routing: {isRouting}");
        GUILayout.Label($"Kills: {killsThisWave} / {targetKillsThisWave}");
        GUILayout.Label(CurrentWaveCard != null
            ? $"Card: {CurrentWaveCard.ObjectiveId} ({CurrentWaveCard.stance}, {CurrentWaveCard.skulls} skulls, {CurrentWaveCard.ClanName})"
            : "Card: none");
        GUILayout.Label(currentObjective != null ? $"Objective: {currentObjective.Title}" : "Objective: None");

        if (spawner != null)
        {
            GUILayout.Label($"Spawner Active: {spawner.IsSpawningActive}");
            GUILayout.Label($"Enemies Alive: {spawner.AliveCount} / {spawner.MaxConcurrentEnemies}");
            GUILayout.Label($"Remaining to Spawn: {spawner.RemainingToSpawn}");
        }

        GUILayout.EndArea();
    }
}
