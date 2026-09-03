using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Controller for the 3-Tier Permanent Meta-Progression UI opened via the Spirit NPC.
///     Displays Goblin Blood and Orcish Metal currencies prominently.
///     Features 3 horizontal tier rows, tier unlock buttons with Orcish Metal costs,
///     and individual perk purchase buttons with Goblin Blood costs.
/// </summary>
public class MetaUpgradesUI : MonoBehaviour
{
    public static MetaUpgradesUI Instance { get; private set; }

    [Header("Perk Data")]
    [SerializeField] private List<MetaPerkDefinitionSO> allPerks = new List<MetaPerkDefinitionSO>();

    [Header("UI Panels")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private Button closeButton;

    [Header("Currencies Display")]
    [SerializeField] private TMP_Text goblinBloodText;
    [SerializeField] private TMP_Text orcishMetalText;

    [Header("Tier Containers (Horizontal Rows)")]
    [SerializeField] private Transform tier1RowContainer;
    [SerializeField] private Transform tier2RowContainer;
    [SerializeField] private Transform tier3RowContainer;

    [Header("Tier Unlock Buttons")]
    [SerializeField] private Button unlockTier2Button;
    [SerializeField] private TMP_Text unlockTier2ButtonText;
    [SerializeField] private Button unlockTier3Button;
    [SerializeField] private TMP_Text unlockTier3ButtonText;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipBox;
    [SerializeField] private TMP_Text tooltipTitle;
    [SerializeField] private TMP_Text tooltipDescription;

    [Header("Perk Card Prefab (Optional / Fallback)")]
    [SerializeField] private GameObject perkCardPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (unlockTier2Button != null) unlockTier2Button.onClick.AddListener(() => UnlockTier(2, 5));
        if (unlockTier3Button != null) unlockTier3Button.onClick.AddListener(() => UnlockTier(3, 10));

        if (windowRoot != null) windowRoot.SetActive(false);
        if (tooltipBox != null) tooltipBox.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (windowRoot != null) windowRoot.SetActive(true);
        CursorLockManager.SetUnlock("MetaUpgrades", true);
        Time.timeScale = 0f;

        RefreshUI();
    }

    public void Close()
    {
        if (windowRoot != null) windowRoot.SetActive(false);
        if (tooltipBox != null) tooltipBox.SetActive(false);
        CursorLockManager.SetUnlock("MetaUpgrades", false);
        Time.timeScale = 1f;
    }

    public void RefreshUI()
    {
        SaveData data = SaveSystem.Load();
        int blood = data != null ? data.goblinBlood : 0;
        int metal = data != null ? data.orcishMetal : 0;
        int unlockedTier = data != null ? data.unlockedMetaTier : 1;

        if (goblinBloodText != null) goblinBloodText.text = $"{blood}";
        if (orcishMetalText != null) orcishMetalText.text = $"{metal}";

        // Configure Tier 2 unlock button
        if (unlockTier2Button != null)
        {
            if (unlockedTier >= 2)
            {
                unlockTier2Button.gameObject.SetActive(false);
            }
            else
            {
                unlockTier2Button.gameObject.SetActive(true);
                bool canAfford = metal >= 5;
                unlockTier2Button.interactable = canAfford;
                if (unlockTier2ButtonText != null)
                {
                    unlockTier2ButtonText.text = "Unlock Tier 2 (5 Orcish Metal)";
                    unlockTier2ButtonText.color = canAfford ? Color.white : Color.red;
                }
            }
        }

        // Configure Tier 3 unlock button
        if (unlockTier3Button != null)
        {
            if (unlockedTier >= 3)
            {
                unlockTier3Button.gameObject.SetActive(false);
            }
            else
            {
                unlockTier3Button.gameObject.SetActive(true);
                bool canAfford = unlockedTier >= 2 && metal >= 10;
                unlockTier3Button.interactable = canAfford;
                if (unlockTier3ButtonText != null)
                {
                    unlockTier3ButtonText.text = "Unlock Tier 3 (10 Orcish Metal)";
                    unlockTier3ButtonText.color = canAfford ? Color.white : Color.red;
                }
            }
        }

        // Render perk rows
        RenderPerkRow(1, tier1RowContainer, true, data, blood);
        RenderPerkRow(2, tier2RowContainer, unlockedTier >= 2, data, blood);
        RenderPerkRow(3, tier3RowContainer, unlockedTier >= 3, data, blood);
    }

    private void RenderPerkRow(int tier, Transform container, bool isTierUnlocked, SaveData data, int blood)
    {
        if (container == null) return;

        List<MetaPerkDefinitionSO> tierPerks = allPerks.FindAll(p => p.tier == tier);

        for (int i = 0; i < tierPerks.Count; i++)
        {
            MetaPerkDefinitionSO perk = tierPerks[i];
            bool isOwned = data != null && data.purchasedMetaPerks != null && data.purchasedMetaPerks.Contains(perk.id);

            Transform cardTransform = i < container.childCount ? container.GetChild(i) : null;
            if (cardTransform == null && perkCardPrefab != null)
            {
                cardTransform = Instantiate(perkCardPrefab, container).transform;
            }

            if (cardTransform != null)
            {
                cardTransform.gameObject.SetActive(true);
                ConfigurePerkCard(cardTransform, perk, isTierUnlocked, isOwned, blood);
            }
        }
    }

    private void ConfigurePerkCard(Transform card, MetaPerkDefinitionSO perk, bool isTierUnlocked, bool isOwned, int blood)
    {
        TMP_Text nameText = card.Find("PerkName")?.GetComponent<TMP_Text>();
        TMP_Text costText = card.Find("PerkCost")?.GetComponent<TMP_Text>();
        Image iconImage = card.Find("PerkIcon")?.GetComponent<Image>();
        Button buyBtn = card.GetComponent<Button>() ?? card.Find("BuyButton")?.GetComponent<Button>();

        if (nameText != null) nameText.text = perk.displayName;
        if (iconImage != null && perk.icon != null)
        {
            iconImage.sprite = perk.icon;
            iconImage.enabled = true;
        }

        CanvasGroup cg = card.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = isTierUnlocked ? 1.0f : 0.4f;
            cg.interactable = isTierUnlocked;
        }

        if (costText != null)
        {
            if (isOwned)
            {
                costText.text = "OWNED";
                costText.color = Color.green;
            }
            else if (!isTierUnlocked)
            {
                costText.text = "LOCKED";
                costText.color = Color.gray;
            }
            else
            {
                bool canAfford = blood >= perk.goblinBloodCost;
                costText.text = $"{perk.goblinBloodCost} Blood";
                costText.color = canAfford ? Color.white : Color.red;
            }
        }

        if (buyBtn != null)
        {
            buyBtn.onClick.RemoveAllListeners();
            buyBtn.interactable = isTierUnlocked && !isOwned && (blood >= perk.goblinBloodCost);
            buyBtn.onClick.AddListener(() => PurchasePerk(perk));
        }

        // Tooltip hover triggers
        EventTriggerListener listener = card.GetComponent<EventTriggerListener>() ?? card.gameObject.AddComponent<EventTriggerListener>();
        listener.OnHoverEnter = () => ShowTooltip(perk.displayName, perk.description);
        listener.OnHoverExit = HideTooltip;
    }

    private void UnlockTier(int tier, int metalCost)
    {
        SaveData data = SaveSystem.Load();
        if (data.orcishMetal >= metalCost)
        {
            data.orcishMetal -= metalCost;
            data.unlockedMetaTier = Mathf.Max(data.unlockedMetaTier, tier);
            SaveSystem.Save(data);
            RefreshUI();
            Debug.Log($"[MetaUpgradesUI] Unlocked Meta Tier {tier}!");
        }
    }

    private void PurchasePerk(MetaPerkDefinitionSO perk)
    {
        SaveData data = SaveSystem.Load();
        if (data.goblinBlood >= perk.goblinBloodCost && !data.purchasedMetaPerks.Contains(perk.id))
        {
            data.goblinBlood -= perk.goblinBloodCost;
            data.purchasedMetaPerks.Add(perk.id);
            SaveSystem.Save(data);
            RefreshUI();
            Debug.Log($"[MetaUpgradesUI] Purchased Meta Perk: {perk.displayName}!");
        }
    }

    public void ShowTooltip(string title, string description)
    {
        if (tooltipBox != null) tooltipBox.SetActive(true);
        if (tooltipTitle != null) tooltipTitle.text = title;
        if (tooltipDescription != null) tooltipDescription.text = description;
    }

    public void HideTooltip()
    {
        if (tooltipBox != null) tooltipBox.SetActive(false);
    }
}

public class EventTriggerListener : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public Action OnHoverEnter;
    public Action OnHoverExit;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => OnHoverEnter?.Invoke();
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => OnHoverExit?.Invoke();
}
