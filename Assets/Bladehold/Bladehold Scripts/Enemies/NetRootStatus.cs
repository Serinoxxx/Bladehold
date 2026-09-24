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

    private GameObject captureVisual;

    public bool IsRooted => rootActive;
    public GameObject CaptureVisual => captureVisual;

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
        RemoveCaptureVisual();
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
            EnsureCaptureVisual();
        }
    }

    [SerializeField] private NetRootConfigSO config;
    private static NetRootConfigSO cachedConfig;

    private static NetRootConfigSO GetConfig()
    {
        if (cachedConfig == null)
        {
            cachedConfig = Resources.Load<NetRootConfigSO>("NetRootConfigSO");
        }
        return cachedConfig;
    }

    private void EnsureCaptureVisual()
    {
        if (captureVisual != null) return;

        NetRootConfigSO activeConfig = config != null ? config : GetConfig();
        if (activeConfig == null || activeConfig.captureVisualPrefab == null)
        {
            Debug.LogWarning("[NetRootStatus] No captureVisualPrefab configured in NetRootConfigSO!");
            return;
        }

        captureVisual = Instantiate(activeConfig.captureVisualPrefab, transform);
        captureVisual.transform.localPosition = Vector3.zero;
        captureVisual.transform.localRotation = Quaternion.identity;

        if (activeConfig.scaleWithTargetCollider)
        {
            float scale = activeConfig.scaleMultiplier;
            Collider col = GetComponentInChildren<Collider>();
            if (col != null)
            {
                scale *= Mathf.Max(activeConfig.minScale, col.bounds.size.magnitude * 0.45f);
            }
            captureVisual.transform.localScale = Vector3.one * scale;
        }
    }

    private void RemoveCaptureVisual()
    {
        if (captureVisual != null)
        {
            Destroy(captureVisual);
            captureVisual = null;
        }
    }

    public void RestoreMovement()
    {
        if (!rootActive) return;
        rootActive = false;
        remainingSeconds = 0f;
        RemoveCaptureVisual();

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
        RemoveCaptureVisual();
        RestoreMovement();
    }
}
