using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;

/// <summary>
///     One remappable binding row: shows the action/binding's display name and current path, and on
///     click starts an interactive rebind via <see cref="InputRebindHelper" />. Instantiated by
///     <see cref="SettingsPanelView" />, which builds one of these per binding on the vendored Controls
///     asset's Player action map (composite parts included) so remapping covers every gameplay control
///     generically rather than hand-picking specific actions.
/// </summary>
public class RebindButtonView : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text bindingPathLabel;
    [SerializeField] private Button button;

    private InputAction action;
    private int bindingIndex;
    private RebindingOperation activeRebind;

    public void Bind(InputAction boundAction, int index, string displayLabel)
    {
        action = boundAction;
        bindingIndex = index;

        if (label != null)
        {
            label.text = displayLabel;
        }
        RefreshPathLabel();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
        activeRebind?.Dispose();
    }

    private void RefreshPathLabel()
    {
        if (bindingPathLabel != null && action != null)
        {
            bindingPathLabel.text = action.GetBindingDisplayString(bindingIndex);
        }
    }

    private void HandleClick()
    {
        if (action == null || activeRebind != null)
        {
            return;
        }

        if (bindingPathLabel != null)
        {
            bindingPathLabel.text = "Press any key...";
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
