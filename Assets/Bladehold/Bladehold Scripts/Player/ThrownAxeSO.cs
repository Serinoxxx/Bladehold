using UnityEngine;

/// <summary>
///     Base tunables for the Berserker's throwing axe, registered as <see cref="PlayerStats" /> bases
///     by <see cref="PlayerThrownAxe" /> in <c>Start</c> (the <see cref="BowSO" /> convention).
///     Skill-tree upgrades layer modifiers on top of these without ever mutating this asset. Like the
///     bow's draw, the charge works out of the box — winding up while aiming *is* the weapon — so the
///     charge bases start above zero.
/// </summary>
[CreateAssetMenu(fileName = "ThrownAxeSO", menuName = "Scriptable Objects/ThrownAxeSO")]
public class ThrownAxeSO : ScriptableObject
{
    [Header("Throw")]
    [Tooltip("Damage of one throw per enemy hit, before charge/crit/multipliers. Registered as the AxeThrowDamage stat base.")]
    public float baseDamage = 25f;

    [Tooltip("Maximum flight distance of a throw, in metres. With Boomerang this is where the axe turns around.")]
    public float maxRange = 25f;

    [Tooltip("Minimum seconds between throws while holding aim.")]
    public float throwCooldownSeconds = 0.6f;

    [Header("Flight (the axe is a real projectile — AxeProjectile does the damaging as it travels)")]
    [Tooltip("Metres per second the thrown axe flies. Deliberately slow — the axe is a lingering hazard the horde walks into, not a hitscan bolt.")]
    public float projectileSpeed = 12f;

    [Header("Boomerang (locked until the axe_boomerang node sets AxeBoomerangUnlocked)")]
    [Tooltip("Return-leg speed as a multiple of projectileSpeed (the flick of the wrist coming home).")]
    public float returnSpeedMultiplier = 1.25f;

    [Header("Charge (power builds while aiming, the bow-draw convention)")]
    [Tooltip("Seconds of aiming to gain each charge level (level 1 at 1x, level 2 at 2x, ...).")]
    public float chargeTimePerLevel = 0.33f;

    [Tooltip("Charge levels the wind-up can reach before upgrades. Registered as the AxeThrowMaxChargeLevels stat base.")]
    public float baseMaxChargeLevels = 1f;

    [Tooltip("Extra throw damage per charge level before upgrades (0.5 = +50% per level). Registered as the AxeThrowChargeDamageBonus stat base.")]
    public float baseChargeDamageBonus = 0f;

    [Header("Piercing line")]
    [Tooltip("Enemies one uncharged throw can pass through before upgrades. Registered as the AxeThrowPierceCount stat base.")]
    public float basePierceCount = 2f;

    [Tooltip("Extra enemies pierced per charge level held.")]
    public float piercePerChargeLevel = 1f;

    [Tooltip("Width of the axe's flight line in metres, before upgrades. Registered as the AxeThrowWidth stat base.")]
    public float baseWidth = 0.6f;

    [Header("Knockback")]
    [Tooltip("Knockback impulse each hit shoves its target with, before upgrades. Registered as the AxeThrowKnockback stat base.")]
    public float baseKnockback = 6f;

    [Tooltip("Extra knockback per charge level held, as a fraction (0.25 = +25% per level).")]
    public float knockbackPerChargeLevel = 0.25f;

    [Header("Aim camera (consumed by BowAimCamera via IChargedAimWeapon)")]
    [Tooltip("Ranged weapon type integer passed to the player Animator during aim (0 = Bow, 1 = Thrown Axe, 2 = Wand).")]
    public int rangedWeaponType = 1;

    [Tooltip("Camera boom distance while aiming, in metres (the rig's authored distance is ~5; smaller = zoomed in).")]
    public float aimCameraDistance = 1f;

    [Tooltip("Horizontal camera offset while aiming, in metres — positive moves the camera over the right shoulder.")]
    public float aimCameraHorizontalOffset = 0.7f;

    [Tooltip("Camera field of view while aiming, as a percentage of the resting FOV (1 = unchanged).")]
    public float aimFieldOfViewPercent = 1f;

    [Tooltip("Seconds the camera takes to blend into (and out of) the aim framing.")]
    public float aimBlendSeconds = 0.2f;
}
