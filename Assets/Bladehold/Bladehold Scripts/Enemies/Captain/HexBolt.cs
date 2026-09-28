using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Captain Mogra's hex bolt: a slow glowing orb flying in a straight line (no homing), so strafing
///     sideways dodges it. Hits only the player, fizzles on walls and props, and flies through other
///     enemies. Moves via a kinematic <see cref="Rigidbody" /> like <see cref="LightningBall" />.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HexBolt : MonoBehaviour
{
    [SerializeField] private Rigidbody body;
    [Tooltip("Impact burst + sound where the bolt hits or fizzles.")]
    [SerializeField] private MMF_Player impactFeedback;
    [Tooltip("Visual root hidden on impact (the object lingers briefly so trails fade).")]
    [SerializeField] private GameObject visualRoot;

    private Vector3 direction;
    private float speed;
    private float damage;
    private IDamageable owner;
    private HexHitGroup hitGroup;
    private bool launched;
    private bool spent;
    private bool anyError;

    private void OnValidate()
    {
        if (body == null) body = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (body == null)
        {
            Debug.LogError($"{name}: HexBolt needs a Rigidbody.", this);
            anyError = true;
            return;
        }
        body.isKinematic = true;
        body.useGravity = false;
        if (impactFeedback == null) Debug.LogError($"{name}: impactFeedback is not assigned.", this);
    }

    /// <summary>Sets the bolt in motion. Call right after Instantiate.</summary>
    /// <param name="group">Optional: bolts of one volley share a group, so a volley hits at most once.</param>
    public void Launch(Vector3 travelDirection, float travelSpeed, float damageValue, float lifetime, IDamageable ownerDamageable, HexHitGroup group = null)
    {
        hitGroup = group;
        direction = travelDirection;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        speed = travelSpeed;
        damage = damageValue;
        owner = ownerDamageable;
        launched = true;
        transform.rotation = Quaternion.LookRotation(direction);
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (anyError || !launched || spent) return;
        body.MovePosition(body.position + direction * speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (anyError || !launched || spent) return;

        Player player = Player.Instance;
        if (player != null && other.GetComponentInParent<Player>() == player)
        {
            if (player.Health != null && !player.Health.IsDead && (hitGroup == null || hitGroup.TryConsume()))
            {
                player.Health.ReceiveDamage(new Damage
                {
                    value = damage,
                    type = DamageType.elemental,
                    isProjectile = true,
                    source = owner,
                    sourcePosition = transform.position,
                    direction = direction,
                });
            }
            Spend();
            return;
        }

        // Other enemies (and their weapons/triggers) don't stop it; solid scenery does.
        if (other.isTrigger || other.GetComponentInParent<Health>() != null) return;
        Spend();
    }

    private void Spend()
    {
        spent = true;
        if (impactFeedback != null) impactFeedback.PlayFeedbacks(transform.position);
        if (visualRoot != null) visualRoot.SetActive(false);
        Destroy(gameObject, 1.5f);
    }
}
