using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Survivors objective: A Golden Goblin spawns and runs around a set of waypoints.
///     No regular enemies spawn during this objective (<see cref="ISuppressRegularSpawns" />). It drops
///     gold every 10% HP lost and a bonus on death. Killing it completes the objective; if the timer
///     (<c>timerSeconds</c> of its <c>WaveObjectives.csv</c> row) runs out it escapes and the objective fails.
///     Parked (plan 20): its CSV row is <c>draftable=false</c>, so wave cards never offer it.
/// </summary>
public class GoldenGoblinObjective : MonoBehaviour, ISurvivorsObjective, ISuppressRegularSpawns, IObjectivePreview
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "golden_goblin_event";
    [SerializeField] private string title = "Golden Goblin";
    [SerializeField] private string description = "Catch the Golden Goblin before he escapes!";
    [Tooltip("Fallback escape timer when WaveObjectives.csv has no row for this objective.")]
    [SerializeField] private float duration = 30f;

    [Header("Golden Goblin Settings")]
    [SerializeField] private GameObject goldenGoblinPrefab;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private Coin coinPrefab;
    [SerializeField] private int goldPerDrop = 5;
    [SerializeField] private int killBonusGold = 100;
    [SerializeField] private float maxHealthOverride = 999f;
    [SerializeField] private float knockbackResistOverride = 999f;

    private float timer;
    private float activeDuration;
    private int lastReportedSeconds = -1;
    private bool isActive;
    private bool isComplete;
    private bool isFailed;
    private Health targetHealth;
    private NavMeshAgent targetAgent;
    private int currentWaypointIndex;
    private float initialHealth;
    private float lastDropHealth;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public float Duration => activeDuration > 0f ? activeDuration : ObjectiveCsv.RequiredDuration(objectiveId, duration);

    public string ProgressText
    {
        get
        {
            if (isFailed) return Loc.Get("wave.obj.golden_goblin.escaped", "The Golden Goblin escaped!");
            if (isComplete) return Loc.Get("wave.obj.golden_goblin.caught", "Golden Goblin caught!");
            int secs = Mathf.Max(0, Mathf.CeilToInt(Duration - timer));
            return Loc.Get("wave.obj.golden_goblin.progress", "Escapes in: {0}s").Replace("{0}", secs.ToString());
        }
    }

    public float ProgressNormalized => Duration > 0f ? Mathf.Clamp01(timer / Duration) : 1f;
    public bool IsComplete => isComplete;
    public bool IsFailed => isFailed;
    public bool IsActive => isActive;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        isFailed = false;
        timer = 0f;
        activeDuration = ObjectiveCsv.RequiredDuration(objectiveId, duration);
        lastReportedSeconds = Mathf.CeilToInt(activeDuration);

        // Belt and braces: the real "no regular spawns" rule is ISuppressRegularSpawns, which
        // GameLoopManager honours when it starts the wave (the spawner is started after this runs).
        if (SurvivorsSpawner.Instance != null)
        {
            SurvivorsSpawner.Instance.StopSpawning();
        }

        SpawnGoblin();
        if (isActive) OnProgressChanged?.Invoke(this);
    }

    private void SpawnGoblin()
    {
        if (goldenGoblinPrefab == null || waypoints == null || waypoints.Length == 0 || waypoints[0] == null)
        {
            Debug.LogError("[GoldenGoblinObjective] Missing goldenGoblinPrefab or waypoints; completing the objective instantly.", this);
            CompleteObjective();
            return;
        }

        Vector3 spawnPos = waypoints[0].position;
        GameObject goblinGo = Instantiate(goldenGoblinPrefab, spawnPos, Quaternion.identity);

        GoldenGoblinFlee flee = goblinGo.GetComponent<GoldenGoblinFlee>();
        if (flee != null) flee.enabled = false;
        AIMovement aiMove = goblinGo.GetComponent<AIMovement>();
        if (aiMove != null) aiMove.enabled = false;

        targetHealth = goblinGo.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.SetMaxHealth(maxHealthOverride);
            targetHealth.Heal(maxHealthOverride);

            KnockbackReceiver kr = goblinGo.GetComponent<KnockbackReceiver>();
            if (kr != null) kr.SetResistance(knockbackResistOverride);

            ImpulseGoblin impulse = goblinGo.GetComponent<ImpulseGoblin>();
            if (impulse != null) impulse.enabled = false;

            targetHealth.OnDamaged += HandleGoblinDamaged;
            targetHealth.OnDied += HandleGoblinDied;

            initialHealth = targetHealth.MaxHealth;
            lastDropHealth = initialHealth;
        }
        else
        {
            Debug.LogError($"[GoldenGoblinObjective] Golden goblin prefab '{goblinGo.name}' has no Health.", this);
        }

        targetAgent = goblinGo.GetComponent<NavMeshAgent>();
        if (targetAgent != null)
        {
            targetAgent.speed = 8f;
            currentWaypointIndex = 1 % waypoints.Length;
            if (waypoints[currentWaypointIndex] != null && targetAgent.isOnNavMesh)
            {
                targetAgent.SetDestination(waypoints[currentWaypointIndex].position);
            }
        }
    }

    private void HandleGoblinDamaged(Damage damage)
    {
        if (targetHealth == null || isComplete || isFailed || !isActive) return;

        float dropThreshold = initialHealth * 0.1f;
        if (dropThreshold <= 0f) return;

        while (lastDropHealth - targetHealth.CurrentHealth >= dropThreshold)
        {
            lastDropHealth -= dropThreshold;
            DropGold(goldPerDrop);
        }
    }

    private void HandleGoblinDied()
    {
        if (isComplete || isFailed || !isActive) return;

        DropGold(killBonusGold);
        CompleteObjective();
    }

    private void DropGold(int amount)
    {
        if (coinPrefab == null || targetHealth == null || amount <= 0) return;

        Coin coin = Instantiate(coinPrefab, targetHealth.transform.position + Vector3.up, Quaternion.identity);
        coin.SetAmount(amount);
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete || isFailed) return;

        timer += deltaTime;
        int currentSeconds = Mathf.Max(0, Mathf.CeilToInt(activeDuration - timer));
        if (currentSeconds != lastReportedSeconds)
        {
            lastReportedSeconds = currentSeconds;
            OnProgressChanged?.Invoke(this);
        }

        if (timer >= activeDuration)
        {
            HandleEscape();
            return;
        }

        if (targetAgent != null && targetAgent.isOnNavMesh && waypoints != null && waypoints.Length > 0)
        {
            if (!targetAgent.pathPending && targetAgent.remainingDistance < 1f)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                if (waypoints[currentWaypointIndex] != null)
                {
                    targetAgent.SetDestination(waypoints[currentWaypointIndex].position);
                }
            }
        }
    }

    /// <summary>Timer ran out: the goblin gets away and the objective fails (the card reward is lost).</summary>
    private void HandleEscape()
    {
        if (!isActive || isComplete || isFailed) return;

        isFailed = true;
        isActive = false;
        DespawnGoblin();

        OnProgressChanged?.Invoke(this);
        OnFailed?.Invoke(this);
    }

    private void CompleteObjective()
    {
        isActive = false;
        isComplete = true;
        OnProgressChanged?.Invoke(this);
        OnCompleted?.Invoke(this);
    }

    /// <summary>Unsubscribes and removes a still-living goblin. A dead one is left as a corpse.</summary>
    private void DespawnGoblin()
    {
        if (targetHealth == null) return;

        targetHealth.OnDamaged -= HandleGoblinDamaged;
        targetHealth.OnDied -= HandleGoblinDied;
        if (!targetHealth.IsDead)
        {
            Destroy(targetHealth.gameObject);
        }
        targetHealth = null;
        targetAgent = null;
    }

    public void CleanupObjective()
    {
        isActive = false;
        DespawnGoblin();
    }

    private void OnDestroy()
    {
        if (targetHealth != null)
        {
            targetHealth.OnDamaged -= HandleGoblinDamaged;
            targetHealth.OnDied -= HandleGoblinDied;
        }
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        return targetHealth != null ? targetHealth.transform.position : null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        return targetHealth;
    }

    public void GetActiveWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (!isActive || results == null) return;

        if (targetHealth != null && !targetHealth.IsDead)
        {
            results.Add(new ObjectiveWaypointTarget(targetHealth.transform, label: Loc.Get("wave.obj.golden_goblin.waypoint", "Golden Goblin")));
        }
    }

    public void GetPreviewWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (results == null || waypoints == null || waypoints.Length == 0 || waypoints[0] == null) return;

        // It always spawns on the first waypoint, then laps the rest.
        results.Add(new ObjectiveWaypointTarget(
            waypoints[0],
            worldOffset: new Vector3(0f, 1.8f, 0f),
            tintColor: ObjectiveCsv.PreviewTint(new Color(1f, 0.85f, 0.2f)),
            label: Loc.Get("wave.obj.golden_goblin.preview", "Golden Goblin")));
    }
}
