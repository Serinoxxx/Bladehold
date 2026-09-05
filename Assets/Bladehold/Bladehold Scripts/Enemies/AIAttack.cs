using MoreMountains.Feedbacks;
using System.Collections;
using UnityEngine;

/// <summary>
///     Lets an AI goblin attack the player. When the player comes within
///     <see cref="AIAttackSO.attackRange" /> (and the attack is off cooldown) the goblin plays its
///     attack animation; if the player is still in range at the attack's apex, the player takes
///     damage. This never touches the <see cref="UnityEngine.AI.NavMeshAgent" />, so the goblin can
///     keep chasing and attack while moving. All tunable values live on <see cref="AIAttackSO" />.
/// </summary>
public class AIAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIAttackSO attackData;
    [Tooltip("Optional target-selection layer (gate defense): the attack lands on the current target — gate or player. Without it, only the player is ever attacked, as before.")]
    [SerializeField] private AITargetSelector targetSelector;
    [SerializeField] private MMF_Player startAttackFeedback;
    [SerializeField] private MMF_Player attackHitFeedback;
    [SerializeField] private KnockbackReceiver knockbackReceiver;
    [SerializeField] private AIMovement movement;

    // Animator trigger that starts the attack. Wire an attack state driven by this in the Animator.
    [SerializeField] private string attackTrigger = "Attack";

    private int attackTriggerHash;
    private int staggerTriggerHash;
    private float? damageOverride;
    private Coroutine attackRoutine;
    private Transform player;
    private IDamageable playerDamageable;
    private Health playerHealth;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    private float damageMultiplier = 1f;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="AIAttackSO" /> is
    ///     never mutated.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    public void SetDamageMultiplier(float value)
    {
        damageMultiplier = Mathf.Max(0f, value);
    }

    public float BaseDamage => (damageOverride ?? (attackData != null ? attackData.damage : 0f)) * damageMultiplier;

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
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
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
        if (attackData == null)
        {
            Debug.LogError("AIAttackSO is not assigned in the inspector.");
            anyError = true;
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
        }

        if (anyError)
        {
            return;
        }

        attackTriggerHash = Animator.StringToHash(attackTrigger);
        staggerTriggerHash = Animator.StringToHash("Stagger");

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the goblin has no one to attack.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;
        playerDamageable = playerInstance.Damageable;

        // Stop attacking once this goblin dies.
        health.OnDied += HandleDied;
        health.OnDamaged += HandleDamaged;

        // Stop attacking once the player dies (combat's over — time to celebrate).
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
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (movement != null)
        {
            movement.SetTurningPaused(false);
        }
        // Corpses have nothing left to tick. (Coroutines survive a disable, and ApplyDamageAtApex
        // already bails on isDead.)
        enabled = false;
    }

    private void HandleDamaged(Damage damage)
    {
        if (isDead || health.CurrentHealth <= 0f) return;
        if (knockbackReceiver != null && knockbackReceiver.IsIncapacitated) return;

        // If the player hit us, stagger and interrupt the attack
        if (damage.source != null && playerHealth != null && ReferenceEquals(damage.source, playerHealth))
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            animator.SetTrigger(staggerTriggerHash);
            
            if (movement != null)
            {
                movement.SetTurningPaused(true);
            }

            // Put attack on cooldown so they don't immediately attack again when the animation finishes
            lastAttackTime = Time.time + attackData.staggerCooldown - attackData.attackCooldown;
            
            // We need to unpause turning after stagger cooldown. Since staggerCooldown represents the animation duration effectively here.
            // We can just use Invoke or a Coroutine to unpause turning.
            StartCoroutine(UnpauseTurningAfterDelay(attackData.staggerCooldown));
        }
    }

    private IEnumerator UnpauseTurningAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (movement != null && !isDead)
        {
            movement.SetTurningPaused(false);
        }
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastAttackTime < attackData.attackCooldown) return;

        // Do not attack if there is no damageable target (e.g. flocking to an objective and waiting for player)
        if (CurrentTargetDamageable() == null) return;

        if (attackRoutine == null && IsTargetInRange())
        {
            attackRoutine = StartCoroutine(PrepareAndAttack());
        }
    }

    /// <summary>The current target's damage sink — the selector's pick (gate or player), else the player.</summary>
    private IDamageable CurrentTargetDamageable()
    {
        return targetSelector != null ? targetSelector.TargetDamageable : playerDamageable;
    }

    private bool IsTargetInRange()
    {
        Vector3 targetPosition;
        if (targetSelector != null)
        {
            targetPosition = targetSelector.TargetPosition;
        }
        else if (player != null)
        {
            targetPosition = player.position;
        }
        else
        {
            return false;
        }

        // Planar (XZ) distance: a mounted player sits ~1.8m up in the saddle, which would push the
        // rider outside short melee ranges in 3D even when the horse is flank-to-face with the
        // attacker. Ground-to-ground targets are unaffected (their height difference is ~0).
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        
        if (toTarget.sqrMagnitude > attackData.attackRange * attackData.attackRange)
        {
            return false;
        }
        
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            float angle = Vector3.Angle(forward, toTarget);
            if (angle > attackData.attackConeAngle)
            {
                return false;
            }
        }
        
        return true;
    }

    private IEnumerator PrepareAndAttack()
    {
        if (attackData.preAttackDelay > 0f)
        {
            yield return new WaitForSeconds(attackData.preAttackDelay);
        }

        if (isDead || playerDead || !IsTargetInRange())
        {
            attackRoutine = null;
            yield break;
        }

        if (startAttackFeedback != null)
        {
            startAttackFeedback.PlayFeedbacks();
        }
        lastAttackTime = Time.time;
        animator.SetTrigger(attackTriggerHash);
        
        if (movement != null)
        {
            movement.SetTurningPaused(true);
        }

        // The apex is approximated by a tunable wind-up so it stays in sync with the attack clip
        // without needing an animation event.
        yield return new WaitForSeconds(attackData.windupToApex);

        // Only connect if this goblin is still alive, the run is still going, and the target it
        // wound up on (re-resolved — it may have switched between player and gate) is still in range.
        IDamageable target = CurrentTargetDamageable();
        if (!isDead && !playerDead && target != null && IsTargetInRange())
        {
            target.ReceiveDamage(new Damage
            {
                value = BaseDamage,
                isCritical = false,
                type = attackData.damageType,
                sourcePosition = transform.position,
                source = health
            });

            if (attackHitFeedback != null)
            {
                attackHitFeedback.PlayFeedbacks();
            }
        }
        
        if (movement != null)
        {
            movement.SetTurningPaused(false);
            movement.ApplyTurnPenalty(attackData.postAttackTurnMultiplier, attackData.postAttackTurnPenaltyDuration);
        }
        
        attackRoutine = null;
    }
}
