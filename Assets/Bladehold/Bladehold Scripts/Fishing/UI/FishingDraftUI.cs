using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     3-card modal popup presented when leveling up during the Fishing Minigame.
///     Pauses time (Time.timeScale = 0) and applies chosen upgrade to FishingUpgradeManager.
///     Level-ups queue: each one opens its own draft after the previous pick.
/// </summary>
public class FishingDraftUI : MonoBehaviour
{
    [System.Serializable]
    public class CardSlotUI
    {
        public GameObject root;
        public TMP_Text titleText;
        public TMP_Text levelText;
        public TMP_Text descText;
        public Button selectButton;
    }

    private const string CursorOwner = "FishingDraft";

    [Header("Modal Overlay")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private CardSlotUI[] cardSlots = new CardSlotUI[3];

    private List<FishingUpgradeCardInfo> currentChoices = new List<FishingUpgradeCardInfo>();
    private int pendingDrafts;
    private bool isOpen;
    private bool anyError;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (modalPanel != null) modalPanel.SetActive(false);

        for (int i = 0; i < cardSlots.Length; i++)
        {
            int index = i;
            if (cardSlots[i] != null && cardSlots[i].selectButton != null)
            {
                cardSlots[i].selectButton.onClick.AddListener(() => OnCardSelected(index));
            }
        }
    }

    private void Start()
    {
        if (modalPanel == null)
        {
            Debug.LogError("[FishingDraftUI] modalPanel is not assigned.", this);
            anyError = true;
        }

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null || cardSlots[i].root == null || cardSlots[i].selectButton == null)
            {
                Debug.LogError($"[FishingDraftUI] Card slot {i} is missing its root or selectButton.", this);
                anyError = true;
            }
        }
    }

    private void OnDestroy()
    {
        CursorLockManager.SetUnlock(CursorOwner, false);
    }

    /// <summary>Queues one level-up draft; opens now if no draft is showing.</summary>
    public void QueueDraft()
    {
        if (anyError) return;
        pendingDrafts++;
        if (!isOpen) OpenNextDraft();
    }

    /// <summary>Drops queued drafts (frenzy over) and closes any open one.</summary>
    public void CancelPendingDrafts()
    {
        pendingDrafts = 0;
        if (isOpen) CloseDraft();
    }

    private void OpenNextDraft()
    {
        if (FishingUpgradeManager.Instance == null)
        {
            Debug.LogError("[FishingDraftUI] No FishingUpgradeManager in the scene.", this);
            pendingDrafts = 0;
            return;
        }

        while (pendingDrafts > 0)
        {
            pendingDrafts--;
            currentChoices = FishingUpgradeManager.Instance.RollDraftChoices(3);
            if (currentChoices.Count > 0) break;
        }

        // All cards maxed out
        if (currentChoices.Count == 0) return;

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (i < currentChoices.Count)
            {
                cardSlots[i].root.SetActive(true);
                var choice = currentChoices[i];
                if (cardSlots[i].titleText != null) cardSlots[i].titleText.text = choice.title;
                if (cardSlots[i].levelText != null) cardSlots[i].levelText.text = $"Tier {choice.currentLevel + 1}/{choice.maxLevel}";
                if (cardSlots[i].descText != null) cardSlots[i].descText.text = choice.description;
            }
            else
            {
                cardSlots[i].root.SetActive(false);
            }
        }

        // Pause minigame
        isOpen = true;
        Time.timeScale = 0f;
        modalPanel.SetActive(true);
        CursorLockManager.SetUnlock(CursorOwner, true);

        // Gamepad: start with the first card selected
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(cardSlots[0].selectButton.gameObject);
        }
    }

    private void OnCardSelected(int index)
    {
        if (!isOpen) return;

        if (index >= 0 && index < currentChoices.Count && FishingUpgradeManager.Instance != null)
        {
            FishingUpgradeManager.Instance.ApplyUpgrade(currentChoices[index].type);
        }

        CloseDraft();
        if (pendingDrafts > 0) OpenNextDraft();
    }

    public void CloseDraft()
    {
        isOpen = false;
        currentChoices.Clear();
        if (modalPanel != null) modalPanel.SetActive(false);
        CursorLockManager.SetUnlock(CursorOwner, false);
        Time.timeScale = 1f;
    }
}
