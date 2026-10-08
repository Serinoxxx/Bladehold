using System.Collections.Generic;
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
///     Open panels form a stack (most recently opened on top), and only the top one acts: it alone
///     handles B, and pad focus that wanders into a panel underneath (the wave cards behind the pause
///     menu) is pulled back. When the top panel closes, the one below gets its last selection back.
///     "Opened" means active and, if the panel has its own CanvasGroup, interactable — so panels that
///     stay active and fade (the pause menu, the confirm dialog) count too.
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

    /// <summary>Open panels, oldest first; the last entry is the one in front.</summary>
    private static readonly List<MenuFocusController> openStack = new List<MenuFocusController>();
    /// <summary>One B press closes one panel: the panel revealed underneath mustn't see the same press.</summary>
    private static int lastCancelFrame = -1;

    private CanvasGroup canvasGroup;
    private bool registeredOpen;
    private GameObject lastSelected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openStack.Clear();
        lastCancelFrame = -1;
    }

    /// <summary>
    ///     A panel faded out through its own CanvasGroup (e.g. the confirm dialog, which stays active
    ///     at alpha 0) is closed: it must neither grab focus nor trap it.
    /// </summary>
    private bool PanelOpen => canvasGroup == null || canvasGroup.interactable;

    private bool IsTop => openStack.Count > 0 && openStack[openStack.Count - 1] == this;

    /// <summary>
    ///     True when <see cref="onCancel" /> has at least one listener with a live target. Panels whose
    ///     cancel wiring was lost (a null target on a scene copy) use this to add a code fallback.
    /// </summary>
    public bool HasCancelHandler
    {
        get
        {
            if (onCancel == null)
            {
                return false;
            }
            for (int i = 0; i < onCancel.GetPersistentEventCount(); i++)
            {
                if (onCancel.GetPersistentTarget(i) != null && !string.IsNullOrEmpty(onCancel.GetPersistentMethodName(i)))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Adds a runtime B handler (see <see cref="HasCancelHandler" />).</summary>
    public void AddCancelListener(UnityAction action)
    {
        if (onCancel == null)
        {
            onCancel = new UnityEvent();
        }
        onCancel.AddListener(action);
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
        RefreshOpenState();
    }

    private void OnDisable()
    {
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
        Unregister();
    }

    private void Update()
    {
        RefreshOpenState();

        if (!registeredOpen || !IsTop || !InputDeviceWatcher.GamepadActive || EventSystem.current == null)
        {
            return;
        }

        if (!disableCancel && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame && lastCancelFrame != Time.frameCount)
        {
            lastCancelFrame = Time.frameCount;
            onCancel?.Invoke();
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        Selectable selectedControl = selected != null ? selected.GetComponent<Selectable>() : null;
        // A selection left on a control that can't be used (e.g. inside a dialog that just faded out) is as good as dead.
        bool selectionDead = selected == null || !selected.activeInHierarchy || (selectedControl != null && !selectedControl.IsInteractable());
        bool selectionEscaped = !selectionDead && SelectionEscaped(selected.transform);

        if (selectionDead || selectionEscaped)
        {
            RestoreFocus();
        }
        else
        {
            lastSelected = selected;
        }
    }

    /// <summary>
    ///     Outside <see cref="restrictTo" /> when set; otherwise inside a panel underneath this one and
    ///     not inside this one (pad navigation reaching through the pause menu to the cards behind).
    /// </summary>
    private bool SelectionEscaped(Transform selected)
    {
        if (restrictTo != null)
        {
            return !selected.IsChildOf(restrictTo);
        }
        if (selected.IsChildOf(transform))
        {
            return false;
        }
        for (int i = 0; i < openStack.Count - 1; i++)
        {
            if (openStack[i] != null && selected.IsChildOf(openStack[i].transform))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Tracks this panel opening/closing (activation or its CanvasGroup going interactable).</summary>
    private void RefreshOpenState()
    {
        bool open = isActiveAndEnabled && PanelOpen;
        if (open && !registeredOpen)
        {
            registeredOpen = true;
            openStack.Remove(this);
            openStack.Add(this);
            lastSelected = null;
            if (InputDeviceWatcher.GamepadActive)
            {
                SelectDefault();
            }
        }
        else if (!open && registeredOpen)
        {
            Unregister();
        }
    }

    private void Unregister()
    {
        if (!registeredOpen)
        {
            return;
        }
        bool wasTop = IsTop;
        registeredOpen = false;
        openStack.Remove(this);
        if (!wasTop || openStack.Count == 0 || !InputDeviceWatcher.GamepadActive)
        {
            return;
        }
        MenuFocusController revealed = openStack[openStack.Count - 1];
        if (revealed != null)
        {
            revealed.RestoreFocus();
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
            if (registeredOpen && IsTop)
            {
                RestoreFocus();
            }
        }
        else
        {
            // Mouse users navigate by hover/click — a lingering pad selection just draws a stray highlight.
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>Replaces the default control, for panels whose rows are built at runtime, and focuses it under a pad.</summary>
    public void SetDefaultSelectable(Selectable selectable)
    {
        defaultSelectable = selectable;
        if (InputDeviceWatcher.GamepadActive && isActiveAndEnabled)
        {
            SelectDefault();
        }
    }

    /// <summary>Focuses the default control — also callable by panel code after it swaps tab content.</summary>
    public void SelectDefault()
    {
        if (EventSystem.current == null || defaultSelectable == null || !defaultSelectable.isActiveAndEnabled || !PanelOpen)
        {
            return;
        }
        // A panel behind another must not pull focus out of the one in front.
        if (registeredOpen && !IsTop)
        {
            return;
        }
        EventSystem.current.SetSelectedGameObject(defaultSelectable.gameObject);
    }

    /// <summary>Returns focus to this panel's last pad selection if it's still usable, else to the default.</summary>
    private void RestoreFocus()
    {
        if (EventSystem.current == null)
        {
            return;
        }
        Selectable last = lastSelected != null ? lastSelected.GetComponent<Selectable>() : null;
        if (last != null && last.isActiveAndEnabled && last.IsInteractable() && !SelectionEscaped(last.transform))
        {
            EventSystem.current.SetSelectedGameObject(lastSelected);
            return;
        }
        SelectDefault();
    }
}
