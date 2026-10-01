using System;
using System.Globalization;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     One card on the <see cref="SaveSlotsScreen" />: a used slot shows when it was created, total time
///     played, when it was last played and a short progress summary, with Continue on the card and a
///     delete button; an empty slot shows New Game. Pure display — the screen decides what a click does.
///     Hover/focus grows the card and plays the draft cards' paper sound; picking it plays their select
///     sound (<see cref="WaveCardUI" /> pattern: MMF_Players on the prefab, forced to unscaled time).
/// </summary>
public class SaveSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Button cardButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private TMP_Text slotTitleText;
    [SerializeField] private TMP_Text actionText;

    [Header("Used slot")]
    [SerializeField] private GameObject usedGroup;
    [SerializeField] private TMP_Text createdText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text lastPlayedText;
    [SerializeField] private TMP_Text summaryText;

    [Header("Empty slot")]
    [SerializeField] private GameObject emptyGroup;

    [Header("Feedbacks")]
    [SerializeField] private MMF_Player hoverEnterFeedback;
    [SerializeField] private MMF_Player hoverExitFeedback;
    [SerializeField] private MMF_Player selectFeedback;

    public Button CardButton => cardButton;
    public Button DeleteButton => deleteButton;

    private int slot;
    private Action<int> onChosen;
    private Action<int> onDeleteRequested;

    private void OnValidate()
    {
        if (cardButton == null)
        {
            cardButton = GetComponent<Button>();
        }
    }

    private void Awake()
    {
        if (cardButton == null || deleteButton == null || slotTitleText == null || actionText == null
            || usedGroup == null || emptyGroup == null || createdText == null || playTimeText == null
            || lastPlayedText == null || summaryText == null)
        {
            Debug.LogError("[SaveSlotView] A reference is not assigned on the slot prefab.", this);
            return;
        }
        if (hoverEnterFeedback == null || hoverExitFeedback == null || selectFeedback == null)
        {
            Debug.LogError("[SaveSlotView] Hover/select MMF_Player feedbacks are not assigned on the slot prefab.", this);
        }
        ForceUnscaledTime(hoverEnterFeedback);
        ForceUnscaledTime(hoverExitFeedback);
        ForceUnscaledTime(selectFeedback);

        cardButton.onClick.AddListener(HandleClick);
        deleteButton.onClick.AddListener(() => onDeleteRequested?.Invoke(slot));
    }

    /// <summary>Fills the card for <paramref name="slotIndex" />; <paramref name="data" /> is null for an empty slot.</summary>
    public void Setup(int slotIndex, SaveData data, Action<int> chosen, Action<int> deleteRequested)
    {
        slot = slotIndex;
        onChosen = chosen;
        onDeleteRequested = deleteRequested;

        bool used = data != null;
        slotTitleText.text = string.Format(Loc.Get("saveslots.slot", "Slot {0}"), slotIndex + 1);
        usedGroup.SetActive(used);
        emptyGroup.SetActive(!used);
        deleteButton.gameObject.SetActive(used);
        actionText.text = used ? Loc.Get("saveslots.continue", "Continue") : Loc.Get("saveslots.new_game", "New Game");

        if (!used)
        {
            return;
        }

        createdText.text = FormatDate(data.createdUtcTicks);
        playTimeText.text = FormatPlayTime(data.playTimeSeconds);
        lastPlayedText.text = FormatDate(data.lastPlayedUtcTicks);
        summaryText.text = !data.tutorialCompleted
            ? Loc.Get("saveslots.summary_tutorial", "In the tutorial")
            : string.Format(Loc.Get("saveslots.summary", "Tier {0}  ·  {1} Goblin Blood"), data.unlockedMetaTier, data.goblinBlood);
    }

    private void HandleClick()
    {
        if (!cardButton.interactable)
        {
            return;
        }
        if (selectFeedback != null) selectFeedback.PlayFeedbacks();
        onChosen?.Invoke(slot);
    }

    public void OnPointerEnter(PointerEventData eventData) => PlayHover(true);
    public void OnPointerExit(PointerEventData eventData) => PlayHover(false);
    public void OnSelect(BaseEventData eventData) => PlayHover(true);
    public void OnDeselect(BaseEventData eventData) => PlayHover(false);

    private bool hovered;

    private void PlayHover(bool enter)
    {
        // Pointer-over and pad focus can both report on the same card; only react to real state changes.
        if (enter == hovered || cardButton == null || !cardButton.interactable)
        {
            return;
        }
        hovered = enter;
        MMF_Player player = enter ? hoverEnterFeedback : hoverExitFeedback;
        if (player != null) player.PlayFeedbacks();
    }

    private static void ForceUnscaledTime(MMF_Player player)
    {
        if (player == null) return;
        player.ForceTimescaleMode = true;
        player.ForcedTimescaleMode = TimescaleModes.Unscaled;
        player.PlayerTimescaleMode = TimescaleModes.Unscaled;
    }

    public static string FormatPlayTime(double seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        if (t.TotalHours >= 1d)
        {
            return string.Format(Loc.Get("saveslots.time_hm", "{0}h {1}m"), (int)t.TotalHours, t.Minutes);
        }
        return t.TotalMinutes >= 1d
            ? string.Format(Loc.Get("saveslots.time_m", "{0}m"), (int)t.TotalMinutes)
            : Loc.Get("saveslots.time_under_minute", "<1m");
    }

    private static string FormatDate(long utcTicks)
    {
        if (utcTicks <= 0)
        {
            return "—";
        }
        DateTime local = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("d MMM yyyy  HH:mm", CultureInfo.CurrentCulture);
    }
}
