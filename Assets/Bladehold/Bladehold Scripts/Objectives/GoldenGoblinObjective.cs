using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Survivors objective: A Golden Goblin spawns and runs around a set of waypoints.
///     No other enemies spawn during this objective. It drops gold periodically on damage.
/// </summary>
public class GoldenGoblinObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "golden_goblin_event";
    [SerializeField] private string title = "Golden Goblin";
    [SerializeField] private string description = "Catch the Golden Goblin before he escapes!";
    [SerializeField] private float duration = 30f;
    
    [Header("Golden Goblin Settings")]
    [SerializeField] private GameObject goldenGoblinPrefab;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private Coin coinPrefab;
    [SerializeField] private int goldPerDrop = 5;
    [SerializeField] private int killBonusGold = 100;
    [SerializeField] private float maxHealthOverride = 999f;
    [SerializeField] private float knockbackResistOverride = 999f;

    private float timer;
    private bool isActive;
    private bool isComplete;
    private Health targetHealth;
    private NavMeshAgent targetAgent;
    private int currentWaypointIndex;
    private float initialHealth;
    private float lastDropHealth;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;
    public string ProgressText => "Escapes in: {Mathf.CeilToInt(duration - timer)}s";
    public float ProgressNormalized => Mathf.Clamp01(timer / duration);
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
        timer = 0f;

        if (SurvivorsSpawner.Instance != null)
        {
            SurvivorsSpawner.Instance.StopSpawning();
        }

        SpawnGoblin();
        OnProgressChanged?.Invoke(this);
    }

    private void SpawnGoblin()
    {
        if (goldenGoblinPrefab == null || waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("[GoldenGoblinObjective] Missing prefab or waypoints, completing objective instantly.");
            CompleteObjective();
            return;
        }

        Vector3 spawnPos = waypoints[0].position;
        GameObject goblinGo = Instantiate(goldenGoblinPrefab, spawnPos, Quaternion.identity);

        GoldenGoblinFlee flee = goblinGo.GetComponent<GoldenGoblinFlee>();
        if (flee != null) flee.enabled = false;
        AIMovement aiMove = goblinGo.GetComponent<AIMovement>();
        if (aiMove != null) aiMove.enabled = false;

        targetHealth = goblinGo.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.SetMaxHealth(maxHealthOverride);
            targetHealth.Heal(maxHealthOverride);
            
            KnockbackReceiver kr = goblinGo.GetComponent<KnockbackReceiver>();
            if (kr != null) kr.SetResistance(knockbackResistOverride);
            
            ImpulseGoblin impulse = goblinGo.GetComponent<ImpulseGoblin>();
            if (impulse != null) impulse.enabled = false;

            targetHealth.OnDamaged += HandleGoblinDamaged;
            targetHealth.OnDied += HandleGoblinDied;
            
            initialHealth = targetHealth.MaxHealth;
            lastDropHealth = initialHealth;
        }

        targetAgent = goblinGo.GetComponent<NavMeshAgent>();
        if (targetAgent != null)
        {
            targetAgent.speed = 8f;
            currentWaypointIndex = 1 % waypoints.Length;
            targetAgent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    private void HandleGoblinDamaged(Damage damage)
    {
        if (targetHealth == null || isComplete || !isActive) return;

        float dropThreshold = initialHealth * 0.1f;
        
        while (lastDropHealth - targetHealth.CurrentHealth >= dropThreshold)
        {
            lastDropHealth -= dropThreshold;
            DropGold(goldPerDrop);
        }
    }

    private void HandleGoblinDied()
    {
        if (isComplete || !isActive) return;

        DropGold(killBonusGold);
        
        if (GameLoopManager.Instance != null && SurvivorsSpawner.Instance != null)
        {
            GameLoopManager.Instance.DebugCompleteObjective();
        }
        
        CompleteObjective();
    }

    private void DropGold(int amount)
    {
        if (coinPrefab == null || targetHealth == null) return;
        
        Coin coin = Instantiate(coinPrefab, targetHealth.transform.position + Vector3.up, Quaternion.identity);
        coin.SetAmount(amount);
    }

    public void UpdateObjective(float deltaTime)
    {
        if (!isActive || isComplete) return;

        timer += deltaTime;
        OnProgressChanged?.Invoke(this);

        if (timer >= duration)
        {
            if (targetHealth != null && targetHealth.gameObject != null)
            {
                Destroy(targetHealth.gameObject);
            }
            
            if (GameLoopManager.Instance != null)
            {
                GameLoopManager.Instance.DebugCompleteObjective();
            }
            CompleteObjective();
            return;
        }

        if (targetAgent != null && targetAgent.isOnNavMesh && waypoints != null && waypoints.Length > 0)
        {
            if (!targetAgent.pathPending && targetAgent.remainingDistance < 1f)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                targetAgent.SetDestination(waypoints[currentWaypointIndex].position);
            }
        }
    }

    private void CompleteObjective()
    {
        isActive = false;
        isComplete = true;
        OnCompleted?.Invoke(this);
    }

    public void CleanupObjective()
    {
        isActive = false;
        if (targetHealth != null && targetHealth.gameObject != null)
        {
            Destroy(targetHealth.gameObject);
        }
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        return targetHealth != null ? targetHealth.transform.position : null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        return targetHealth;
    }

    public void GetActiveWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (targetHealth != null)
        {
            results.Add(new ObjectiveWaypointTarget { Position = targetHealth.transform.position, Label = "Golden Goblin" });
        }
    }
}
