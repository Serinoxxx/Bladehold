using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Drives the Golden Goblin's fleeing AI.
///     Disables standard <see cref="AIMovement" /> so the goblin runs around and away from the player
///     without ever attacking.
/// </summary>
public class GoldenGoblinFlee : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement aiMovement;
    [SerializeField] private GoldenGoblinFleeSO fleeData;
    [SerializeField] private Coin coinPrefab;

    [Tooltip("World-space offset from this transform where bonus coins spawn on death.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    private Player player;
    private Health playerHealth;
    private PlayerStats stats;

    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;
    private float lastRepathTime = 0f;

    private static readonly float[] SearchAngles = new float[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f, 180f };

    private void OnValidate()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<Health>();
        if (aiMovement == null) aiMovement = GetComponent<AIMovement>();
    }

    /// <summary>
    ///     Exposed for WaveSpawner CSV definition routing compatibility.
    ///     Golden Goblin does not deal damage, so this setter is a no-op.
    /// </summary>
    public void SetDamage(float value)
    {
        // No-op: Golden Goblin doesn't attack.
    }

    private void Start()
    {
        if (agent == null)
        {
            Debug.LogError("[GoldenGoblinFlee] NavMeshAgent is missing on " + gameObject.name);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("[GoldenGoblinFlee] Health is missing on " + gameObject.name);
            anyError = true;
        }
        if (fleeData == null)
        {
            Debug.LogError("[GoldenGoblinFlee] GoldenGoblinFleeSO is not assigned on " + gameObject.name);
            anyError = true;
        }

        if (anyError) return;

        // Take over pathfinding: disable AIMovement so it doesn't overwrite SetDestination with player chase.
        if (aiMovement != null)
        {
            agent.speed = aiMovement.BaseSpeed;
            aiMovement.enabled = false;
        }
        else
        {
            agent.speed = 6.5f;
        }

        player = Player.Instance;
        stats = player != null ? player.Stats : null;

        health.OnDied += HandleDied;

        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        // Stagger repath start time so multiple golden goblins don't set destination on the same frame.
        lastRepathTime = Time.time - Random.value * fleeData.repathInterval;
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

        if (fleeData != null)
        {
            if (fleeData.deathVfxPrefab != null)
            {
                Instantiate(fleeData.deathVfxPrefab, transform.position, Quaternion.identity);
            }
            if (fleeData.deathSfx != null)
            {
                AudioSource.PlayClipAtPoint(fleeData.deathSfx, transform.position);
            }
        }

        // Drop bonus coins if player has GoldenGoblinGoldBonusPercent stat
        if (stats != null && coinPrefab != null)
        {
            float bonusPercent = stats.GetValue(StatType.GoldenGoblinGoldBonusPercent);
            if (bonusPercent > 0f)
            {
                int bonusAmount = Mathf.RoundToInt(50f * bonusPercent);
                if (bonusAmount > 0)
                {
                    Coin coin = Instantiate(coinPrefab, transform.position + dropOffset, Quaternion.identity);
                    coin.SetAmount(bonusAmount);
                }
            }
        }

        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        StopAgent();
    }

    private void StopAgent()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || player == null) return;

        if (Time.time - lastRepathTime >= fleeData.repathInterval)
        {
            lastRepathTime = Time.time;
            UpdateFleeDestination();
        }
    }

    private void UpdateFleeDestination()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 playerPos = player.transform.position;
        Vector3 myPos = transform.position;

        Vector3 dirFromPlayer = myPos - playerPos;
        dirFromPlayer.y = 0f;

        if (dirFromPlayer.sqrMagnitude < 0.01f)
        {
            dirFromPlayer = transform.forward;
        }
        dirFromPlayer.Normalize();

        Vector3 bestFleePos = myPos;
        float maxDistanceSqr = (myPos - playerPos).sqrMagnitude;
        bool foundValidPos = false;

        float sampleRadius = fleeData != null ? fleeData.fleeSampleRadius : 8f;

        foreach (float angle in SearchAngles)
        {
            Vector3 testDir = Quaternion.AngleAxis(angle, Vector3.up) * dirFromPlayer;
            Vector3 targetPos = myPos + testDir * sampleRadius;

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                float distSqr = (hit.position - playerPos).sqrMagnitude;
                if (distSqr > maxDistanceSqr)
                {
                    maxDistanceSqr = distSqr;
                    bestFleePos = hit.position;
                    foundValidPos = true;
                }
            }
        }

        if (foundValidPos)
        {
            agent.SetDestination(bestFleePos);
        }
        else
        {
            // Fallback: pick point along dirFromPlayer
            Vector3 fallbackTarget = myPos + dirFromPlayer * sampleRadius;
            if (NavMesh.SamplePosition(fallbackTarget, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
}
