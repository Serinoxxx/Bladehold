using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Controller for the Rest Area Shop UI modal.
///     Displays player's in-run gold, generates 3 (or 4 with Deep Pockets) items,
///     and processes item purchases. On top of those it always offers the ultimates of the equipped
///     weapons while the run has a free ultimate slot (<see cref="DraftUpgradeService.GetShopUltimates" />),
///     priced by <see cref="UltimateShopConfigSO" />. Ultimate offers never take one of the item slots.
///     The same featured row pins a Replacement Warhorse while the run's horse is dead
///     (<see cref="RunSession.MountLost" />), so losing the mount is always fixable here, for a price.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    [Header("Shop Stock Config")]
    [SerializeField] private List<ShopItemSO> itemPool = new List<ShopItemSO>();
    [SerializeField] private UltimateShopConfigSO ultimateConfig;
    [Tooltip("Pinned in the featured row while the run's warhorse is dead (effect type ReplaceMount).")]
    [SerializeField] private ShopItemSO replacementMountItem;

    [Header("UI References")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private TMP_Text goldLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private GameObject slotPrefab;
    [Tooltip("Optional row for ultimate offers. Empty = they follow the item slots in Slots Container.")]
    [SerializeField] private Transform ultimateSlotsContainer;
    [Tooltip("Optional: the panel's focus controller; its default moves to the first offer's buy button on each refresh.")]
    [SerializeField] private MenuFocusController focusController;

    // Ultimate offers use slot indices from here up, so HandleBuyAttempt can tell them from item slots.
    private const int UltimateSlotIndexBase = 1000;

    private readonly List<ShopItemSO> currentStock = new List<ShopItemSO>();
    private readonly List<ShopItemSO> ultimateStock = new List<ShopItemSO>();
    private bool anyError;
    private readonly HashSet<int> purchasedSlotIndices = new HashSet<int>();
    private bool isStockGenerated = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (closeButton != null) closeButton.onClick.AddListener(CloseShop);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    private void Start()
    {
        if (ultimateConfig == null)
        {
            Debug.LogError("[ShopUI] No UltimateShopConfigSO assigned, so the shop can't sell ultimates.");
            anyError = true;
        }
        if (replacementMountItem == null)
        {
            Debug.LogError("[ShopUI] No Replacement Warhorse item assigned, so a dead horse can't be replaced.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseShop);
        ClearUltimateStock();
    }

    public void OpenShop()
    {
        if (!isStockGenerated || currentStock == null || currentStock.Count == 0)
        {
            GenerateStock();
            isStockGenerated = true;
        }
        GenerateUltimateStock();

        if (shopPanel != null) shopPanel.SetActive(true);
        CursorLockManager.SetUnlock("RestShop", true);
        Time.timeScale = 0f;

        RefreshUI();
    }

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        CursorLockManager.SetUnlock("RestShop", false);
        Time.timeScale = 1f;
    }

    private void GenerateStock()
    {
        currentStock.Clear();
        purchasedSlotIndices.Clear();

        int slotCount = RunSession.HasMetaPerk("deep_pockets") ? 4 : 3;
        List<ShopItemSO> candidates = new List<ShopItemSO>(itemPool);
        // A poultice is useless for a dead or unhurt horse; the replacement is pinned, never rolled.
        candidates.RemoveAll(item => item == null
            || item.effectType == ShopItemEffectType.ReplaceMount
            || (item.effectType == ShopItemEffectType.HealMount && (RunSession.MountLost || RunSession.MountHealthFraction >= 0.999f)));

        for (int i = 0; i < slotCount && candidates.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            currentStock.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
    }

    /// <summary>
    ///     Rebuilds the ultimate offers from the current run state. They're runtime ShopItemSO instances built
    ///     from the draft catalog's ultimate rows, so no per-ultimate asset exists.
    /// </summary>
    private void GenerateUltimateStock()
    {
        ClearUltimateStock();
        if (anyError || ultimateConfig == null) return;

        DraftUpgradeService drafts = DraftUpgradeService.GetOrCreateInstance();
        int cost = ultimateConfig.CostForNextUltimate(RunSession.OwnedUltimateCount);
        foreach (DraftUpgradeDefinition def in drafts.GetShopUltimates())
        {
            string slotLabel = DraftUpgradeService.SlotForUltimate(def) == UltimateSlot.Ranged ? "Ranged" : "Melee";
            ShopItemSO offer = ScriptableObject.CreateInstance<ShopItemSO>();
            offer.hideFlags = HideFlags.DontSave;
            offer.name = def.id;
            offer.itemId = def.id;
            offer.displayName = def.displayName;
            offer.description = $"{slotLabel} ultimate. {def.description}";
            offer.icon = drafts.GetIcon(def.iconName);
            offer.goldCost = cost;
            offer.effectType = ShopItemEffectType.UnlockUltimate;
            ultimateStock.Add(offer);
        }

        if (RunSession.MountLost && replacementMountItem != null)
        {
            ShopItemSO offer = Instantiate(replacementMountItem);
            offer.hideFlags = HideFlags.DontSave;
            offer.name = replacementMountItem.name;
            ultimateStock.Add(offer);
        }
    }

    private void ClearUltimateStock()
    {
        foreach (ShopItemSO offer in ultimateStock)
        {
            if (offer != null) Destroy(offer);
        }
        ultimateStock.Clear();
    }

    public void RefreshUI()
    {
        if (goldLabel != null)
        {
            goldLabel.text = $"Gold: {RunSession.InRunGold}";
        }

        if (slotsContainer == null) return;

        bool sharedRow = ultimateSlotsContainer == null || ultimateSlotsContainer == slotsContainer;
        int used = PopulateSlots(slotsContainer, 0, currentStock, 0);
        if (sharedRow) used = PopulateSlots(slotsContainer, used, ultimateStock, UltimateSlotIndexBase);
        HideSlotsFrom(slotsContainer, used);

        FitRow(slotsContainer);

        if (!sharedRow)
        {
            int usedUltimate = PopulateSlots(ultimateSlotsContainer, 0, ultimateStock, UltimateSlotIndexBase);
            HideSlotsFrom(ultimateSlotsContainer, usedUltimate);
            FitRow(ultimateSlotsContainer);
        }

        FocusFirstOffer();
    }

    /// <summary>Points pad focus at the first offer still for sale (the close button when sold out).</summary>
    private void FocusFirstOffer()
    {
        if (focusController == null) return;
        foreach (Transform container in new[] { slotsContainer, ultimateSlotsContainer })
        {
            if (container == null) continue;
            foreach (Transform child in container)
            {
                ShopSlotUI slot = child.GetComponent<ShopSlotUI>();
                if (child.gameObject.activeSelf && slot != null && slot.BuyButton != null)
                {
                    focusController.SetDefaultSelectable(slot.BuyButton);
                    return;
                }
            }
        }
        focusController.SetDefaultSelectable(closeButton);
    }

    /// <summary>
    ///     Scales a slot row down uniformly when its active slots are wider than the row (Deep Pockets plus
    ///     two ultimates plus a pinned replacement horse is seven cards), so no offer is ever pushed off the panel.
    /// </summary>
    private static void FitRow(Transform container)
    {
        RectTransform row = container as RectTransform;
        if (row == null) return;

        HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;
        float needed = 0f;
        int active = 0;
        foreach (Transform child in container)
        {
            if (!child.gameObject.activeSelf) continue;
            RectTransform slot = child as RectTransform;
            needed += slot != null ? slot.rect.width : 0f;
            active++;
        }
        needed += Mathf.Max(0, active - 1) * spacing;

        float scale = needed > row.rect.width && needed > 0f ? row.rect.width / needed : 1f;
        row.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>Fills container children from <paramref name="firstChild" /> on; returns the next free child index.</summary>
    private int PopulateSlots(Transform container, int firstChild, List<ShopItemSO> items, int indexBase)
    {
        for (int i = 0; i < items.Count; i++)
        {
            int slotIndex = indexBase + i;
            int child = firstChild + i;
            bool isPurchased = indexBase == 0 && purchasedSlotIndices.Contains(slotIndex);

            // Re-use or instantiate slot
            Transform slotTransform = child < container.childCount ? container.GetChild(child) : null;
            if (slotTransform == null && slotPrefab != null)
            {
                slotTransform = Instantiate(slotPrefab, container).transform;
            }

            if (slotTransform != null)
            {
                ConfigureSlotUI(slotTransform, items[i], slotIndex, isPurchased);
            }
        }
        return firstChild + items.Count;
    }

    private static void HideSlotsFrom(Transform container, int firstChild)
    {
        for (int i = firstChild; i < container.childCount; i++)
        {
            container.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void ConfigureSlotUI(Transform slotTransform, ShopItemSO item, int slotIndex, bool isPurchased)
    {
        ShopSlotUI slotUI = slotTransform.GetComponent<ShopSlotUI>();
        if (slotUI != null)
        {
            slotUI.Setup(item, slotIndex, isPurchased, HandleBuyAttempt);
            return;
        }

        // Fallback for legacy slots without ShopSlotUI component
        if (isPurchased)
        {
            slotTransform.gameObject.SetActive(false);
            return;
        }

        slotTransform.gameObject.SetActive(true);
        TMP_Text nameText = slotTransform.Find("ItemName")?.GetComponent<TMP_Text>();
        TMP_Text descText = slotTransform.Find("ItemDesc")?.GetComponent<TMP_Text>();
        Image iconImage = slotTransform.Find("ItemIcon")?.GetComponent<Image>();
        Button buyButton = slotTransform.Find("BuyButton")?.GetComponent<Button>();
        TMP_Text costText = buyButton != null ? buyButton.GetComponentInChildren<TMP_Text>() : null;

        if (nameText != null) nameText.text = item != null ? item.displayName : "Item";
        if (descText != null) descText.text = item != null ? item.description : "";
        if (iconImage != null)
        {
            if (item != null && item.icon != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            bool canAfford = RunSession.InRunGold >= item.goldCost;
            buyButton.interactable = true;
            if (costText != null)
            {
                costText.text = $"{item.goldCost} Gold";
                costText.color = UITheme.For(this).Get(canAfford ? UIColorRole.Cost : UIColorRole.Danger);
            }
            buyButton.onClick.AddListener(() => HandleBuyAttempt(slotIndex, null));
        }
    }

    private void HandleBuyAttempt(int slotIndex, ShopSlotUI slotUI)
    {
        bool isUltimate = slotIndex >= UltimateSlotIndexBase;
        List<ShopItemSO> stock = isUltimate ? ultimateStock : currentStock;
        int stockIndex = isUltimate ? slotIndex - UltimateSlotIndexBase : slotIndex;
        if (stockIndex < 0 || stockIndex >= stock.Count) return;
        if (!isUltimate && purchasedSlotIndices.Contains(slotIndex)) return;

        ShopItemSO item = stock[stockIndex];
        if (item == null) return;

        if (RunSession.InRunGold >= item.goldCost)
        {
            if (RunSession.TrySpendInRunGold(item.goldCost))
            {
                if (!isUltimate) purchasedSlotIndices.Add(slotIndex);
                ApplyItemEffect(item);
                // Owning an ultimate removes the other weapon's offer or re-prices it (second_ultimate perk);
                // buying the replacement horse removes its pinned offer.
                if (isUltimate) GenerateUltimateStock();
                if (goldLabel != null)
                {
                    goldLabel.text = $"Gold: {RunSession.InRunGold}";
                }

                if (slotUI != null)
                {
                    slotUI.PlayPurchaseFeedback(() => RefreshUI());
                }
                else
                {
                    RefreshUI();
                }

                Debug.Log($"[ShopUI] Purchased item: {item.displayName}");
            }
        }
        else
        {
            if (slotUI != null)
            {
                slotUI.PlayInvalidFeedback();
            }
            Debug.Log($"[ShopUI] Cannot afford item {item.displayName} (Cost: {item.goldCost}, Gold: {RunSession.InRunGold})");
        }
    }

    private void ApplyItemEffect(ShopItemSO item)
    {
        Player p = Player.Instance != null ? Player.Instance : UnityEngine.Object.FindAnyObjectByType<Player>();
        Health h = p != null ? (p.Health != null ? p.Health : p.GetComponent<Health>()) : null;

        switch (item.effectType)
        {
            case ShopItemEffectType.HealInstant:
                if (h != null)
                {
                    h.Heal(item.effectValue);
                }
                break;

            case ShopItemEffectType.MaxHealthRun:
                RunSession.PlayerBonusMaxHealth += item.effectValue;
                if (h != null)
                {
                    float current = h.CurrentHealth;
                    h.SetMaxHealth(h.MaxHealth + item.effectValue);
                    h.SetCurrentHealth(current + item.effectValue);
                }
                break;

            case ShopItemEffectType.MoveSpeedTemporary:
                RunSession.CrystalWaterWavesRemaining = item.durationWaves;
                break;

            case ShopItemEffectType.WaveEndHealTemporary:
                RunSession.SpecialHerbsWavesRemaining = item.durationWaves;
                break;

            case ShopItemEffectType.UnlockUltimate:
                DraftUpgradeService drafts = DraftUpgradeService.GetOrCreateInstance();
                DraftUpgradeDefinition ultimate = drafts.GetById(item.itemId);
                if (ultimate != null && ultimate.isUltimate)
                {
                    drafts.ApplyUpgrade(ultimate);
                }
                else
                {
                    Debug.LogError($"[ShopUI] '{item.itemId}' is not an ultimate in the draft catalog.");
                }
                break;

            case ShopItemEffectType.ReplaceMount:
                RunSession.ReplaceMount();
                break;

            case ShopItemEffectType.HealMount:
                PlayerMount mount = p != null ? p.transform.root.GetComponentInChildren<PlayerMount>(true) : null;
                if (mount != null)
                {
                    mount.HealMount(item.effectValue);
                }
                else if (!RunSession.MountLost)
                {
                    RunSession.MountHealthFraction = Mathf.Min(1f, RunSession.MountHealthFraction + item.effectValue);
                }
                break;

            case ShopItemEffectType.RunStatModifier:
                RunSession.AddShopStatModifier(item.stat, item.statKind, item.effectValue, p);
                break;

            case ShopItemEffectType.AmmoRefill:
                RunSession.AddInRunAmmo(Mathf.RoundToInt(item.effectValue));
                if (p != null && p.Ammo != null)
                {
                    p.Ammo.AddAmmo(Mathf.RoundToInt(item.effectValue));
                }
                break;
        }
    }
}
