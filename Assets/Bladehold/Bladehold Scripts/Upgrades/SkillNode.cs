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
///     A draft card as the card UI sees it, built from a <see cref="DraftUpgradeDefinition" /> by
///     <see cref="DraftUpgradeService.ConvertToSkillNode" />. (The name is left over from the deleted gold
///     skill tree, whose nodes had the same shape; the tree-only fields below are unused by drafts.)
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
    ///     <see cref="Loc" /> key prefix for this card's text. The UI appends
    ///     <c>.name</c>/<c>.desc</c>/<c>.upgrade</c> — see the Localized* properties below. Empty = no
    ///     localization (everything falls back to the CSV English).
    /// </summary>
    public string locKey = "";

    /// <summary>The display name in the active language, falling back to the gameplay CSV's English.</summary>
    public string LocalizedDisplayName =>
        string.IsNullOrEmpty(locKey) ? displayName : Loc.Get(locKey + ".name", displayName);

    /// <summary>The unlock text in the active language, falling back to the gameplay CSV's English.</summary>
    public string LocalizedDescription =>
        string.IsNullOrEmpty(locKey) ? description : Loc.Get(locKey + ".desc", description);

    /// <summary>The upgrade text in the active language; empty (in both languages) falls back to <see cref="LocalizedDescription" /> at the callsite.</summary>
    public string LocalizedUpgradeText =>
        string.IsNullOrEmpty(locKey) || string.IsNullOrEmpty(upgradeText) ? upgradeText : Loc.Get(locKey + ".upgrade", upgradeText);

    /// <summary>
    ///     Name of this card's icon sprite, resolved by <see cref="DraftUpgradeService.GetIcon" />
    ///     (Resources/SkillTreeIcons). Empty = no icon.
    /// </summary>
    public string iconName = "";

    /// <summary>How many times this node can be purchased (levels). 1 = a single-purchase node.</summary>
    public int maxLevel = 1;

    /// <summary>
    ///     Cost of each level (tree-era field; drafts are free). Index 0 = level 1.
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
    ///     have any number of roots (tree-era field).
    /// </summary>
    public bool isRoot;

    /// <summary>
    ///     Ids of the nodes this one is linked to. Links are <b>symmetric and stored on both ends</b> (the
    ///     editor writes each link into both nodes' lists), so this list is the node's full set of
    ///     neighbours. A non-root node is revealed once ANY linked node reaches level 1. There's no arrow on
    ///     the connector — buying either endpoint's first level unlocks the other. (Tree-era field.)
    /// </summary>
    public List<string> prereqs = new List<string>();

    /// <summary>Layout coordinates for the tree UI (column, row); multiplied by spacing by the view.</summary>
    public float x;
    public float y;

    /// <summary>True if this skill is purchasable in the Main Menu Meta-Progression Grid with persistent Gold.</summary>
    public bool isMeta = true;

    /// <summary>True if this skill can be drafted mid-run upon level up.</summary>
    public bool isCard = true;

    /// <summary>True if this skill is an active weapon/ability that counts towards the in-run Active Weapon slot limit (max 4).</summary>
    public bool isActiveWeapon = false;
}
