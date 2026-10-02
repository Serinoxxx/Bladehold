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
    private float knockbackAway;
    private float knockbackUp;
    private int pierceRemaining;
    private float aliveTime = 0f;
    private readonly HashSet<Health> hitTargets = new HashSet<Health>();

    private void Awake()
    {
        // PlayerBarrier colliders (tower plot pads) only keep the player out: bolts fly through them.
        hitLayers = PlayerBarrier.Exclude(hitLayers);
    }

    public void Init(Vector3 dir, float spd, float dmg, int pierce = 3, float knockAway = 0f, float knockUp = 0f)
    {
        direction = dir.normalized;
        speed = spd;
        damageAmount = dmg;
        knockbackAway = knockAway;
        knockbackUp = knockUp;
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

                Vector3 knockback = KnockbackVelocity(direction, knockbackAway, knockbackUp);
                Damage dmg = new Damage
                {
                    isDefenseDamage = true,
                    piercesShields = true,
                    isProjectile = true,
                    value = damageAmount,
                    type = DamageType.sharp,
                    isPlayerDamage = true,
                    sourcePosition = curPos,
                    knockbackVelocity = knockback,
                    knockbackForce = knockback.magnitude,
                    source = Player.Instance != null ? Player.Instance.Damageable : null
                };

                targetHealth.ReceiveDamage(dmg);
                ApplyElement(targetHealth);

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

    /// <summary>Launch velocity for a bolt hit: <paramref name="away" /> along the flattened flight direction plus <paramref name="up" />.</summary>
    public static Vector3 KnockbackVelocity(Vector3 flightDir, float away, float up)
    {
        Vector3 flat = new Vector3(flightDir.x, 0f, flightDir.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
        return flat * away + Vector3.up * up;
    }

    private StructureElement element = StructureElement.None;

    /// <summary>The firing ballista's element (plan 17); every bolt hit applies its status.</summary>
    public void SetElement(StructureElement value)
    {
        element = value;
    }

    private void ApplyElement(Health target)
    {
        if (element == StructureElement.None || target == null || target.IsDead) return;
        EnemyStatusManager.GetOrAdd(target)?.ApplyStatus(element.StatusId());
    }
}
