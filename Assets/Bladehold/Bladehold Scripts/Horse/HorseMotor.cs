using System.Collections.Generic;
using MoreMountains.Feedbacks;
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
///     The shared <see cref="HorseChargeDamage" /> trample window is speed-gated: it opens whenever
///     momentum passes <see cref="HorseSO.trampleMinSpeedFraction" /> of max speed. How hard it hits
///     depends on the charge: riding without charging ("cruise") applies the Cruise fractions on
///     <see cref="HorseSO" /> (a light shove that bogs down in a crowd), while a Shift-charge hits at
///     full strength with the charging speed-loss / crowd-drag values (tuned to plough straight
///     through). Charging drains stamina; stamina comes only from carrots and on-foot kills (see
///     <see cref="AddStamina" />, fed by <c>PlayerMount</c>) — no passive regen by default — so
///     the charge is a burst you build up rather than a cooldown. An emptied pool locks charging
///     until it recovers past <see cref="HorseSO.exhaustedRecoveryFraction" />.
///
///     Enemies never physically block the horse: <see cref="SetRider" /> excludes
///     <see cref="HorseSO.crowdLayers" /> from the CharacterController, and each frame the moving
///     horse laterally nudges overlapping enemies' NavMeshAgents aside (the
///     <see cref="KnockbackReceiver" /> agent.Move idiom) while enemies in the front arc apply a
///     soft drag on target speed instead of a hard stop. The exception is a
///     <see cref="MountStopper" /> enemy (the Bulwark): one squarely ahead is a wall — the horse
///     rears in place and can't push forward into it. If level geometry does block the
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
    [Tooltip("Optional: played when the horse runs into a MountStopper enemy and rears (impact thud, whinny, screenshake).")]
    [SerializeField] private MMF_Player mountStopFeedback;

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

    /// <summary>True while the horse is locked in a rear after running into a <see cref="MountStopper" />.</summary>
    public bool IsRearing => Time.time < rearLockUntil;

    /// <summary>Current stamina, drained by charging (see <see cref="HorseSO.maxStamina" />).</summary>
    public float Stamina { get; private set; }

    /// <summary>0..1 stamina, for UI.</summary>
    public float NormalizedStamina => horseData != null && horseData.maxStamina > 0f ? Stamina / horseData.maxStamina : 1f;

    /// <summary>True while an emptied stamina pool locks charging (clears at the recovery threshold).</summary>
    public bool IsExhausted { get; private set; }

    /// <summary>Size of the stamina pool (from <see cref="HorseSO.maxStamina" />).</summary>
    public float MaxStamina => horseData != null ? horseData.maxStamina : 100f;

    /// <summary>The horse's tuning (instanced per mount definition), for systems that scale with it.</summary>
    public HorseSO Data => horseData;

    /// <summary>Raised when the player's trample kills an enemy (alive before the hit, dead after).</summary>
    public event System.Action<IDamageable> OnTrampleKill;

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
    private MountStopper stopperAhead;
    private float rearLockUntil;
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
        if (health != null)
        {
            health.ImmuneToPlayerDamage = true;
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

    /// <summary>
    ///     Applies the custom stats, materials, and scale from a MountDefinitionSO.
    /// </summary>
    public void ApplyMountDefinition(MountDefinitionSO mountDef)
    {
        if (mountDef == null) return;

        if (horseData != null)
        {
            horseData = Instantiate(horseData);
            horseData.maxSpeed = mountDef.maxSpeed;
            horseData.chargeSpeed = mountDef.chargeSpeed;
            horseData.chargeDamage = mountDef.chargeDamage;
            horseData.knockbackForce = mountDef.knockbackForce;

            if (chargeDamage != null)
            {
                chargeDamage.SetHorseData(horseData);
            }
        }

        if (mountDef.scaleMultiplier > 0f)
        {
            transform.localScale = Vector3.one * mountDef.scaleMultiplier;
        }

        if (mountDef.material != null)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = mountDef.material;
                }
                r.materials = mats;
            }
        }
    }

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (health != null)
        {
            health.ImmuneToPlayerDamage = true;
        }

        // Initialised here, not in Start: PlayerMount restores the banked stamina right after
        // Instantiate, and Start would run afterwards and refill it.
        if (horseData != null)
        {
            Stamina = horseData.maxStamina;
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

        health.OnDied += HandleDied;
        health.ScaleDamageTaken += HandleScaleDamageTaken;
        chargeDamage.OnHit += HandleTrampleHit;
        chargeDamage.OnKill += HandleTrampleKill;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.ScaleDamageTaken -= HandleScaleDamageTaken;
        }
        if (chargeDamage != null)
        {
            chargeDamage.OnHit -= HandleTrampleHit;
            chargeDamage.OnKill -= HandleTrampleKill;
        }
    }

    /// <summary>Sets stamina to a 0..1 fraction of the pool (PlayerMount restores the banked value on mount).</summary>
    public void SetNormalizedStamina(float fraction)
    {
        Stamina = Mathf.Clamp01(fraction) * MaxStamina;
        IsExhausted = Stamina <= 0f;
    }

    /// <summary>Adds raw stamina (kills, cards); clears exhaustion once past the recovery threshold.</summary>
    public void AddStamina(float amount)
    {
        if (amount <= 0f || horseData == null) return;
        Stamina = Mathf.Min(horseData.maxStamina, Stamina + amount);
        if (IsExhausted && Stamina >= horseData.maxStamina * horseData.exhaustedRecoveryFraction)
        {
            IsExhausted = false;
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

        // Zero min-move: the default 0.001 m threshold silently swallows the tiny first steps of a
        // standing start at high framerates (or low timeScale) — the controller doesn't move at all,
        // which used to read as "blocked" below and pin the horse at 0 until a long frame broke free.
        characterController.minMoveDistance = 0f;

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
        stopperAhead = null;
        rearLockUntil = 0f;
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

        UpdateCrowd(dt, wantsCharge);

        // A MountStopper dead ahead is a wall: arriving with momentum rears the horse in place,
        // and either way the horse can't push forward into it — steer round or back off.
        if (stopperAhead != null && CurrentSpeed > 0f)
        {
            if (!IsRearing && CurrentSpeed >= horseData.mountStopMinRearSpeed)
            {
                TakeStopperImpact(stopperAhead);
                Rear(stopperAhead.RearLockSeconds);
            }
            CurrentSpeed = 0f;
        }
        bool rearing = IsRearing;
        if (rearing)
        {
            wantsCharge = false;
        }

        float targetSpeed;
        if (rearing || (stopperAhead != null && move.y > 0f))
        {
            targetSpeed = 0f;
        }
        else if (move.y > 0f)
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
        TurnInput = rearing ? 0f : Mathf.Clamp(move.x, -1f, 1f);
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
        CollisionFlags collisionFlags = characterController.Move(motion * dt);

        // A controller wedged on level geometry doesn't move, but CurrentSpeed would keep
        // integrating toward target — then burst out at full speed the moment the obstruction
        // clears. Pull it down toward the forward speed actually achieved instead — but only on a
        // real side collision: a short move for any other reason (min-move threshold, a tiny
        // frame) must never be mistaken for a wall, or it cancels acceleration from a standstill.
        if (dt > 0.0001f && (collisionFlags & CollisionFlags.Sides) != 0)
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

        // Stamina: charging drains; otherwise only the (default 0) passive regen applies. Hitting empty locks
        // charging until the pool recovers past the hysteresis threshold, so it can't stutter on/off at zero.
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
            // Charging hits at full strength; cruising is a light shove (HorseSO Cruise fractions).
            float momentum = TrampleDamageFactor(effectiveMaxSpeed);
            float damageMultiplier = stats != null ? Mathf.Max(0f, stats.GetValue(StatType.HorseTrampleDamageMultiplier)) : 1f;
            float damageFactor = momentum * damageMultiplier * (IsCharging ? 1f : horseData.cruiseDamageFraction);
            float knockbackFactor = momentum * (IsCharging ? 1f : horseData.cruiseKnockbackFraction);
            chargeDamage.SetFactors(damageFactor, knockbackFactor);
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
    private void UpdateCrowd(float dt, bool charging)
    {
        crowdFactor = 1f;
        stopperAhead = null;
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

            // Mount stoppers (the Bulwark's shield wall) are never shouldered aside: one squarely
            // in the horse's path stops it outright (see Update).
            if (agent.TryGetComponent(out MountStopper stopper) && stopper.Halts(transform, health))
            {
                float sideways = Mathf.Abs(Vector3.Dot(toEnemy, transform.right));
                if (stopperAhead == null && ahead > 0f && ahead <= horseData.mountStopReach && sideways <= horseData.mountStopHalfWidth)
                {
                    stopperAhead = stopper;
                }
                continue;
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

        float dragPerEnemy = charging ? horseData.crowdDragPerEnemy : horseData.cruiseCrowdDragPerEnemy;
        float minFraction = charging ? horseData.crowdMinSpeedFraction : horseData.cruiseCrowdMinSpeedFraction;
        crowdFactor = Mathf.Max(minFraction, 1f - dragPerEnemy * frontCount);
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

        float baseLoss = IsCharging ? horseData.hitSpeedLossFraction : horseData.cruiseHitSpeedLossFraction;
        float perResistance = IsCharging ? horseData.hitSpeedLossPerResistance : horseData.cruiseHitSpeedLossPerResistance;
        float loss = Mathf.Clamp01(baseLoss + perResistance * resistance);
        CurrentSpeed *= 1f - loss;
    }

    /// <summary>Bloodlust: trample kills refund stamina (HorseTrampleKillStamina) and are re-raised for listeners.</summary>
    private void HandleTrampleKill(IDamageable victim)
    {
        if (inputReader == null) return;

        if (stats != null)
        {
            AddStamina(stats.GetValue(StatType.HorseTrampleKillStamina));
        }
        OnTrampleKill?.Invoke(victim);
    }

    /// <summary>Iron Barding: a charging, player-ridden horse takes HorseChargeDamageReduction less damage.</summary>
    private float HandleScaleDamageTaken(Damage damage)
    {
        if (!IsCharging || stats == null) return 1f;
        return 1f - Mathf.Clamp(stats.GetValue(StatType.HorseChargeDamageReduction), 0f, 0.9f);
    }

    /// <summary>
    ///     A braced <see cref="MountStopper" /> (the Spearman's spears) hurts the horse that runs into
    ///     it, harder the faster it was going. Called before <see cref="Rear" /> stops the charge, so
    ///     Iron Barding's charging damage reduction still softens the blow.
    /// </summary>
    private void TakeStopperImpact(MountStopper stopper)
    {
        float damage = stopper.HorseImpactDamage(NormalizedSpeed);
        if (damage <= 0f) return;

        health.ReceiveDamage(new Damage
        {
            value = damage,
            type = DamageType.sharp,
            source = stopper.Health,
            sourcePosition = stopper.transform.position,
            direction = -transform.forward,
            unparryable = true,
        });
    }

    /// <summary>
    ///     The horse ran into a <see cref="MountStopper" />: kill the charge and trample, play the
    ///     rear, and lock movement and steering for <paramref name="lockSeconds" />.
    /// </summary>
    private void Rear(float lockSeconds)
    {
        rearLockUntil = Time.time + lockSeconds;
        StopCharging();
        StopTrampling();

        if (horseAnimation != null)
        {
            horseAnimation.TriggerRear();
        }
        if (mountStopFeedback != null)
        {
            mountStopFeedback.PlayFeedbacks();
        }
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
