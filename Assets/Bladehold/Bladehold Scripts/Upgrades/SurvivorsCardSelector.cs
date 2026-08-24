using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Queries the active class skill tree and selects 3 valid candidate skill upgrade cards upon level up.
///     Enforces skill tree dependency chains (root node, prerequisite purchased, or currently owned upgradeable skill).
/// </summary>
public class SurvivorsCardSelector : MonoBehaviour
{
    public static SurvivorsCardSelector Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [Header("Slot Limits")]
    [Tooltip("Maximum active weapons/abilities a player can hold in a run.")]
    [SerializeField] private int maxActiveWeapons = 4;

    private readonly HashSet<string> banishedNodeIds = new HashSet<string>();

    private SkillTreeService TreeService => SkillTreeService.Instance != null ? SkillTreeService.Instance : UnityEngine.Object.FindAnyObjectByType<SkillTreeService>();

    /// <summary>
    ///     Evaluates available skill tree nodes and returns up to <paramref name="count"/> candidate skill cards.
    ///     A node is eligible if it is marked as a card (isCard), not banished, unlocked/revealed by dependency rules,
    ///     not maxed, and does not violate the active weapon slot limit.
    /// </summary>
    public List<SkillNode> GetRandomSkillCards(int count = 3, List<SkillNode> excludeList = null)
    {
        List<SkillNode> candidates = new List<SkillNode>();
        SkillTreeService service = TreeService;

        if (service == null || service.Tree == null)
        {
            Debug.LogWarning("[SurvivorsCardSelector] SkillTreeService or SkillTreeSO is missing!");
            return candidates;
        }

        SkillTreeSO tree = service.Tree;
        IReadOnlyList<SkillNode> allNodes = tree.Nodes;

        int ownedActiveWeapons = GetOwnedActiveWeaponsCount(allNodes);

        foreach (SkillNode node in allNodes)
        {
            if (node == null) continue;

            // Rule 1: Must be designated as an in-run card
            if (!node.isCard) continue;

            // Rule 2: Cannot be banished
            if (banishedNodeIds.Contains(node.id)) continue;

            // Rule 3: Exclude if in excludeList
            if (excludeList != null && excludeList.Contains(node)) continue;

            // Rule 4: Exclude maxed nodes
            if (service.IsMaxed(node)) continue;

            // Rule 5: Active weapon slot cap (if at max active weapons, only offer upgrades to already-owned active weapons)
            if (node.isActiveWeapon && ownedActiveWeapons >= maxActiveWeapons && service.GetLevel(node) <= 0)
            {
                continue;
            }

            // Rule 6: Dependency chain verification
            bool isEligible = IsNodeDependencyMet(node);
            if (isEligible)
            {
                candidates.Add(node);
            }
        }

        // Shuffle candidates using Fisher-Yates shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            SkillNode temp = candidates[i];
            candidates[i] = candidates[randomIndex];
            candidates[randomIndex] = temp;
        }

        // Return up to 'count' cards
        if (candidates.Count > count)
        {
            candidates = candidates.GetRange(0, count);
        }

        return candidates;
    }

    /// <summary>
    ///     Fetches a single replacement card that is eligible and not currently displayed.
    /// </summary>
    public SkillNode GetSingleReplacementCard(List<SkillNode> currentOffered)
    {
        List<SkillNode> choices = GetRandomSkillCards(1, currentOffered);
        return choices.Count > 0 ? choices[0] : null;
    }

    /// <summary>
    ///     Permanently banishes a card from being offered again for the rest of the current run.
    /// </summary>
    public bool BanishCard(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return banishedNodeIds.Add(id);
    }

    public bool IsBanished(string id) => !string.IsNullOrEmpty(id) && banishedNodeIds.Contains(id);

    public void ResetBanished()
    {
        banishedNodeIds.Clear();
    }

    /// <summary>
    ///     Counts how many active weapons/abilities the player currently owns (level >= 1).
    /// </summary>
    public int GetOwnedActiveWeaponsCount(IReadOnlyList<SkillNode> allNodes)
    {
        SkillTreeService service = TreeService;
        if (service == null || allNodes == null) return 0;
        int count = 0;
        foreach (SkillNode node in allNodes)
        {
            if (node != null && node.isActiveWeapon && service.GetLevel(node) >= 1)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    ///     Checks if a node's dependency chain is satisfied (is root, is revealed, or has a purchased prerequisite).
    /// </summary>
    public bool IsNodeDependencyMet(SkillNode node)
    {
        if (node == null) return false;

        // Root nodes are always unlocked
        if (node.isRoot) return true;

        SkillTreeService service = TreeService;
        if (service == null) return false;

        // Check if already owned (can upgrade to next level)
        if (service.GetLevel(node) >= 1) return true;

        // Check SkillTreeService reveal state
        if (service.IsRevealed(node)) return true;

        // Explicit prereq check: any prerequisite purchased?
        if (node.prereqs != null)
        {
            foreach (string prereqId in node.prereqs)
            {
                if (service.IsPurchased(prereqId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Applies the selected card upgrade to the player's stats via SkillTreeService and resumes gameplay.
    /// </summary>
    public bool SelectCard(SkillNode node)
    {
        if (node == null) return false;

        bool success = false;
        SkillTreeService service = TreeService;
        if (service != null)
        {
            success = service.ApplyFreePurchase(node.id);
            Debug.Log($"[SurvivorsCardSelector] Selected card: '{node.displayName}' (ID: {node.id}). Granted level {service.GetLevel(node)}.");
        }

        if (FortDefenseManager.Instance != null)
        {
            FortDefenseManager.Instance.HandleSkillNodePurchased(node.id);
        }

        if (SurvivorsGameManager.Instance != null)
        {
            SurvivorsGameManager.Instance.ResumeFromCardSelection();
        }

        return success;
    }
}
