using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Target-leading maths for tower projectiles: where to aim on a target, how fast it's moving, and
///     the intercept point for a projectile of a given speed.
/// </summary>
public static class TargetLead
{
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
}
