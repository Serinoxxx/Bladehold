using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The main menu's save slot picker, shown by Play: one <see cref="SaveSlotView" /> card per
///     <see cref="SaveSystem.SlotCount" /> slot, built from disk each time the screen opens. A used slot
///     continues, an empty one starts a New Game there, and a used slot's delete button opens the
///     <see cref="SaveSlotDeleteDialog" /> warning. Picking a slot makes it <see cref="SaveSystem.ActiveSlot" />
///     and hands off to <see cref="onSlotReady" /> (the menu's loading screen).
/// </summary>
public class SaveSlotsScreen : MonoBehaviour
{
    [SerializeField] private SaveSlotView slotPrefab;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private SaveSlotDeleteDialog deleteDialog;
    [Tooltip("This screen's own focus controller; paused while the delete dialog is open so pad B only closes the dialog.")]
    [SerializeField] private MenuFocusController focus;
    [SerializeField] private Button backButton;

    /// <summary>Raised once a slot is active and the game should load. Set by the main menu.</summary>
    public Action onSlotReady;

    /// <summary>Back button and pad B. Set by the main menu.</summary>
    public Action onBack;

    private SaveSlotView[] views = new SaveSlotView[0];
    private bool choosing;
    private bool anyError;
    private bool validated;

    private void OnValidate()
    {
        if (focus == null)
        {
            focus = GetComponent<MenuFocusController>();
        }
    }

    private void Validate()
    {
        if (validated)
        {
            return;
        }
        validated = true;
        if (slotPrefab == null || slotContainer == null || deleteDialog == null || focus == null || backButton == null)
        {
            Debug.LogError("[SaveSlotsScreen] Slot prefab, container, delete dialog, focus controller or back button is not assigned.", this);
            anyError = true;
            return;
        }
        backButton.onClick.AddListener(Back);
    }

    private void OnEnable()
    {
        Validate();
        if (anyError)
        {
            return;
        }
        choosing = false;
        focus.enabled = true;
        Rebuild(0);
    }

    /// <summary>MenuFocusController.onCancel (pad B) and the Back button.</summary>
    public void Back()
    {
        if (choosing || deleteDialog.IsOpen)
        {
            return;
        }
        onBack?.Invoke();
    }

    private void Rebuild(int focusSlot)
    {
        foreach (Transform child in slotContainer)
        {
            Destroy(child.gameObject);
        }

        views = new SaveSlotView[SaveSystem.SlotCount];
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            SaveSlotView view = Instantiate(slotPrefab, slotContainer);
            view.name = $"SaveSlot_{i + 1}";
            view.Setup(i, SaveSystem.PeekSlot(i), HandleChosen, HandleDeleteRequested);
            views[i] = view;
        }

        focus.SetDefaultSelectable(views[Mathf.Clamp(focusSlot, 0, views.Length - 1)].CardButton);
    }

    private void HandleChosen(int slot)
    {
        if (choosing || deleteDialog.IsOpen)
        {
            return;
        }
        choosing = true;
        foreach (SaveSlotView view in views)
        {
            view.CardButton.interactable = false;
            view.DeleteButton.interactable = false;
        }

        if (SaveSystem.SlotExists(slot))
        {
            SaveSystem.SelectSlot(slot);
        }
        else
        {
            SaveSystem.CreateSlot(slot);
        }
        onSlotReady?.Invoke();
    }

    private void HandleDeleteRequested(int slot)
    {
        if (choosing || deleteDialog.IsOpen)
        {
            return;
        }
        focus.enabled = false;
        deleteDialog.Show(slot, SaveSystem.PeekSlot(slot),
            () =>
            {
                SaveSystem.DeleteSlot(slot);
                focus.enabled = true;
                Rebuild(slot);
            },
            () =>
            {
                focus.enabled = true;
                focus.SetDefaultSelectable(views[slot].CardButton);
            });
    }
}
