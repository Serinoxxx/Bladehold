using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Red Demon's leap &amp; slam: a <see cref="TrollSlamAttack" />-style ground telegraph is
///     locked at the player's position, the demon winds up, then flies a parabolic arc to the
///     telegraph with its <see cref="NavMeshAgent" /> disabled and slams on landing — the Troll's
///     impact block (<c>unparryable</c>, impulse-stamped, never hits itself). The landing re-seats
///     the agent with <see cref="NavMesh.SamplePosition" /> + <c>agent.Warp</c> (borrowing
///     <see cref="ImpulseReceiver" />'s NavMesh-recovery shape); the telegraph stays visible for the
///     whole wind-up + flight, so the dodge window is honest. Movement is paused throughout.
/// </summary>
public class LeapSlamAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private LeapSlamAttackSO attackData;
    [Tooltip("Flat decal/quad revealed on the ground over the landing area. Scaled on x/z to the slam's diameter.")]
    [SerializeField] private GameObject telegraphPrefab;
    [Tooltip("Optional VFX instantiated at the slam centre on impact (assumed to clean itself up).")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private MMF_Player windupFeedback;
    [SerializeField] private MMF_Player slamFeedback;

    // Animator trigger for the leap wind-up. Wire a crouch/leap state driven by this in the Animator.
    [SerializeField] private string attackTrigger = "Attack";

    private const int MaxOverlapResults = 128;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private int attackTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;
    private GameObject activeTelegraph;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool leaping;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="LeapSlamAttackSO" />
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
            Debug.LogError("LeapSlamAttackSO is not assigned in the inspector.");
            anyError = true;
        }
        if (telegraphPrefab == null)
        {
            Debug.LogError("Telegraph prefab is not assigned in the inspector; the landing area must be revealed before the slam lands.");
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
            Debug.LogError("Player.Instance is not set; the demon has no one to leap at.");
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
        // Corpses have nothing left to tick. (Coroutines survive a disable; RunLeap bails on
        // isDead, cleans up the telegraph, and leaves the corpse wherever the flight got to —
        // AIMovement's own death handling has already disabled the agent.)
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || leaping) return;

        if (Time.time - lastAttackTime < attackData.attackCooldown) return;

        if (IsPlayerInRange())
        {
            StartLeap();
        }
    }

    private bool IsPlayerInRange()
    {
        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.triggerRange * attackData.triggerRange;
    }

    private void StartLeap()
    {
        lastAttackTime = Time.time;
        leaping = true;

        // The landing spot is locked when the wind-up starts — that's what makes the telegraph
        // honest and the slam dodgeable. Snap it to the NavMesh so the demon can't land off-mesh.
        Vector3 target = player.position;
        if (NavMesh.SamplePosition(target, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
        {
            target = navHit.position;
        }

        movement.SetMovementPaused(true);

        if (windupFeedback != null)
        {
            windupFeedback.PlayFeedbacks();
        }
        animator.SetTrigger(attackTriggerHash);

        activeTelegraph = Instantiate(telegraphPrefab, target + Vector3.up * 0.05f, Quaternion.identity);
        Vector3 scale = activeTelegraph.transform.localScale;
        activeTelegraph.transform.localScale = new Vector3(attackData.slamRadius * 2f, scale.y, attackData.slamRadius * 2f);

        StartCoroutine(RunLeap(target));
    }

    private IEnumerator RunLeap(Vector3 target)
    {
        yield return new WaitForSeconds(attackData.windupSeconds);

        // A demon killed mid-wind-up never leaves the ground.
        if (isDead)
        {
            CleanupTelegraph();
            movement.SetMovementPaused(false);
            leaping = false;
            yield break;
        }

        // Airborne: the agent is off so the arc isn't clamped to the ground plane.
        agent.enabled = false;

        Vector3 start = transform.position;
        Vector3 flatDirection = target - start;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(flatDirection.normalized);
        }

        float elapsed = 0f;
        float flight = Mathf.Max(0.05f, attackData.flightSeconds);
        while (elapsed < flight && !isDead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flight);
            Vector3 position = Vector3.Lerp(start, target, t);
            position.y += Mathf.Sin(t * Mathf.PI) * attackData.arcHeight;
            transform.position = position;
            yield return null;
        }

        CleanupTelegraph();

        if (isDead)
        {
            // Killed mid-air: the corpse stays where the flight stopped (AIMovement's death
            // handler owns the disabled agent from here).
            leaping = false;
            yield break;
        }

        // Land: re-seat the agent (the ImpulseReceiver NavMesh-recovery shape), then slam. The
        // target was NavMesh-sampled at lock time, so Warp lands on the mesh; the fallback sample
        // covers the mesh having no exact point under the locked spot.
        transform.position = target;
        agent.enabled = true;
        if (NavMesh.SamplePosition(target, out NavMeshHit landHit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(landHit.position);
        }
        else
        {
            agent.Warp(start);
        }

        if (slamFeedback != null)
        {
            slamFeedback.PlayFeedbacks();
        }
        if (impactVfxPrefab != null)
        {
            Instantiate(impactVfxPrefab, target, Quaternion.identity);
        }

        // The player being dead by now just means the slam hits nobody that matters — but skip it
        // entirely so cheering goblins aren't flung by a leftover leap.
        if (!playerDead)
        {
            ApplySlamDamage(target);
        }

        movement.SetMovementPaused(false);
        lastAttackTime = Time.time;
        leaping = false;
    }

    private void CleanupTelegraph()
    {
        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }
    }

    /// <summary>The Troll's impact block: every unique <see cref="IDamageable" /> in the area except the demon itself, impulse-stamped.</summary>
    private void ApplySlamDamage(Vector3 center)
    {
        hitTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, attackData.slamRadius, overlapBuffer);
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
                sourcePosition = center,
                impulsePower = attackData.impulsePower,
                impulseForce = attackData.impulseForce,
                source = ownerDamageable,
                unparryable = true,
            });
        }
    }
}
