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

    private void EnsureCaptureVisual()
    {
        if (captureVisual != null) return;

        captureVisual = new GameObject("NetCaptureVisual");
        captureVisual.transform.SetParent(transform, false);
        captureVisual.transform.localPosition = Vector3.zero;
        captureVisual.transform.localRotation = Quaternion.identity;

        float scale = 1.0f;
        Collider col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            scale = Mathf.Max(0.8f, col.bounds.size.magnitude * 0.45f);
        }

        // 1. Ground rope ring at feet
        GameObject ropePrefab = null;
#if UNITY_EDITOR
        ropePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonGeneric/Models/SM_Gen_Prop_Rope_02.fbx");
#endif
        if (ropePrefab != null)
        {
            GameObject feetRing = new GameObject("RopeFeetRing");
            feetRing.transform.SetParent(captureVisual.transform, false);
            feetRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            feetRing.transform.localScale = Vector3.one * (scale * 0.85f);

            var mf = feetRing.AddComponent<MeshFilter>();
            mf.sharedMesh = ropePrefab.GetComponentInChildren<MeshFilter>()?.sharedMesh;

            var mr = feetRing.AddComponent<MeshRenderer>();
#if UNITY_EDITOR
            mr.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Bladehold/Materials/MAT_RopeNet.mat");
#endif
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 2. Draped Net Dome lattice over enemy body
        Mesh netMesh = null;
#if UNITY_EDITOR
        netMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Bladehold/Models/NetDomeMesh.asset");
#endif
        if (netMesh != null)
        {
            GameObject netDome = new GameObject("DrapedNetDome");
            netDome.transform.SetParent(captureVisual.transform, false);
            netDome.transform.localPosition = new Vector3(0f, scale * 1.1f, 0f);
            netDome.transform.localScale = new Vector3(scale * 1.1f, -scale * 1.05f, scale * 1.1f);

            var mf = netDome.AddComponent<MeshFilter>();
            mf.sharedMesh = netMesh;

            var mr = netDome.AddComponent<MeshRenderer>();
#if UNITY_EDITOR
            mr.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Bladehold/Materials/MAT_RopeNet.mat");
#endif
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
