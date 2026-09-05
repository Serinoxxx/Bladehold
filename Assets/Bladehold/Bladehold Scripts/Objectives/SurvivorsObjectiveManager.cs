using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Lifecycle phase for survival mode objectives.
/// </summary>
public enum SurvivorsObjectivePhase
{
    Active,
    Cleanup,
    Intermission
}

/// <summary>
///     Central manager for survival mode objectives. Sequences wave objectives (e.g. Hold the Gate,
///     Free Prisoners, Destroy Siege Engines), drives the 30-second post-objective cleanup window
///     (where enemies stop spawning and must be killed before disappearing), and manages the 30-second
///     pre-wave intermission countdown.
/// </summary>
public class SurvivorsObjectiveManager : MonoBehaviour
{
    public static SurvivorsObjectiveManager Instance { get; private set; }

    [Header("Objectives Configuration")]
    [Tooltip("Initial objective played at start (e.g. KillEnemiesObjective for Hold the Gate).")]
    [SerializeField] private KillEnemiesObjective introductoryObjective;

    [Tooltip("Pool of repeating objectives drawn randomly after the introductory wave.")]
    [SerializeField] private List<MonoBehaviour> repeatingObjectiveComponents = new List<MonoBehaviour>();

    [Header("Rewards & Pacing")]
    [Tooltip("Bonus gold XP granted to level progression when an objective is cleared.")]
    [SerializeField] private int goldXpRewardPerObjective = 100;

    [Tooltip("Grace duration in seconds to kill remaining enemies before they disappear.")]
    [SerializeField] private float cleanupDuration = 30.0f;

    [Tooltip("Intermission delay in seconds between completing cleanup and starting the next wave.")]
    [SerializeField] private float intermissionDuration = 30.0f;

    private readonly List<ISurvivorsObjective> objectivePool = new List<ISurvivorsObjective>();
    private ISurvivorsObjective currentObjective;
    private SurvivorsObjectivePhase currentPhase = SurvivorsObjectivePhase.Active;
    private float phaseTimer = 0f;
    private int currentWave = 0;
    private int lastObjectiveIndex = -1;
    private int completedObjectiveCount = 0;
    private bool isRunning;

    public SurvivorsObjectivePhase Phase => currentPhase;
    public float PhaseTimeRemaining => Mathf.Max(0f, phaseTimer);
    public float CleanupDuration => cleanupDuration;
    public float IntermissionDuration => intermissionDuration;
    public int CurrentWave => currentWave;
    public ISurvivorsObjective CurrentObjective => currentObjective;
    public int CompletedObjectiveCount => completedObjectiveCount;
    public int GoldXpRewardPerObjective => goldXpRewardPerObjective;
    public IReadOnlyList<ISurvivorsObjective> ObjectivePool => objectivePool;

    public event Action<ISurvivorsObjective> OnObjectiveStarted;
    public event Action<ISurvivorsObjective> OnObjectiveProgressChanged;
    public event Action<ISurvivorsObjective> OnObjectiveCompleted;
    public event Action<ISurvivorsObjective> OnObjectiveFailed;
    public event Action<SurvivorsObjectivePhase> OnPhaseChanged;
    public event Action<SurvivorsObjectivePhase, float> OnPhaseTimeTick;
    public event Action<int> OnWaveStarted;
    public event Action<int> OnWaveCleared;

    /// <summary>Debug method: Stops any active intermission/cleanup and immediately starts the next objective in rotation.</summary>
    public void DebugNextObjective()
    {
        StopAllCoroutines();
        currentWave++;
        currentPhase = SurvivorsObjectivePhase.Active;
        OnPhaseChanged?.Invoke(currentPhase);

        ISurvivorsObjective next = PickNextRandomObjective();
        if (next != null)
        {
            SetActiveObjective(next);
            OnWaveStarted?.Invoke(currentWave);
        }
    }

    /// <summary>Debug method: Forces starting a specific objective from the pool by index.</summary>
    public void DebugStartObjective(int index)
    {
        if (index >= 0 && index < objectivePool.Count)
        {
            StopAllCoroutines();
            currentPhase = SurvivorsObjectivePhase.Active;
            OnPhaseChanged?.Invoke(currentPhase);

            lastObjectiveIndex = index;
            SetActiveObjective(objectivePool[index]);
            OnWaveStarted?.Invoke(currentWave);
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
        else if (Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }

    private void Start()
    {
        InitializePool();
    }

    /// <summary>
    /// Populates the internal objective pool from configured components or auto-discovery.
    /// </summary>
    public void InitializePool()
    {
        if (objectivePool.Count > 0) return;

        // Auto-discover if repeatingObjectiveComponents is empty
        if (repeatingObjectiveComponents == null || repeatingObjectiveComponents.Count == 0)
        {
            MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (MonoBehaviour mb in allScripts)
            {
                if (mb is ISurvivorsObjective && mb != introductoryObjective && mb != (MonoBehaviour)(object)this)
                {
                    if (repeatingObjectiveComponents == null) repeatingObjectiveComponents = new List<MonoBehaviour>();
                    repeatingObjectiveComponents.Add(mb);
                }
            }
        }

        if (repeatingObjectiveComponents != null)
        {
            foreach (MonoBehaviour mb in repeatingObjectiveComponents)
            {
                if (mb is ISurvivorsObjective obj && !objectivePool.Contains(obj))
                {
                    objectivePool.Add(obj);
                }
            }
        }

        if (introductoryObjective != null && !objectivePool.Contains(introductoryObjective))
        {
            objectivePool.Insert(0, introductoryObjective);
        }
    }

    /// <summary>
    /// Starts the objective for the specified wave. Wave 1 uses introductoryObjective if present,
    /// otherwise an objective is picked randomly from the pool without consecutive repeats.
    /// </summary>
    public void StartWaveObjective(int waveNumber)
    {
        InitializePool();
        isRunning = true;
        currentWave = waveNumber;
        currentPhase = SurvivorsObjectivePhase.Active;
        OnPhaseChanged?.Invoke(currentPhase);

        ISurvivorsObjective next = null;
        if (waveNumber == 1 && introductoryObjective != null)
        {
            next = introductoryObjective;
            lastObjectiveIndex = objectivePool.IndexOf(introductoryObjective);
        }
        else
        {
            next = PickNextRandomObjective();
        }

        if (next != null)
        {
            SetActiveObjective(next);
            OnWaveStarted?.Invoke(currentWave);
        }
        else
        {
            Debug.LogWarning("[SurvivorsObjectiveManager] No objective available in pool to start!");
        }
    }

    /// <summary>
    /// Randomly starts an objective from the pool without consecutive repeats.
    /// </summary>
    public void StartRandomObjective()
    {
        InitializePool();
        isRunning = true;
        currentPhase = SurvivorsObjectivePhase.Active;
        OnPhaseChanged?.Invoke(currentPhase);

        ISurvivorsObjective next = PickNextRandomObjective();
        if (next != null)
        {
            SetActiveObjective(next);
        }
    }

    /// <summary>
    /// Updates the current objective phase and time remaining (e.g. for UI countdowns).
    /// </summary>
    public void SetPhase(SurvivorsObjectivePhase phase, float duration = 0f)
    {
        currentPhase = phase;
        phaseTimer = duration;
        OnPhaseChanged?.Invoke(currentPhase);
    }

    /// <summary>
    /// Stops and cleans up the active objective.
    /// </summary>
    public void StopActiveObjective()
    {
        if (currentObjective != null)
        {
            currentObjective.OnProgressChanged -= HandleObjectiveProgress;
            currentObjective.OnCompleted -= HandleObjectiveCompleted;
            currentObjective.OnFailed -= HandleObjectiveFailed;
            currentObjective.CleanupObjective();
            currentObjective = null;
        }
    }

    private void Update()
    {
        if (!isRunning) return;

        if (currentPhase == SurvivorsObjectivePhase.Active && currentObjective != null)
        {
            currentObjective.UpdateObjective(Time.deltaTime);
        }
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
        OnWaveCleared?.Invoke(currentWave);
    }

    private void HandleObjectiveFailed(ISurvivorsObjective obj)
    {
        Debug.Log($"[SurvivorsObjectiveManager] Objective '{obj.Title}' Failed!");
        OnObjectiveFailed?.Invoke(obj);
    }

    /// <summary>Stops and cleans up all rotating sub-objectives (e.g. when Siegebreaker arrives or game ends).</summary>
    public void StopAllObjectives()
    {
        StopAllCoroutines();
        isRunning = false;
        StopActiveObjective();
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
