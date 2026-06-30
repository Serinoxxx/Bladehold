using System.Collections.Generic;

/// <summary>
///     One node in the skill tree, parsed from a row of the CSV by <see cref="SkillTreeSO" />. A node may
///     grant a stat modifier (<see cref="hasEffect" /> true) or be a pure connector/unlock node (effect
///     columns blank). Duplicate effects across different ids are intentional — buying both stacks the buff.
/// </summary>
public class SkillNode
{
    /// <summary>Unique node id (the CSV's first column). Each id can be purchased at most once.</summary>
    public string id;

    public string displayName;
    public string description;

    /// <summary>Gold cost to purchase.</summary>
    public int cost;

    /// <summary>Whether this node applies a stat modifier when purchased.</summary>
    public bool hasEffect;

    /// <summary>The stat this node modifies (only meaningful when <see cref="hasEffect" />).</summary>
    public StatType stat;

    /// <summary>How the modifier combines (Flat or Percent).</summary>
    public ModifierKind kind;

    /// <summary>The modifier amount (e.g. 1 for +1, 0.05 for +5%).</summary>
    public float amount;

    /// <summary>Ids of prerequisite nodes. Empty = a root, visible from the start. Otherwise revealed once ANY prereq is purchased.</summary>
    public List<string> prereqs = new List<string>();

    /// <summary>Layout coordinates for the tree UI (column, row); multiplied by spacing by the view.</summary>
    public float x;
    public float y;
}
