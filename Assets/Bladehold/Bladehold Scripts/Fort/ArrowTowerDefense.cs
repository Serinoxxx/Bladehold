using UnityEngine;

/// <summary>
///     Arrow Tower: Rapidly fires single-target arrows at the closest enemy.
/// </summary>
public class ArrowTowerDefense : DefenseStructure
{
    [Header("Combat Config")]
    [SerializeField] private float range = 18f;
    [SerializeField] private float fireInterval = 0.8f;
    [SerializeField] private float arrowSpeed = 26f;
    [SerializeField] private float arrowDamage = 18f;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private AudioClip fireSfx;

    private float nextFireTime = 0f;

    protected override void Awake()
    {
        defenseType = FortDefenseType.ArrowSlits;
        supplyPerAction = 1;
        base.Awake();
    }

    protected override void ApplyLevelStats(int level)
    {
        switch (level)
        {
            case 1:
                arrowDamage = 18f;
                fireInterval = 0.8f;
                range = 18f;
                break;
            case 2:
                arrowDamage = 32f;
                fireInterval = 0.65f;
                range = 20f;
                break;
            case 3:
                arrowDamage = 50f;
                fireInterval = 0.5f;
                range = 22f;
                break;
        }
    }

    private void Update()
    {
        if (Time.time < nextFireTime) return;

        Health target = FindClosestEnemy();
        if (target != null)
        {
            ShootAt(target);
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void ShootAt(Health target)
    {
        if (!ConsumeSupply()) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 2.5f;
        Vector3 targetPos = target.transform.position + Vector3.up * 0.8f;
        Vector3 dir = (targetPos - spawnPos).normalized;

        if (arrowPrefab != null)
        {
            GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(dir));
            FortArrowProjectile proj = arrowObj.GetComponent<FortArrowProjectile>();
            if (proj != null)
            {
                proj.Init(dir, arrowSpeed, arrowDamage);
            }
        }
        else
        {
            // Fallback direct ray / damage
            Damage dmg = new Damage
            {
                value = arrowDamage,
                type = DamageType.sharp,
                isPlayerDamage = true,
                sourcePosition = spawnPos,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            };
            target.ReceiveDamage(dmg);
        }

        if (fireSfx != null)
        {
            AudioSource.PlayClipAtPoint(fireSfx, spawnPos);
        }
    }

    private Health FindClosestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        Health bestTarget = null;
        float bestDistSqr = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            float distSqr = (h.transform.position - transform.position).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestTarget = h;
            }
        }

        return bestTarget;
    }
}
