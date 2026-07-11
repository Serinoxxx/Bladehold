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

    [Tooltip("Applied to the player rig's Animator on load, swapping attack clips while keeping the shared state graph. Null = keep the controller as authored (the Swordsman).")]
    public AnimatorOverrideController animatorOverride;

    [Tooltip("Seconds of holding the attack button per melee charge level for this class (heavier weapons charge slower).")]
    public float chargeTimePerLevel = 1f;

    [Tooltip("This class's gold skill tree. Null = SkillTreeService's serialized default (the Swordsman tree).")]
    public SkillTreeSO skillTree;
}
