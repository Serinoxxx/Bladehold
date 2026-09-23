using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Arrow projectile in the Fishing Minigame supporting pierce (Fish Skewer) and bounces (Bounce Shot).
/// </summary>
public class FishingBowArrow : MonoBehaviour
{
    [SerializeField] private float speed = 36f;
    [SerializeField] private float maxLifetime = 3.5f;
    [SerializeField] private LayerMask hitLayers = ~0;

    private int remainingPierces = 0;
    private int remainingBounces = 0;
    private float lifetimeTimer = 0f;
    private readonly HashSet<FishController> hitFish = new HashSet<FishController>();
    private Vector3 velocity;

    public void Setup(Vector3 direction, int pierces, int bounces)
    {
        velocity = direction.normalized * speed;
        transform.forward = direction.normalized;
        remainingPierces = pierces;
        remainingBounces = bounces;
        lifetimeTimer = 0f;
        hitFish.Clear();
    }

    private void Update()
    {
        lifetimeTimer += Time.deltaTime;
        if (lifetimeTimer >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        float stepDist = speed * Time.deltaTime;
        Vector3 moveStep = velocity.normalized * stepDist;
        Ray ray = new Ray(transform.position, velocity.normalized);

        if (Physics.SphereCast(ray, 0.4f, out RaycastHit hit, stepDist, hitLayers, QueryTriggerInteraction.Collide))
        {
            FishController fish = hit.collider.GetComponentInParent<FishController>();
            if (fish != null && !fish.IsDead && !hitFish.Contains(fish))
            {
                hitFish.Add(fish);
                Damage d = new Damage
                {
                    value = 1f,
                    type = DamageType.sharp,
                    isPlayerDamage = true,
                    isProjectile = true,
                    sourcePosition = transform.position,
                    direction = velocity.normalized
                };
                fish.ReceiveDamage(d);

                // Handle pierce
                if (remainingPierces > 0)
                {
                    remainingPierces--;
                    transform.position += moveStep;
                    return;
                }

                // Handle bounce
                if (remainingBounces > 0)
                {
                    remainingBounces--;
                    FishController nextTarget = FindBounceTarget(fish.transform.position);
                    if (nextTarget != null)
                    {
                        Vector3 bounceDir = (nextTarget.transform.position - transform.position).normalized;
                        velocity = bounceDir * speed;
                        transform.forward = bounceDir;
                        transform.position += bounceDir * 0.5f;
                        return;
                    }
                }

                // If no pierce or bounce left, destroy arrow
                Destroy(gameObject);
                return;
            }
            else if (fish == null)
            {
                // Hit pond edge or environment
                Destroy(gameObject);
                return;
            }
        }

        transform.position += moveStep;
    }

    private FishController FindBounceTarget(Vector3 origin)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, 12f);
        FishController closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            FishController candidate = colliders[i].GetComponentInParent<FishController>();
            if (candidate != null && !candidate.IsDead && !hitFish.Contains(candidate))
            {
                float dist = (candidate.transform.position - origin).sqrMagnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = candidate;
                }
            }
        }

        return closest;
    }
}
