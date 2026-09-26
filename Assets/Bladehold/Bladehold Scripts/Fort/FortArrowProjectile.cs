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
    private bool hasHit = false;
    private float aliveTime = 0f;

    private bool isFrost = false;
    private bool isLightning = false;
    private bool isFire = false;
    private string infusedElement = "";
    private GameObject visualFxInstance;

    public void Init(Vector3 dir, float spd, float dmg)
    {
        direction = dir.normalized;
        speed = spd;
        damageAmount = dmg;
        transform.forward = direction;
    }

    public void SetElementalInfusion(bool frost, bool lightning, bool fire)
    {
        isFrost = frost;
        isLightning = lightning;
        isFire = fire;

        if (fire) infusedElement = "Fire";
        else if (lightning) infusedElement = "Lightning";
        else if (frost) infusedElement = "Ice";

        ApplyElementalVisuals();
    }

    private void ApplyElementalVisuals()
    {
        if (visualFxInstance != null) return;

        Color tint = Color.white;
        GameObject fxPrefab = null;

        if (isFire)
        {
            tint = new Color(1f, 0.4f, 0.1f);
            if (ElementalEffectsManager.Instance != null) fxPrefab = ElementalEffectsManager.Instance.fireStatusVfx;
        }
        else if (isLightning)
        {
            tint = new Color(1f, 0.95f, 0.2f);
            if (ElementalEffectsManager.Instance != null) fxPrefab = ElementalEffectsManager.Instance.lightningTrailVfx;
        }
        else if (isFrost)
        {
            tint = new Color(0.4f, 0.8f, 1f);
            if (ElementalEffectsManager.Instance != null) fxPrefab = ElementalEffectsManager.Instance.iceStatusVfx;
        }

        if (fxPrefab != null)
        {
            visualFxInstance = Instantiate(fxPrefab, transform.position, transform.rotation, transform);
            visualFxInstance.transform.localScale = Vector3.one * 0.4f;
        }

        // Tint renderers
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r != null && r.material != null)
            {
                r.material.color = tint;
            }
        }
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
                hit.collider.GetComponentInParent<DefenseStructure>() != null ||
                hit.collider.GetComponentInParent<TowerPlot>() != null)
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
                    type = (!string.IsNullOrEmpty(infusedElement)) ? DamageType.elemental : DamageType.sharp,
                    isProjectile = true,
                    direction = direction,
                    sourcePosition = currentPos,
                    hitCollider = hit.collider,
                    isPlayerDamage = true,
                    elementId = !string.IsNullOrEmpty(infusedElement) ? infusedElement : RunSession.GetElementInSlot("SLOT_FORTRESS")
                };

                if (Player.Instance != null && Player.Instance.Stats != null)
                {
                    float pyreBonus = Player.Instance.Stats.GetValue(StatType.FireFortressPyreBonus);
                    if (pyreBonus > 0f)
                    {
                        if (targetHealth.GetComponent<EnemyStatusManager>()?.HasStatus("Fire") == true)
                        {
                            damage.value *= (1f + pyreBonus);
                        }
                    }

                    if (Time.time - targetHealth.LastPlayerRangedHitTime <= 5.0f)
                    {
                        float focusBonus = Player.Instance.Stats.GetValue(StatType.FortFocusFireBonus);
                        if (focusBonus > 0f)
                        {
                            damage.value *= (1f + focusBonus);
                        }
                    }
                }

                targetHealth.ReceiveDamage(damage);

                if (isFire)
                {
                    EnemyStatusManager.GetOrAdd(targetHealth)?.ApplyStatus("Fire");
                }
                if (isFrost)
                {
                    EnemyStatusManager.GetOrAdd(targetHealth)?.ApplyStatus("Ice", 0.5f);
                }
                if (isLightning)
                {
                    EnemyStatusManager.GetOrAdd(targetHealth)?.ApplyStatus("Lightning");
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
