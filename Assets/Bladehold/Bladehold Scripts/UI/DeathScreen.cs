using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("Restarts the run from wave 1.")]
    [SerializeField] private Button tryAgainButton;
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
    [Tooltip("Optional: the full-screen class-select screen shown after banking Reincarnate Points, so the player can reincarnate as a different class. Leave unassigned to keep the current class (the one-click Reincarnate() fallback).")]
    [SerializeField] private ClassSelectScreen classSelectScreen;
    [Tooltip("Seconds to fade the screen in.")]
    [SerializeField] private float fadeDuration = 1f;

    private Health playerHealth;
    private bool reincarnateBanked = false;
    private bool shown = false;   // latch: the run only ends once, whichever signal fires first
    private bool anyError = false;

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

        // Hidden and non-interactive until the player dies.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // The Reincarnate tree (and the class-select screen) only appear after the player commits
        // to reincarnating.
        if (reincarnateTreePanel != null)
        {
            reincarnateTreePanel.SetActive(false);
        }
        if (classSelectScreen != null)
        {
            classSelectScreen.gameObject.SetActive(false);
        }

        tryAgainButton.onClick.AddListener(RestartFromLevelOne);
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
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
        Gate.OnAnyGateDestroyed -= HandleGateDestroyed;
        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.RemoveListener(RestartFromLevelOne);
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

    private void HandlePlayerDied()
    {
        ShowRunOver(Loc.Get(playerDiedTitleKey), Loc.Get(playerDiedReasonKey));
    }

    private void HandleGateDestroyed(Gate gate)
    {
        // Unlike a player death, the player is still alive and controllable — freeze time so the
        // run visibly ends behind the screen. Reload() restores the timescale.
        Time.timeScale = 0f;
        ShowRunOver(Loc.Get(gateFellTitleKey), Loc.Get(gateFellReasonKey));
    }

    private void ShowRunOver(string title, string failureReason)
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

        int killed = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        int earned = GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0;
        int total = Player.Instance != null && Player.Instance.Wallet != null ? Player.Instance.Wallet.Coins : 0;

        if (goblinsKilledText != null)
        {
            goblinsKilledText.text = Loc.Format("wavestats.goblins_slain", killed);
        }
        if (goldEarnedText != null)
        {
            goldEarnedText.text = Loc.Format("wavestats.gold_earned", earned);
        }
        if (totalGoldText != null)
        {
            totalGoldText.text = Loc.Format("wavestats.total_gold", total);
        }

        // Only offer "restart from current wave" if there's a wave in progress to return to.
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

        StartCoroutine(RunOverSequence(failureReason));
    }

    private IEnumerator RunOverSequence(string failureReason)
    {
        // The failure-reason banner plays fully (fade in, hold, fade out) before the screen appears.
        if (failureBanner != null)
        {
            yield return failureBanner.PlayRoutine(failureReason);
        }

        // PlayerCameraPivot locks/hides the cursor for gameplay look; the skill tree needs it
        // free to click buttons. Freed only now so no cursor floats over the banner. Reload()
        // re-locks it before restarting.
        CursorLockManager.SetUnlock("DeathScreen", true);

        yield return FadeIn();
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

        // Without a Reincarnate tree panel or class-select screen to hand off to, fall back to the
        // one-click flow.
        if (reincarnateTreePanel == null || classSelectScreen == null)
        {
            ReincarnateService.Instance.Reincarnate();
            return;
        }

        // Single click: bank the points, wipe the gold tree, and open the full-screen class picker —
        // it owns the Reincarnate tree ("Spend Points" toggle) and the reload (Confirm) from here.
        reincarnateBanked = true;
        ReincarnateService.Instance.BankPointsAndResetGoldTree();

        if (goldTreePanel != null)
        {
            goldTreePanel.SetActive(false);
        }
        // The old run is already gone (gold tree wiped, wave reset) — the only way forward is the new run.
        tryAgainButton.gameObject.SetActive(false);
        if (restartCurrentWaveButton != null)
        {
            restartCurrentWaveButton.gameObject.SetActive(false);
        }
        reincarnateButton.gameObject.SetActive(false);

        classSelectScreen.Open();
    }

    private void Reload()
    {
        // Reload the active scene; scene-scoped singletons (GameStats, Wallet) reset naturally, while the
        // wave to resume from rides across the reload in the static RunState.
        Time.timeScale = GameSettingsService.TargetTimeScale; // ensure normal speed resumes even if something paused time on death.
        CursorLockManager.SetUnlock("DeathScreen", false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
