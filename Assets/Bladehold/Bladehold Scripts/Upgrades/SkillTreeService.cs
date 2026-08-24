using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Owns skill-tree state: how many levels of each node are purchased, upgrading them (spending gold and
///     applying the stat modifier for each level reached), and persistence. Node ids live in
///     <see cref="SaveData.purchasedNodeIds" /> as a <b>multiset</b> — one entry per level owned, so a node's
///     level is how many times its id appears. Upgrades are permanent meta-progression like gold — on every
///     run this re-applies each owned level's modifier to <see cref="PlayerStats" /> in <c>Start</c>.
///
///     Scene singleton (<see cref="Instance" />) like Player/GameStats/WaveSpawner. The tree UI reads state
///     through it and calls <see cref="TryPurchase" />; <see cref="OnTreeChanged" /> notifies the UI to refresh.
/// </summary>
public class SkillTreeService : MonoBehaviour, ISkillTreeService
{
    public static SkillTreeService Instance;

    [SerializeField] private SkillTreeSO tree;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional; defaults to Player.Instance.Wallet.")]
    [SerializeField] private Wallet wallet;

    private SaveData saveData;
    private readonly Dictionary<string, int> metaLevels = new Dictionary<string, int>();
    private readonly Dictionary<string, int> runLevels = new Dictionary<string, int>();
    private bool anyError = false;

    /// <summary>Raised whenever the set of purchased levels changes, so the tree UI can refresh.</summary>
    public event Action OnTreeChanged;

    /// <summary>Raised after a level purchase goes through, with the price actually paid.</summary>
    public event Action<SkillNode, int> OnNodePurchased;

    public SkillTreeSO Tree => tree;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        // Per-class tree: the active class (resolved by PlayerClassController in Awake, strictly
        // before any Start) may carry its own SkillTreeSO; null keeps the serialized default (the
        // Swordsman tree). Saved ids not in the active tree are skipped below, never rewritten, so
        // switching class leaves the other class's purchases dormant rather than lost.
        PlayerClassController classController = UnityEngine.Object.FindAnyObjectByType<PlayerClassController>();
        if (classController != null)
        {
            Debug.Log($"[SkillTreeService] Found PlayerClassController. ActiveClass: {(classController.ActiveClass != null ? classController.ActiveClass.id : "null")}");
            if (classController.ActiveClass != null && classController.ActiveClass.skillTree != null)
            {
                tree = classController.ActiveClass.skillTree;
                Debug.Log($"[SkillTreeService] Swapped tree to {tree.name}");
            }
        }
        else
        {
            Debug.Log("[SkillTreeService] PlayerClassController NOT FOUND in scene!");
        }

        if (tree == null)
        {
            Debug.LogError("SkillTreeSO is not assigned in the inspector.");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (wallet == null)
        {
            wallet = Player.Instance != null ? Player.Instance.Wallet : null;
        }

        if (stats == null)
        {
            Debug.LogError("SkillTreeService could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }
        if (wallet == null)
        {
            Debug.LogError("SkillTreeService could not find a Wallet (set it or ensure Player.Instance.Wallet exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Re-apply persisted META purchases to this run's stats. purchasedNodeIds is a multiset.
        saveData = SaveSystem.Load();
        metaLevels.Clear();
        runLevels.Clear();

        if (saveData != null && saveData.purchasedNodeIds != null)
        {
            foreach (string id in saveData.purchasedNodeIds)
            {
                SkillNode node = tree.GetById(id);
                if (node == null)
                {
                    continue;
                }
                if (metaLevels.TryGetValue(id, out int current) && current >= node.maxLevel)
                {
                    continue;
                }
                int level = current + 1;
                metaLevels[id] = level;
                ApplyLevel(node, level);
            }
        }

        OnTreeChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsPurchased(string id) => (metaLevels.TryGetValue(id, out int m) && m >= 1) || (runLevels.TryGetValue(id, out int r) && r >= 1);

    public int GetLevel(SkillNode node)
    {
        if (node == null) return 0;
        int m = metaLevels.TryGetValue(node.id, out int ml) ? ml : 0;
        int r = runLevels.TryGetValue(node.id, out int rl) ? rl : 0;
        return m + r;
    }

    public int GetMetaLevel(SkillNode node) => node != null && metaLevels.TryGetValue(node.id, out int level) ? level : 0;
    public int GetRunLevel(SkillNode node) => node != null && runLevels.TryGetValue(node.id, out int level) ? level : 0;

    public bool IsMaxed(SkillNode node) => node != null && GetLevel(node) >= node.maxLevel;

    /// <summary>A node is revealed if it is a root (start-unlocked) or any linked node has reached level 1 (links are symmetric).</summary>
    public bool IsRevealed(SkillNode node)
    {
        if (node == null) return false;
        if (node.isRoot) return true;
        foreach (string p in node.prereqs)
        {
            if (IsPurchased(p)) return true;
        }
        if (tree != null)
        {
            foreach (string dependentId in tree.GetDependents(node.id))
            {
                if (IsPurchased(dependentId)) return true;
            }
        }
        return false;
    }

    /// <summary>A node is teased (shown dimmed, not purchasable) when a linked node is revealed but none is purchased.</summary>
    public bool IsTeased(SkillNode node)
    {
        if (node == null || IsRevealed(node)) return false;
        if (tree == null) return false;
        foreach (string p in node.prereqs)
        {
            SkillNode linked = tree.GetById(p);
            if (linked != null && IsRevealed(linked)) return true;
        }
        foreach (string dependentId in tree.GetDependents(node.id))
        {
            SkillNode linked = tree.GetById(dependentId);
            if (linked != null && IsRevealed(linked)) return true;
        }
        return false;
    }

    /// <summary>The cost of the node's next level (0 when already maxed).</summary>
    public int GetCost(SkillNode node)
    {
        if (node == null) return 0;
        int level = GetLevel(node);
        return level >= node.maxLevel ? 0 : node.CostForLevel(level + 1);
    }

    /// <summary>True if the node can be upgraded right now: revealed, not maxed, and affordable.</summary>
    public bool CanPurchase(SkillNode node)
    {
        if (anyError || node == null) return false;
        if (IsMaxed(node)) return false;
        if (!IsRevealed(node)) return false;
        return wallet != null && wallet.Coins >= GetCost(node);
    }

    /// <summary>
    ///     Buys the node's next level: spends its cost, records the level (persisted in SaveData), and applies that
    ///     level's stat modifier(s). Used by the Main Menu Meta Progression Grid.
    /// </summary>
    public bool TryPurchase(string id)
    {
        if (anyError) return false;

        SkillNode node = tree.GetById(id);
        if (node == null || !CanPurchase(node))
        {
            return false;
        }

        int nextLevel = GetLevel(node) + 1;
        int price = node.CostForLevel(nextLevel);
        if (wallet == null || !wallet.TrySpend(price))
        {
            return false;
        }

        int metaLvl = GetMetaLevel(node) + 1;
        metaLevels[id] = metaLvl;

        if (saveData != null)
        {
            saveData.purchasedNodeIds.Add(id);
            SaveSystem.Save(saveData);
        }

        ApplyLevel(node, nextLevel);
        OnNodePurchased?.Invoke(node, price);
        OnTreeChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Grants the node's next level for free in the CURRENT RUN ONLY (via Survivors mode level up card selection).
    ///     Does NOT persist to SaveData on disk.
    /// </summary>
    public bool ApplyFreePurchase(string id)
    {
        if (anyError) return false;

        SkillNode node = tree != null ? tree.GetById(id) : null;
        if (node == null || IsMaxed(node))
        {
            return false;
        }

        int nextLevel = GetLevel(node) + 1;
        int curRunLvl = GetRunLevel(node);
        runLevels[id] = curRunLvl + 1;

        ApplyLevel(node, nextLevel);
        OnNodePurchased?.Invoke(node, 0);
        OnTreeChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Debug method: Sets a skill node's level directly (0 to maxLevel), updating PlayerStats and persistence.
    /// </summary>
    public void DebugSetLevel(string id, int targetLevel)
    {
        if (anyError || tree == null) return;
        SkillNode node = tree.GetById(id);
        if (node == null) return;

        targetLevel = Mathf.Clamp(targetLevel, 0, node.maxLevel);
        int currentLevel = GetLevel(node);
        if (targetLevel == currentLevel) return;

        if (targetLevel > currentLevel)
        {
            for (int lvl = currentLevel + 1; lvl <= targetLevel; lvl++)
            {
                metaLevels[id] = lvl;
                if (saveData != null && saveData.purchasedNodeIds != null)
                {
                    saveData.purchasedNodeIds.Add(id);
                }
                ApplyLevel(node, lvl);
            }
        }
        else
        {
            for (int lvl = currentLevel; lvl > targetLevel; lvl--)
            {
                foreach (SkillEffect effect in node.effects)
                {
                    if (stats != null)
                    {
                        stats.AddModifier(effect.stat, effect.kind, -effect.AmountForLevel(lvl));
                    }
                }
                if (saveData != null && saveData.purchasedNodeIds != null)
                {
                    saveData.purchasedNodeIds.Remove(id);
                }
            }
            if (targetLevel == 0)
            {
                metaLevels.Remove(id);
            }
            else
            {
                metaLevels[id] = targetLevel;
            }
        }

        if (saveData != null)
        {
            SaveSystem.Save(saveData);
        }

        OnTreeChanged?.Invoke();
    }

    /// <summary>Applies the per-level increment each of the node's effects contributes at <paramref name="level" />.</summary>
    private void ApplyLevel(SkillNode node, int level)
    {
        foreach (SkillEffect effect in node.effects)
        {
            if (stats != null)
            {
                stats.AddModifier(effect.stat, effect.kind, effect.AmountForLevel(level));
            }
        }
    }
}
