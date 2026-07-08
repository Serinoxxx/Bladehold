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
///     column's button is disabled and shows a dash.
/// </summary>
public class RebindButtonView : MonoBehaviour
{
    private const string EmptyBindingText = "—";

    [SerializeField] private TMP_Text label;

    [Header("Keyboard / Mouse column")]
    [SerializeField] private TMP_Text kbmBindingPathLabel;
    [SerializeField] private Button kbmButton;

    [Header("Gamepad column")]
    [SerializeField] private TMP_Text gamepadBindingPathLabel;
    [SerializeField] private Button gamepadButton;

    private InputAction action;
    private int kbmBindingIndex = -1;
    private int gamepadBindingIndex = -1;
    private RebindingOperation activeRebind;

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
        activeRebind?.Dispose();
    }

    /// <summary>Re-reads both bindings' current display strings — e.g. after Reset Settings clears overrides.</summary>
    public void RefreshPathLabel()
    {
        RefreshColumn(kbmBindingPathLabel, kbmBindingIndex);
        RefreshColumn(gamepadBindingPathLabel, gamepadBindingIndex);
    }

    private void RefreshColumn(TMP_Text pathLabel, int bindingIndex)
    {
        if (pathLabel == null)
        {
            return;
        }
        pathLabel.text = action != null && bindingIndex >= 0
            ? action.GetBindingDisplayString(bindingIndex)
            : EmptyBindingText;
    }

    private void HandleKbmClick() => StartRebind(kbmBindingIndex, kbmBindingPathLabel);
    private void HandleGamepadClick() => StartRebind(gamepadBindingIndex, gamepadBindingPathLabel);

    private void StartRebind(int bindingIndex, TMP_Text pathLabel)
    {
        if (action == null || bindingIndex < 0 || activeRebind != null)
        {
            return;
        }

        if (pathLabel != null)
        {
            pathLabel.text = "Press any key...";
        }
        PauseMenuController.Instance?.SetToggleEnabled(false);

        activeRebind = InputRebindHelper.StartRebind(action, bindingIndex, HandleRebindFinished, HandleRebindFinished);
    }

    private void HandleRebindFinished()
    {
        activeRebind = null;
        PauseMenuController.Instance?.SetToggleEnabled(true);
        RefreshPathLabel();
        GameSettingsService.Instance?.PersistInputOverrides();
    }
}
