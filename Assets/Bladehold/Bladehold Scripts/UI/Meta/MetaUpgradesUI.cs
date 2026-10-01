using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     The Spirit's permanent meta-progression window (opened via <see cref="SpiritNPC" />).
///     Left: one section per tier, each a grid of <see cref="MetaPerkCardUI" /> tiles with rank pips.
///     Right: the details panel for the hovered/selected perk, with its current and next-rank effect and a Buy button.
///     Perks come from <see cref="MetaPerkCatalogSO" />; tiers 2 and 3 unlock for Orcish Metal, ranks cost Goblin Blood.
///     Each bought rank adds the perk id to <see cref="SaveData.purchasedMetaPerks" /> once more.
/// </summary>
public class MetaUpgradesUI : MonoBehaviour
{
    [Serializable]
    public class TierSection
    {
        public Transform cardGrid;
        public TMP_Text label;
        [Tooltip("Unused for tier 1 (always unlocked).")]
        public Button unlockButton;
        public TMP_Text unlockButtonText;
        [Tooltip("Orcish Metal to unlock this tier. Ignored for tier 1.")]
        public int unlockMetalCost;
    }

    private const string CursorOwner = "MetaUpgrades";

    public static MetaUpgradesUI Instance { get; private set; }

    [Header("Window")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private MenuFocusController focusController;

    [Header("Currencies")]
    [SerializeField] private TMP_Text goblinBloodText;
    [SerializeField] private TMP_Text orcishMetalText;

    [Header("Tiers (index 0 = tier 1)")]
    [SerializeField] private TierSection[] tiers = new TierSection[3];
    [SerializeField] private MetaPerkCardUI perkCardPrefab;

    [Header("Details Panel")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailRank;
    [SerializeField] private TMP_Text detailCurrent;
    [SerializeField] private TMP_Text detailNext;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;

    private readonly List<MetaPerkCardUI> cards = new List<MetaPerkCardUI>();
    private readonly List<MetaPerkDefinitionSO> cardPerks = new List<MetaPerkDefinitionSO>();
    private MetaPerkDefinitionSO selectedPerk;
    private bool anyError;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (windowRoot != null) windowRoot.SetActive(false);
    }

    private void Start()
    {
        if (windowRoot == null || perkCardPrefab == null || tiers == null || tiers.Length < 3 || buyButton == null)
        {
            Debug.LogError("[MetaUpgradesUI] windowRoot, perkCardPrefab, buyButton and all three tier sections must be assigned.", this);
            anyError = true;
            return;
        }
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i] == null || tiers[i].cardGrid == null)
            {
                Debug.LogError($"[MetaUpgradesUI] Tier {i + 1} has no card grid.", this);
                anyError = true;
            }
        }
        if (MetaPerkCatalogSO.Instance == null)
        {
            Debug.LogError($"[MetaUpgradesUI] No MetaPerkCatalogSO at Resources/{MetaPerkCatalogSO.ResourcePath}.", this);
            anyError = true;
        }
        if (anyError) return;

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        buyButton.onClick.AddListener(() => { if (selectedPerk != null) PurchasePerk(selectedPerk); });
        for (int i = 1; i < tiers.Length; i++)
        {
            int tier = i + 1;
            if (tiers[i].unlockButton != null) tiers[i].unlockButton.onClick.AddListener(() => UnlockTier(tier));
        }
        BuildCards();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        CursorLockManager.SetUnlock(CursorOwner, false);
    }

    public void Open()
    {
        if (anyError || windowRoot == null) return;
        windowRoot.SetActive(true);
        CursorLockManager.SetUnlock(CursorOwner, true);
        Time.timeScale = 0f;

        RefreshUI();
        if (cards.Count > 0)
        {
            ShowDetails(cardPerks[0]);
            if (focusController != null) focusController.SetDefaultSelectable(cards[0].Button);
        }
    }

    /// <summary>Public so the window's MenuFocusController can close it on pad B.</summary>
    public void Close()
    {
        if (windowRoot != null) windowRoot.SetActive(false);
        CursorLockManager.SetUnlock(CursorOwner, false);
        Time.timeScale = 1f;
    }

    private void BuildCards()
    {
        IReadOnlyList<MetaPerkDefinitionSO> perks = MetaPerkCatalogSO.Instance.Perks;
        for (int t = 0; t < tiers.Length; t++)
        {
            int tier = t + 1;
            foreach (MetaPerkDefinitionSO perk in perks)
            {
                if (perk == null || perk.tier != tier) continue;
                MetaPerkCardUI card = Instantiate(perkCardPrefab, tiers[t].cardGrid);
                card.name = $"PerkCard_{perk.id}";
                cards.Add(card);
                cardPerks.Add(perk);
            }
        }
    }

    public void RefreshUI()
    {
        if (anyError) return;
        SaveData data = SaveSystem.Load();
        int blood = data != null ? data.goblinBlood : 0;
        int metal = data != null ? data.orcishMetal : 0;
        int unlockedTier = data != null ? data.unlockedMetaTier : 1;

        if (goblinBloodText != null) goblinBloodText.text = $"{blood}";
        if (orcishMetalText != null) orcishMetalText.text = $"{metal}";

        for (int i = 1; i < tiers.Length; i++) RefreshUnlockButton(i + 1, unlockedTier, metal);

        for (int i = 0; i < cards.Count; i++)
        {
            MetaPerkCardUI card = cards[i];
            MetaPerkDefinitionSO perk = cardPerks[i];
            int rank = RankOf(data, perk.id);
            MetaPerkCardUI.CardState state = GetState(perk, rank, unlockedTier, blood);
            card.Bind(perk, rank, state, CostLabel(perk, rank, state), () => ShowDetails(perk), () => PurchasePerk(perk));
            card.SetHighlighted(perk == selectedPerk);
        }

        if (selectedPerk != null) ShowDetails(selectedPerk);
    }

    private void RefreshUnlockButton(int tier, int unlockedTier, int metal)
    {
        TierSection section = tiers[tier - 1];
        if (section.unlockButton == null) return;

        if (DemoConfigSO.IsMetaTierLocked(tier))
        {
            section.unlockButton.gameObject.SetActive(true);
            section.unlockButton.interactable = false;
            if (section.unlockButtonText != null) section.unlockButtonText.text = DemoConfigSO.LockedLabel;
            return;
        }
        if (unlockedTier >= tier)
        {
            section.unlockButton.gameObject.SetActive(false);
            return;
        }

        section.unlockButton.gameObject.SetActive(true);
        bool previousUnlocked = unlockedTier >= tier - 1;
        section.unlockButton.interactable = previousUnlocked && metal >= section.unlockMetalCost;
        if (section.unlockButtonText != null)
        {
            section.unlockButtonText.text = previousUnlocked
                ? string.Format(Loc.Get("meta.unlock_tier", "Unlock for {0} Metal"), section.unlockMetalCost)
                : string.Format(Loc.Get("meta.requires_tier", "Requires Tier {0}"), ToRoman(tier - 1));
        }
    }

    private static bool IsTierUsable(int tier, int unlockedTier) => !DemoConfigSO.IsMetaTierLocked(tier) && unlockedTier >= tier;

    private static int RankOf(SaveData data, string perkId)
    {
        if (data == null || data.purchasedMetaPerks == null) return 0;
        int rank = 0;
        foreach (string id in data.purchasedMetaPerks)
        {
            if (id == perkId) rank++;
        }
        return rank;
    }

    private static MetaPerkCardUI.CardState GetState(MetaPerkDefinitionSO perk, int rank, int unlockedTier, int blood)
    {
        if (rank >= perk.MaxRank) return MetaPerkCardUI.CardState.Maxed;
        if (!IsTierUsable(perk.tier, unlockedTier)) return MetaPerkCardUI.CardState.Locked;
        return blood >= perk.CostForRank(rank + 1) ? MetaPerkCardUI.CardState.Buyable : MetaPerkCardUI.CardState.TooExpensive;
    }

    private static string CostLabel(MetaPerkDefinitionSO perk, int rank, MetaPerkCardUI.CardState state)
    {
        switch (state)
        {
            case MetaPerkCardUI.CardState.Maxed:
                return perk.MaxRank > 1 ? Loc.Get("meta.mastered", "MASTERED") : Loc.Get("meta.owned", "OWNED");
            case MetaPerkCardUI.CardState.Locked:
                return DemoConfigSO.IsMetaTierLocked(perk.tier) ? DemoConfigSO.LockedLabel : Loc.Get("meta.locked", "LOCKED");
            default:
                return string.Format(Loc.Get("meta.cost_blood", "{0} Blood"), perk.CostForRank(rank + 1));
        }
    }

    private void ShowDetails(MetaPerkDefinitionSO perk)
    {
        if (perk == null) return;
        selectedPerk = perk;
        foreach (MetaPerkCardUI card in cards) card.SetHighlighted(card.Perk == perk);

        SaveData data = SaveSystem.Load();
        int rank = RankOf(data, perk.id);
        int blood = data != null ? data.goblinBlood : 0;
        int unlockedTier = data != null ? data.unlockedMetaTier : 1;
        MetaPerkCardUI.CardState state = GetState(perk, rank, unlockedTier, blood);
        bool ranked = perk.MaxRank > 1;

        if (detailIcon != null)
        {
            detailIcon.sprite = perk.icon;
            detailIcon.enabled = perk.icon != null;
        }
        if (detailName != null) detailName.text = perk.displayName;
        if (detailRank != null)
        {
            string tierText = string.Format(Loc.Get("meta.tier_label", "Tier {0}"), ToRoman(perk.tier));
            detailRank.text = ranked
                ? $"{tierText}  ·  {string.Format(Loc.Get("meta.rank_label", "Rank {0} / {1}"), rank, perk.MaxRank)}"
                : tierText;
        }

        if (detailCurrent != null)
        {
            if (rank > 0) detailCurrent.text = perk.DescriptionForRank(rank);
            else detailCurrent.text = ranked ? Loc.Get("meta.not_learned", "Not yet learned.") : perk.DescriptionForRank(1);
        }

        if (detailNext != null)
        {
            if (rank >= perk.MaxRank) detailNext.text = ranked ? Loc.Get("meta.fully_mastered", "Fully mastered.") : "";
            else if (ranked) detailNext.text = string.Format(Loc.Get("meta.next_rank", "<b>Next rank:</b> {0}"), perk.DescriptionForRank(rank + 1));
            else detailNext.text = "";
            if (state == MetaPerkCardUI.CardState.Locked && DemoConfigSO.IsMetaTierLocked(perk.tier))
            {
                detailNext.text += $"\n\n<i>{DemoConfigSO.LockedPrompt}</i>";
            }
        }

        buyButton.interactable = state == MetaPerkCardUI.CardState.Buyable;
        if (buyButtonText != null)
        {
            buyButtonText.text = state == MetaPerkCardUI.CardState.Buyable || state == MetaPerkCardUI.CardState.TooExpensive
                ? string.Format(Loc.Get(rank > 0 ? "meta.buy_upgrade" : "meta.buy_learn", rank > 0 ? "Upgrade · {0} Blood" : "Learn · {0} Blood"), perk.CostForRank(rank + 1))
                : CostLabel(perk, rank, state);
        }
    }

    private void UnlockTier(int tier)
    {
        if (DemoConfigSO.IsMetaTierLocked(tier)) return;
        int cost = tiers[tier - 1].unlockMetalCost;

        SaveData data = SaveSystem.Load();
        if (data.unlockedMetaTier >= tier - 1 && data.orcishMetal >= cost)
        {
            data.orcishMetal -= cost;
            data.unlockedMetaTier = Mathf.Max(data.unlockedMetaTier, tier);
            SaveSystem.Save(data);
            RefreshUI();
            Debug.Log($"[MetaUpgradesUI] Unlocked Meta Tier {tier}!");
        }
    }

    private void PurchasePerk(MetaPerkDefinitionSO perk)
    {
        ShowDetails(perk);
        SaveData data = SaveSystem.Load();
        int rank = RankOf(data, perk.id);
        if (rank >= perk.MaxRank || !IsTierUsable(perk.tier, data.unlockedMetaTier)) return;

        int cost = perk.CostForRank(rank + 1);
        if (data.goblinBlood < cost) return;

        data.goblinBlood -= cost;
        data.purchasedMetaPerks.Add(perk.id);
        SaveSystem.Save(data);
        RefreshUI();
        Debug.Log($"[MetaUpgradesUI] Purchased {perk.displayName} rank {rank + 1}/{perk.MaxRank}.");

        // Keep pad focus on the card just bought (the buy button may have gone non-interactable).
        foreach (MetaPerkCardUI card in cards)
        {
            if (card.Perk == perk && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == buyButton.gameObject && !buyButton.interactable)
            {
                EventSystem.current.SetSelectedGameObject(card.Button.gameObject);
            }
        }
    }

    private static string ToRoman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", _ => n.ToString() };
}
