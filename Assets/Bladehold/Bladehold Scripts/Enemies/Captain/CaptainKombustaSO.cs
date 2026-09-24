using UnityEngine;

/// <summary>
///     Tunables for Captain Kombusta, a Clan Captain specializing in:
///     1. Long-range 10-dynamite volley (at distance >= 5m, 1/sec, 2m telegraphed radius, 20 damage).
///     2. Close-range self-immolation (lights on fire after 3 seconds in melee range, dealing DoT).
/// </summary>
[CreateAssetMenu(fileName = "CaptainKombustaSO", menuName = "Scriptable Objects/Enemies/Captain Kombusta SO")]
public class CaptainKombustaSO : ScriptableObject
{
    [Header("Base Stats")]
    [Tooltip("Base maximum health before banner difficulty scaling.")]
    public float baseMaxHealth = 350f;

    [Tooltip("Base melee attack damage.")]
    public float baseMeleeDamage = 15f;

    [Tooltip("Base movement speed.")]
    public float baseMoveSpeed = 3.8f;

    [Header("Special Attack: Dynamite Volley")]
    [Tooltip("Distance from target to qualify for dynamite special attack (default: 5m or more).")]
    public float dynamiteTriggerDistance = 5.0f;

    [Tooltip("Total number of dynamite sticks thrown per volley (default: 10).")]
    public int dynamiteCount = 10;

    [Tooltip("Interval in seconds between each thrown dynamite (default: 3.0s: 2s telegraph + 1s gap).")]
    public float dynamiteInterval = 3.0f;

    [Tooltip("Explosion damage dealt by each dynamite stick (default: 20).")]
    public float dynamiteDamage = 20.0f;

    [Tooltip("Radius of the ground telegraph and explosion area (default: 2.0m).")]
    public float dynamiteExplosionRadius = 2.0f;

    [Tooltip("Flight travel time in seconds for each thrown dynamite to target position / telegraph duration (default: 2.0s).")]
    public float dynamiteFlightTime = 2.0f;

    [Tooltip("Arc height of the thrown dynamite trajectory.")]
    public float dynamiteArcHeight = 2.5f;

    [Tooltip("Knockback force applied to targets caught in the explosion.")]
    public float explosionKnockback = 8.0f;

    [Tooltip("Cooldown in seconds after a complete 10-dynamite barrage before another can occur.")]
    public float dynamiteSpecialCooldown = 12.0f;

    [Header("Melee Proximity & Self-Immolation")]
    [Tooltip("Maximum distance from target considered 'melee range' (default: 3.5m).")]
    public float meleeRangeThreshold = 3.5f;

    [Tooltip("Continuous seconds target must spend in melee range before captain ignites (default: 3.0s).")]
    public float meleeIgniteDelay = 3.0f;

    [Tooltip("Radius of the burning aura around the captain while on fire (default: 4.0m).")]
    public float burnAuraRadius = 4.0f;

    [Tooltip("Damage dealt per second to targets staying in burn aura range.")]
    public float burnDamagePerSecond = 10.0f;

    [Tooltip("Interval between burn damage ticks in seconds.")]
    public float burnTickInterval = 0.5f;

    [Tooltip("Duration in seconds that Captain Kombusta remains on fire before extinguishing.")]
    public float burnDuration = 8.0f;

    [Header("Visual & Audio Assets")]
    [Tooltip("Ground telegraph prefab scaled to dynamiteExplosionRadius * 2.")]
    public GameObject telegraphPrefab;

    [Tooltip("Explosion particle effect prefab spawned on detonation.")]
    public GameObject explosionVfxPrefab;

    [Tooltip("Fire particle effect prefab attached to captain while on fire.")]
    public GameObject fireAuraVfxPrefab;

    [Tooltip("Optional 3D dynamite stick model or projectile prefab.")]
    public GameObject dynamitePrefab;

    [Tooltip("Sound played on dynamite explosion.")]
    public AudioClip explosionSfx;

    [Tooltip("Sound played while fuse is burning or throwing.")]
    public AudioClip fuseSfx;

    [Tooltip("Sound played when Captain Kombusta ignites himself.")]
    public AudioClip igniteSfx;
}
