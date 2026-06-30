using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Owns skill-tree state: which nodes are purchased, buying them (spending gold and applying the stat
///     modifier), and persistence. Purchased node ids live in <see cref="SaveData" />, so upgrades are
///     permanent meta-progression like gold — on every run this re-applies each purchased node's modifier to
///     <see cref="PlayerStats" /> in <c>Start</c>.
///
///     Scene singleton (<see cref="Instance" />) like Player/GameStats/WaveSpawner. The tree UI reads state
///     through it and calls <see cref="TryPurchase" />; <see cref="OnTreeChanged" /> notifies the UI to refresh.
/// </summary>
public class SkillTreeService : MonoBehaviour
{
    public static SkillTreeService Instance;

    [SerializeField] private SkillTreeSO tree;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional; defaults to Player.Instance.Wallet.")]
    [SerializeField] private Wallet wallet;

    private SaveData saveData;
    private readonly HashSet<string> purchased = new HashSet<string>();
    private bool anyError = false;

    /// <summary>Raised whenever the set of purchased nodes changes, so the tree UI can refresh.</summary>
    public event Action OnTreeChanged;

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

        // Re-apply persisted purchases to this run's stats.
        saveData = SaveSystem.Load();
        foreach (string id in saveData.purchasedNodeIds)
        {
            SkillNode node = tree.GetById(id);
            if (node == null)
            {
                // Node was removed/renamed in the CSV since this save; skip it.
                continue;
            }
            purchased.Add(id);
            ApplyEffect(node);
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

    public bool IsPurchased(string id) => purchased.Contains(id);

    /// <summary>A node is revealed if it is a root (no prereqs) or any prerequisite has been purchased.</summary>
    public bool IsRevealed(SkillNode node)
    {
        if (node == null) return false;
        if (node.prereqs.Count == 0) return true;
        foreach (string p in node.prereqs)
        {
            if (purchased.Contains(p)) return true;
        }
        return false;
    }

    /// <summary>True if the node can be bought right now: revealed, not already owned, and affordable.</summary>
    public bool CanPurchase(SkillNode node)
    {
        if (anyError || node == null) return false;
        if (purchased.Contains(node.id)) return false;
        if (!IsRevealed(node)) return false;
        return wallet.Coins >= node.cost;
    }

    /// <summary>
    ///     Buys the node: spends its cost, records it (persisted), and applies its stat modifier. Returns
    ///     false (changing nothing) if it can't be bought. Idempotent per id — a node can be owned once, but
    ///     duplicate-effect nodes have distinct ids so their buffs still stack.
    /// </summary>
    public bool TryPurchase(string id)
    {
        if (anyError) return false;

        SkillNode node = tree.GetById(id);
        if (node == null || !CanPurchase(node))
        {
            return false;
        }

        if (!wallet.TrySpend(node.cost))
        {
            return false;
        }

        purchased.Add(id);
        saveData.purchasedNodeIds.Add(id);
        SaveSystem.Save(saveData);

        ApplyEffect(node);
        OnTreeChanged?.Invoke();
        return true;
    }

    private void ApplyEffect(SkillNode node)
    {
        if (node.hasEffect)
        {
            stats.AddModifier(node.stat, node.kind, node.amount);
        }
    }
}
