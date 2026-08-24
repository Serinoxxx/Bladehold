using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
///     3-Card Level-Up Selection Modal for Survivors mode.
///     Stays active on the Canvas, opening and populating
///     the card modal overlay panel using <see cref="SurvivorsCardUI"/> components when triggered.
///     Supports stacked level drafts and embeds the player info/skills sidebar.
/// </summary>
public class SurvivorsCardSelectUI : MonoBehaviour
{
    public static SurvivorsCardSelectUI Instance { get; private set; }

    [Header("Modal Containers")]
    [Tooltip("Child modal overlay panel containing the cards.")]
    [SerializeField] private GameObject modalPanel;

    [Tooltip("Header text displaying 'LEVEL {X}' in big text.")]
    [SerializeField] private TextMeshProUGUI headerText;

    [Header("Card UI References")]
    [Tooltip("Array of 3 SurvivorsCardUI components (attached to card prefabs).")]
    [SerializeField] private SurvivorsCardUI[] cards = new SurvivorsCardUI[3];

    [Header("Sidebar Reference")]
    [Tooltip("Right-side player info and acquired skills sidebar.")]
    [SerializeField] private SurvivorsPlayerInfoSidebarUI sidebar;

    [Header("Click Protection & Transition")]
    [Tooltip("Delay in seconds before cards become interactable after modal opens (default: 0.5s).")]
    [SerializeField] private float clickDelaySeconds = 0.5f;

    [Tooltip("Delay in seconds after selecting the FINAL card before fade-out starts (default: 0.2s).")]
    [SerializeField] private float finalSelectionDelaySeconds = 0.2f;

    [Tooltip("Duration in seconds of the quick fade-out on final card selection (default: 0.15s).")]
    [SerializeField] private float finalFadeDurationSeconds = 0.15f;

    private readonly List<SkillNode> currentOfferedNodes = new List<SkillNode>();
    private float modalOpenedUnscaledTime;
    private Coroutine enableButtonsCoroutine;
    private Coroutine closeRoutine;
    private bool hasBanishedThisDraft = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (modalPanel != null)
        {
            modalPanel.SetActive(false);
        }

        if (sidebar == null)
        {
            sidebar = GetComponentInChildren<SurvivorsPlayerInfoSidebarUI>(true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    ///     Opens the card selection modal, pauses gameplay, and populates 3 card choices.
    /// </summary>
    public void OpenDraft()
    {
        if (SurvivorsCardSelector.Instance == null)
        {
            Debug.LogError("[SurvivorsCardSelectUI] SurvivorsCardSelector instance missing!");
            return;
        }

        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
            closeRoutine = null;
        }

        if (SurvivorsGameManager.Instance != null)
        {
            SurvivorsGameManager.Instance.PauseForCardSelection();
        }

        hasBanishedThisDraft = false;

        int currentLvl = SurvivorsLevelSystem.Instance != null ? SurvivorsLevelSystem.Instance.CurrentLevel : 1;
        int pending = SurvivorsLevelSystem.Instance != null ? SurvivorsLevelSystem.Instance.PendingDrafts : 1;

        if (headerText != null)
        {
            if (pending > 1)
            {
                headerText.text = $"LEVEL {currentLvl}\n<size=20><color=#FFD700>{pending} skill drafts remaining</color></size>";
            }
            else
            {
                headerText.text = $"LEVEL {currentLvl}";
            }
        }

        List<SkillNode> offered = SurvivorsCardSelector.Instance.GetRandomSkillCards(3);
        currentOfferedNodes.Clear();
        currentOfferedNodes.AddRange(offered);

        PopulateCards(offered);

        if (sidebar != null)
        {
            sidebar.RefreshSidebar();
        }

        if (modalPanel != null)
        {
            CanvasGroup cg = modalPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
            }
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
                cardUI.SetData(
                    node,
                    currentLevel,
                    icon,
                    () => OnCardClicked(index),
                    () => OnCardBanished(index),
                    canBanish: !hasBanishedThisDraft);
            }
            else
            {
                // Hide unused card slots if fewer than 3 skills available
                cardUI.gameObject.SetActive(false);
            }
        }
    }

    private void OnCardBanished(int cardIndex)
    {
        if (hasBanishedThisDraft || cardIndex < 0 || cardIndex >= currentOfferedNodes.Count)
        {
            return;
        }

        SkillNode target = currentOfferedNodes[cardIndex];
        if (target == null) return;

        hasBanishedThisDraft = true;
        if (SurvivorsCardSelector.Instance != null)
        {
            SurvivorsCardSelector.Instance.BanishCard(target.id);
            Debug.Log($"[SurvivorsCardSelectUI] Banished skill '{target.displayName}' (ID: {target.id}) for this run.");

            SkillNode replacement = SurvivorsCardSelector.Instance.GetSingleReplacementCard(currentOfferedNodes);
            if (replacement != null)
            {
                currentOfferedNodes[cardIndex] = replacement;
            }
        }

        PopulateCards(currentOfferedNodes);
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

        // Apply chosen card upgrade
        if (SkillTreeService.Instance != null)
        {
            SkillTreeService.Instance.ApplyFreePurchase(chosenNode.id);
            Debug.Log($"[SurvivorsCardSelectUI] Selected skill: '{chosenNode.displayName}' (ID: {chosenNode.id}). Granted level {SkillTreeService.Instance.GetLevel(chosenNode)}.");
        }

        // Consume one draft
        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.ConsumeDraft();
        }

        if (sidebar != null)
        {
            sidebar.RefreshSidebar();
        }

        // Check if more drafts are queued
        int remaining = SurvivorsLevelSystem.Instance != null ? SurvivorsLevelSystem.Instance.PendingDrafts : 0;
        if (remaining > 0)
        {
            // Re-roll and present the next 3 cards instantly
            OpenDraft();
            return;
        }

        // All drafts complete: start slight delay and quick fade out before closing modal
        if (enableButtonsCoroutine != null)
        {
            StopCoroutine(enableButtonsCoroutine);
            enableButtonsCoroutine = null;
        }

        SetButtonsInteractable(false);
        closeRoutine = StartCoroutine(CloseModalWithFadeRoutine());
    }

    private IEnumerator CloseModalWithFadeRoutine()
    {
        // Slight pause so the user sees/hears the final card selection feedback
        if (finalSelectionDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(finalSelectionDelaySeconds);
        }

        // Quickly fade out modal panel alpha
        CanvasGroup cg = modalPanel != null ? modalPanel.GetComponent<CanvasGroup>() : null;
        if (cg == null && modalPanel != null)
        {
            cg = modalPanel.AddComponent<CanvasGroup>();
        }

        if (cg != null && finalFadeDurationSeconds > 0f)
        {
            float elapsed = 0f;
            float startAlpha = cg.alpha;
            while (elapsed < finalFadeDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / finalFadeDurationSeconds);
                yield return null;
            }
            cg.alpha = 0f;
        }

        if (modalPanel != null)
        {
            modalPanel.SetActive(false);
            if (cg != null)
            {
                cg.alpha = 1f; // Restore full alpha for next draft modal
            }
        }

        CursorLockManager.SetUnlock("SurvivorsLevelUp", false);

        if (SurvivorsGameManager.Instance != null)
        {
            SurvivorsGameManager.Instance.ResumeFromCardSelection();
        }

        closeRoutine = null;
    }
}
