using MoreMountains.Feedbacks;
using System.Collections;
using UnityEngine;

/// <summary>
///     Lets the Storm Witch fire a slow, dodgeable <see cref="LightningBall" /> projectile at the player.
///     When the player comes within <see cref="LightningBallAttackSO.attackRange" /> (and the attack is
///     off cooldown) the witch plays her cast animation; if the player is still alive at the cast's apex,
///     a ball is instantiated at <see cref="firePoint" /> and launched toward the player's position at
///     that moment. Follows the same skeleton as <see cref="AIAttack" />, but instantiates a projectile
///     instead of applying damage directly. Never touches the <see cref="UnityEngine.AI.NavMeshAgent" />.
/// </summary>
public class LightningBallAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private LightningBallAttackSO attackData;
    [SerializeField] private Transform firePoint;
    [SerializeField] private LightningBall ballPrefab;
    [SerializeField] private MMF_Player startAttackFeedback;

    // Animator trigger that starts the cast. Wire a cast state driven by this in the Animator.
    [SerializeField] private string attackTrigger = "Attack";

    private int attackTriggerHash;
    private float? damageOverride;
    private Transform player;
    private IDamageable ownerDamageable;
    private Health playerHealth;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="LightningBallAttackSO" />
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
            Debug.LogError("LightningBallAttackSO is not assigned in the inspector.");
            anyError = true;
        }
        if (firePoint == null)
        {
            Debug.LogError("Fire point Transform is not assigned in the inspector.");
            anyError = true;
        }
        if (ballPrefab == null)
        {
            Debug.LogError("LightningBall prefab is not assigned in the inspector.");
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
            Debug.LogError("Player.Instance is not set; the witch has no one to attack.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        // Stop attacking once this witch dies.
        health.OnDied += HandleDied;

        // Stop attacking once the player dies.
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
        // Corpses have nothing left to tick. (Coroutines survive a disable, and LaunchAfterWindup
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

        if (IsPlayerInRange())
        {
            StartAttack();
        }
    }

    private bool IsPlayerInRange()
    {
        if (player == null) return false;

        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.attackRange * attackData.attackRange;
    }

    private void StartAttack()
    {
        if (startAttackFeedback != null)
        {
            startAttackFeedback.PlayFeedbacks();
        }
        lastAttackTime = Time.time;
        animator.SetTrigger(attackTriggerHash);
        StartCoroutine(LaunchAfterWindup());
    }

    private IEnumerator LaunchAfterWindup()
    {
        // The apex is approximated by a tunable wind-up so it stays in sync with the cast clip
        // without needing an animation event. (An animation event could call an equivalent method
        // for frame-perfect timing.)
        yield return new WaitForSeconds(attackData.windupToApex);

        // Only fire if this witch is still alive and the player is still alive and in range.
        if (isDead || playerDead || player == null || !IsPlayerInRange())
        {
            yield break;
        }

        var offsetPlayerPosition = player.position + 1.5f * Vector3.up; // Aim for the player's chest/torso instead of their feet.
        Vector3 direction = offsetPlayerPosition - firePoint.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }
        direction.Normalize();

        LightningBall ball = Instantiate(ballPrefab, firePoint.position, Quaternion.LookRotation(direction));
        ball.Launch(direction, attackData.ballSpeed, damageOverride ?? attackData.damage, attackData.damageType, attackData.ballLifetime, ownerDamageable);
    }
}
