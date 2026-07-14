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

    [Tooltip("Fraction of Max Speed the horse must already be doing for held Shift to count as charging (below it, Shift just accelerates).")]
    [Range(0f, 1f)]
    public float chargeMinSpeedFraction = 0.5f;

    [Header("Stamina (player mode)")]
    [Tooltip("Stamina pool. Charging drains it; an empty pool locks charging until it recovers (see Exhausted Recovery Fraction).")]
    public float maxStamina = 100f;

    [Tooltip("Stamina drained per second while actually charging (Shift held at charging speed).")]
    public float staminaDrainPerSecond = 25f;

    [Tooltip("Stamina regained per second while not charging.")]
    public float staminaRegenPerSecond = 15f;

    [Tooltip("Fraction of Max Stamina an exhausted horse must recover before it can charge again — hysteresis so charging doesn't stutter on/off at empty.")]
    [Range(0f, 1f)]
    public float exhaustedRecoveryFraction = 0.35f;

    [Header("Trample (player mode)")]
    [Tooltip("Fraction of (stat-scaled) Max Speed above which the horse tramples anything it runs into, charging or not. Below it the horse is harmless.")]
    [Range(0f, 1f)]
    public float trampleMinSpeedFraction = 0.55f;

    [Tooltip("Fraction of Charge Damage (and impulse power/force) dealt right at the trample threshold speed; scales linearly up to 1.0 at full charge speed.")]
    [Range(0f, 1f)]
    public float trampleMinDamageFraction = 0.35f;

    [Tooltip("Fraction of current speed the horse loses per victim trampled, before the per-resistance term — running through a horde bleeds momentum.")]
    [Range(0f, 1f)]
    public float hitSpeedLossFraction = 0.04f;

    [Tooltip("Extra fraction of current speed lost per point of the victim's impulse resistance (the roster CSV column) — heavies like the Troll (50) stop a charge dead.")]
    public float hitSpeedLossPerResistance = 0.02f;

    [Header("Charge damage (both modes — the TrollSlamAttackSO shape)")]
    [Tooltip("Damage per trample hit at full charge speed in player mode (scaled down toward Trample Min Damage Fraction at lower speeds). The knight's AI charge overrides this via HorseChargeDamage.BeginCharge (roster damage × MountedKnightSO.chargeDamageMultiplier).")]
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

    [Header("Crowd (player mode)")]
    [Tooltip("Layers the ridden horse never physically collides with (excluded from its CharacterController) — it shoulders these aside instead. Defaults to Enemy | Ragdoll.")]
    public LayerMask crowdLayers = (1 << 7) | (1 << 8);

    [Tooltip("Radius of the crowd scan sphere around the horse's chest — enemies inside are nudged aside and counted for drag. 0 disables both.")]
    public float crowdPushRadius = 3f;

    [Tooltip("How far ahead of the horse's origin the crowd scan sphere is centred.")]
    public float crowdForwardOffset = 1f;

    [Tooltip("Lateral nudge speed in m/s applied to enemies inside the scan at full (non-charge) speed — scales down with horse speed and with distance from the scan centre.")]
    public float crowdPushSpeed = 4f;

    [Tooltip("Fraction of target speed lost per enemy in the front half of the scan — riding into a crowd eases the horse off instead of hard-stopping it.")]
    [Range(0f, 1f)]
    public float crowdDragPerEnemy = 0.12f;

    [Tooltip("Floor for the crowd drag multiplier — even a wall of enemies never drags the target speed below this fraction.")]
    [Range(0f, 1f)]
    public float crowdMinSpeedFraction = 0.35f;

    [Tooltip("How fast (m/s²) CurrentSpeed is pulled down toward the speed the CharacterController actually achieved when level geometry blocks it — prevents banked speed from bursting out the moment an obstruction clears.")]
    public float blockedSpeedReconcileRate = 30f;

    [Header("Mounting")]
    [Tooltip("Local-space offset from the horse where the player lands on dismount (x = to the side).")]
    public Vector3 dismountLocalOffset = new Vector3(1.4f, 0f, 0f);
}
