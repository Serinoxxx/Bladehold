using UnityEngine;

/// <summary>
///     The Mystic's slow homing orb projectile, fired by <see cref="HomingOrbAttack" />. A sibling of
///     <see cref="LightningBall" /> (same kinematic <see cref="Rigidbody" /> + trigger-hit shape) that
///     each physics tick steers its travel direction toward the player, capped at
///     <c>turnRateDegPerSec</c> so it stays dodgeable, and gives up homing after <c>homingSeconds</c>
///     (then flies straight — a committed dodge always works). Deliberately a copy rather than a
///     subclass: <see cref="LightningBall" /> carries the Storm Witch-specific Conduit skill
///     interaction, which the Mystic's arcane orbs don't share.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HomingOrb : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [Tooltip("Instantiated at the impact point when the orb hits something.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private AudioClip impactSfx;

    private Vector3 direction;
    private float speed;
    private float damage;
    private DamageType damageType;
    private IDamageable owner;
    private float turnRateDegPerSec;
    private float homingEndTime;
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

    /// <summary>Sets this orb in motion. Call right after Instantiate.</summary>
    public void Launch(Vector3 travelDirection, float travelSpeed, float damageValue, DamageType type,
        float lifetime, IDamageable ownerDamageable, float turnRate, float homingSeconds)
    {
        direction = travelDirection.normalized;
        speed = travelSpeed;
        damage = damageValue;
        damageType = type;
        owner = ownerDamageable;
        turnRateDegPerSec = turnRate;
        homingEndTime = Time.time + homingSeconds;
        launched = true;

        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void FixedUpdate()
    {
        if (anyError || !launched) return;

        // Steer toward the player's chest while the homing window is live; a dead player stops the
        // homing (the orb just coasts out its lifetime).
        if (Time.time < homingEndTime && turnRateDegPerSec > 0f)
        {
            Player playerInstance = Player.Instance;
            if (playerInstance != null && playerInstance.Health != null && !playerInstance.Health.IsDead)
            {
                Vector3 toTarget = playerInstance.transform.position + 1.5f * Vector3.up - body.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    float maxRadians = turnRateDegPerSec * Mathf.Deg2Rad * Time.fixedDeltaTime;
                    direction = Vector3.RotateTowards(direction, toTarget.normalized, maxRadians, 0f);
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

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

        damageable.ReceiveDamage(new Damage
        {
            value = damage,
            type = damageType,
            sourcePosition = transform.position,
        });

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
