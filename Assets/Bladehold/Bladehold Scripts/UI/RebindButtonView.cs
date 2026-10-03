using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;

/// <summary>
///     One remappable action row with two binding columns — Keyboard/Mouse and Gamepad — each showing
///     its binding's current path and starting an interactive rebind via
///     <see cref="InputRebindHelper" /> on click. Instantiated by <see cref="SettingsPanelView" />,
///     which pairs each action's keyboard/mouse and gamepad bindings into one row (composite parts
///     included) so remapping covers every gameplay control generically rather than hand-picking
///     specific actions. Either column may be absent (index -1) — e.g. the gamepad moves with one
///     stick binding while the keyboard has per-direction composite parts — in which case that
///     column's button is disabled and shows a dash. When the optional per-column
///     <see cref="InputGlyph" />s are assigned, each column shows its binding as a button glyph
///     (pinned to that column's device family, so both columns read correctly side by side) and the
///     text label only appears for the dash and the "press any key" prompt, which pulses while
///     listening.
/// </summary>
public class RebindButtonView : MonoBehaviour
{
    private const string EmptyBindingText = "—";

    [SerializeField] private TMP_Text label;

    [Header("Keyboard / Mouse column")]
    [SerializeField] private TMP_Text kbmBindingPathLabel;
    [SerializeField] private Button kbmButton;
    [Tooltip("Optional: glyph drawn for the keyboard/mouse binding. Null = text-only column.")]
    [SerializeField] private InputGlyph kbmGlyph;

    [Header("Gamepad column")]
    [SerializeField] private TMP_Text gamepadBindingPathLabel;
    [SerializeField] private Button gamepadButton;
    [Tooltip("Optional: glyph drawn for the gamepad binding. Null = text-only column.")]
    [SerializeField] private InputGlyph gamepadGlyph;

    [Header("Listening pulse")]
    [SerializeField] private float pulseSpeed = 6f;

    private InputAction action;
    private int kbmBindingIndex = -1;
    private int gamepadBindingIndex = -1;
    private RebindingOperation activeRebind;

    /// <summary>True while any row is listening for input, so the panel's own shortcuts stay out of the way.</summary>
    public static bool AnyRebindActive { get; private set; }
    private TMP_Text listeningLabel;

    /// <summary>Column buttons, exposed so <see cref="SettingsPanelView" /> can wire explicit gamepad navigation across the grid.</summary>
    public Button KbmButton => kbmButton;
    public Button GamepadButton => gamepadButton;

    /// <summary>Binds this row to an action; pass -1 for a column the action has no binding in.</summary>
    public void Bind(InputAction boundAction, int kbmIndex, int gamepadIndex, string displayLabel)
    {
        action = boundAction;
        kbmBindingIndex = kbmIndex;
        gamepadBindingIndex = gamepadIndex;

        if (label != null)
        {
            label.text = displayLabel;
        }
        RefreshPathLabel();

        if (kbmButton != null)
        {
            kbmButton.onClick.RemoveListener(HandleKbmClick);
            kbmButton.onClick.AddListener(HandleKbmClick);
            kbmButton.interactable = kbmBindingIndex >= 0;
        }
        if (gamepadButton != null)
        {
            gamepadButton.onClick.RemoveListener(HandleGamepadClick);
            gamepadButton.onClick.AddListener(HandleGamepadClick);
            gamepadButton.interactable = gamepadBindingIndex >= 0;
        }
    }

    private void OnDestroy()
    {
        if (kbmButton != null)
        {
            kbmButton.onClick.RemoveListener(HandleKbmClick);
        }
        if (gamepadButton != null)
        {
            gamepadButton.onClick.RemoveListener(HandleGamepadClick);
        }
        if (activeRebind != null)
        {
            activeRebind.Dispose();
            AnyRebindActive = false;
        }
    }

    /// <summary>Re-reads both bindings' current display strings — e.g. after Reset Settings clears overrides.</summary>
    public void RefreshPathLabel()
    {
        RefreshColumn(kbmBindingPathLabel, kbmGlyph, kbmBindingIndex, ControlScheme.KeyboardMouse);
        RefreshColumn(gamepadBindingPathLabel, gamepadGlyph, gamepadBindingIndex, ControlScheme.Gamepad);
    }

    private void RefreshColumn(TMP_Text pathLabel, InputGlyph glyph, int bindingIndex, ControlScheme scheme)
    {
        bool hasBinding = action != null && bindingIndex >= 0;
        bool showGlyph = glyph != null && hasBinding;
        if (glyph != null)
        {
            glyph.gameObject.SetActive(showGlyph);
            if (showGlyph)
            {
                glyph.SetBinding(action, bindingIndex, scheme);
            }
        }
        if (pathLabel == null)
        {
            return;
        }
        pathLabel.text = hasBinding ? action.GetBindingDisplayString(bindingIndex) : EmptyBindingText;
        pathLabel.alpha = 1f;
        pathLabel.gameObject.SetActive(!showGlyph);
    }

    private void Update()
    {
        if (listeningLabel != null)
        {
            listeningLabel.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulseSpeed * 0.5f));
        }
    }

    private void HandleKbmClick() => StartRebind(kbmBindingIndex, kbmBindingPathLabel, kbmGlyph, gamepadColumn: false);
    private void HandleGamepadClick() => StartRebind(gamepadBindingIndex, gamepadBindingPathLabel, gamepadGlyph, gamepadColumn: true);

    private void StartRebind(int bindingIndex, TMP_Text pathLabel, InputGlyph glyph, bool gamepadColumn)
    {
        if (action == null || bindingIndex < 0 || activeRebind != null)
        {
            return;
        }

        if (glyph != null)
        {
            glyph.gameObject.SetActive(false);
        }
        if (pathLabel != null)
        {
            pathLabel.gameObject.SetActive(true);
            pathLabel.text = Loc.Get("rebind.press_any_key");
            listeningLabel = pathLabel;
        }
        PauseMenuController.Instance?.SetToggleEnabled(false);

        AnyRebindActive = true;
        activeRebind = InputRebindHelper.StartRebind(action, bindingIndex, HandleRebindFinished, HandleRebindFinished, gamepadColumn);
    }

    private void HandleRebindFinished()
    {
        activeRebind = null;
        AnyRebindActive = false;
        listeningLabel = null;
        PauseMenuController.Instance?.SetToggleEnabled(true);
        RefreshPathLabel();
        GameSettingsService.Instance?.PersistInputOverrides();
    }
}
