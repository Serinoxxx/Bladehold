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

        agent.speed = movementSO.speed;

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
    // Update is called once per frame
    void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastUpdateTime >= movementSO.updateInterval)
        {
            lastUpdateTime = Time.time;
            agent.SetDestination(player.transform.position);
        }
    }
}
