using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Survivors objective: Free the prisoners.
///     Spawns prisoner cages across the battlefield. Player must find and break the cages to free them.
/// </summary>
public class FreePrisonersObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "free_prisoners";
    [SerializeField] private string title = "Free the Prisoners";
    [SerializeField] private string description = "Break open the cages to free the prisoners";
    [SerializeField] private int requiredCount = 3;

    [Header("Timer & Failure Configuration")]
    [Tooltip("Time limit in seconds to free all prisoners before failing (e.g. 120s = 2 minutes). <= 0 means no time limit.")]
    [SerializeField] private float timeLimit = 120f;

    [Header("Prefabs & Spawn Points")]
    [Tooltip("Prefab instantiated for each cage. Must contain PrisonerCage component.")]
    [SerializeField] private GameObject cagePrefab;

    [Tooltip("Pre-placed spawn point transforms for the cages.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Fallback spawn offsets if spawn points array is empty.")]
    [SerializeField] private Vector3[] fallbackSpawnPositions = new Vector3[]
    {
        new Vector3(-15f, 0f, 15f),
        new Vector3(18f, 0f, 22f),
        new Vector3(-10f, 0f, 35f)
    };

    private readonly List<PrisonerCage> spawnedCages = new List<PrisonerCage>();
    private int freedCount;
    private float timeRemaining;
    private int lastReportedSeconds = -1;
    private bool isActive;
    private bool isComplete;
    private bool isFailed;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public float TimeLimit => timeLimit;
    public float TimeRemaining => timeRemaining;
    public bool IsComplete => isComplete;
    public bool IsFailed => isFailed;
    public bool IsActive => isActive;

    public string ProgressText
    {
        get
        {
            if (isFailed)
            {
                return "Failed! Time expired to rescue prisoners!";
            }
            if (isComplete)
            {
                return $"Prisoners freed: {freedCount}/{requiredCount}";
            }
            if (timeLimit > 0f)
            {
                int totalSec = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
                int mins = totalSec / 60;
                int secs = totalSec % 60;
                return $"Prisoners freed: {freedCount}/{requiredCount} ({mins}:{secs:D2})";
            }
            return $"Prisoners freed: {freedCount}/{requiredCount}";
        }
    }

    public float ProgressNormalized => requiredCount > 0 ? Mathf.Clamp01((float)freedCount / requiredCount) : 1f;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        isFailed = false;
        freedCount = 0;
        timeRemaining = timeLimit;
        lastReportedSeconds = Mathf.CeilToInt(timeRemaining);
        spawnedCages.Clear();

        SpawnCages();
        OnProgressChanged?.Invoke(this);
    }

    private void SpawnCages()
    {
        if (cagePrefab == null)
        {
            Debug.LogError("[FreePrisonersObjective] cagePrefab is not assigned!");
            return;
        }

        int countToSpawn = requiredCount;
        for (int i = 0; i < countToSpawn; i++)
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            if (spawnPoints != null && spawnPoints.Length > i && spawnPoints[i] != null)
            {
                pos = spawnPoints[i].position;
                rot = spawnPoints[i].rotation;
            }
            else if (fallbackSpawnPositions != null && fallbackSpawnPositions.Length > i)
            {
                Vector3 center = Player.Instance != null ? Player.Instance.transform.position : Vector3.zero;
                pos = center + fallbackSpawnPositions[i];
            }

            GameObject instance = Instantiate(cagePrefab, pos, rot);
            PrisonerCage cage = instance.GetComponent<PrisonerCage>();
            if (cage == null)
            {
                cage = instance.AddComponent<PrisonerCage>();
            }

            spawnedCages.Add(cage);
            cage.OnCageBroken += HandleCageBroken;
        }
    }

    private void HandleCageBroken(PrisonerCage cage)
    {
        if (!isActive || isComplete || isFailed) return;

        freedCount++;
        OnProgressChanged?.Invoke(this);

        if (freedCount >= requiredCount)
        {
            isComplete = true;
            isActive = false;
            OnCompleted?.Invoke(this);
        }
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete || isFailed) return;

        if (timeLimit > 0f)
        {
            timeRemaining -= deltaTime;
            int currentSeconds = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
            if (currentSeconds != lastReportedSeconds)
            {
                lastReportedSeconds = currentSeconds;
                OnProgressChanged?.Invoke(this);
            }

            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                HandleTimeout();
            }
        }
    }

    private void HandleTimeout()
    {
        if (!isActive || isComplete || isFailed) return;

        isFailed = true;
        isActive = false;

        Debug.Log("[FreePrisonersObjective] Timed out! Failed to rescue prisoners in time.");

        OnProgressChanged?.Invoke(this);
        OnFailed?.Invoke(this);
    }

    public void CleanupObjective()
    {
        isActive = false;
        foreach (PrisonerCage cage in spawnedCages)
        {
            if (cage != null && !cage.IsBroken)
            {
                cage.OnCageBroken -= HandleCageBroken;
                Destroy(cage.gameObject);
            }
        }
        spawnedCages.Clear();
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        PrisonerCage nearest = GetNearestCage(searchFromPosition);
        return nearest != null ? nearest.transform.position : (Vector3?)null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        // Enemies flock to and guard prisoner cages; they do not attack them.
        return null;
    }

    private PrisonerCage GetNearestCage(Vector3 searchFromPosition)
    {
        if (!isActive || isComplete || isFailed) return null;
        PrisonerCage best = null;
        float bestSqDist = float.MaxValue;
        foreach (PrisonerCage cage in spawnedCages)
        {
            if (cage != null && !cage.IsBroken)
            {
                float sqDist = (cage.transform.position - searchFromPosition).sqrMagnitude;
                if (sqDist < bestSqDist)
                {
                    bestSqDist = sqDist;
                    best = cage;
                }
            }
        }
        return best;
    }

    [Header("Waypoint Icon Configuration")]
    [Tooltip("Optional custom waypoint icon for unbroken cages.")]
    [SerializeField] private Sprite cageWaypointIcon;

    public void GetActiveWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (!isActive || isComplete || isFailed || results == null) return;

        for (int i = 0; i < spawnedCages.Count; i++)
        {
            PrisonerCage cage = spawnedCages[i];
            if (cage != null && !cage.IsBroken)
            {
                results.Add(new ObjectiveWaypointTarget(
                    cage.transform,
                    worldOffset: new Vector3(0f, 1.8f, 0f),
                    customIcon: cageWaypointIcon,
                    tintColor: new Color(1f, 0.8f, 0.2f, 1f),
                    label: "Prisoner"
                ));
            }
        }
    }
}
