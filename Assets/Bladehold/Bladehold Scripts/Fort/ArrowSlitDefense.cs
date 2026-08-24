using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Fort defense for wall arrow slits and battlements.
///     Scans for nearby enemies within its cone / range, leads target, and periodically shoots high-velocity arrows.
///     Scales damage, fire rate, and multishot volleys with upgrade levels.
/// </summary>
public class ArrowSlitDefense : FortDefense
{
    [Header("Targeting & Range")]
    [SerializeField] private float range = 35f;
    [SerializeField] private float maxTargetAngle = 150f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Firing Specs")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private float baseDamage = 18f;
    [SerializeField] private float baseFireInterval = 1.6f;
    [SerializeField] private float arrowSpeed = 35f;
    [SerializeField] private GameObject arrowPrefab;

    [Header("Audio & Feedbacks")]
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private MMF_Player shootFeedback;

    private float fireTimer;
    private readonly Collider[] targetBuffer = new Collider[128];

    private void Awake()
    {
        defenseType = FortDefenseType.ArrowSlits;
        if (firePoint == null)
        {
            firePoint = transform;
        }

        if (enemyLayers == ~0 || enemyLayers == 0)
        {
            int mask = LayerMask.GetMask("Enemy");
            enemyLayers = mask != 0 ? mask : (1 << 7);
        }
    }

    private void Update()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            Health target = FindBestTarget();
            if (target != null)
            {
                ShootAtTarget(target);
                fireTimer = GetEffectiveFireInterval();
            }
            else
            {
                // Retry in short interval when no target found
                fireTimer = 0.25f;
            }
        }
    }

    private float GetEffectiveDamage()
    {
        // Level 1: base (18), Level 2: +10, Level 3: +20, Level 4: +35
        return baseDamage + (currentLevel - 1) * 12f;
    }

    private float GetEffectiveFireInterval()
    {
        // Level 1: 1.6s, Level 2: 1.3s, Level 3: 1.0s, Level 4: 0.75s
        return Mathf.Max(0.5f, baseFireInterval - (currentLevel - 1) * 0.25f);
    }

    private int GetBurstCount()
    {
        return currentLevel >= 4 ? 2 : 1;
    }

    private Health FindBestTarget()
    {
        Transform fp = (firePoint != null && firePoint) ? firePoint : transform;
        Vector3 origin = fp.position;

        int mask = enemyLayers.value;
        if (mask == ~0 || mask == 0)
        {
            mask = LayerMask.GetMask("Enemy");
            if (mask == 0) mask = 1 << 7;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(origin, range, targetBuffer, mask, QueryTriggerInteraction.Collide);

        Health bestTarget = null;
        float closestDistSqr = float.MaxValue;

        Vector3 forwardXZ = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = targetBuffer[i];
            if (col == null) continue;

            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;

            // Check if player or ally
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            Vector3 toTarget = h.transform.position - origin;
            float distSqr = toTarget.sqrMagnitude;

            // Horizontal (yaw) angle check relative to wall slit forward facing
            Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            if (toTargetXZ != Vector3.zero && forwardXZ != Vector3.zero)
            {
                if (Vector3.Angle(forwardXZ, toTargetXZ) > maxTargetAngle * 0.5f) continue;
            }

            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                bestTarget = h;
            }
        }

        return bestTarget;
    }

    private void ShootAtTarget(Health target)
    {
        int burst = GetBurstCount();
        if (burst > 1)
        {
            StartCoroutine(BurstRoutine(target, burst));
        }
        else
        {
            FireSingleArrow(target);
        }
    }

    private IEnumerator BurstRoutine(Health target, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (target != null && !target.IsDead)
            {
                FireSingleArrow(target);
            }
            yield return new WaitForSeconds(0.15f);
        }
    }

    private void FireSingleArrow(Health target)
    {
        if (target == null) return;

        Transform fp = (firePoint != null && firePoint) ? firePoint : transform;
        Vector3 startPos = fp.position + fp.forward * 0.6f;
        Vector3 targetPos = target.transform.position + Vector3.up * 1.0f; // Aim at chest height
        Vector3 dir = (targetPos - startPos).normalized;

        if (shootFeedback != null)
        {
            shootFeedback.PlayFeedbacks(startPos);
        }
        else if (shootSound != null)
        {
            AudioSource.PlayClipAtPoint(shootSound, startPos, 0.8f);
        }

        // Spawn visual arrow projectile
        if (arrowPrefab != null)
        {
            GameObject arrowObj = Instantiate(arrowPrefab, startPos, Quaternion.LookRotation(dir));
            FortArrowProjectile proj = arrowObj.GetComponent<FortArrowProjectile>();
            if (proj == null)
            {
                proj = arrowObj.AddComponent<FortArrowProjectile>();
            }
            proj.Init(dir, arrowSpeed, GetEffectiveDamage(), hitSound);
        }
        else
        {
            // Instant linecast hit with delay if no visual prefab assigned
            StartCoroutine(DelayedHitRoutine(target, startPos, targetPos, dir));
        }
    }

    private IEnumerator DelayedHitRoutine(Health target, Vector3 startPos, Vector3 targetPos, Vector3 dir)
    {
        float distance = Vector3.Distance(startPos, targetPos);
        float travelTime = distance / arrowSpeed;

        yield return new WaitForSeconds(travelTime);

        if (target != null && !target.IsDead)
        {
            Damage damage = new Damage
            {
                value = GetEffectiveDamage(),
                type = DamageType.sharp,
                isProjectile = true,
                direction = dir,
                sourcePosition = startPos,
                isPlayerDamage = true
            };
            target.ReceiveDamage(damage);

            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, target.transform.position, 0.7f);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);

        Vector3 leftRay = Quaternion.Euler(0f, -maxTargetAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightRay = Quaternion.Euler(0f, maxTargetAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position, leftRay * range);
        Gizmos.DrawRay(transform.position, rightRay * range);
    }
#endif
}
