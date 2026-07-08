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

    bool IsPurchased(string id);

    /// <summary>
    ///     A node is revealed if it is a root (no links) or any node it's linked to has been purchased.
    ///     Links are symmetric — this checks both the node's own listed links and the reverse direction
    ///     (nodes that list this one).
    /// </summary>
    bool IsRevealed(SkillNode node);

    /// <summary>
    ///     A node is teased when it isn't revealed yet but a linked node is — the one-step lookahead the UI
    ///     shows dimmed, so the player can see what buying the next node unlocks.
    /// </summary>
    bool IsTeased(SkillNode node);

    /// <summary>
    ///     The node's current price: its authored cost, or — for nodes in a <see cref="SkillNode.family" /> —
    ///     the next rung of the family's shared price ladder (each purchase anywhere in the family climbs it).
    /// </summary>
    int GetCost(SkillNode node);

    /// <summary>True if the node can be bought right now: revealed, not already owned, and affordable.</summary>
    bool CanPurchase(SkillNode node);

    /// <summary>Buys the node: spends its cost, records it (persisted), and applies its effect(s).</summary>
    bool TryPurchase(string id);
}
