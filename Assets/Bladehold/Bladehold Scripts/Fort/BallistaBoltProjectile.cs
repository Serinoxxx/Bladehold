using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     High-speed piercing bolt for Ballista defenses.
///     Travels in a straight line, penetrating multiple enemies and dealing heavy damage.
/// </summary>
public class BallistaBoltProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float radius = 0.4f;
    [SerializeField] private int maxPierce = 3;
    [SerializeField] private LayerMask hitLayers = ~0;

    private Vector3 direction;
    private float speed = 35f;
    private float damageAmount;
    private int pierceRemaining;
    private float aliveTime = 0f;
    private readonly HashSet<Health> hitTargets = new HashSet<Health>();

    public void Init(Vector3 dir, float spd, float dmg, int pierce = 3)
    {
        direction = dir.normalized;
        speed = spd;
        damageAmount = dmg;
        maxPierce = pierce;
        pierceRemaining = pierce;
        transform.forward = direction;
    }

    private void Update()
    {
        StepSimulation(Time.deltaTime);
    }

    public void StepSimulation(float dt)
    {
        aliveTime += dt;
        if (aliveTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float stepDist = speed * dt;
        Vector3 curPos = transform.position;
        Vector3 nextPos = curPos + direction * stepDist;

        RaycastHit[] hits = Physics.SphereCastAll(curPos, radius, direction, stepDist, hitLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) continue;
            if (Player.Instance != null && hit.collider.transform.root == Player.Instance.transform.root) continue;
            if (hit.collider.GetComponentInParent<DefenseStructure>() != null || hit.collider.GetComponentInParent<TowerPlot>() != null) continue;

            Health targetHealth = hit.collider.GetComponentInParent<Health>();
            if (targetHealth != null && !targetHealth.IsDead && !hitTargets.Contains(targetHealth))
            {
                hitTargets.Add(targetHealth);

                Damage dmg = new Damage
                {
                    value = damageAmount,
                    type = DamageType.sharp,
                    isPlayerDamage = true,
                    sourcePosition = curPos,
                    source = Player.Instance != null ? Player.Instance.Damageable : null
                };

                targetHealth.ReceiveDamage(dmg);

                // Knockback
                if (targetHealth.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
                {
                    rb.AddForce(direction * 15f, ForceMode.Impulse);
                }

                pierceRemaining--;
                if (pierceRemaining <= 0)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }

        transform.position = nextPos;
    }
}
