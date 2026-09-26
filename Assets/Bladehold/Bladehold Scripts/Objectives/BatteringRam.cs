using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Battering Ram entity for the "Stop the Battering Ram" objective.
///     Moves along NavMesh toward a target fortress gate only when enemies are in its proximity.
///     Once it reaches the gate, it plays a procedural ramming animation every 5 seconds, dealing
///     50 damage at the point of impact with sound, VFX, and MMF_Player camera impulse.
///     The player must destroy the 1000 HP ram to complete the objective.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class BatteringRam : MonoBehaviour
{
    [Header("Health & Stats")]
    [Tooltip("Maximum health of the battering ram (default 1000).")]
    [SerializeField] private float maxHealth = 1000f;

    [Header("Movement & Push Settings")]
    [Tooltip("Movement speed along NavMesh when enemies are pushing.")]
    [SerializeField] private float moveSpeed = 1.5f;

    [Tooltip("Proximity radius in meters within which alive enemies will push the ram.")]
    [SerializeField] private float pushRadius = 6.0f;

    [Tooltip("Distance threshold to gate destination to consider arrived.")]
    [SerializeField] private float arrivalThreshold = 4.5f;

    [Header("Gate Assault & Ramming")]
    [Tooltip("Damage dealt to the gate per ram impact.")]
    [SerializeField] private float gateDamage = 50f;

    [Tooltip("Interval in seconds between ramming impacts.")]
    [SerializeField] private float ramInterval = 5.0f;

    [Tooltip("Transform of the ramming log (e.g. SM_Wep_Rammer_Log_01).")]
    [SerializeField] private Transform ramLogTransform;

    [Tooltip("Transform marking the impact point at the tip of the ram log.")]
    [SerializeField] private Transform impactPoint;

    [Header("Feedbacks & Juiciness")]
    [Tooltip("MMF_Player played at point of impact (boom, wood splinters, camera impulse, screen shake).")]
    [SerializeField] private MMF_Player impactFeedback;

    [Tooltip("MMF_Player played when taking damage (red flicker, shield bash sfx, wood splinters).")]
    [SerializeField] private MMF_Player hitFeedback;

    [Header("Visual Range Indicator")]
    [Tooltip("Transform of the range circle indicator.")]
    [SerializeField] private Transform rangeCircleTransform;

    [Tooltip("Renderer of the range circle to tint based on enemy pushers.")]
    [SerializeField] private Renderer rangeCircleRenderer;

    [SerializeField] private Color activeColor = new Color(0.9f, 0.25f, 0.15f, 0.4f);
    [SerializeField] private Color inactiveColor = new Color(0.9f, 0.7f, 0.2f, 0.2f);

    [Header("Wheel & Movement Animation")]
    [Tooltip("Wheel transforms rotated while moving forward.")]
    [SerializeField] private Transform[] wheels;
    [SerializeField] private float wheelRotationSpeed = 120f;

    [Tooltip("Looping audio source for heavy wood rolling / creaking.")]
    [SerializeField] private AudioSource movementAudioSource;

    [Header("Destruction Effects")]
    [Tooltip("MMF_Player played when the ram is destroyed (wood break + debris burst, on unscaled time).")]
    [SerializeField] private MMF_Player deathFeedback;

    [Tooltip("Seconds before despawning GameObject after destruction.")]
    [SerializeField] private float destroyDelay = 0.5f;

    private NavMeshAgent agent;
    private Health health;
    private Gate targetGate;
    private Vector3 destinationPoint;
    private float totalPathDistance;
    private bool isInitialized;
    private bool isPushed;
    private int pusherCount;
    private bool hasReachedGate;
    private bool isDestroyed;
    private Coroutine ramRoutine;

    // Log procedural animation cache
    private Vector3 initialLogLocalPos;
    private Quaternion initialLogLocalRot;
    private readonly Collider[] overlapBuffer = new Collider[32];

    public event Action<BatteringRam> OnDestroyed;
    public Health Health => health;
    public bool IsPushed => isPushed;
    public int PusherCount => pusherCount;
    public bool HasReachedGate => hasReachedGate;
    public bool IsDestroyed => isDestroyed;
    public float MoveSpeed => moveSpeed;

    /// <summary>Returns 0..1 normalized progress toward the gate destination.</summary>
    public float ProgressNormalized
    {
        get
        {
            if (!isInitialized || totalPathDistance <= 0.01f) return 0f;
            if (hasReachedGate) return 1f;
            float remaining = agent.hasPath ? agent.remainingDistance : Vector3.Distance(transform.position, destinationPoint);
            return Mathf.Clamp01(1f - (remaining / totalPathDistance));
        }
    }

    /// <summary>
    /// Computes a destination for pusher/escort enemies just ahead of and around the ram.
    /// Distributes agents laterally across the front and flanks so they don't bottleneck.
    /// </summary>
    public Vector3 GetEscortTargetPosition(Vector3 fromPosition, int agentId = 0)
    {
        // Deterministic spread based on agent ID (-1.5m to +1.5m lateral offset)
        float lateralFactor = Mathf.Sin(agentId);
        float forwardFactor = 2.5f + Mathf.Abs(Mathf.Cos(agentId)) * 1.5f; // 2.5m to 4.0m ahead

        Vector3 leadPosition = transform.position + (transform.forward * forwardFactor) + (transform.right * (lateralFactor * 1.5f));
        leadPosition.y = transform.position.y;
        return leadPosition;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = arrivalThreshold;
            agent.autoBraking = true;
        }

        if (health != null)
        {
            health.SetMaxHealth(maxHealth);
            health.ImmuneToPlayerDamage = false;
        }

        if (ramLogTransform != null)
        {
            initialLogLocalPos = ramLogTransform.localPosition;
            initialLogLocalRot = ramLogTransform.localRotation;
        }
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    private void Start()
    {
        UpdateCircleScale();
        UpdateVisualState();
        if (impactFeedback == null) Debug.LogError("[BatteringRam] impactFeedback is not assigned.", this);
        if (deathFeedback == null) Debug.LogError("[BatteringRam] deathFeedback is not assigned.", this);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }

        if (ramRoutine != null)
        {
            StopCoroutine(ramRoutine);
            ramRoutine = null;
        }
    }

    /// <summary>
    /// Initializes destination gate and starts pathfinding.
    /// </summary>
    public void InitializeDestination(Vector3 destination, Gate gate = null)
    {
        destinationPoint = destination;
        targetGate = gate;

        if (targetGate == null)
        {
            targetGate = Gate.NearestAlive(destinationPoint);
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(destinationPoint);
        }

        totalPathDistance = Vector3.Distance(transform.position, destinationPoint);
        isInitialized = true;
        UpdateCircleScale();
    }

    private void UpdateCircleScale()
    {
        if (rangeCircleTransform != null)
        {
            rangeCircleTransform.localScale = new Vector3(pushRadius * 2f, pushRadius * 2f, 1f);
        }
    }

    private void Update()
    {
        if (!isInitialized || isDestroyed) return;

        if (!hasReachedGate)
        {
            CheckEnemyProximity();
            DriveMovement();
            CheckGateArrival();
        }
    }

    /// <summary>
    /// Scans for alive enemies within pushRadius to determine if the ram is being pushed.
    /// </summary>
    private void CheckEnemyProximity()
    {
        bool wasPushed = isPushed;
        pusherCount = 0;

        int count = Physics.OverlapSphereNonAlloc(transform.position, pushRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || col.transform.root == transform.root) continue;

            // Player does not push the ram
            if (Player.Instance != null && col.transform.root == Player.Instance.transform.root) continue;

            Health enemyHealth = col.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead && enemyHealth != health)
            {
                // Ensure it's an enemy (not player, horse, or friendly structure)
                if (enemyHealth.GetComponent<AITargetSelector>() != null ||
                    enemyHealth.GetComponent<AIMovement>() != null ||
                    col.CompareTag("Enemy"))
                {
                    pusherCount++;
                }
            }
        }

        isPushed = pusherCount > 0;

        if (isPushed != wasPushed)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (rangeCircleRenderer != null)
        {
            rangeCircleRenderer.material.color = isPushed ? activeColor : inactiveColor;
        }

        if (movementAudioSource != null)
        {
            if (isPushed && !hasReachedGate && !movementAudioSource.isPlaying)
            {
                movementAudioSource.Play();
            }
            else if ((!isPushed || hasReachedGate) && movementAudioSource.isPlaying)
            {
                movementAudioSource.Pause();
            }
        }
    }

    private void DriveMovement()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (isPushed)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;

            // Rotate wheels based on motion
            if (wheels != null && wheels.Length > 0 && agent.velocity.sqrMagnitude > 0.01f)
            {
                float rotAmount = wheelRotationSpeed * Time.deltaTime;
                foreach (Transform wheel in wheels)
                {
                    if (wheel != null)
                    {
                        wheel.Rotate(Vector3.right, rotAmount, Space.Self);
                    }
                }
            }
        }
        else
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void CheckGateArrival()
    {
        if (hasReachedGate) return;

        float distToDest = Vector3.Distance(transform.position, destinationPoint);
        if (distToDest <= arrivalThreshold)
        {
            ArriveAtGate();
        }
    }

    private void ArriveAtGate()
    {
        hasReachedGate = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        UpdateVisualState();

        // Face the gate doors
        Vector3 dirToDest = destinationPoint - transform.position;
        dirToDest.y = 0f;
        if (dirToDest.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dirToDest);
        }

        if (targetGate == null)
        {
            targetGate = Gate.NearestAlive(transform.position);
        }

        ramRoutine = StartCoroutine(RammingAttackRoutine());
    }

    /// <summary>
    /// Executes the ramming attack cycle every ramInterval (5 seconds).
    /// </summary>
    private IEnumerator RammingAttackRoutine()
    {
        // Initial brief alignment pause before starting ramming
        yield return new WaitForSeconds(0.5f);

        while (!isDestroyed && (targetGate == null || !targetGate.IsDestroyed))
        {
            float cycleStartTime = Time.time;

            yield return PlayRamLogAnimation();

            // Calculate remaining time in the 5 second interval
            float elapsed = Time.time - cycleStartTime;
            float remainingWait = Mathf.Max(0.1f, ramInterval - elapsed);
            yield return new WaitForSeconds(remainingWait);
        }
    }

    /// <summary>
    /// Procedurally animates the ram log forward and back convincingly,
    /// triggering gate damage, MMF_Player feedbacks, sound, and VFX at the exact point of impact.
    /// </summary>
    private IEnumerator PlayRamLogAnimation()
    {
        if (ramLogTransform == null) yield break;

        // Phase 1: Windup Pullback (~1.2s)
        // Log draws backward on its suspension ropes (-1.3m local Z) and rises slightly (+0.15m local Y, -6 deg pitch)
        float pullbackDuration = 1.2f;
        float elapsed = 0f;
        Vector3 targetPullbackPos = initialLogLocalPos + new Vector3(0f, 0.15f, -1.3f);
        Quaternion targetPullbackRot = initialLogLocalRot * Quaternion.Euler(-6f, 0f, 0f);

        while (elapsed < pullbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / pullbackDuration);
            ramLogTransform.localPosition = Vector3.Lerp(initialLogLocalPos, targetPullbackPos, t);
            ramLogTransform.localRotation = Quaternion.Slerp(initialLogLocalRot, targetPullbackRot, t);
            yield return null;
        }

        // Brief pause at peak tension (~0.15s)
        yield return new WaitForSeconds(0.15f);

        // Phase 2: Violent Thrust Surge (~0.35s)
        // Log accelerates rapidly forward to slam into the gate doors (+1.8m local Z)
        float thrustDuration = 0.35f;
        elapsed = 0f;
        Vector3 impactLocalPos = initialLogLocalPos + new Vector3(0f, -0.05f, 1.8f);
        Quaternion impactLocalRot = initialLogLocalRot * Quaternion.Euler(2f, 0f, 0f);

        while (elapsed < thrustDuration)
        {
            elapsed += Time.deltaTime;
            // EaseInQuad for powerful acceleration
            float t = (elapsed / thrustDuration) * (elapsed / thrustDuration);
            ramLogTransform.localPosition = Vector3.Lerp(targetPullbackPos, impactLocalPos, t);
            ramLogTransform.localRotation = Quaternion.Slerp(targetPullbackRot, impactLocalRot, t);
            yield return null;
        }

        ramLogTransform.localPosition = impactLocalPos;
        ramLogTransform.localRotation = impactLocalRot;

        // === POINT OF IMPACT ===
        TriggerImpact();

        // Phase 3: Recoil Bounce & Settle (~1.2s)
        // Bounces back from hard gate impact (+0.5m local Z), then settles with damped oscillation to rest
        float recoilDuration = 1.2f;
        elapsed = 0f;
        Vector3 recoilPeakPos = initialLogLocalPos + new Vector3(0f, 0.05f, 0.5f);

        while (elapsed < recoilDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / recoilDuration;
            // Damped harmonic oscillation settling to 0
            float decay = Mathf.Exp(-3.5f * t);
            float oscillation = Mathf.Cos(t * Mathf.PI * 3.5f);
            float offsetZ = 0.5f * decay * oscillation;

            ramLogTransform.localPosition = initialLogLocalPos + new Vector3(0f, 0.05f * decay, offsetZ);
            ramLogTransform.localRotation = Quaternion.Slerp(ramLogTransform.localRotation, initialLogLocalRot, t);
            yield return null;
        }

        ramLogTransform.localPosition = initialLogLocalPos;
        ramLogTransform.localRotation = initialLogLocalRot;
    }

    /// <summary>
    /// Executes impact effects and delivers 50 damage to the gate at the exact point of impact.
    /// </summary>
    private void TriggerImpact()
    {
        Vector3 pos = impactPoint != null ? impactPoint.position : transform.position + transform.forward * 3f + Vector3.up * 1.5f;

        // 1. Deliver 50 damage to the gate
        if (targetGate == null || targetGate.IsDestroyed)
        {
            targetGate = Gate.NearestAlive(transform.position);
        }

        if (targetGate != null && targetGate.Damageable != null && !targetGate.IsDestroyed)
        {
            Damage ramDamage = new Damage
            {
                value = gateDamage,
                type = DamageType.blunt,
                unparryable = true,
                isPlayerDamage = false,
                source = null,
                sourcePosition = pos
            };
            targetGate.Damageable.ReceiveDamage(ramDamage);
        }

        // 2. Play MMF_Player (boom, splinters, camera impulse, screen shake)
        if (impactFeedback != null)
        {
            impactFeedback.PlayFeedbacks(pos);
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (isDestroyed) return;
        if (hitFeedback != null)
        {
            Vector3 hitPos = damage != null && damage.sourcePosition != Vector3.zero ? damage.sourcePosition : transform.position;
            hitFeedback.PlayFeedbacks(hitPos);
        }
    }

    private void HandleDied()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (ramRoutine != null)
        {
            StopCoroutine(ramRoutine);
            ramRoutine = null;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (movementAudioSource != null && movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
        }

        if (deathFeedback != null)
        {
            deathFeedback.PlayFeedbacks(transform.position);
        }

        // Hide visuals and disable colliders
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = false;
        }

        OnDestroyed?.Invoke(this);

        if (destroyDelay <= 0f)
        {
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(DespawnRoutine());
        }
    }

    private IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}
