using MoreMountains.Feedbacks;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The horse's trample hitbox, shared by the mounted knight's AI charge and the player's
///     Shift-charge. While a charge is active it overlap-boxes the area ahead of the horse each
///     physics step and damages every unique <see cref="IDamageable" /> it finds, stamping
///     <see cref="Damage.impulsePower" />/<see cref="Damage.impulseForce" /> exactly like
///     <see cref="TrollSlamAttack.ApplySlamDamage" /> so victims with an <see cref="ImpulseReceiver" />
///     are ragdoll-flung. Never hits the horse itself, the charge's <c>source</c>, or the rider.
///
///     Deliberately not a <see cref="DamageTrigger" /> — its non-player branch can't stamp impulse,
///     and the trample needs a per-target re-hit cooldown rather than a per-activation cap. Whoever
///     drives the charge (<c>MountedKnightBrain</c> or <see cref="HorseMotor" />) brackets it with
///     <see cref="BeginCharge" />/<see cref="EndCharge" />.
/// </summary>
public class HorseChargeDamage : MonoBehaviour
{
    [SerializeField] private HorseSO horseData;
    [SerializeField] private Health health;
    [Tooltip("Optional: the horse's animator bridge; the trample drives its Charge bool so the gallop-lean state tracks the actual damage window.")]
    [SerializeField] private HorseAnimation horseAnimation;
    [Tooltip("Layers the trample can hit.")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private MMF_Player hitFeedback;

    /// <summary>Fired once per trample hit with the victim and the (approximate) hit point.</summary>
    public event Action<IDamageable, Vector3> OnHit;

    /// <summary>True while a charge is active (between BeginCharge and EndCharge).</summary>
    public bool IsCharging { get; private set; }

    private const int MaxOverlapResults = 64;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly Dictionary<IDamageable, float> nextAllowedHitTime = new Dictionary<IDamageable, float>();
    private static readonly List<IDamageable> pruneScratch = new List<IDamageable>();

    private IDamageable ownerDamageable;
    private IDamageable source;
    private IDamageable rider;
    private float damagePerHit;
    private float speedFactor = 1f;
    private bool drivingChargeAnimation;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
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
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // The trample never damages the horse itself (the DamageTrigger owner idiom).
        ownerDamageable = GetComponentInParent<IDamageable>();

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        EndCharge();
        enabled = false;
    }

    /// <summary>
    ///     Opens the trample window. <paramref name="chargeSource" /> is stamped as
    ///     <see cref="Damage.source" /> and excluded from hits (the knight's Health for the AI charge,
    ///     the horse's own Health for the player's); <paramref name="riderDamageable" /> is an extra
    ///     exclusion for the seated rider (null when the driver IS the source, e.g. the knight).
    ///     <paramref name="damage" /> is the per-hit value (roster-scaled for the knight,
    ///     <see cref="HorseSO.chargeDamage" /> for the player). Pass
    ///     <paramref name="driveChargeAnimation" /> false when the caller owns the gallop-lean state
    ///     itself — the player's trample window opens at plain running speed, where the charge lean
    ///     would look wrong (<see cref="HorseMotor" /> drives it from the actual Shift-charge instead).
    /// </summary>
    public void BeginCharge(IDamageable chargeSource, IDamageable riderDamageable, float damage, bool driveChargeAnimation = true)
    {
        if (anyError || (health != null && health.IsDead))
        {
            return;
        }

        source = chargeSource;
        rider = riderDamageable;
        damagePerHit = damage;
        speedFactor = 1f;
        drivingChargeAnimation = driveChargeAnimation;
        IsCharging = true;

        if (horseAnimation != null && drivingChargeAnimation)
        {
            horseAnimation.SetCharging(true);
        }
    }

    /// <summary>
    ///     Scales the next hits' damage and impulse by the horse's current momentum (1 = the full
    ///     BeginCharge values). <see cref="HorseMotor" /> updates this every frame while its trample
    ///     window is open; the knight's fixed-speed AI charge never calls it and stays at 1.
    /// </summary>
    public void SetSpeedFactor(float factor)
    {
        speedFactor = Mathf.Max(0f, factor);
    }

    /// <summary>Closes the trample window and prunes expired re-hit cooldown entries.</summary>
    public void EndCharge()
    {
        if (!IsCharging)
        {
            return;
        }

        IsCharging = false;

        if (horseAnimation != null && drivingChargeAnimation)
        {
            horseAnimation.SetCharging(false);
        }

        // Prune stale cooldown entries so a long run's dead targets don't accumulate forever.
        pruneScratch.Clear();
        foreach (KeyValuePair<IDamageable, float> entry in nextAllowedHitTime)
        {
            if (Time.time >= entry.Value)
            {
                pruneScratch.Add(entry.Key);
            }
        }
        foreach (IDamageable expired in pruneScratch)
        {
            nextAllowedHitTime.Remove(expired);
        }
    }

    private void FixedUpdate()
    {
        if (anyError || !IsCharging) return;

        Vector3 center = transform.position
            + transform.forward * horseData.hitBoxForwardOffset
            + Vector3.up * horseData.hitBoxHalfExtents.y;

        int count = Physics.OverlapBoxNonAlloc(center, horseData.hitBoxHalfExtents, overlapBuffer, transform.rotation, hitLayers);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;
            if (damageable == source) continue;
            if (rider != null && damageable == rider) continue;
            if (nextAllowedHitTime.TryGetValue(damageable, out float nextAllowed) && Time.time < nextAllowed) continue;

            nextAllowedHitTime[damageable] = Time.time + horseData.hitCooldownSeconds;

            bool isPlayer = (rider != null && Player.Instance != null && (ReferenceEquals(rider, Player.Instance.Damageable) || ReferenceEquals(rider, Player.Instance.Health)))
                || (source != null && Player.Instance != null && (ReferenceEquals(source, Player.Instance.Damageable) || ReferenceEquals(source, Player.Instance.Health)));

            damageable.ReceiveDamage(new Damage
            {
                value = damagePerHit * speedFactor,
                type = horseData.damageType,
                sourcePosition = transform.position,
                knockbackForce = horseData.knockbackForce * speedFactor,
                source = source,
                unparryable = true,
                isPlayerDamage = isPlayer,
            });

            if (hitFeedback != null)
            {
                hitFeedback.PlayFeedbacks();
            }

            Vector3 hitPoint = (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider || (collider is MeshCollider mc && mc.convex))
                ? collider.ClosestPoint(center)
                : collider.bounds.ClosestPoint(center);
            OnHit?.Invoke(damageable, hitPoint);
        }
    }
}
