using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     One thrown axe in flight — a real, slow projectile that carries the throw's damage (it is
///     *not* the cosmetic tracer it grew out of; if no axe flies, nothing gets hit).
///     <see cref="PlayerThrownAxe" /> instantiates one per throw and calls <see cref="Launch" />
///     with everything captured at release time (charge, pierce budget, Pain into Power bonus).
///
///     Each <c>FixedUpdate</c> the axe advances along its line and **sphere casts from its previous
///     position to its current one** (radius = <see cref="StatType.AxeThrowWidth" /> / 2 — the
///     "Wide Arc" nodes literally widen the swept volume), so a fast tick or thin collider can't
///     tunnel through unhit. Every unique enemy swept is damaged via
///     <see cref="PlayerThrownAxe.CreateHitDamage" /> (fresh crit roll per target, the
///     <see cref="DamageTrigger" /> convention) until the pierce budget runs out; solid environment
///     lodges the axe.
///
///     With the "Boomerang" node (<see cref="StatType.AxeBoomerangUnlocked" />), the axe never
///     lodges: striking terrain, exhausting its pierce, or reaching max range turns it around and it
///     homes back to the thrower's hand, damaging enemies on the return leg too (fresh target set
///     and pierce budget — enemies can be hit once per leg). The return ignores terrain (it flies
///     home regardless) and despawns on catch.
///
///     The prefab owns all the looks (mesh, trail, spin tunables below).
/// </summary>
public class AxeProjectile : MonoBehaviour, IPlayerProjectile
{
    /// <summary>Current world position, for the whirlwind's radius check.</summary>
    public Vector3 Position => transform.position;

    /// <summary>
    ///     Destroys the axe mid-flight without landing more hits (the Barbarian Giant's whirlwind).
    ///     Safe — the axe already destroys itself mid-flight liberally, and
    ///     <see cref="PlayerThrownAxe" /> keeps no in-flight reference.
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

    /// <summary>Everything a throw decides at release time, captured so the axe stays truthful to the wind-up that launched it.</summary>
    public struct LaunchSpec
    {
        public Vector3 origin;
        public Vector3 direction;
        /// <summary>Outbound flight speed in metres per second.</summary>
        public float speed;
        /// <summary>Flight distance at which the axe stops (or turns around, with boomerang).</summary>
        public float maxRange;
        /// <summary>Radius of the swept damage volume — AxeThrowWidth / 2.</summary>
        public float radius;
        /// <summary>Unique enemies one leg may damage before the axe is spent.</summary>
        public int pierceBudget;
        public LayerMask hitLayers;
        /// <summary>Charge level held when thrown — damage/knockback read it per hit.</summary>
        public int chargeLevel;
        /// <summary>Pain into Power flat bonus consumed by this throw, shared by every target.</summary>
        public float painBonus;
        /// <summary>The thrower's own IDamageable — never hit (the DamageTrigger owner idiom).</summary>
        public IDamageable owner;
        /// <summary>An extra fly-through target — the horse under a mounted thrower. Null = none.</summary>
        public IDamageable ignoredTarget;
        /// <summary>True once the "Boomerang" node is bought: return instead of lodging.</summary>
        public bool boomerang;
        /// <summary>Return speed as a multiple of the outbound speed.</summary>
        public float returnSpeedMultiplier;
        /// <summary>Where a boomeranging axe flies home to (the throw origin — it tracks the player).</summary>
        public Transform returnTarget;
        /// <summary>Uniform prop scale so "Wide Arc" width upgrades read visually (the SwordRange localScale convention).</summary>
        public float visualScale;
    }

    [Tooltip("Local axis the axe tumbles around in flight.")]
    [SerializeField] private Vector3 spinAxis = Vector3.right;
    [Tooltip("Tumble speed in degrees per second.")]
    [SerializeField] private float spinDegreesPerSecond = 1080f;
    [Tooltip("Seconds the axe stays visible where it lodges before despawning (non-boomerang throws).")]
    [SerializeField] private float lingerSeconds = 1.5f;
    [Tooltip("Metres from the return target at which a boomeranging axe counts as caught and despawns.")]
    [SerializeField] private float catchRadius = 0.75f;
    [Tooltip("Safety despawn for a boomeranging axe whose return target vanished mid-flight.")]
    [SerializeField] private float maxLifetimeSeconds = 15f;

    private enum FlightState
    {
        Outbound,
        Returning,
        Lodged,
    }

    private const int MaxCastHits = 64;

    private readonly RaycastHit[] castBuffer = new RaycastHit[MaxCastHits];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private PlayerThrownAxe thrower;
    private LaunchSpec spec;
    private FlightState state = FlightState.Lodged;
    private float travelled;
    private int damaged;
    private bool launched;

    /// <summary>Sends the axe flying. Damage happens as it travels; the prefab is destroyed when it lodges, is caught, or times out.</summary>
    public void Launch(PlayerThrownAxe thrower, LaunchSpec spec)
    {
        this.thrower = thrower;
        this.spec = spec;
        state = FlightState.Outbound;
        travelled = 0f;
        damaged = 0;
        hitTargets.Clear();
        launched = true;

        transform.position = spec.origin;
        if (spec.direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(spec.direction);
        }
        if (spec.visualScale > 0f)
        {
            transform.localScale = Vector3.one * spec.visualScale;
        }

        Destroy(gameObject, maxLifetimeSeconds);
    }

    private void Update()
    {
        if (!launched || state == FlightState.Lodged)
        {
            return;
        }
        transform.Rotate(spinAxis, spinDegreesPerSecond * Time.deltaTime, Space.Self);
    }

    private void FixedUpdate()
    {
        if (!launched || state == FlightState.Lodged)
        {
            return;
        }

        Vector3 from = transform.position;
        Vector3 direction;
        float speed;

        if (state == FlightState.Outbound)
        {
            direction = spec.direction;
            speed = spec.speed;
        }
        else
        {
            if (spec.returnTarget == null)
            {
                // The hand it was flying back to is gone (scene teardown) — just vanish.
                Destroy(gameObject);
                return;
            }
            Vector3 home = spec.returnTarget.position - from;
            if (home.magnitude <= catchRadius)
            {
                Destroy(gameObject);
                return;
            }
            direction = home.normalized;
            speed = spec.speed * Mathf.Max(0.1f, spec.returnSpeedMultiplier);
        }

        float step = speed * Time.fixedDeltaTime;
        if (state == FlightState.Outbound)
        {
            // Clamp the last tick to the range line so boomerangs turn around exactly at max range.
            step = Mathf.Min(step, spec.maxRange - travelled);
            travelled += step;
        }
        Vector3 to = from + direction * step;

        if (SweepDamage(from, direction, step))
        {
            // The sweep ended the leg (lodged or turned around) and already placed the axe.
            return;
        }

        transform.position = to;

        if (state == FlightState.Outbound && travelled >= spec.maxRange)
        {
            EndOutboundLeg(to);
        }
    }

    /// <summary>
    ///     Sphere-casts the tick's travel and damages every fresh enemy along it. Returns true when
    ///     the leg ended inside the sweep (environment hit or pierce spent) and the axe was re-placed.
    /// </summary>
    private bool SweepDamage(Vector3 from, Vector3 direction, float step)
    {
        int count = Physics.SphereCastNonAlloc(from, spec.radius, direction, castBuffer, step, spec.hitLayers, QueryTriggerInteraction.Collide);
        Array.Sort(castBuffer, 0, count, PlayerThrownAxe.HitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = castBuffer[i];
            IDamageable damageable = PlayerThrownAxe.ResolveDamageable(hit.collider);

            if (damageable == null)
            {
                // Trigger colliders with no damageable (coins, orbs, hitboxes) never stop the axe;
                // solid environment ends the outbound leg. The return leg flies home through terrain
                // (a homing arc has no honest collision line to respect).
                if (!hit.collider.isTrigger && state == FlightState.Outbound)
                {
                    // Initial overlap with environment at the launch position (distance == 0)
                    // occurs when the axe sweep touches the floor beneath the wielder; skip it.
                    if (hit.distance <= 0.0001f)
                    {
                        continue;
                    }

                    EndOutboundLeg(HitPointOf(hit, from, direction));
                    return true;
                }
                continue;
            }

            if (IsOwner(damageable) || !hitTargets.Add(damageable))
            {
                continue;
            }
            if (damaged >= spec.pierceBudget)
            {
                continue;
            }

            if (thrower == null)
            {
                // Thrower gone mid-flight (scene teardown) — stop dealing damage.
                Destroy(gameObject);
                return true;
            }

            Vector3 hitPoint = HitPointOf(hit, from, direction);
            Damage damage = thrower.CreateHitDamage(spec.chargeLevel, spec.painBonus, from);
            damageable.ReceiveDamage(damage);
            thrower.ReportHit(damageable, damage, hitPoint);
            damaged++;

            if (damaged >= spec.pierceBudget && state == FlightState.Outbound)
            {
                // Out of penetration: the axe stops in this target (or swings back around).
                EndOutboundLeg(hitPoint);
                return true;
            }
            // A spent return leg keeps flying home, it just stops hurting anyone new.
        }

        return false;
    }

    /// <summary>The outbound leg is over: lodge and despawn, or — with Boomerang — turn around with a fresh target set and pierce budget.</summary>
    private void EndOutboundLeg(Vector3 at)
    {
        transform.position = at;

        if (!spec.boomerang || spec.returnTarget == null)
        {
            state = FlightState.Lodged;
            Destroy(gameObject, lingerSeconds);
            return;
        }

        state = FlightState.Returning;
        hitTargets.Clear();
        damaged = 0;
    }

    private bool IsOwner(IDamageable damageable)
    {
        if (damageable == null) return false;
        if (spec.owner != null && damageable == spec.owner) return true;
        if (spec.ignoredTarget != null && damageable == spec.ignoredTarget) return true;
        if (Player.Instance != null && (damageable == Player.Instance.Damageable || damageable == Player.Instance.Health)) return true;
        if (thrower != null && damageable is Component comp && (object)comp.transform.root == (object)thrower.transform.root) return true;
        return false;
    }

    /// <summary>
    ///     A sphere cast that starts overlapping a collider reports distance 0 and a zero hit point —
    ///     fall back to the collider's closest point (safely handling non-convex mesh colliders) so feedback/lodge positions stay sane.
    /// </summary>
    private static Vector3 HitPointOf(RaycastHit hit, Vector3 origin, Vector3 direction)
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
                return c.ClosestPoint(origin + direction * 0.1f);
            }
            return c.bounds.ClosestPoint(origin + direction * 0.1f);
        }
        return origin;
    }
}
