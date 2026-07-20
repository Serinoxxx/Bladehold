using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

public class AIMovement : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] AIMovementSO movementSO;
    [SerializeField] Health health;
    [Tooltip("Optional target-selection layer (gate defense). Without it, the agent chases the player as before.")]
    [SerializeField] AITargetSelector targetSelector;
    [Tooltip("Optional: played once when this enemy starts chasing (spawn aggro bark/growl).")]
    [SerializeField] MMF_Player aggroFeedback;

    Player player;
    Health playerHealth;

    bool isDead = false;
    bool playerDead = false;
    bool isPaused = false;
    bool anyError = false;
    float? speedOverride;
    float speedMultiplier = 1f;

    /// <summary>
    ///     Per-instance agent-speed override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate, before Start runs; the shared
    ///     <see cref="AIMovementSO" /> is never mutated.
    /// </summary>
    public void SetSpeed(float value)
    {
        speedOverride = value;
    }

    /// <summary>
    ///     This enemy's unslowed agent speed (the roster override or the SO value, times any
    ///     <see cref="SetSpeedMultiplier" /> in effect) — what <see cref="SlowStatus" /> scales from
    ///     and restores to, so its own writes to <c>agent.speed</c> never compound. Including the
    ///     multiplier here means a slow and a speed burst (the Bomber's lit fuse) compose instead
    ///     of overwriting each other.
    /// </summary>
    public float BaseSpeed => (speedOverride ?? (movementSO != null ? movementSO.speed : 0f)) * speedMultiplier;

    /// <summary>
    ///     Temporary agent-speed multiplier on top of the base/roster speed (1 = normal) — e.g. the
    ///     Bomber sprinting while its fuse burns (<see cref="BomberAttack" />). Applied immediately,
    ///     preserving any active <see cref="SlowStatus" /> scaling.
    /// </summary>
    public void SetSpeedMultiplier(float value)
    {
        speedMultiplier = Mathf.Max(0f, value);
        if (anyError || isDead || agent == null || !agent.enabled)
        {
            return;
        }
        float slowFraction = TryGetComponent(out SlowStatus slow) ? slow.CurrentSlowFraction : 0f;
        agent.speed = BaseSpeed * (1f - slowFraction);
    }
    private void OnValidate()
    {
        agent = GetComponent<NavMeshAgent>();
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }
    }

    private void Start()
    {

        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (movementSO == null)
        {
            Debug.LogError("AIMovementSO is not assigned in the inspector.");
            anyError = true;
        }

        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        agent.speed = BaseSpeed;

        // Avoidance is applied in code so the prefab's NavMeshAgent stays untouched. Start in the
        // near tier; the repath tick moves the agent between tiers as its distance changes.
        agent.obstacleAvoidanceType = movementSO.nearAvoidance;
        isFar = false;

        // Unequal priorities let agents shoulder past each other instead of mutually oscillating.
        agent.avoidancePriority = Random.Range(movementSO.avoidancePriorityMin, movementSO.avoidancePriorityMax + 1);

        // De-phase the repath ticks so hundreds of agents spawned together don't all call
        // SetDestination on the same frames.
        lastUpdateTime = Time.time - Random.value * movementSO.updateInterval;

        player = Player.Instance;

        // Movement reacts to death; Health never reaches back into this component.
        health.OnDied += HandleDied;

        // Stop chasing once the player dies (e.g. so goblins can celebrate instead).
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        if (aggroFeedback != null)
        {
            aggroFeedback.PlayFeedbacks();
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        StopAgent();

        // Corpse: take the agent off the NavMesh entirely so it stops occupying it
        // and shoving live goblins around. Disable after StopAgent/ResetPath, which
        // need the agent still enabled and on the NavMesh.
        if (agent != null)
        {
            agent.enabled = false;
        }

        // Corpses have nothing left to tick.
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        StopAgent();
    }

    private void StopAgent()
    {
        // Halt pathfinding and bring the agent to rest where it stands.
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    /// <summary>
    ///     Temporarily halts/resumes chasing without touching the death/player-death state above —
    ///     used by attacks with a long wind-up (e.g. <see cref="TrollSlamAttack" />) that shouldn't
    ///     slide out from under a telegraph locked to the enemy's position when the wind-up started.
    /// </summary>
    public void SetMovementPaused(bool paused)
    {
        if (anyError || isDead) return;

        isPaused = paused;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = paused;
        }
    }

    float lastUpdateTime;
    bool isFar;
    // Update is called once per frame
    void Update()
    {
        if (anyError || isDead || playerDead || isPaused) return;

        // Far agents repath less often — with the stagger above, ~300 agents spread their
        // SetDestination calls evenly instead of spiking the path queue in lockstep.
        float repathInterval = isFar ? movementSO.farRepathInterval : movementSO.updateInterval;
        if (Time.time - lastUpdateTime >= repathInterval)
        {
            lastUpdateTime = Time.time;
            // The selector (gate defense) picks between the player and a gate; without one, the
            // player is the only target, as before.
            Vector3 destination = targetSelector != null ? targetSelector.TargetPosition : player.transform.position;
            agent.SetDestination(destination);
            UpdateAvoidanceTier();
        }

        FaceTargetWhenStopped();
    }

    /// <summary>
    ///     The NavMeshAgent only auto-rotates while it is moving along a path, so an agent resting
    ///     inside its stopping distance stops tracking the player. Keep turning toward the target
    ///     manually while stopped so melee enemies stay squared up to what they're attacking.
    /// </summary>
    private void FaceTargetWhenStopped()
    {
        if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance) return;

        Vector3 targetPosition = targetSelector != null ? targetSelector.TargetPosition : player.transform.position;
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(toTarget);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, movementSO.stoppedTurnSpeed * Time.deltaTime);
    }

    /// <summary>
    ///     Re-tiers avoidance on the repath tick (not per frame): full avoidance only matters in the
    ///     dense ring around the player; distant agents marching in open field skip the N-body cost.
    /// </summary>
    private void UpdateAvoidanceTier()
    {
        float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
        bool nowFar = sqrDistance > movementSO.farDistance * movementSO.farDistance;
        if (nowFar != isFar)
        {
            isFar = nowFar;
            agent.obstacleAvoidanceType = nowFar ? movementSO.farAvoidance : movementSO.nearAvoidance;
        }
    }
}
