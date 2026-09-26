using MoreMountains.Feedbacks;
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
    [Tooltip("Played at the fire point on each shot.")]
    [SerializeField] private MMF_Player fireFeedback;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Prediction & Intercept")]
    [Tooltip("Predicts enemy movement and shoots ahead to collide with moving targets.")]
    [SerializeField] private bool leadTarget = true;
    [Tooltip("Maximum future prediction time in seconds to avoid over-leading erratic targets.")]
    [SerializeField] private float maxPredictionTime = 2.0f;

    private float nextFireTime = 0f;

    protected override void ValidateFeedbackReferences()
    {
        base.ValidateFeedbackReferences();
        if (fireFeedback == null) Debug.LogError($"{name}: ArrowTowerDefense.fireFeedback is not assigned.", this);
    }

    protected override void Awake()
    {
        defenseType = FortDefenseType.ArrowSlits;
        supplyPerAction = 1;
        base.Awake();

        if (enemyLayers == ~0 || enemyLayers == 0)
        {
            int mask = LayerMask.GetMask("Enemy");
            enemyLayers = mask != 0 ? mask : (1 << 7);
        }
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

    public float GetEffectiveFireInterval()
    {
        float interval = fireInterval;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            float rateBonus = Player.Instance.Stats.GetValue(StatType.TowerArrowFireRateBonus);
            if (Player.Instance.Stats.GetValue(StatType.TowerLightningArrows) > 0f && rateBonus <= 0f)
            {
                rateBonus = 0.50f;
            }
            if (rateBonus > 0f)
            {
                interval /= (1f + rateBonus);
            }
        }
        return Mathf.Max(0.12f, interval);
    }

    public float GetEffectiveArrowDamage()
    {
        float dmg = arrowDamage;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            float dmgBonus = Player.Instance.Stats.GetValue(StatType.TowerArrowDamageBonus);
            if (Player.Instance.Stats.GetValue(StatType.TowerFireArrows) > 0f && dmgBonus <= 0f)
            {
                dmgBonus = 0.40f;
            }
            if (dmgBonus > 0f)
            {
                dmg *= (1f + dmgBonus);
            }
            dmg *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
        }
        return dmg;
    }

    private void Update()
    {
        if (IsDepleted || currentSupply <= 0) return;

        Health target = FindClosestEnemy();
        if (target != null)
        {
            RotateTowardsTarget(target.transform.position);

            if (Time.time >= nextFireTime)
            {
                ShootAt(target);
                nextFireTime = Time.time + GetEffectiveFireInterval();
            }
        }
    }

    private void ShootAt(Health target)
    {
        if (!ConsumeSupply()) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 2.5f;
        Vector3 targetCenter = TargetLead.GetTargetAimPosition(target);
        Vector3 aimPoint = targetCenter;

        if (leadTarget)
        {
            Vector3 targetVelocity = TargetLead.GetTargetVelocity(target);
            if (TargetLead.TryCalculateIntercept(spawnPos, arrowSpeed, targetCenter, targetVelocity, maxPredictionTime, out Vector3 predictedPoint))
            {
                aimPoint = predictedPoint;
            }
        }

        aimPoint.y = Mathf.Max(aimPoint.y, target.transform.position.y + 0.3f);
        Vector3 dir = (aimPoint - spawnPos).normalized;
        if (dir == Vector3.zero) dir = transform.forward;

        bool isFrost = Player.Instance != null && Player.Instance.Stats != null && Player.Instance.Stats.GetValue(StatType.TowerFrostArrows) > 0f;
        bool isLightning = Player.Instance != null && Player.Instance.Stats != null && Player.Instance.Stats.GetValue(StatType.TowerLightningArrows) > 0f;
        bool isFire = Player.Instance != null && Player.Instance.Stats != null && Player.Instance.Stats.GetValue(StatType.TowerFireArrows) > 0f;

        float finalDamage = GetEffectiveArrowDamage();

        if (arrowPrefab != null)
        {
            GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(dir));
            FortArrowProjectile proj = arrowObj.GetComponent<FortArrowProjectile>();
            if (proj != null)
            {
                proj.Init(dir, arrowSpeed, finalDamage);
                proj.SetElementalInfusion(isFrost, isLightning, isFire);
            }
        }
        else
        {
            // Fallback direct ray / damage
            Damage dmg = new Damage
            {
                value = finalDamage,
                type = (isFrost || isLightning || isFire) ? DamageType.elemental : DamageType.sharp,
                elementId = isFire ? "Fire" : (isLightning ? "Lightning" : (isFrost ? "Ice" : "")),
                isPlayerDamage = true,
                sourcePosition = spawnPos,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            };
            target.ReceiveDamage(dmg);

            if (isFire) EnemyStatusManager.GetOrAdd(target)?.ApplyStatus("Fire");
            if (isFrost) EnemyStatusManager.GetOrAdd(target)?.ApplyStatus("Ice", 0.5f);
            if (isLightning) EnemyStatusManager.GetOrAdd(target)?.ApplyStatus("Lightning");
        }

        if (fireFeedback != null)
        {
            fireFeedback.PlayFeedbacks(spawnPos);
        }
    }

    private Health FindClosestEnemy()
    {
        int mask = enemyLayers.value;
        if (mask == ~0 || mask == 0)
        {
            mask = LayerMask.GetMask("Enemy");
            if (mask == 0) mask = 1 << 7;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, range, mask, QueryTriggerInteraction.Collide);
        Health bestTarget = null;
        float bestDistSqr = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            if (h.ImmuneToPlayerDamage) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;
            if (h.GetComponentInParent<Gate>() != null) continue;

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
