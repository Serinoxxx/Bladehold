using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Survivors objective: Protect the supply wagon.
///     A slow moving cart rolls from a spawn point toward the gate along NavMesh only when the player
///     is inside its proximity circle. It must be delivered before the timer (<c>timerSeconds</c> of its
///     <c>WaveObjectives.csv</c> row, 3:00) runs out, or the objective fails and the card reward is lost.
///     The wagon has no health of its own, so the timer is the only way to fail.
/// </summary>
public class ProtectWagonObjective : MonoBehaviour, ISurvivorsObjective, IRequiresContinuousSpawns, IObjectivePreview
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "protect_supply_wagon";
    [SerializeField] private string title = "Protect the Supply Wagon";
    [SerializeField] private string description = "Escort the supply cart to the fortress gate";

    [Header("Timer & Failure Configuration")]
    [Tooltip("Fallback time limit in seconds when WaveObjectives.csv has no row for this objective (the row's timerSeconds wins). <= 0 means no time limit.")]
    [SerializeField] private float timeLimit = 180f;

    [Header("Prefabs & Route")]
    [Tooltip("Prefab instantiated for the supply wagon. Must contain SupplyWagonEscort.")]
    [SerializeField] private GameObject supplyWagonPrefab;

    [Tooltip("Transform marking the starting spawn position for the wagon.")]
    [SerializeField] private Transform wagonSpawnPoint;

    [Tooltip("Transform marking the destination gate.")]
    [SerializeField] private Transform gateDestinationPoint;

    [Tooltip("Fallback spawn point if wagonSpawnPoint is unassigned.")]
    [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(-25f, 0f, 35f);

    [Tooltip("Fallback destination point if gateDestinationPoint is unassigned.")]
    [SerializeField] private Vector3 fallbackDestinationPosition = new Vector3(0f, 0f, 0f);

    private SupplyWagonEscort currentWagon;
    private bool isActive;
    private bool isComplete;
    private bool isFailed;
    private float lastReportedProgress;
    private float timeRemaining;
    private float activeTimeLimit = -1f;
    private int lastReportedSeconds = -1;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public float TimeLimit => activeTimeLimit >= 0f ? activeTimeLimit : ObjectiveCsv.TimerSeconds(objectiveId, timeLimit);
    public float TimeRemaining => timeRemaining;

    public string ProgressText
    {
        get
        {
            if (isFailed) return "Failed! The wagon didn't reach the gate in time.";
            if (isComplete) return "Supply wagon delivered!";

            string clock = TimeLimit > 0f ? $" ({ObjectiveCsv.FormatClock(timeRemaining)})" : "";
            if (currentWagon == null) return $"Escort wagon to the gate{clock}";
            int pct = Mathf.RoundToInt(currentWagon.ProgressNormalized * 100f);
            string proximityState = currentWagon.IsPlayerInRadius ? "Moving" : "Paused (Get closer!)";
            return $"Escort progress: {pct}% [{proximityState}]{clock}";
        }
    }

    public float ProgressNormalized => currentWagon != null ? currentWagon.ProgressNormalized : (isComplete ? 1f : 0f);
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
        lastReportedProgress = 0f;
        activeTimeLimit = ObjectiveCsv.TimerSeconds(objectiveId, timeLimit);
        timeRemaining = activeTimeLimit;
        lastReportedSeconds = Mathf.CeilToInt(timeRemaining);

        SpawnWagon();
        OnProgressChanged?.Invoke(this);
    }

    private void SpawnWagon()
    {
        if (supplyWagonPrefab == null)
        {
            Debug.LogError("[ProtectWagonObjective] supplyWagonPrefab is not assigned!");
            return;
        }

        Vector3 spawnPos = wagonSpawnPoint != null ? wagonSpawnPoint.position : fallbackSpawnPosition;
        Quaternion spawnRot = wagonSpawnPoint != null ? wagonSpawnPoint.rotation : Quaternion.identity;

        GameObject instance = Instantiate(supplyWagonPrefab, spawnPos, spawnRot);
        currentWagon = instance.GetComponent<SupplyWagonEscort>();
        if (currentWagon == null)
        {
            currentWagon = instance.AddComponent<SupplyWagonEscort>();
        }

        Vector3 destPos = gateDestinationPoint != null ? gateDestinationPoint.position : fallbackDestinationPosition;
        currentWagon.InitializeDestination(destPos);
        currentWagon.OnArrived += HandleWagonArrived;
    }

    private void HandleWagonArrived(SupplyWagonEscort wagon)
    {
        if (!isActive || isComplete || isFailed) return;

        isComplete = true;
        isActive = false;
        OnProgressChanged?.Invoke(this);
        OnCompleted?.Invoke(this);
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete || isFailed) return;

        if (activeTimeLimit > 0f)
        {
            timeRemaining -= deltaTime;
            int currentSeconds = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
            if (currentSeconds != lastReportedSeconds)
            {
                lastReportedSeconds = currentSeconds;
                OnProgressChanged?.Invoke(this);
            }

            // A wagon that has reached the gate is delivered: it raises OnArrived after its short burst
            // delay, so don't fail it in that window.
            bool delivered = currentWagon != null && currentWagon.HasArrived;
            if (timeRemaining <= 0f && !delivered)
            {
                timeRemaining = 0f;
                HandleTimeout();
                return;
            }
        }

        if (currentWagon == null) return;

        // Fire progress updates periodically or on significant change
        float currentProg = currentWagon.ProgressNormalized;
        if (Mathf.Abs(currentProg - lastReportedProgress) > 0.02f)
        {
            lastReportedProgress = currentProg;
            OnProgressChanged?.Invoke(this);
        }
    }

    private void HandleTimeout()
    {
        if (!isActive || isComplete || isFailed) return;

        isFailed = true;
        isActive = false;
        RemoveWagon();

        OnProgressChanged?.Invoke(this);
        OnFailed?.Invoke(this);
    }

    /// <summary>Unsubscribes and removes an undelivered wagon (a delivered one destroys itself after its burst).</summary>
    private void RemoveWagon()
    {
        if (currentWagon == null) return;

        currentWagon.OnArrived -= HandleWagonArrived;
        if (!currentWagon.HasArrived)
        {
            Destroy(currentWagon.gameObject);
        }
        currentWagon = null;
    }

    public void CleanupObjective()
    {
        isActive = false;
        RemoveWagon();
    }

    private void OnDestroy()
    {
        if (currentWagon != null)
        {
            currentWagon.OnArrived -= HandleWagonArrived;
        }
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        if (isActive && !isComplete && currentWagon != null && !currentWagon.HasArrived)
        {
            return currentWagon.transform.position;
        }
        return null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        // Enemies flock to intercept the wagon and wait for the player; they do not attack the wagon.
        return null;
    }

    [Header("Waypoint Icon Configuration")]
    [Tooltip("Optional custom waypoint icon for the supply wagon.")]
    [SerializeField] private Sprite wagonWaypointIcon;

    [Tooltip("Optional custom waypoint icon for the destination gate.")]
    [SerializeField] private Sprite destinationWaypointIcon;

    public void GetActiveWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (!isActive || isComplete || results == null) return;

        if (currentWagon != null && !currentWagon.HasArrived)
        {
            results.Add(new ObjectiveWaypointTarget(
                currentWagon.transform,
                worldOffset: new Vector3(0f, 2.0f, 0f),
                customIcon: wagonWaypointIcon,
                tintColor: new Color(0.2f, 0.9f, 0.4f, 1f),
                label: "Supply Cart"
            ));
        }

        Transform dest = gateDestinationPoint;
        if (dest != null)
        {
            results.Add(new ObjectiveWaypointTarget(
                dest,
                worldOffset: new Vector3(0f, 2.5f, 0f),
                customIcon: destinationWaypointIcon,
                tintColor: new Color(0.4f, 0.8f, 1f, 1f),
                label: "Gate"
            ));
        }
    }

    public void GetPreviewWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (results == null) return;

        if (wagonSpawnPoint != null)
        {
            results.Add(new ObjectiveWaypointTarget(
                wagonSpawnPoint,
                worldOffset: new Vector3(0f, 2.0f, 0f),
                customIcon: wagonWaypointIcon,
                tintColor: ObjectiveCsv.PreviewTint(new Color(0.2f, 0.9f, 0.4f)),
                label: Loc.Get("wave.obj.supply_wagon.preview_start", "Wagon start")));
        }

        if (gateDestinationPoint != null)
        {
            results.Add(new ObjectiveWaypointTarget(
                gateDestinationPoint,
                worldOffset: new Vector3(0f, 2.5f, 0f),
                customIcon: destinationWaypointIcon,
                tintColor: ObjectiveCsv.PreviewTint(new Color(0.4f, 0.8f, 1f)),
                label: Loc.Get("wave.obj.supply_wagon.preview_end", "Wagon destination")));
        }
    }
}
