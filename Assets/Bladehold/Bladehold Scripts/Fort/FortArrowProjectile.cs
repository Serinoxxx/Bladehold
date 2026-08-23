using UnityEngine;

/// <summary>
///     Lightweight ballistic projectile for fort wall arrows. Sweeps forward each frame,
///     applies damage on contact with enemies, and lodges/despawns.
/// </summary>
public class FortArrowProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float radius = 0.2f;
    [SerializeField] private LayerMask hitLayers = ~0;

    private Vector3 direction;
    private float speed;
    private float damageAmount;
    private AudioClip hitSound;
    private bool hasHit = false;
    private float aliveTime = 0f;

    public void Init(Vector3 dir, float spd, float dmg, AudioClip hitSfx = null)
    {
        direction = dir.normalized;
        speed = spd;
        damageAmount = dmg;
        hitSound = hitSfx;
        transform.forward = direction;
    }

    private void Update()
    {
        StepSimulation(Time.deltaTime);
    }

    public void StepSimulation(float dt)
    {
        if (hasHit) return;

        aliveTime += dt;
        if (aliveTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float stepDistance = speed * dt;
        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + direction * stepDistance;

        if (Physics.SphereCast(currentPos, radius, direction, out RaycastHit hit, stepDistance, hitLayers, QueryTriggerInteraction.Ignore))
        {
            // Check if player or fort defense structure
            if ((Player.Instance != null && hit.collider.transform.root == Player.Instance.transform.root) ||
                hit.collider.GetComponentInParent<FortDefense>() != null ||
                hit.collider.GetComponentInParent<FortDefenseSocket>() != null)
            {
                transform.position = nextPos;
                return;
            }

            Health targetHealth = hit.collider.GetComponentInParent<Health>();
            if (targetHealth != null && !targetHealth.IsDead)
            {
                Damage damage = new Damage
                {
                    value = damageAmount,
                    type = DamageType.sharp,
                    isProjectile = true,
                    direction = direction,
                    sourcePosition = currentPos,
                    hitCollider = hit.collider,
                    isPlayerDamage = true
                };
                targetHealth.ReceiveDamage(damage);

                if (hitSound != null)
                {
                    AudioSource.PlayClipAtPoint(hitSound, hit.point, 0.8f);
                }

                hasHit = true;
                Destroy(gameObject);
                return;
            }
            else if (!hit.collider.isTrigger)
            {
                // Solid obstacle (ground / wall)
                hasHit = true;
                transform.position = hit.point;
                Destroy(gameObject, 0.5f);
                return;
            }
        }

        transform.position = nextPos;
    }
}
