using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Central manager for survival mode objectives. Sequences the introductory wave objective
///     (kill 50 goblins), then randomly selects and rotates subsequent objectives.
///     Integrates with the HUD, Quest Complete banner, and handles rewards.
/// </summary>
public class SurvivorsObjectiveManager : MonoBehaviour
{
    public static SurvivorsObjectiveManager Instance { get; private set; }

    [Header("Objectives Configuration")]
    [Tooltip("Initial objective played at start (e.g. KillEnemiesObjective for 50 goblins).")]
    [SerializeField] private KillEnemiesObjective introductoryObjective;

    [Tooltip("Pool of repeating objectives drawn randomly after the introductory wave.")]
    [SerializeField] private List<MonoBehaviour> repeatingObjectiveComponents = new List<MonoBehaviour>();

    [Header("Rewards & Pacing")]
    [Tooltip("Bonus gold XP granted to level progression when an objective is cleared.")]
    [SerializeField] private int goldXpRewardPerObjective = 100;

    [Tooltip("Intermission delay in seconds between completing an objective and starting the next.")]
    [SerializeField] private float intermissionDuration = 4.0f;

    private readonly List<ISurvivorsObjective> objectivePool = new List<ISurvivorsObjective>();
    private ISurvivorsObjective currentObjective;
    private int lastObjectiveIndex = -1;
    private int completedObjectiveCount = 0;
    private bool isRunning;

    public ISurvivorsObjective CurrentObjective => currentObjective;
    public int CompletedObjectiveCount => completedObjectiveCount;
    public int GoldXpRewardPerObjective => goldXpRewardPerObjective;
    public IReadOnlyList<ISurvivorsObjective> ObjectivePool => objectivePool;

    public event Action<ISurvivorsObjective> OnObjectiveStarted;
    public event Action<ISurvivorsObjective> OnObjectiveProgressChanged;
    public event Action<ISurvivorsObjective> OnObjectiveCompleted;
    public event Action<ISurvivorsObjective> OnObjectiveFailed;

    /// <summary>Debug method: Stops any active intermission and immediately starts the next objective in rotation.</summary>
    public void DebugNextObjective()
    {
        StopAllCoroutines();
        ISurvivorsObjective next = PickNextRandomObjective();
        if (next != null)
        {
            SetActiveObjective(next);
        }
    }

    /// <summary>Debug method: Forces starting a specific objective from the pool by index.</summary>
    public void DebugStartObjective(int index)
    {
        if (index >= 0 && index < objectivePool.Count)
        {
            StopAllCoroutines();
            lastObjectiveIndex = index;
            SetActiveObjective(objectivePool[index]);
        }
    }

    /// <summary>Debug method: Instantly marks the current objective as complete.</summary>
    public void DebugCompleteCurrentObjective()
    {
        if (currentObjective != null && !currentObjective.IsComplete)
        {
            StopAllCoroutines();
            HandleObjectiveCompleted(currentObjective);
        }
    }

    /// <summary>Debug method: Instantly marks the current objective as failed.</summary>
    public void DebugFailCurrentObjective()
    {
        if (currentObjective != null && !currentObjective.IsComplete && !currentObjective.IsFailed)
        {
            StopAllCoroutines();
            HandleObjectiveFailed(currentObjective);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Populate objective pool
        if (repeatingObjectiveComponents != null)
        {
            foreach (MonoBehaviour mb in repeatingObjectiveComponents)
            {
                if (mb is ISurvivorsObjective obj)
                {
                    objectivePool.Add(obj);
                }
            }
        }

        // If introductory objective is set, add to pool if missing or start directly
        if (introductoryObjective != null && !objectivePool.Contains(introductoryObjective))
        {
            objectivePool.Insert(0, introductoryObjective);
        }

        StartInitialObjective();
    }

    private void StartInitialObjective()
    {
        isRunning = true;
        if (introductoryObjective != null)
        {
            SetActiveObjective(introductoryObjective);
        }
        else if (objectivePool.Count > 0)
        {
            SetActiveObjective(objectivePool[0]);
        }
        else
        {
            Debug.LogWarning("[SurvivorsObjectiveManager] No objectives configured in pool!");
        }
    }

    private void Update()
    {
        if (!isRunning || currentObjective == null) return;

        currentObjective.UpdateObjective(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (currentObjective != null)
        {
            currentObjective.OnProgressChanged -= HandleObjectiveProgress;
            currentObjective.OnCompleted -= HandleObjectiveCompleted;
            currentObjective.OnFailed -= HandleObjectiveFailed;
            currentObjective.CleanupObjective();
        }
    }

    private void SetActiveObjective(ISurvivorsObjective obj)
    {
        if (currentObjective != null)
        {
            currentObjective.OnProgressChanged -= HandleObjectiveProgress;
            currentObjective.OnCompleted -= HandleObjectiveCompleted;
            currentObjective.OnFailed -= HandleObjectiveFailed;
            currentObjective.CleanupObjective();
        }

        currentObjective = obj;
        if (currentObjective != null)
        {
            currentObjective.OnProgressChanged += HandleObjectiveProgress;
            currentObjective.OnCompleted += HandleObjectiveCompleted;
            currentObjective.OnFailed += HandleObjectiveFailed;
            currentObjective.StartObjective();
            OnObjectiveStarted?.Invoke(currentObjective);
        }
    }

    private void HandleObjectiveProgress(ISurvivorsObjective obj)
    {
        OnObjectiveProgressChanged?.Invoke(obj);
    }

    private void HandleObjectiveCompleted(ISurvivorsObjective obj)
    {
        completedObjectiveCount++;
        Debug.Log($"[SurvivorsObjectiveManager] Objective '{obj.Title}' Completed! (Total: {completedObjectiveCount})");

        // Grant reward
        if (goldXpRewardPerObjective > 0 && SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.AddXP(goldXpRewardPerObjective);
        }

        OnObjectiveCompleted?.Invoke(obj);

        StartCoroutine(IntermissionAndNextRoutine());
    }

    private void HandleObjectiveFailed(ISurvivorsObjective obj)
    {
        Debug.Log($"[SurvivorsObjectiveManager] Objective '{obj.Title}' Failed!");

        OnObjectiveFailed?.Invoke(obj);

        StartCoroutine(IntermissionAndNextRoutine());
    }

    /// <summary>Stops and cleans up all rotating sub-objectives (e.g. when Siegebreaker arrives).</summary>
    public void StopAllObjectives()
    {
        StopAllCoroutines();
        isRunning = false;
        if (currentObjective != null)
        {
            currentObjective.OnProgressChanged -= HandleObjectiveProgress;
            currentObjective.OnCompleted -= HandleObjectiveCompleted;
            currentObjective.OnFailed -= HandleObjectiveFailed;
            currentObjective.CleanupObjective();
            currentObjective = null;
        }
    }

    private IEnumerator IntermissionAndNextRoutine()
    {
        yield return new WaitForSeconds(intermissionDuration);

        if (!isRunning) yield break;

        if (SurvivorsGameManager.Instance != null && !SurvivorsGameManager.Instance.CanStartNewObjectives)
        {
            Debug.Log("[SurvivorsObjectiveManager] Objective cutoff reached (final countdown to Siegebreaker). No new sub-objectives will spawn.");
            SetActiveObjective(null);
            yield break;
        }

        ISurvivorsObjective next = PickNextRandomObjective();
        if (next != null)
        {
            SetActiveObjective(next);
        }
    }

    private ISurvivorsObjective PickNextRandomObjective()
    {
        if (objectivePool.Count == 0) return null;
        if (objectivePool.Count == 1) return objectivePool[0];

        // Random roll without immediate repeat
        int nextIndex;
        int attempts = 0;
        do
        {
            nextIndex = UnityEngine.Random.Range(0, objectivePool.Count);
            attempts++;
        }
        while (nextIndex == lastObjectiveIndex && attempts < 10);

        lastObjectiveIndex = nextIndex;
        return objectivePool[nextIndex];
    }
}
