using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     AI component for Armored Knights in the Princess Sanctuary boss encounter (Part 5 of Castle Campaign Overhaul).
///     - Standard state: pursues player, performs telegraphed melee attacks with knockback.
///     - Downed state on 0 HP: intercepts lethal damage via <see cref="Health.TryPreventDeath"/>,
///       enters downed kneeling/grounded posture, disables hit collider, emits a holy soul beacon,
///       and alerts the Princess Boss to initiate revival.
///     - Revival: <see cref="Revive(float)"/> restores full HP, clears downed state, and resumes combat.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class ArmoredKnightAI : MonoBehaviour
{
    [Header("Combat Stats")]
    [SerializeField] private float attackDamage = 22f;
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private float windupTime = 0.4f;
    [SerializeField] private float knockbackForce = 5.5f;
    [SerializeField] private float moveSpeed = 4.2f;
    [SerializeField] private float maxHealth = 180f;

    [Header("Downed & Soul Beacon")]
    [SerializeField] private GameObject holySoulBeaconVisual;
    [SerializeField] private GameObject reviveBurstVfxPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip attackSwingSfx;
    [SerializeField] private AudioClip hitSfx;
    [SerializeField] private AudioClip downedSfx;
    [SerializeField] private AudioClip reviveSfx;

    private Health health;
    private NavMeshAgent agent;
    private Animator animator;
    private Collider knightCollider;

    private bool isDowned = false;
    private bool isAttacking = false;
    private bool isDefeated = false;
    private float nextAttackTime = 0f;

    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashDeath = Animator.StringToHash("Death");
    private static readonly int HashFallDown = Animator.StringToHash("FallDown");
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

    public bool IsDowned => isDowned;
    public bool IsDefeated => isDefeated;
    public Health Health => health;

    public event Action<ArmoredKnightAI> OnKnightDowned;
    public event Action<ArmoredKnightAI> OnKnightRevived;
    public event Action<ArmoredKnightAI> OnKnightDefeated;

    public void Initialize()
    {
        EnsureComponents();
        SubscribeEvents();
    }

    private void Awake()
    {
        EnsureComponents();
        SubscribeEvents();
    }

    private bool eventsSubscribed = false;

    public void SubscribeEvents()
    {
        if (eventsSubscribed) return;
        EnsureComponents();
        if (health != null)
        {
            health.TryPreventDeath -= HandleTryPreventDeath;
            health.TryPreventDeath += HandleTryPreventDeath;
            health.TryBlockDamage -= HandleTryBlockDamage;
            health.TryBlockDamage += HandleTryBlockDamage;
            eventsSubscribed = true;
        }
    }

    public void EnsureComponents()
    {
        if (health == null) health = GetComponent<Health>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (knightCollider == null) knightCollider = GetComponent<Collider>();

        if (holySoulBeaconVisual == null)
        {
            CreateProceduralSoulBeacon();
        }
    }

    private void Start()
    {
        EnsureComponents();
        SubscribeEvents();

        if (health != null)
        {
            if (maxHealth > 0f)
            {
                health.SetMaxHealth(maxHealth);
            }
        }

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange * 0.85f;
        }

        if (holySoulBeaconVisual != null)
        {
            holySoulBeaconVisual.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.TryPreventDeath -= HandleTryPreventDeath;
            health.TryBlockDamage -= HandleTryBlockDamage;
        }
        eventsSubscribed = false;
    }

    private void CreateProceduralSoulBeacon()
    {
        // Check if child beacon already exists
        Transform existing = transform.Find("HolySoulBeacon");
        if (existing != null)
        {
            holySoulBeaconVisual = existing.gameObject;
            return;
        }

        // Procedural vertical golden pillar of light
        holySoulBeaconVisual = new GameObject("HolySoulBeacon");
        holySoulBeaconVisual.transform.SetParent(transform, false);
        holySoulBeaconVisual.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        LineRenderer lr = holySoulBeaconVisual.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = 0.55f;
        lr.endWidth = 0.25f;
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, new Vector3(0f, 9f, 0f));

        Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material beaconMat = new Material(s);
        beaconMat.color = new Color(1.0f, 0.9f, 0.35f, 0.85f);
        lr.sharedMaterial = beaconMat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        holySoulBeaconVisual.SetActive(false);
    }

    private void Update()
    {
        if (isDowned || isDefeated) return;

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
        float distToPlayer = Vector3.Distance(transform.position, targetPos);

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

        if (distToPlayer <= attackRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(PerformMeleeAttack(player));
        }
        else
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(targetPos);
                UpdateAnimatorSpeed(agent.velocity.magnitude);
            }
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

        if (attackSwingSfx != null)
        {
            AudioSource.PlayClipAtPoint(attackSwingSfx, transform.position, 0.85f);
        }

        yield return new WaitForSeconds(windupTime);

        if (!isDowned && !isDefeated && player != null && player.Damageable != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist <= attackRange + 0.85f)
            {
                Damage dmg = new Damage
                {
                    value = attackDamage,
                    type = DamageType.slash,
                    knockbackForce = knockbackForce,
                    sourcePosition = transform.position,
                    source = health,
                    direction = transform.forward,
                    unparryable = false
                };

                player.Damageable.ReceiveDamage(dmg);

                if (hitSfx != null)
                {
                    AudioSource.PlayClipAtPoint(hitSfx, player.transform.position, 1.0f);
                }
            }
        }

        float recovery = Mathf.Max(0.1f, attackCooldown - windupTime);
        yield return new WaitForSeconds(Mathf.Min(0.5f, recovery));

        if (!isDowned && !isDefeated && agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        isAttacking = false;
    }

    /// <summary>
    ///     Intercepts lethal hit before death latches in Health, entering Downed state.
    /// </summary>
    private bool HandleTryPreventDeath()
    {
        if (isDowned) return true;
        EnterDownedState();
        return true;
    }

    /// <summary>
    ///     Blocks all damage while in downed state so downed knight is non-targetable.
    /// </summary>
    private bool HandleTryBlockDamage(Damage dmg)
    {
        return isDowned;
    }

    /// <summary>
    ///     Enters the Downed state: disables movement, disables hit collider, emits holy soul beacon.
    /// </summary>
    public void EnterDownedState()
    {
        if (isDowned) return;
        isDowned = true;
        isAttacking = false;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (knightCollider != null)
        {
            knightCollider.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger(HashFallDown);
        }

        if (holySoulBeaconVisual != null)
        {
            holySoulBeaconVisual.SetActive(true);
        }

        if (downedSfx != null)
        {
            AudioSource.PlayClipAtPoint(downedSfx, transform.position, 1.0f);
        }

        if (health != null)
        {
            // Keep HP slightly positive so Health stays active and IsDead does not latch
            health.SetCurrentHealth(1f);
        }

        Debug.Log($"[ArmoredKnightAI] {name} has been DOWNED! Emitting soul beacon for Princess revival.");
        OnKnightDowned?.Invoke(this);
    }

    /// <summary>
    ///     Revives the downed knight to active combat state, called by PrincessBossController when 5s spell finishes.
    /// </summary>
    /// <param name="healthPercent">Fraction of max health to restore (default 1.0 = 100%).</param>
    public void Revive(float healthPercent = 1.0f)
    {
        if (!isDowned) return;
        isDowned = false;
        isAttacking = false;

        if (knightCollider != null)
        {
            knightCollider.enabled = true;
        }

        if (holySoulBeaconVisual != null)
        {
            holySoulBeaconVisual.SetActive(false);
        }

        if (health != null)
        {
            health.Revive(health.MaxHealth * Mathf.Clamp01(healthPercent));
        }

        if (reviveBurstVfxPrefab != null)
        {
            Instantiate(reviveBurstVfxPrefab, transform.position, Quaternion.identity);
        }

        if (reviveSfx != null)
        {
            AudioSource.PlayClipAtPoint(reviveSfx, transform.position, 1.0f);
        }

        if (animator != null)
        {
            animator.Play("IdleRunBlend", 0, 0f);
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
        }

        Debug.Log($"[ArmoredKnightAI] {name} has been REVIVED by Princess Katherine! Returning to combat with {health?.CurrentHealth} HP.");
        OnKnightRevived?.Invoke(this);
    }

    /// <summary>
    ///     Handles defeat when Princess is slain: knight yields or falls permanently.
    /// </summary>
    public void DefeatOrSurrender()
    {
        if (isDefeated) return;
        isDefeated = true;
        isAttacking = false;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (knightCollider != null)
        {
            knightCollider.enabled = false;
        }

        if (holySoulBeaconVisual != null)
        {
            holySoulBeaconVisual.SetActive(false);
        }

        if (animator != null)
        {
            animator.SetTrigger(HashDeath);
        }

        OnKnightDefeated?.Invoke(this);
    }

    private void UpdateAnimatorSpeed(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat(HashMoveSpeed, speed);
        }
    }
}
