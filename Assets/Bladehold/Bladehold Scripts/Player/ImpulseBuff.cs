using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The player's Impulse buff: a timed power-up granted by picking up Impulse Orbs
///     (<see cref="ImpulseOrb" />, dropped by Impulse Goblins). While active, the sword's
///     <see cref="DamageTrigger" /> stamps <see cref="Damage.impulsePower" /> /
///     <see cref="Damage.impulseForce" /> onto every hit (flinging enemies whose resistance it beats —
///     see <see cref="ImpulseReceiver" />) and multiplies damage by the stack bonus.
///
///     Each orb adds <see cref="StatType.ImpulseOrbDuration" /> seconds to the remaining time and one
///     stack; every stack beyond the first adds extra force and damage (<see cref="ImpulseSO" />).
///     All stacks expire together when the timer runs out. <see cref="PlayerStats" /> has no timed
///     modifiers, so the buff owns its own countdown (the <see cref="DeathNova" /> cooldown idiom);
///     scene reload resets it naturally. Unlocking is the <see cref="StatType.ImpulseOrbDuration" />
///     stat itself (base 0 = orbs grant nothing), per the "expose upgradeable numbers as stats"
///     convention.
/// </summary>
public class ImpulseBuff : MonoBehaviour
{
    [SerializeField] private ImpulseSO config;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;

    [Header("Buff feedback (cosmetic, optional)")]
    [Tooltip("Played when the buff activates (first orb of a run of stacks).")]
    [SerializeField] private MMF_Player activationFeedback;
    [Tooltip("Played when the buff expires.")]
    [SerializeField] private MMF_Player deactivationFeedback;
    [Tooltip("Looping aura child object, shown while the buff is active.")]
    [SerializeField] private GameObject auraVisual;

    /// <summary>Raised whenever the buff's remaining time or stack count changes (pickup, expiry).</summary>
    public event Action OnChanged;

    public float RemainingSeconds { get; private set; }
    public int StackCount { get; private set; }

    private bool anyError = false;

    public bool IsActive => !anyError && RemainingSeconds > 0f;

    /// <summary>Sword-damage multiplier from stacked orbs (1 while inactive or single-stacked).</summary>
    public float DamageMultiplier =>
        IsActive ? 1f + (StackCount - 1) * config.damagePerExtraStackPercent : 1f;

    /// <summary>The resistance-piercing rating stamped on hits (before the charge bonus).</summary>
    public float CurrentImpulsePower => anyError ? 0f : stats.GetValue(StatType.ImpulsePower);

    /// <summary>Extra Impulse Power per attack charge level (see <see cref="ImpulseSO.powerPerChargeLevel" />).</summary>
    public float PowerPerChargeLevel => anyError ? 0f : config.powerPerChargeLevel;

    /// <summary>Launch speed (m/s) stamped on hits, folding in the power and stack bonuses (charge is applied by <see cref="DamageTrigger" />).</summary>
    public float CurrentImpulseForce => anyError
        ? 0f
        : config.baseImpulseForce
          * (1f + CurrentImpulsePower * config.forcePerPower)
          * (1f + (StackCount - 1) * config.forcePerExtraStackPercent);

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("ImpulseSO is not assigned in the inspector.");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("ImpulseBuff could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.ImpulseOrbDuration, config.baseOrbDurationSeconds);
        stats.SetBase(StatType.ImpulsePower, config.basePower);

        if (auraVisual != null)
        {
            auraVisual.SetActive(false);
        }
    }

    private void Update()
    {
        if (RemainingSeconds <= 0f)
        {
            return;
        }

        RemainingSeconds -= Time.deltaTime;
        if (RemainingSeconds <= 0f)
        {
            Deactivate();
        }
    }

    /// <summary>
    ///     Grants one orb's worth of buff time and a stack. Returns the seconds granted (0 while the
    ///     feature is still locked, i.e. no skill node has raised <see cref="StatType.ImpulseOrbDuration" />).
    /// </summary>
    public float CollectOrb()
    {
        if (anyError)
        {
            return 0f;
        }

        float granted = stats.GetValue(StatType.ImpulseOrbDuration);
        if (granted <= 0f)
        {
            return 0f;
        }

        bool wasActive = IsActive;
        RemainingSeconds += granted;
        StackCount++;

        if (!wasActive)
        {
            if (activationFeedback != null)
            {
                activationFeedback.PlayFeedbacks();
            }
            if (auraVisual != null)
            {
                auraVisual.SetActive(true);
            }
        }

        OnChanged?.Invoke();
        return granted;
    }

    private void Deactivate()
    {
        RemainingSeconds = 0f;
        StackCount = 0;

        if (deactivationFeedback != null)
        {
            deactivationFeedback.PlayFeedbacks();
        }
        if (auraVisual != null)
        {
            auraVisual.SetActive(false);
        }

        OnChanged?.Invoke();
    }
}
