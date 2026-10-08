using System;
using UnityEngine;

/// <summary>
///     The target-selection layer between an enemy and what it chases/attacks. By default an enemy
///     heads for its assigned <see cref="Gate" /> (set by whatever spawned it, right
///     after Instantiate, the MarkGolden timing) or, with no
///     assignment, the nearest still-standing gate — but the player always takes priority when they
///     come within <see cref="playerEngageRange" />. In a scene with no gates (or once every gate has
///     fallen) the target is simply the player, so enemies without this component — and every
///     existing scene — behave exactly as before; <see cref="AIMovement" />, <see cref="AIAttack" />
///     and <see cref="TrollSlamAttack" /> all consult this component only when present.
///
///     Wave spawns also get a <see cref="EnemyRole" /> (<see cref="AssignSpawnRole" />, weights on
///     <see cref="EnemyRoleConfigSO" />) so a horde splits its pressure: Hunters chase the player, Guards
///     hold the active objective's object, Assault pushes to the gate and the walls in its way. Roles sit
///     below every override (wall claim, tower, formation, player in engage range or retaliation).
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
    private DefenseStructure towerTarget;

    /// <summary>
    ///     Points this enemy at a built tower (the Sapper). While the tower stands and still has
    ///     supply it beats every other target, player included, and there is no damage target, so
    ///     <see cref="AIAttack" /> stays idle and the caller does its own work at the tower.
    ///     Pass null to clear; a depleted or dismantled tower clears itself.
    /// </summary>
    public void SetTowerTarget(DefenseStructure tower)
    {
        towerTarget = tower;
    }

    /// <summary>The tower being targeted, or null when none (or it's depleted/gone).</summary>
    public DefenseStructure TowerTarget => HasTowerTarget ? towerTarget : null;

    private bool HasTowerTarget => towerTarget != null && towerTarget.isActiveAndEnabled && !towerTarget.IsDepleted;

    private WallStructure wallTarget;

    /// <summary>
    ///     Points this enemy at a shut wall it has walked up to (plan 17; the wall assigns itself to every
    ///     enemy at its outside face). While the wall blocks, it beats every other target except a player
    ///     standing on the enemy's side of it, so nothing walks through a shut door to reach the hero.
    ///     Clears itself when the wall falls or its door opens.
    /// </summary>
    public void SetWallTarget(WallStructure wall)
    {
        wallTarget = wall;
    }

    /// <summary>The wall being attacked, or null.</summary>
    public WallStructure WallTarget => wallTarget != null && wallTarget.IsBlocking ? wallTarget : null;

    private bool UseWallTarget()
    {
        if (wallTarget == null) return false;
        if (!wallTarget.IsBlocking)
        {
            wallTarget = null;
            return false;
        }
        // A player out here on the same side takes priority; one behind the wall doesn't.
        if (ShouldTargetPlayer() && Player.Instance != null && wallTarget.IsOutside(Player.Instance.transform.position)) return false;
        return true;
    }

    private WardenEscort escortLeader;
    private int escortSlot;

    /// <summary>
    ///     Puts this enemy in a Dome Warden's formation at <paramref name="slot" />. While the Warden
    ///     holds formation, the slot beats the player (and there's no swing target); once the Warden
    ///     calls its escorts to engage, normal targeting resumes. Pass null to leave the formation.
    /// </summary>
    public void SetEscortLeader(WardenEscort leader, int slot)
    {
        escortLeader = leader;
        escortSlot = slot;
    }

    /// <summary>The Warden this enemy is escorting, or null.</summary>
    public WardenEscort EscortLeader => escortLeader != null && escortLeader.IsLeading ? escortLeader : null;

    private bool HoldingFormation => EscortLeader != null && !escortLeader.EscortsEngage;

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

    // ---- Roles (Hunter / Guard / Assault) ---------------------------------------------------

    private EnemyRole role = EnemyRole.None;
    private float guardAngleDegrees;
    private float guardRingRadius;
    private Vector3 guardAnchor;
    private bool hasGuardAnchor;
    private float nextGuardAnchorTime = Mathf.NegativeInfinity;

    /// <summary>The spawn role this enemy was given (None = legacy targeting).</summary>
    public EnemyRole Role => role;

    /// <summary>
    ///     Sets this enemy's role. <see cref="EnemyRole.None" /> keeps the legacy targeting. Guards get a
    ///     random post on the ring round the objective object.
    /// </summary>
    public void SetRole(EnemyRole newRole)
    {
        role = newRole;
        if (newRole == EnemyRole.Guard)
        {
            EnemyRoleConfigSO config = EnemyRoleConfigSO.Current;
            guardAngleDegrees = UnityEngine.Random.Range(0f, 360f);
            guardRingRadius = UnityEngine.Random.Range(config.guardRingMin, config.guardRingMax);
            nextGuardAnchorTime = Mathf.NegativeInfinity;
        }
    }

    /// <summary>
    ///     Rolls and sets a spawn role on a freshly spawned wave enemy (<see cref="SurvivorsSpawner" />), from
    ///     <see cref="EnemyRoleConfigSO" /> weights. Units with their own targeting job stay
    ///     <see cref="EnemyRole.None" />: siege units, sappers, hexers, captains, bosses, the golden goblin
    ///     and anything set to ignore the player.
    /// </summary>
    public static EnemyRole AssignSpawnRole(GameObject enemy)
    {
        if (enemy == null || !enemy.TryGetComponent(out AITargetSelector selector)) return EnemyRole.None;
        if (!IsRoleEligible(selector))
        {
            selector.SetRole(EnemyRole.None);
            return EnemyRole.None;
        }

        EnemyRole rolled = EnemyRoleConfigSO.Current.Roll(TryGetGuardAnchor(enemy.transform.position, out _));
        selector.SetRole(rolled);
        return rolled;
    }

    private static bool IsRoleEligible(AITargetSelector selector)
    {
        GameObject go = selector.gameObject;
        if (selector.ignorePlayer) return false;
        if (WallNavCost.IsSiege(go)) return false; // SiegeUnit, troll, sapper, ram, captains
        if (go.GetComponent<TowerHexer>() != null) return false;
        if (go.GetComponent<SlayerDashAttack>() != null) return false; // dashes at whatever TargetPosition is, post included
        if (go.GetComponent<GoldenGoblinFlee>() != null) return false;
        if (go.TryGetComponent(out GoldenGoblin golden) && golden.IsGolden) return false;
        if (go.GetComponent<NecromancerBossController>() != null || go.GetComponent<PrincessBossController>() != null) return false;
        return true;
    }

    /// <summary>
    ///     The active objective's object for Guards to hold (nearest cage, siege engine, the wagon), or false
    ///     when the objective has none. The battering ram is excluded (Assault escorts it) and so is the
    ///     golden goblin (its position is the quarry, not something to defend).
    /// </summary>
    public static bool TryGetGuardAnchor(Vector3 from, out Vector3 anchor)
    {
        anchor = default;
        SurvivorsObjectiveManager manager = SurvivorsObjectiveManager.Instance;
        ISurvivorsObjective objective = manager != null ? manager.CurrentObjective : null;
        if (objective == null || !objective.IsActive) return false;
        if (objective is StopBatteringRamObjective || objective is GoldenGoblinObjective) return false;

        Vector3? position = objective.GetObjectiveTargetPosition(from);
        if (!position.HasValue) return false;
        anchor = position.Value;
        return true;
    }

    /// <summary>Cached guard anchor, re-read every <see cref="EnemyRoleConfigSO.guardAnchorRefreshSeconds" />.</summary>
    private bool RefreshGuardAnchor()
    {
        if (Time.time >= nextGuardAnchorTime)
        {
            nextGuardAnchorTime = Time.time + EnemyRoleConfigSO.Current.guardAnchorRefreshSeconds;
            hasGuardAnchor = TryGetGuardAnchor(transform.position, out guardAnchor);
        }
        return hasGuardAnchor;
    }

    /// <summary>The role actually in force: overrides (ignore-player, an assigned gate) drop to None; a Guard with nothing left to guard assaults.</summary>
    private EnemyRole EffectiveRole
    {
        get
        {
            if (role == EnemyRole.None || ignorePlayer) return EnemyRole.None;
            if (assignedGate != null && !assignedGate.IsDestroyed) return EnemyRole.None;
            if (role == EnemyRole.Guard && !RefreshGuardAnchor()) return EnemyRole.Assault;
            return role;
        }
    }

    private Vector3 GuardPost()
    {
        EnemyRoleConfigSO config = EnemyRoleConfigSO.Current;
        float angle = (guardAngleDegrees + Time.time * config.guardPatrolDegreesPerSecond) * Mathf.Deg2Rad;
        return guardAnchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * guardRingRadius;
    }

    private bool PlayerNearGuardAnchor()
    {
        Player p = Player.Instance;
        if (p == null || p.Health == null || p.Health.IsDead) return false;
        float r = EnemyRoleConfigSO.Current.guardEngageRadius;
        return (p.transform.position - guardAnchor).sqrMagnitude <= r * r;
    }

    // ---- Target resolution ---------------------------------------------------------------------

    private enum TargetKind
    {
        Wall,
        Tower,
        Formation,
        Player,
        RamEscort,
        Gate,
        GuardPost,
        /// <summary>No live player and no gate: the player's position/damageable if any, else self.</summary>
        Fallback
    }

    /// <summary>
    ///     The single priority chain behind <see cref="IsTargetingPlayer" />, <see cref="TargetPosition" /> and
    ///     <see cref="TargetDamageable" />. Overrides first (wall claim, sapper/hexer tower, Warden formation,
    ///     player in engage range or retaliation), then the role: Hunter: the player; Guard: engage a player
    ///     near its object, else hold its post; Assault: ram escort, then the gate. Role None keeps the
    ///     legacy chain (ram escort, the gate during Hold the Gate, else the player).
    /// </summary>
    private TargetKind Resolve(out Vector3 point, out Gate gate)
    {
        point = default;
        gate = null;

        if (UseWallTarget()) return TargetKind.Wall;
        if (HasTowerTarget) return TargetKind.Tower;
        if (HoldingFormation) return TargetKind.Formation;
        if (ShouldTargetPlayer()) return TargetKind.Player;

        Player p = Player.Instance;
        bool playerAlive = p != null && p.Health != null && !p.Health.IsDead && !ignorePlayer;

        switch (EffectiveRole)
        {
            case EnemyRole.Hunter:
                if (playerAlive) return TargetKind.Player;
                break;

            case EnemyRole.Guard:
                if (PlayerNearGuardAnchor()) return TargetKind.Player;
                point = GuardPost();
                return TargetKind.GuardPost;

            case EnemyRole.Assault:
                if (TryGetRamEscortTarget(out point)) return TargetKind.RamEscort;
                gate = ResolveGate();
                if (gate != null) return TargetKind.Gate;
                break;

            default:
                if (TryGetRamEscortTarget(out point)) return TargetKind.RamEscort;
                if (IsDefendingGate())
                {
                    gate = ResolveGate();
                    if (gate != null) return TargetKind.Gate;
                }
                break;
        }

        if (playerAlive) return TargetKind.Player;
        gate = ResolveGate();
        return gate != null ? TargetKind.Gate : TargetKind.Fallback;
    }

    /// <summary>True when the current target is the player rather than a gate, wall, tower or post.</summary>
    public bool IsTargetingPlayer
    {
        get
        {
            TargetKind kind = Resolve(out _, out _);
            return kind == TargetKind.Player || kind == TargetKind.Fallback;
        }
    }

    /// <summary>The point to path toward / measure attack range from.</summary>
    public Vector3 TargetPosition
    {
        get
        {
            switch (Resolve(out Vector3 point, out Gate gate))
            {
                case TargetKind.Wall: return wallTarget.GetAttackPoint(transform.position);
                case TargetKind.Tower: return towerTarget.transform.position;
                case TargetKind.Formation: return escortLeader.GetSlotPosition(escortSlot);
                case TargetKind.RamEscort:
                case TargetKind.GuardPost: return point;
                case TargetKind.Gate: return gate.TargetPosition;
                default:
                    Player p = Player.Instance;
                    return p != null ? p.transform.position : transform.position;
            }
        }
    }

    /// <summary>Enemies no longer flock to objectives; they path to the player or gate (Guards hold posts via their role).</summary>
    public bool IsFlockingToObjective => false;

    /// <summary>
    ///     The current target's damage sink (the gate's Health, a wall's, or the player's). Null when there
    ///     is nothing to swing at: a sapper/hexer tower, a Warden formation slot, the ram escort, a guard post.
    /// </summary>
    public IDamageable TargetDamageable
    {
        get
        {
            switch (Resolve(out _, out Gate gate))
            {
                case TargetKind.Wall: return wallTarget.Damageable;
                case TargetKind.Gate: return gate.Damageable;
                case TargetKind.Player:
                case TargetKind.Fallback:
                    Player p = Player.Instance;
                    return p != null ? p.Damageable : null;
                default: return null;
            }
        }
    }

    /// <summary>
    /// Checks whether the enemy should target the gate (e.g. assigned gate, Slayer charging gate, or when defending the gate in Hold the Gate).
    /// </summary>
    private bool IsDefendingGate()
    {
        if (assignedGate != null && !assignedGate.IsDestroyed) return true;
        if (ignorePlayer) return true;

        if (SurvivorsObjectiveManager.Instance != null && SurvivorsObjectiveManager.Instance.CurrentObjective != null)
        {
            var currentObj = SurvivorsObjectiveManager.Instance.CurrentObjective;
            if (currentObj.IsActive)
            {
                if (currentObj is KillEnemiesObjective) return true;
                if (!string.IsNullOrEmpty(currentObj.Title) && currentObj.Title.IndexOf("Hold the Gate", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if there is an active Battering Ram objective with an alive ram that hasn't reached the gate yet.
    /// Returns the escort/push formation position just ahead of the ram.
    /// </summary>
    private bool TryGetRamEscortTarget(out Vector3 escortTarget)
    {
        escortTarget = Vector3.zero;
        if (SurvivorsObjectiveManager.Instance != null && SurvivorsObjectiveManager.Instance.CurrentObjective != null)
        {
            var currentObj = SurvivorsObjectiveManager.Instance.CurrentObjective;
            if (currentObj.IsActive && currentObj is StopBatteringRamObjective ramObj)
            {
                BatteringRam ram = ramObj.CurrentRam;
                if (ram != null && !ram.IsDestroyed && !ram.HasReachedGate)
                {
                    escortTarget = ram.GetEscortTargetPosition(transform.position, GetInstanceID());
                    return true;
                }
            }
        }
        return false;
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
