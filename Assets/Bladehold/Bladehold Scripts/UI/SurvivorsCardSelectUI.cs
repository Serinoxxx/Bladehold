using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Wave Choice (plan 15)")]
    [Tooltip("Row holding the three skill cards; hidden while the wave choice is open.")]
    [SerializeField] private GameObject skillCardsRow;

    [Tooltip("Empty layout row the wave cards are instantiated into.")]
    [SerializeField] private RectTransform waveCardsRow;

    [Tooltip("WaveCard.prefab, one per offered wave card.")]
    [SerializeField] private WaveCardUI waveCardPrefab;

    [Tooltip("Pad focus for the modal; the first wave card becomes its default.")]
    [SerializeField] private MenuFocusController focusController;

    [Header("Reroll")]
    [Tooltip("Rerolls all three skill cards; shown only on a draft opened with rerolls (2+ skull wave cards).")]
    [SerializeField] private Button rerollButton;

    [Tooltip("The reroll button's label, e.g. 'Reroll (1)'.")]
    [SerializeField] private TextMeshProUGUI rerollLabel;

    [Header("Sidebar Reference")]
    [Tooltip("Right-side player info and acquired skills sidebar.")]
    [SerializeField] private SurvivorsPlayerInfoSidebarUI sidebar;

    [Header("Icon Lookup (optional)")]
    [Tooltip("Central icons asset for resolving draft card icons.")]
    [SerializeField] private SkillTreeIconsSO iconsConfig;

    [Header("Click Protection & Transition")]
    [Tooltip("Delay in seconds before cards become interactable after modal opens (default: 0.5s).")]
    [SerializeField] private float clickDelaySeconds = 0.5f;

    [Tooltip("Delay in seconds after selecting the FINAL card before fade-out starts (default: 0.2s).")]
    [SerializeField] private float finalSelectionDelaySeconds = 0.2f;

    [Tooltip("Duration in seconds of the quick fade-out on final card selection (default: 0.15s).")]
    [SerializeField] private float finalFadeDurationSeconds = 0.15f;

    private readonly List<SkillNode> currentOfferedNodes = new List<SkillNode>();
    private readonly List<WaveCardUI> spawnedWaveCards = new List<WaveCardUI>();
    private readonly List<WaveCard> offeredWaveCards = new List<WaveCard>();
    private Action<WaveCard> onWaveCardPicked;
    private WaveCard pickedWaveCard;
    private bool waveChoiceOpen;
    private float modalOpenedUnscaledTime;
    private Coroutine enableButtonsCoroutine;
    private Coroutine closeRoutine;
    private bool hasBanishedThisDraft = false;
    private int rerollsRemaining;

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
        if (sidebar != null)
        {
            // Big-icon, inspectable skill rows here; the death screen's sidebar keeps its compact rows.
            sidebar.EnableDraftLayout();
        }

        if (iconsConfig == null)
        {
            iconsConfig = Resources.Load<SkillTreeIconsSO>("SkillTreeIcons");
        }

        if (skillCardsRow == null || waveCardsRow == null || waveCardPrefab == null)
        {
            Debug.LogError("[SurvivorsCardSelectUI] Wave choice refs (skillCardsRow, waveCardsRow, waveCardPrefab) are not assigned; OpenWaveChoice can't show cards.", this);
        }
        if (waveCardsRow != null)
        {
            waveCardsRow.gameObject.SetActive(false);
        }

        if (rerollButton == null)
        {
            Debug.LogError("[SurvivorsCardSelectUI] rerollButton is not assigned; drafts with rerolls can't offer them.", this);
        }
        else
        {
            rerollButton.onClick.AddListener(OnRerollClicked);
            rerollButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private Action onDraftCompletedCallback;
    private DraftCategory? activeCategory;

    /// <summary>
    ///     Opens the card selection modal, pauses gameplay, and populates 3 card choices.
    ///     Can be targeted to a specific DraftCategory (Weapon or Elemental). <paramref name="rerolls" /> shows
    ///     the reroll button, which swaps all three cards for fresh ones.
    /// </summary>
    public void OpenDraft(DraftCategory? category = null, Action onComplete = null, int rerolls = 0)
    {
        if (SurvivorsCardSelector.Instance == null)
        {
            SurvivorsCardSelector found = FindAnyObjectByType<SurvivorsCardSelector>();
            if (found == null)
            {
                GameObject scObj = new GameObject("SurvivorsCardSelector");
                scObj.AddComponent<SurvivorsCardSelector>();
            }
        }

        activeCategory = category;
        onDraftCompletedCallback = onComplete;

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
        rerollsRemaining = Mathf.Max(0, rerolls);
        waveChoiceOpen = false;
        ShowWaveRow(false);
        RefreshRerollButton();

        if (headerText != null)
        {
            string catName = category switch
            {
                DraftCategory.Weapon => Loc.Get("draft.header.weapon", "Weapon Upgrade"),
                DraftCategory.Elemental => Loc.Get("draft.header.elemental", "Elemental Upgrade"),
                _ => Loc.Get("draft.header.upgrade", "Upgrade Draft")
            };
            // Same ink colour as the wave choice header; the subtitle at 60% stays readable on the parchment.
            headerText.text = $"{catName}\n<size=60%>{Loc.Get("draft.header.subtitle", "Choose 1 of 3 upgrades")}</size>";
        }

        List<SkillNode> offered = SurvivorsCardSelector.Instance.GetRandomSkillCards(3, category: activeCategory);
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
        FitSidebar();

        modalOpenedUnscaledTime = Time.unscaledTime;
        if (enableButtonsCoroutine != null)
        {
            StopCoroutine(enableButtonsCoroutine);
        }
        enableButtonsCoroutine = StartCoroutine(EnableButtonsAfterDelay(clickDelaySeconds));

        CursorLockManager.SetUnlock("SurvivorsLevelUp", true);
    }

    /// <summary>
    ///     Opens the between-wave choice (plan 15): pauses, shows one <see cref="WaveCardUI" /> per offered
    ///     card, and calls <paramref name="onPicked" /> with the chosen card after the modal fades out.
    ///     Same pause, click guard and fade as <see cref="OpenDraft" />; no banish.
    /// </summary>
    public void OpenWaveChoice(IReadOnlyList<WaveCard> waveCards, Action<WaveCard> onPicked)
    {
        if (waveCards == null || waveCards.Count == 0 || waveCardsRow == null || waveCardPrefab == null)
        {
            Debug.LogError("[SurvivorsCardSelectUI] OpenWaveChoice called with no cards or missing wave card refs; picking nothing.", this);
            onPicked?.Invoke(waveCards != null && waveCards.Count > 0 ? waveCards[0] : null);
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

        waveChoiceOpen = true;
        rerollsRemaining = 0;
        RefreshRerollButton();
        onWaveCardPicked = onPicked;
        pickedWaveCard = null;
        onDraftCompletedCallback = null;
        activeCategory = null;

        if (headerText != null)
        {
            headerText.text = Loc.Get("wave.choice.header", "Choose your next battle");
        }

        ShowWaveRow(true);
        offeredWaveCards.Clear();
        offeredWaveCards.AddRange(waveCards);
        foreach (WaveCardUI old in spawnedWaveCards)
        {
            if (old != null) Destroy(old.gameObject);
        }
        spawnedWaveCards.Clear();

        for (int i = 0; i < offeredWaveCards.Count; i++)
        {
            WaveCardUI view = Instantiate(waveCardPrefab, waveCardsRow);
            int index = i; // closure capture
            view.SetData(offeredWaveCards[i], () => OnWaveCardClicked(index));
            spawnedWaveCards.Add(view);
        }

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
        FitSidebar();

        if (focusController != null && spawnedWaveCards.Count > 0)
        {
            focusController.SetDefaultSelectable(spawnedWaveCards[0].SelectButton);
        }

        modalOpenedUnscaledTime = Time.unscaledTime;
        if (enableButtonsCoroutine != null)
        {
            StopCoroutine(enableButtonsCoroutine);
        }
        enableButtonsCoroutine = StartCoroutine(EnableButtonsAfterDelay(clickDelaySeconds));

        CursorLockManager.SetUnlock("SurvivorsLevelUp", true);
    }

    /// <summary>Grows the hero panel as far as the space beside the visible card row allows (call once the modal is active).</summary>
    private void FitSidebar()
    {
        if (sidebar == null) return;
        RectTransform row = waveChoiceOpen
            ? waveCardsRow
            : skillCardsRow != null ? skillCardsRow.transform as RectTransform : null;
        sidebar.FitBeside(row);
    }

    private void OnWaveCardClicked(int index)
    {
        if (!waveChoiceOpen || pickedWaveCard != null) return;
        if (Time.unscaledTime - modalOpenedUnscaledTime < clickDelaySeconds) return;
        if (index < 0 || index >= offeredWaveCards.Count) return;

        pickedWaveCard = offeredWaveCards[index];

        if (enableButtonsCoroutine != null)
        {
            StopCoroutine(enableButtonsCoroutine);
            enableButtonsCoroutine = null;
        }
        SetButtonsInteractable(false);
        closeRoutine = StartCoroutine(CloseModalWithFadeRoutine());
    }

    private void ShowWaveRow(bool showWave)
    {
        if (skillCardsRow != null) skillCardsRow.SetActive(!showWave);
        if (waveCardsRow != null) waveCardsRow.gameObject.SetActive(showWave);
        if (!showWave && focusController != null && cards.Length > 0 && cards[0] != null)
        {
            focusController.SetDefaultSelectable(cards[0].GetComponent<Selectable>());
        }
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
        foreach (WaveCardUI view in spawnedWaveCards)
        {
            if (view != null) view.SetInteractable(interactable);
        }
        if (rerollButton != null) rerollButton.interactable = interactable && rerollsRemaining > 0;
    }

    private void RefreshRerollButton()
    {
        if (rerollButton == null) return;
        bool show = !waveChoiceOpen && rerollsRemaining > 0;
        rerollButton.gameObject.SetActive(show);
        if (show && rerollLabel != null)
        {
            rerollLabel.text = Loc.Get("draft.reroll", "Reroll ({0})").Replace("{0}", rerollsRemaining.ToString());
        }
    }

    /// <summary>Swaps all three offered cards for ones not just shown (falling back to any eligible card when the pool is thin).</summary>
    private void OnRerollClicked()
    {
        if (waveChoiceOpen || rerollsRemaining <= 0 || closeRoutine != null) return;
        if (Time.unscaledTime - modalOpenedUnscaledTime < clickDelaySeconds) return;
        if (SurvivorsCardSelector.Instance == null) return;

        List<SkillNode> fresh = SurvivorsCardSelector.Instance.GetRandomSkillCards(3, currentOfferedNodes, activeCategory);
        if (fresh.Count < 3)
        {
            List<SkillNode> exclude = new List<SkillNode>(fresh);
            fresh.AddRange(SurvivorsCardSelector.Instance.GetRandomSkillCards(3 - fresh.Count, exclude, activeCategory));
        }
        if (fresh.Count == 0) return;

        rerollsRemaining--;
        currentOfferedNodes.Clear();
        currentOfferedNodes.AddRange(fresh);
        PopulateCards(currentOfferedNodes);
        // A pad focused on the button when it hides falls back to the first card (MenuFocusController).
        RefreshRerollButton();
    }

    private IEnumerator EnableButtonsAfterDelay(float delay)
    {
        SetButtonsInteractable(false);
        yield return new WaitForSecondsRealtime(delay);
        SetButtonsInteractable(true);
        enableButtonsCoroutine = null;
    }

    /// <summary>
    ///     Resolves a sprite icon by name via configured SkillTreeIconsSO, DraftUpgradeService, or fallback.
    /// </summary>
    public Sprite ResolveIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;

        if (iconsConfig != null)
        {
            Sprite s = iconsConfig.GetIcon(iconName);
            if (s != null) return s;
        }

        DraftUpgradeService draftService = DraftUpgradeService.Instance ?? DraftUpgradeService.GetOrCreateInstance();
        if (draftService != null)
        {
            Sprite s = draftService.GetIcon(iconName);
            if (s != null) return s;
        }

        return null;
    }

    /// <summary>
    ///     The icon tint for a card's element (Fire/Ice/Lightning) from the icons asset, or null to keep the
    ///     card's default icon colour.
    /// </summary>
    public Color? ResolveIconTint(string element)
    {
        if (string.IsNullOrEmpty(element)) return null;

        if (iconsConfig != null)
        {
            return iconsConfig.TryGetElementTint(element, out Color tint) ? tint : (Color?)null;
        }

        DraftUpgradeService draftService = DraftUpgradeService.Instance ?? DraftUpgradeService.GetOrCreateInstance();
        if (draftService != null && draftService.TryGetElementIconTint(element, out Color serviceTint))
        {
            return serviceTint;
        }

        return null;
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

                int currentLevel = RunSession.GetUpgradeLevel(node.id);

                Sprite icon = ResolveIcon(node.iconName);

                int index = i; // Closure capture
                cardUI.SetData(
                    node,
                    currentLevel,
                    icon,
                    () => OnCardClicked(index),
                    () => OnCardBanished(index),
                    canBanish: !hasBanishedThisDraft,
                    iconTint: ResolveIconTint(node.element));
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

            SkillNode replacement = SurvivorsCardSelector.Instance.GetSingleReplacementCard(currentOfferedNodes, activeCategory);
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

        DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
        DraftUpgradeDefinition draftDef = draftService != null ? draftService.GetById(chosenNode.id) : null;
        if (draftDef != null)
        {
            draftService.ApplyUpgrade(draftDef);
        }
        else
        {
            Debug.LogError($"[SurvivorsCardSelectUI] No draft definition for picked card '{chosenNode.id}'.");
        }

        if (sidebar != null)
        {
            sidebar.RefreshSidebar();
        }

        // All drafts complete: start slight delay and quick fade out before closing modal
        if (enableButtonsCoroutine != null)
        {
            StopCoroutine(enableButtonsCoroutine);
            enableButtonsCoroutine = null;
        }

        SetButtonsInteractable(false);
        rerollsRemaining = 0;
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

        if (waveChoiceOpen)
        {
            waveChoiceOpen = false;
            ShowWaveRow(false);
            Action<WaveCard> waveCallback = onWaveCardPicked;
            WaveCard picked = pickedWaveCard;
            onWaveCardPicked = null;
            pickedWaveCard = null;
            waveCallback?.Invoke(picked);
            yield break;
        }

        Action callback = onDraftCompletedCallback;
        onDraftCompletedCallback = null;
        activeCategory = null;
        callback?.Invoke();
    }
}
