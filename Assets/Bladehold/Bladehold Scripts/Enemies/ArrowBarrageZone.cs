using System.Collections;
using UnityEngine;

/// <summary>
///     An arrow barrage hazard. Strikes everything caught in its radius every tickRate seconds
///     until barrageDuration has elapsed, then destroys itself.
/// </summary>
public class ArrowBarrageZone : MonoBehaviour
{
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private AudioClip hitSfx;

    private const int MaxOverlapResults = 16;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];

    private float radius;
    private float damage;
    private DamageType damageType;
    private IDamageable owner;

    // The ground telegraph is the prefab's authored red round-marker VFX.
    public void Initialize(float strikeRadius, float duration, float strikeInterval, float strikeDamage, DamageType type, IDamageable ownerDamageable)
    {
        radius = strikeRadius;
        damage = strikeDamage;
        damageType = type;
        owner = ownerDamageable;

        StartCoroutine(RunBarrage(duration, strikeInterval));
    }

    private IEnumerator RunBarrage(float duration, float strikeInterval)
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
            Collider col = overlapBuffer[i];
            if (!col.TryGetComponent(out IDamageable damageable))
            {
                damageable = col.GetComponentInParent<IDamageable>();
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

        if (!struckAnything) return;

        if (hitVfxPrefab != null) Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
        if (hitSfx != null) AudioSource.PlayClipAtPoint(hitSfx, transform.position);
    }
}
