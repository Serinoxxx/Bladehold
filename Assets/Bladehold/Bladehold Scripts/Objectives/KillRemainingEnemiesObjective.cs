using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Survivors objective: Eliminates all remaining enemies on the battlefield after a wave objective completes.
///     Renders red skull HUD waypoints on all remaining enemies and completes once every straggler is defeated.
/// </summary>
public class KillRemainingEnemiesObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "kill_remaining_enemies";
    [SerializeField] private string title = "Kill All Remaining Enemies";
    [SerializeField] private string description = "Eliminate all remaining enemies";

    [Header("Visuals & Waypoints")]
    [Tooltip("Skull sprite displayed on remaining enemy waypoints.")]
    [SerializeField] private Sprite skullIcon;
    [SerializeField] private Color waypointColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Vector3 waypointOffset = new Vector3(0f, 1.8f, 0f);

    private readonly List<Health> trackedEnemies = new List<Health>();
    private readonly HashSet<Health> registeredListeners = new HashSet<Health>();
    private int initialCount = 0;
    private int remainingCount = 0;
    private bool isActive = false;
    private bool isComplete = false;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public string ProgressText => $"Kill all remaining enemies: {remainingCount} left";
    public float ProgressNormalized => initialCount > 0 ? Mathf.Clamp01(1f - ((float)remainingCount / initialCount)) : 1f;
    public bool IsComplete => isComplete;
    public bool IsFailed => false;
    public bool IsActive => isActive && !isComplete;
    public int RemainingCount => remainingCount;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    private void Awake()
    {
        EnsureSkullIcon();
    }

    public void SetSkullIcon(Sprite sprite)
    {
        skullIcon = sprite;
    }

    private void EnsureSkullIcon()
    {
        if (skullIcon != null) return;

        if (ObjectiveWaypointTrackerUI.Instance != null && ObjectiveWaypointTrackerUI.Instance.CleanupEnemySkullIcon != null)
        {
            skullIcon = ObjectiveWaypointTrackerUI.Instance.CleanupEnemySkullIcon;
            return;
        }

        Debug.LogError("[KillRemainingEnemiesObjective] No skull icon: assign skullIcon or the tracker's cleanupEnemySkullIcon.", this);
    }

    public void StartObjective()
    {
        EnsureSkullIcon();
        isActive = true;
        isComplete = false;
        UnregisterAllListeners();
        trackedEnemies.Clear();

        // 1. Gather all currently living tracked enemies from SurvivorsSpawner
        if (SurvivorsSpawner.Instance != null)
        {
            SurvivorsSpawner.Instance.GetAliveEnemies(trackedEnemies);
        }

        // 2. Also query any remaining enemy Health in the scene not yet tracked (e.g. objective captains)
        Health[] allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
        foreach (Health h in allHealth)
        {
            if (h != null && !h.IsDead && !trackedEnemies.Contains(h))
            {
                if (Player.Instance != null && (h == Player.Instance.Health || h.gameObject == Player.Instance.gameObject || h.transform.root == Player.Instance.transform.root))
                    continue;
                if (h.GetComponent<Gate>() != null || h.GetComponentInParent<Gate>() != null)
                    continue;
                if (h.GetComponent<DestructibleSiegeEngine>() != null || h.GetComponentInParent<DestructibleSiegeEngine>() != null)
                    continue;
                if (h.GetComponent<PrisonerCage>() != null || h.GetComponentInParent<PrisonerCage>() != null)
                    continue;
                if (h.GetComponent<BatteringRam>() != null || h.GetComponentInParent<BatteringRam>() != null)
                    continue;

                // Must be an actual enemy character
                bool isEnemy = h.GetComponent<AIMovement>() != null ||
                               h.GetComponent<AITargetSelector>() != null ||
                               h.GetComponent<UnityEngine.AI.NavMeshAgent>() != null ||
                               h.CompareTag("Enemy") ||
                               h.gameObject.name.StartsWith("Test_RemainingEnemy");

                if (isEnemy)
                {
                    trackedEnemies.Add(h);
                }
            }
        }

        // Prune dead or null
        trackedEnemies.RemoveAll(h => h == null || h.IsDead);

        initialCount = trackedEnemies.Count;
        remainingCount = initialCount;

        // Register death callbacks
        foreach (Health health in trackedEnemies)
        {
            RegisterEnemy(health);
        }

        Debug.Log($"[KillRemainingEnemiesObjective] Started with {remainingCount} remaining enemies.");
        OnProgressChanged?.Invoke(this);

        // Immediate complete if no enemies were remaining
        if (remainingCount <= 0)
        {
            CompleteObjective();
        }
    }

    private void RegisterEnemy(Health health)
    {
        if (health == null || registeredListeners.Contains(health)) return;

        registeredListeners.Add(health);
        health.OnDied += HandleEnemyDied;
    }

    private void HandleEnemyDied()
    {
        if (!isActive || isComplete) return;

        RefreshRemainingCount();
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete) return;

        // Verify count periodically in case enemies despawned or were destroyed without triggering OnDied
        RefreshRemainingCount();
    }

    private void RefreshRemainingCount()
    {
        trackedEnemies.RemoveAll(h => h == null || h.IsDead);
        int currentCount = trackedEnemies.Count;

        if (currentCount != remainingCount)
        {
            remainingCount = currentCount;
            OnProgressChanged?.Invoke(this);
        }

        if (remainingCount <= 0 && !isComplete)
        {
            CompleteObjective();
        }
    }

    private void CompleteObjective()
    {
        isComplete = true;
        isActive = false;
        remainingCount = 0;
        UnregisterAllListeners();
        trackedEnemies.Clear();

        Debug.Log("[KillRemainingEnemiesObjective] All remaining enemies eliminated!");
        OnProgressChanged?.Invoke(this);
        OnCompleted?.Invoke(this);
    }

    public void CleanupObjective()
    {
        isActive = false;
        UnregisterAllListeners();
        trackedEnemies.Clear();
    }

    private void UnregisterAllListeners()
    {
        foreach (Health h in registeredListeners)
        {
            if (h != null)
            {
                h.OnDied -= HandleEnemyDied;
            }
        }
        registeredListeners.Clear();
    }

    private void OnDestroy()
    {
        CleanupObjective();
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        return null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        return null;
    }

    public void GetActiveWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (!isActive || isComplete || results == null) return;

        EnsureSkullIcon();

        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            Health enemy = trackedEnemies[i];
            if (enemy != null && !enemy.IsDead)
            {
                results.Add(new ObjectiveWaypointTarget(
                    enemy.transform,
                    worldOffset: waypointOffset,
                    customIcon: skullIcon,
                    tintColor: waypointColor,
                    label: null
                ));
            }
        }
    }
}
