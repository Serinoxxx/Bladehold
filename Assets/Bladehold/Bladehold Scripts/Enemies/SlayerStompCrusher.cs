using UnityEngine;
using UnityEngine.AI;
using MoreMountains.Feedbacks;
using System;

/// <summary>
///     Attached to the Slayer boss. As the Slayer stomps forward, he ignores minor enemy avoidance
///     and crushes non-boss minions underfoot with lethal damage, blood splatters, and heavy stomp SFX.
/// </summary>
public class SlayerStompCrusher : MonoBehaviour
{
    [Header("Crush Settings")]
    [Tooltip("Radius around the Slayer's feet where minor enemies are crushed.")]
    [SerializeField] private float crushRadius = 1.6f;

    [Tooltip("Forward offset from the Slayer's transform origin for the crush sphere.")]
    [SerializeField] private float forwardOffset = 0.6f;

    [Tooltip("Minimum movement speed required to crush enemies.")]
    [SerializeField] private float minMoveSpeed = 0.2f;

    [Header("Juice & Feedback")]
    [Tooltip("Optional blood splatter / impact prefab spawned on crushed enemies.")]
    [SerializeField] private GameObject crushVfxPrefab;

    [Tooltip("Heavy stomp / crunch audio clip played on crushing.")]
    [SerializeField] private AudioClip crushSfx;

    [Tooltip("Optional MMF_Player feedback played on crush.")]
    [SerializeField] private MMF_Player crushFeedback;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Health health;

    [SerializeField] AIAnimationEvents aiAnimationEvents;

    private readonly Collider[] hitColliders = new Collider[32];
    private IDamageable myDamageable;
    private bool anyError = false;
    private bool isDead = false;
    private float lastCrushSoundTime = -999f;

   

    private void OnValidate()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<Health>();
    }

    private void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<Health>();

        if (aiAnimationEvents!=null)
        {
            aiAnimationEvents.OnLeftFootStomp += TryCrushAction;
            aiAnimationEvents.OnRightFootStomp += TryCrushAction;
        }

        if (agent == null)
        {
            Debug.LogError("[SlayerStompCrusher] NavMeshAgent not found on Slayer!", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("[SlayerStompCrusher] Health not found on Slayer!", this);
            anyError = true;
        }

        if (anyError) return;

        myDamageable = GetComponent<IDamageable>();
        health.OnDied += HandleDied;

        // Force Slayer to barrel straight through without avoiding small goblins
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        agent.avoidancePriority = 0; // Highest priority
    }

    private void TryCrushAction()
    {
        TryCrush();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        enabled = false;
    }

    private void TryCrush()
    {
        if (anyError || isDead || agent == null || !agent.enabled) return;

        if (agent.velocity.magnitude < minMoveSpeed) return;

        Vector3 crushCenter = transform.position + transform.forward * forwardOffset;
        int hitCount = Physics.OverlapSphereNonAlloc(crushCenter, crushRadius, hitColliders);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            if (col == null) continue;

            // Skip self and player
            if (col.transform.root == transform.root) continue;
            if (Player.Instance != null && col.transform.root == Player.Instance.transform.root) continue;

            Health targetHealth = col.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth.IsDead) continue;

            // Skip bosses / special enemies / siege engines with objective components
            if (col.GetComponentInParent<SpecialEnemyIntro>() != null) continue;

            // Deal crushing lethal damage to minor enemy
            Damage damage = new Damage
            {
                value = 5,
                type = DamageType.blunt,
                isCritical = true,
                sourcePosition = crushCenter,
                source = myDamageable,
                direction = (col.transform.position - crushCenter).normalized,
                unparryable = true,
                isPlayerDamage = false
            };

            targetHealth.ReceiveDamage(damage);

            // Spawn crush feedback / VFX
            if (crushVfxPrefab != null)
            {
                Instantiate(crushVfxPrefab, col.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            if (crushFeedback != null)
            {
                crushFeedback.PlayFeedbacks(col.transform.position);
            }
            else if (crushSfx != null && Time.time - lastCrushSoundTime > 0.15f)
            {
                AudioSource.PlayClipAtPoint(crushSfx, col.transform.position, 1.0f);
                lastCrushSoundTime = Time.time;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 crushCenter = transform.position + transform.forward * forwardOffset;
        Gizmos.DrawWireSphere(crushCenter, crushRadius);
    }
}
