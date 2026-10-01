using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     Top-centre guidance panel for the tutorial and one-time first-encounter hints: a primary line with
///     its input glyph, an optional counter ("2/3"), and an optional secondary line with its own glyph.
///     Lives in the HUD; hidden until something calls <see cref="Show" />. Glyphs follow device switches
///     and rebinds through <see cref="InputGlyph" />; text follows language changes.
///     Pure display: <see cref="TutorialDirector" /> and <see cref="FirstTimeHints" /> decide what it says.
/// </summary>
public class TutorialHintUI : MonoBehaviour
{
    public static TutorialHintUI Instance { get; private set; }

    [Header("Layout")]
    [Tooltip("Root toggled on Show/Hide (a child, so this component keeps running).")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private InputGlyph primaryGlyph;
    [SerializeField] private TMP_Text primaryText;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private GameObject secondaryRoot;
    [SerializeField] private InputGlyph secondaryGlyph;
    [SerializeField] private TMP_Text secondaryText;
    [Tooltip("Labels grow to fit their text up to this width, then wrap onto more lines (the rows grow in height).")]
    [Min(100f)] [SerializeField] private float maxLabelWidth = 760f;

    [Header("Feedback")]
    [Tooltip("Panel slides/pops in with a new hint (UI, unscaled).")]
    [SerializeField] private MMF_Player showFeedback;
    [Tooltip("Step done: tick + chime (UI, unscaled).")]
    [SerializeField] private MMF_Player completeFeedback;
    [Tooltip("Counter changed: small punch (UI, unscaled).")]
    [SerializeField] private MMF_Player counterFeedback;

    // The persistent hint (the tutorial's current step) and a timed overlay (a first-encounter hint)
    // that shows on top of it for a few seconds, then hands the panel back.
    private TutorialHint stepPrimary;
    private TutorialHint stepSecondary;
    private string stepCounter;
    private TutorialHint overlayPrimary;
    private TutorialHint overlaySecondary;
    private float overlayUntil = -1f;
    private bool anyError;

    private bool OverlayActive => overlayPrimary != null;
    private TutorialHint primary => OverlayActive ? overlayPrimary : stepPrimary;
    private TutorialHint secondary => OverlayActive ? overlaySecondary : stepSecondary;

    public bool IsShowing => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void Start()
    {
        if (panelRoot == null) { Debug.LogError("[TutorialHintUI] panelRoot is not assigned.", this); anyError = true; }
        if (primaryText == null) { Debug.LogError("[TutorialHintUI] primaryText is not assigned.", this); anyError = true; }
        if (primaryGlyph == null) Debug.LogError("[TutorialHintUI] primaryGlyph is not assigned.", this);
        if (counterText == null) Debug.LogError("[TutorialHintUI] counterText is not assigned.", this);
        if (secondaryRoot == null || secondaryText == null) Debug.LogError("[TutorialHintUI] secondaryRoot/secondaryText is not assigned.", this);
        if (showFeedback == null) Debug.LogError("[TutorialHintUI] showFeedback is not assigned.", this);
        if (completeFeedback == null) Debug.LogError("[TutorialHintUI] completeFeedback is not assigned.", this);
        if (counterFeedback == null) Debug.LogError("[TutorialHintUI] counterFeedback is not assigned.", this);

        InputDeviceWatcher.BindingsChanged += Refresh;
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
        Loc.OnLanguageChanged += Refresh;
    }

    private void OnDestroy()
    {
        InputDeviceWatcher.BindingsChanged -= Refresh;
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
        Loc.OnLanguageChanged -= Refresh;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (OverlayActive && Time.unscaledTime >= overlayUntil)
        {
            overlayPrimary = null;
            overlaySecondary = null;
            Redraw(playShow: stepPrimary != null);
        }
    }

    /// <summary>Shows the persistent hint (the tutorial's current step) until replaced or hidden.</summary>
    public void Show(TutorialHint hint, TutorialHint secondaryHint = null)
    {
        if (anyError || hint == null) return;
        bool changed = stepPrimary != hint || !IsShowing;
        stepPrimary = hint;
        stepSecondary = secondaryHint;
        stepCounter = null;
        if (!OverlayActive) Redraw(changed);
    }

    /// <summary>Shows a hint on top of the current one for <paramref name="seconds" />, then restores it.</summary>
    public void ShowTimed(TutorialHint hint, float seconds, TutorialHint secondaryHint = null)
    {
        if (anyError || hint == null) return;
        overlayPrimary = hint;
        overlaySecondary = secondaryHint;
        overlayUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
        Redraw(playShow: true);
    }

    /// <summary>Replaces only the persistent secondary line (heavy-attack nudge, low-ammo tip). Null clears it.</summary>
    public void SetSecondary(TutorialHint hint)
    {
        if (anyError) return;
        stepSecondary = hint;
        if (!OverlayActive) Refresh();
    }

    /// <summary>The persistent hint's counter ("2/3"). Null or empty hides it.</summary>
    public void SetCounter(string text)
    {
        if (anyError) return;
        bool changed = !string.IsNullOrEmpty(text) && text != stepCounter && !string.IsNullOrEmpty(stepCounter);
        stepCounter = text;
        if (OverlayActive) return;
        ApplyCounter(stepCounter);
        if (changed && counterFeedback != null) counterFeedback.PlayFeedbacks();
    }

    public void PlayComplete()
    {
        if (completeFeedback != null) completeFeedback.PlayFeedbacks();
    }

    /// <summary>Clears the persistent hint. A timed overlay still runs out on its own.</summary>
    public void Hide()
    {
        stepPrimary = null;
        stepSecondary = null;
        stepCounter = null;
        if (!OverlayActive) Redraw(false);
    }

    private void Redraw(bool playShow)
    {
        if (anyError) return;
        bool show = primary != null;
        panelRoot.SetActive(show);
        if (!show) return;
        ApplyCounter(OverlayActive ? null : stepCounter);
        Refresh();
        if (playShow && showFeedback != null) showFeedback.PlayFeedbacks();
    }

    private void ApplyCounter(string text)
    {
        if (counterText == null) return;
        bool show = !string.IsNullOrEmpty(text);
        counterText.gameObject.SetActive(show);
        if (show) counterText.text = text;
    }

    private void HandleSchemeChanged(ControlScheme scheme) => Refresh();

    private void Refresh()
    {
        if (anyError || primary == null) return;
        SetLabel(primaryText, primary.Text);
        BindGlyph(primaryGlyph, primary);

        bool hasSecondary = secondary != null && !secondary.IsEmpty;
        if (secondaryRoot != null) secondaryRoot.SetActive(hasSecondary);
        if (hasSecondary && secondaryText != null)
        {
            SetLabel(secondaryText, secondary.Text);
            BindGlyph(secondaryGlyph, secondary);
        }
    }

    // The rows' layout groups size each label from its LayoutElement: one line up to maxLabelWidth, then
    // wrapped, so a long line (the heavy-attack nudge) pushes the rows apart instead of overflowing onto them.
    private void SetLabel(TMP_Text label, string text)
    {
        label.text = text;
        LayoutElement layout = label.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = Mathf.Min(label.GetPreferredValues(text, Mathf.Infinity, Mathf.Infinity).x + 2f, maxLabelWidth);
        }
    }

    private static void BindGlyph(InputGlyph glyph, TutorialHint hint)
    {
        if (glyph == null) return;
        if (!hint.HasGlyph)
        {
            glyph.gameObject.SetActive(false);
            return;
        }

        if (!string.IsNullOrEmpty(hint.actionName))
        {
            InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
                ? Player.Instance.InputSettings.GetRebindableActionMap()
                : null;
            InputAction action = map != null ? map.FindAction(hint.actionName) : null;
            if (action == null)
            {
                glyph.gameObject.SetActive(false);
                return;
            }
            glyph.SetAction(action);
        }
        else
        {
            glyph.SetPaths(hint.kbmPath, hint.gamepadPath);
        }
        glyph.gameObject.SetActive(glyph.HasBinding);
    }
}
