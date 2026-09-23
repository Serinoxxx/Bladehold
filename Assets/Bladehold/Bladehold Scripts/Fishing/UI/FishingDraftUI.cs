using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     3-card modal popup presented when leveling up during the Fishing Minigame.
///     Pauses time (Time.timeScale = 0) and applies chosen upgrade to FishingUpgradeManager.
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

    [Header("Modal Overlay")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private CardSlotUI[] cardSlots = new CardSlotUI[3];

    private List<FishingUpgradeCardInfo> currentChoices = new List<FishingUpgradeCardInfo>();

    private void Awake()
    {
        if (modalPanel != null) modalPanel.SetActive(false);

        for (int i = 0; i < cardSlots.Length; i++)
        {
            int index = i;
            if (cardSlots[i].selectButton != null)
            {
                cardSlots[i].selectButton.onClick.AddListener(() => OnCardSelected(index));
            }
        }
    }

    public void OpenDraft()
    {
        if (FishingUpgradeManager.Instance == null) return;

        currentChoices = FishingUpgradeManager.Instance.RollDraftChoices(3);
        if (currentChoices.Count == 0)
        {
            // All maxed out
            return;
        }

        // Pause minigame
        Time.timeScale = 0f;
        if (modalPanel != null) modalPanel.SetActive(true);

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
    }

    private void OnCardSelected(int index)
    {
        if (index >= 0 && index < currentChoices.Count)
        {
            FishingUpgradeType chosenType = currentChoices[index].type;
            if (FishingUpgradeManager.Instance != null)
            {
                FishingUpgradeManager.Instance.ApplyUpgrade(chosenType);
            }
        }

        CloseDraft();
    }

    public void CloseDraft()
    {
        if (modalPanel != null) modalPanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
