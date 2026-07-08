using System.Collections.Generic;

/// <summary>One stat modifier a <see cref="SkillNode" /> applies when purchased.</summary>
public struct SkillEffect
{
    public StatType stat;
    public ModifierKind kind;
    public float amount;
}

/// <summary>
///     One node in the skill tree, parsed from a row of the CSV by <see cref="SkillTreeSO" />. A node may
///     grant one or more stat modifiers (<see cref="effects" /> non-empty) or be a pure connector/unlock node
///     (effect columns blank). Duplicate effects across different ids are intentional — buying both stacks
///     the buff.
/// </summary>
public class SkillNode
{
    /// <summary>Unique node id (the CSV's first column). Each id can be purchased at most once.</summary>
    public string id;

    public string displayName;
    public string description;

    /// <summary>
    ///     Name of this node's icon sprite (the CSV's optional 11th column), resolved against the owning
    ///     <see cref="SkillTreeSO" />'s icon list by <see cref="SkillTreeSO.GetIcon" />. Empty = no icon.
    /// </summary>
    public string iconName = "";

    /// <summary>Cost to purchase, in whichever currency the owning service spends (gold, Reincarnate Points, ...).</summary>
    public int cost;

    /// <summary>
    ///     Optional price-pool name (the CSV's optional 12th column). Nodes sharing a family can be bought
    ///     in any order but share one escalating price ladder: the Nth family purchase costs the Nth-cheapest
    ///     authored cost in the family, whichever node it is spent on. Empty = priced individually.
    /// </summary>
    public string family = "";

    /// <summary>
    ///     Stat modifiers applied together when this node is purchased. Empty for a pure connector/unlock
    ///     node. Usually one entry; a node can carry several (e.g. a Golden Goblin tier bumping both its
    ///     spawn chance and its bonus gold at once) via the CSV's ';'-separated stat/kind/amount columns.
    /// </summary>
    public List<SkillEffect> effects = new List<SkillEffect>();

    /// <summary>
    ///     Ids of nodes this one is linked to. Empty = a root, visible from the start. Otherwise a link is
    ///     symmetric: this node is revealed once ANY node it's linked to is purchased — either one listed
    ///     here, or one that lists this node's id in its own <see cref="prereqs" /> (the reverse direction,
    ///     via <see cref="SkillTreeSO.GetDependents" />). There's no visual arrow on the connector, so
    ///     purchasing either endpoint of a link always unlocks the other.
    /// </summary>
    public List<string> prereqs = new List<string>();

    /// <summary>Layout coordinates for the tree UI (column, row); multiplied by spacing by the view.</summary>
    public float x;
    public float y;
}
