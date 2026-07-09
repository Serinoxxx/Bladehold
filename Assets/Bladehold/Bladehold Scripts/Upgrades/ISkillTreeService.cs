using System;

/// <summary>
///     The contract a skill-tree UI (<see cref="SkillTreeView" />/<see cref="SkillNodeView" />) needs from
///     whatever owns a tree's purchase state. <see cref="SkillTreeService" /> (gold, the regular skill tree)
///     and <see cref="ReincarnateService" /> (Reincarnate Points, the meta tree) both implement this so the
///     same view code can render either tree without knowing which currency it spends.
/// </summary>
public interface ISkillTreeService
{
    /// <summary>The tree definition this service is purchasing against.</summary>
    SkillTreeSO Tree { get; }

    /// <summary>Raised whenever the set of purchased nodes changes, so the tree UI can refresh.</summary>
    event Action OnTreeChanged;

    /// <summary>True once the node owns at least one level (used for reveal and by external listeners).</summary>
    bool IsPurchased(string id);

    /// <summary>How many levels of the node are owned (0..<see cref="SkillNode.maxLevel" />).</summary>
    int GetLevel(SkillNode node);

    /// <summary>True when every level of the node has been purchased.</summary>
    bool IsMaxed(SkillNode node);

    /// <summary>
    ///     A node is revealed if it is a root (no links) or any node it's linked to has reached level 1.
    ///     Links are symmetric — this checks both the node's own listed links and the reverse direction
    ///     (nodes that list this one).
    /// </summary>
    bool IsRevealed(SkillNode node);

    /// <summary>
    ///     A node is teased when it isn't revealed yet but a linked node is — the one-step lookahead the UI
    ///     shows dimmed, so the player can see what buying the next node unlocks.
    /// </summary>
    bool IsTeased(SkillNode node);

    /// <summary>The cost of the node's <em>next</em> level (0 when already maxed).</summary>
    int GetCost(SkillNode node);

    /// <summary>True if the node can be upgraded right now: revealed, not maxed, and affordable.</summary>
    bool CanPurchase(SkillNode node);

    /// <summary>Buys the node's next level: spends its cost, records it (persisted), and applies that level's effect(s).</summary>
    bool TryPurchase(string id);
}
