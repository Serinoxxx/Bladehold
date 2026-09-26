using MoreMountains.Feedbacks;
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
    [Tooltip("Played at the fire point on each shot.")]
    [SerializeField] private MMF_Player fireFeedback;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Aim & Animation")]
    [SerializeField] private float windUpDuration = 0.35f;

    private float nextFireTime = 0f;
    private bool isFiring = false;
    private Transform stringTransform;
    private Transform loaderTransform;
    private Vector3 stringInitialLocalPos;
    private Vector3 loaderInitialLocalPos;

    protected override void ValidateFeedbackReferences()
    {
        base.ValidateFeedbackReferences();
        if (fireFeedback == null) Debug.LogError($"{name}: BallistaDefense.fireFeedback is not assigned.", this);
    }

    protected override void Awake()
    {
        defenseType = FortDefenseType.Ballista;
        supplyPerAction = 4;
        rotateToTarget = true;
        rotationSpeed = 160f;
        base.Awake();

        if (enemyLayers == ~0 || enemyLayers == 0)
        {
            int mask = LayerMask.GetMask("Enemy");
            enemyLayers = mask != 0 ? mask : (1 << 7);
        }

        CacheMovingParts();
    }

    private void CacheMovingParts()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("String"))
            {
                stringTransform = child;
                stringInitialLocalPos = child.localPosition;
            }
            else if (child.name.Contains("Loader"))
            {
                loaderTransform = child;
                loaderInitialLocalPos = child.localPosition;
            }
        }
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
        if (IsDepleted || currentSupply <= 0) return;

        Health target = FindPriorityEnemy();
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

        // 1. Wind-up anticipation (drawing the bowstring / loader backward)
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

            // Pull string and loader backwards
            if (stringTransform != null)
            {
                stringTransform.localPosition = stringInitialLocalPos - Vector3.forward * (0.35f * t);
            }
            if (loaderTransform != null)
            {
                loaderTransform.localPosition = loaderInitialLocalPos - Vector3.forward * (0.35f * t);
            }

            yield return null;
        }

        // 2. Fire projectile if target still valid and has supply
        if (target != null && !target.IsDead && ConsumeSupply())
        {
            ExecuteFire(target);
        }

        // 3. Punchy snap forward and return to rest
        if (stringTransform != null) stringTransform.localPosition = stringInitialLocalPos;
        if (loaderTransform != null) loaderTransform.localPosition = loaderInitialLocalPos;

        yield return new WaitForSeconds(0.15f);

        isFiring = false;
        nextFireTime = Time.time + fireInterval;
    }

    private void ExecuteFire(Health target)
    {

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

        if (fireFeedback != null)
        {
            fireFeedback.PlayFeedbacks(spawnPos);
        }
    }

    private Health FindPriorityEnemy()
    {
        int mask = enemyLayers.value;
        if (mask == ~0 || mask == 0)
        {
            mask = LayerMask.GetMask("Enemy");
            if (mask == 0) mask = 1 << 7;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, range, mask, QueryTriggerInteraction.Collide);
        Health highestHpTarget = null;
        float highestHp = -1f;

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            if (h.ImmuneToPlayerDamage) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;
            if (h.GetComponentInParent<Gate>() != null) continue;

            if (h.CurrentHealth > highestHp)
            {
                highestHp = h.CurrentHealth;
                highestHpTarget = h;
            }
        }

        return highestHpTarget;
    }
}
