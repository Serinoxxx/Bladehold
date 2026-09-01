using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Prediction & Intercept")]
    [Tooltip("Predicts enemy movement and shoots ahead to collide with moving targets.")]
    [SerializeField] private bool leadTarget = true;
    [Tooltip("Maximum future prediction time in seconds to avoid over-leading erratic targets.")]
    [SerializeField] private float maxPredictionTime = 2.0f;

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

        Vector3 targetCenter = GetTargetAimPosition(target);
        Vector3 aimPoint = targetCenter;

        if (leadTarget)
        {
            Vector3 targetVelocity = GetTargetVelocity(target);
            if (TryCalculateIntercept(startPos, arrowSpeed, targetCenter, targetVelocity, maxPredictionTime, out Vector3 predictedPoint))
            {
                aimPoint = predictedPoint;
            }
        }

        // Clamp predicted aim point so it never sinks below terrain/ground level
        aimPoint.y = Mathf.Max(aimPoint.y, target.transform.position.y + 0.3f);

        Vector3 dir = (aimPoint - startPos).normalized;
        if (dir == Vector3.zero)
        {
            dir = transform.forward;
        }

        // Clamp horizontal firing direction to the wall slit aperture cone
        dir = ClampDirectionToCone(dir, transform.forward, maxTargetAngle);

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
            StartCoroutine(DelayedHitRoutine(target, startPos, aimPoint, dir));
        }
    }

    /// <summary>
    ///     Calculates the predicted intercept point for a projectile traveling at projectileSpeed
    ///     toward a target moving at targetVelocity from targetPos.
    ///     Returns true if a positive forward-in-time intercept was found; otherwise false.
    /// </summary>
    public static bool TryCalculateIntercept(
        Vector3 shooterPos,
        float projectileSpeed,
        Vector3 targetPos,
        Vector3 targetVelocity,
        float maxPrediction,
        out Vector3 interceptPoint)
    {
        interceptPoint = targetPos;

        if (projectileSpeed <= 0.001f)
        {
            return false;
        }

        Vector3 toTarget = targetPos - shooterPos;
        float targetSpeedSq = targetVelocity.sqrMagnitude;

        // Stationary or negligible target velocity — direct aim is exact
        if (targetSpeedSq < 0.01f)
        {
            return true;
        }

        float projSpeedSq = projectileSpeed * projectileSpeed;

        // Quadratic intercept equation: a*t^2 + b*t + c = 0
        float a = targetSpeedSq - projSpeedSq;
        float b = 2f * Vector3.Dot(toTarget, targetVelocity);
        float c = toTarget.sqrMagnitude;

        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0f)
        {
            // Target is moving too fast away from shooter to intercept
            return false;
        }

        float t = -1f;
        float sqrtDisc = Mathf.Sqrt(discriminant);

        if (Mathf.Abs(a) < 0.0001f)
        {
            // Linear case: b*t + c = 0
            if (Mathf.Abs(b) > 0.0001f)
            {
                float tLin = -c / b;
                if (tLin > 0f) t = tLin;
            }
        }
        else
        {
            float t1 = (-b - sqrtDisc) / (2f * a);
            float t2 = (-b + sqrtDisc) / (2f * a);

            if (t1 > 0f && t2 > 0f)
            {
                t = Mathf.Min(t1, t2);
            }
            else if (t1 > 0f)
            {
                t = t1;
            }
            else if (t2 > 0f)
            {
                t = t2;
            }
        }

        if (t <= 0f)
        {
            return false;
        }

        // Cap flight prediction time to reasonable bounds
        t = Mathf.Min(t, maxPrediction);

        interceptPoint = targetPos + targetVelocity * t;
        return true;
    }

    /// <summary>
    ///     Gets the current world velocity of the target, inspecting NavMeshAgent or Rigidbody.
    /// </summary>
    public static Vector3 GetTargetVelocity(Health target)
    {
        if (target == null) return Vector3.zero;

        // 1. Check NavMeshAgent (primary movement for enemies in Bladehold)
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = target.GetComponentInParent<NavMeshAgent>();
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (agent.isStopped)
            {
                return Vector3.zero;
            }

            if (agent.velocity.sqrMagnitude > 0.04f)
            {
                return agent.velocity;
            }

            if (agent.hasPath && agent.desiredVelocity.sqrMagnitude > 0.04f)
            {
                return agent.desiredVelocity;
            }
        }

        // 2. Check Rigidbody (e.g. physics knockback or airborne state)
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = target.GetComponentInParent<Rigidbody>();
        }

        if (rb != null && !rb.isKinematic)
        {
            if (rb.linearVelocity.sqrMagnitude > 0.04f)
            {
                return rb.linearVelocity;
            }
        }

        return Vector3.zero;
    }

    /// <summary>
    ///     Finds the best aim point on the target (capsule collider center or body height).
    /// </summary>
    public static Vector3 GetTargetAimPosition(Health target)
    {
        if (target == null) return Vector3.zero;

        CapsuleCollider cap = target.GetComponent<CapsuleCollider>();
        if (cap == null) cap = target.GetComponentInChildren<CapsuleCollider>();
        if (cap != null && !cap.isTrigger)
        {
            return cap.bounds.center;
        }

        Collider col = target.GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            return col.bounds.center;
        }

        return target.transform.position + Vector3.up * 1.0f;
    }

    /// <summary>
    ///     Clamps a firing direction vector so its horizontal yaw stays within the wall slit cone.
    /// </summary>
    public static Vector3 ClampDirectionToCone(Vector3 dir, Vector3 forward, float maxAngle)
    {
        Vector3 forwardXZ = new Vector3(forward.x, 0f, forward.z).normalized;
        Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z).normalized;

        if (forwardXZ == Vector3.zero || dirXZ == Vector3.zero)
        {
            return dir;
        }

        float halfAngle = maxAngle * 0.5f;
        float angle = Vector3.Angle(forwardXZ, dirXZ);

        if (angle > halfAngle)
        {
            Vector3 cross = Vector3.Cross(forwardXZ, dirXZ);
            float sign = cross.y >= 0f ? 1f : -1f;
            Vector3 clampedXZ = Quaternion.Euler(0f, sign * halfAngle, 0f) * forwardXZ;
            return new Vector3(clampedXZ.x, dir.y, clampedXZ.z).normalized;
        }

        return dir;
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
