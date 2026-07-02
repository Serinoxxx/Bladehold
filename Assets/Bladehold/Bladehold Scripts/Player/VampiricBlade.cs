using UnityEngine;

/// <summary>
///     The "Vampiric Blade" skill line: heals the player for <see cref="StatType.LifeStealPercent" /> of
///     every point of sword damage dealt. Follows the <see cref="SwordHitFeedback" /> pattern — a separate
///     listener on the sword's <see cref="DamageTrigger.OnHit" />, so the trigger stays unaware of it.
///     Registers the stat base at 0 (locked) per convention; the skill-tree nodes raise it.
/// </summary>
public class VampiricBlade : MonoBehaviour
{
    [Tooltip("The sword's DamageTrigger whose hits heal the player. Assign explicitly — the player has other DamageTriggers (e.g. the Death Nova hitbox).")]
    [SerializeField] private DamageTrigger swordTrigger;
    [Tooltip("Optional; defaults to Player.Instance.Health.")]
    [SerializeField] private Health health;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;

    private bool anyError = false;

    private void Start()
    {
        if (health == null)
        {
            health = Player.Instance != null ? Player.Instance.Health : null;
        }
        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }

        if (swordTrigger == null)
        {
            Debug.LogError("VampiricBlade 'swordTrigger' (the sword's DamageTrigger) is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("VampiricBlade could not find Health (set it or ensure Player.Instance.Health exists).");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("VampiricBlade could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.LifeStealPercent, 0f);
        swordTrigger.OnHit += HandleHit;
    }

    private void OnDestroy()
    {
        if (swordTrigger != null)
        {
            swordTrigger.OnHit -= HandleHit;
        }
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        if (anyError)
        {
            return;
        }

        float fraction = stats.GetValue(StatType.LifeStealPercent);
        if (fraction <= 0f)
        {
            return;
        }

        health.Heal(damage.value * fraction);
    }
}
