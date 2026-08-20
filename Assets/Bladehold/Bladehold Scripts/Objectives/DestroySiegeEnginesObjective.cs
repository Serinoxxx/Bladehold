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
    private bool isActive;
    private bool isComplete;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public string ProgressText => $"Catapults destroyed: {destroyedCount}/{requiredCount}";
    public float ProgressNormalized => requiredCount > 0 ? Mathf.Clamp01((float)destroyedCount / requiredCount) : 1f;
    public bool IsComplete => isComplete;
    public bool IsActive => isActive;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        destroyedCount = 0;
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
        if (!isActive || isComplete) return;

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
        // State updates are event-driven via HandleEngineDestroyed
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
