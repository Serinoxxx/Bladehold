using System;
using UnityEngine;

/// <summary>
///     Survivors objective: Defeat the first wave / kill a specified count of enemies (e.g. 50 goblins).
/// </summary>
public class KillEnemiesObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "kill_enemies";
    [SerializeField] private string title = "Defeat the First Wave";
    [SerializeField] private string description = "Kill goblins";
    [SerializeField] private int requiredKills = 50;

    private int initialKills;
    private int currentKills;
    private bool isActive;
    private bool isComplete;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public string ProgressText => $"Slay enemies: {currentKills}/{requiredKills}";
    public float ProgressNormalized => requiredKills > 0 ? Mathf.Clamp01((float)currentKills / requiredKills) : 1f;
    public bool IsComplete => isComplete;
    public bool IsFailed => false;
    public bool IsActive => isActive;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    public void SetRequiredKills(int amount)
    {
        requiredKills = Mathf.Max(1, amount);
    }

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        currentKills = 0;
        initialKills = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        OnProgressChanged?.Invoke(this);
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete) return;

        int totalKilled = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        int delta = Mathf.Max(0, totalKilled - initialKills);

        if (delta != currentKills)
        {
            currentKills = delta;
            OnProgressChanged?.Invoke(this);

            if (currentKills >= requiredKills)
            {
                currentKills = requiredKills;
                isComplete = true;
                isActive = false;
                OnCompleted?.Invoke(this);
            }
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
        // Enemies are spread throughout the arena; no specific fixed target waypoints needed.
    }
}
