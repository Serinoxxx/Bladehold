using System.Collections;
using UnityEngine;

/// <summary>
///     A patch of burning ground left by the Mage's fire imbuement (the "Scorched Earth" node) —
///     a player-owned clone of <see cref="LightningStormZone" />: ticks damage to everything caught
///     in its radius every <c>tickInterval</c> seconds until <c>duration</c> elapses, then destroys
///     itself. Two deliberate upgrades over the original: the overlap is masked to enemy layers
///     (an all-layers sweep would torch friendly <see cref="Gate" /> Health), and the tick damage is
///     computed once at spawn from the triggering hit's value — which already folds in charge, crit,
///     and AllDamageMultiplier — so the zone never re-applies player multipliers (the chain
///     lightning precedent). The prefab owns the looks (looping fire VFX).
/// </summary>
public class FlameZone : MonoBehaviour
{
    [Tooltip("Optional VFX instantiated at the zone's position each tick that burns anything.")]
    [SerializeField] private GameObject burnVfxPrefab;
    [SerializeField] private AudioClip burnSfx;

    private const int MaxOverlapResults = 16;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];

    private float radius;
    private float tickDamage;
    private IDamageable owner;
    private LayerMask enemyLayers;

    /// <summary>Sets this zone's tunables and starts its burn loop. Call right after Instantiate.</summary>
    public void Initialize(float burnRadius, float duration, float tickInterval, float damagePerTick, IDamageable ownerDamageable, LayerMask layers)
    {
        radius = burnRadius;
        tickDamage = damagePerTick;
        owner = ownerDamageable;
        enemyLayers = layers;

        StartCoroutine(RunBurn(duration, tickInterval));
    }

    private IEnumerator RunBurn(float duration, float tickInterval)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
            Burn();
        }
        Destroy(gameObject);
    }

    private void Burn()
    {
        bool burnedAnything = false;
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer, enemyLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }
            if (damageable == null || damageable == owner)
            {
                continue;
            }

            damageable.ReceiveDamage(new Damage
            {
                value = tickDamage,
                type = DamageType.elemental,
                sourcePosition = transform.position,
                source = owner,
            });
            burnedAnything = true;
        }

        if (!burnedAnything)
        {
            return;
        }

        if (burnVfxPrefab != null)
        {
            Instantiate(burnVfxPrefab, transform.position, Quaternion.identity);
        }
        if (burnSfx != null)
        {
            AudioSource.PlayClipAtPoint(burnSfx, transform.position);
        }
    }
}
