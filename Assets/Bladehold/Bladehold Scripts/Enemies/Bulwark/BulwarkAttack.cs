using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Combat behavior for the Bulwark enemy.
///     When its shield blocks an incoming attack, it plays a block-hit reaction and launches
///     an immediate telegraphed counter-slam attack facing the attacker.
///     Can also initiate a slam when in close proximity to the player.
///     Pauses AIMovement during the wind-up and slam.
/// </summary>
public class BulwarkAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private BulwarkAttackSO attackData;
    [SerializeField] private BulwarkShield shield;
    [SerializeField] private GameObject telegraphPrefab;
    [SerializeField] private MMF_Player windupFeedback;
    [SerializeField] private MMF_Player slamFeedback;

    [SerializeField] private string slamTrigger = "Slam";
    [SerializeField] private string blockHitTrigger = "BlockHit";
    [SerializeField] private string staggerTrigger = "Stagger";
    [SerializeField] private string shieldBrokenTrigger = "ShieldBroken";
    [SerializeField] private float staggerDuration = 0.8f;

    private const int MaxOverlapResults = 64;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private int slamTriggerHash;
    private int blockHitHash;
    private int staggerTriggerHash;
    private int shieldBrokenHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;
    private GameObject activeTelegraph;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;
    private bool isSlamming = false;

    public bool IsSlamming => isSlamming;
    public BulwarkShield Shield => shield;

    /// <summary>
    ///     Per-instance damage override applied by WaveSpawner.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (animator == null)
        {
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
        if (shield == null)
        {
            shield = GetComponentInChildren<BulwarkShield>(true);
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[BulwarkAttack] Animator component is missing.");
                anyError = true;
            }
        }
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError("[BulwarkAttack] Health component is missing.");
                anyError = true;
            }
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
            if (movement == null)
            {
                Debug.LogError("[BulwarkAttack] AIMovement component is missing.");
                anyError = true;
            }
        }
        if (shield == null)
        {
            shield = GetComponentInChildren<BulwarkShield>(true);
        }

        if (anyError)
        {
            return;
        }

        slamTriggerHash = Animator.StringToHash(slamTrigger);
        blockHitHash = Animator.StringToHash(blockHitTrigger);
        staggerTriggerHash = Animator.StringToHash(staggerTrigger);
        shieldBrokenHash = Animator.StringToHash(shieldBrokenTrigger);

        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance != null)
        {
            player = playerInstance.transform;
            if (playerInstance.Health != null)
            {
                playerHealth = playerInstance.Health;
                playerHealth.OnDied += HandlePlayerDied;
            }
        }

        health.OnDied += HandleDied;
        health.OnDamaged += HandleDamaged;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDied += HandleDied;
            health.OnDamaged -= HandleDamaged;
            health.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }

        if (isSlamming)
        {
            StopAllCoroutines();
            CleanupTelegraph();
            if (movement != null)
            {
                movement.SetMovementPaused(false);
                movement.SetTurningPaused(false);
            }
            isSlamming = false;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        if (animator != null && animator.layerCount > 1)
        {
            animator.SetLayerWeight(1, 0f);
        }
        enabled = false;
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || isDead || playerDead) return;

        // If damaged by player (e.g. hit in the back or shield broken), stagger normally!
        if (damage != null && damage.IsPlayerOwned)
        {
            TriggerBodyStagger();
        }
    }

    public void TriggerBodyStagger()
    {
        if (isDead || playerDead) return;

        // Interrupt any ongoing slam
        if (isSlamming)
        {
            StopAllCoroutines();
            CleanupTelegraph();
            isSlamming = false;
        }

        if (animator != null)
        {
            animator.ResetTrigger(slamTriggerHash);
            animator.SetTrigger(staggerTriggerHash);
        }

        if (movement != null)
        {
            movement.SetMovementPaused(true);
            movement.SetTurningPaused(true);
            StartCoroutine(UnpauseAfterDelay(staggerDuration));
        }

        // Put attack on cooldown so he doesn't immediately attack right out of stagger
        lastAttackTime = Time.time + staggerDuration;
    }

    private IEnumerator UnpauseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isDead && !playerDead && movement != null && !isSlamming)
        {
            movement.SetMovementPaused(false);
            movement.SetTurningPaused(false);
        }
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || isSlamming)
        {
            return;
        }

        float cooldown = attackData != null ? attackData.slamCooldown : 2.5f;
        if (Time.time - lastAttackTime < cooldown)
        {
            return;
        }

        if (player == null && Player.Instance != null)
        {
            player = Player.Instance.transform;
        }

        if (player != null)
        {
            float triggerRange = attackData != null ? attackData.triggerRange : 3f;
            float sqrDist = (player.position - transform.position).sqrMagnitude;
            if (sqrDist <= triggerRange * triggerRange)
            {
                StartSlam(player.position);
            }
        }
    }

    /// <summary>
    ///     Called by BulwarkShield when an attack is successfully blocked.
    ///     Triggers block hit animation and initiates immediate counter-slam towards the attacker.
    /// </summary>
    public void HandleShieldBlocked(Damage damage)
    {
        if (anyError || isDead || playerDead)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(blockHitHash);
        }

        // If not already in the middle of a slam, counter-attack immediately!
        if (!isSlamming)
        {
            Vector3 targetPos = player != null ? player.position : transform.position + transform.forward * 2f;
            if (damage != null && damage.sourcePosition != Vector3.zero)
            {
                targetPos = damage.sourcePosition;
            }
            StartSlam(targetPos);
        }
    }

    public void HandleShieldBroken()
    {
        // Shield has broken; Bulwark can no longer block.
        if (animator != null)
        {
            animator.SetTrigger(shieldBrokenHash);
            if (animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, 0f);
            }
        }
    }

    private void StartSlam(Vector3 targetPosition)
    {
        isSlamming = true;
        lastAttackTime = Time.time;

        // Face towards target
        Vector3 dir = (targetPosition - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        if (movement != null)
        {
            movement.SetMovementPaused(true);
        }

        if (animator != null)
        {
            animator.SetTrigger(slamTriggerHash);
        }

        if (windupFeedback != null)
        {
            windupFeedback.PlayFeedbacks();
        }

        float forwardOffset = attackData != null ? attackData.forwardOffset : 1.6f;
        float radius = attackData != null ? attackData.slamRadius : 2.5f;
        Vector3 center = transform.position + transform.forward * forwardOffset;

        if (telegraphPrefab != null)
        {
            activeTelegraph = Instantiate(telegraphPrefab, center + Vector3.up * 0.05f, Quaternion.identity);
            Vector3 scale = activeTelegraph.transform.localScale;
            activeTelegraph.transform.localScale = new Vector3(radius * 2f, scale.y, radius * 2f);
        }

        float telegraphDuration = attackData != null ? attackData.telegraphSeconds : 1.2f;
        StartCoroutine(ExecuteSlamAfterDelay(center, telegraphDuration));
    }

    private IEnumerator ExecuteSlamAfterDelay(Vector3 center, float delay)
    {
        yield return new WaitForSeconds(delay);

        CleanupTelegraph();

        if (isDead || !enabled)
        {
            if (movement != null)
            {
                movement.SetMovementPaused(false);
            }
            isSlamming = false;
            yield break;
        }

        if (slamFeedback != null)
        {
            slamFeedback.PlayFeedbacks();
        }

        float radius = attackData != null ? attackData.slamRadius : 2.5f;
        float damageValue = damageOverride ?? (attackData != null ? attackData.slamDamage : 15f);
        float knockback = attackData != null ? attackData.knockbackForce : 12f;

        hitTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            if (!col.TryGetComponent(out IDamageable damageable))
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            if (damageable == null || damageable == ownerDamageable)
            {
                continue;
            }

            // Don't damage own shield
            if (shield != null && (damageable == (IDamageable)shield || col.transform.IsChildOf(transform)))
            {
                continue;
            }

            if (!hitTargets.Add(damageable))
            {
                continue;
            }

            Damage damage = new Damage
            {
                value = damageValue,
                type = DamageType.blunt,
                unparryable = true,
                knockbackForce = knockback,
                source = ownerDamageable,
                sourcePosition = center,
                direction = (col.transform.position - center).normalized
            };

            damageable.ReceiveDamage(damage);
        }

        // Brief delay before resuming movement after slam impact
        yield return new WaitForSeconds(0.4f);

        if (movement != null)
        {
            movement.SetMovementPaused(false);
        }

        isSlamming = false;
    }

    private void CleanupTelegraph()
    {
        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }
    }
}
