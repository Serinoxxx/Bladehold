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
///     Charging is in discrete <b>levels</b>: each level takes another <see cref="chargeTimePerLevel" />
///     seconds of holding (level 1 at 1×, level 2 at 2×, …) up to <see cref="StatType.MaxChargeLevels" />.
///     <see cref="AttackDamageMultiplier" /> is <c>1 + ChargeLevel × ChargeDamageBonus</c> and is updated
///     live, so whenever the sword's <see cref="DamageTrigger" /> hitbox activates (via the attack clip's
///     animation event) it reads the multiplier for the current hold — correct whether the strike lands on
///     press or on release.
///
///     Both stats start at 0, so by default there is no hold-to-charge at all: the "Heavy Strike" skill-tree
///     node unlocks level 1 (raising <see cref="StatType.MaxChargeLevels" /> and
///     <see cref="StatType.ChargeDamageBonus" />), and further tiers add more levels.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("Synty InputReader that raises the attack press/release events. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerStats stats;

    [Tooltip("Seconds of holding the attack button to gain each charge level (level 1 at 1×, level 2 at 2×, ...).")]
    [SerializeField] private float chargeTimePerLevel = 1f;

    private bool charging;
    private float chargeStartTime;
    private bool subscribed;
    private bool anyError = false;

    /// <summary>Charge level of the swing in progress (or the last one), 0..MaxChargeLevels. Useful for VFX/feedback.</summary>
    public int ChargeLevel { get; private set; }

    /// <summary>Levels the current hold can reach; 0 means hold-to-charge is still locked.</summary>
    public int MaxChargeLevels => anyError ? 0 : Mathf.RoundToInt(stats.GetValue(StatType.MaxChargeLevels));

    /// <summary>True while the attack button is held and the swing is charging up.</summary>
    public bool IsCharging => charging;

    /// <summary>
    ///     Damage multiplier for the current swing: <c>1 + ChargeLevel × ChargeDamageBonus</c>. The sword's
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

        // Both start at 0 (= no hold-to-charge); the "Heavy Strike" upgrades raise them.
        stats.SetBase(StatType.ChargeDamageBonus, 0f);
        stats.SetBase(StatType.MaxChargeLevels, 0f);

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

        ChargeLevel = 0;
        AttackDamageMultiplier = 1f;

        // Until a skill-tree node grants a charge level, a press is just an ordinary swing.
        if (MaxChargeLevels <= 0) return;

        charging = true;
        chargeStartTime = Time.time;
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
        int maxLevels = MaxChargeLevels;
        int level = chargeTimePerLevel > 0f
            ? Mathf.FloorToInt((Time.time - chargeStartTime) / chargeTimePerLevel)
            : maxLevels;
        ChargeLevel = Mathf.Clamp(level, 0, maxLevels);
        AttackDamageMultiplier = 1f + ChargeLevel * stats.GetValue(StatType.ChargeDamageBonus);
    }
}
