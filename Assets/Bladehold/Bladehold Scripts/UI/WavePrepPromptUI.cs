using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     Top-centre prep prompt (plan 15), the battle-scene twin of the Fishing Pond start prompt:
///     "PREPARE YOUR DEFENCES" with the upcoming objective, a [T] / D-pad Down glyph with "Hold to start
///     next wave", and a fill bar tracking <see cref="GameLoopManager.ReadyHoldProgress" />. During the
///     3-2-1 it shows the big countdown number instead. Pure view: polls <see cref="GameLoopManager" />.
///     While <see cref="GameLoopManager.IsReadyBlocked" /> (a tutorial lesson owns the prep phase, plan 21)
///     the hold row and bar give way to "Finish the lesson first", and pressing Ready plays
///     <see cref="deniedFeedback" />.
/// </summary>
public class WavePrepPromptUI : MonoBehaviour
{
    private const string StartWaveActionName = "StartWave";

    [Header("Prompt")]
    [Tooltip("Shown while the game waits for the Ready hold.")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TMP_Text headerText;
    [Tooltip("Upcoming objective line, e.g. \"Next: Free the Prisoners\". Optional.")]
    [SerializeField] private TMP_Text objectiveText;
    [Tooltip("Glyph + label row bound to the StartWave action, so it follows rebinds and the active device.")]
    [SerializeField] private HintEntryView holdHint;
    [Tooltip("Filled Image (Horizontal) tracking the 1 s hold.")]
    [SerializeField] private Image holdFill;

    [Header("Locked (tutorial lesson running)")]
    [Tooltip("Hold bar root, hidden while Ready is blocked. Empty = the fill image itself.")]
    [SerializeField] private GameObject holdBar;
    [Tooltip("\"Finish the lesson first\", shown instead of the hold row while Ready is blocked.")]
    [SerializeField] private TMP_Text lockedText;
    [Tooltip("Played when Ready is pressed while blocked: the denied sound and a red pulse of the locked line. Unscaled time.")]
    [SerializeField] private MMF_Player deniedFeedback;

    [Header("Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [Tooltip("Played on each 3-2-1 beat (the fishing countdown's punch + thump). Optional.")]
    [SerializeField] private MMF_Player countdownBeatFeedback;

    private bool anyError;
    private bool hintBound;
    private bool showingLocked;
    private GameLoopManager subscribedLoop;
    private int lastCountdown;

    private void Start()
    {
        if (promptPanel == null) { Debug.LogError("[WavePrepPromptUI] promptPanel is not assigned.", this); anyError = true; }
        if (headerText == null) { Debug.LogError("[WavePrepPromptUI] headerText is not assigned.", this); anyError = true; }
        if (holdHint == null) { Debug.LogError("[WavePrepPromptUI] holdHint is not assigned.", this); anyError = true; }
        if (holdFill == null) { Debug.LogError("[WavePrepPromptUI] holdFill is not assigned.", this); anyError = true; }
        if (countdownPanel == null) { Debug.LogError("[WavePrepPromptUI] countdownPanel is not assigned.", this); anyError = true; }
        if (countdownText == null) { Debug.LogError("[WavePrepPromptUI] countdownText is not assigned.", this); anyError = true; }

        if (lockedText == null) { Debug.LogError("[WavePrepPromptUI] lockedText is not assigned.", this); anyError = true; }
        if (deniedFeedback == null) Debug.LogError("[WavePrepPromptUI] deniedFeedback is not assigned.", this);
        if (holdBar == null && holdFill != null) holdBar = holdFill.gameObject;

        if (headerText != null) headerText.text = Loc.Get("wave.prep.header", "Prepare your defences");
        if (lockedText != null)
        {
            lockedText.text = Loc.Get("wave.prep.locked", "Finish the lesson first");
            lockedText.gameObject.SetActive(false);
        }
        Hide();
    }

    private void OnDestroy()
    {
        if (subscribedLoop != null) subscribedLoop.OnReadyDenied -= HandleReadyDenied;
    }

    private void HandleReadyDenied()
    {
        if (deniedFeedback != null) deniedFeedback.PlayFeedbacks();
    }

    private void Update()
    {
        if (anyError) return;

        GameLoopManager loop = GameLoopManager.Instance;
        if (loop == null)
        {
            Hide();
            return;
        }
        if (loop != subscribedLoop)
        {
            if (subscribedLoop != null) subscribedLoop.OnReadyDenied -= HandleReadyDenied;
            subscribedLoop = loop;
            loop.OnReadyDenied += HandleReadyDenied;
        }

        int countdown = loop.CountdownSeconds;
        if (countdown > 0)
        {
            promptPanel.SetActive(false);
            countdownPanel.SetActive(true);
            if (countdown != lastCountdown)
            {
                countdownText.text = countdown.ToString();
                if (countdownBeatFeedback != null) countdownBeatFeedback.PlayFeedbacks();
            }
            lastCountdown = countdown;
            return;
        }
        lastCountdown = 0;
        countdownPanel.SetActive(false);

        bool show = loop.IsAwaitingReady;
        promptPanel.SetActive(show);
        if (!show) return;

        if (!hintBound) BindHint();
        SetLocked(loop.IsReadyBlocked);
        holdFill.fillAmount = loop.ReadyHoldProgress;

        if (objectiveText != null)
        {
            string title = loop.UpcomingObjectiveTitle;
            objectiveText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            objectiveText.text = Loc.Get("wave.prep.next", "Next: {0}").Replace("{0}", title);
        }
    }

    private void BindHint()
    {
        InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
            ? Player.Instance.InputSettings.GetRebindableActionMap()
            : null;
        InputAction action = map != null ? map.FindAction(StartWaveActionName) : null;
        if (action != null)
        {
            holdHint.Bind(action, "wave.prep.hold", "Hold to start next wave");
        }
        else
        {
            holdHint.Bind("<Keyboard>/t", "<Gamepad>/dpad/down", "wave.prep.hold", "Hold to start next wave");
        }
        hintBound = true;
    }

    private void SetLocked(bool locked)
    {
        if (locked == showingLocked) return;
        showingLocked = locked;
        lockedText.gameObject.SetActive(locked);
        holdBar.SetActive(!locked);
        // The hint row hides itself when the device has no binding, so let it decide when it comes back.
        if (locked) holdHint.gameObject.SetActive(false);
        else holdHint.UpdateVisibility();
    }

    private void Hide()
    {
        if (promptPanel != null) promptPanel.SetActive(false);
        if (countdownPanel != null) countdownPanel.SetActive(false);
    }
}
