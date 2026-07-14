using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Mechanical Golem's pinball charge: after a stationary rev-up telegraph it detaches from
///     its <see cref="NavMeshAgent" /> and careens in a straight line for
///     <see cref="PinballChargeSO.chargeSeconds" />, reflecting off NavMesh edges like a pinball —
///     each tick's travel is pre-checked with <see cref="NavMesh.Raycast" /> and, on a wall, the
///     velocity is reflected about the hit normal (bounce). Anything it touches takes contact
///     damage (<c>unparryable</c>, impulse-stamped so clipped goblins get flung; a per-target re-hit
///     window keeps one charge from grinding a cornered player). When the run ends it re-seats with
///     <see cref="NavMesh.SamplePosition" /> + <c>agent.Warp</c> and resumes normal chasing.
///     Movement bounces are NavMesh-clamped, so it physically cannot leave the arena.
/// </summary>
public class PinballCharge : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private PinballChargeSO attackData;
    [SerializeField] private MMF_Player revFeedback;
    [SerializeField] private MMF_Player bounceFeedback;

    // Animator trigger for the rev-up. Wire a shudder/rev state driven by this in the Animator.
    [SerializeField] private string revTrigger = "Attack";

    private const int MaxOverlapResults = 32;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly Dictionary<IDamageable, float> lastHitTimes = new Dictionary<IDamageable, float>();

    private int revTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;
    private float lastChargeTime = Mathf.NegativeInfinity;
    private bool charging;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="PinballChargeSO" />
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
            Debug.LogError("PinballChargeSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        revTriggerHash = Animator.StringToHash(revTrigger);
        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the golem has no one to charge.");
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
        // Corpses have nothing left to tick. (Coroutines survive a disable; RunCharge bails on
        // isDead and leaves the corpse wherever the run stopped — AIMovement's death handling owns
        // the agent from here.)
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || charging) return;

        if (Time.time - lastChargeTime < attackData.attackCooldown) return;

        if (IsPlayerInRange())
        {
            StartCoroutine(RunCharge());
        }
    }

    private bool IsPlayerInRange()
    {
        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.triggerRange * attackData.triggerRange;
    }

    private IEnumerator RunCharge()
    {
        charging = true;
        lastChargeTime = Time.time;

        // Rev-up: stationary telegraph, aimed at the player's current position.
        movement.SetMovementPaused(true);
        if (revFeedback != null)
        {
            revFeedback.PlayFeedbacks();
        }
        animator.SetTrigger(revTriggerHash);

        yield return new WaitForSeconds(attackData.revSeconds);

        if (isDead || playerDead)
        {
            if (!isDead)
            {
                movement.SetMovementPaused(false);
            }
            charging = false;
            yield break;
        }

        // Launch: direction locked at the end of the rev (the player had the whole rev to move).
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }
        direction.Normalize();

        Vector3 chargeStart = transform.position;
        agent.enabled = false;
        lastHitTimes.Clear();

        float elapsed = 0f;
        while (elapsed < attackData.chargeSeconds && !isDead)
        {
            float step = attackData.chargeSpeed * Time.deltaTime;
            Vector3 from = transform.position;
            Vector3 to = from + direction * step;

            // The pinball part: a NavMesh edge in this tick's travel reflects the velocity about
            // the edge normal instead of being crossed.
            if (NavMesh.Raycast(from, to, out NavMeshHit navHit, NavMesh.AllAreas))
            {
                Vector3 normal = navHit.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.0001f)
                {
                    // Degenerate normal (mesh corner) — turn around rather than tunnel out.
                    direction = -direction;
                }
                else
                {
                    direction = Vector3.Reflect(direction, normal.normalized);
                    direction.y = 0f;
                    direction.Normalize();
                }

                // Park at the wall this tick; next tick travels along the reflected line.
                to = navHit.position + direction * 0.05f;

                if (bounceFeedback != null)
                {
                    bounceFeedback.PlayFeedbacks();
                }
            }

            transform.position = to;
            transform.rotation = Quaternion.LookRotation(direction);

            ApplyContactDamage();

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isDead)
        {
            charging = false;
            yield break;
        }

        // Re-seat on the mesh (the ImpulseReceiver NavMesh-recovery shape). Every bounce stayed
        // NavMesh-clamped, so the sample is a formality; the charge start is the last-ditch anchor.
        agent.enabled = true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit seatHit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(seatHit.position);
        }
        else
        {
            agent.Warp(chargeStart);
        }

        movement.SetMovementPaused(false);
        lastChargeTime = Time.time;
        charging = false;
    }

    /// <summary>Contact damage with a per-target re-hit window — one charge can clip many targets, but can't grind a cornered one.</summary>
    private void ApplyContactDamage()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, attackData.contactRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;
            if (lastHitTimes.TryGetValue(damageable, out float lastHit) && Time.time - lastHit < attackData.rehitSeconds) continue;

            lastHitTimes[damageable] = Time.time;
            damageable.ReceiveDamage(new Damage
            {
                value = damageOverride ?? attackData.damage,
                type = attackData.damageType,
                sourcePosition = transform.position,
                impulsePower = attackData.impulsePower,
                impulseForce = attackData.impulseForce,
                source = ownerDamageable,
                unparryable = true,
            });
        }
    }
}
