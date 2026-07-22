using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The knight's horseback AI, driving the HORSE's <see cref="NavMeshAgent" /> (the knight's own
///     goblin-style components stay disabled until <see cref="MountedKnightRider" /> unseats him).
///     Cycle: circle the player at a standoff distance → turn to face → REAR (the telegraph — a
///     ground lane shows exactly where the run ends) → charge dead straight along the locked lane
///     with the shared <see cref="HorseChargeDamage" /> trample open → decelerate → cooldown.
///
///     The dash uses <c>agent.Move</c>, which is clamped to the NavMesh, so the horse physically
///     cannot charge out of the arena; the lane is pre-clamped with <see cref="NavMesh.Raycast" />
///     at rear time so the telegraph stays honest. Follows the <see cref="TrollSlamAttack" />
///     skeleton (validate in Start, stop on own/player death). Tunables on
///     <see cref="MountedKnightSO" />.
/// </summary>
public class MountedKnightBrain : MonoBehaviour
{
    [SerializeField] private MountedKnightSO data;
    [SerializeField] private Health health;
    [Tooltip("The horse's animator bridge (auto-wired from the horse child at edit time; the refs survive the runtime detach).")]
    [SerializeField] private HorseAnimation horseAnimation;
    [SerializeField] private HorseChargeDamage chargeDamage;
    [SerializeField] private NavMeshAgent horseAgent;
    [Tooltip("Flat quad stretched along the charge lane during the rear. Scaled to (lane width, 1, lane length).")]
    [SerializeField] private GameObject telegraphPrefab;
    [Tooltip("Optional debris/particle trail prefab spawned on the horse during the charge.")]
    [SerializeField] private GameObject trailPrefab;
    [Tooltip("World width of the telegraph lane; match the trample box's x extent (2 × HorseSO.hitBoxHalfExtents.x).")]
    [SerializeField] private float telegraphWidth = 2.4f;
    [Tooltip("Fallback charge damage before the chargeDamageMultiplier when no roster override arrives.")]
    [SerializeField] private float baseDamage = 4f;
    [SerializeField] private MMF_Player rearFeedback;
    [SerializeField] private MMF_Player chargeFeedback;

    private enum KnightState
    {
        Reposition,
        Aim,
        Rear,
        Charge,
        Recover,
    }

    private KnightState state = KnightState.Reposition;
    private Transform horse;
    private Transform player;
    private Health playerHealth;
    private float? damageOverride;
    private GameObject activeTelegraph;
    private GameObject activeTrail;

    private float nextRepathTime;
    private float lastChargeTime = Mathf.NegativeInfinity;
    private float stateTimer;
    private Vector3 chargeDirection;
    private float chargeLaneLength;
    private float chargeTraveled;
    private float stallTimer;
    private Vector3 lastChargePosition;

    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (the roster CSV's damage column, applied by
    ///     <see cref="WaveSpawner.ApplyDefinition" />). The charge deals this ×
    ///     <see cref="MountedKnightSO.chargeDamageMultiplier" /> per trample hit; the shared SO is
    ///     never mutated.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (horseAnimation == null)
        {
            // The horse is still a child at edit time; MountedKnightRider detaches it in Awake but
            // these object references survive the reparent.
            horseAnimation = GetComponentInChildren<HorseAnimation>();
        }
        if (horseAnimation != null)
        {
            if (chargeDamage == null)
            {
                chargeDamage = horseAnimation.GetComponent<HorseChargeDamage>();
            }
            if (horseAgent == null)
            {
                horseAgent = horseAnimation.GetComponent<NavMeshAgent>();
            }
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("MountedKnightSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (horseAnimation == null || chargeDamage == null || horseAgent == null)
        {
            Debug.LogError("Horse references (HorseAnimation/HorseChargeDamage/NavMeshAgent) are not assigned or found on the horse child.");
            anyError = true;
        }
        if (telegraphPrefab == null)
        {
            Debug.LogError("Telegraph prefab is not assigned in the inspector; the charge lane must be revealed before the horse runs it.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        horse = horseAgent.transform;
        horseAgent.speed = data.repositionSpeed;

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the knight has no one to charge.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        health.OnDied += HandleDied;

        if (playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        // Stagger the first repath so simultaneous knights (dev spawns) don't tick in lockstep —
        // the AIMovement de-phasing trick.
        nextRepathTime = Time.time + Random.Range(0f, data.repathInterval);
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
        Abort();
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    /// <summary>Called by <see cref="MountedKnightRider" /> when the knight is unseated: abort whatever is in flight and stop driving the horse.</summary>
    public void OnDismounted()
    {
        Abort();
        enabled = false;
    }

    /// <summary>Cleans up any in-flight state: telegraph gone, trample closed, horse stopped.</summary>
    private void Abort()
    {
        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }
        if (activeTrail != null)
        {
            var psList = activeTrail.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList) ps.Stop();
            Destroy(activeTrail, 2f);
            activeTrail = null;
        }
        if (chargeDamage != null)
        {
            chargeDamage.EndCharge();
        }
        if (horseAgent != null && horseAgent.enabled && horseAgent.isOnNavMesh)
        {
            horseAgent.isStopped = true;
            horseAgent.ResetPath();
            horseAgent.updateRotation = true;
        }
        state = KnightState.Reposition;
    }

    private void Update()
    {
        if (anyError || isDead) return;

        if (playerDead)
        {
            // The run is over; stop where we are and idle (no cheer while mounted — the knight's
            // AIAnimation never went live).
            if (state != KnightState.Reposition)
            {
                Abort();
            }
            return;
        }

        switch (state)
        {
            case KnightState.Reposition: TickReposition(); break;
            case KnightState.Aim: TickAim(); break;
            case KnightState.Rear: TickRear(); break;
            case KnightState.Charge: TickCharge(); break;
            case KnightState.Recover: TickRecover(); break;
        }
    }

    private float PlanarDistanceToPlayer()
    {
        Vector3 toPlayer = player.position - horse.position;
        toPlayer.y = 0f;
        return toPlayer.magnitude;
    }

    private void TickReposition()
    {
        float distance = PlanarDistanceToPlayer();

        // Ready to line up a charge? Needs cooldown, and enough runway to build the run.
        if (Time.time - lastChargeTime >= data.chargeCooldown
            && distance >= data.minChargeRange
            && distance <= data.standoffDistance * 1.25f)
        {
            horseAgent.isStopped = true;
            horseAgent.ResetPath();
            horseAgent.updateRotation = false;
            state = KnightState.Aim;
            return;
        }

        if (Time.time < nextRepathTime) return;
        nextRepathTime = Time.time + data.repathInterval;

        // Hold the standoff ring: too close → a point directly away from the player; otherwise the
        // nearest ring point (which naturally circles as the player moves).
        Vector3 fromPlayer = horse.position - player.position;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude < 0.01f)
        {
            fromPlayer = -player.forward;
        }
        Vector3 ringPoint = player.position + fromPlayer.normalized * data.standoffDistance;

        horseAgent.speed = data.repositionSpeed;
        horseAgent.SetDestination(ringPoint);
    }

    private void TickAim()
    {
        Vector3 toPlayer = player.position - horse.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f)
        {
            BeginRear(horse.forward);
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized);
        horse.rotation = Quaternion.RotateTowards(horse.rotation, targetRotation, data.aimTurnSpeed * Time.deltaTime);

        if (Quaternion.Angle(horse.rotation, targetRotation) <= data.aimToleranceDegrees)
        {
            BeginRear(horse.forward);
        }
    }

    private void BeginRear(Vector3 direction)
    {
        direction.y = 0f;
        chargeDirection = direction.normalized;

        // Lock the lane now and pre-clamp it to the NavMesh so the telegraph shows exactly where
        // the run ends — near a wall the lane is visibly shorter.
        chargeLaneLength = data.maxChargeDistance;
        if (NavMesh.Raycast(horse.position, horse.position + chargeDirection * data.maxChargeDistance, out NavMeshHit hit, NavMesh.AllAreas))
        {
            chargeLaneLength = Mathf.Max(1f, hit.distance);
        }

        Vector3 laneCenter = horse.position + chargeDirection * (chargeLaneLength * 0.5f) + Vector3.up * 0.05f;
        activeTelegraph = Instantiate(telegraphPrefab, laneCenter, Quaternion.LookRotation(chargeDirection));
        Vector3 scale = activeTelegraph.transform.localScale;
        activeTelegraph.transform.localScale = new Vector3(telegraphWidth, scale.y, chargeLaneLength);

        horseAnimation.TriggerRear();
        if (rearFeedback != null)
        {
            rearFeedback.PlayFeedbacks();
        }

        stateTimer = data.rearSeconds;
        state = KnightState.Rear;
    }

    private void TickRear()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }

        float effectiveDamage = (damageOverride ?? baseDamage) * data.chargeDamageMultiplier;
        chargeDamage.BeginCharge(health, null, effectiveDamage);
        if (chargeFeedback != null)
        {
            chargeFeedback.PlayFeedbacks();
        }

        if (trailPrefab != null && activeTrail == null)
        {
            activeTrail = Instantiate(trailPrefab, horse.position, horse.rotation, horse);
        }

        chargeTraveled = 0f;
        stallTimer = 0f;
        lastChargePosition = horse.position;
        state = KnightState.Charge;
    }

    private void TickCharge()
    {
        float dt = Time.deltaTime;
        float requested = data.chargeSpeed * dt;

        horse.rotation = Quaternion.LookRotation(chargeDirection);
        horseAgent.Move(chargeDirection * requested);

        Vector3 actualDelta = horse.position - lastChargePosition;
        actualDelta.y = 0f;
        lastChargePosition = horse.position;
        float actual = actualDelta.magnitude;
        chargeTraveled += actual;

        // Belt-and-braces stall guard: agent.Move grinding an off-lane obstacle or mesh edge at an
        // angle makes little real progress — end the run rather than treadmill in place.
        if (requested > 0f && actual < requested * 0.3f)
        {
            stallTimer += dt;
        }
        else
        {
            stallTimer = 0f;
        }

        if (chargeTraveled >= chargeLaneLength + data.overshootDistance || stallTimer >= 0.2f)
        {
            if (activeTrail != null)
            {
                var psList = activeTrail.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in psList) ps.Stop();
                Destroy(activeTrail, 2f);
                activeTrail = null;
            }

            chargeDamage.EndCharge();
            stateTimer = data.decelSeconds;
            state = KnightState.Recover;
        }
    }

    private void TickRecover()
    {
        float dt = Time.deltaTime;
        stateTimer -= dt;

        // Bleed the dash speed off linearly over decelSeconds.
        float speed = data.chargeSpeed * Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, data.decelSeconds));
        horseAgent.Move(chargeDirection * speed * dt);

        if (stateTimer <= 0f)
        {
            lastChargeTime = Time.time;
            horseAgent.isStopped = false;
            horseAgent.updateRotation = true;
            state = KnightState.Reposition;
        }
    }
}
