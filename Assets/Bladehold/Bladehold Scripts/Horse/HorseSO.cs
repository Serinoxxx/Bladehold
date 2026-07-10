using UnityEngine;

/// <summary>
///     Shared tunables for the Horse prefab: player-mode locomotion (<see cref="HorseMotor" />), the
///     charge trample hitbox (<see cref="HorseChargeDamage" />, used by both the player's Shift-charge
///     and the mounted knight's AI charge), and mounting. Knight-AI-specific numbers (standoff,
///     telegraph, charge speed/distance) live on <c>MountedKnightSO</c> instead.
/// </summary>
[CreateAssetMenu(fileName = "HorseSO", menuName = "Scriptable Objects/HorseSO")]
public class HorseSO : ScriptableObject
{
    [Header("Locomotion (player mode)")]
    [Tooltip("Top forward speed in m/s at full W without charging.")]
    public float maxSpeed = 8f;

    [Tooltip("Top reverse speed in m/s (S while stopped).")]
    public float reverseSpeed = 2f;

    [Tooltip("Forward acceleration in m/s² while W is held.")]
    public float acceleration = 6f;

    [Tooltip("Passive deceleration in m/s² when no forward/back input is held.")]
    public float deceleration = 8f;

    [Tooltip("Braking deceleration in m/s² when input opposes the current motion (S while moving forward).")]
    public float brakeDeceleration = 12f;

    [Tooltip("Turn rate in degrees per second at full A/D.")]
    public float turnDegreesPerSecond = 90f;

    [Tooltip("Constant downward acceleration applied through the CharacterController.")]
    public float gravity = -20f;

    [Tooltip("Seconds the rear animation locks the horse in place (mount flavor / knight telegraph pose).")]
    public float rearSeconds = 1.2f;

    [Header("Charge (player mode)")]
    [Tooltip("Top speed in m/s while Shift-charging.")]
    public float chargeSpeed = 12f;

    [Tooltip("Extra acceleration in m/s² while charging.")]
    public float chargeAcceleration = 10f;

    [Tooltip("Fraction of Max Speed the horse must already be doing for held Shift to count as charging (below it, Shift just accelerates). The trample hitbox only ticks while actually charging.")]
    [Range(0f, 1f)]
    public float chargeMinSpeedFraction = 0.5f;

    [Header("Charge damage (both modes — the TrollSlamAttackSO shape)")]
    [Tooltip("Damage per trample hit in player mode. The knight's AI charge overrides this via HorseChargeDamage.BeginCharge (roster damage × MountedKnightSO.chargeDamageMultiplier).")]
    public float chargeDamage = 15f;

    public DamageType damageType = DamageType.blunt;

    [Tooltip("Impulse rating stamped on trample hits — at or above a victim's resistance it is ragdoll-flung (see ImpulseReceiver).")]
    public float impulsePower = 10f;

    [Tooltip("Launch speed in m/s for flung victims.")]
    public float impulseForce = 14f;

    [Tooltip("Seconds before the same target can be trampled again (matters for long player charges through a horde).")]
    public float hitCooldownSeconds = 1f;

    [Tooltip("Half extents of the trample overlap box ahead of the horse.")]
    public Vector3 hitBoxHalfExtents = new Vector3(1.2f, 1.2f, 1.6f);

    [Tooltip("How far ahead of the horse's origin the trample box is centred.")]
    public float hitBoxForwardOffset = 1.8f;

    [Header("Mounting")]
    [Tooltip("Local-space offset from the horse where the player lands on dismount (x = to the side).")]
    public Vector3 dismountLocalOffset = new Vector3(1.4f, 0f, 0f);
}
