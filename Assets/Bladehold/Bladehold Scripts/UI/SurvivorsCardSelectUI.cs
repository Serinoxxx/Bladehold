using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
///     3-Card Level-Up Selection Modal for Survivors mode.
///     Stays active on the Canvas to listen for level-up events, opening and populating
///     the card modal overlay panel using <see cref="SurvivorsCardUI"/> components when the player levels up.
/// </summary>
public class SurvivorsCardSelectUI : MonoBehaviour
{
    [Header("Modal Containers")]
    [Tooltip("Child modal overlay panel containing the cards.")]
    [SerializeField] private GameObject modalPanel;

    [Tooltip("Header text displaying 'LEVEL UP!' or level number.")]
    [SerializeField] private TextMeshProUGUI headerText;

    [Header("Card UI References")]
    [Tooltip("Array of 3 SurvivorsCardUI components (attached to card prefabs).")]
    [SerializeField] private SurvivorsCardUI[] cards = new SurvivorsCardUI[3];

    [Header("Click Protection")]
    [Tooltip("Delay in seconds before cards become interactable after modal opens (default: 0.5s).")]
    [SerializeField] private float clickDelaySeconds = 0.5f;

    private readonly List<SkillNode> currentOfferedNodes = new List<SkillNode>();
    private float modalOpenedUnscaledTime;
    private Coroutine enableButtonsCoroutine;

    private void Awake()
    {
        // Subscribe in Awake so event listeners are bound regardless of start timing
        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.OnLevelUp += HandleLevelUp;
        }
    }

    private void Start()
    {
        if (modalPanel != null)
        {
            modalPanel.SetActive(false);
        }

        // Fallback subscription if Instance wasn't ready during Awake
        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.OnLevelUp -= HandleLevelUp;
            SurvivorsLevelSystem.Instance.OnLevelUp += HandleLevelUp;
        }
    }

    private void OnDestroy()
    {
        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    public void HandleLevelUp(int newLevel)
    {
        Debug.Log($"[SurvivorsCardSelectUI] HandleLevelUp received for Level {newLevel}!");

        if (SurvivorsCardSelector.Instance == null)
        {
            Debug.LogError("[SurvivorsCardSelectUI] SurvivorsCardSelector instance missing!");
            return;
        }

        List<SkillNode> offered = SurvivorsCardSelector.Instance.GetRandomSkillCards(3);
        currentOfferedNodes.Clear();
        currentOfferedNodes.AddRange(offered);

        if (headerText != null)
        {
            headerText.text = $"LEVEL UP! (LEVEL {newLevel})";
        }

        PopulateCards(offered);

        if (modalPanel != null)
        {
            modalPanel.SetActive(true);
        }

        modalOpenedUnscaledTime = Time.unscaledTime;
        if (enableButtonsCoroutine != null)
        {
            StopCoroutine(enableButtonsCoroutine);
        }
        enableButtonsCoroutine = StartCoroutine(EnableButtonsAfterDelay(clickDelaySeconds));

        CursorLockManager.SetUnlock("SurvivorsLevelUp", true);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null)
            {
                cards[i].SetInteractable(interactable);
            }
        }
    }

    private IEnumerator EnableButtonsAfterDelay(float delay)
    {
        SetButtonsInteractable(false);
        yield return new WaitForSecondsRealtime(delay);
        SetButtonsInteractable(true);
        enableButtonsCoroutine = null;
    }

    private void PopulateCards(List<SkillNode> offeredNodes)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            SurvivorsCardUI cardUI = cards[i];
            if (cardUI == null) continue;

            if (i < offeredNodes.Count)
            {
                SkillNode node = offeredNodes[i];
                cardUI.gameObject.SetActive(true);

                int currentLevel = SkillTreeService.Instance != null ? SkillTreeService.Instance.GetLevel(node) : 0;
                Sprite icon = SkillTreeService.Instance != null && SkillTreeService.Instance.Tree != null
                    ? SkillTreeService.Instance.Tree.GetIcon(node.iconName)
                    : null;

                int index = i; // Closure capture
                cardUI.SetData(node, currentLevel, icon, () => OnCardClicked(index));
            }
            else
            {
                // Hide unused card slots if fewer than 3 skills available
                cardUI.gameObject.SetActive(false);
            }
        }
    }

    private void OnCardClicked(int cardIndex)
    {
        if (Time.unscaledTime - modalOpenedUnscaledTime < clickDelaySeconds)
        {
            return;
        }

        if (cardIndex < 0 || cardIndex >= currentOfferedNodes.Count)
        {
            return;
        }

        SkillNode chosenNode = currentOfferedNodes[cardIndex];

        if (enableButtonsCoroutine != null)
        {
            StopCoroutine(enableButtonsCoroutine);
            enableButtonsCoroutine = null;
        }

        if (modalPanel != null)
        {
            modalPanel.SetActive(false);
        }

        CursorLockManager.SetUnlock("SurvivorsLevelUp", false);

        if (SurvivorsCardSelector.Instance != null)
        {
            SurvivorsCardSelector.Instance.SelectCard(chosenNode);
        }
    }
}
