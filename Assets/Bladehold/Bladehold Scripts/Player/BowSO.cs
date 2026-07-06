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

    [Tooltip("Maximum hitscan distance of an arrow, in metres.")]
    public float maxRange = 60f;

    [Tooltip("Minimum seconds between shots while holding aim.")]
    public float fireCooldownSeconds = 0.35f;

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

    [Header("Impulse blasts (Exploding Heads / Unstable Orbs)")]
    [Tooltip("Radius of the impulse blast detonated by Exploding Heads headshots and Unstable Orbs orb hits.")]
    public float impulseBlastRadius = 4f;

    [Tooltip("Impulse Power of the blast: against an enemy of resistance r, power >= r flings, power >= r-1 knocks down (see ImpulseReceiver).")]
    public float impulseBlastPower = 2f;

    [Tooltip("Launch speed in m/s seeded onto flung ragdolls by the blast (the ImpulseSO.baseImpulseForce convention).")]
    public float impulseBlastForce = 10f;
}
