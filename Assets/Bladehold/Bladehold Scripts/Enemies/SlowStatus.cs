using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     A slow/chill status effect on an enemy: scales the <see cref="NavMeshAgent" />'s speed *and*
///     the Animator's playback speed by the strongest active slow, restoring both when it expires.
///     Added lazily at runtime by whichever skill applies the first slow (via <see cref="GetOrAdd" />,
///     the <see cref="EnemyRagdoll" /> lazy-build idiom — enemy prefabs need no wiring and un-slowed
///     enemies cost nothing). Re-applying keeps the strongest fraction and the longest remaining time.
///
///     Only things with a <see cref="NavMeshAgent" /> on their <see cref="Health" /> root qualify, so
///     gates and the player (a <see cref="CharacterController" />) can never be slowed — per the design
///     rule that CC only ever applies to enemies. <see cref="IsSlowed" /> is read by the sword's
///     <see cref="DamageTrigger" /> for the "Ice Breaker" bonus.
/// </summary>
public class SlowStatus : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private AIMovement movement;
    private Health health;

    private float slowFraction;
    private float remainingSeconds;
    private float baseAnimatorSpeed = 1f;
    private float fallbackAgentSpeed;
    private bool slowActive;

    /// <summary>True while any slow is active — the "Ice Breaker" melee-bonus check.</summary>
    public bool IsSlowed => slowActive;

    /// <summary>Strongest active slow fraction (0-1), 0 while unslowed.</summary>
    public float CurrentSlowFraction => slowActive ? slowFraction : 0f;

    /// <summary>
    ///     Resolves <paramref name="target" /> (any collider/damageable component on an enemy) to its
    ///     <see cref="Health" /> root and returns the root's <see cref="SlowStatus" />, adding one on
    ///     first use. Returns null for the dead, for things without a <see cref="Health" />, and for
    ///     anything that isn't a NavMesh enemy (gates, the player).
    /// </summary>
    public static SlowStatus GetOrAdd(Component target)
    {
        if (target == null)
        {
            return null;
        }

        Health health = target.GetComponentInParent<Health>();
        if (health == null || health.IsDead)
        {
            return null;
        }

        GameObject root = health.gameObject;
        if (!root.TryGetComponent(out NavMeshAgent _))
        {
            return null;
        }

        if (!root.TryGetComponent(out SlowStatus status))
        {
            status = root.AddComponent<SlowStatus>();
        }
        return status;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        // Synty rigs keep the Animator on a child model object.
        animator = GetComponentInChildren<Animator>();
        movement = GetComponent<AIMovement>();
        health = GetComponent<Health>();

        if (health != null)
        {
            health.OnDied += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    /// <summary>
    ///     Applies a slow of <paramref name="fraction" /> (0-1) for <paramref name="durationSeconds" />.
    ///     A weaker slow never overrides a stronger one; a shorter one never trims remaining time.
    /// </summary>
    public void ApplySlow(float fraction, float durationSeconds)
    {
        fraction = Mathf.Clamp01(fraction);
        if (fraction <= 0f || durationSeconds <= 0f || (health != null && health.IsDead))
        {
            return;
        }

        if (!slowActive)
        {
            // Latch the unslowed speeds on the first application, not per re-apply.
            baseAnimatorSpeed = animator != null ? animator.speed : 1f;
            fallbackAgentSpeed = agent != null ? agent.speed : 0f;
            slowActive = true;
        }

        remainingSeconds = Mathf.Max(remainingSeconds, durationSeconds);
        if (fraction > slowFraction)
        {
            slowFraction = fraction;
        }
        ApplyMultipliers();
    }

    private void Update()
    {
        if (!slowActive)
        {
            return;
        }

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds <= 0f)
        {
            ClearSlow();
        }
    }

    private void ApplyMultipliers()
    {
        float multiplier = 1f - slowFraction;
        if (agent != null && agent.enabled)
        {
            agent.speed = UnslowedAgentSpeed() * multiplier;
        }
        if (animator != null)
        {
            animator.speed = baseAnimatorSpeed * multiplier;
        }
    }

    private void ClearSlow()
    {
        slowActive = false;
        slowFraction = 0f;
        remainingSeconds = 0f;

        if (agent != null && agent.enabled)
        {
            agent.speed = UnslowedAgentSpeed();
        }
        if (animator != null)
        {
            animator.speed = baseAnimatorSpeed;
        }
    }

    /// <summary>
    ///     The speed to scale/restore from. Prefer <see cref="AIMovement.BaseSpeed" /> (roster override
    ///     or SO — immune to this component's own writes) over the speed latched at first application.
    /// </summary>
    private float UnslowedAgentSpeed()
    {
        if (movement != null)
        {
            float baseSpeed = movement.BaseSpeed;
            if (baseSpeed > 0f)
            {
                return baseSpeed;
            }
        }
        return fallbackAgentSpeed;
    }

    private void HandleDied()
    {
        // Corpses aren't slowed: let the death animation play at full speed and stop ticking.
        // AIMovement disables the agent itself, so only the animator needs restoring.
        if (animator != null)
        {
            animator.speed = baseAnimatorSpeed;
        }
        slowActive = false;
        enabled = false;
    }
}
