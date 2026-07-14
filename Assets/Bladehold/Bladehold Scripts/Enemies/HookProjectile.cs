using System;
using UnityEngine;

/// <summary>
///     The Pig Butcher's flying hook, thrown by <see cref="HookProjectileAttack" /> — the
///     <see cref="LightningBall" /> shape (straight kinematic flight, trigger hit, owner-safe), but
///     <c>sharp</c> and deliberately <b>parryable</b>: a single readable projectile. On damaging the
///     player it starts a <see cref="PlayerPullReceiver.Pull" /> toward the butcher; the pull only
///     fires if the damage actually landed (a momentary <see cref="Health.OnDamaged" /> subscription
///     around the hit), so a Parry or Solid block negates the drag along with the damage — and
///     <see cref="Damage.source" /> is stamped, so Counterstrike punishes the butcher through his
///     own hook.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HookProjectile : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [Tooltip("Instantiated at the impact point when the hook hits something.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private AudioClip impactSfx;

    private Vector3 direction;
    private float speed;
    private float damage;
    private DamageType damageType;
    private IDamageable owner;
    private Transform ownerTransform;
    private float pullSeconds;
    private float pullStopDistance;
    private bool launched;
    private bool anyError = false;

    private void OnValidate()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
    }

    private void Awake()
    {
        if (body == null)
        {
            Debug.LogError("Rigidbody component is not assigned or found on the GameObject.");
            anyError = true;
            return;
        }

        body.isKinematic = true;
        body.useGravity = false;
    }

    /// <summary>Sets this hook in motion. Call right after Instantiate.</summary>
    public void Launch(Vector3 travelDirection, float travelSpeed, float damageValue, DamageType type,
        float lifetime, IDamageable ownerDamageable, Transform ownerRoot, float dragSeconds, float dragStopDistance)
    {
        direction = travelDirection.normalized;
        speed = travelSpeed;
        damage = damageValue;
        damageType = type;
        owner = ownerDamageable;
        ownerTransform = ownerRoot;
        pullSeconds = dragSeconds;
        pullStopDistance = dragStopDistance;
        launched = true;

        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void FixedUpdate()
    {
        if (anyError || !launched) return;

        body.MovePosition(body.position + direction * speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (anyError || !launched) return;

        if (!other.TryGetComponent(out IDamageable damageable))
        {
            damageable = other.GetComponentInParent<IDamageable>();
        }

        if (damageable == null || damageable == owner)
        {
            return;
        }

        // The pull only fires if the damage actually lands — Health raises OnDamaged synchronously
        // inside ReceiveDamage unless a TryBlockDamage handler (Parry, Solid) negated the hit, so a
        // momentary subscription is an honest "did it connect?" probe.
        bool landed = false;
        Action<Damage> landedProbe = _ => landed = true;
        Health targetHealth = damageable as Health;
        if (targetHealth != null)
        {
            targetHealth.OnDamaged += landedProbe;
        }

        damageable.ReceiveDamage(new Damage
        {
            value = damage,
            type = damageType,
            sourcePosition = transform.position,
            source = owner,
        });

        if (targetHealth != null)
        {
            targetHealth.OnDamaged -= landedProbe;
        }

        if (landed && ownerTransform != null
            && Player.Instance != null && ReferenceEquals(damageable, Player.Instance.Damageable))
        {
            PlayerPullReceiver pullReceiver = Player.Instance.GetComponentInChildren<PlayerPullReceiver>();
            if (pullReceiver != null)
            {
                pullReceiver.Pull(ownerTransform, pullSeconds, pullStopDistance);
            }
        }

        if (impactVfxPrefab != null)
        {
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        }
        if (impactSfx != null)
        {
            AudioSource.PlayClipAtPoint(impactSfx, transform.position);
        }

        Destroy(gameObject);
    }
}
