using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Survivors objective: Stop the Battering Ram.
///     Enemies push a heavy 1000 HP battering ram towards the fortress gate.
///     Once it arrives at the gate, it pounds the doors every 5 seconds dealing 50 damage.
///     The player must destroy the battering ram before it breaches the gate.
/// </summary>
public class StopBatteringRamObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "stop_battering_ram";
    [SerializeField] private string title = "Stop the Battering Ram";
    [SerializeField] private string description = "Destroy the battering ram before it breaches the gate!";

    [Header("Prefabs & Route")]
    [Tooltip("Prefab instantiated for the battering ram. Must contain BatteringRam component.")]
    [SerializeField] private GameObject batteringRamPrefab;

    [Tooltip("Transform marking the starting spawn position for the ram.")]
    [SerializeField] private Transform ramSpawnPoint;

    [Tooltip("Transform marking the destination gate.")]
    [SerializeField] private Transform gateDestinationPoint;

    [Tooltip("Fallback spawn point if ramSpawnPoint is unassigned.")]
    [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(30f, 4.1f, -25f);

    [Tooltip("Fallback destination point if gateDestinationPoint is unassigned.")]
    [SerializeField] private Vector3 fallbackDestinationPosition = new Vector3(0f, 0f, 0f);

    [Header("Waypoint Icons")]
    [Tooltip("Optional custom waypoint icon for the battering ram.")]
    [SerializeField] private Sprite ramWaypointIcon;

    [Tooltip("Optional custom waypoint icon for the destination gate.")]
    [SerializeField] private Sprite destinationWaypointIcon;

    private BatteringRam currentRam;
    private bool isActive;
    private bool isComplete;
    private bool isFailed;
    private float lastReportedHealth = -1f;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public BatteringRam CurrentRam => currentRam;

    public string ProgressText
    {
        get
        {
            if (isComplete) return "Battering Ram Destroyed!";
            if (currentRam == null || currentRam.Health == null) return "Destroy the Battering Ram";

            float currentHp = Mathf.Max(0f, currentRam.Health.CurrentHealth);
            float maxHp = currentRam.Health.MaxHealth;

            if (currentRam.HasReachedGate)
            {
                return $"Ram HP: {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)} - BREACHING GATE!";
            }

            if (currentRam.IsPushed)
            {
                string enemiesStr = currentRam.PusherCount == 1 ? "enemy" : "enemies";
                return $"Ram HP: {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)} [Pushed by {currentRam.PusherCount} {enemiesStr}]";
            }

            return $"Ram HP: {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)} [Stalled (Kill nearby enemies!)]";
        }
    }

    public float ProgressNormalized
    {
        get
        {
            if (isComplete) return 1f;
            if (currentRam == null || currentRam.Health == null) return 0f;
            return Mathf.Clamp01(1f - (currentRam.Health.CurrentHealth / currentRam.Health.MaxHealth));
        }
    }

    public bool IsComplete => isComplete;
    public bool IsFailed => isFailed;
    public bool IsActive => isActive;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    public void StartObjective()
    {
        isActive = true;
        isComplete = false;
        isFailed = false;
        lastReportedHealth = -1f;

        SpawnBatteringRam();
        OnProgressChanged?.Invoke(this);
    }

    private void SpawnBatteringRam()
    {
        if (batteringRamPrefab == null)
        {
            Debug.LogError("[StopBatteringRamObjective] batteringRamPrefab is not assigned!");
            return;
        }

        Vector3 spawnPos = ramSpawnPoint != null ? ramSpawnPoint.position : fallbackSpawnPosition;
        Quaternion spawnRot = ramSpawnPoint != null ? ramSpawnPoint.rotation : Quaternion.identity;

        GameObject instance = Instantiate(batteringRamPrefab, spawnPos, spawnRot);
        currentRam = instance.GetComponent<BatteringRam>();
        if (currentRam == null)
        {
            currentRam = instance.AddComponent<BatteringRam>();
        }

        Vector3 destPos = fallbackDestinationPosition;
        Gate destGate = null;

        if (gateDestinationPoint != null)
        {
            destPos = gateDestinationPoint.position;
            destGate = gateDestinationPoint.GetComponentInParent<Gate>();
        }
        else
        {
            destGate = Gate.NearestAlive(spawnPos);
            if (destGate != null)
            {
                destPos = destGate.TargetPosition;
            }
        }

        currentRam.InitializeDestination(destPos, destGate);
        currentRam.OnDestroyed += HandleRamDestroyed;
    }

    private void HandleRamDestroyed(BatteringRam ram)
    {
        if (!isActive || isComplete) return;

        isComplete = true;
        isActive = false;

        OnProgressChanged?.Invoke(this);
        OnCompleted?.Invoke(this);
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete || currentRam == null) return;

        // Check if ram was destroyed externally
        if (currentRam.IsDestroyed || (currentRam.Health != null && currentRam.Health.IsDead))
        {
            HandleRamDestroyed(currentRam);
            return;
        }

        // Notify progress changes on health updates
        if (currentRam.Health != null)
        {
            float curHp = currentRam.Health.CurrentHealth;
            if (Mathf.Abs(curHp - lastReportedHealth) > 5f)
            {
                lastReportedHealth = curHp;
                OnProgressChanged?.Invoke(this);
            }
        }
    }

    public void CleanupObjective()
    {
        isActive = false;
        if (currentRam != null)
        {
            currentRam.OnDestroyed -= HandleRamDestroyed;
            if (!currentRam.IsDestroyed)
            {
                Destroy(currentRam.gameObject);
            }
        }
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        if (isActive && !isComplete && currentRam != null && !currentRam.IsDestroyed)
        {
            return currentRam.GetEscortTargetPosition(searchFromPosition);
        }
        return null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        if (isActive && !isComplete && currentRam != null && !currentRam.IsDestroyed)
        {
            return currentRam.Health;
        }
        return null;
    }

    public void GetActiveWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (!isActive || isComplete || results == null) return;

        if (currentRam != null && !currentRam.IsDestroyed)
        {
            results.Add(new ObjectiveWaypointTarget(
                currentRam.transform,
                worldOffset: new Vector3(0f, 2.5f, 0f),
                customIcon: ramWaypointIcon,
                tintColor: new Color(0.95f, 0.3f, 0.2f, 1f),
                label: "Battering Ram"
            ));
        }

        Transform dest = gateDestinationPoint;
        if (dest != null)
        {
            results.Add(new ObjectiveWaypointTarget(
                dest,
                worldOffset: new Vector3(0f, 2.5f, 0f),
                customIcon: destinationWaypointIcon,
                tintColor: new Color(0.4f, 0.8f, 1f, 1f),
                label: "Gate"
            ));
        }
    }
}
