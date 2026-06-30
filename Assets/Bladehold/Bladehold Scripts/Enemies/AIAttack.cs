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

    // Animator trigger that starts the attack. Wire an attack state driven by this in the Animator.
    [SerializeField] private string attackTrigger = "Attack";

    private int attackTriggerHash;
    private Transform player;
    private IDamageable playerDamageable;
    private Health playerHealth;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

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

        // Only connect if this goblin is still alive, the player is still alive, and in range.
        if (isDead || playerDead || playerDamageable == null || !IsPlayerInRange())
        {
            yield break;
        }

        playerDamageable.ReceiveDamage(new Damage
        {
            value = attackData.damage,
            type = attackData.damageType
        });
    }
}
