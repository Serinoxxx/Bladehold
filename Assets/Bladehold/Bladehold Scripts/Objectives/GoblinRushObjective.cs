using System;
using UnityEngine;

/// <summary>
///     Survivors objective: Goblin Rush. Only goblins spawn continuously for a set time limit, 
///     and the objective is to kill as many as possible within that time.
/// </summary>
public class GoblinRushObjective : MonoBehaviour, ISurvivorsObjective, IOverrideEnemySpawns
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "goblin_rush";
    [SerializeField] private string title = "Goblin Rush!";
    [SerializeField] private string description = "Survive the continuous goblin rush and kill as many as you can!";
    [SerializeField] private float durationSeconds = 120f;
    
    [Header("Overrides")]
    [SerializeField] private string[] allowedEnemyIds = new string[] { "goblin" };

    private int initialKills;
    private int currentKills;
    private float timeRemaining;
    private bool isActive;
    private bool isComplete;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    
    public string ProgressText 
    {
        get
        {
            int mins = Mathf.FloorToInt(Mathf.Max(0, timeRemaining) / 60f);
            int secs = Mathf.FloorToInt(Mathf.Max(0, timeRemaining) % 60f);
            return $"Time: {mins:00}:{secs:00} | Killed: {currentKills}";
        }
    }
    
    public float ProgressNormalized => durationSeconds > 0 ? Mathf.Clamp01(1f - (timeRemaining / durationSeconds)) : 1f;
    public bool IsComplete => isComplete;
    public bool IsFailed => false;
    public bool IsActive => isActive;

    public string[] AllowedEnemyIds => allowedEnemyIds;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        currentKills = 0;
        timeRemaining = durationSeconds;
        initialKills = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        OnProgressChanged?.Invoke(this);
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete) return;

        timeRemaining -= deltaTime;
        
        int totalKilled = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        int delta = Mathf.Max(0, totalKilled - initialKills);

        bool progressChanged = false;

        if (delta != currentKills)
        {
            currentKills = delta;
            progressChanged = true;
        }

        if (timeRemaining <= 0)
        {
            timeRemaining = 0;
            isComplete = true;
            isActive = false;
            OnProgressChanged?.Invoke(this);
            OnCompleted?.Invoke(this);
        }
        else if (progressChanged || Mathf.FloorToInt(timeRemaining) != Mathf.FloorToInt(timeRemaining + deltaTime))
        {
            // Update UI every second or when kill count changes
            OnProgressChanged?.Invoke(this);
        }
    }

    public void CleanupObjective()
    {
        isActive = false;
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        return null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        return null;
    }

    public void GetActiveWaypointTargets(System.Collections.Generic.List<ObjectiveWaypointTarget> results)
    {
        // Goblins are everywhere; no specific waypoint targets needed
    }
}
