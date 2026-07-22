using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Fort Golem's arrow barrage. Telegraphs, then summons an <see cref="ArrowBarrageZone"/>
///     at the player's position that rains arrows for a duration.
/// </summary>
public class ArrowBarrageAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private ArrowBarrageAttackSO attackData;
    [SerializeField] private GameObject barrageZonePrefab;

    [SerializeField] private string revTrigger = "Attack";

    private int revTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool attacking;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<AIMovement>();
    }

    private void Start()
    {
        if (animator == null || health == null || movement == null || attackData == null || barrageZonePrefab == null)
        {
            Debug.LogError("ArrowBarrageAttack is missing required components or references.");
            anyError = true;
            return;
        }

        revTriggerHash = Animator.StringToHash(revTrigger);
        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
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
        if (health != null) health.OnDied -= HandleDied;
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
    }

    private void HandleDied()
    {
        isDead = true;
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || attacking) return;

        if (Time.time - lastAttackTime < attackData.attackCooldown) return;

        if (IsPlayerInRange())
        {
            StartCoroutine(RunAttack());
        }
    }

    private bool IsPlayerInRange()
    {
        float sqrDistance = (player.position - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.triggerRange * attackData.triggerRange;
    }

    private IEnumerator RunAttack()
    {
        attacking = true;
        lastAttackTime = Time.time;

        movement.SetMovementPaused(true);
        animator.SetTrigger(revTriggerHash);

        // Turn to face player during rev-up
        float revElapsed = 0f;
        while (revElapsed < attackData.revSeconds && !isDead && !playerDead)
        {
            FacePlayer(15f);
            revElapsed += Time.deltaTime;
            yield return null;
        }

        if (isDead || playerDead)
        {
            attacking = false;
            if (!isDead) movement.SetMovementPaused(false);
            yield break;
        }

        // Spawn the barrage zone at the player's position
        Vector3 targetPos = player.position;
        targetPos.y = 0f; // Ensure it stays on the ground plane
        
        GameObject zoneObj = Instantiate(barrageZonePrefab, targetPos, Quaternion.identity);
        if (zoneObj.TryGetComponent(out ArrowBarrageZone zone))
        {
            zone.Initialize(attackData.zoneRadius, attackData.barrageDuration, attackData.tickRate, damageOverride ?? attackData.damage, attackData.damageType, ownerDamageable);
        }

        if (!isDead)
        {
            movement.SetMovementPaused(false);
        }

        lastAttackTime = Time.time;
        attacking = false;
    }

    private void FacePlayer(float turnSpeed)
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }
}
