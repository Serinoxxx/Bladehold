using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The "Solid" skill line: automatically negates one incoming damage source every
///     <see cref="StatType.BlockCooldown" /> seconds. Hooks <see cref="Health.TryBlockDamage" /> on the
///     player's own <see cref="Health" /> (the damage-negating sibling of <see cref="DeathNova" />'s
///     <c>TryPreventDeath</c> hook). The stat base is 0 = locked; the first "Solid" node sets the cooldown
///     to 10s and later tiers subtract from it.
/// </summary>
public class DamageBlocker : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional: played when a hit is blocked.")]
    [SerializeField] private MMF_Player blockFeedback;

    private float nextReadyTime;
    private bool anyError = false;

    /// <summary>True if blocking is unlocked and off cooldown right now.</summary>
    public bool IsAvailable => !anyError && stats.GetValue(StatType.BlockCooldown) > 0f && Time.time >= nextReadyTime;

    /// <summary>Seconds left before the next block is available, or 0 if it's ready (or locked).</summary>
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);

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
            Debug.LogError("DamageBlocker could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.BlockCooldown, 0f);
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
        if (!IsAvailable)
        {
            return false;
        }

        nextReadyTime = Time.time + stats.GetValue(StatType.BlockCooldown);
        if (blockFeedback != null)
        {
            blockFeedback.PlayFeedbacks();
        }
        return true;
    }
}
