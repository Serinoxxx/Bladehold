using UnityEngine;

/// <summary>
///     Asset-side definition of a playable class (Swordsman, Berserker, …). Holds only asset-safe data —
///     scene references (weapon GameObjects, triggers) live in <see cref="PlayerClassController" />'s
///     slot list on the Player prefab, keyed by this asset. The chosen class's <see cref="id" /> is
///     persisted in <see cref="SaveData.playerClassId" /> and applied on scene load.
/// </summary>
[CreateAssetMenu(fileName = "ClassDefinitionSO", menuName = "Scriptable Objects/ClassDefinitionSO")]
public class ClassDefinitionSO : ScriptableObject
{
    [Tooltip("Stable identifier persisted in the save file. Never rename once shipped.")]
    public string id = "swordsman";

    [Tooltip("Player-facing name shown on the class-select UI.")]
    public string displayName;

    [Tooltip("Short player-facing blurb for the class-select UI.")]
    [TextArea] public string description;

    /// <summary>The class name in the active language (Strings.csv key <c>class.&lt;id&gt;.name</c>), falling back to this asset's English.</summary>
    public string LocalizedDisplayName => Loc.Get("class." + id + ".name", displayName);

    /// <summary>The class blurb in the active language (Strings.csv key <c>class.&lt;id&gt;.desc</c>), falling back to this asset's English.</summary>
    public string LocalizedDescription => Loc.Get("class." + id + ".desc", description);

    [Tooltip("Applied to the player rig's Animator on load, swapping attack clips while keeping the shared state graph. Null = keep the controller as authored (the Swordsman).")]
    public AnimatorOverrideController animatorOverride;

    [Tooltip("Character model prefab swapped onto the shared player rig at scene load — a Synty Sidekick sharing the rig's base skeleton (bone names match; outfit-only bones like cape danglers are grafted on automatically). Its SkinnedMeshRenderers are re-bound onto the existing bones by name, so the Animator, animation events, weapon bones, and camera all keep working untouched. Null = keep the model as authored (the Swordsman).")]
    public GameObject characterModelPrefab;

    [Tooltip("Melee weapon type integer parameter sent to the player Animator on class load for state machine branching (0 = Sword, 1 = Greataxe, 2 = Staff).")]
    public int meleeWeaponType = 0;

    [Tooltip("The icon shown on the HUD for this class's melee weapon.")]
    public Sprite meleeIcon;

    [Tooltip("The icon shown on the HUD for this class's ranged weapon.")]
    public Sprite rangedIcon;

    [Tooltip("Seconds of holding the attack button per melee charge level for this class (heavier weapons charge slower).")]
    public float chargeTimePerLevel = 0.33f;

    [Tooltip("Minimum seconds between melee attacks for this class (prevents mid-swing click interruptions).")]
    public float meleeAttackCooldown = 0.5f;

    [Tooltip("This class's gold skill tree. Null = SkillTreeService's serialized default (the Swordsman tree).")]
    public SkillTreeSO skillTree;

    [Tooltip("Skill-tree node ids showcased as this class's \"Key Skills\" on the class-select screen (~3).")]
    public string[] keySkillIds;

    [Header("Character Select UI Presentation")]
    [Tooltip("Individual character/role name shown under the portrait (e.g. Galan, Bronin, Casteria).")]
    public string characterName;

    [Tooltip("Hero portrait sprite shown on the class select card.")]
    public Sprite portrait;

    [Tooltip("Health display string shown under the class title (e.g. 100/100, 250/250, 80/80).")]
    public string healthDisplay;

    [System.Serializable]
    public class KeySkillEntry
    {
        [Tooltip("Skill tree node id (optional).")]
        public string skillId;
        [Tooltip("Display name of the skill shown in the tooltip.")]
        public string skillTitle;
        [TextArea(2, 4)]
        [Tooltip("Detailed description of the skill shown in the tooltip.")]
        public string skillDescription;
        [Tooltip("Icon sprite for the preview badge.")]
        public Sprite icon;

        public KeySkillEntry() { }

        public KeySkillEntry(string id, string title, string description, Sprite iconSprite)
        {
            skillId = id;
            skillTitle = title;
            skillDescription = description;
            icon = iconSprite;
        }
    }

    [Tooltip("Key skills showcased on the character select card (name, description, icon).")]
    public System.Collections.Generic.List<KeySkillEntry> keySkills = new System.Collections.Generic.List<KeySkillEntry>();

    /// <summary>The character name, falling back to displayName if unset.</summary>
    public string CharacterName => !string.IsNullOrEmpty(characterName) ? characterName : displayName;

    /// <summary>This class's skill tree, falling back to <paramref name="defaultTree" /> when <see cref="skillTree" /> is unset (the Swordsman).</summary>
    public SkillTreeSO ResolveSkillTree(SkillTreeSO defaultTree) => skillTree != null ? skillTree : defaultTree;
}
