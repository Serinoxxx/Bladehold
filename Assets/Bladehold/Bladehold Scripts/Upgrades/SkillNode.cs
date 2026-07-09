using System.Collections.Generic;

/// <summary>
///     One stat modifier a <see cref="SkillNode" /> applies. A node can be upgraded through several
///     levels; the increment applied per level is either uniform (one <see cref="amounts" /> entry, reused
///     every level) or authored per level (an <see cref="amounts" /> array of length
///     <see cref="SkillNode.maxLevel" />, for skills whose steps aren't equal). Use
///     <see cref="AmountForLevel" /> to read the increment a given (1-based) level applies.
/// </summary>
public struct SkillEffect
{
    public StatType stat;
    public ModifierKind kind;

    /// <summary>
    ///     The per-level increment(s). Length 1 = the same increment every level; otherwise one entry
    ///     per level (index 0 = level 1). Never empty.
    /// </summary>
    public float[] amounts;

    /// <summary>The increment this effect applies when buying <paramref name="level" /> (1-based).</summary>
    public float AmountForLevel(int level)
    {
        if (amounts == null || amounts.Length == 0)
        {
            return 0f;
        }
        if (amounts.Length == 1)
        {
            return amounts[0];
        }
        int index = level - 1;
        if (index < 0) index = 0;
        if (index >= amounts.Length) index = amounts.Length - 1;
        return amounts[index];
    }
}

/// <summary>
///     One node in the skill tree, parsed from a row of the CSV by <see cref="SkillTreeSO" />. A node is a
///     single skill that can be upgraded through <see cref="maxLevel" /> levels by purchasing it repeatedly
///     (each purchase re-applies the node's <see cref="effects" /> at the newly reached level). Buying the
///     first level reveals linked nodes. A node may instead be a pure connector/unlock node
///     (effect columns blank).
/// </summary>
public class SkillNode
{
    /// <summary>Unique node id (the CSV's first column).</summary>
    public string id;

    public string displayName;

    /// <summary>Text shown before purchase (and the only text for single-level nodes) — the "unlock" text.</summary>
    public string description;

    /// <summary>
    ///     Text shown once the node is owned and still upgradeable — the "upgrade" text. Empty falls back
    ///     to <see cref="description" />.
    /// </summary>
    public string upgradeText = "";

    /// <summary>
    ///     Name of this node's icon sprite, resolved against the owning <see cref="SkillTreeSO" />'s icon
    ///     list by <see cref="SkillTreeSO.GetIcon" />. Empty = no icon.
    /// </summary>
    public string iconName = "";

    /// <summary>How many times this node can be purchased (levels). 1 = a single-purchase node.</summary>
    public int maxLevel = 1;

    /// <summary>
    ///     Cost of each level, precomputed by <see cref="SkillTreeSO" /> from the authored base cost and
    ///     growth multiplier. Index 0 = level 1. Length == <see cref="maxLevel" />.
    /// </summary>
    public int[] costPerLevel = { 0 };

    /// <summary>Cost to purchase <paramref name="level" /> (1-based), in the owning service's currency.</summary>
    public int CostForLevel(int level)
    {
        if (costPerLevel == null || costPerLevel.Length == 0)
        {
            return 0;
        }
        int index = level - 1;
        if (index < 0) index = 0;
        if (index >= costPerLevel.Length) index = costPerLevel.Length - 1;
        return costPerLevel[index];
    }

    /// <summary>
    ///     Stat modifiers applied per level when this node is upgraded. Empty for a pure connector/unlock
    ///     node. Usually one entry; a node can carry several (e.g. Golden Goblin bumping both its spawn
    ///     chance and its bonus gold) via the CSV's ';'-separated stat/kind/amount columns.
    /// </summary>
    public List<SkillEffect> effects = new List<SkillEffect>();

    /// <summary>
    ///     True if this node is unlocked from the start — a tree entry point. Set from the CSV's <c>root</c>
    ///     column. This is the <b>only</b> thing that makes a node a root; an empty <see cref="prereqs" />
    ///     list no longer implies rootness, so a linked node can never become an accidental root. A tree may
    ///     have any number of roots (the gold tree has one, <c>sword_dmg</c>; the Reincarnate tree has one
    ///     per independent branch).
    /// </summary>
    public bool isRoot;

    /// <summary>
    ///     Ids of the nodes this one is linked to. Links are <b>symmetric and stored on both ends</b> (the
    ///     editor writes each link into both nodes' lists), so this list is the node's full set of
    ///     neighbours. A non-root node is revealed once ANY linked node reaches level 1. There's no arrow on
    ///     the connector — buying either endpoint's first level unlocks the other. (Reveal also honours a
    ///     one-sided link via <see cref="SkillTreeSO.GetDependents" />, in case a link was hand-authored on
    ///     only one end.)
    /// </summary>
    public List<string> prereqs = new List<string>();

    /// <summary>Layout coordinates for the tree UI (column, row); multiplied by spacing by the view.</summary>
    public float x;
    public float y;
}
