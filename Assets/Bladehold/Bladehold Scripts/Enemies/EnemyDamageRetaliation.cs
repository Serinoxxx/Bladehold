using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Boss/Special Enemy mechanic: when cumulative damage received crosses a threshold
///     (e.g. every 25% of Max HP), the enemy temporarily retaliates by turning around to chase
///     and attack the player for a set duration (e.g. 10s) before resuming its primary objective (gate).
/// </summary>
public class EnemyDamageRetaliation : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private AITargetSelector targetSelector;

    [Header("Retaliation Tuning")]
    [Tooltip("Fraction of max HP taken to trigger a retaliation phase (0.25 = 25% max health).")]
    [SerializeField] private float damageFractionPerTrigger = 0.25f;

    [Tooltip("Duration in seconds the enemy will chase and prioritize the player before returning to its gate.")]
    [SerializeField] private float retaliationDuration = 10f;

    [Tooltip("Optional feedback played when the enemy enrages and turns to retaliate against the player.")]
    [SerializeField] private MMF_Player retaliationFeedback;

    private float accumulatedDamage = 0f;
    private bool isDead = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }

        if (health == null)
        {
            Debug.LogError("[EnemyDamageRetaliation] Health component is missing.");
            anyError = true;
        }
        if (targetSelector == null)
        {
            Debug.LogError("[EnemyDamageRetaliation] AITargetSelector component is missing.");
            anyError = true;
        }

        if (anyError) return;

        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        enabled = false;
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || isDead || damage.value <= 0f) return;

        accumulatedDamage += damage.value;
        float threshold = health.MaxHealth * Mathf.Clamp01(damageFractionPerTrigger);

        if (threshold > 0f && accumulatedDamage >= threshold)
        {
            accumulatedDamage -= threshold;
            TriggerRetaliation();
        }
    }

    private void TriggerRetaliation()
    {
        if (targetSelector != null)
        {
            targetSelector.SetPlayerTargetOverride(retaliationDuration);
        }

        if (retaliationFeedback != null)
        {
            retaliationFeedback.PlayFeedbacks();
        }
    }
}
