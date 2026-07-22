using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoulderProjectile : MonoBehaviour, IDamageable
{
    [SerializeField] private Rigidbody body;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private AudioClip impactSfx;

    private float damage;
    private DamageType damageType;
    private float explosionRadius;
    private IDamageable owner;
    private bool launched;
    private bool exploded = false;

    private void OnValidate()
    {
        if (body == null) body = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (body == null)
        {
            Debug.LogError("Rigidbody missing on BoulderProjectile.");
            return;
        }
        body.isKinematic = true;
    }

    private float customGravity = 20f;

    public void Launch(Vector3 velocity, float dmg, DamageType type, float radius, IDamageable ownerDamageable, float gravity)
    {
        body.isKinematic = false;
        body.useGravity = false; // We will apply custom gravity for better arc control
        body.linearVelocity = velocity;
        damage = dmg;
        damageType = type;
        explosionRadius = radius;
        owner = ownerDamageable;
        customGravity = gravity;
        launched = true;

        Collider[] myCols = GetComponentsInChildren<Collider>();
        foreach (var mc in myCols) mc.enabled = false;
        Invoke(nameof(EnableColliders), 0.25f);

        if (owner is Component ownerComp)
        {
            Collider[] ownerCols = ownerComp.GetComponentsInChildren<Collider>();
            foreach (var mc in myCols)
            {
                foreach (var oc in ownerCols)
                {
                    Physics.IgnoreCollision(mc, oc);
                }
            }
        }

        Destroy(gameObject, 10f); // Failsafe
    }

    private void EnableColliders()
    {
        Collider[] myCols = GetComponentsInChildren<Collider>();
        foreach (var mc in myCols) mc.enabled = true;
    }

    private void FixedUpdate()
    {
        if (!launched || exploded) return;
        body.AddForce(Vector3.down * Mathf.Abs(customGravity), ForceMode.Acceleration); // Custom gravity
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!launched || exploded) return;

        // Extra safety: don't explode if the physics engine somehow hits the owner anyway
        if (owner is Component ownerComp && collision.collider.transform.IsChildOf(ownerComp.transform))
        {
            return;
        }

        Explode();
    }

    public void ReceiveDamage(Damage damage)
    {
        if (!launched || exploded) return;

        if (damage.isProjectile)
        {
            // Explode mid air if hit by a projectile
            Explode();
        }
        else
        {
            // Sent flying in the opposite direction if hit by a melee weapon
            Vector3 deflectDir = (transform.position - damage.sourcePosition).normalized;
            if (deflectDir.sqrMagnitude < 0.01f) deflectDir = -transform.forward;
            
            // Give it a slight upward arc
            deflectDir.y = Mathf.Max(deflectDir.y, 0.3f);
            deflectDir.Normalize();
            
            body.linearVelocity = deflectDir * Mathf.Max(body.linearVelocity.magnitude * 1.5f, 25f);

            // The deflector becomes the new owner, so it can hurt the original thrower
            if (damage.source != null)
            {
                owner = damage.source;
            }
        }
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider col in hits)
        {
            if (!col.TryGetComponent(out IDamageable d))
            {
                d = col.GetComponentInParent<IDamageable>();
            }

            if (d != null && d != owner)
            {
                d.ReceiveDamage(new Damage
                {
                    value = damage,
                    type = damageType,
                    sourcePosition = transform.position,
                    source = owner,
                    unparryable = true
                });
            }
        }

        if (impactVfxPrefab != null) Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        if (impactSfx != null) AudioSource.PlayClipAtPoint(impactSfx, transform.position);

        Destroy(gameObject);
    }
}
