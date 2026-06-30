using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Applies knockback to this character when it takes a hit carrying knockback. Like every other
///     reaction in this codebase it <b>subscribes to <see cref="Health.OnDamaged" /></b> rather than being
///     called directly — Health stays unaware of it. Reads <see cref="Damage.knockbackForce" /> and
///     <see cref="Damage.sourcePosition" /> and slides the <see cref="NavMeshAgent" /> away from the source
///     for a short time, staying on the NavMesh.
///
///     Plays nicely with <see cref="AIMovement" />: it pauses the agent for the slide and resumes it after,
///     and bails out if the character has died (the agent is disabled on death).
/// </summary>
public class KnockbackReceiver : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private NavMeshAgent agent;

    [Tooltip("Seconds the knockback slide lasts; the impulse decays linearly to zero over this time.")]
    [SerializeField] private float duration = 0.18f;

    private Coroutine routine;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (damage.knockbackForce <= 0f) return;
        if (health.IsDead) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector3 direction = transform.position - damage.sourcePosition;
        direction.y = 0f;
        // Degenerate (hit from directly above/same spot) → shove backwards from facing.
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;

        if (routine != null)
        {
            StopCoroutine(routine);
        }
        routine = StartCoroutine(Push(direction, damage.knockbackForce));
    }

    private IEnumerator Push(Vector3 direction, float force)
    {
        // Pause pathfinding so AIMovement's SetDestination doesn't fight the slide; the agent keeps its
        // path and resumes chasing afterwards.
        agent.isStopped = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Stop early if the character died mid-slide (AIMovement disables the agent on death).
            if (health.IsDead || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                routine = null;
                yield break;
            }

            float decay = 1f - (elapsed / duration); // linear fade-out
            agent.Move(direction * (force * decay * Time.deltaTime));

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh && !health.IsDead)
        {
            agent.isStopped = false;
        }
        routine = null;
    }
}
