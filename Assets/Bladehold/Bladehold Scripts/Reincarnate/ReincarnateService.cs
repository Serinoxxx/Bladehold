using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Owns the Reincarnate meta-tree: the permanent point currency, which nodes are purchased, and the
///     Reincarnate action itself. Mirrors <see cref="SkillTreeService" /> almost exactly — same
///     <see cref="SkillTreeSO" />/<see cref="SkillNode" /> data model, same reveal/purchase rules — but spends
///     Reincarnate Points instead of gold, and its purchases survive reincarnating rather than being reset by
///     it (the regular gold tree is the one that resets).
///
///     Scene singleton (<see cref="Instance" />) like <see cref="SkillTreeService" />. Reincarnate Points and
///     purchased node ids live in <see cref="SaveData" />, re-applied to <see cref="PlayerStats" /> in
///     <c>Start</c> every run, just like the gold tree.
/// </summary>
public class ReincarnateService : MonoBehaviour, ISkillTreeService
{
    public static ReincarnateService Instance;

    [SerializeField] private SkillTreeSO tree;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;

    private SaveData saveData;
    private readonly Dictionary<string, int> levels = new Dictionary<string, int>();
    private bool anyError = false;

    /// <summary>Raised whenever the set of purchased levels changes, so the tree UI can refresh.</summary>
    public event Action OnTreeChanged;

    /// <summary>Raised after a level purchase goes through, with the points actually paid.</summary>
    public event Action<SkillNode, int> OnNodePurchased;

    public SkillTreeSO Tree => tree;

    /// <summary>The player's current Reincarnate Point balance.</summary>
    public int Points => saveData != null ? saveData.reincarnatePoints : 0;

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
        if (tree == null)
        {
            Debug.LogError("SkillTreeSO (Reincarnate tree) is not assigned in the inspector.");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }

        if (stats == null)
        {
            Debug.LogError("ReincarnateService could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Re-apply persisted purchases to this run's stats — the Reincarnate tree is never wiped by
        // reincarnating, only the regular gold tree is. purchasedReincarnateNodeIds is a multiset: the
        // id appears once per owned level, so counting rebuilds each node's level and re-applies each
        // level's per-level effect increment.
        saveData = SaveSystem.Load();
        foreach (string id in saveData.purchasedReincarnateNodeIds)
        {
            SkillNode node = tree.GetById(id);
            if (node == null)
            {
                // Node was removed/renamed in the CSV since this save; skip it.
                continue;
            }
            if (levels.TryGetValue(id, out int current) && current >= node.maxLevel)
            {
                continue;
            }
            int level = current + 1;
            levels[id] = level;
            ApplyLevel(node, level);
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

    public bool IsPurchased(string id) => levels.TryGetValue(id, out int level) && level >= 1;

    public int GetLevel(SkillNode node) => node != null && levels.TryGetValue(node.id, out int level) ? level : 0;

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

    public bool CanPurchase(SkillNode node)
    {
        if (anyError || node == null) return false;
        if (IsMaxed(node)) return false;
        if (!IsRevealed(node)) return false;
        return Points >= GetCost(node);
    }

    public bool TryPurchase(string id)
    {
        if (anyError) return false;

        SkillNode node = tree.GetById(id);
        if (node == null || !CanPurchase(node))
        {
            return false;
        }

        int level = GetLevel(node) + 1;
        int price = node.CostForLevel(level);
        saveData.reincarnatePoints -= price;
        levels[id] = level;
        saveData.purchasedReincarnateNodeIds.Add(id);
        SaveSystem.Save(saveData);

        ApplyLevel(node, level);
        OnNodePurchased?.Invoke(node, price);
        OnTreeChanged?.Invoke();
        return true;
    }

    /// <summary>Reincarnate Points banked if the player reincarnates right now (highest wave reached this run).</summary>
    public int PreviewPointsForReincarnate()
    {
        return WaveSpawner.Instance != null ? WaveSpawner.Instance.CurrentWave : 0;
    }

    /// <summary>
    ///     First half of reincarnating: banks the points earned this run and resets the regular gold skill
    ///     tree (its purchases are cleared so next run's <see cref="SkillTreeService" /> comes back empty).
    ///     Reincarnate-tree purchases are untouched — they're permanent. The scene is <b>not</b> reloaded, so
    ///     the death screen can show the Reincarnate tree and let the player spend the banked points before
    ///     calling <see cref="CompleteReincarnate" />.
    /// </summary>
    public void BankPointsAndResetGoldTree()
    {
        if (anyError) return;

        saveData.reincarnatePoints += PreviewPointsForReincarnate();
        saveData.purchasedNodeIds.Clear();
        SaveSystem.Save(saveData);

        RunState.StartingWave = 1;

        // Points changed, so node affordability tints need a refresh.
        OnTreeChanged?.Invoke();
    }

    /// <summary>Second half of reincarnating: starts the new run by reloading the scene.</summary>
    public void CompleteReincarnate()
    {
        if (anyError) return;

        // Ensure normal speed resumes even if something paused time on death, mirroring DeathScreen.Reload().
        Time.timeScale = GameSettingsService.TargetTimeScale;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Banks points, resets the gold tree, and immediately starts the new run — the one-click flow.</summary>
    public void Reincarnate()
    {
        BankPointsAndResetGoldTree();
        CompleteReincarnate();
    }

    /// <summary>Applies the per-level increment each of the node's effects contributes at <paramref name="level" />.</summary>
    private void ApplyLevel(SkillNode node, int level)
    {
        foreach (SkillEffect effect in node.effects)
        {
            stats.AddModifier(effect.stat, effect.kind, effect.AmountForLevel(level));
        }
    }
}
