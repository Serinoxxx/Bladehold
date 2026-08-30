using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Slayer's telegraphed line dash: when the player is in range and the attack is off
///     cooldown, it locks a lane toward the player's position, pre-clamps it to the NavMesh with
///     <see cref="NavMesh.Raycast" /> (the <see cref="MountedKnightBrain" /> lane precedent), shows a
///     stretched ground telegraph for <see cref="SlayerDashAttackSO.telegraphSeconds" /> (the
///     <see cref="TrollSlamAttack" /> telegraph handling), then executes near-instantly: the swept
///     lane is capsule-overlapped for damage (<c>unparryable</c> — a whole-lane sweep has no single
///     swing to read) and the slayer is re-seated at the lane's end via <c>agent.Warp</c>.
///     Sidestepping the lane during the telegraph avoids everything.
/// </summary>
public class SlayerDashAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private SlayerDashAttackSO attackData;
    [Tooltip("Optional target-selection layer (gate defense): dash aims at current target — gate or player. Without it, only player is targeted.")]
    [SerializeField] private AITargetSelector targetSelector;
    [Tooltip("Flat quad stretched along the dash lane during the telegraph. Scaled to (lane width, y, lane length).")]
    [SerializeField] private GameObject telegraphPrefab;
    [Tooltip("Optional debris/particle trail prefab spawned on the slayer during the dash lerp.")]
    [SerializeField] private GameObject trailPrefab;
    [Tooltip("Massive ground smash / impact VFX spawned when the dash lands.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [Tooltip("Massive smash audio clip played on dash landing.")]
    [SerializeField] private AudioClip smashSfx;
    [SerializeField] private MMF_Player windupFeedback;
    [SerializeField] private MMF_Player dashFeedback;
    [Tooltip("Optional camera shake / Feel feedback played on dash landing.")]
    [SerializeField] private MMF_Player smashFeedback;

    // Animator trigger for the dash wind-up. Wire a crouch/ready state driven by this in the Animator.
    [SerializeField] private string attackTrigger = "Attack";

    private const int MaxOverlapResults = 64;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private int attackTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;
    private GameObject activeTelegraph;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="SlayerDashAttackSO" />
    ///     is never mutated.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
        }
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (movement == null)
        {
            Debug.LogError("AIMovement component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (attackData == null)
        {
            Debug.LogError("SlayerDashAttackSO is not assigned in the inspector.");
            anyError = true;
        }
        if (telegraphPrefab == null)
        {
            Debug.LogError("Telegraph prefab is not assigned in the inspector; the dash lane must be revealed before it executes.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        attackTriggerHash = Animator.StringToHash(attackTrigger);
        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the slayer has no one to dash at.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        health.OnDied += HandleDied;

        if (playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        // Corpses have nothing left to tick. (Coroutines survive a disable; DashAfterTelegraph
        // bails on isDead and cleans up the telegraph.)
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastAttackTime < attackData.attackCooldown) return;

        if (IsTargetInRange())
        {
            StartDash();
        }
    }

    private Vector3 GetTargetPosition()
    {
        if (targetSelector != null)
        {
            return targetSelector.TargetPosition;
        }
        return player != null ? player.position : transform.position;
    }

    private bool IsTargetInRange()
    {
        Vector3 targetPos = GetTargetPosition();
        Vector3 diff = targetPos - transform.position;
        diff.y = 0f;
        return diff.sqrMagnitude <= attackData.triggerRange * attackData.triggerRange;
    }

    private void StartDash()
    {
        lastAttackTime = Time.time;

        // The lane is locked to the target's position when the wind-up starts — that's what makes
        // the telegraph honest and the dash dodgeable.
        Vector3 targetPos = GetTargetPosition();
        Vector3 direction = targetPos - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }
        direction.Normalize();

        // Pre-clamp the lane with NavMesh.Raycast so the telegraph shows exactly where the dash
        // ends — near a wall the lane is visibly shorter (the MountedKnightBrain precedent).
        float laneLength = attackData.maxDashDistance;
        if (NavMesh.Raycast(transform.position, transform.position + direction * attackData.maxDashDistance, out NavMeshHit navHit, NavMesh.AllAreas))
        {
            laneLength = Mathf.Max(2f, navHit.distance);
        }

        movement.SetMovementPaused(true);
        transform.rotation = Quaternion.LookRotation(direction);

        if (windupFeedback != null)
        {
            windupFeedback.PlayFeedbacks();
        }
        animator.SetTrigger(attackTriggerHash);

        Vector3 laneCenter = transform.position + direction * (laneLength * 0.5f) + Vector3.up * 0.05f;
        activeTelegraph = Instantiate(telegraphPrefab, laneCenter, Quaternion.LookRotation(direction));
        Vector3 scale = activeTelegraph.transform.localScale;
        activeTelegraph.transform.localScale = new Vector3(attackData.laneWidth * transform.localScale.x, scale.y, laneLength);

        StartCoroutine(DashAfterTelegraph(direction, laneLength));
    }

    private IEnumerator DashAfterTelegraph(Vector3 direction, float laneLength)
    {
        yield return new WaitForSeconds(attackData.telegraphSeconds);

        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }

        // A slayer killed mid-wind-up never dashes; a dead player means the run is over.
        if (isDead || playerDead)
        {
            movement.SetMovementPaused(false);
            yield break;
        }

        if (dashFeedback != null)
        {
            dashFeedback.PlayFeedbacks();
        }

        Vector3 start = transform.position;
        Vector3 end = start + direction * laneLength;

        GameObject activeTrail = null;
        if (trailPrefab != null)
        {
            activeTrail = Instantiate(trailPrefab, transform.position, transform.rotation, transform);
        }

        if (agent != null && agent.enabled)
        {
            agent.enabled = false;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, attackData.dashDuration);
        while (elapsed < duration)
        {
            if (isDead) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, end, t);
            transform.rotation = Quaternion.LookRotation(direction);
            yield return null;
        }

        if (!isDead)
        {
            transform.position = end;
            if (impactVfxPrefab != null)
            {
                Instantiate(impactVfxPrefab, end, Quaternion.identity);
            }
            if (smashFeedback != null)
            {
                smashFeedback.PlayFeedbacks(end);
            }
            else if (smashSfx != null)
            {
                AudioSource.PlayClipAtPoint(smashSfx, end, 1.0f);
            }
            ApplyLaneDamage(start, end, direction);
        }

        if (activeTrail != null)
        {
            var psList = activeTrail.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList)
            {
                ps.Stop();
            }
            Destroy(activeTrail, 2f);
        }

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(end);
            }
        }
        transform.rotation = Quaternion.LookRotation(direction);

        movement.SetMovementPaused(false);
        lastAttackTime = Time.time;
    }

    /// <summary>Damages every unique <see cref="IDamageable" /> in the swept lane except the slayer itself.</summary>
    private void ApplyLaneDamage(Vector3 start, Vector3 end, Vector3 direction)
    {
        hitTargets.Clear();
        Vector3 up = Vector3.up * 1.5f;
        float radius = Mathf.Max(1.5f, attackData.laneWidth * 0.5f * transform.localScale.x);
        Vector3 sweepEnd = end + direction * (radius * 1.5f);
        int count = Physics.OverlapCapsuleNonAlloc(start + up, sweepEnd + up, radius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;
            if (!hitTargets.Add(damageable)) continue;

            damageable.ReceiveDamage(new Damage
            {
                value = damageOverride ?? attackData.damage,
                type = attackData.damageType,
                sourcePosition = start,
                source = ownerDamageable,
                unparryable = true,
            });
        }

        // Direct fallback for assigned/targeted gate or player in front of the slayer
        IDamageable directTarget = targetSelector != null ? targetSelector.TargetDamageable : null;
        if (directTarget != null && directTarget != ownerDamageable && !hitTargets.Contains(directTarget))
        {
            Vector3 targetPos = GetTargetPosition();
            float distToTarget = Vector3.Distance(end, targetPos);
            if (distToTarget <= Mathf.Max(6f, radius * 3f))
            {
                hitTargets.Add(directTarget);
                directTarget.ReceiveDamage(new Damage
                {
                    value = damageOverride ?? attackData.damage,
                    type = attackData.damageType,
                    sourcePosition = start,
                    source = ownerDamageable,
                    unparryable = true,
                });
            }
        }
    }
}
