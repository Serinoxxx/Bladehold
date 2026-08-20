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
    private bool isActive;
    private bool isComplete;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public string ProgressText => $"Prisoners freed: {freedCount}/{requiredCount}";
    public float ProgressNormalized => requiredCount > 0 ? Mathf.Clamp01((float)freedCount / requiredCount) : 1f;
    public bool IsComplete => isComplete;
    public bool IsActive => isActive;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        freedCount = 0;
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
        if (!isActive || isComplete) return;

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
        // Event-driven progress via HandleCageBroken
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
}
