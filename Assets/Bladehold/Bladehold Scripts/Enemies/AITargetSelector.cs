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
    [Tooltip("When true, this enemy will ignore the player and strictly head for its gate unless a temporary player target override is active.")]
    [SerializeField] private bool ignorePlayer = false;

    private Gate assignedGate;
    private float playerOverrideUntilTime = Mathf.NegativeInfinity;

    /// <summary>When true, this enemy ignores player proximity and prioritizes the gate.</summary>
    public bool IgnorePlayer
    {
        get => ignorePlayer;
        set => ignorePlayer = value;
    }

    /// <summary>
    ///     Temporarily forces this enemy to target the player for the specified duration (e.g. retaliation on damage).
    /// </summary>
    public void SetPlayerTargetOverride(float durationSeconds)
    {
        playerOverrideUntilTime = Time.time + durationSeconds;
    }

    /// <summary>
    ///     Assigns the gate this enemy beelines for. Call right after Instantiate (the MarkGolden
    ///     timing trick); pass null to fall back to nearest-gate targeting.
    /// </summary>
    public void AssignGate(Gate gate)
    {
        assignedGate = gate;
    }

    /// <summary>True when the current target is the player rather than a gate or objective point.</summary>
    public bool IsTargetingPlayer
    {
        get
        {
            if (ShouldTargetPlayer()) return true;
            if (TryGetObjectiveTarget(out _, out _)) return false;
            return ResolveGate() == null;
        }
    }

    /// <summary>The point to path toward / measure attack range from.</summary>
    public Vector3 TargetPosition
    {
        get
        {
            if (ShouldTargetPlayer())
            {
                Player player = Player.Instance;
                return player != null ? player.transform.position : transform.position;
            }

            if (TryGetObjectiveTarget(out Vector3 objPos, out _))
            {
                return objPos;
            }

            Gate gate = ResolveGate();
            if (gate != null)
            {
                return gate.TargetPosition;
            }
            Player fallbackPlayer = Player.Instance;
            return fallbackPlayer != null ? fallbackPlayer.transform.position : transform.position;
        }
    }

    /// <summary>True when flocking to an objective point (not targeting player or gate).</summary>
    public bool IsFlockingToObjective => !ShouldTargetPlayer() && TryGetObjectiveTarget(out _, out _);

    /// <summary>The current target's damage sink (the gate's Health, or the player's). Null if none exists or flocking to an objective.</summary>
    public IDamageable TargetDamageable
    {
        get
        {
            if (ShouldTargetPlayer())
            {
                Player player = Player.Instance;
                return player != null ? player.Damageable : null;
            }

            if (TryGetObjectiveTarget(out _, out _))
            {
                // Flocking to an objective: enemies do NOT attack the objective, they only wait there.
                return null;
            }

            Gate gate = ResolveGate();
            if (gate != null)
            {
                return gate.Damageable;
            }
            Player fallbackPlayer = Player.Instance;
            return fallbackPlayer != null ? fallbackPlayer.Damageable : null;
        }
    }

    private bool ShouldTargetPlayer()
    {
        if (Time.time < playerOverrideUntilTime)
        {
            Player player = Player.Instance;
            if (player != null && player.Health != null && !player.Health.IsDead)
            {
                return true;
            }
        }

        if (!ignorePlayer)
        {
            Player activePlayer = Player.Instance;
            if (activePlayer != null && activePlayer.Health != null && !activePlayer.Health.IsDead)
            {
                float sqrDistance = (activePlayer.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance <= playerEngageRange * playerEngageRange)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetObjectiveTarget(out Vector3 objPos, out IDamageable objDmg)
    {
        objPos = Vector3.zero;
        objDmg = null;
        if (SurvivorsObjectiveManager.Instance != null && SurvivorsObjectiveManager.Instance.CurrentObjective != null)
        {
            var obj = SurvivorsObjectiveManager.Instance.CurrentObjective;
            if (obj.IsActive)
            {
                Vector3? pos = obj.GetObjectiveTargetPosition(transform.position);
                if (pos.HasValue)
                {
                    objPos = pos.Value;
                    objDmg = obj.GetObjectiveDamageable(transform.position);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>The gate to target right now, or null when no gate is alive.</summary>
    private Gate ResolveGate()
    {
        return assignedGate != null && !assignedGate.IsDestroyed
            ? assignedGate
            : Gate.NearestAlive(transform.position);
    }
}
