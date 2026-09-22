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
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Aim & Animation")]
    [SerializeField] private float windUpDuration = 0.45f;

    private float nextFireTime = 0f;
    private bool isFiring = false;
    private Transform armTransform;
    private Quaternion armInitialLocalRot;

    protected override void Awake()
    {
        defenseType = FortDefenseType.Catapult;
        supplyPerAction = 3;
        rotateToTarget = true;
        rotationSpeed = 120f;
        base.Awake();

        if (enemyLayers == ~0 || enemyLayers == 0)
        {
            int mask = LayerMask.GetMask("Enemy");
            enemyLayers = mask != 0 ? mask : (1 << 7);
        }

        CacheArm();
    }

    private void CacheArm()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("Arm"))
            {
                armTransform = child;
                armInitialLocalRot = child.localRotation;
                break;
            }
        }
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
        if (IsDepleted || currentSupply <= 0) return;

        Vector3 targetPoint = FindTargetPoint();
        if (targetPoint != Vector3.zero)
        {
            RotateTowardsTarget(targetPoint);

            if (!isFiring && Time.time >= nextFireTime)
            {
                StartCoroutine(FireSequenceRoutine(targetPoint));
            }
        }
    }

    private System.Collections.IEnumerator FireSequenceRoutine(Vector3 targetPos)
    {
        isFiring = true;

        // 1. Wind-up anticipation: Crank arm backward
        float elapsed = 0f;
        while (elapsed < windUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / windUpDuration);

            // Rotate toward target while winding up
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * 1.5f * Time.deltaTime);
            }

            if (armTransform != null)
            {
                // Crank backward ~35 degrees
                armTransform.localRotation = armInitialLocalRot * Quaternion.Euler(-35f * t, 0f, 0f);
            }

            yield return null;
        }

        // 2. Rapid forward swing and launch
        float swingDuration = 0.14f;
        elapsed = 0f;
        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swingDuration);
            if (armTransform != null)
            {
                // Swing vigorously forward to +40 degrees
                float angle = Mathf.Lerp(-35f, 40f, t * t);
                armTransform.localRotation = armInitialLocalRot * Quaternion.Euler(angle, 0f, 0f);
            }
            yield return null;
        }

        // Fire at the apex of swing
        if (ConsumeSupply())
        {
            ExecuteFire(targetPos);
        }

        // 3. Smooth recovery back to resting pose
        float recoverDuration = 0.28f;
        elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoverDuration);
            if (armTransform != null)
            {
                float angle = Mathf.Lerp(40f, 0f, t);
                armTransform.localRotation = armInitialLocalRot * Quaternion.Euler(angle, 0f, 0f);
            }
            yield return null;
        }

        if (armTransform != null) armTransform.localRotation = armInitialLocalRot;

        isFiring = false;
        nextFireTime = Time.time + fireInterval;
    }

    private void ExecuteFire(Vector3 targetPos)
    {

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
        int mask = enemyLayers.value;
        if (mask == ~0 || mask == 0)
        {
            mask = LayerMask.GetMask("Enemy");
            if (mask == 0) mask = 1 << 7;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, maxRange, mask, QueryTriggerInteraction.Collide);
        List<Health> enemiesInRange = new List<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead || enemiesInRange.Contains(h)) continue;
            if (h.ImmuneToPlayerDamage) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;
            if (h.GetComponentInParent<Gate>() != null) continue;

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
