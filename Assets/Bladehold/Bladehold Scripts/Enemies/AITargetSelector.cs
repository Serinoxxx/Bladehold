using UnityEngine;

/// <summary>
///     The target-selection layer between an enemy and what it chases/attacks. By default an enemy
///     heads for its assigned <see cref="Gate" /> (set by whatever spawned it, e.g.
///     <see cref="GateAssaultSpawner" /> — the MarkGolden right-after-Instantiate timing) or, with no
///     assignment, the nearest still-standing gate — but the player always takes priority when they
///     come within <see cref="playerEngageRange" />. In a scene with no gates (or once every gate has
///     fallen) the target is simply the player, so enemies without this component — and every
///     existing scene — behave exactly as before; <see cref="AIMovement" />, <see cref="AIAttack" />
///     and <see cref="TrollSlamAttack" /> all consult this component only when present.
///
///     Targets are resolved on demand (a couple of distance checks), so there is no per-frame cost
///     beyond what callers already do.
/// </summary>
public class AITargetSelector : MonoBehaviour
{
    [Tooltip("Distance within which the player becomes the target even when a gate is assigned; beyond it the enemy heads for its gate.")]
    [SerializeField] private float playerEngageRange = 8f;

    private Gate assignedGate;

    /// <summary>
    ///     Assigns the gate this enemy beelines for. Call right after Instantiate (the MarkGolden
    ///     timing trick); pass null to fall back to nearest-gate targeting.
    /// </summary>
    public void AssignGate(Gate gate)
    {
        assignedGate = gate;
    }

    /// <summary>True when the current target is the player rather than a gate.</summary>
    public bool IsTargetingPlayer => ResolveGate() == null;

    /// <summary>The point to path toward / measure attack range from.</summary>
    public Vector3 TargetPosition
    {
        get
        {
            Gate gate = ResolveGate();
            if (gate != null)
            {
                return gate.TargetPosition;
            }
            Player player = Player.Instance;
            return player != null ? player.transform.position : transform.position;
        }
    }

    /// <summary>The current target's damage sink (the gate's Health, or the player's). Null if neither exists.</summary>
    public IDamageable TargetDamageable
    {
        get
        {
            Gate gate = ResolveGate();
            if (gate != null)
            {
                return gate.Damageable;
            }
            Player player = Player.Instance;
            return player != null ? player.Damageable : null;
        }
    }

    /// <summary>The gate to target right now, or null when the player is (engaged, dead gates, no gates).</summary>
    private Gate ResolveGate()
    {
        Gate gate = assignedGate != null && !assignedGate.IsDestroyed
            ? assignedGate
            : Gate.NearestAlive(transform.position);
        if (gate == null)
        {
            return null;
        }

        // A living player inside engage range always wins over the gate.
        Player player = Player.Instance;
        if (player != null && player.Health != null && !player.Health.IsDead)
        {
            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance <= playerEngageRange * playerEngageRange)
            {
                return null;
            }
        }
        return gate;
    }
}
