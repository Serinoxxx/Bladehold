using System;
using MoreMountains.Feedbacks;
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
    [Tooltip("Played where the net lands (sound + wood/dust puff). Add a ground-net visual here too if one gets made.")]
    [SerializeField] private MMF_Player impactFeedback;

    private Vector3 startPoint;
    private Vector3 targetPoint;
    private float elapsedTime = 0f;
    private bool hasImpacted = false;
    private Action<Vector3> onImpactCallback;

    private void Start()
    {
        if (impactFeedback == null)
        {
            Debug.LogError("[NetProjectile] impactFeedback is not assigned on " + gameObject.name + ".", this);
        }
    }

    public void Launch(Vector3 start, Vector3 target, float duration, Action<Vector3> onImpact)
    {
        startPoint = start;
        targetPoint = target;
        flightDuration = Mathf.Max(0.2f, duration);
        onImpactCallback = onImpact;
        transform.position = start;
        elapsedTime = 0f;
        hasImpacted = false;
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

        if (impactFeedback != null)
        {
            impactFeedback.PlayFeedbacks(hitPos);
        }

        onImpactCallback?.Invoke(hitPos);
        Destroy(gameObject);
    }
}
