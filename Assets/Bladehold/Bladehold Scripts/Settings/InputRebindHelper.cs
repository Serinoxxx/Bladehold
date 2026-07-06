using System;
using UnityEngine.InputSystem;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;

/// <summary>
///     Thin wrapper around the Input System's interactive rebinding API, used by
///     <see cref="RebindButtonView" /> to remap any binding on the vendored Controls asset's Player
///     action map. No state of its own — callers own persisting the result (see
///     <see cref="GameSettingsService.PersistInputOverrides" />) and suspending
///     <see cref="PauseMenuController" />'s own Esc toggle for the duration, since Esc is also this
///     operation's cancel key.
/// </summary>
public static class InputRebindHelper
{
    public static RebindingOperation StartRebind(InputAction action, int bindingIndex, Action onComplete, Action onCancel)
    {
        return action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op =>
            {
                op.Dispose();
                onComplete?.Invoke();
            })
            .OnCancel(op =>
            {
                op.Dispose();
                onCancel?.Invoke();
            })
            .Start();
    }
}
