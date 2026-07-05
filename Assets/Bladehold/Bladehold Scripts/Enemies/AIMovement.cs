using UnityEngine;
using UnityEngine.AI;

public class AIMovement : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] AIMovementSO movementSO;
    [SerializeField] Health health;

    Player player;
    Health playerHealth;

    bool isDead = false;
    bool playerDead = false;
    bool anyError = false;
    float? speedOverride;

    /// <summary>
    ///     Per-instance agent-speed override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate, before Start runs; the shared
    ///     <see cref="AIMovementSO" /> is never mutated.
    /// </summary>
    public void SetSpeed(float value)
    {
        speedOverride = value;
    }
    private void OnValidate()
    {
        agent = GetComponent<NavMeshAgent>();
        if (health == null)
        {
            health = GetComponent<Health>();
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

        agent.speed = speedOverride ?? movementSO.speed;

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

    float lastUpdateTime;
    bool isFar;
    // Update is called once per frame
    void Update()
    {
        if (anyError || isDead || playerDead) return;

        // Far agents repath less often — with the stagger above, ~300 agents spread their
        // SetDestination calls evenly instead of spiking the path queue in lockstep.
        float repathInterval = isFar ? movementSO.farRepathInterval : movementSO.updateInterval;
        if (Time.time - lastUpdateTime >= repathInterval)
        {
            lastUpdateTime = Time.time;
            agent.SetDestination(player.transform.position);
            UpdateAvoidanceTier();
        }
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
