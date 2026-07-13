using UnityEngine;

/// <summary>
///     Base tunables for the Mage's wand, registered as <see cref="PlayerStats" /> bases by
///     <see cref="PlayerWand" /> in <c>Start</c> (the <see cref="ThrownAxeSO" /> convention).
///     Skill-tree upgrades layer modifiers on top of these without ever mutating this asset. Like the
///     bow's draw, the charge works out of the box — the wand's wind-up *is* the weapon — so the
///     charge bases start above zero. Elemental behaviour (fireball explosions, chains, chills) is
///     not the wand's concern: it lives on <see cref="MageImbuement" />, which listens to the wand's
///     hits exactly like it listens to the staff's.
/// </summary>
[CreateAssetMenu(fileName = "WandSO", menuName = "Scriptable Objects/WandSO")]
public class WandSO : ScriptableObject
{
    [Header("Missile")]
    [Tooltip("Damage of one magic missile before charge/crit/multipliers. Registered as the WandDamage stat base.")]
    public float baseDamage = 12f;

    [Tooltip("Maximum flight distance of a missile, in metres.")]
    public float maxRange = 30f;

    [Tooltip("Minimum seconds between shots while holding aim.")]
    public float shotCooldownSeconds = 0.5f;

    [Header("Flight (the missile is a real projectile — MagicMissileProjectile does the damaging as it travels)")]
    [Tooltip("Metres per second the missile flies. Faster than the axe's lingering hazard — a bolt, not a bowling ball.")]
    public float projectileSpeed = 24f;

    [Tooltip("Radius of the missile's swept hit volume in metres (sphere-cast from last to current position each tick).")]
    public float missileRadius = 0.25f;

    [Header("Charge (power builds while aiming, the bow-draw convention)")]
    [Tooltip("Seconds of aiming to gain each charge level (level 1 at 1x, level 2 at 2x, ...).")]
    public float chargeTimePerLevel = 0.7f;

    [Tooltip("Charge levels the wind-up can reach before upgrades. Registered as the WandMaxChargeLevels stat base.")]
    public float baseMaxChargeLevels = 3f;

    [Tooltip("Extra missile damage per charge level before upgrades (0.5 = +50% per level). Registered as the WandChargeDamageBonus stat base.")]
    public float baseChargeDamageBonus = 0.5f;

    [Header("Knockback")]
    [Tooltip("Knockback impulse each missile hit shoves its target with, before upgrades. Registered as the WandKnockback stat base.")]
    public float baseKnockback = 2f;

    [Header("Aim camera (consumed by BowAimCamera via IChargedAimWeapon)")]
    [Tooltip("Camera boom distance while aiming, in metres (the rig's authored distance is ~5; smaller = zoomed in).")]
    public float aimCameraDistance = 2.75f;

    [Tooltip("Horizontal camera offset while aiming, in metres — positive moves the camera over the right shoulder.")]
    public float aimCameraHorizontalOffset = 0.7f;

    [Tooltip("Camera field of view while aiming, as a percentage of the resting FOV (1 = unchanged).")]
    public float aimFieldOfViewPercent = 1f;

    [Tooltip("Seconds the camera takes to blend into (and out of) the aim framing.")]
    public float aimBlendSeconds = 0.2f;
}
