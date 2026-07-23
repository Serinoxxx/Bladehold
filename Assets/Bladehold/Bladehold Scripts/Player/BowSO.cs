using UnityEngine;

/// <summary>
///     Base tunables for the player's bow, registered as <see cref="PlayerStats" /> bases by
///     <see cref="PlayerBow" /> in <c>Start</c> (same convention as <see cref="ImpulseSO" /> /
///     <see cref="ChainLightningSO" />). Skill-tree upgrades layer modifiers on top of these without
///     ever mutating this asset. Unlike the sword's Heavy Strike, the bow's charge works out of the
///     box — drawing while aiming *is* the weapon — so its charge bases start above zero.
/// </summary>
[CreateAssetMenu(fileName = "BowSO", menuName = "Scriptable Objects/BowSO")]
public class BowSO : ScriptableObject
{
    [Header("Shot")]
    [Tooltip("Damage of one arrow before charge/crit/multipliers. Registered as the BowDamage stat base.")]
    public float baseDamage = 15f;

    [Tooltip("Maximum flight distance of an arrow along its path, in metres (also the aim ray's reach).")]
    public float maxRange = 60f;

    [Tooltip("Minimum seconds between shots while holding aim.")]
    public float fireCooldownSeconds = 0.35f;

    [Header("Arrow flight (consumed by ArrowProjectile)")]
    [Tooltip("Flight speed of an arrow in metres per second before upgrades. Registered as the BowArrowSpeed stat base — fairly slow on purpose; the Swift Arrows line raises it (and less flight time = less drop).")]
    public float baseArrowSpeed = 30f;

    [Tooltip("Downward acceleration on an arrow in flight, in m/s². Fixed — arrow speed upgrades flatten the arc by shortening flight time, not by changing gravity.")]
    public float arrowGravity = 9.81f;

    [Tooltip("Radius of the arrow's swept damage volume in metres (the AxeProjectile sphere-cast convention — a little forgiveness so thin colliders can't slip between ticks).")]
    public float arrowRadius = 0.05f;

    [Header("Charge (power builds while aiming, the sword-hold convention)")]
    [Tooltip("Seconds of aiming to gain each charge level (level 1 at 1x, level 2 at 2x, ...).")]
    public float chargeTimePerLevel = 0.6f;

    [Tooltip("Charge levels the draw can reach before upgrades. Registered as the BowMaxChargeLevels stat base.")]
    public float baseMaxChargeLevels = 3f;

    [Tooltip("Extra arrow damage per charge level before upgrades (0.5 = +50% per level). Registered as the BowChargeDamageBonus stat base.")]
    public float baseChargeDamageBonus = 0.5f;

    [Header("Multi Shot")]
    [Tooltip("Horizontal degrees between neighbouring arrows when Multi Shot fans extra arrows around the main one.")]
    public float multishotSpreadDegrees = 6f;

    [Tooltip("Fraction of the main arrow's damage each extra arrow deals before upgrades. Registered as the BowMultishotDamagePercent stat base.")]
    public float baseMultishotDamagePercent = 0.25f;

    [Header("Bounce Shot")]
    [Tooltip("World-space radius around a hit within which a bounce looks for one additional enemy (the chain-lightning convention).")]
    public float bounceRadius = 6f;

    [Header("Pickup Arrows")]
    [Tooltip("Radius of the capsule swept along the arrow's flight path when collecting gold/power-ups (the Pickup Arrows skill).")]
    public float pickupRadius = 1.5f;

    [Header("Freezing Draw")]
    [Tooltip("World-space radius around the player within which enemies are slowed while the bow is drawn.")]
    public float freezingDrawRadius = 8f;

    [Header("Brain Freeze")]
    [Tooltip("Seconds a Brain Freeze headshot slow lasts before the Elongated Freeze bonus extends it. The brainfreeze_* skill descriptions assume 3.")]
    public float brainFreezeSeconds = 3f;

    [Header("Aim camera (consumed by BowAimCamera)")]
    [Tooltip("Ranged weapon type integer passed to the player Animator during aim (0 = Bow, 1 = Thrown Axe, 2 = Wand).")]
    public int rangedWeaponType = 0;

    [Tooltip("Camera boom distance while aiming, in metres (the rig's authored distance is ~5; smaller = zoomed in).")]
    public float aimCameraDistance = 2.75f;

    [Tooltip("Horizontal camera offset while aiming, in metres — positive moves the camera over the right shoulder.")]
    public float aimCameraHorizontalOffset = 0.7f;

    [Tooltip("Camera field of view while aiming, as a percentage of the resting FOV (1 = unchanged, 0.8 = 20% narrower). The resting FOV is restored on release.")]
    public float aimFieldOfViewPercent = 1f;

    [Tooltip("Seconds the camera takes to blend into (and out of) the aim framing. BowAimLook's spine bend blends over the same window.")]
    public float aimBlendSeconds = 0.2f;

    [Header("Aim look (consumed by BowAimLook)")]
    [Tooltip("Furthest the spine bends up or down (degrees) to follow the camera pitch while aiming.")]
    public float aimLookMaxPitchDegrees = 60f;

    [Header("Impulse blasts (Exploding Heads / Unstable Orbs)")]
    [Tooltip("Radius of the impulse blast detonated by Exploding Heads headshots and Unstable Orbs orb hits.")]
    public float impulseBlastRadius = 4f;

    [Tooltip("Knockback rating of the blast: against an enemy of resistance r, power >= r flings, power >= r-1 knocks down (see KnockbackReceiver).")]
    public float knockbackBlastForce = 10f;
}
