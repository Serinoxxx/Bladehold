using UnityEngine;

/// <summary>
///     The Death Nova: a Reincarnate-tree ability. Hooks <see cref="Health.TryPreventDeath" /> on the
///     player's own <see cref="Health" /> so it's asked before every lethal hit is finalized.
///
///     Unlocking is itself a stat (<see cref="StatType.DeathNovaCharges" />) rather than a hardcoded node-id
///     check, per this project's "expose upgradeable numbers as stats" convention (see CLAUDE.md). While
///     locked (0 charges) or on cooldown, this simply returns <c>false</c> and death proceeds normally.
///
///     Once unlocked, a lethal hit always triggers the blast — <see cref="novaHitbox" /> (a <see cref="DamageTrigger" />
///     child of the player) is activated regardless of whether the revive upgrade is owned, since the base
///     node is "blast on death", and revive is a separate stacking upgrade on top of it. Only when
///     <see cref="StatType.DeathNovaRevivePercent" /> is above zero does this also call <see cref="Health.Revive" />
///     and return <c>true</c> to cancel the death outright.
/// </summary>
public class DeathNova : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("A DamageTrigger child of the player (readsPlayerStats off) configured with the nova's radius/damage/knockback.")]
    [SerializeField] private DamageTrigger novaHitbox;
    [SerializeField] private DeathNovaSO config;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;

    private float cooldownRemaining;
    private bool anyError = false;

    /// <summary>True if the nova is unlocked and off cooldown right now.</summary>
    public bool IsAvailable => !anyError && stats.GetValue(StatType.DeathNovaCharges) >= 1f && cooldownRemaining <= 0f;

    /// <summary>Seconds left before the nova is available again, or 0 if it's ready (or locked).</summary>
    public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);

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
        if (novaHitbox == null)
        {
            Debug.LogError("DeathNova 'novaHitbox' (DamageTrigger) is not assigned in the inspector.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("DeathNovaSO is not assigned in the inspector.");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("DeathNova could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.DeathNovaCharges, config.baseCharges);
        stats.SetBase(StatType.DeathNovaCooldown, config.baseCooldownSeconds);
        stats.SetBase(StatType.DeathNovaRevivePercent, config.baseRevivePercent);

        health.TryPreventDeath += HandleLethal;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.TryPreventDeath -= HandleLethal;
        }
    }

    private void Update()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= Time.deltaTime;
        }
    }

    private bool HandleLethal()
    {
        if (!IsAvailable)
        {
            return false;
        }

        cooldownRemaining = Mathf.Max(1f, stats.GetValue(StatType.DeathNovaCooldown));
        novaHitbox.Activate();

        float revivePercent = Mathf.Clamp01(stats.GetValue(StatType.DeathNovaRevivePercent));
        if (revivePercent <= 0f)
        {
            // Base tier: the blast still fires, but there's no revive upgrade yet — death proceeds normally.
            return false;
        }

        health.Revive(health.MaxHealth * revivePercent);
        return true;
    }
}
