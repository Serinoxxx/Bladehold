using MoreMountains.Feedbacks;
using System.Collections;
using UnityEngine;

/// <summary>
///     The Evil God's 360° radial burst: every cooldown, while the player is anywhere within
///     <see cref="RadialBurstAttackSO.attackRange" /> (no line of sight or facing needed), it releases
///     <see cref="RadialBurstAttackSO.projectileCount" /> straight <see cref="LightningBall" />
///     projectiles at even angles around itself — the player dodges the gaps, not the aim. The
///     <see cref="LightningBallAttack" /> skeleton with the single aimed launch swapped for the ring.
///     Never touches the <see cref="UnityEngine.AI.NavMeshAgent" />.
/// </summary>
public class RadialBurstAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private RadialBurstAttackSO attackData;
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
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="RadialBurstAttackSO" />
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
            Debug.LogError("RadialBurstAttackSO is not assigned in the inspector.");
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
            Debug.LogError("Player.Instance is not set; the evil god has no one to attack.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        // Stop attacking once this enemy dies.
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
        // Corpses have nothing left to tick. (Coroutines survive a disable, and ReleaseAfterWindup
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
        StartCoroutine(ReleaseAfterWindup());
    }

    private IEnumerator ReleaseAfterWindup()
    {
        // The apex is approximated by a tunable wind-up so it stays in sync with the cast clip
        // without needing an animation event.
        yield return new WaitForSeconds(attackData.windupToApex);

        // Only fire if this enemy is still alive and the player is still alive. Range is NOT
        // re-checked: once the wind-up committed, the ring comes out — backing off mid-cast
        // doesn't cancel a boss burst.
        if (isDead || playerDead)
        {
            yield break;
        }

        float damage = damageOverride ?? attackData.damage;
        int count = Mathf.Max(1, attackData.projectileCount);

        // Even angles around the vertical axis, anchored on the current facing so consecutive
        // bursts rotate with the body instead of repeating identical lanes.
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 360f / count, 0f) * transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }
            direction.Normalize();

            LightningBall ball = Instantiate(ballPrefab, firePoint.position, Quaternion.LookRotation(direction));
            ball.Launch(direction, attackData.projectileSpeed, damage, attackData.damageType,
                attackData.projectileLifetime, ownerDamageable);
        }
    }
}
