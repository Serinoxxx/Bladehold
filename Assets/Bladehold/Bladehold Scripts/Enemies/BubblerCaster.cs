using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Bubbler enemy behavior:
///     Does not attack the player, keeps its distance.
///     Maintains a bubble shield on an alive ally within 15m cast range,
///     displaying a SineVFX LightningSystemChain beam connecting the Bubbler to the shielded enemy.
/// </summary>
public class BubblerCaster : MonoBehaviour
{
    [SerializeField] private BubblerCasterSO data;
    [SerializeField] private BubbleShieldSO shieldData;
    [SerializeField] private Health health;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private AIMovement movement;
    [SerializeField] private AIAttack baseAiAttack;

    [Tooltip("The lightning effect component to enable when tethering to an ally (same as Medusa).")]
    [SerializeField] private LightningSystemChain lightningEffect;

    [Tooltip("Transform on the Bubbler to use as origin for the beam (e.g. staff tip or hand).")]
    [SerializeField] private Transform lightningOriginPoint;

    [Tooltip("Offset from the target enemy's origin for the beam anchor.")]
    [SerializeField] private Vector3 lightningTargetOffset = new Vector3(0, 1.2f, 0);

    private Transform lightningTargetTransform;
    private Health currentTargetHealth;
    private BubbleShield currentShield;
    private Player player;
    private Health playerHealth;

    private float lastTickTime;
    private bool isDead;
    private bool playerDead;
    private bool anyError;

    private static readonly float[] FleeAngles = new float[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f, 160f, -160f, 180f };

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (movement == null) movement = GetComponent<AIMovement>();
        if (baseAiAttack == null) baseAiAttack = GetComponent<AIAttack>();
    }

    /// <summary>
    ///     WaveSpawner CSV routing compatibility. Bubblers do not deal damage.
    /// </summary>
    public void SetDamage(float value)
    {
        // No-op: Bubblers are pure support casters.
    }

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null)
        {
            health.OnDied += HandleDied;
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("[BubblerCaster] BubblerCasterSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("[BubblerCaster] Health component is not assigned or found.");
            anyError = true;
        }
        if (agent == null)
        {
            Debug.LogError("[BubblerCaster] NavMeshAgent component is not assigned or found.");
            anyError = true;
        }

        if (anyError) return;

        // Ensure base melee attack is disabled
        if (baseAiAttack != null)
        {
            baseAiAttack.enabled = false;
        }

        // Take control of movement so AIMovement doesn't push this caster into melee
        if (movement != null)
        {
            agent.speed = movement.BaseSpeed;
            movement.enabled = false;
        }

        player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        // Create beam target anchor object
        lightningTargetTransform = new GameObject("BubblerBeamAnchor").transform;

        // Configure beam points
        if (lightningEffect != null)
        {
            Transform origin = lightningOriginPoint != null ? lightningOriginPoint : transform;
            lightningEffect.chainPoints = new Transform[] { origin, lightningTargetTransform };
            lightningEffect.gameObject.SetActive(false);
        }

        lastTickTime = Time.time - Random.value * data.tickInterval;
    }

    private void OnDestroy()
    {
        BreakCurrentShield();

        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }

        if (lightningTargetTransform != null)
        {
            Destroy(lightningTargetTransform.gameObject);
        }
    }

    private void HandleDied()
    {
        isDead = true;
        BreakCurrentShield();
        StopAgent();
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        BreakCurrentShield();
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
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastTickTime < data.tickInterval) return;
        lastTickTime = Time.time;

        UpdateShieldTarget();
        UpdateNavigation();
    }

    private void UpdateShieldTarget()
    {
        // Check if existing target is still valid and in break range
        if (currentTargetHealth != null && !currentTargetHealth.IsDead && currentShield != null)
        {
            float distToTarget = Vector3.Distance(transform.position, currentTargetHealth.transform.position);
            if (distToTarget <= data.breakRange)
            {
                // Update beam target position to follow ally
                if (lightningTargetTransform != null)
                {
                    lightningTargetTransform.position = currentTargetHealth.transform.position + lightningTargetOffset;
                }
                return;
            }
        }

        // Current target is lost or out of range, clear it and find new ally
        BreakCurrentShield();
        FindAndShieldAlly();
    }

    private void FindAndShieldAlly()
    {
        // Scan for nearest alive ally within castRange
        Collider[] hits = Physics.OverlapSphere(transform.position, data.castRange);
        Health bestAlly = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h == health || h.IsDead) continue;

            // Must be an enemy and not player
            if (player != null && (h == player.Health || col.transform.root == player.transform.root))
            {
                continue;
            }

            // Check if already bubbled
            if (h.GetComponent<BubbleShield>() != null)
            {
                continue;
            }

            // Check distance
            float dSqr = (h.transform.position - transform.position).sqrMagnitude;
            if (dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                bestAlly = h;
            }
        }

        if (bestAlly != null)
        {
            CastBubbleOn(bestAlly);
        }
    }

    public void CastBubbleOn(Health allyHealth)
    {
        if (allyHealth == null || allyHealth.IsDead) return;

        currentTargetHealth = allyHealth;
        currentShield = allyHealth.gameObject.AddComponent<BubbleShield>();
        currentShield.Initialize(shieldData, transform, HandleShieldBroken);

        if (lightningTargetTransform != null)
        {
            lightningTargetTransform.position = allyHealth.transform.position + lightningTargetOffset;
        }

        if (lightningEffect != null)
        {
            Transform origin = lightningOriginPoint != null ? lightningOriginPoint : transform;
            lightningEffect.chainPoints = new Transform[] { origin, lightningTargetTransform };
            lightningEffect.gameObject.SetActive(true);
        }
    }

    private void HandleShieldBroken()
    {
        currentShield = null;
        currentTargetHealth = null;
        if (lightningEffect != null)
        {
            lightningEffect.gameObject.SetActive(false);
        }
    }

    private void BreakCurrentShield()
    {
        if (currentShield != null)
        {
            currentShield.CollapseShield();
            currentShield = null;
        }
        currentTargetHealth = null;

        if (lightningEffect != null)
        {
            lightningEffect.gameObject.SetActive(false);
        }
    }

    private void UpdateNavigation()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 myPos = transform.position;
        Vector3 playerPos = player != null ? player.transform.position : myPos;
        float distToPlayer = Vector3.Distance(myPos, playerPos);

        // 1. If player is too close (< keepDistance), flee away from player
        if (player != null && distToPlayer < data.keepDistance)
        {
            FleeFromPlayer(playerPos, myPos);
            return;
        }

        // 2. If shielding an ally, navigate to stay within follow distance of ally
        if (currentTargetHealth != null && !currentTargetHealth.IsDead)
        {
            Vector3 allyPos = currentTargetHealth.transform.position;
            float distToAlly = Vector3.Distance(myPos, allyPos);

            if (distToAlly > data.allyFollowDistance)
            {
                // Move towards ally to maintain range
                agent.SetDestination(allyPos);
            }
            else if (distToAlly < 4.0f)
            {
                // Too close to ally, back up slightly away from ally
                Vector3 away = (myPos - allyPos).normalized;
                Vector3 standSpot = allyPos + away * data.allyFollowDistance;
                if (NavMesh.SamplePosition(standSpot, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
            else
            {
                // In good position
                if (agent.hasPath) agent.ResetPath();
            }
            return;
        }

        // 3. Neither fleeing nor following: stay alert or wander slightly towards center
        if (agent.hasPath && agent.remainingDistance < 1f)
        {
            agent.ResetPath();
        }
    }

    private void FleeFromPlayer(Vector3 playerPos, Vector3 myPos)
    {
        Vector3 dirFromPlayer = myPos - playerPos;
        dirFromPlayer.y = 0f;
        if (dirFromPlayer.sqrMagnitude < 0.01f)
        {
            dirFromPlayer = transform.forward;
        }
        dirFromPlayer.Normalize();

        Vector3 bestFleePos = myPos;
        float maxDistanceSqr = (myPos - playerPos).sqrMagnitude;
        float sampleRadius = data.fleeSampleRadius;

        foreach (float angle in FleeAngles)
        {
            Vector3 testDir = Quaternion.AngleAxis(angle, Vector3.up) * dirFromPlayer;
            Vector3 targetPos = myPos + testDir * sampleRadius;

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                float distSqr = (hit.position - playerPos).sqrMagnitude;
                if (distSqr > maxDistanceSqr)
                {
                    maxDistanceSqr = distSqr;
                    bestFleePos = hit.position;
                }
            }
        }

        agent.SetDestination(bestFleePos);
    }
}
