using System.Collections;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     The horse's locomotion authority while the PLAYER rides: W/S accelerates/brakes/reverses,
///     A/D turns, and holding Sprint (Shift) at speed charges — opening the shared
///     <see cref="HorseChargeDamage" /> trample window. Moves via its own
///     <see cref="CharacterController" /> (the knight's AI mode uses the NavMeshAgent instead; a
///     riderless horse has no driver at all). Disabled by default — <c>PlayerMount</c> enables it
///     through <see cref="SetRider" /> and reverses everything with <see cref="ClearRider" />.
///     All tunables live on <see cref="HorseSO" />; speeds scale with the
///     <see cref="StatType.HorseSpeedMultiplier" /> stat (the "Thoroughbred" line).
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

    /// <summary>True while Shift is held AND the horse is at charging speed — the trample window is open.</summary>
    public bool IsCharging { get; private set; }

    /// <summary>True while the rear pose holds the horse in place (see <see cref="TriggerRear" />).</summary>
    public bool IsRearing { get; private set; }

    /// <summary>The horse's own Health, for the mount's damage forwarding and death handling.</summary>
    public Health Health => health;

    /// <summary>The saddle transform the mounted player snaps to.</summary>
    public Transform RiderSeat => riderSeat;

    /// <summary>Local-space landing offset for a dismounting player (from <see cref="HorseSO" />).</summary>
    public Vector3 DismountLocalOffset => horseData != null ? horseData.dismountLocalOffset : new Vector3(1.4f, 0f, 0f);

    private InputReader inputReader;
    private IDamageable riderDamageable;
    private PlayerStats stats;
    private bool sprintHeld;
    private float verticalVelocity;
    private Coroutine rearRoutine;
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

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (health != null)
        {
            health.OnDied -= HandleDied;
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

        characterController.enabled = true;
        enabled = true;
    }

    /// <summary>Reverses <see cref="SetRider" />: stops any charge, drops the reins, and disables the motor.</summary>
    public void ClearRider()
    {
        Unsubscribe();
        StopCharging();
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

    /// <summary>Plays the rear and holds the horse in place for <see cref="HorseSO.rearSeconds" /> (mount flavor).</summary>
    public void TriggerRear()
    {
        if (anyError || health.IsDead || IsRearing) return;

        if (horseAnimation != null)
        {
            horseAnimation.TriggerRear();
        }
        if (rearRoutine != null)
        {
            StopCoroutine(rearRoutine);
        }
        rearRoutine = StartCoroutine(RearRoutine());
    }

    private IEnumerator RearRoutine()
    {
        IsRearing = true;
        yield return new WaitForSeconds(horseData.rearSeconds);
        IsRearing = false;
        rearRoutine = null;
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
        // the target speed up, so a standing start builds into the charge naturally.
        bool wantsCharge = sprintHeld && move.y > 0f;
        float effectiveMax = EffectiveSpeed(wantsCharge ? horseData.chargeSpeed : horseData.maxSpeed);

        float targetSpeed;
        if (IsRearing)
        {
            targetSpeed = 0f;
        }
        else if (move.y > 0f)
        {
            targetSpeed = effectiveMax * move.y;
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
        TurnInput = IsRearing ? 0f : Mathf.Clamp(move.x, -1f, 1f);
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
        characterController.Move(motion * dt);

        bool chargingNow = wantsCharge
            && Mathf.Abs(CurrentSpeed) >= horseData.chargeMinSpeedFraction * EffectiveSpeed(horseData.maxSpeed);
        if (chargingNow && !IsCharging)
        {
            IsCharging = true;
            // The horse's own Health is the charge source: enemies hit back at the horse (not the
            // invulnerable rider), and Counterstrike-style retaliation has somewhere real to land.
            chargeDamage.BeginCharge(health, riderDamageable, horseData.chargeDamage);
        }
        else if (!chargingNow && IsCharging)
        {
            StopCharging();
        }
    }

    private void StopCharging()
    {
        if (!IsCharging) return;
        IsCharging = false;
        if (chargeDamage != null)
        {
            chargeDamage.EndCharge();
        }
    }
}
