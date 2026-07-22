using UnityEngine;

/// <summary>
///     Base tunables for the Impulse buff, registered as <see cref="PlayerStats" /> bases by
///     <see cref="ImpulseBuff" /> in <c>Start</c> (same convention as <see cref="DeathNovaSO" />).
///     Gold-tree upgrades layer modifiers on top of these without ever mutating this asset. The
///     enemy/reaction-side tunables (resistance defaults, launch shaping, recovery timing) live on
///     <see cref="ImpulseConfigSO" /> instead.
/// </summary>
[CreateAssetMenu(fileName = "ImpulseSO", menuName = "Scriptable Objects/ImpulseSO")]
public class ImpulseSO : ScriptableObject
{
    [Tooltip("Seconds of buff granted per orb before upgrades. 0 = the Impulse feature is locked until the 'Impulse' node is bought (it grants the first 3s).")]
    public float baseOrbDurationSeconds = 0f;

    [Tooltip("Multiplier applied to knockback force per orb stack. (1.5 = 1.5x knockback force at 1 stack, 3.0x at 2 stacks, etc)")]
    public float knockbackMultiplierPerStack = 1.5f;

    [Tooltip("Extra sword damage per orb stacked beyond the first (0.10 = +10% per extra stack).")]
    public float damagePerExtraStackPercent = 0.10f;
}
