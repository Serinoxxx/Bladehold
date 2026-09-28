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

    [Header("Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [Tooltip("Played on each 3-2-1 beat (the fishing countdown's punch + thump). Optional.")]
    [SerializeField] private MMF_Player countdownBeatFeedback;

    private bool anyError;
    private bool hintBound;
    private int lastCountdown;

    private void Start()
    {
        if (promptPanel == null) { Debug.LogError("[WavePrepPromptUI] promptPanel is not assigned.", this); anyError = true; }
        if (headerText == null) { Debug.LogError("[WavePrepPromptUI] headerText is not assigned.", this); anyError = true; }
        if (holdHint == null) { Debug.LogError("[WavePrepPromptUI] holdHint is not assigned.", this); anyError = true; }
        if (holdFill == null) { Debug.LogError("[WavePrepPromptUI] holdFill is not assigned.", this); anyError = true; }
        if (countdownPanel == null) { Debug.LogError("[WavePrepPromptUI] countdownPanel is not assigned.", this); anyError = true; }
        if (countdownText == null) { Debug.LogError("[WavePrepPromptUI] countdownText is not assigned.", this); anyError = true; }

        if (headerText != null) headerText.text = Loc.Get("wave.prep.header", "Prepare your defences");
        Hide();
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

    private void Hide()
    {
        if (promptPanel != null) promptPanel.SetActive(false);
        if (countdownPanel != null) countdownPanel.SetActive(false);
    }
}
