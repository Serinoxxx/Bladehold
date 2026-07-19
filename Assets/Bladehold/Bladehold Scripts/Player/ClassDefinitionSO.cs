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

    [Tooltip("Seconds of holding the attack button per melee charge level for this class (heavier weapons charge slower).")]
    public float chargeTimePerLevel = 1f;

    [Tooltip("This class's gold skill tree. Null = SkillTreeService's serialized default (the Swordsman tree).")]
    public SkillTreeSO skillTree;

    [Tooltip("Skill-tree node ids showcased as this class's \"Key Skills\" on the class-select screen (~3).")]
    public string[] keySkillIds;

    /// <summary>This class's skill tree, falling back to <paramref name="defaultTree" /> when <see cref="skillTree" /> is unset (the Swordsman).</summary>
    public SkillTreeSO ResolveSkillTree(SkillTreeSO defaultTree) => skillTree != null ? skillTree : defaultTree;
}
