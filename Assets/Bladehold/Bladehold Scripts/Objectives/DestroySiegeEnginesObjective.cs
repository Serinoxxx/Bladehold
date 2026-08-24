using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Survivors objective: Spawns 3 catapults / siege engines at designated positions.
///     Player must seek out and destroy all 3.
/// </summary>
public class DestroySiegeEnginesObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "destroy_siege_engines";
    [SerializeField] private string title = "Destroy the Siege Engines";
    [SerializeField] private string description = "Destroy the enemy catapults";
    [SerializeField] private int requiredCount = 3;

    [Header("Timer & Failure Configuration")]
    [Tooltip("Time limit in seconds to destroy all catapults before the objective fails and damages the gate (e.g. 120s = 2 minutes). <= 0 means no time limit.")]
    [SerializeField] private float timeLimit = 120f;

    [Tooltip("Damage dealt to the gate if the objective times out.")]
    [SerializeField] private float gateDamageOnTimeout = 100f;

    [Header("Prefab & Spawn Points")]
    [Tooltip("Prefab instantiated for each siege engine. Must have DestructibleSiegeEngine component.")]
    [SerializeField] private GameObject siegeEnginePrefab;

    [Tooltip("Pre-placed spawn point transforms in the scene.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Fallback spawn offsets from scene center if spawn points array is empty.")]
    [SerializeField] private Vector3[] fallbackSpawnPositions = new Vector3[]
    {
        new Vector3(-20f, 0f, 25f),
        new Vector3(20f, 0f, 30f),
        new Vector3(0f, 0f, 40f)
    };

    private readonly List<DestructibleSiegeEngine> spawnedEngines = new List<DestructibleSiegeEngine>();
    private int destroyedCount;
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
                return "Failed! Fortress gate took 100 damage!";
            }
            if (isComplete)
            {
                return $"Catapults destroyed: {destroyedCount}/{requiredCount}";
            }
            if (timeLimit > 0f)
            {
                int totalSec = Mathf.Max(0, Mathf.CeilToInt(timeRemaining));
                int mins = totalSec / 60;
                int secs = totalSec % 60;
                return $"Catapults destroyed: {destroyedCount}/{requiredCount} ({mins}:{secs:D2})";
            }
            return $"Catapults destroyed: {destroyedCount}/{requiredCount}";
        }
    }

    public float ProgressNormalized => requiredCount > 0 ? Mathf.Clamp01((float)destroyedCount / requiredCount) : 1f;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        isFailed = false;
        destroyedCount = 0;
        timeRemaining = timeLimit;
        lastReportedSeconds = Mathf.CeilToInt(timeRemaining);
        spawnedEngines.Clear();

        SpawnSiegeEngines();
        OnProgressChanged?.Invoke(this);
    }

    private void SpawnSiegeEngines()
    {
        if (siegeEnginePrefab == null)
        {
            Debug.LogError("[DestroySiegeEnginesObjective] siegeEnginePrefab is not assigned!");
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

            GameObject instance = Instantiate(siegeEnginePrefab, pos, rot);
            DestructibleSiegeEngine engine = instance.GetComponent<DestructibleSiegeEngine>();
            if (engine == null)
            {
                engine = instance.AddComponent<DestructibleSiegeEngine>();
            }

            spawnedEngines.Add(engine);
            engine.OnDestroyed += HandleEngineDestroyed;
        }
    }

    private void HandleEngineDestroyed(DestructibleSiegeEngine engine)
    {
        if (!isActive || isComplete || isFailed) return;

        destroyedCount++;
        OnProgressChanged?.Invoke(this);

        if (destroyedCount >= requiredCount)
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

        DamageGateOnTimeout();

        OnProgressChanged?.Invoke(this);
        OnFailed?.Invoke(this);
    }

    private void DamageGateOnTimeout()
    {
        if (gateDamageOnTimeout <= 0f) return;

        Vector3 searchPos = Player.Instance != null ? Player.Instance.transform.position : transform.position;
        Gate targetGate = Gate.NearestAlive(searchPos);

        if (targetGate == null && Gate.All != null && Gate.All.Count > 0)
        {
            foreach (Gate g in Gate.All)
            {
                if (g != null && !g.IsDestroyed)
                {
                    targetGate = g;
                    break;
                }
            }
        }

        if (targetGate == null)
        {
            targetGate = FindFirstObjectByType<Gate>() ?? FindObjectOfType<Gate>();
        }

        if (targetGate != null && targetGate.Damageable != null)
        {
            Damage dmg = new Damage
            {
                value = gateDamageOnTimeout,
                type = DamageType.blunt,
                unparryable = true,
                isPlayerDamage = false,
                source = null,
                sourcePosition = transform.position
            };
            targetGate.Damageable.ReceiveDamage(dmg);
            Debug.Log($"[DestroySiegeEnginesObjective] Timed out! Gate '{targetGate.gameObject.name}' took {gateDamageOnTimeout} damage.");
        }
        else
        {
            Debug.LogWarning("[DestroySiegeEnginesObjective] Timed out, but no alive gate found in scene to damage!");
        }
    }

    public void CleanupObjective()
    {
        isActive = false;
        foreach (DestructibleSiegeEngine engine in spawnedEngines)
        {
            if (engine != null && !engine.IsDestroyed)
            {
                engine.OnDestroyed -= HandleEngineDestroyed;
                Destroy(engine.gameObject);
            }
        }
        spawnedEngines.Clear();
    }
}
