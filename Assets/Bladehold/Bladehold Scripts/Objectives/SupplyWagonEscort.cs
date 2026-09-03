using System;
using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Supply wagon entity for the "Protect the supply wagon" objective.
///     Moves along NavMesh toward a destination gate only when the player is within its detection radius.
///     Dynamically sizes and animates its visual range circle indicator and plays movement feedbacks.
///     On arrival, after a configurable delay, it bursts with feedback/VFX, spawns gold bags, and destroys itself.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SupplyWagonEscort : MonoBehaviour
{
    [Header("Escort Settings")]
    [Tooltip("Proximity radius in meters required for the wagon to move.")]
    [SerializeField] private float escortRadius = 7f;

    [Tooltip("Forward movement speed along NavMesh when player is in radius.")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("Distance threshold to gate destination to consider arrived.")]
    [SerializeField] private float arrivalThreshold = 3.5f;

    [Header("Visual Range Indicator")]
    [Tooltip("Transform of the range circle (e.g. flat ring quad/cylinder/projector) representing the escort area.")]
    [SerializeField] private Transform rangeCircleTransform;

    [Tooltip("Renderer of the range circle to tint based on player presence.")]
    [SerializeField] private Renderer rangeCircleRenderer;

    [SerializeField] private Color activeColor = new Color(0.2f, 0.9f, 0.4f, 0.35f);
    [SerializeField] private Color inactiveColor = new Color(1f, 0.8f, 0.2f, 0.2f);

    [Header("Wheel & Movement Animation")]
    [Tooltip("Wheel transforms rotated while moving forward.")]
    [SerializeField] private Transform[] wheels;
    [SerializeField] private float wheelRotationSpeed = 180f;

    [Header("Feedbacks & Audio")]
    [Tooltip("Particle system active while moving (e.g. road dust).")]
    [SerializeField] private ParticleSystem wheelDustParticles;

    [Tooltip("Looping audio source for cart squeaking/rolling.")]
    [SerializeField] private AudioSource movementAudioSource;

    [Header("Arrival & Burst Settings")]
    [Tooltip("Delay in seconds after reaching the destination before playing the burst feedback and dropping gold.")]
    [SerializeField] private float burstDelay = 0.5f;

    [Tooltip("MMF_Player played when wagon reaches the destination and bursts.")]
    [SerializeField] private MMF_Player arrivalFeedback;

    [Tooltip("Arrival fanfare / celebration SFX.")]
    [SerializeField] private AudioClip arrivalSound;

    [Tooltip("Optional burst VFX prefab spawned at the wagon position when it bursts.")]
    [SerializeField] private GameObject goldBurstVfxPrefab;

    [Tooltip("Coin / Gold Bag pickup prefab dropped on arrival burst.")]
    [SerializeField] private Coin goldBagPrefab;

    [Tooltip("Minimum number of gold bags dropped on arrival burst.")]
    [SerializeField] private int minGoldBags = 4;

    [Tooltip("Maximum number of gold bags dropped on arrival burst.")]
    [SerializeField] private int maxGoldBags = 5;

    [Tooltip("Amount of gold contained in each dropped gold bag.")]
    [SerializeField] private int goldPerBag = 25;

    [Tooltip("Scatter radius in meters around the wagon for dropped gold bags.")]
    [SerializeField] private float dropScatterRadius = 2.0f;

    [Tooltip("Height / position offset for spawning gold bags and burst VFX.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("Seconds to wait after the burst before destroying the wagon GameObject (lets feedbacks play out).")]
    [SerializeField] private float destroyDelay = 0.5f;

    private NavMeshAgent agent;
    private Vector3 destinationPoint;
    private float totalPathDistance;
    private bool isInitialized;
    private bool isPlayerInRadius;
    private bool hasArrived;

    public event Action<SupplyWagonEscort> OnArrived;
    public float EscortRadius => escortRadius;
    public bool IsPlayerInRadius => isPlayerInRadius;
    public bool HasArrived => hasArrived;

    /// <summary>Returns 0..1 normalized progress toward the gate destination.</summary>
    public float ProgressNormalized
    {
        get
        {
            if (!isInitialized || totalPathDistance <= 0.01f) return 0f;
            float remaining = agent.hasPath ? agent.remainingDistance : Vector3.Distance(transform.position, destinationPoint);
            return Mathf.Clamp01(1f - (remaining / totalPathDistance));
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.stoppingDistance = arrivalThreshold;
        agent.autoBraking = true;
    }

    private void Start()
    {
        UpdateCircleScale();
    }

    /// <summary>Sets the destination gate point and starts pathfinding.</summary>
    public void InitializeDestination(Vector3 destination)
    {
        destinationPoint = destination;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(destinationPoint);
        }
        totalPathDistance = Vector3.Distance(transform.position, destinationPoint);
        isInitialized = true;
        UpdateCircleScale();
    }

    public void SetEscortRadius(float radius)
    {
        escortRadius = Mathf.Max(1f, radius);
        UpdateCircleScale();
    }

    private void UpdateCircleScale()
    {
        if (rangeCircleTransform != null)
        {
            rangeCircleTransform.localScale = new Vector3(escortRadius * 2f, escortRadius * 2f, 1f);
        }
    }

    private void Update()
    {
        if (!isInitialized || hasArrived) return;

        CheckPlayerProximity();
        DriveMovement();
        CheckArrival();
    }

    private void CheckPlayerProximity()
    {
        bool wasInRadius = isPlayerInRadius;
        Vector3 playerPos = Player.Instance != null ? Player.Instance.transform.position : transform.position + Vector3.forward * 100f;
        float distToPlayer = Vector3.Distance(transform.position, playerPos);

        isPlayerInRadius = distToPlayer <= escortRadius;

        if (isPlayerInRadius != wasInRadius)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (rangeCircleRenderer != null)
        {
            rangeCircleRenderer.material.color = isPlayerInRadius ? activeColor : inactiveColor;
        }

        if (movementAudioSource != null)
        {
            if (isPlayerInRadius && !movementAudioSource.isPlaying)
            {
                movementAudioSource.Play();
            }
            else if (!isPlayerInRadius && movementAudioSource.isPlaying)
            {
                movementAudioSource.Pause();
            }
        }

        if (wheelDustParticles != null)
        {
            if (isPlayerInRadius && !wheelDustParticles.isPlaying)
            {
                wheelDustParticles.Play();
            }
            else if (!isPlayerInRadius && wheelDustParticles.isPlaying)
            {
                wheelDustParticles.Stop();
            }
        }
    }

    private void DriveMovement()
    {
        if (isPlayerInRadius)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;

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

    private void CheckArrival()
    {
        if (hasArrived) return;

        float distToDest = Vector3.Distance(transform.position, destinationPoint);
        if (distToDest <= arrivalThreshold)
        {
            hasArrived = true;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            isPlayerInRadius = false;
            UpdateVisualState();

            StartCoroutine(ArrivalBurstRoutine());
        }
    }

    private IEnumerator ArrivalBurstRoutine()
    {
        if (burstDelay > 0f)
        {
            yield return new WaitForSeconds(burstDelay);
        }

        if (arrivalFeedback != null)
        {
            arrivalFeedback.PlayFeedbacks();
        }

        if (arrivalSound != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = transform.position;
            options.Volume = 1.0f;
            MMSoundManagerSoundPlayEvent.Trigger(arrivalSound, options);
        }

        if (goldBurstVfxPrefab != null)
        {
            Instantiate(goldBurstVfxPrefab, transform.position + dropOffset, Quaternion.identity);
        }

        SpawnGoldBags();

        // Disable colliders immediately upon burst so movement/combat no longer interacts with cart
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        if (rangeCircleTransform != null)
        {
            rangeCircleTransform.gameObject.SetActive(false);
        }

        // Hide mesh renderers so the cart vanishes with the burst effect
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = false;
        }

        OnArrived?.Invoke(this);

        Destroy(gameObject, Mathf.Max(0.05f, destroyDelay));
    }

    private void SpawnGoldBags()
    {
        Coin prefab = goldBagPrefab;
#if UNITY_EDITOR
        if (prefab == null)
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Coin>("Assets/Bladehold/Bladehold Prefabs/SM_Icon_CoinBag_01/SM_Icon_CoinBag_01.prefab");
        }
#endif

        if (prefab == null)
        {
            Debug.LogWarning("[SupplyWagonEscort] goldBagPrefab is not assigned and could not be loaded!");
            return;
        }

        int count = UnityEngine.Random.Range(minGoldBags, maxGoldBags + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * dropScatterRadius;
            Vector3 spawnPos = transform.position + dropOffset + new Vector3(jitter.x, 0f, jitter.y);
            Coin bag = Instantiate(prefab, spawnPos, Quaternion.identity);
            if (bag != null)
            {
                bag.SetAmount(goldPerBag);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, escortRadius);
    }
}
