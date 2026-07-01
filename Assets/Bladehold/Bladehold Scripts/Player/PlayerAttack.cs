using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Implements the charged-attack mechanic on top of the vendored
///     <see cref="SamplePlayerAnimationController" />, which already owns attack input and animation: its
///     <see cref="InputReader" /> raises <c>onAttackActivated</c> on press and <c>onAttackDeactivated</c> on
///     release, and the controller drives the <c>StartAttack</c>/<c>IsHoldingAttack</c> animator params. This
///     component does <b>not</b> read input directly or trigger any animation — it only times the hold and
///     exposes the resulting damage multiplier.
///
///     While the button is held the charge ramps 0 → 1 over <see cref="fullChargeTime" /> and
///     <see cref="AttackDamageMultiplier" /> is updated live, so whenever the sword's
///     <see cref="DamageTrigger" /> hitbox activates (via the attack clip's animation event) it reads the
///     multiplier for the current hold — correct whether the strike lands on press or on release.
///
///     The charge cap is the <see cref="StatType.ChargeDamageBonus" /> stat (0 until the "Heavy Strike"
///     upgrade is bought, then e.g. 0.5 for +50% at full charge). With a bonus of 0 a quick tap behaves
///     like an ordinary swing.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("Synty InputReader that raises the attack press/release events. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerStats stats;

    [Tooltip("Seconds of holding the attack button to reach full charge.")]
    [SerializeField] private float fullChargeTime = 1f;

    private bool charging;
    private float chargeStartTime;
    private bool subscribed;
    private bool anyError = false;

    /// <summary>Charge of the swing in progress (or the last one), 0..1. Useful for VFX/feedback.</summary>
    public float LastChargeFraction { get; private set; }

    /// <summary>True while the attack button is held and the swing is charging up.</summary>
    public bool IsCharging => charging;

    /// <summary>
    ///     Damage multiplier for the current swing: <c>1 + chargeFraction × ChargeDamageBonus</c>. The sword's
    ///     <see cref="DamageTrigger" /> multiplies its damage by this when its hitbox activates.
    /// </summary>
    public float AttackDamageMultiplier { get; private set; } = 1f;

    private void OnValidate()
    {
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; charged attack can't time the hold.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("PlayerStats component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // The charge cap starts at 0; the "Heavy Strike" upgrade raises it (e.g. to 0.5).
        stats.SetBase(StatType.ChargeDamageBonus, 0f);

        Subscribe();
    }

    private void OnEnable()
    {
        // Re-subscribe if this component is toggled (e.g. re-enabled after a non-death disable).
        if (!anyError && inputReader != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        charging = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated += HandlePressed;
        inputReader.onAttackDeactivated += HandleReleased;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated -= HandlePressed;
        inputReader.onAttackDeactivated -= HandleReleased;
        subscribed = false;
    }

    private void Update()
    {
        if (anyError || !charging)
        {
            return;
        }

        // Keep the multiplier live as the hold grows, so the hitbox reads the right value at any frame.
        RecomputeMultiplier();
    }

    private void HandlePressed()
    {
        if (anyError) return;

        charging = true;
        chargeStartTime = Time.time;
        LastChargeFraction = 0f;
        AttackDamageMultiplier = 1f;
    }

    private void HandleReleased()
    {
        if (anyError || !charging) return;

        // Latch the final value for the strike that plays on release.
        RecomputeMultiplier();
        charging = false;
    }

    private void RecomputeMultiplier()
    {
        float fraction = fullChargeTime > 0f
            ? Mathf.Clamp01((Time.time - chargeStartTime) / fullChargeTime)
            : 1f;
        LastChargeFraction = fraction;
        AttackDamageMultiplier = 1f + fraction * stats.GetValue(StatType.ChargeDamageBonus);
    }
}
