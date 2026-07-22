using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Elemental Golem's boulder throw. Lob a massive boulder at the player's predicted position.
/// </summary>
public class BoulderThrowAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private BoulderThrowAttackSO attackData;
    [SerializeField] private Transform firePoint;
    [SerializeField] private BoulderProjectile boulderPrefab;

    [SerializeField] private string revTrigger = "Attack";

    private int revTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private AITargetSelector targetSelector;
    private Transform player;
    private Health playerHealth;
    private float lastAttackTime = Mathf.NegativeInfinity;
    private bool attacking;
    private bool isDead = false;
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
        if (animator == null || health == null || movement == null || attackData == null || firePoint == null || boulderPrefab == null)
        {
            Debug.LogError("BoulderThrowAttack is missing required components or references.");
            anyError = true;
            return;
        }

        revTriggerHash = Animator.StringToHash(revTrigger);
        ownerDamageable = GetComponentInParent<IDamageable>();
        targetSelector = GetComponent<AITargetSelector>();

        Player playerInstance = Player.Instance;
        if (playerInstance != null)
        {
            player = playerInstance.transform;
            if (playerInstance.Health != null)
            {
                playerHealth = playerInstance.Health;
            }
        }
        
        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        isDead = true;
        enabled = false;
    }

    private Vector3 CurrentTargetPosition => targetSelector != null ? targetSelector.TargetPosition : (player != null ? player.position : transform.position);

    private bool IsTargetDead()
    {
        if (targetSelector != null)
        {
            IDamageable targetDamageable = targetSelector.TargetDamageable;
            if (targetDamageable == null) return true;
            if (targetDamageable is Component comp && comp.TryGetComponent(out Health h))
            {
                return h.IsDead;
            }
            return false;
        }
        return playerHealth == null || playerHealth.IsDead;
    }

    private void Update()
    {
        if (anyError || isDead || IsTargetDead() || attacking) return;

        if (Time.time - lastAttackTime < attackData.attackCooldown) return;

        if (IsTargetInRange())
        {
            StartCoroutine(RunAttack());
        }
    }

    private bool IsTargetInRange()
    {
        float sqrDistance = (CurrentTargetPosition - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.triggerRange * attackData.triggerRange;
    }

    private IEnumerator RunAttack()
    {
        attacking = true;
        lastAttackTime = Time.time;

        movement.SetMovementPaused(true);
        animator.SetTrigger(revTriggerHash);

        // Turn to face target during rev-up
        float revElapsed = 0f;
        while (revElapsed < attackData.revSeconds && !isDead && !IsTargetDead())
        {
            FaceTarget(15f);
            revElapsed += Time.deltaTime;
            yield return null;
        }

        if (isDead || IsTargetDead())
        {
            attacking = false;
            if (!isDead) movement.SetMovementPaused(false);
            yield break;
        }

        // Calculate velocity for parabolic arc
        Vector3 targetPos = CurrentTargetPosition;
        Vector3 throwPos = firePoint.position;
        
        // Basic physics calculation for the throw
        float displacementY = targetPos.y - throwPos.y;
        Vector3 displacementXZ = new Vector3(targetPos.x - throwPos.x, 0, targetPos.z - throwPos.z);
        
        // Ensure arc height is higher than the target
        float h = Mathf.Max(attackData.arcHeight, displacementY + 1f);
        float g = Mathf.Abs(attackData.gravity);
        if (g < 0.001f) g = 0.001f;

        float time = Mathf.Sqrt(2f * h / g) + Mathf.Sqrt(2f * (h - displacementY) / g);
        
        Vector3 velocityY = Vector3.up * Mathf.Sqrt(2f * g * h);
        Vector3 velocityXZ = displacementXZ / time;
        if (float.IsNaN(velocityXZ.x)) velocityXZ = Vector3.zero;

        Vector3 launchVelocity = velocityXZ + velocityY;

        // Instantiate and throw
        BoulderProjectile boulder = Instantiate(boulderPrefab, throwPos, Quaternion.identity);
        boulder.Launch(launchVelocity, damageOverride ?? attackData.damage, attackData.damageType, attackData.explosionRadius, ownerDamageable, attackData.gravity);

        if (!isDead)
        {
            movement.SetMovementPaused(false);
        }

        lastAttackTime = Time.time;
        attacking = false;
    }

    private void FaceTarget(float turnSpeed)
    {
        Vector3 dir = CurrentTargetPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }
}
