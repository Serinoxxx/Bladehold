using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MoreMountains.Feedbacks;

/// <summary>
///     Fades in a death screen when the run ends — the player dying (the player's
///     <see cref="Health.OnDied" /> via the <see cref="Player" /> singleton) or, in gate defense, any
///     <see cref="Gate" /> falling (<see cref="Gate.OnAnyGateDestroyed" />; time is frozen for that
///     one since the player is still alive behind the screen). Shows goblins killed and gold earned
///     this run (from <see cref="GameStats" />) plus the player's total gold (from
///     <see cref="Wallet" />), and offers two restart options: from wave 1, or from the wave the run
///     ended on (via <see cref="RunState" />). Both reload the scene. When an optional
///     <see cref="FailureBanner" /> is assigned, it plays first with a per-condition failure reason
///     ("The hero has fallen…" / "The gate was destroyed…") and the screen only fades in after it
///     finishes.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DeathScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Optional headline label, set per loss condition (see the two title strings below).")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Loc key of the headline when the player died.")]
    [SerializeField] private string playerDiedTitleKey = "death.player_title";
    [Tooltip("Loc key of the headline when a gate fell (gate defense).")]
    [SerializeField] private string gateFellTitleKey = "death.gate_title";
    [Tooltip("Optional: a failure-reason banner played to completion before this screen fades in. Must live outside this screen's CanvasGroup. Leave unassigned to fade the death screen in immediately, as before.")]
    [SerializeField] private FailureBanner failureBanner;
    [Tooltip("Loc key of the banner message when the player died.")]
    [SerializeField] private string playerDiedReasonKey = "death.player_reason";
    [Tooltip("Loc key of the banner message when a gate fell (gate defense).")]
    [SerializeField] private string gateFellReasonKey = "death.gate_reason";
    [SerializeField] private TMP_Text goblinsKilledText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text totalGoldText;
    [Tooltip("Optional: text displaying time survived in Survivors mode.")]
    [SerializeField] private TMP_Text timeSurvivedText;
    [Tooltip("Optional: text displaying total damage dealt in Survivors mode.")]
    [SerializeField] private TMP_Text damageDealtText;
    [Tooltip("Optional: text displaying total damage taken in Survivors mode.")]
    [SerializeField] private TMP_Text damageTakenText;
    [Tooltip("Optional: text displaying critical hits in Survivors mode.")]
    [SerializeField] private TMP_Text critsText;
    [Tooltip("Optional: text displaying final level reached in Survivors mode.")]
    [SerializeField] private TMP_Text levelReachedText;
    [Tooltip("Optional: right-side player info and skills sidebar for Survivors mode.")]
    [SerializeField] private SurvivorsPlayerInfoSidebarUI survivorsSidebar;
    [Tooltip("Optional: animated stats panel for Survivors mode.")]
    [SerializeField] private SurvivorsStatsPanelUI survivorsStatsPanel;
    [Tooltip("Restarts the run from wave 1.")]
    [SerializeField] private Button tryAgainButton;
    [Tooltip("Optional: button to proceed to the next stage level in Survivors mode.")]
    [SerializeField] private Button nextStageButton;
    [Tooltip("Optional: button to return to the Main Menu meta-progression upgrades screen.")]
    [SerializeField] private Button returnToMetaButton;
    [Tooltip("Optional: restarts from the wave the player died on. Leave unassigned to offer only a wave-1 restart.")]
    [SerializeField] private Button restartCurrentWaveButton;
    [Tooltip("Optional label on the restart-current-wave button, set to e.g. \"Restart Wave 3\".")]
    [SerializeField] private TMP_Text restartCurrentWaveLabel;
    [Tooltip("Optional: reincarnates — banks Reincarnate Points, resets the gold skill tree and wave to 1, restarts. Leave unassigned to omit the option.")]
    [SerializeField] private Button reincarnateButton;
    [Tooltip("Optional label on the reincarnate button, set to e.g. \"Reincarnate (+7 pts)\".")]
    [SerializeField] private TMP_Text reincarnatePreviewLabel;
    [Tooltip("Optional: the gold skill-tree panel shown when the player dies; hidden once they choose to reincarnate.")]
    [SerializeField] private GameObject goldTreePanel;
    [Tooltip("Optional: the Reincarnate skill-tree panel; hidden until the player clicks Reincarnate, then shown so banked points can be spent before the new run starts.")]
    [SerializeField] private GameObject reincarnateTreePanel;
    [Tooltip("Seconds to fade the screen in.")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Victory Settings")]
    [Tooltip("Default headline label when all waves are cleared in victory.")]
    [SerializeField] private string victoryTitle = "VICTORY!";
    [Tooltip("Label for the button when proceeding to campaign map upon victory.")]
    [SerializeField] private string victoryButtonText = "PROCEED TO CAMPAIGN MAP";
    [Tooltip("Label for the button when returning to meta scene upon defeat.")]
    [SerializeField] private string defeatButtonText = "RETURN TO META AREA";

    public static DeathScreen Instance { get; private set; }

    private Health playerHealth;
    private bool reincarnateBanked = false;
    private bool shown = false;   // latch: the run only ends once, whichever signal fires first
    private bool anyError = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (tryAgainButton == null)
        {
            Debug.LogError("Try Again button is not assigned in the inspector.");
            anyError = true;
        }

        Player player = Player.Instance;
        if (player == null || player.Health == null)
        {
            Debug.LogError("No player Health found for DeathScreen to listen to.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Hidden and non-interactive until the player dies or wins.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // The Reincarnate tree (and the class-select screen) only appear after the player commits
        // to reincarnating.
        if (reincarnateTreePanel != null)
        {
            reincarnateTreePanel.SetActive(false);
        }

        tryAgainButton.onClick.AddListener(RestartFromLevelOne);
        if (nextStageButton != null)
        {
            nextStageButton.onClick.AddListener(ProceedToCampaignMap);
        }
        if (returnToMetaButton != null)
        {
            returnToMetaButton.onClick.AddListener(ReturnToMetaScene);
        }
        if (restartCurrentWaveButton != null)
        {
            restartCurrentWaveButton.onClick.AddListener(RestartFromCurrentWave);
        }
        if (reincarnateButton != null)
        {
            reincarnateButton.onClick.AddListener(HandleReincarnate);
        }

        playerHealth = player.Health;
        playerHealth.OnDied += HandlePlayerDied;
        Gate.OnAnyGateDestroyed += HandleGateDestroyed;
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
        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.RemoveListener(RestartFromLevelOne);
        }
        if (nextStageButton != null)
        {
            nextStageButton.onClick.RemoveListener(ProceedToCampaignMap);
        }
        if (returnToMetaButton != null)
        {
            returnToMetaButton.onClick.RemoveListener(ReturnToMetaScene);
        }
        if (restartCurrentWaveButton != null)
        {
            restartCurrentWaveButton.onClick.RemoveListener(RestartFromCurrentWave);
        }
        if (reincarnateButton != null)
        {
            reincarnateButton.onClick.RemoveListener(HandleReincarnate);
        }
    }

    /// <summary>
    ///     Public entrypoint to display the victory screen when all waves are cleared.
    /// </summary>
    public void ShowVictory(string title = null)
    {
        string t = !string.IsNullOrEmpty(title) ? title : victoryTitle;
        ShowRunOver(t, null, isVictory: true);
    }

    /// <summary>
    ///     Public entrypoint to display the defeat screen.
    /// </summary>
    public void ShowDefeat(string title = null, string failureReason = null)
    {
        string t = !string.IsNullOrEmpty(title) ? title : Loc.Get(playerDiedTitleKey);
        string r = !string.IsNullOrEmpty(failureReason) ? failureReason : Loc.Get(playerDiedReasonKey);
        ShowRunOver(t, r, isVictory: false);
    }

    private void HandlePlayerDied()
    {
        bool isSurvivorsMode = SurvivorsGameManager.Instance != null;
        if (isSurvivorsMode && SurvivorsGameManager.Instance.HasSurvivedSiege)
        {
            ShowRunOver("SIEGE SURVIVED!", "You survived the 20-minute siege! The next stage is unlocked!");
        }
        else if (isSurvivorsMode)
        {
            ShowRunOver("YOU DIDN'T HOLD THE DOOR", "You didn't hold the door...");
        }
        else
        {
            ShowRunOver(Loc.Get(playerDiedTitleKey), Loc.Get(playerDiedReasonKey));
        }
    }

    private void HandleGateDestroyed(Gate gate)
    {
        // Unlike a player death, the player is still alive and controllable — freeze time so the
        // run visibly ends behind the screen. Reload() restores the timescale.
        Time.timeScale = 0f;
        bool isSurvivorsMode = SurvivorsGameManager.Instance != null;
        if (isSurvivorsMode && SurvivorsGameManager.Instance.HasSurvivedSiege)
        {
            ShowRunOver("SIEGE SURVIVED!", "You survived the 20-minute siege! The next stage is unlocked!");
        }
        else
        {
            ShowRunOver(Loc.Get(gateFellTitleKey), Loc.Get(gateFellReasonKey));
        }
    }

    private void ShowRunOver(string title, string failureReason, bool isVictory = false)
    {
        if (shown)
        {
            return;
        }
        shown = true;

        GameObject hudGO = GameObject.Find("Bladehold HUD") ?? GameObject.Find("HUD Canvas") ?? GameObject.Find("HUD");
        if (hudGO != null)
        {
            CanvasGroup hudCG = hudGO.GetComponent<CanvasGroup>();
            if (hudCG != null)
            {
                hudCG.alpha = 0f;
                hudCG.interactable = false;
                hudCG.blocksRaycasts = false;
            }
            else
            {
                hudGO.SetActive(false);
            }
        }

        if (titleText != null)
        {
            titleText.text = title;
        }

        RefreshCurrencies();

        int killed = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        int earned = GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0;
        int total = Player.Instance != null && Player.Instance.Wallet != null ? Player.Instance.Wallet.Coins : 0;

        bool isSurvivorsMode = SurvivorsGameManager.Instance != null;

        if (!isSurvivorsMode)
        {
            if (goblinsKilledText != null)
            {
                goblinsKilledText.text = Loc.Format("wavestats.goblins_slain", killed);
            }
            if (goldEarnedText != null)
            {
                goldEarnedText.text = Loc.Format("wavestats.gold_earned", earned);
            }
        }
        if (totalGoldText != null)
        {
            totalGoldText.text = Loc.Format("wavestats.total_gold", total);
        }

        float runSeconds = 0f;
        int lvl = 1;
        int dmgDealt = 0;
        int dmgTaken = 0;
        int crits = 0;

        if (isVictory)
        {
            // Victory: proceed directly to Campaign Map
            if (tryAgainButton != null)
            {
                tryAgainButton.gameObject.SetActive(false);
            }
            if (returnToMetaButton != null)
            {
                returnToMetaButton.gameObject.SetActive(false);
            }
            if (nextStageButton != null)
            {
                nextStageButton.gameObject.SetActive(true);
                TMP_Text lbl = nextStageButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = victoryButtonText;
            }
            if (restartCurrentWaveButton != null)
            {
                restartCurrentWaveButton.gameObject.SetActive(false);
            }
            if (reincarnateButton != null)
            {
                reincarnateButton.gameObject.SetActive(false);
            }
        }
        else
        {
            // Defeat: return to Meta Scene
            if (nextStageButton != null)
            {
                nextStageButton.gameObject.SetActive(false);
            }
            if (returnToMetaButton != null)
            {
                returnToMetaButton.gameObject.SetActive(true);
                TMP_Text lbl = returnToMetaButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = defeatButtonText;
            }
            if (tryAgainButton != null)
            {
                tryAgainButton.gameObject.SetActive(true);
                TMP_Text lbl = tryAgainButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = "Retry Level";
            }
        }

        if (isSurvivorsMode)
        {
            if (goldTreePanel != null)
            {
                goldTreePanel.SetActive(false);
            }
            if (reincarnateTreePanel != null)
            {
                reincarnateTreePanel.SetActive(false);
            }
            if (reincarnateButton != null)
            {
                reincarnateButton.gameObject.SetActive(false);
            }
            if (restartCurrentWaveButton != null)
            {
                restartCurrentWaveButton.gameObject.SetActive(false);
            }

            // Survivors run telemetry & stats
            runSeconds = SurvivorsGameManager.Instance != null ? SurvivorsGameManager.Instance.RunTimer : 0f;
            lvl = SurvivorsLevelSystem.Instance != null ? SurvivorsLevelSystem.Instance.CurrentLevel : 1;

            if (RunTelemetry.Instance != null)
            {
                var waveStats = RunTelemetry.Instance.GetCurrentWaveStats();
                dmgDealt = Mathf.RoundToInt(waveStats.damageDealt);
                dmgTaken = Mathf.RoundToInt(waveStats.damageTaken);
                crits = waveStats.crits;
            }

            if (survivorsStatsPanel == null)
            {
                survivorsStatsPanel = GetComponentInChildren<SurvivorsStatsPanelUI>(true);
            }

            if (survivorsStatsPanel != null)
            {
                survivorsStatsPanel.HideAllRows();
            }
            else
            {
                int minutes = Mathf.FloorToInt(runSeconds / 60f);
                int seconds = Mathf.FloorToInt(runSeconds % 60f);
                if (timeSurvivedText != null) timeSurvivedText.text = $"{minutes:00}:{seconds:00}";
                if (levelReachedText != null) levelReachedText.text = lvl.ToString();
                if (goblinsKilledText != null) goblinsKilledText.text = killed == 0 ? "0" : killed.ToString("#,##0");
                if (goldEarnedText != null) goldEarnedText.text = earned == 0 ? "0" : earned.ToString("#,##0");
                if (damageDealtText != null) damageDealtText.text = dmgDealt == 0 ? "0" : dmgDealt.ToString("#,##0");
                if (damageTakenText != null) damageTakenText.text = dmgTaken == 0 ? "0" : dmgTaken.ToString("#,##0");
                if (critsText != null) critsText.text = crits == 0 ? "0" : crits.ToString("#,##0");
            }

            if (survivorsSidebar != null)
            {
                survivorsSidebar.gameObject.SetActive(true);
                survivorsSidebar.RefreshSidebar();
            }
        }
        else if (!isVictory)
        {
            // Non-survivors mode extra buttons on defeat
            if (restartCurrentWaveButton != null)
            {
                bool hasWave = WaveSpawner.Instance != null;
                restartCurrentWaveButton.gameObject.SetActive(hasWave);
                if (hasWave && restartCurrentWaveLabel != null)
                {
                    restartCurrentWaveLabel.text = Loc.Format("death.restart_wave", WaveSpawner.Instance.CurrentWave);
                }
            }

            if (reincarnateButton != null)
            {
                bool hasService = ReincarnateService.Instance != null;
                reincarnateButton.gameObject.SetActive(hasService);
                if (hasService && reincarnatePreviewLabel != null)
                {
                    reincarnatePreviewLabel.text = Loc.Format("death.reincarnate", ReincarnateService.Instance.PreviewPointsForReincarnate());
                }
            }
        }

        StartCoroutine(RunOverSequence(failureReason, isSurvivorsMode, isVictory, runSeconds, lvl, killed, earned, dmgDealt, dmgTaken, crits));
    }

    private IEnumerator RunOverSequence(string failureReason, bool isSurvivorsMode = false, bool isVictory = false, float runSeconds = 0f, int lvl = 1, int killed = 0, int earned = 0, int dmgDealt = 0, int dmgTaken = 0, int crits = 0)
    {
        // The failure-reason banner only plays on defeat, not on victory.
        if (!isVictory && failureBanner != null && !string.IsNullOrEmpty(failureReason))
        {
            yield return failureBanner.PlayRoutine(failureReason);
        }

        // PlayerCameraPivot locks/hides the cursor for gameplay look; the buttons need it free.
        CursorLockManager.SetUnlock("DeathScreen", true);

        yield return FadeIn();

        if (isSurvivorsMode && survivorsStatsPanel != null)
        {
            yield return survivorsStatsPanel.PlaySequenceRoutine(runSeconds, lvl, killed, earned, dmgDealt, dmgTaken, crits);
        }
    }

    private IEnumerator FadeIn()
    {
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            // Unscaled so the fade still runs if the game pauses time on death.
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }

    private void RestartFromLevelOne()
    {
        RunState.StartingWave = 1;
        Reload();
    }

    private void ProceedToCampaignMap()
    {
        Time.timeScale = 1f;
        MMTimeScaleEvent.Reset();
        CursorLockManager.SetUnlock("DeathScreen", false);

        // Preserve player health ratio so it carries over
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            RunSession.PlayerHealthRatio = Mathf.Clamp01(Player.Instance.Health.CurrentHealth / Player.Instance.Health.MaxHealth);
        }

        // Preserve player ultimate charge
        if (Player.Instance != null)
        {
            var ult = Player.Instance.GetComponent<PlayerUltimateController>();
            if (ult != null)
            {
                RunSession.PlayerUltimateCharge = ult.CurrentCharge;
            }
        }

        SaveData data = SaveSystem.Load();
        if (data != null)
        {
            int currentStage = data.selectedStage;
            data.highestUnlockedStage = Mathf.Max(data.highestUnlockedStage, currentStage + 1);
            SaveSystem.Save(data);
        }

        if (CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive)
        {
            CampaignManager.Instance.CompleteCurrentNodeAndOpenMap();
        }
        else if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(
                "Bladehold Campaign Map Scene",
                "Castle Campaign",
                "War Room Map",
                "Select your tactical route through the fortress battlements and inner halls."
            );
        }
        else
        {
            SceneManager.LoadScene("Bladehold Campaign Map Scene");
        }
    }

    private void ReturnToMetaScene()
    {
        Time.timeScale = 1f;
        MMTimeScaleEvent.Reset();
        CursorLockManager.SetUnlock("DeathScreen", false);
        RunSession.ClearRun();
        if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(
                "Bladehold Meta Area Scene",
                "Sanctuary",
                "Safe Haven",
                "Prepare upgrades, forge weapons, and plan your next assault."
            );
        }
        else
        {
            SceneManager.LoadScene("Bladehold Meta Area Scene");
        }
    }

    public void RefreshCurrencies()
    {
        foreach (var blood in GetComponentsInChildren<GoblinBloodUI>(true))
        {
            blood.Refresh();
        }
        foreach (var metal in GetComponentsInChildren<OrcishMetalUI>(true))
        {
            metal.Refresh();
        }
        foreach (var coin in GetComponentsInChildren<CoinUI>(true))
        {
            coin.Refresh();
        }
        foreach (var supply in GetComponentsInChildren<SupplyUI>(true))
        {
            supply.Refresh();
        }
    }

    private void ProceedToNextLevel()
    {
        ProceedToCampaignMap();
    }

    private void ReturnToMetaProgression()
    {
        ReturnToMetaScene();
    }

    private void RestartFromCurrentWave()
    {
        // WaveSpawner keeps RunState.StartingWave at the current wave, but read it back explicitly in case
        // execution order ever changes.
        if (WaveSpawner.Instance != null)
        {
            RunState.StartingWave = WaveSpawner.Instance.CurrentWave;
        }
        Reload();
    }

    private void HandleReincarnate()
    {
        if (ReincarnateService.Instance == null || reincarnateBanked)
        {
            return;
        }

        ReincarnateService.Instance.Reincarnate();
    }

    private void Reload()
    {
        // Reload the active scene; scene-scoped singletons (GameStats, Wallet) reset naturally, while the
        // wave to resume from rides across the reload in the static RunState.
        Time.timeScale = 1f;
        MMTimeScaleEvent.Reset();
        CursorLockManager.SetUnlock("DeathScreen", false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
