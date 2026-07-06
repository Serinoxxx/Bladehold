using UnityEngine;

/// <summary>
///     The Storm Witch's lightning ball projectile, fired by <see cref="LightningBallAttack" />. Moves in
///     a straight line at a constant (slow, dodgeable) speed via a kinematic <see cref="Rigidbody" />, and
///     on touching an <see cref="IDamageable" /> (other than its owner) deals damage and destroys itself.
///     A <see cref="Destroy(GameObject, float)" /> failsafe cleans it up if it never hits anything (the
///     <see cref="ImpulseOrb" /> lifetime idiom).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class LightningBall : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [Tooltip("Instantiated at the impact point when the ball hits something.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private AudioClip impactSfx;

    private Vector3 direction;
    private float speed;
    private float damage;
    private DamageType damageType;
    private IDamageable owner;
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

    /// <summary>Sets this ball in motion. Call right after Instantiate.</summary>
    public void Launch(Vector3 travelDirection, float travelSpeed, float damageValue, DamageType type, float lifetime, IDamageable ownerDamageable)
    {
        direction = travelDirection.normalized;
        speed = travelSpeed;
        damage = damageValue;
        damageType = type;
        owner = ownerDamageable;
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

        float value = damage;

        // Conduit (gold-tree skill): the player takes reduced lightning-ball damage and can deflect
        // the strike into a chain of their own. Read through Player.Instance.Stats, the same
        // enemy-side pattern GoldenGoblin uses.
        if (Player.Instance != null && ReferenceEquals(damageable, Player.Instance.Damageable))
        {
            PlayerStats stats = Player.Instance.Stats;
            if (stats != null)
            {
                float reduction = stats.GetValue(StatType.ConduitDamageReductionPercent);
                if (reduction > 0f)
                {
                    value *= 1f - Mathf.Clamp01(reduction);
                }

                if (Random.value < stats.GetValue(StatType.ConduitChainChance))
                {
                    ChainLightning chainLightning = Player.Instance.GetComponentInChildren<ChainLightning>();
                    if (chainLightning != null)
                    {
                        // The chain is fuelled by the ball's full, unreduced damage.
                        chainLightning.ForceChain(damage, transform.position);
                    }
                }
            }
        }

        damageable.ReceiveDamage(new Damage
        {
            value = value,
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
