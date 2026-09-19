using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Root / immobilization status effect applied by Net Thrower defenses.
///     Stops the <see cref="NavMeshAgent" /> in place for a duration.
///     Added dynamically at runtime via <see cref="GetOrAdd" />.
/// </summary>
public class NetRootStatus : MonoBehaviour
{
    private NavMeshAgent agent;
    private AIMovement movement;
    private Health health;

    private float remainingSeconds;
    private bool rootActive;

    public bool IsRooted => rootActive;

    public static NetRootStatus GetOrAdd(Component target)
    {
        if (target == null) return null;

        Health health = target.GetComponentInParent<Health>();
        if (health == null || health.IsDead) return null;

        GameObject root = health.gameObject;
        if (!root.TryGetComponent(out NavMeshAgent _)) return null;

        if (!root.TryGetComponent(out NetRootStatus status))
        {
            status = root.AddComponent<NetRootStatus>();
        }

        return status;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
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
        RestoreMovement();
    }

    private void Update()
    {
        if (!rootActive) return;

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds <= 0f)
        {
            RestoreMovement();
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }
    }

    public void ApplyRoot(float durationSeconds)
    {
        if (durationSeconds <= 0f) return;

        remainingSeconds = Mathf.Max(remainingSeconds, durationSeconds);
        if (!rootActive)
        {
            rootActive = true;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            if (movement != null)
            {
                movement.enabled = false;
            }
        }
    }

    private void RestoreMovement()
    {
        if (!rootActive) return;
        rootActive = false;
        remainingSeconds = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        if (movement != null)
        {
            movement.enabled = true;
        }
    }

    private void HandleDied()
    {
        RestoreMovement();
    }
}
