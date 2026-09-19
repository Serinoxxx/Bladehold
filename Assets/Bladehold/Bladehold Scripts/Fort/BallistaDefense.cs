using UnityEngine;

/// <summary>
///     Ballista: High damage piercing bolt that punches through lines of enemies with knockback.
/// </summary>
public class BallistaDefense : DefenseStructure
{
    [Header("Ballista Config")]
    [SerializeField] private float range = 22f;
    [SerializeField] private float fireInterval = 3.0f;
    [SerializeField] private float boltSpeed = 35f;
    [SerializeField] private float boltDamage = 110f;
    [SerializeField] private int pierceCount = 3;
    [SerializeField] private GameObject boltPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private AudioClip fireSfx;

    private float nextFireTime = 0f;

    protected override void Awake()
    {
        defenseType = FortDefenseType.Ballista;
        supplyPerAction = 4;
        base.Awake();
    }

    protected override void ApplyLevelStats(int level)
    {
        switch (level)
        {
            case 1:
                boltDamage = 110f;
                fireInterval = 3.0f;
                pierceCount = 2;
                break;
            case 2:
                boltDamage = 190f;
                fireInterval = 2.3f;
                pierceCount = 3;
                break;
            case 3:
                boltDamage = 300f;
                fireInterval = 1.6f;
                pierceCount = 4;
                break;
        }
    }

    private void Update()
    {
        if (Time.time < nextFireTime) return;

        Health target = FindPriorityEnemy();
        if (target != null)
        {
            FireBallista(target);
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void FireBallista(Health target)
    {
        if (!ConsumeSupply()) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = target.transform.position + Vector3.up * 0.8f;
        Vector3 dir = (targetPos - spawnPos).normalized;

        if (boltPrefab != null)
        {
            GameObject boltObj = Instantiate(boltPrefab, spawnPos, Quaternion.LookRotation(dir));
            BallistaBoltProjectile proj = boltObj.GetComponent<BallistaBoltProjectile>();
            if (proj != null)
            {
                proj.Init(dir, boltSpeed, boltDamage, pierceCount);
            }
        }
        else
        {
            // Fallback direct raycast piercing
            RaycastHit[] hits = Physics.SphereCastAll(spawnPos, 0.5f, dir, range);
            int pierced = 0;
            foreach (var hit in hits)
            {
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead && (Player.Instance == null || h.transform.root != Player.Instance.transform.root))
                {
                    Damage dmg = new Damage
                    {
                        value = boltDamage,
                        type = DamageType.sharp,
                        isPlayerDamage = true,
                        sourcePosition = spawnPos,
                        source = Player.Instance != null ? Player.Instance.Damageable : null
                    };
                    h.ReceiveDamage(dmg);
                    pierced++;
                    if (pierced >= pierceCount) break;
                }
            }
        }

        if (fireSfx != null)
        {
            AudioSource.PlayClipAtPoint(fireSfx, spawnPos);
        }
    }

    private Health FindPriorityEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        Health highestHpTarget = null;
        float highestHp = -1f;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            if (h.CurrentHealth > highestHp)
            {
                highestHp = h.CurrentHealth;
                highestHpTarget = h;
            }
        }

        return highestHpTarget;
    }
}
