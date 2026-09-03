using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Survivors / Game Objective: Defeat the Slayer.
///     Spawns a powerful Slayer boss that ignores the player and charges the fortress gate.
///     Plays a cinematic introduction sequence, binds to the HUD boss health bar,
///     and completes when the Slayer is defeated.
/// </summary>
public class DefeatSlayerObjective : MonoBehaviour, ISurvivorsObjective
{
    [Header("Objective Configuration")]
    [SerializeField] private string objectiveId = "defeat_slayer";
    [SerializeField] private string title = "Defeat the Slayer";
    [SerializeField] private string description = "Stop the Slayer before he destroys the gate!";

    [Header("Spawning & Prefab")]
    [Tooltip("The Slayer enemy prefab to instantiate.")]
    [SerializeField] private GameObject slayerPrefab;

    [Tooltip("Optional spawn point transforms in the scene.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Fallback spawn offset if no spawn points are assigned.")]
    [SerializeField] private Vector3 fallbackSpawnOffset = new Vector3(0f, 0f, 35f);

    private GameObject spawnedSlayer;
    private Health slayerHealth;
    private bool isActive;
    private bool isComplete;

    public string ObjectiveId => objectiveId;
    public string Title => title;
    public string Description => description;

    public string ProgressText
    {
        get
        {
            if (slayerHealth == null) return "Defeat the Slayer";
            return $"Slayer Health: {Mathf.CeilToInt(slayerHealth.CurrentHealth)} / {Mathf.CeilToInt(slayerHealth.MaxHealth)}";
        }
    }

    public float ProgressNormalized
    {
        get
        {
            if (slayerHealth == null || slayerHealth.MaxHealth <= 0f) return 0f;
            return Mathf.Clamp01(1f - (slayerHealth.CurrentHealth / slayerHealth.MaxHealth));
        }
    }

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

        SpawnSlayer();
        OnProgressChanged?.Invoke(this);
    }

    private void SpawnSlayer()
    {
        if (slayerPrefab == null)
        {
            Debug.LogError("[DefeatSlayerObjective] slayerPrefab is not assigned!");
            return;
        }

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[0] != null)
        {
            int idx = UnityEngine.Random.Range(0, spawnPoints.Length);
            spawnPos = spawnPoints[idx].position;
            spawnRot = spawnPoints[idx].rotation;
        }
        else
        {
            Vector3 center = Player.Instance != null ? Player.Instance.transform.position : Vector3.zero;
            spawnPos = center + fallbackSpawnOffset;
        }

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        spawnedSlayer = Instantiate(slayerPrefab, spawnPos, spawnRot);
        slayerHealth = spawnedSlayer.GetComponent<Health>();

        if (slayerHealth != null)
        {
            slayerHealth.OnHealthChanged += HandleHealthChanged;
            slayerHealth.OnDied += HandleSlayerDied;
        }

        // Ensure 999 knockback resistance is applied
        KnockbackReceiver knockback = spawnedSlayer.GetComponent<KnockbackReceiver>();
        if (knockback != null)
        {
            knockback.SetResistance(999f);
        }

        // Configure AI to charge gate and ignore player by default
        AITargetSelector targetSelector = spawnedSlayer.GetComponent<AITargetSelector>();
        if (targetSelector == null)
        {
            targetSelector = spawnedSlayer.AddComponent<AITargetSelector>();
        }
        targetSelector.IgnorePlayer = true;

        // Ensure 25% HP damage retaliation phase component is present
        EnemyDamageRetaliation retaliation = spawnedSlayer.GetComponent<EnemyDamageRetaliation>();
        if (retaliation == null)
        {
            retaliation = spawnedSlayer.AddComponent<EnemyDamageRetaliation>();
        }

        // Trigger Intro Cinematic
        SpecialEnemyIntro intro = spawnedSlayer.GetComponent<SpecialEnemyIntro>();
        if (intro == null)
        {
            intro = spawnedSlayer.AddComponent<SpecialEnemyIntro>();
        }

        if (EnemyIntroController.Instance != null)
        {
            EnemyIntroController.Instance.PlayIntro(intro);
        }
    }

    private void HandleHealthChanged()
    {
        if (!isActive || isComplete) return;
        OnProgressChanged?.Invoke(this);
    }

    private void HandleSlayerDied()
    {
        if (!isActive || isComplete) return;

        isComplete = true;
        isActive = false;

        OnProgressChanged?.Invoke(this);
        OnCompleted?.Invoke(this);
    }

    public void UpdateObjective(float deltaTime)
    {
        // Event-driven via health events
    }

    public void CleanupObjective()
    {
        isActive = false;

        if (slayerHealth != null)
        {
            slayerHealth.OnHealthChanged -= HandleHealthChanged;
            slayerHealth.OnDied -= HandleSlayerDied;
        }

        if (spawnedSlayer != null && (slayerHealth == null || !slayerHealth.IsDead))
        {
            if (Application.isPlaying) Destroy(spawnedSlayer);
            else DestroyImmediate(spawnedSlayer);
        }

        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.Hide();
        }
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        return null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        return null;
    }

    [Header("Waypoint Icon Configuration")]
    [Tooltip("Optional custom waypoint icon for the Slayer boss.")]
    [SerializeField] private Sprite slayerWaypointIcon;

    public void GetActiveWaypointTargets(System.Collections.Generic.List<ObjectiveWaypointTarget> results)
    {
        if (!isActive || isComplete || results == null) return;

        if (spawnedSlayer != null && slayerHealth != null && !slayerHealth.IsDead)
        {
            results.Add(new ObjectiveWaypointTarget(
                spawnedSlayer.transform,
                worldOffset: new Vector3(0f, 2.5f, 0f),
                customIcon: slayerWaypointIcon,
                tintColor: new Color(1f, 0.25f, 0.25f, 1f),
                label: "Slayer"
            ));
        }
    }
}
