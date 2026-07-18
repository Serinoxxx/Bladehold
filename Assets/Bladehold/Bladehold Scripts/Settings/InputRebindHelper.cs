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
    /// <summary>
    ///     <paramref name="gamepadColumn" /> keeps each column on its own device family: the Gamepad
    ///     column only accepts gamepad controls (cancel via the pad's Select button, since Esc may not
    ///     be reachable in a pad session), the Keyboard/Mouse column excludes gamepads (and, as
    ///     before, the mouse — pointer buttons stay reserved for UI).
    /// </summary>
    public static RebindingOperation StartRebind(InputAction action, int bindingIndex, Action onComplete, Action onCancel, bool gamepadColumn = false)
    {
        RebindingOperation operation = action.PerformInteractiveRebinding(bindingIndex);
        if (gamepadColumn)
        {
            operation
                .WithControlsHavingToMatchPath("<Gamepad>")
                .WithCancelingThrough("<Gamepad>/select");
        }
        else
        {
            operation
                .WithControlsExcluding("Mouse")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape");
        }
        return operation
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
