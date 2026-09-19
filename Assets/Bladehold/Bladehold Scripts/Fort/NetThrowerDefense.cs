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
    [SerializeField] private GameObject netVfxPrefab;
    [SerializeField] private AudioClip fireSfx;
    [SerializeField] private AudioClip netImpactSfx;

    private float nextFireTime = 0f;

    protected override void Awake()
    {
        defenseType = FortDefenseType.NetThrower;
        supplyPerAction = 3;
        base.Awake();
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
        if (Time.time < nextFireTime) return;

        Health target = FindUnrootedEnemy();
        if (target != null)
        {
            LaunchNetAt(target.transform.position);
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void LaunchNetAt(Vector3 targetPos)
    {
        if (!ConsumeSupply()) return;

        if (fireSfx != null)
        {
            AudioSource.PlayClipAtPoint(fireSfx, transform.position);
        }

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
        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        Health bestTarget = null;
        float closestDistSqr = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

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
