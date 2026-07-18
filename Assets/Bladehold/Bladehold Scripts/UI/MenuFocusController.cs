using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     Gamepad focus management for one menu panel — the missing piece that makes the existing
///     mouse-driven panels (pause, settings, death screen, intermission, class select, confirm
///     dialog) navigable by pad. The EventSystem's <c>InputSystemUIInputModule</c> already binds
///     gamepad Navigate/Submit/Cancel; nothing ever <em>selected</em> a control, so navigation had
///     no starting point. This component, one per panel root:
///     <list type="bullet">
///         <item>selects <see cref="defaultSelectable" /> when the panel opens with a pad active
///         (mouse users keep their unselected click-driven flow);</item>
///         <item>re-selects when the pad becomes active mid-panel, and clears selection when the
///         mouse takes over;</item>
///         <item>watches for the selection dying (tab content swapped, row rebuilt) and reselects
///         the default so navigation can't strand;</item>
///         <item>optionally traps focus inside <see cref="restrictTo" /> (modal dialogs);</item>
///         <item>invokes <see cref="onCancel" /> on gamepad B so every panel gets a back path.
///         (Polled directly rather than via <c>ICancelHandler</c>, which only reaches the selected
///         object; Esc keeps its existing <see cref="PauseMenuController" /> route.)</item>
///     </list>
/// </summary>
public class MenuFocusController : MonoBehaviour
{
    [Tooltip("Control selected when this panel opens (or regains focus) under gamepad control.")]
    [SerializeField] private Selectable defaultSelectable;
    [Tooltip("Optional focus trap: if pad selection leaves this subtree while the panel is open (e.g. a modal dialog), it is yanked back to the default.")]
    [SerializeField] private RectTransform restrictTo;
    [Tooltip("Invoked on gamepad B while this panel is open. Wire the panel's back/close action; leave empty for panels with no back (e.g. the death screen).")]
    [SerializeField] private UnityEvent onCancel;
    [Tooltip("Suppresses the B-cancel poll, for panels whose B press means something else while open.")]
    [SerializeField] private bool disableCancel = false;

    private void OnEnable()
    {
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
        if (InputDeviceWatcher.GamepadActive)
        {
            SelectDefault();
        }
    }

    private void OnDisable()
    {
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
    }

    private void Update()
    {
        if (!InputDeviceWatcher.GamepadActive || EventSystem.current == null)
        {
            return;
        }

        if (!disableCancel && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            onCancel?.Invoke();
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        bool selectionDead = selected == null || !selected.activeInHierarchy;
        bool selectionEscaped = !selectionDead && restrictTo != null && !selected.transform.IsChildOf(restrictTo);

        if (selectionDead || selectionEscaped)
        {
            SelectDefault();
        }
    }

    private void HandleSchemeChanged(ControlScheme scheme)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        if (scheme == ControlScheme.Gamepad)
        {
            SelectDefault();
        }
        else
        {
            // Mouse users navigate by hover/click — a lingering pad selection just draws a stray highlight.
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>Focuses the default control — also callable by panel code after it swaps tab content.</summary>
    public void SelectDefault()
    {
        if (EventSystem.current == null || defaultSelectable == null || !defaultSelectable.isActiveAndEnabled)
        {
            return;
        }
        EventSystem.current.SetSelectedGameObject(defaultSelectable.gameObject);
    }
}
