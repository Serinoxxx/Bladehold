using System.Collections;
using UnityEngine;

/// <summary>
///     A lightning storm hazard, cast at the player's position by <see cref="LightningStormAttack" />.
///     Strikes everything caught in its radius every <c>strikeInterval</c> seconds until <c>duration</c>
///     has elapsed, then destroys itself. Unlike <see cref="DamageTrigger" />'s Sphere mode (which hits
///     each target once per activation), this hazard is meant to hit repeatedly while something lingers
///     inside it, so it isn't reused directly.
/// </summary>
public class LightningStormZone : MonoBehaviour
{
    [Tooltip("Instantiated at this zone's position each time it strikes anything.")]
    [SerializeField] private GameObject strikeVfxPrefab;
    [SerializeField] private AudioClip strikeSfx;

    private const int MaxOverlapResults = 8;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];

    private float radius;
    private float damage;
    private DamageType damageType;
    private IDamageable owner;

    /// <summary>Sets this zone's tunables and starts its strike loop. Call right after Instantiate.</summary>
    public void Initialize(float strikeRadius, float duration, float strikeInterval, float strikeDamage, DamageType type, IDamageable ownerDamageable)
    {
        radius = strikeRadius;
        damage = strikeDamage;
        damageType = type;
        owner = ownerDamageable;

        StartCoroutine(RunStorm(duration, strikeInterval));
    }

    private IEnumerator RunStorm(float duration, float strikeInterval)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(strikeInterval);
            elapsed += strikeInterval;
            Strike();
        }
        Destroy(gameObject);
    }

    private void Strike()
    {
        bool struckAnything = false;
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer);
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
                value = damage,
                type = damageType,
                sourcePosition = transform.position,
            });
            struckAnything = true;
        }

        if (!struckAnything)
        {
            return;
        }

        if (strikeVfxPrefab != null)
        {
            Instantiate(strikeVfxPrefab, transform.position, Quaternion.identity);
        }
        if (strikeSfx != null)
        {
            AudioSource.PlayClipAtPoint(strikeSfx, transform.position);
        }
    }
}
