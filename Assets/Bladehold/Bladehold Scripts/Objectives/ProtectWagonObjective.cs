using System;
using UnityEngine;

/// <summary>
///     Survivors objective: Protect the supply wagon.
///     A slow moving cart rolls from a spawn point toward the gate along NavMesh only when the player
///     is inside its proximity circle.
/// </summary>
public class ProtectWagonObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "protect_supply_wagon";
    [SerializeField] private string title = "Protect the Supply Wagon";
    [SerializeField] private string description = "Escort the supply cart to the fortress gate";

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
    private float lastReportedProgress;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;

    public string ProgressText
    {
        get
        {
            if (currentWagon == null) return "Escort wagon to the gate";
            int pct = Mathf.RoundToInt(currentWagon.ProgressNormalized * 100f);
            string proximityState = currentWagon.IsPlayerInRadius ? "Moving" : "Paused (Get closer!)";
            return $"Escort progress: {pct}% [{proximityState}]";
        }
    }

    public float ProgressNormalized => currentWagon != null ? currentWagon.ProgressNormalized : 0f;
    public bool IsComplete => isComplete;
    public bool IsFailed => false;
    public bool IsActive => isActive;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        lastReportedProgress = 0f;

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
        if (!isActive || isComplete) return;

        isComplete = true;
        isActive = false;
        OnProgressChanged?.Invoke(this);
        OnCompleted?.Invoke(this);
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete || currentWagon == null) return;

        // Fire progress updates periodically or on significant change
        float currentProg = currentWagon.ProgressNormalized;
        if (Mathf.Abs(currentProg - lastReportedProgress) > 0.02f)
        {
            lastReportedProgress = currentProg;
            OnProgressChanged?.Invoke(this);
        }
    }

    public void CleanupObjective()
    {
        isActive = false;
        if (currentWagon != null)
        {
            currentWagon.OnArrived -= HandleWagonArrived;
            if (!currentWagon.HasArrived)
            {
                Destroy(currentWagon.gameObject);
            }
        }
    }
}
