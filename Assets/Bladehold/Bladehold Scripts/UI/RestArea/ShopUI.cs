using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Controller for the Rest Area Shop UI modal.
///     Displays player's in-run gold, generates 3 (or 4 with Deep Pockets) items,
///     and processes item purchases.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    [Header("Shop Stock Config")]
    [SerializeField] private List<ShopItemSO> itemPool = new List<ShopItemSO>();

    [Header("UI References")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private TMP_Text goldLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private GameObject slotPrefab;

    private readonly List<ShopItemSO> currentStock = new List<ShopItemSO>();
    private readonly HashSet<int> purchasedSlotIndices = new HashSet<int>();
    private bool isStockGenerated = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (closeButton != null) closeButton.onClick.AddListener(CloseShop);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseShop);
    }

    public void OpenShop()
    {
        if (!isStockGenerated || currentStock == null || currentStock.Count == 0)
        {
            GenerateStock();
            isStockGenerated = true;
        }

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

        for (int i = 0; i < slotCount && candidates.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            currentStock.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
    }

    public void RefreshUI()
    {
        if (goldLabel != null)
        {
            goldLabel.text = $"Gold: {RunSession.InRunGold}";
        }

        if (slotsContainer == null) return;

        // Clear or populate slots
        for (int i = 0; i < currentStock.Count; i++)
        {
            int slotIndex = i;
            ShopItemSO item = currentStock[i];
            bool isPurchased = purchasedSlotIndices.Contains(slotIndex);

            // Re-use or instantiate slot
            Transform slotTransform = i < slotsContainer.childCount ? slotsContainer.GetChild(i) : null;
            if (slotTransform == null && slotPrefab != null)
            {
                slotTransform = Instantiate(slotPrefab, slotsContainer).transform;
            }

            if (slotTransform != null)
            {
                ConfigureSlotUI(slotTransform, item, slotIndex, isPurchased);
            }
        }

        // Hide extra unused child slots
        for (int i = currentStock.Count; i < slotsContainer.childCount; i++)
        {
            slotsContainer.GetChild(i).gameObject.SetActive(false);
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
        if (iconImage != null && item != null && item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = true;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            bool canAfford = RunSession.InRunGold >= item.goldCost;
            buyButton.interactable = true;
            if (costText != null)
            {
                costText.text = $"{item.goldCost} Gold";
                costText.color = canAfford ? Color.white : new Color(1f, 0.4f, 0.4f, 1f);
            }
            buyButton.onClick.AddListener(() => HandleBuyAttempt(slotIndex, null));
        }
    }

    private void HandleBuyAttempt(int slotIndex, ShopSlotUI slotUI)
    {
        if (slotIndex < 0 || slotIndex >= currentStock.Count) return;
        if (purchasedSlotIndices.Contains(slotIndex)) return;

        ShopItemSO item = currentStock[slotIndex];
        if (item == null) return;

        if (RunSession.InRunGold >= item.goldCost)
        {
            if (RunSession.TrySpendInRunGold(item.goldCost))
            {
                purchasedSlotIndices.Add(slotIndex);
                ApplyItemEffect(item);
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
                    h.SetMaxHealth(h.MaxHealth + item.effectValue);
                    h.Heal(item.effectValue);
                }
                break;

            case ShopItemEffectType.MoveSpeedTemporary:
                RunSession.CrystalWaterWavesRemaining = item.durationWaves;
                break;

            case ShopItemEffectType.WaveEndHealTemporary:
                RunSession.SpecialHerbsWavesRemaining = item.durationWaves;
                break;
        }
    }
}
