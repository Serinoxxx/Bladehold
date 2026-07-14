using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A lingering toxic pool, left on the ground by a dying Mutant (<see cref="ToxicPoolOnDeath" />).
///     A copy-tune of <see cref="LightningStormZone" />: damages every unique
///     <see cref="IDamageable" /> caught in its radius every tick until its duration elapses, then
///     destroys itself. Hits are <c>unparryable</c> (a pool has no swing to read) and hurt player and
///     enemies alike — walking goblins through a dead mutant's puddle is legitimate tactics.
/// </summary>
public class ToxicPoolZone : MonoBehaviour
{
    [Tooltip("Optional VFX instantiated at the pool's position on any tick that damaged something.")]
    [SerializeField] private GameObject tickVfxPrefab;
    [SerializeField] private AudioClip tickSfx;

    private const int MaxOverlapResults = 32;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitThisTick = new HashSet<IDamageable>();

    private float radius;
    private float damage;
    private DamageType damageType;
    private IDamageable owner;

    /// <summary>Sets this pool's tunables and starts its tick loop. Call right after Instantiate.</summary>
    public void Initialize(float poolRadius, float duration, float tickInterval, float tickDamage, DamageType type, IDamageable ownerDamageable)
    {
        radius = poolRadius;
        damage = tickDamage;
        damageType = type;
        owner = ownerDamageable;

        StartCoroutine(RunPool(duration, tickInterval));
    }

    private IEnumerator RunPool(float duration, float tickInterval)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
            Tick();
        }
        Destroy(gameObject);
    }

    private void Tick()
    {
        hitThisTick.Clear();
        bool hitAnything = false;

        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }
            if (damageable == null || damageable == owner || !hitThisTick.Add(damageable))
            {
                continue;
            }

            damageable.ReceiveDamage(new Damage
            {
                value = damage,
                type = damageType,
                sourcePosition = transform.position,
                unparryable = true,
            });
            hitAnything = true;
        }

        if (!hitAnything)
        {
            return;
        }

        if (tickVfxPrefab != null)
        {
            Instantiate(tickVfxPrefab, transform.position, Quaternion.identity);
        }
        if (tickSfx != null)
        {
            AudioSource.PlayClipAtPoint(tickSfx, transform.position);
        }
    }
}
