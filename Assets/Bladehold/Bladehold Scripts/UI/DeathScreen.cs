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
///     Meta Area with the run wiped. Victory has one: on to the Campaign Map (no retry, the sector is
///     won), and adds a Sector Rewards panel from <see cref="GameLoopManager.Summary" />. The stats panel
///     shows this scene's totals (<see cref="RunTelemetry.GetRunTotals" />). When an optional
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
    [Tooltip("English fallback for the tower refund line (Loc key endscreen.tower_refund); {0} is the supply amount.")]
    [SerializeField] private string towerRefundFormat = "Towers dismantled: +{0} supply";

    [Header("Title Band")]
    [Tooltip("Optional line under the title. Victory: 'Sector defended' (or the campaign-complete line). Defeat: the failure reason.")]
    [SerializeField] private TMP_Text subtitleText;

    [Header("Sector Rewards (victory only)")]
    [Tooltip("Optional: panel listing what the sector paid out (GameLoopManager.Summary). Hidden on defeat and when there's no GameLoopManager.")]
    [SerializeField] private GameObject sectorRewardsPanel;
    [Tooltip("Inactive template row inside the rewards list, cloned once per reward line.")]
    [SerializeField] private SectorRewardRowUI rewardRowTemplate;
    [Tooltip("Captains defeated this sector, one per line, or 'None'.")]
    [SerializeField] private TMP_Text captainsText;
    [SerializeField] private RewardIcon wavesWonIcon;
    [SerializeField] private RewardIcon wavesLostIcon;
    [SerializeField] private RewardIcon goldIcon;
    [SerializeField] private RewardIcon supplyIcon;
    [SerializeField] private RewardIcon goblinBloodIcon;
    [SerializeField] private RewardIcon orcishMetalIcon;
    [SerializeField] private RewardIcon trollHeartIcon;
    [SerializeField] private RewardIcon draftPicksIcon;

    [System.Serializable]
    private struct RewardIcon
    {
        public Sprite sprite;
        public Color tint;
    }
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
        if (sectorRewardsPanel != null)
        {
            sectorRewardsPanel.SetActive(false);
        }
        if (rewardRowTemplate != null)
        {
            rewardRowTemplate.gameObject.SetActive(false);
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
        string t;
        if (isCampaignComplete)
        {
            t = Loc.Get("endscreen.campaign_title", campaignCompleteTitle);
        }
        else if (string.IsNullOrEmpty(title) || title == victoryTitle)
        {
            // GameLoopManager passes the English default; route it through Loc like our own default.
            t = Loc.Get("endscreen.victory_title", victoryTitle);
        }
        else
        {
            t = title;
        }
        string subtitle = isCampaignComplete
            ? Loc.Get("endscreen.campaign_subtitle", "The campaign is won")
            : Loc.Get("endscreen.victory_subtitle", "Sector defended");
        if (towerRefundText != null)
        {
            towerRefundText.gameObject.SetActive(towerRefund > 0);
            towerRefundText.text = Loc.Get("endscreen.tower_refund", towerRefundFormat).Replace("{0}", towerRefund.ToString());
        }
        ShowRunOver(t, subtitle, isVictory: true);
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
        // Tutorial arena: a death just restarts the room, no run-over screen.
        if (TutorialDirector.Instance != null && TutorialDirector.Instance.ReloadOnDeath)
        {
            TutorialDirector.Instance.ReloadAfterDeath();
            return;
        }

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

    /// <param name="subtitle">Victory: the line under the title. Defeat: the failure reason (banner + subtitle).</param>
    private void ShowRunOver(string title, string subtitle, bool isVictory = false)
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
        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            subtitleText.text = subtitle;
        }

        RefreshCurrencies();

        int killed = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        int earned = GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0;
        int total = RunSession.InRunGold;

        bool isSurvivorsMode = SurvivorsGameManager.Instance != null;

        if (totalGoldText != null)
        {
            totalGoldText.text = Loc.Format("wavestats.total_gold", total);
        }

        Button focusButton;
        if (isVictory)
        {
            // Victory: proceed directly to the Campaign Map. No retry: the sector is already won.
            returnToMetaButton.gameObject.SetActive(false);
            nextStageButton.gameObject.SetActive(true);
            TMP_Text lbl = nextStageButton.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
            {
                lbl.text = isCampaignComplete
                    ? Loc.Get("endscreen.return_sanctuary", campaignCompleteButtonText)
                    : Loc.Get("endscreen.proceed_map", victoryButtonText);
            }
            focusButton = nextStageButton;
        }
        else
        {
            // Defeat: the only way out is back to the Meta Area
            nextStageButton.gameObject.SetActive(false);
            returnToMetaButton.gameObject.SetActive(true);
            TMP_Text lbl = returnToMetaButton.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = Loc.Get("endscreen.return_sanctuary", defeatButtonText);
            if (towerRefundText != null)
            {
                towerRefundText.gameObject.SetActive(false);
            }
            focusButton = returnToMetaButton;
        }
        MenuFocusController focus = GetComponent<MenuFocusController>();
        if (focus != null)
        {
            focus.SetDefaultSelectable(focusButton);
        }

        PopulateSectorRewards(isVictory);

        // Sector stats. Damage and crits are this scene's totals from RunTelemetry (the per-wave
        // accumulators it also keeps are reset between waves, so they read 0 at the end screen).
        float runSeconds = 0f;
        int dmgDealt = 0;
        int dmgTaken = 0;
        int crits = 0;
        if (RunTelemetry.Instance != null)
        {
            RunTelemetry.WaveStats totals = RunTelemetry.Instance.GetRunTotals(out runSeconds);
            dmgDealt = Mathf.RoundToInt(totals.damageDealt);
            dmgTaken = Mathf.RoundToInt(totals.damageTaken);
            crits = totals.crits;
        }
        if (isSurvivorsMode)
        {
            runSeconds = SurvivorsGameManager.Instance.RunTimer;
        }
        // The "Wave Reached" row: the wave number the sector ended on (the XP level system is gone).
        int wave = GameLoopManager.Instance != null ? GameLoopManager.Instance.CurrentWave : 1;

        if (survivorsStatsPanel == null)
        {
            survivorsStatsPanel = GetComponentInChildren<SurvivorsStatsPanelUI>(true);
        }

        if (survivorsStatsPanel != null)
        {
            survivorsStatsPanel.HideAllRows();
        }
        else if (!isSurvivorsMode)
        {
            if (goblinsKilledText != null) goblinsKilledText.text = Loc.Format("wavestats.goblins_slain", killed);
            if (goldEarnedText != null) goldEarnedText.text = Loc.Format("wavestats.gold_earned", earned);
        }
        else
        {
            int minutes = Mathf.FloorToInt(runSeconds / 60f);
            int seconds = Mathf.FloorToInt(runSeconds % 60f);
            if (timeSurvivedText != null) timeSurvivedText.text = $"{minutes:00}:{seconds:00}";
            if (levelReachedText != null) levelReachedText.text = wave.ToString();
            if (goblinsKilledText != null) goblinsKilledText.text = killed.ToString("#,##0");
            if (goldEarnedText != null) goldEarnedText.text = earned.ToString("#,##0");
            if (damageDealtText != null) damageDealtText.text = dmgDealt.ToString("#,##0");
            if (damageTakenText != null) damageTakenText.text = dmgTaken.ToString("#,##0");
            if (critsText != null) critsText.text = crits.ToString("#,##0");
        }

        if (survivorsSidebar != null)
        {
            survivorsSidebar.gameObject.SetActive(true);
            survivorsSidebar.RefreshSidebar();
        }

        StartCoroutine(RunOverSequence(isVictory ? null : subtitle, isVictory, runSeconds, wave, killed, earned, dmgDealt, dmgTaken, crits));
    }

    /// <summary>
    ///     Victory only: fills the Sector Rewards panel from <see cref="GameLoopManager.Summary" />,
    ///     one cloned template row per line. Gold and supply always show; the rest only when earned.
    /// </summary>
    private void PopulateSectorRewards(bool isVictory)
    {
        if (sectorRewardsPanel == null)
        {
            return;
        }
        GameLoopManager loop = GameLoopManager.Instance;
        bool show = isVictory && loop != null && rewardRowTemplate != null;
        sectorRewardsPanel.SetActive(show);
        if (!show)
        {
            return;
        }

        SectorSummary s = loop.Summary;
        AddRewardRow(Loc.Get("endscreen.waves_won", "Waves won"), s.wavesWon.ToString(), wavesWonIcon);
        AddRewardRow(Loc.Get("endscreen.waves_lost", "Waves lost"), s.wavesFailed.ToString(), wavesLostIcon);
        AddRewardRow(Loc.Get("endscreen.reward_gold", "Gold"), "+" + s.cardGold.ToString("#,##0"), goldIcon);
        AddRewardRow(Loc.Get("endscreen.reward_supply", "Supply"), "+" + s.cardSupply.ToString("#,##0"), supplyIcon);
        if (s.goblinBlood > 0)
        {
            AddRewardRow(Loc.Get("endscreen.reward_blood", "Goblin Blood"), "+" + s.goblinBlood.ToString("#,##0"), goblinBloodIcon);
        }
        if (s.orcishMetal > 0)
        {
            AddRewardRow(Loc.Get("endscreen.reward_metal", "Orcish Metal"), "+" + s.orcishMetal.ToString("#,##0"), orcishMetalIcon);
        }
        if (s.trollHeartHp > 0)
        {
            AddRewardRow(Loc.Get("endscreen.reward_troll_heart", "Troll Heart max HP"), "+" + s.trollHeartHp.ToString("#,##0"), trollHeartIcon);
        }
        if (s.draftPicks > 0)
        {
            AddRewardRow(Loc.Get("endscreen.reward_draft_picks", "Draft picks"), s.draftPicks.ToString(), draftPicksIcon);
        }

        if (captainsText != null)
        {
            captainsText.text = s.captainsDefeated.Count > 0
                ? string.Join("\n", s.captainsDefeated)
                : Loc.Get("endscreen.captains_none", "None");
        }
    }

    private void AddRewardRow(string label, string value, RewardIcon icon)
    {
        SectorRewardRowUI row = Instantiate(rewardRowTemplate, rewardRowTemplate.transform.parent);
        row.gameObject.SetActive(true);
        row.Set(label, value, icon.sprite, icon.tint);
    }

    private IEnumerator RunOverSequence(string failureReason, bool isVictory, float runSeconds, int wave, int killed, int earned, int dmgDealt, int dmgTaken, int crits)
    {
        // The failure-reason banner only plays on defeat, not on victory.
        if (!isVictory && failureBanner != null && !string.IsNullOrEmpty(failureReason))
        {
            yield return failureBanner.PlayRoutine(failureReason);
        }

        // PlayerCameraPivot locks/hides the cursor for gameplay look; the buttons need it free.
        CursorLockManager.SetUnlock("DeathScreen", true);

        yield return FadeIn();

        if (survivorsStatsPanel != null)
        {
            yield return survivorsStatsPanel.PlaySequenceRoutine(runSeconds, wave, killed, earned, dmgDealt, dmgTaken, crits);
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
                RunSession.MeleeUltimateCharge = ult.GetCharge(UltimateSlot.Melee);
                RunSession.RangedUltimateCharge = ult.GetCharge(UltimateSlot.Ranged);
            }
        }

        if (!CampaignManager.Instance.IsCampaignActive)
        {
            // Only happens when a battle scene is played straight from the Editor: there's no node
            // to complete, so hand over to a fresh campaign run instead.
            Debug.Log("[DeathScreen] Victory with no campaign run active (tutorial, or scene played standalone). Starting a fresh campaign run.");
            if (TutorialRun.Active)
            {
                // The tutorial's gold and supply were a sandbox: the first real run starts clean.
                TutorialRun.End();
                RunSession.StartNewRun();
            }
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
        TutorialRun.End();
        FirstRunGift.TryGrant();
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
