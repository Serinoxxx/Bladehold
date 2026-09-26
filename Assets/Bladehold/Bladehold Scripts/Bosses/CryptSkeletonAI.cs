using System;
using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     AI controller for Crypt Skeletons summoned by Malakor during Phase 1.
///     Pursues the player via NavMeshAgent, executes telegraphed melee strikes,
///     and alerts NecromancerBossController upon death to update the boss shield counter.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class CryptSkeletonAI : MonoBehaviour
{
    [Header("Combat Stats")]
    [SerializeField] private float attackDamage = 12f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float attackCooldown = 1.8f;
    [SerializeField] private float windupTime = 0.35f;
    [SerializeField] private float knockbackForce = 4.0f;

    [Header("Feedback (optional)")]
    [Tooltip("Optional: played at the skeleton as it swings. The Necromancer adds this component at runtime unless the skeleton prefab carries it, so nothing is authored yet.")]
    [SerializeField] private MMF_Player attackSwingFeedback;
    [Tooltip("Optional: played at the player when a strike lands. Nothing is authored yet.")]
    [SerializeField] private MMF_Player hitFeedback;
    [Tooltip("Optional: played at the skeleton when it dies. Nothing is authored yet.")]
    [SerializeField] private MMF_Player deathFeedback;

    [Header("Despawn")]
    [SerializeField] private float sinkDelay = 2.5f;
    [SerializeField] private float sinkDuration = 1.5f;

    private Health health;
    private NavMeshAgent agent;
    private Animator animator;
    private Collider col;
    private NecromancerBossController ownerBoss;

    private float nextAttackTime = 0f;
    private bool isAttacking = false;
    private bool isDead = false;

    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashDeath = Animator.StringToHash("Death");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

    public event Action<CryptSkeletonAI> OnDiedEvent;

    private void Awake()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        col = GetComponent<Collider>();
    }

    private void Start()
    {
        if (health != null)
        {
            health.OnDied += HandleDied;
            health.OnDamaged += HandleDamaged;
        }

        if (agent != null)
        {
            agent.stoppingDistance = attackRange * 0.8f;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }
    }

    /// <summary>
    ///     Binds this skeleton instance to the summoning Necromancer boss.
    /// </summary>
    public void Initialize(NecromancerBossController boss)
    {
        ownerBoss = boss;
    }

    private void Update()
    {
        if (isDead) return;

        Player player = Player.Instance;
        if (player == null || player.Health == null || player.Health.IsDead)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            UpdateAnimatorSpeed(0f);
            return;
        }

        Vector3 targetPos = player.transform.position;
        float distanceToPlayer = Vector3.Distance(transform.position, targetPos);

        if (isAttacking)
        {
            // Face target during attack windup
            Vector3 lookDir = (targetPos - transform.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
            }
            return;
        }

        if (distanceToPlayer <= attackRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(PerformMeleeAttack(player));
        }
        else
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(targetPos);
            }
            UpdateAnimatorSpeed(agent != null ? agent.velocity.magnitude : 0f);
        }
    }

    private void UpdateAnimatorSpeed(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat(HashMoveSpeed, speed);
        }
    }

    private IEnumerator PerformMeleeAttack(Player player)
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        UpdateAnimatorSpeed(0f);

        if (animator != null)
        {
            animator.SetTrigger(HashAttack);
        }

        if (attackSwingFeedback != null)
        {
            attackSwingFeedback.PlayFeedbacks(transform.position);
        }

        yield return new WaitForSeconds(windupTime);

        if (!isDead && player != null && player.Damageable != null)
        {
            float currentDist = Vector3.Distance(transform.position, player.transform.position);
            if (currentDist <= attackRange + 1.0f)
            {
                Damage dmg = new Damage
                {
                    value = attackDamage,
                    type = DamageType.slash,
                    knockbackForce = knockbackForce,
                    sourcePosition = transform.position,
                    source = health,
                    direction = transform.forward
                };

                player.Damageable.ReceiveDamage(dmg);

                if (hitFeedback != null)
                {
                    hitFeedback.PlayFeedbacks(player.transform.position);
                }
            }
        }

        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
    }

    private void HandleDamaged(Damage dmg)
    {
        // React to hit
    }

    private void HandleDied()
    {
        if (isDead) return;
        isDead = true;

        if (col != null) col.enabled = false;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger(HashDeath);
        }

        if (deathFeedback != null)
        {
            deathFeedback.PlayFeedbacks(transform.position);
        }

        OnDiedEvent?.Invoke(this);

        if (ownerBoss != null)
        {
            ownerBoss.OnSkeletonDied(this);
        }

        StartCoroutine(SinkAndDespawnRoutine());
    }

    private IEnumerator SinkAndDespawnRoutine()
    {
        yield return new WaitForSeconds(sinkDelay);

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * 2.5f;
        float elapsed = 0f;

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / sinkDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
