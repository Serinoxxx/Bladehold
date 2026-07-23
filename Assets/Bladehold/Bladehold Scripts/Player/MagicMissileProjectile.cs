using System;
using UnityEngine;

/// <summary>
///     One magic missile in flight — a real projectile that carries the shot's damage (the
///     <see cref="AxeProjectile" /> shape without pierce or boomerang: the bolt stops in the first
///     thing it hits). <see cref="PlayerWand" /> instantiates one per shot and calls
///     <see cref="Launch" /> with everything captured at release time.
///
///     Each <c>FixedUpdate</c> the missile advances along its line and **sphere casts from its
///     previous position to its current one**, so a fast tick or thin collider can't tunnel through
///     unhit. Trigger colliders without a damageable (coins, orbs, hitboxes) never stop it — except
///     an <see cref="ElementNode" />, which the missile collects in passing and flies on (the bow's
///     Pickup Arrows precedent; this is how the Mage swaps imbuement from a distance). The first
///     non-owner <see cref="IDamageable" /> or solid environment ends the flight: damage via
///     <see cref="PlayerWand.CreateHitDamage" />, impact VFX, despawn.
///
///     The prefab owns all the looks. Per-element child visuals (fire = fireball, etc.) are authored
///     on the prefab and toggled by <see cref="SetElement" /> — the element changes nothing about the
///     flight or the direct hit; elemental effects come from <see cref="MageImbuement" /> listening
///     to the wand's <see cref="PlayerWand.OnHit" />.
/// </summary>
public class MagicMissileProjectile : MonoBehaviour, IPlayerProjectile
{
    /// <summary>Current world position, for the whirlwind's radius check.</summary>
    public Vector3 Position => transform.position;

    /// <summary>Destroys the missile mid-flight without dealing its damage (the Barbarian Giant's whirlwind).</summary>
    public void Shatter()
    {
        if (impactVfxPrefab != null)
        {
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }

    private void OnEnable()
    {
        PlayerProjectileRegistry.Register(this);
    }

    private void OnDisable()
    {
        PlayerProjectileRegistry.Unregister(this);
    }

    /// <summary>Everything a shot decides at release time, captured so the missile stays truthful to the wind-up that launched it.</summary>
    public struct LaunchSpec
    {
        public Vector3 origin;
        public Vector3 direction;
        /// <summary>Flight speed in metres per second.</summary>
        public float speed;
        /// <summary>Flight distance at which the missile fizzles out.</summary>
        public float maxRange;
        /// <summary>Radius of the swept hit volume.</summary>
        public float radius;
        public LayerMask hitLayers;
        /// <summary>Charge level held when fired — damage reads it at impact.</summary>
        public int chargeLevel;
        /// <summary>The caster's own IDamageable — never hit (the DamageTrigger owner idiom).</summary>
        public IDamageable owner;
        /// <summary>An extra fly-through target — the horse under a mounted caster. Null = none.</summary>
        public IDamageable ignoredTarget;
        /// <summary>The Mage's imbuement, for collecting ElementNodes flown past. Null-safe (pre-imbuement wiring, or another class somehow firing).</summary>
        public MageImbuement imbuement;
    }

    [Serializable]
    private struct ElementVisual
    {
        public ElementType element;
        [Tooltip("Child object shown while the missile carries this element (e.g. the fireball look).")]
        public GameObject visual;
    }

    [Tooltip("Child object shown while the missile is un-imbued (the plain magic-missile look). Optional.")]
    [SerializeField] private GameObject neutralVisual;
    [Tooltip("Per-element child visuals, toggled by the element the wand carried at fire time. Optional.")]
    [SerializeField] private ElementVisual[] elementVisuals;
    [Tooltip("Instantiated where the missile stops (enemy or terrain). Optional — imbued explosion VFX comes from MageImbuement, this is just the bolt fizzling.")]
    [SerializeField] private GameObject impactVfxPrefab;
    [Tooltip("Safety despawn for a missile that somehow never lands.")]
    [SerializeField] private float maxLifetimeSeconds = 10f;

    private const int MaxCastHits = 64;

    private readonly RaycastHit[] castBuffer = new RaycastHit[MaxCastHits];

    private PlayerWand caster;
    private LaunchSpec spec;
    private float travelled;
    private bool launched;

    /// <summary>Sends the missile flying. Damage happens where it stops; the prefab is destroyed on impact, at max range, or on timeout.</summary>
    public void Launch(PlayerWand caster, LaunchSpec spec)
    {
        this.caster = caster;
        this.spec = spec;
        travelled = 0f;
        launched = true;

        transform.position = spec.origin;
        if (spec.direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(spec.direction);
        }

        Destroy(gameObject, maxLifetimeSeconds);
    }

    /// <summary>
    ///     Matches the missile's looks to the element the wand carried at fire time (null = un-imbued).
    ///     Purely cosmetic — call any time after Instantiate.
    /// </summary>
    public void SetElement(ElementType? element)
    {
        if (neutralVisual != null)
        {
            neutralVisual.SetActive(element == null);
        }
        if (elementVisuals == null)
        {
            return;
        }
        foreach (ElementVisual entry in elementVisuals)
        {
            if (entry.visual != null)
            {
                entry.visual.SetActive(element != null && entry.element == element.Value);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!launched)
        {
            return;
        }

        Vector3 from = transform.position;
        float step = Mathf.Min(spec.speed * Time.fixedDeltaTime, spec.maxRange - travelled);
        travelled += step;
        Vector3 to = from + spec.direction * step;

        if (SweepForImpact(from, step))
        {
            // The sweep ended the flight and already handled impact/despawn.
            return;
        }

        transform.position = to;

        if (travelled >= spec.maxRange)
        {
            // Fizzled out at max range — no impact VFX, the bolt just dissipates.
            Destroy(gameObject);
        }
    }

    /// <summary>
    ///     Sphere-casts the tick's travel; collects element nodes in passing and stops on the first
    ///     enemy or solid surface. Returns true when the flight ended inside the sweep.
    /// </summary>
    private bool SweepForImpact(Vector3 from, float step)
    {
        int count = Physics.SphereCastNonAlloc(from, spec.radius, spec.direction, castBuffer, step, spec.hitLayers, QueryTriggerInteraction.Collide);
        Array.Sort(castBuffer, 0, count, PlayerThrownAxe.HitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castBuffer[i];
            IDamageable damageable = PlayerThrownAxe.ResolveDamageable(hit.collider);

            if (damageable == null)
            {
                // Element nodes are collected in passing — the missile flies on (the bow's Pickup
                // Arrows precedent; this is the "swap imbuement from a distance" half for ground nodes).
                if (spec.imbuement != null)
                {
                    ElementNode node = hit.collider.GetComponentInParent<ElementNode>();
                    if (node != null)
                    {
                        node.TryCollectRemote(spec.imbuement);
                        continue;
                    }
                }

                // Other trigger colliders with no damageable (coins, orbs, hitboxes) never stop the
                // missile; solid environment does.
                if (!hit.collider.isTrigger)
                {
                    if (hit.distance <= 0.0001f)
                    {
                        continue;
                    }
                    Impact(HitPointOf(hit, from));
                    return true;
                }
                continue;
            }

            if (IsOwner(damageable))
            {
                continue;
            }

            if (caster == null)
            {
                // Caster gone mid-flight (scene teardown) — stop dealing damage.
                Destroy(gameObject);
                return true;
            }

            Vector3 hitPoint = HitPointOf(hit, from);
            Damage damage = caster.CreateHitDamage(spec.chargeLevel, from, damageable);
            damageable.ReceiveDamage(damage);
            caster.ReportHit(damageable, damage, hitPoint);

            Impact(hitPoint);
            return true;
        }

        return false;
    }

    private void Impact(Vector3 at)
    {
        transform.position = at;
        if (impactVfxPrefab != null)
        {
            Instantiate(impactVfxPrefab, at, Quaternion.identity);
        }
        Destroy(gameObject);
    }

    private bool IsOwner(IDamageable damageable)
    {
        if (damageable == null) return false;
        if (spec.owner != null && damageable == spec.owner) return true;
        if (spec.ignoredTarget != null && damageable == spec.ignoredTarget) return true;
        if (Player.Instance != null && (damageable == Player.Instance.Damageable || damageable == Player.Instance.Health)) return true;
        if (caster != null && damageable is Component comp && (object)comp.transform.root == (object)caster.transform.root) return true;
        return false;
    }

    /// <summary>
    ///     A sphere cast that starts overlapping a collider reports distance 0 and a zero hit point —
    ///     fall back to the collider's closest point (safely handling non-convex mesh colliders) so impact positions stay sane.
    /// </summary>
    private Vector3 HitPointOf(RaycastHit hit, Vector3 origin)
    {
        if (hit.distance > 0f && hit.point != Vector3.zero)
        {
            return hit.point;
        }
        Collider c = hit.collider;
        if (c != null)
        {
            if (c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider mc && mc.convex))
            {
                return c.ClosestPoint(origin + spec.direction * 0.1f);
            }
            return c.bounds.ClosestPoint(origin + spec.direction * 0.1f);
        }
        return origin;
    }
}
