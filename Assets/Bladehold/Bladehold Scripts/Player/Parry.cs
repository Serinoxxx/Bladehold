using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The "Parry" skill line: a chance to auto-block an incoming melee hit, but only while facing
///     the attacker — unlike the omnidirectional "Solid" auto-block (<see cref="DamageBlocker" />,
///     which sits on a cooldown instead), Parry rolls independently on every melee hit. Hooks
///     <see cref="Health.TryBlockDamage" /> on the player's own <see cref="Health" />.
///     <see cref="DamageType.elemental" /> hits (e.g. Storm Witch lightning) are never parryable —
///     only "melee" (sharp/blunt) hits, which is also what carries a usable
///     <see cref="Damage.sourcePosition" /> to check facing against. <see cref="Damage.unparryable" />
///     hits (e.g. the Troll's ground slam — a wide AoE with no single swing to read) are excluded
///     too, regardless of type or facing. Raises <see cref="OnParried" /> for reactive effects
///     (e.g. <see cref="Counterstrike" />) rather than knowing about them itself, same as every
///     other Health reaction in this codebase.
/// </summary>
public class Parry : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional: played when a hit is parried.")]
    [SerializeField] private MMF_Player parryFeedback;
    [Tooltip("Minimum dot product between facing and the direction to the attacker required to parry. 0 = anywhere in the front hemisphere, 1 = dead-on only.")]
    [SerializeField] private float facingDotThreshold = 0.3f;

    /// <summary>Raised when a hit is successfully parried, carrying the blocked Damage (its <see cref="Damage.source" /> is the attacker, if known).</summary>
    public event Action<Damage> OnParried;

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("Parry could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.ParryChance, 0f);
        health.TryBlockDamage += HandleIncomingDamage;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.TryBlockDamage -= HandleIncomingDamage;
        }
    }

    private bool HandleIncomingDamage(Damage damage)
    {
        if (damage.type == DamageType.elemental || damage.unparryable)
        {
            return false;
        }

        float chance = stats.GetValue(StatType.ParryChance);
        if (chance <= 0f)
        {
            return false;
        }

        if (!IsFacingAttacker(damage))
        {
            return false;
        }

        if (UnityEngine.Random.value >= chance)
        {
            return false;
        }

        if (parryFeedback != null)
        {
            parryFeedback.PlayFeedbacks();
        }
        OnParried?.Invoke(damage);
        return true;
    }

    private bool IsFacingAttacker(Damage damage)
    {
        Vector3 toAttacker = damage.sourcePosition - transform.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.0001f)
        {
            // No usable attacker position — can't confirm facing, so it can't be parried.
            return false;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Dot(forward.normalized, toAttacker.normalized) >= facingDotThreshold;
    }
}
