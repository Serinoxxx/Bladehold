using System;
using UnityEngine;

/// <summary>
///     Lobbed ballistic projectile launched by Net Thrower defenses.
///     Arcs towards the target point with a spinning net bundle and trail VFX,
///     expanding into a grounded net circle upon impact and triggering the AOE root.
/// </summary>
public class NetProjectile : MonoBehaviour
{
    [Header("Flight Arc")]
    [SerializeField] private float arcHeight = 3.5f;
    [SerializeField] private float flightDuration = 0.65f;
    [SerializeField] private float spinSpeed = 540f;
    [SerializeField] private Transform netMeshTransform;

    [Header("Impact Feedback")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private GameObject groundNetVfxPrefab;
    [SerializeField] private AudioClip impactSfx;

    private Vector3 startPoint;
    private Vector3 targetPoint;
    private float elapsedTime = 0f;
    private bool hasImpacted = false;
    private Action<Vector3> onImpactCallback;

    public void Launch(Vector3 start, Vector3 target, float duration, Action<Vector3> onImpact)
    {
        startPoint = start;
        targetPoint = target;
        flightDuration = Mathf.Max(0.2f, duration);
        onImpactCallback = onImpact;
        transform.position = start;
        elapsedTime = 0f;
        hasImpacted = false;

        ResolveFallbacks();
    }

    private void Awake()
    {
        ResolveFallbacks();
    }

    private void ResolveFallbacks()
    {
#if UNITY_EDITOR
        if (impactVfxPrefab == null)
        {
            impactVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Impact_Wood_01.prefab");
            if (impactVfxPrefab == null)
            {
                impactVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Dust_Small_01.prefab");
            }
        }
        if (impactSfx == null)
        {
            impactSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/Generic Wood Item Break A.wav");
        }
#endif
    }

    private void Update()
    {
        if (hasImpacted) return;

        elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(elapsedTime / flightDuration);

        // Parabolic arc flight
        Vector3 currentPos = Vector3.Lerp(startPoint, targetPoint, t);
        float heightOffset = 4f * arcHeight * t * (1f - t);
        currentPos.y += heightOffset;

        Vector3 moveDir = currentPos - transform.position;
        if (moveDir.sqrMagnitude > 0.001f)
        {
            transform.forward = moveDir.normalized;
        }
        transform.position = currentPos;

        // Vigorously spin net bundle in air
        if (netMeshTransform != null)
        {
            netMeshTransform.Rotate(Vector3.up * (spinSpeed * Time.deltaTime), Space.Self);
            netMeshTransform.Rotate(Vector3.right * (spinSpeed * 0.45f * Time.deltaTime), Space.Self);
        }

        if (t >= 1f)
        {
            Impact();
        }
    }

    public void Impact()
    {
        if (hasImpacted) return;
        hasImpacted = true;

        Vector3 hitPos = targetPoint;

        // Play impact sound
        if (impactSfx != null)
        {
            AudioSource.PlayClipAtPoint(impactSfx, hitPos, 1.0f);
        }

        // Spawn dust/wood impact puff
        if (impactVfxPrefab != null)
        {
            GameObject impactVfx = Instantiate(impactVfxPrefab, hitPos + Vector3.up * 0.1f, Quaternion.identity);
            Destroy(impactVfx, 2.5f);
        }

        // Spawn ground net visual ring if assigned
        if (groundNetVfxPrefab != null)
        {
            GameObject groundNet = Instantiate(groundNetVfxPrefab, hitPos + Vector3.up * 0.05f, Quaternion.identity);
            Destroy(groundNet, 3.5f);
        }

        onImpactCallback?.Invoke(hitPos);
        Destroy(gameObject);
    }
}
