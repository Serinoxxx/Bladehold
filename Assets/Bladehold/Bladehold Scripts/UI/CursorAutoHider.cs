using UnityEngine;

/// <summary>
///     Hides the hardware cursor while the gamepad is the active device — a pad session has no
///     pointer, so a frozen arrow in menu corners is just noise — and restores it the moment the
///     mouse moves again. Only <see cref="Cursor.visible" /> is touched, never
///     <see cref="Cursor.lockState" />: gameplay's cursor lock stays owned by
///     <see cref="PlayerCameraPivot" /> (and lock already implies hidden), and menus keep the cursor
///     free for mouse users. One instance lives on a UI root object in the scene.
/// </summary>
public class CursorAutoHider : MonoBehaviour
{
    private void OnEnable()
    {
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
    }

    private void OnDisable()
    {
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
    }

    private void HandleSchemeChanged(ControlScheme scheme)
    {
        if (scheme == ControlScheme.Gamepad)
        {
            Cursor.visible = false;
        }
        else if (Cursor.lockState != CursorLockMode.Locked)
        {
            // Mouse is back: show it again unless gameplay's lock (which implies hidden) is in force.
            Cursor.visible = true;
        }
    }
}
