using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Net Thrower: Launches heavy nets that immobilize (root) groups of enemies in place.
/// </summary>
public class NetThrowerDefense : DefenseStructure
{
    [Header("Net Thrower Config")]
    [SerializeField] private float range = 18f;
    [SerializeField] private float fireInterval = 4.5f;
    [SerializeField] private float netRadius = 4.0f;
    [SerializeField] private float rootDuration = 3.5f;
    [SerializeField] private GameObject netProjectilePrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private GameObject netVfxPrefab;
    [SerializeField] private AudioClip fireSfx;
    [SerializeField] private AudioClip netImpactSfx;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Aim & Animation")]
    [SerializeField] private float windUpDuration = 0.35f;

    private float nextFireTime = 0f;
    private bool isFiring = false;
    private Transform barrelTransform;
    private Quaternion barrelInitialLocalRot;
    private Vector3 barrelInitialLocalPos;

    protected override void Awake()
    {
        defenseType = FortDefenseType.NetThrower;
        supplyPerAction = 3;
        rotateToTarget = true;
        rotationSpeed = 140f;
        base.Awake();

        if (enemyLayers == ~0 || enemyLayers == 0)
        {
            int mask = LayerMask.GetMask("Enemy");
            enemyLayers = mask != 0 ? mask : (1 << 7);
        }

        CacheBarrel();
    }

    private void CacheBarrel()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("Barrel"))
            {
                barrelTransform = child;
                barrelInitialLocalRot = child.localRotation;
                barrelInitialLocalPos = child.localPosition;
                break;
            }
        }
    }

    protected override void ApplyLevelStats(int level)
    {
        switch (level)
        {
            case 1:
                fireInterval = 4.5f;
                rootDuration = 3.5f;
                netRadius = 4.0f;
                break;
            case 2:
                fireInterval = 3.5f;
                rootDuration = 4.2f;
                netRadius = 5.0f;
                break;
            case 3:
                fireInterval = 2.5f;
                rootDuration = 5.0f;
                netRadius = 6.0f;
                break;
        }
    }

    private void Update()
    {
        if (IsDepleted || currentSupply <= 0) return;

        Health target = FindUnrootedEnemy();
        if (target != null)
        {
            RotateTowardsTarget(target.transform.position);

            if (!isFiring && Time.time >= nextFireTime)
            {
                StartCoroutine(FireSequenceRoutine(target));
            }
        }
    }

    private System.Collections.IEnumerator FireSequenceRoutine(Health target)
    {
        isFiring = true;

        // 1. Wind-up anticipation: barrel compresses back / charges
        float elapsed = 0f;
        while (elapsed < windUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / windUpDuration);

            // Keep facing moving target while winding up
            if (target != null && !target.IsDead)
            {
                Vector3 toTarget = target.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * 1.5f * Time.deltaTime);
                }
            }

            if (barrelTransform != null)
            {
                barrelTransform.localRotation = barrelInitialLocalRot * Quaternion.Euler(20f * t, 0f, 0f);
                barrelTransform.localPosition = barrelInitialLocalPos - Vector3.up * (0.12f * t);
            }

            yield return null;
        }

        // 2. Launch net at target position with recoil kick
        if (target != null && !target.IsDead && ConsumeSupply())
        {
            if (barrelTransform != null)
            {
                barrelTransform.localRotation = barrelInitialLocalRot * Quaternion.Euler(-25f, 0f, 0f);
                barrelTransform.localPosition = barrelInitialLocalPos + Vector3.up * 0.08f;
            }

            LaunchNetAt(target.transform.position);
        }

        // 3. Settle back to resting pose
        float recoverDuration = 0.22f;
        elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoverDuration);
            if (barrelTransform != null)
            {
                barrelTransform.localRotation = Quaternion.Slerp(barrelInitialLocalRot * Quaternion.Euler(-25f, 0f, 0f), barrelInitialLocalRot, t);
                barrelTransform.localPosition = Vector3.Lerp(barrelInitialLocalPos + Vector3.up * 0.08f, barrelInitialLocalPos, t);
            }
            yield return null;
        }

        if (barrelTransform != null)
        {
            barrelTransform.localRotation = barrelInitialLocalRot;
            barrelTransform.localPosition = barrelInitialLocalPos;
        }

        isFiring = false;
        nextFireTime = Time.time + fireInterval;
    }

    private void LaunchNetAt(Vector3 targetPos)
    {
        Vector3 spawnPos = launchPoint != null ? launchPoint.position
            : (barrelTransform != null ? barrelTransform.position + barrelTransform.forward * 0.75f : transform.position + Vector3.up * 1.5f);

        if (fireSfx != null)
        {
            AudioSource.PlayClipAtPoint(fireSfx, spawnPos);
        }

        if (netProjectilePrefab != null)
        {
            GameObject projObj = Instantiate(netProjectilePrefab, spawnPos, Quaternion.identity);
            NetProjectile proj = projObj.GetComponent<NetProjectile>();
            if (proj != null)
            {
                proj.Launch(spawnPos, targetPos, 0.65f, (hitPos) => ApplyNetImpact(hitPos));
                return;
            }
        }

        // Fallback instant impact if projectile prefab not present
        ApplyNetImpact(targetPos);
    }

    public void ApplyNetImpact(Vector3 targetPos)
    {
        if (netImpactSfx != null)
        {
            AudioSource.PlayClipAtPoint(netImpactSfx, targetPos);
        }

        if (netVfxPrefab != null)
        {
            Instantiate(netVfxPrefab, targetPos, Quaternion.identity);
        }

        // Apply root in radius
        Collider[] hits = Physics.OverlapSphere(targetPos, netRadius);
        HashSet<Health> processed = new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead || processed.Contains(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            processed.Add(h);

            NetRootStatus root = NetRootStatus.GetOrAdd(h);
            if (root != null)
            {
                root.ApplyRoot(rootDuration);
            }

            // Also deliver minor blunt impact damage
            Damage dmg = new Damage
            {
                value = 10f * currentLevel,
                type = DamageType.blunt,
                isPlayerDamage = true,
                sourcePosition = targetPos,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            };
            h.ReceiveDamage(dmg);
        }
    }

    private Health FindUnrootedEnemy()
    {
        int mask = enemyLayers.value;
        if (mask == ~0 || mask == 0)
        {
            mask = LayerMask.GetMask("Enemy");
            if (mask == 0) mask = 1 << 7;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, range, mask, QueryTriggerInteraction.Collide);
        Health bestTarget = null;
        float closestDistSqr = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            if (h.ImmuneToPlayerDamage) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;
            if (h.GetComponentInParent<Gate>() != null) continue;

            NetRootStatus existingRoot = h.GetComponent<NetRootStatus>();
            if (existingRoot != null && existingRoot.IsRooted) continue; // Skip already rooted enemies

            float distSqr = (h.transform.position - transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                bestTarget = h;
            }
        }

        return bestTarget;
    }
}
