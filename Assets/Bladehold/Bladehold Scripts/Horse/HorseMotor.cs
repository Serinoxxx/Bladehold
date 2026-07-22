using System.Collections.Generic;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The horse's locomotion authority while the PLAYER rides: W/S accelerates/brakes/reverses,
///     A/D turns, and holding Sprint (Shift) at speed charges toward the higher charge speed.
///     Moves via its own <see cref="CharacterController" /> (the knight's AI mode uses the
///     NavMeshAgent instead; a riderless horse has no driver at all). Disabled by default —
///     <c>PlayerMount</c> enables it through <see cref="SetRider" /> and reverses everything with
///     <see cref="ClearRider" />. All tunables live on <see cref="HorseSO" />; speeds scale with
///     the <see cref="StatType.HorseSpeedMultiplier" /> stat (the "Thoroughbred" line).
///
///     The shared <see cref="HorseChargeDamage" /> trample window is speed-gated, not
///     charge-gated: it opens whenever momentum passes
///     <see cref="HorseSO.trampleMinSpeedFraction" /> of max speed (charging or not), with damage
///     and impulse scaled by current speed — full values only at full charge speed. Each victim
///     trampled bleeds a fraction of the horse's speed scaled by that victim's
///     <see cref="ImpulseReceiver" /> resistance, so a charge through goblins barely slows while a
///     Troll stops it dead. Charging drains a stamina pool; an emptied pool locks charging until
///     it recovers past <see cref="HorseSO.exhaustedRecoveryFraction" />.
///
///     Enemies never physically block the horse: <see cref="SetRider" /> excludes
///     <see cref="HorseSO.crowdLayers" /> from the CharacterController, and each frame the moving
///     horse laterally nudges overlapping enemies' NavMeshAgents aside (the
///     <see cref="KnockbackReceiver" /> agent.Move idiom) while enemies in the front arc apply a
///     soft drag on target speed instead of a hard stop. If level geometry does block the
///     controller, <see cref="CurrentSpeed" /> is reconciled down to the movement actually
///     achieved, so speed can't be "banked" against an obstruction and burst out when it clears.
/// </summary>
public class HorseMotor : MonoBehaviour
{
    [SerializeField] private HorseSO horseData;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Health health;
    [SerializeField] private HorseChargeDamage chargeDamage;
    [SerializeField] private HorseAnimation horseAnimation;
    [Tooltip("The saddle transform the mounted player is parented to.")]
    [SerializeField] private Transform riderSeat;

    /// <summary>Signed forward speed in m/s (negative = reversing).</summary>
    public float CurrentSpeed { get; private set; }

    /// <summary>0..1 against the effective (stat-scaled) charge speed.</summary>
    public float NormalizedSpeed => Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, EffectiveSpeed(horseData.chargeSpeed)));

    /// <summary>-1..1 steering input this frame.</summary>
    public float TurnInput { get; private set; }

    /// <summary>True while Shift is held AND the horse is at charging speed (gallop lean + stamina drain; damage is governed by <see cref="IsTrampling" />).</summary>
    public bool IsCharging { get; private set; }

    /// <summary>True while the horse is fast enough to trample — the damage window, open with or without Shift.</summary>
    public bool IsTrampling { get; private set; }

    /// <summary>Current stamina, drained by charging (see <see cref="HorseSO.maxStamina" />).</summary>
    public float Stamina { get; private set; }

    /// <summary>0..1 stamina, for UI.</summary>
    public float NormalizedStamina => horseData != null && horseData.maxStamina > 0f ? Stamina / horseData.maxStamina : 1f;

    /// <summary>True while an emptied stamina pool locks charging (clears at the recovery threshold).</summary>
    public bool IsExhausted { get; private set; }

    /// <summary>The horse's own Health, for the mount's damage forwarding and death handling.</summary>
    public Health Health => health;

    /// <summary>The saddle transform the mounted player snaps to.</summary>
    public Transform RiderSeat => riderSeat;

    /// <summary>Local-space landing offset for a dismounting player (from <see cref="HorseSO" />).</summary>
    public Vector3 DismountLocalOffset => horseData != null ? horseData.dismountLocalOffset : new Vector3(1.4f, 0f, 0f);

    private const int MaxCrowdResults = 32;

    private InputReader inputReader;
    private IDamageable riderDamageable;
    private PlayerStats stats;
    private bool sprintHeld;
    private float verticalVelocity;
    private float crowdFactor = 1f;
    private readonly Collider[] crowdBuffer = new Collider[MaxCrowdResults];
    private readonly HashSet<NavMeshAgent> crowdScratch = new HashSet<NavMeshAgent>();
    private bool anyError = false;

    private void OnValidate()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (chargeDamage == null)
        {
            chargeDamage = GetComponent<HorseChargeDamage>();
        }
        if (horseAnimation == null)
        {
            horseAnimation = GetComponent<HorseAnimation>();
        }
    }

    private void Start()
    {
        if (horseData == null)
        {
            Debug.LogError("HorseSO is not assigned in the inspector.");
            anyError = true;
        }
        if (characterController == null)
        {
            Debug.LogError("CharacterController component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (chargeDamage == null)
        {
            Debug.LogError("HorseChargeDamage component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (riderSeat == null)
        {
            Debug.LogWarning("HorseMotor has no Rider Seat assigned; the player cannot be seated on this horse.", this);
        }

        Stamina = horseData.maxStamina;

        health.OnDied += HandleDied;
        chargeDamage.OnHit += HandleTrampleHit;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (chargeDamage != null)
        {
            chargeDamage.OnHit -= HandleTrampleHit;
        }
    }

    /// <summary>
    ///     Hands the reins to the player: enables this motor and its CharacterController, reads the
    ///     rider's <see cref="InputReader" /> from here on, and excludes the rider from trample hits.
    ///     Called by <c>PlayerMount</c>.
    /// </summary>
    public void SetRider(InputReader input, IDamageable rider)
    {
        if (anyError || health.IsDead)
        {
            return;
        }

        inputReader = input;
        riderDamageable = rider;
        stats = Player.Instance != null ? Player.Instance.Stats : null;

        if (inputReader != null)
        {
            inputReader.onSprintActivated += HandleSprintActivated;
            inputReader.onSprintDeactivated += HandleSprintDeactivated;
        }

        // Enemies (and ragdolls) never physically block the ridden horse — the crowd nudge and
        // drag in Update handle them instead. Level geometry still collides normally.
        characterController.excludeLayers |= horseData.crowdLayers;

        characterController.enabled = true;
        enabled = true;
    }

    /// <summary>Reverses <see cref="SetRider" />: stops any charge/trample, drops the reins, and disables the motor.</summary>
    public void ClearRider()
    {
        Unsubscribe();
        StopCharging();
        StopTrampling();
        sprintHeld = false;
        CurrentSpeed = 0f;
        TurnInput = 0f;
        riderDamageable = null;

        if (characterController != null)
        {
            characterController.enabled = false;
        }
        enabled = false;
    }

    /// <summary>Plays the rear animation as mount flavor — cosmetic only; movement is never locked, so the rider can move immediately.</summary>
    public void TriggerRear()
    {
        if (anyError || health.IsDead) return;

        if (horseAnimation != null)
        {
            horseAnimation.TriggerRear();
        }
    }

    private void Unsubscribe()
    {
        if (inputReader != null)
        {
            inputReader.onSprintActivated -= HandleSprintActivated;
            inputReader.onSprintDeactivated -= HandleSprintDeactivated;
            inputReader = null;
        }
    }

    private void HandleSprintActivated()
    {
        sprintHeld = true;
    }

    private void HandleSprintDeactivated()
    {
        sprintHeld = false;
    }

    private void HandleDied()
    {
        // The corpse stops where it is; PlayerMount reacts to the same OnDied and dismounts.
        ClearRider();
    }

    private float EffectiveSpeed(float baseSpeed)
    {
        float multiplier = stats != null ? stats.GetValue(StatType.HorseSpeedMultiplier) : 1f;
        return baseSpeed * (multiplier > 0f ? multiplier : 1f);
    }

    private void Update()
    {
        if (anyError || inputReader == null || health.IsDead) return;

        float dt = Time.deltaTime;
        Vector2 move = inputReader._moveComposite;

        // Charging requires held Shift AND real momentum — below the threshold Shift just pushes
        // the target speed up, so a standing start builds into the charge naturally. An exhausted
        // horse ignores Shift entirely until stamina recovers.
        bool wantsCharge = sprintHeld && move.y > 0f && !IsExhausted;
        float effectiveMax = EffectiveSpeed(wantsCharge ? horseData.chargeSpeed : horseData.maxSpeed);

        UpdateCrowd(dt);

        float targetSpeed;
        if (move.y > 0f)
        {
            targetSpeed = effectiveMax * move.y * crowdFactor;
        }
        else if (move.y < 0f)
        {
            targetSpeed = EffectiveSpeed(horseData.reverseSpeed) * move.y;
        }
        else
        {
            targetSpeed = 0f;
        }

        float rate;
        if (Mathf.Sign(targetSpeed) * Mathf.Sign(CurrentSpeed) < 0f && Mathf.Abs(CurrentSpeed) > 0.01f)
        {
            // Input opposes the current motion: brake hard first.
            rate = horseData.brakeDeceleration;
        }
        else if (Mathf.Abs(targetSpeed) > Mathf.Abs(CurrentSpeed))
        {
            rate = horseData.acceleration + (wantsCharge ? horseData.chargeAcceleration : 0f);
        }
        else
        {
            rate = horseData.deceleration;
        }
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * dt);

        // Steering: full rate regardless of speed feels responsive; the blend tree leans via Turn.
        TurnInput = Mathf.Clamp(move.x, -1f, 1f);
        if (TurnInput != 0f)
        {
            transform.Rotate(0f, TurnInput * horseData.turnDegreesPerSecond * dt, 0f);
        }

        if (characterController.isGrounded)
        {
            verticalVelocity = horseData.gravity * dt;
        }
        else
        {
            verticalVelocity += horseData.gravity * dt;
        }

        Vector3 motion = transform.forward * CurrentSpeed + Vector3.up * verticalVelocity;
        Vector3 positionBefore = transform.position;
        characterController.Move(motion * dt);

        // A controller wedged on level geometry doesn't move, but CurrentSpeed would keep
        // integrating toward target — then burst out at full speed the moment the obstruction
        // clears. Pull it down toward the forward speed actually achieved instead.
        if (dt > 0.0001f)
        {
            Vector3 achievedDelta = transform.position - positionBefore;
            achievedDelta.y = 0f;
            float achievedSpeed = Vector3.Dot(achievedDelta, transform.forward) / dt;
            if ((CurrentSpeed > 0f && achievedSpeed < CurrentSpeed) || (CurrentSpeed < 0f && achievedSpeed > CurrentSpeed))
            {
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, achievedSpeed, horseData.blockedSpeedReconcileRate * dt);
            }
        }

        float effectiveMaxSpeed = EffectiveSpeed(horseData.maxSpeed);
        bool chargingNow = wantsCharge
            && Mathf.Abs(CurrentSpeed) >= horseData.chargeMinSpeedFraction * effectiveMaxSpeed;

        // Stamina: charging drains, everything else regens. Hitting empty locks charging until the
        // pool recovers past the hysteresis threshold, so it can't stutter on/off at zero.
        if (chargingNow)
        {
            Stamina = Mathf.Max(0f, Stamina - horseData.staminaDrainPerSecond * dt);
            if (Stamina <= 0f)
            {
                IsExhausted = true;
                chargingNow = false;
            }
        }
        else
        {
            Stamina = Mathf.Min(horseData.maxStamina, Stamina + horseData.staminaRegenPerSecond * dt);
            if (IsExhausted && Stamina >= horseData.maxStamina * horseData.exhaustedRecoveryFraction)
            {
                IsExhausted = false;
            }
        }

        if (chargingNow && !IsCharging)
        {
            IsCharging = true;
            if (horseAnimation != null)
            {
                horseAnimation.SetCharging(true);
            }
        }
        else if (!chargingNow && IsCharging)
        {
            StopCharging();
        }

        // The trample window is pure momentum — Shift only matters in that it buys more speed.
        bool tramplingNow = Mathf.Abs(CurrentSpeed) >= horseData.trampleMinSpeedFraction * effectiveMaxSpeed;
        if (tramplingNow && !IsTrampling)
        {
            IsTrampling = true;
            // The horse's own Health is the charge source: enemies hit back at the horse (not the
            // invulnerable rider), and Counterstrike-style retaliation has somewhere real to land.
            // The gallop-lean animation stays ours (driveChargeAnimation false) — a fast trot
            // tramples without looking like a charge.
            chargeDamage.BeginCharge(health, riderDamageable, horseData.chargeDamage, driveChargeAnimation: false);
        }
        else if (!tramplingNow && IsTrampling)
        {
            StopTrampling();
        }

        if (IsTrampling)
        {
            chargeDamage.SetSpeedFactor(TrampleDamageFactor(effectiveMaxSpeed));
        }
    }

    /// <summary>
    ///     Scans <see cref="HorseSO.crowdPushRadius" /> around a point ahead of the horse's chest.
    ///     Enemies inside are gently shouldered aside — a lateral <see cref="NavMeshAgent.Move" />
    ///     scaled by horse speed and proximity — and everyone in the front half arc contributes to
    ///     <see cref="crowdFactor" />, a soft drag on target speed (floored at
    ///     <see cref="HorseSO.crowdMinSpeedFraction" />) so a horde eases the horse off rather than
    ///     stopping it. Dead, ragdolling, and knocked-down enemies are left alone.
    /// </summary>
    private void UpdateCrowd(float dt)
    {
        crowdFactor = 1f;
        if (horseData.crowdPushRadius <= 0f || horseData.crowdLayers.value == 0)
        {
            return;
        }

        Vector3 center = transform.position + transform.forward * horseData.crowdForwardOffset;
        int count = Physics.OverlapSphereNonAlloc(center, horseData.crowdPushRadius, crowdBuffer, horseData.crowdLayers, QueryTriggerInteraction.Ignore);
        if (count == 0)
        {
            return;
        }

        float speedFraction = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, EffectiveSpeed(horseData.maxSpeed)));
        int frontCount = 0;
        crowdScratch.Clear();
        for (int i = 0; i < count; i++)
        {
            NavMeshAgent agent = crowdBuffer[i].GetComponentInParent<NavMeshAgent>();
            if (agent == null || !crowdScratch.Add(agent)) continue;
            if (!agent.enabled || !agent.isOnNavMesh) continue;

            Health enemyHealth = agent.GetComponent<Health>();
            if (enemyHealth != null && enemyHealth.IsDead) continue;

            KnockbackReceiver receiver = agent.GetComponent<KnockbackReceiver>();
            if (receiver != null && receiver.IsIncapacitated) continue;

            Vector3 toEnemy = agent.transform.position - transform.position;
            toEnemy.y = 0f;
            float ahead = Vector3.Dot(toEnemy, transform.forward);
            if (ahead > 0f)
            {
                frontCount++;
            }

            // A standing horse doesn't shove anyone; the nudge scales up with momentum.
            if (speedFraction <= 0.01f) continue;

            Vector3 lateral = toEnemy - transform.forward * ahead;
            if (lateral.sqrMagnitude < 0.01f)
            {
                // Dead ahead on our exact line: pick the side it's fractionally closer to.
                lateral = transform.right * (Vector3.Dot(toEnemy, transform.right) >= 0f ? 1f : -1f);
            }

            float falloff = 1f - Mathf.Clamp01(Vector3.Distance(agent.transform.position, center) / horseData.crowdPushRadius);
            agent.Move(lateral.normalized * (horseData.crowdPushSpeed * speedFraction * falloff * dt));
        }

        crowdFactor = Mathf.Max(horseData.crowdMinSpeedFraction, 1f - horseData.crowdDragPerEnemy * frontCount);
    }

    /// <summary>
    ///     Momentum → damage scale: <see cref="HorseSO.trampleMinDamageFraction" /> right at the
    ///     trample threshold, 1.0 at (stat-scaled) full charge speed.
    /// </summary>
    private float TrampleDamageFactor(float effectiveMaxSpeed)
    {
        float threshold = horseData.trampleMinSpeedFraction * effectiveMaxSpeed;
        float full = EffectiveSpeed(horseData.chargeSpeed);
        float t = Mathf.InverseLerp(threshold, Mathf.Max(threshold + 0.01f, full), Mathf.Abs(CurrentSpeed));
        return Mathf.Lerp(horseData.trampleMinDamageFraction, 1f, t);
    }

    /// <summary>
    ///     Every victim trampled bleeds speed: a base fraction plus a per-point term for the
    ///     victim's impulse resistance, so a goblin barely registers while a Troll (resistance 50)
    ///     stops the charge dead — the speed drop closes the trample window on the next Update.
    /// </summary>
    private void HandleTrampleHit(IDamageable victim, Vector3 hitPoint)
    {
        // Only the player-driven trample bleeds momentum; the knight's AI charge paces itself.
        if (!IsTrampling || inputReader == null) return;

        float resistance = 0f;
        if (victim is Component victimComponent)
        {
            KnockbackReceiver receiver = victimComponent.GetComponentInParent<KnockbackReceiver>();
            if (receiver != null)
            {
                resistance = receiver.CurrentResistance;
            }
        }

        float loss = Mathf.Clamp01(horseData.hitSpeedLossFraction + horseData.hitSpeedLossPerResistance * resistance);
        CurrentSpeed *= 1f - loss;
    }

    private void StopCharging()
    {
        if (!IsCharging) return;
        IsCharging = false;
        if (horseAnimation != null)
        {
            horseAnimation.SetCharging(false);
        }
    }

    private void StopTrampling()
    {
        if (!IsTrampling) return;
        IsTrampling = false;
        if (chargeDamage != null)
        {
            chargeDamage.EndCharge();
        }
    }
}
