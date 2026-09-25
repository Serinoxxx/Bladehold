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
///     one since the player is still alive behind the screen) — or, via <see cref="ShowVictory" />, when
///     the sector is won. Shows goblins killed and gold earned this run (from <see cref="GameStats" />)
///     plus the run's gold (<see cref="RunSession.InRunGold" />). Defeat has one way out: back to the
///     Meta Area with the run wiped. Victory has one: on to the Campaign Map. When an optional
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
    [Tooltip("Victory: proceeds to the Campaign Map.")]
    [SerializeField] private Button nextStageButton;
    [Tooltip("Defeat: wipes the run and returns to the Meta Area.")]
    [SerializeField] private Button returnToMetaButton;
    [Tooltip("Optional: victory line showing the supply refunded for dismantled towers. Hidden on defeat or when nothing was refunded.")]
    [SerializeField] private TMP_Text towerRefundText;
    [Tooltip("Format for the tower refund line; {0} is the supply amount.")]
    [SerializeField] private string towerRefundFormat = "Towers dismantled: +{0} supply";
    [Tooltip("Seconds to fade the screen in.")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("Victory Settings")]
    [Tooltip("Default headline label when all waves are cleared in victory.")]
    [SerializeField] private string victoryTitle = "VICTORY!";
    [Tooltip("Label for the button when proceeding to campaign map upon victory.")]
    [SerializeField] private string victoryButtonText = "PROCEED TO CAMPAIGN MAP";
    [Tooltip("Label for the button when returning to meta scene upon defeat.")]
    [SerializeField] private string defeatButtonText = "RETURN TO META AREA";
    [Tooltip("Headline when the cleared node ends the campaign (final boss or demo cutoff).")]
    [SerializeField] private string campaignCompleteTitle = "CAMPAIGN COMPLETE!";
    [Tooltip("Victory button label when the campaign is over: it wipes the run and returns to the Meta Area.")]
    [SerializeField] private string campaignCompleteButtonText = "RETURN TO SANCTUARY";

    public static DeathScreen Instance { get; private set; }

    private Health playerHealth;
    private bool shown = false;   // latch: the run only ends once, whichever signal fires first
    private bool anyError = false;
    private bool isCampaignComplete = false;

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
        if (nextStageButton == null)
        {
            Debug.LogError("[DeathScreen] Next Stage (proceed to map) button is not assigned in the inspector.");
            anyError = true;
        }
        if (returnToMetaButton == null)
        {
            Debug.LogError("[DeathScreen] Return To Meta button is not assigned in the inspector.");
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

        if (towerRefundText != null)
        {
            towerRefundText.gameObject.SetActive(false);
        }

        nextStageButton.onClick.AddListener(ProceedToCampaignMap);
        returnToMetaButton.onClick.AddListener(ReturnToMetaScene);

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
        if (nextStageButton != null)
        {
            nextStageButton.onClick.RemoveListener(ProceedToCampaignMap);
        }
        if (returnToMetaButton != null)
        {
            returnToMetaButton.onClick.RemoveListener(ReturnToMetaScene);
        }
    }

    /// <summary>
    ///     Public entrypoint to display the victory screen when a sector or boss is cleared.
    ///     <paramref name="towerRefund" /> is the supply already refunded for dismantled towers.
    ///     If the current campaign node ends the campaign, this shows the campaign-complete version,
    ///     whose button wipes the run and returns to the Meta Area.
    /// </summary>
    public void ShowVictory(string title = null, int towerRefund = 0)
    {
        isCampaignComplete = CampaignManager.Instance.CurrentNodeEndsCampaign;
        string defaultTitle = isCampaignComplete ? campaignCompleteTitle : victoryTitle;
        string t = !string.IsNullOrEmpty(title) ? title : defaultTitle;
        if (towerRefundText != null)
        {
            towerRefundText.gameObject.SetActive(towerRefund > 0);
            towerRefundText.text = string.Format(towerRefundFormat, towerRefund);
        }
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
        if (isSurvivorsMode)
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
        // run visibly ends behind the screen. ReturnToMetaScene() restores the timescale.
        Time.timeScale = 0f;
        ShowRunOver(Loc.Get(gateFellTitleKey), Loc.Get(gateFellReasonKey));
    }

    private void ShowRunOver(string title, string failureReason, bool isVictory = false)
    {
        if (shown || anyError)
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
        int total = RunSession.InRunGold;

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
            returnToMetaButton.gameObject.SetActive(false);
            nextStageButton.gameObject.SetActive(true);
            TMP_Text lbl = nextStageButton.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = isCampaignComplete ? campaignCompleteButtonText : victoryButtonText;
        }
        else
        {
            // Defeat: the only way out is back to the Meta Area
            nextStageButton.gameObject.SetActive(false);
            returnToMetaButton.gameObject.SetActive(true);
            TMP_Text lbl = returnToMetaButton.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = defeatButtonText;
            if (towerRefundText != null)
            {
                towerRefundText.gameObject.SetActive(false);
            }
        }

        if (isSurvivorsMode)
        {
            // Survivors run telemetry & stats
            runSeconds = SurvivorsGameManager.Instance != null ? SurvivorsGameManager.Instance.RunTimer : 0f;
            // The stats panel's "Level Reached" row now shows the wave reached (the XP level system is gone).
            lvl = GameLoopManager.Instance != null ? GameLoopManager.Instance.CurrentWave : 1;

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
            var ult = Player.Instance.transform.root.GetComponentInChildren<PlayerUltimateController>(true);
            if (ult != null)
            {
                RunSession.PlayerUltimateCharge = ult.CurrentCharge;
            }
        }

        if (!CampaignManager.Instance.IsCampaignActive)
        {
            // Only happens when a battle scene is played straight from the Editor: there's no node
            // to complete, so hand over to a fresh campaign run instead.
            Debug.Log("[DeathScreen] Victory with no campaign run active (scene played standalone). Starting a fresh campaign run.");
            CampaignManager.Instance.StartCampaignRun();
            CampaignManager.Instance.OpenOverviewMap();
            return;
        }

        // Returns to the map, or ends the campaign (run wiped, back to Meta) if this node was the last.
        CampaignManager.Instance.CompleteCurrentNodeAndContinue();
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
}
