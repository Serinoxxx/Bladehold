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

    [Tooltip("Base Impulse Power before upgrades. Against an enemy of resistance r: power >= r flings, power >= r-1 knocks down.")]
    public float basePower = 0f;

    [Tooltip("Launch speed in m/s seeded onto the ragdoll bodies, before power/stack/charge amplification.")]
    public float baseImpulseForce = 10f;

    [Tooltip("Extra launch force per point of Impulse Power (0.15 = +15% per point), so every power tier is felt even before it crosses an enemy's resistance threshold.")]
    public float forcePerPower = 0.15f;

    [Tooltip("Extra Impulse Power per attack charge level held (0.25 = a full 4-level charge pierces 1 more resistance), so charging genuinely helps topple resistant enemies.")]
    public float powerPerChargeLevel = 0.25f;

    [Tooltip("Extra launch force per orb stacked beyond the first (0.15 = +15% per extra stack).")]
    public float forcePerExtraStackPercent = 0.15f;

    [Tooltip("Extra sword damage per orb stacked beyond the first (0.10 = +10% per extra stack).")]
    public float damagePerExtraStackPercent = 0.10f;
}
