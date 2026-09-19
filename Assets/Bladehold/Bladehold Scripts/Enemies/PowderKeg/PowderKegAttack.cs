using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Powder Keg enemy behavior:
///     Carries a red explosive barrel above his head. If struck by an arrow, the barrel detonates
///     immediately, dealing explosive area-of-effect damage to all units around him.
///     If he approaches within 2 metres of a gate, he slams the barrel down and detonates, dealing
///     25 damage in an AoE.
/// </summary>
public class PowderKegAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private PowderKegAttackSO attackData;
    [SerializeField] private AITargetSelector targetSelector;
    [SerializeField] private EnemyRagdoll ragdoll;

    [Header("Visuals & Feedback")]
    [Tooltip("Visual prop of the explosive barrel held above the head; hidden upon detonation.")]
    [SerializeField] private GameObject barrelVisual;
    [Tooltip("Explosion VFX spawned on detonation.")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private MMF_Player slamFeedback;
    [SerializeField] private MMF_Player explodeFeedback;

    [Header("Animation")]
    [Tooltip("Trigger name for slamming the barrel down on gate contact.")]
    [SerializeField] private string slamTrigger = "Slam";

    private const int MaxOverlapResults = 128;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private int slamTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Health playerHealth;
    private bool hasExploded = false;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;
    private bool isSlamming = false;

    /// <summary>
    ///     Per-instance damage override from WaveSpawner / Enemies.csv.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    /// <summary>
    ///     Triggered when hit with an arrow (either on the barrel or on the body).
    /// </summary>
    public void DetonateFromArrow(Vector3 hitPoint)
    {
        if (anyError || isDead || hasExploded) return;
        Explode(isGateDetonation: false);
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
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }
        if (ragdoll == null)
        {
            ragdoll = GetComponent<EnemyRagdoll>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("[PowderKegAttack] Animator component is not assigned or found.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("[PowderKegAttack] Health component is not assigned or found.", this);
            anyError = true;
        }
        if (movement == null)
        {
            Debug.LogError("[PowderKegAttack] AIMovement component is not assigned or found.", this);
            anyError = true;
        }
        if (attackData == null)
        {
            Debug.LogError("[PowderKegAttack] PowderKegAttackSO is not assigned in the inspector.", this);
            anyError = true;
        }

        if (anyError) return;

        slamTriggerHash = Animator.StringToHash(slamTrigger);
        ownerDamageable = GetComponentInParent<IDamageable>();

        health.OnDied += HandleDied;
        health.OnDamaged += HandleDamaged;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            playerHealth = Player.Instance.Health;
            playerHealth.OnDied += HandlePlayerDied;
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
        if (barrelVisual != null)
        {
            barrelVisual.SetActive(false);
        }
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || isDead || hasExploded) return;

        // If the unit takes damage from an arrow or ranged projectile, explode immediately
        if (damage != null && damage.isProjectile)
        {
            DetonateFromArrow(damage.sourcePosition);
        }
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || hasExploded || isSlamming) return;

        CheckGateProximity();
    }

    private void CheckGateProximity()
    {
        Gate nearestGate = Gate.NearestAlive(transform.position);
        if (nearestGate == null) return;

        float sqrDist = (nearestGate.TargetPosition - transform.position).sqrMagnitude;
        float triggerDist = attackData.triggerGateRange;

        if (sqrDist <= triggerDist * triggerDist)
        {
            StartCoroutine(GateSlamRoutine());
        }
    }

    private IEnumerator GateSlamRoutine()
    {
        isSlamming = true;
        if (movement != null)
        {
            movement.SetMovementPaused(true);
        }

        if (animator != null)
        {
            animator.SetTrigger(slamTriggerHash);
        }

        if (slamFeedback != null)
        {
            slamFeedback.PlayFeedbacks();
        }

        if (attackData.slamWindupSeconds > 0f)
        {
            yield return new WaitForSeconds(attackData.slamWindupSeconds);
        }

        if (isDead || hasExploded) yield break;

        Explode(isGateDetonation: true);
    }

    private void Explode(bool isGateDetonation)
    {
        if (hasExploded) return;
        hasExploded = true;

        if (barrelVisual != null)
        {
            barrelVisual.SetActive(false);
        }

        Vector3 explosionPosition = transform.position;
        if (ragdoll != null && ragdoll.IsRagdolled && ragdoll.Pelvis != null)
        {
            explosionPosition = ragdoll.Pelvis.position;
        }

        if (explodeFeedback != null)
        {
            explodeFeedback.transform.position = explosionPosition;
            explodeFeedback.PlayFeedbacks();
        }

        if (explosionVfxPrefab != null)
        {
            Instantiate(explosionVfxPrefab, explosionPosition, Quaternion.identity);
        }

        float damageToDeal = isGateDetonation
            ? attackData.gateExplosionDamage
            : (damageOverride ?? attackData.baseExplosionDamage);

        ApplyExplosionDamage(explosionPosition, damageToDeal);

        // Force-kill self so coin drops, corpse cleanup, and wave progression execute properly
        health.ReceiveDamage(new Damage
        {
            value = 999999f,
            type = attackData.damageType,
            sourcePosition = explosionPosition,
            source = ownerDamageable,
            unparryable = true
        });
    }

    private void ApplyExplosionDamage(Vector3 center, float damageValue)
    {
        hitTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, attackData.explosionRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (!col.TryGetComponent(out IDamageable damageable))
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;
            if (!hitTargets.Add(damageable)) continue;

            damageable.ReceiveDamage(new Damage
            {
                value = damageValue,
                type = attackData.damageType,
                sourcePosition = center,
                knockbackForce = attackData.knockbackForce,
                unparryable = true,
                isPlayerDamage = false,
                source = ownerDamageable
            });
        }
    }
}
