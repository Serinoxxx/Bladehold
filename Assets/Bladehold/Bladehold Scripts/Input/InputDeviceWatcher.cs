using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The two input families the game distinguishes for look handling, glyphs, and menu focus.</summary>
public enum ControlScheme
{
    KeyboardMouse,
    Gamepad,
}

/// <summary>
///     Tracks which input family the player last used — the single source of truth behind button
///     glyphs, on-screen hints, menu auto-focus, and the camera's stick-vs-mouse look scaling. A
///     plain static class (the <see cref="SaveSystem" /> shape, not a scene object): it must stay
///     alive on the death screen and across scene reloads, and needs zero Editor wiring.
///
///     Listens to <see cref="InputSystem.onActionChange" /> filtered to
///     <see cref="InputActionChange.ActionPerformed" /> rather than the raw
///     <see cref="InputSystem.onEvent" /> stream: pads emit constant state packets even when
///     untouched, but an action only *performs* through its bindings' processors — so stick drift
///     under the deadzone can never flip the scheme, while any deliberate press/move on either
///     family flips it immediately (mouse jiggle deliberately counts as keyboard/mouse activity).
/// </summary>
public static class InputDeviceWatcher
{
    /// <summary>The input family most recently used to perform any action. Defaults to keyboard/mouse.</summary>
    public static ControlScheme Current { get; private set; } = ControlScheme.KeyboardMouse;

    /// <summary>True when the last-used device is a gamepad — sugar for the common check.</summary>
    public static bool GamepadActive => Current == ControlScheme.Gamepad;

    /// <summary>Raised when the active input family changes (never for repeated use of the same family).</summary>
    public static event Action<ControlScheme> SchemeChanged;

    /// <summary>
    ///     Raised after gameplay binding overrides change (a rebind is applied or settings reset),
    ///     so glyphs and hint bars re-resolve their sprites. Raised by the settings UI — it lives
    ///     here because the subscribers are the same audience as <see cref="SchemeChanged" />'s.
    /// </summary>
    public static event Action BindingsChanged;

    private static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Domain-reload-off safety (the SaveSystem precedent): drop dead-scene subscribers and
        // re-arm the hook per play session.
        SchemeChanged = null;
        BindingsChanged = null;
        Current = ControlScheme.KeyboardMouse;
        if (hooked)
        {
            InputSystem.onActionChange -= HandleActionChange;
            hooked = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        if (!hooked)
        {
            InputSystem.onActionChange += HandleActionChange;
            hooked = true;
        }
    }

    /// <summary>Called by the settings UI after binding overrides are applied, loaded, or reset.</summary>
    public static void NotifyBindingsChanged()
    {
        BindingsChanged?.Invoke();
    }

    private static void HandleActionChange(object actionOrMap, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed || !(actionOrMap is InputAction action))
        {
            return;
        }

        InputDevice device = action.activeControl?.device;
        if (device == null)
        {
            return;
        }

        ControlScheme scheme;
        if (device is Gamepad || device is UnityEngine.InputSystem.Joystick)
        {
            scheme = ControlScheme.Gamepad;
        }
        else if (device is Keyboard || device is Mouse || device is Pointer)
        {
            scheme = ControlScheme.KeyboardMouse;
        }
        else
        {
            return;
        }

        if (scheme == Current)
        {
            return;
        }

        Current = scheme;
        SchemeChanged?.Invoke(scheme);
    }
}
