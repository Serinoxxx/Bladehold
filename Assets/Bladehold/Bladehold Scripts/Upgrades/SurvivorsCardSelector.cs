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

    /// <summary>
    ///     Evaluates available skill tree nodes and returns up to <paramref name="count"/> candidate skill cards.
    ///     A node is eligible if it is unlocked/revealed by dependency rules and not maxed.
    /// </summary>
    public List<SkillNode> GetRandomSkillCards(int count = 3)
    {
        List<SkillNode> candidates = new List<SkillNode>();

        if (SkillTreeService.Instance == null || SkillTreeService.Instance.Tree == null)
        {
            Debug.LogWarning("[SurvivorsCardSelector] SkillTreeService or SkillTreeSO is missing!");
            return candidates;
        }

        SkillTreeSO tree = SkillTreeService.Instance.Tree;
        IReadOnlyList<SkillNode> allNodes = tree.Nodes;

        foreach (SkillNode node in allNodes)
        {
            if (node == null) continue;

            // Rule 1: Exclude maxed nodes
            if (SkillTreeService.Instance.IsMaxed(node))
            {
                continue;
            }

            // Rule 2: Dependency chain verification
            // A node is valid if it is a root, OR if it's already revealed by SkillTreeService,
            // OR if any of its prereqs have been purchased.
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
    ///     Checks if a node's dependency chain is satisfied (is root, is revealed, or has a purchased prerequisite).
    /// </summary>
    public bool IsNodeDependencyMet(SkillNode node)
    {
        if (node == null) return false;

        // Root nodes are always unlocked
        if (node.isRoot) return true;

        // Check if already owned (can upgrade to next level)
        if (SkillTreeService.Instance.GetLevel(node) >= 1) return true;

        // Check SkillTreeService reveal state
        if (SkillTreeService.Instance.IsRevealed(node)) return true;

        // Explicit prereq check: any prerequisite purchased?
        if (node.prereqs != null)
        {
            foreach (string prereqId in node.prereqs)
            {
                if (SkillTreeService.Instance.IsPurchased(prereqId))
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
        if (SkillTreeService.Instance != null)
        {
            success = SkillTreeService.Instance.ApplyFreePurchase(node.id);
            Debug.Log($"[SurvivorsCardSelector] Selected card: '{node.displayName}' (ID: {node.id}). Granted level {SkillTreeService.Instance.GetLevel(node)}.");
        }

        if (SurvivorsGameManager.Instance != null)
        {
            SurvivorsGameManager.Instance.ResumeFromCardSelection();
        }

        return success;
    }
}
