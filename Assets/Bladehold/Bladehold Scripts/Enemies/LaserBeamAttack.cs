using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Mechanical Golem's chest laser. Telegraphs then sweeps a continuous boxcast beam,
///     applying tick damage to targets caught inside.
/// </summary>
public class LaserBeamAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private LaserBeamAttackSO attackData;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject laserPrefab;

    [SerializeField] private string revTrigger = "Attack";

    private const int MaxOverlapResults = 32;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxOverlapResults];
    private readonly Dictionary<IDamageable, float> lastHitTimes = new Dictionary<IDamageable, float>();

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
    private GameObject activeLaser;

    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<AIMovement>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (animator == null || health == null || movement == null || agent == null || attackData == null || firePoint == null)
        {
            Debug.LogError("LaserBeamAttack is missing required components or references.");
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
        if (activeLaser != null) Destroy(activeLaser);
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
            FacePlayer(360f); // Fast turn during rev
            revElapsed += Time.deltaTime;
            yield return null;
        }

        if (isDead || playerDead)
        {
            attacking = false;
            if (!isDead) movement.SetMovementPaused(false);
            yield break;
        }

        // Add a random offset so the beam "chases" the player instead of starting perfectly glued.
        float offsetAngle = (UnityEngine.Random.value > 0.5f ? 1f : -1f) * 35f;
        transform.rotation *= Quaternion.Euler(0, offsetAngle, 0);

        // Firing beam
        if (laserPrefab != null)
        {
            activeLaser = Instantiate(laserPrefab, firePoint);
            activeLaser.transform.localPosition = Vector3.zero;
            activeLaser.transform.localRotation = Quaternion.identity;
        }
        float elapsed = 0f;
        while (elapsed < attackData.beamDuration && !isDead && !playerDead)
        {
            // Slow tracking during beam
            FacePlayer(attackData.sweepTurnRate);

            Vector3 direction = firePoint.forward;
            Vector3 startPos = firePoint.position;
            
            ApplyBeamDamage(startPos, direction);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (activeLaser != null)
        {
            Destroy(activeLaser);
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

        // Tilt the firePoint up/down to aim at the player's chest (approx 1m up)
        Vector3 chestTarget = player.position + Vector3.up * 1.0f;
        Vector3 fireDir = chestTarget - firePoint.position;
        
        // We only want pitch (up/down), so we convert direction to the local space of the Golem body
        Vector3 localFireDir = transform.InverseTransformDirection(fireDir);
        localFireDir.x = 0; // Ignore left/right to let the body handle yaw
        
        if (localFireDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetLocalRot = Quaternion.LookRotation(localFireDir.normalized);
            // Snap the pitch so it always correctly aims at the chest height
            firePoint.localRotation = targetLocalRot;
        }
    }

    private void ApplyBeamDamage(Vector3 startPos, Vector3 direction)
    {
        Vector3 halfExtents = new Vector3(attackData.beamWidth * 0.5f, attackData.beamWidth * 0.5f, 0.1f);
        int count = Physics.BoxCastNonAlloc(startPos, halfExtents, direction, hitBuffer, firePoint.rotation, attackData.beamLength);

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i].collider;
            if (!col.TryGetComponent(out IDamageable damageable))
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;

            if (lastHitTimes.TryGetValue(damageable, out float lastHit) && Time.time - lastHit < attackData.tickRate)
            {
                continue;
            }

            lastHitTimes[damageable] = Time.time;
            damageable.ReceiveDamage(new Damage
            {
                value = damageOverride ?? attackData.damage,
                type = attackData.damageType,
                sourcePosition = transform.position,
                source = ownerDamageable,
                unparryable = true,
            });
        }
    }
}
