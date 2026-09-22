using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Catapult: Lobs fiery projectiles dealing splash area fire damage.
/// </summary>
public class CatapultDefense : DefenseStructure
{
    [Header("Catapult Config")]
    [SerializeField] private float minRange = 6f;
    [SerializeField] private float maxRange = 24f;
    [SerializeField] private float fireInterval = 3.8f;
    [SerializeField] private float splashDamage = 45f;
    [SerializeField] private float splashRadius = 4.5f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private AudioClip fireSfx;

    private float nextFireTime = 0f;

    protected override void Awake()
    {
        defenseType = FortDefenseType.Catapult;
        supplyPerAction = 3;
        base.Awake();
    }

    protected override void ApplyLevelStats(int level)
    {
        switch (level)
        {
            case 1:
                splashDamage = 45f;
                fireInterval = 3.8f;
                splashRadius = 4.5f;
                break;
            case 2:
                splashDamage = 80f;
                fireInterval = 3.0f;
                splashRadius = 5.5f;
                break;
            case 3:
                splashDamage = 130f;
                fireInterval = 2.2f;
                splashRadius = 6.5f;
                break;
        }
    }

    private void Update()
    {
        if (Time.time < nextFireTime) return;

        Vector3 targetPoint = FindTargetPoint();
        if (targetPoint != Vector3.zero)
        {
            FireCatapult(targetPoint);
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void FireCatapult(Vector3 targetPos)
    {
        if (!ConsumeSupply()) return;

        Vector3 spawnPos = launchPoint != null ? launchPoint.position : transform.position + Vector3.up * 2f;

        bool hasIce = Player.Instance != null && Player.Instance.Stats != null && Player.Instance.Stats.GetValue(StatType.TowerSlipperyGround) > 0f;
        bool hasLightning = Player.Instance != null && Player.Instance.Stats != null && Player.Instance.Stats.GetValue(StatType.TowerStormCloud) > 0f;
        bool hasFire = Player.Instance != null && Player.Instance.Stats != null && Player.Instance.Stats.GetValue(StatType.TowerRollingFireball) > 0f;

        float effectiveSplashDamage = splashDamage;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            effectiveSplashDamage *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
        }

        if (projectilePrefab != null)
        {
            GameObject projObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            CatapultProjectile proj = projObj.GetComponent<CatapultProjectile>();
            if (proj != null)
            {
                proj.SetElementalUpgrades(hasIce, hasLightning, hasFire);
                proj.Launch(spawnPos, targetPos, effectiveSplashDamage, 1.2f, splashRadius);
            }
        }
        else
        {
            // Fallback direct splash
            Collider[] hits = Physics.OverlapSphere(targetPos, splashRadius);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                Health h = hit.GetComponentInParent<Health>();
                if (h != null && !h.IsDead && (Player.Instance == null || h.transform.root != Player.Instance.transform.root))
                {
                    Damage dmg = new Damage
                    {
                        value = effectiveSplashDamage,
                        type = DamageType.elemental,
                        elementId = "Fire",
                        isPlayerDamage = true,
                        sourcePosition = spawnPos,
                        source = Player.Instance != null ? Player.Instance.Damageable : null
                    };
                    h.ReceiveDamage(dmg);
                    EnemyStatusManager.GetOrAdd(h)?.ApplyStatus("Fire");
                }
            }

            if (hasIce) SlipperyIceZone.Spawn(targetPos, splashRadius * 1.25f, 10f);
            if (hasLightning) CatapultStormCloud.Spawn(targetPos, splashRadius * 1.35f, 8f, effectiveSplashDamage * 0.45f);
            if (hasFire)
            {
                Vector3 rollDir = (targetPos - spawnPos);
                rollDir.y = 0f;
                RollingFireball.Spawn(targetPos, rollDir.normalized, 9.5f, effectiveSplashDamage * 0.8f, 10f);
            }
        }

        if (fireSfx != null)
        {
            AudioSource.PlayClipAtPoint(fireSfx, spawnPos);
        }
    }

    private Vector3 FindTargetPoint()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, maxRange);
        List<Health> enemiesInRange = new List<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead || enemiesInRange.Contains(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            float dist = Vector3.Distance(transform.position, h.transform.position);
            if (dist >= minRange && dist <= maxRange)
            {
                enemiesInRange.Add(h);
            }
        }

        if (enemiesInRange.Count == 0) return Vector3.zero;

        // Target the center of mass / enemy with most neighbours
        Health bestEnemy = enemiesInRange[0];
        int bestNeighbors = -1;

        foreach (Health e in enemiesInRange)
        {
            int neighbors = 0;
            foreach (Health other in enemiesInRange)
            {
                if (e != other && Vector3.Distance(e.transform.position, other.transform.position) <= splashRadius)
                {
                    neighbors++;
                }
            }

            if (neighbors > bestNeighbors)
            {
                bestNeighbors = neighbors;
                bestEnemy = e;
            }
        }

        return bestEnemy.transform.position;
    }
}
