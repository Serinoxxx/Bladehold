using System;
using UnityEngine;

/// <summary>
///     One arrow in flight — a real projectile with travel speed and gravity drop (the
///     <see cref="AxeProjectile" /> convention; it replaced the bow's original hitscan, whose
///     <see cref="BowTracer" /> streak only *looked* like travel). <see cref="PlayerBow" />
///     instantiates one per arrow and calls <see cref="Launch" /> with everything captured at release
///     time (charge level, damage scale, the Pickup/Unstable Orbs flags).
///
///     Each <c>FixedUpdate</c> gravity pulls the velocity down, then the arrow **sphere casts from
///     its previous position to its current one** (radius = <see cref="BowSO.arrowRadius" />) so a
///     fast tick or thin collider can't tunnel through unhit. The wielder, the ignored mount target,
///     and trigger colliders with no damageable (coins, orbs, hitboxes) never stop it. The first
///     damageable struck is handed back to <see cref="PlayerBow.ApplyArrowHit" /> — damage, crit,
///     and every arrow skill line stay the bow's business; solid environment just lodges the arrow.
///     While flying it also runs the per-path skills (Pickup Arrows collection, Unstable Orbs
///     detonation) segment by segment, so a dropping arc collects along the *curve* it actually flew.
///
///     Arrow speed comes from <see cref="StatType.BowArrowSpeed" /> and gravity is fixed, so the
///     "Swift Arrows" nodes flatten the arc physically: twice the speed halves the flight time and
///     quarters the drop. The prefab owns all the looks (mesh, trail, linger tunables below).
/// </summary>
public class ArrowProjectile : MonoBehaviour, IPlayerProjectile
{
    /// <summary>Current world position, for the whirlwind's radius check.</summary>
    public Vector3 Position => transform.position;

    /// <summary>
    ///     Destroys the arrow mid-flight without landing its hit (the Barbarian Giant's whirlwind).
    ///     Safe — <see cref="PlayerBow" /> keeps no in-flight reference.
    /// </summary>
    public void Shatter()
    {
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

    /// <summary>Everything a shot decides at release time, captured so the arrow stays truthful to the draw that loosed it (the <see cref="AxeProjectile.LaunchSpec" /> convention).</summary>
    public struct LaunchSpec
    {
        public Vector3 origin;
        public Vector3 direction;
        /// <summary>Muzzle speed in metres per second — the effective BowArrowSpeed stat.</summary>
        public float speed;
        /// <summary>Downward acceleration in m/s² (BowSO.arrowGravity).</summary>
        public float gravity;
        /// <summary>Path length at which the arrow despawns unspent.</summary>
        public float maxRange;
        /// <summary>Radius of the swept damage volume (BowSO.arrowRadius).</summary>
        public float radius;
        public LayerMask hitLayers;
        /// <summary>Draw level held when fired — damage/impulse read it per hit.</summary>
        public int chargeLevel;
        /// <summary>Damage fraction of the main arrow (Multi Shot extras fly at less than 1).</summary>
        public float damageScale;
        /// <summary>True for the main arrow of a shot — only it detonates Unstable Orbs.</summary>
        public bool isMainArrow;
        /// <summary>The wielder's own IDamageable — never hit (the DamageTrigger owner idiom).</summary>
        public IDamageable owner;
        /// <summary>An extra fly-through target — the horse under a mounted archer. Null = none.</summary>
        public IDamageable ignoredTarget;
        /// <summary>True once Pickup Arrows is bought: collect coins/orbs along each flight segment.</summary>
        public bool collectPickups;
        /// <summary>True for a main arrow once Unstable Orbs is bought: detonate orbs along each flight segment.</summary>
        public bool detonateOrbs;
    }

    [Tooltip("Seconds the arrow stays visible where it lodges before despawning.")]
    [SerializeField] private float lingerSeconds = 1.5f;
    [Tooltip("Safety despawn for an arrow that somehow never lands (fired over a cliff).")]
    [SerializeField] private float maxLifetimeSeconds = 15f;

    private const int MaxCastHits = 64;

    private readonly RaycastHit[] castBuffer = new RaycastHit[MaxCastHits];

    private PlayerBow bow;
    private LaunchSpec spec;
    private Vector3 velocity;
    private float travelled;
    private bool launched;
    private bool lodged;

    /// <summary>Sends the arrow flying. Damage happens where it lands; the prefab is destroyed when it lodges, runs out of range, or times out.</summary>
    public void Launch(PlayerBow bow, LaunchSpec spec)
    {
        this.bow = bow;
        this.spec = spec;
        velocity = spec.direction.normalized * spec.speed;
        travelled = 0f;
        launched = true;
        lodged = false;

        transform.position = spec.origin;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        Destroy(gameObject, maxLifetimeSeconds);
    }

    private void FixedUpdate()
    {
        if (!launched || lodged)
        {
            return;
        }

        if (bow == null)
        {
            // The bow that fired it is gone (scene teardown, class switch) — stop dealing damage.
            Destroy(gameObject);
            return;
        }

        // Gravity bends the arc; speed is never re-read mid-flight, so a shot stays truthful to the
        // stats it was fired with.
        velocity += Vector3.down * (spec.gravity * Time.fixedDeltaTime);

        Vector3 from = transform.position;
        Vector3 step = velocity * Time.fixedDeltaTime;
        float stepLength = step.magnitude;
        Vector3 to = from + step;

        // Per-path skills sweep the segment actually flown this tick, so a dropping arc collects
        // along its real curve (both are idempotent per pickup/orb — TryCollect/TryDetonate guard).
        if (spec.collectPickups)
        {
            bow.CollectPickupsAlongPath(from, to);
        }
        if (spec.detonateOrbs)
        {
            bow.DetonateOrbsAlongPath(from, to, spec.damageScale, spec.chargeLevel);
        }

        if (stepLength > 0.0001f && Sweep(from, step / stepLength, stepLength))
        {
            // The sweep landed the arrow (target or terrain) and already placed it.
            return;
        }

        transform.position = to;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        travelled += stepLength;
        if (travelled >= spec.maxRange)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    ///     Sphere-casts the tick's travel: skips the wielder, the ignored mount, and stray
    ///     pickup/ability triggers, stopping at the first damageable target (handed back to
    ///     <see cref="PlayerBow.ApplyArrowHit" />) or solid piece of environment. Returns true when
    ///     the arrow landed and was re-placed.
    /// </summary>
    private bool Sweep(Vector3 from, Vector3 direction, float distance)
    {
        int count = Physics.SphereCastNonAlloc(from, spec.radius, direction, castBuffer, distance, spec.hitLayers, QueryTriggerInteraction.Collide);
        Array.Sort(castBuffer, 0, count, PlayerBow.HitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castBuffer[i];
            IDamageable damageable = PlayerBow.ResolveDamageable(hit.collider);

            if (damageable != null && (damageable == spec.owner || (spec.ignoredTarget != null && damageable == spec.ignoredTarget)))
            {
                continue;
            }

            if (damageable != null)
            {
                // The body capsule can sit in front of a head sphere along the same sweep — check
                // every hit this cast scored on the target for a VulnerableSpot (the hitscan rule).
                bool hitVulnerableSpot = false;
                VulnerableSpot vulnerableSpot = null;
                for (int j = i; j < count; j++)
                {
                    if (PlayerBow.ResolveDamageable(castBuffer[j].collider) != damageable)
                    {
                        continue;
                    }
                    VulnerableSpot spot = castBuffer[j].collider.GetComponentInParent<VulnerableSpot>();
                    if (spot != null)
                    {
                        hitVulnerableSpot = true;
                        vulnerableSpot = spot;
                        break;
                    }
                }

                Vector3 hitPoint = HitPointOf(hit, from, direction);
                Lodge(hitPoint, direction);
                bow.ApplyArrowHit(damageable, hitPoint, direction, hit.collider, hitVulnerableSpot, vulnerableSpot, spec.damageScale, spec.chargeLevel);
                return true;
            }

            // Trigger colliders with no damageable (coins, orbs, hitboxes) never stop an arrow.
            if (!hit.collider.isTrigger)
            {
                Lodge(HitPointOf(hit, from, direction), direction);
                return true;
            }
        }
        return false;
    }

    /// <summary>The arrow stops here: freeze it in place (trail and all) and despawn after the linger.</summary>
    private void Lodge(Vector3 at, Vector3 direction)
    {
        lodged = true;
        transform.position = at;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
        Destroy(gameObject, lingerSeconds);
    }

    /// <summary>
    ///     A sphere cast that starts overlapping a collider reports distance 0 and a zero hit point —
    ///     fall back to the collider's closest point so feedback/lodge positions stay sane (the
    ///     AxeProjectile rule).
    /// </summary>
    private static Vector3 HitPointOf(RaycastHit hit, Vector3 origin, Vector3 direction)
    {
        if (hit.distance > 0f)
        {
            return hit.point;
        }
        return hit.collider.ClosestPoint(origin + direction * 0.1f);
    }
}
