using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Goblin Caravan's gold wagon: drives NavMesh legs across the field (<see cref="GoblinCaravanEvent" />
///     plans each one), spills a gold bag every 10% of health lost, and bursts into a pile of gold bags when
///     wrecked. When the event's timer runs out <see cref="Escape" /> drives it off with the loot. Shows on the
///     HUD waypoint tracker while it's alive.
/// </summary>
public class CaravanWagon : MonoBehaviour, IWaypointSource
{
    [SerializeField] private Health health;
    [SerializeField] private NavMeshAgent agent;
    [Tooltip("The gold bag pickup (SM_Icon_CoinBag_01).")]
    [SerializeField] private Coin goldBagPrefab;

    [Header("Waypoint")]
    [SerializeField] private Sprite waypointIcon;
    [SerializeField] private Color waypointTint = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private Vector3 waypointOffset = new Vector3(0f, 3.5f, 0f);

    [Header("Feedback")]
    [Tooltip("Coin jingle + small coin burst each time a gold bag spills off.")]
    [SerializeField] private MMF_Player goldSpillFeedback;
    [Tooltip("The wreck: splintering wood, a coin fountain, a fanfare.")]
    [SerializeField] private MMF_Player wreckFeedback;
    [Tooltip("Optional: dust poof and a goblin cackle as it gets away.")]
    [SerializeField] private MMF_Player escapeFeedback;

    private GoblinCaravanEventSO config;
    private float lastDropHealth;
    private float goldMultiplier = 1f;
    private bool finished;
    private bool anyError;

    /// <summary>Raised once when the wagon is destroyed by damage.</summary>
    public event Action Wrecked;

    /// <summary>Raised once when the wagon leaves the field with its gold.</summary>
    public event Action Escaped;

    /// <summary>Raised when the wagon reaches the end of its current leg; the event gives it the next one.</summary>
    public event Action LegFinished;

    public Health Health => health;

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    private void Awake()
    {
        if (health == null || agent == null || goldBagPrefab == null)
        {
            Debug.LogError($"[CaravanWagon] {name} is missing Health, NavMeshAgent or goldBagPrefab.", this);
            anyError = true;
        }
        if (goldSpillFeedback == null) Debug.LogError($"[CaravanWagon] {name}: goldSpillFeedback is not assigned.", this);
        if (wreckFeedback == null) Debug.LogError($"[CaravanWagon] {name}: wreckFeedback is not assigned.", this);
    }

    /// <summary>Call right after Instantiate (before Start), with the first leg already planned.</summary>
    public void Launch(GoblinCaravanEventSO caravanConfig, Vector3 firstDestination)
    {
        config = caravanConfig;
        if (anyError) return;

        health.SetMaxHealth(config.wagonHealth);
        lastDropHealth = config.wagonHealth;
        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;

        PlayerStats stats = Player.Instance != null ? Player.Instance.Stats : null;
        float m = stats != null ? stats.GetValue(StatType.GoldDropMultiplier) : 1f;
        goldMultiplier = m > 0f ? m : 1f;

        agent.speed = config.wagonSpeed;
        DriveTo(firstDestination);
        ObjectiveWaypointTrackerUI.RegisterSource(this);
    }

    /// <summary>Starts the next leg.</summary>
    public void DriveTo(Vector3 destination)
    {
        if (anyError || finished || !agent.isOnNavMesh) return;
        agent.SetDestination(destination);
    }

    private void Update()
    {
        if (anyError || finished || config == null || !agent.isOnNavMesh) return;
        // remainingDistance reads 0 until the first path lands, so only trust it once there is a path.
        if (agent.pathPending || !agent.hasPath) return;
        if (agent.remainingDistance > agent.stoppingDistance + 0.5f) return;
        agent.ResetPath();
        LegFinished?.Invoke();
    }

    private void HandleDamaged(Damage damage)
    {
        if (finished) return;
        float chunk = config.wagonHealth * 0.1f;
        while (lastDropHealth - health.CurrentHealth >= chunk && lastDropHealth > chunk * 0.5f)
        {
            lastDropHealth -= chunk;
            DropBag(Gold(config.goldPerChunk));
            if (goldSpillFeedback != null) goldSpillFeedback.PlayFeedbacks(transform.position + Vector3.up);
        }
    }

    private void HandleDied()
    {
        if (finished) return;
        finished = true;
        ObjectiveWaypointTrackerUI.UnregisterSource(this);
        if (agent.isOnNavMesh) agent.isStopped = true;

        int bags = Mathf.Max(1, config.burstBags);
        int perBag = Mathf.Max(1, Gold(config.wreckGold) / bags);
        for (int i = 0; i < bags; i++) DropBag(perBag);
        if (wreckFeedback != null) wreckFeedback.PlayFeedbacks(transform.position + Vector3.up);

        Wrecked?.Invoke();
        Destroy(gameObject, 0.1f);
    }

    /// <summary>The caravan gets away with the gold (the event timer ran out). Safe to call twice.</summary>
    public void Escape()
    {
        if (finished) return;
        finished = true;
        ObjectiveWaypointTrackerUI.UnregisterSource(this);
        if (escapeFeedback != null) escapeFeedback.PlayFeedbacks(transform.position + Vector3.up);
        Escaped?.Invoke();
        Destroy(gameObject);
    }

    private int Gold(int amount) => Mathf.Max(1, Mathf.RoundToInt(amount * goldMultiplier));

    private void DropBag(int amount)
    {
        if (goldBagPrefab == null || amount <= 0) return;
        Vector2 scatter = UnityEngine.Random.insideUnitCircle * config.bagScatter;
        Vector3 at = transform.position + new Vector3(scatter.x, 1f, scatter.y);
        Coin bag = Instantiate(goldBagPrefab, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        bag.SetAmount(amount);
    }

    public void GetWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (finished || results == null) return;
        results.Add(new ObjectiveWaypointTarget(transform, waypointOffset, waypointIcon, waypointTint,
            Loc.Get("world_event.caravan.waypoint", "Caravan")));
    }

    private void OnDestroy()
    {
        ObjectiveWaypointTrackerUI.UnregisterSource(this);
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }
}
