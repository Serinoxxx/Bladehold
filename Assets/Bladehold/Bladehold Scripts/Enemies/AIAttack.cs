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

    // Animator trigger that starts the attack. Wire an attack state driven by this in the Animator.
    [SerializeField] private string attackTrigger = "Attack";

    private int attackTriggerHash;
    private float? damageOverride;
    private Transform player;
    private IDamageable playerDamageable;
    private Health playerHealth;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="AIAttackSO" /> is
    ///     never mutated.
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
        if (attackData == null)
        {
            Debug.LogError("AIAttackSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        attackTriggerHash = Animator.StringToHash(attackTrigger);

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
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        // Corpses have nothing left to tick. (Coroutines survive a disable, and ApplyDamageAtApex
        // already bails on isDead.)
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
            StartAttack();
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
        return toTarget.sqrMagnitude <= attackData.attackRange * attackData.attackRange;
    }

    private void StartAttack()
    {
        if (startAttackFeedback != null)
        {
            startAttackFeedback.PlayFeedbacks();
        }
        lastAttackTime = Time.time;
        animator.SetTrigger(attackTriggerHash);
        StartCoroutine(ApplyDamageAtApex());
    }

    private IEnumerator ApplyDamageAtApex()
    {
        // The apex is approximated by a tunable wind-up so it stays in sync with the attack clip
        // without needing an animation event. (An animation event could call an equivalent method
        // for frame-perfect timing.)
        yield return new WaitForSeconds(attackData.windupToApex);

        // Only connect if this goblin is still alive, the run is still going, and the target it
        // wound up on (re-resolved — it may have switched between player and gate) is still in range.
        IDamageable target = CurrentTargetDamageable();
        if (isDead || playerDead || target == null || !IsTargetInRange())
        {
            yield break;
        }

        target.ReceiveDamage(new Damage
        {
            value = damageOverride ?? attackData.damage,
            type = attackData.damageType,
            sourcePosition = transform.position,
            source = health
        });

        if (attackHitFeedback != null)
        {
            attackHitFeedback.PlayFeedbacks();
        }
    }
}
