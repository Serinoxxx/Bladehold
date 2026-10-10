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

    [Tooltip("Passive stamina trickle per second while not charging. Kept small: stamina is earned by kills (Stamina Per Kill), so the charge is a burst you build up, not a cooldown.")]
    public float staminaRegenPerSecond = 2f;

    [Tooltip("Stamina gained for each enemy the player kills on foot (scaled by the HorseStaminaGainMultiplier stat). Banked even while dismounted, so fighting on foot charges the next trample.")]
    public float staminaPerKill = 8f;

    [Tooltip("Fraction of Stamina Per Kill earned for kills made from the saddle without trampling (mounted sword/bow). Trample kills earn nothing unless a card (Bloodlust) adds it.")]
    [Range(0f, 1f)]
    public float mountedKillStaminaFraction = 0.5f;

    [Tooltip("Fraction of Max Stamina an exhausted horse must recover before it can charge again — hysteresis so charging doesn't stutter on/off at empty.")]
    [Range(0f, 1f)]
    public float exhaustedRecoveryFraction = 0.35f;

    [Header("Trample while charging (player mode)")]
    [Tooltip("Fraction of (stat-scaled) Max Speed above which the horse tramples anything it runs into, charging or not. Below it the horse is harmless. Riding without charging applies the Cruise fractions below on top.")]
    [Range(0f, 1f)]
    public float trampleMinSpeedFraction = 0.55f;

    [Tooltip("Fraction of Charge Damage (and impulse power/force) dealt right at the trample threshold speed; scales linearly up to 1.0 at full charge speed.")]
    [Range(0f, 1f)]
    public float trampleMinDamageFraction = 0.35f;

    [Tooltip("While charging: fraction of current speed the horse loses per victim trampled, before the per-resistance term. 0 = a charge never slows.")]
    [Range(0f, 1f)]
    public float hitSpeedLossFraction = 0.04f;

    [Tooltip("While charging: extra fraction of current speed lost per point of the victim's impulse resistance (the roster CSV column) — heavies like the Troll (50) stop a charge dead.")]
    public float hitSpeedLossPerResistance = 0.02f;

    [Header("Cruise: riding without charging (player mode)")]
    [Tooltip("Fraction of the trample's damage dealt while riding without charging. Low on purpose: plain riding shoulders enemies aside, the charge is what kills.")]
    [Range(0f, 1f)]
    public float cruiseDamageFraction = 0.15f;

    [Tooltip("Fraction of the trample's knockback applied while riding without charging. 0 = only a charge knocks enemies back.")]
    [Range(0f, 1f)]
    public float cruiseKnockbackFraction = 0f;

    [Tooltip("Fraction of current speed lost per victim while riding without charging — a horde bogs a cruising horse down.")]
    [Range(0f, 1f)]
    public float cruiseHitSpeedLossFraction = 0.15f;

    [Tooltip("Extra fraction of speed lost per point of victim impulse resistance while riding without charging.")]
    public float cruiseHitSpeedLossPerResistance = 0.02f;

    [Tooltip("Crowd drag per enemy in the front arc while riding without charging (charging uses Crowd Drag Per Enemy).")]
    [Range(0f, 1f)]
    public float cruiseCrowdDragPerEnemy = 0.12f;

    [Tooltip("Floor for the crowd drag multiplier while riding without charging.")]
    [Range(0f, 1f)]
    public float cruiseCrowdMinSpeedFraction = 0.35f;

    [Header("Charge damage (both modes — the TrollSlamAttackSO shape)")]
    [Tooltip("Damage per trample hit at full charge speed in player mode (scaled down toward Trample Min Damage Fraction at lower speeds). The knight's AI charge overrides this via HorseChargeDamage.BeginCharge (roster damage × MountedKnightSO.chargeDamageMultiplier).")]
    public float chargeDamage = 15f;

    public DamageType damageType = DamageType.blunt;

    [Tooltip("Knockback rating stamped on trample hits — at or above a victim's resistance it is ragdoll-flung (see KnockbackReceiver).")]
    public float knockbackForce = 14f;

    [Tooltip("Seconds before the same target can be trampled again (matters for long player charges through a horde).")]
    public float hitCooldownSeconds = 1f;

    [Tooltip("Half extents of the trample overlap box ahead of the horse.")]
    public Vector3 hitBoxHalfExtents = new Vector3(0.6f, 0.8f, 0.8f);

    [Tooltip("How far ahead of the horse's origin the trample box is centred.")]
    public float hitBoxForwardOffset = 2.2f;

    [Header("Crowd (player mode)")]
    [Tooltip("Layers the ridden horse never physically collides with (excluded from its CharacterController) — it shoulders these aside instead. Defaults to Enemy | Ragdoll.")]
    public LayerMask crowdLayers = (1 << 7) | (1 << 8);

    [Tooltip("Radius of the crowd scan sphere around the horse's chest — enemies inside are nudged aside and counted for drag. 0 disables both.")]
    public float crowdPushRadius = 3f;

    [Tooltip("How far ahead of the horse's origin the crowd scan sphere is centred.")]
    public float crowdForwardOffset = 1f;

    [Tooltip("Lateral nudge speed in m/s applied to enemies inside the scan at full (non-charge) speed — scales down with horse speed and with distance from the scan centre.")]
    public float crowdPushSpeed = 4f;

    [Tooltip("While charging: fraction of target speed lost per enemy in the front half of the scan. 0 = a charge ploughs straight through.")]
    [Range(0f, 1f)]
    public float crowdDragPerEnemy = 0.12f;

    [Tooltip("While charging: floor for the crowd drag multiplier — even a wall of enemies never drags the target speed below this fraction.")]
    [Range(0f, 1f)]
    public float crowdMinSpeedFraction = 0.35f;

    [Tooltip("How fast (m/s²) CurrentSpeed is pulled down toward the speed the CharacterController actually achieved when level geometry blocks it — prevents banked speed from bursting out the moment an obstruction clears.")]
    public float blockedSpeedReconcileRate = 30f;

    [Header("Charge card effects (player mode, see MountChargeAbilities)")]
    [Tooltip("Blazing Hooves: metres galloped between fire trail segments while charging.")]
    public float fireTrailSpacing = 1.2f;

    [Tooltip("Blazing Hooves: seconds each fire trail segment burns.")]
    public float fireTrailLifetime = 3f;

    [Tooltip("Frost Wake: seconds between chill pulses around a charging horse.")]
    public float frostPulseInterval = 0.4f;

    [Tooltip("Frost Wake: chill stacks applied per pulse (EnemyStatusManager.ApplyStatus slow override, the slippery-ice value).")]
    public float frostChillStacks = 0.5f;

    [Header("Mount stoppers (player mode)")]
    [Tooltip("How far ahead of the horse's origin (m) a MountStopper enemy's origin can be and still count as directly in the horse's path.")]
    public float mountStopReach = 2.2f;

    [Tooltip("How far to either side of the horse's centre line (m) a MountStopper enemy can be and still count as in its path.")]
    public float mountStopHalfWidth = 1.1f;

    [Tooltip("Forward speed (m/s) at or above which running into a MountStopper rears the horse. Below it the horse just can't push forward.")]
    public float mountStopMinRearSpeed = 2.5f;

    [Header("Mounting")]
    [Tooltip("Local-space offset from the horse where the player lands on dismount (x = to the side).")]
    public Vector3 dismountLocalOffset = new Vector3(1.4f, 0f, 0f);
}
