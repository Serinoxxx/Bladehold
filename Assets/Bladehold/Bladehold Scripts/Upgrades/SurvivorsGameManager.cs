using System;
using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
///     Manages the lifecycle and state of a Survivors-mode run: tracks the run timer (up to 30 mins),
///     handles victory when time is reached, handles game over on player death, and controls pausing
///     during level-up card selection.
/// </summary>
public class SurvivorsGameManager : MonoBehaviour
{
    public static SurvivorsGameManager Instance { get; private set; }

    [Header("Siege Pacing & Boss Config")]
    [Tooltip("Duration of the siege in seconds before the boss event triggers (default: 1200s = 20 minutes).")]
    [SerializeField] private float siegeDuration = 1200f;

    [Tooltip("Lead time before the boss arrives where no new rotating objectives will spawn (default: 120s = 2 minutes).")]
    [SerializeField] private float objectiveStopLeadTime = 120f;

    [Tooltip("Prefab for the almost-unbeatable endgame boss ('The Siegebreaker').")]
    [SerializeField] private GameObject siegebreakerPrefab;

    [Tooltip("Transform spawn point for the Siegebreaker. If null, uses fallback offset.")]
    [SerializeField] private Transform bossSpawnPoint;

    [Tooltip("Fallback spawn position offset if bossSpawnPoint is null.")]
    [SerializeField] private Vector3 fallbackBossSpawnOffset = new Vector3(0f, 0f, 40f);

    [Header("UI Canvas / Screen References")]
    [Tooltip("Optional reference to the victory UI / clearing banner. Can be wired in Inspector.")]
    [SerializeField] private GameObject victoryBanner;
    [Tooltip("Optional reference to death screen controller.")]
    [SerializeField] private DeathScreen deathScreen;

    private float runTimer;
    private bool isGameActive = true;
    private bool isPausedForLevelUp = false;
    private bool hasSurvivedSiege = false;
    private bool bossSpawned = false;
    private bool isVictory = false;
    private bool isGameOver = false;
    private GameObject spawnedSiegebreaker;

    public event Action<float, float> OnTimerUpdated; // (currentTimer, maxTimer)
    public event Action OnSiegeCompleted;
    public event Action OnSiegebreakerSpawned;
    public event Action OnVictory;
    public event Action OnGameOver;
    public event Action<bool> OnPauseStateChanged; // (isPaused)

    public float RunTimer => runTimer;
    public float SiegeDuration => siegeDuration;
    public float MaxRunDuration => siegeDuration;
    public float SiegeTimeRemaining => Mathf.Max(0f, siegeDuration - runTimer);
    public bool HasSurvivedSiege => hasSurvivedSiege;
    public bool CanStartNewObjectives => (siegeDuration - runTimer) > objectiveStopLeadTime && !hasSurvivedSiege;
    public bool IsInFinalCountdown => (siegeDuration - runTimer) <= objectiveStopLeadTime && !hasSurvivedSiege;
    public bool BossSpawned => bossSpawned;
    public bool IsGameActive => isGameActive && !isPausedForLevelUp;
    public bool IsPausedForLevelUp => isPausedForLevelUp;
    public bool IsVictory => isVictory;
    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        runTimer = 0f;
        isGameActive = true;
        hasSurvivedSiege = false;
        bossSpawned = false;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied += HandlePlayerDeath;
        }
        else
        {
            Debug.LogWarning("[SurvivorsGameManager] Player.Instance or Player.Health not found at Start. Will attempt listener hookup when available.");
        }

        Gate.OnAnyGateDestroyed += HandleGateDestroyed;
    }

    private void Update()
    {
        if (!isGameActive || isPausedForLevelUp || isGameOver)
        {
            return;
        }

        runTimer += Time.deltaTime;
        OnTimerUpdated?.Invoke(runTimer, siegeDuration);

        if (!hasSurvivedSiege && runTimer >= siegeDuration)
        {
            TriggerSiegeEndgameBoss();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied -= HandlePlayerDeath;
        }

        Gate.OnAnyGateDestroyed -= HandleGateDestroyed;
    }

    public void PauseForCardSelection()
    {
        isPausedForLevelUp = true;
        Time.timeScale = 0f;
        MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0f, 0f, false, 0f, true);
        CursorLockManager.SetUnlock("SurvivorsLevelUp", true);
        OnPauseStateChanged?.Invoke(true);
    }

    public void ResumeFromCardSelection()
    {
        isPausedForLevelUp = false;
        MMTimeScaleEvent.Reset();
        Time.timeScale = GameSettingsService.TargetTimeScale;
        CursorLockManager.SetUnlock("SurvivorsLevelUp", false);
        OnPauseStateChanged?.Invoke(false);
    }

    private void TriggerSiegeEndgameBoss()
    {
        if (hasSurvivedSiege) return;
        hasSurvivedSiege = true;
        Debug.Log("[SurvivorsGameManager] 20 Minute Siege Survived! Spawning THE SIEGEBREAKER!");

        // Stop all active and scheduled sub-objectives
        if (SurvivorsObjectiveManager.Instance != null)
        {
            SurvivorsObjectiveManager.Instance.StopAllObjectives();
        }

        SpawnSiegebreaker();
        OnSiegeCompleted?.Invoke();
    }

    private void SpawnSiegebreaker()
    {
        if (bossSpawned) return;
        bossSpawned = true;

        if (siegebreakerPrefab == null)
        {
            #if UNITY_EDITOR
            siegebreakerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Siegebreaker Enemy Variant.prefab");
            #endif
        }

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (bossSpawnPoint != null)
        {
            spawnPos = bossSpawnPoint.position;
            spawnRot = bossSpawnPoint.rotation;
        }
        else
        {
            Vector3 center = Player.Instance != null ? Player.Instance.transform.position : Vector3.zero;
            spawnPos = center + fallbackBossSpawnOffset;
        }

        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        if (siegebreakerPrefab != null)
        {
            spawnedSiegebreaker = Instantiate(siegebreakerPrefab, spawnPos, spawnRot);

            SpecialEnemyIntro intro = spawnedSiegebreaker.GetComponent<SpecialEnemyIntro>();
            if (intro != null && EnemyIntroController.Instance != null)
            {
                EnemyIntroController.Instance.PlayIntro(intro);
            }
        }
        else
        {
            Debug.LogWarning("[SurvivorsGameManager] Siegebreaker prefab not assigned or found!");
        }

        OnSiegebreakerSpawned?.Invoke();
    }

    private void HandleGateDestroyed(Gate gate)
    {
        CheckAndUnlockNextStage();
    }

    private void HandlePlayerDeath()
    {
        if (isGameOver) return;

        isGameOver = true;
        isGameActive = false;
        Debug.Log("[SurvivorsGameManager] Player died! GAME OVER!");

        CheckAndUnlockNextStage();
        OnGameOver?.Invoke();
    }

    private void CheckAndUnlockNextStage()
    {
        if (hasSurvivedSiege)
        {
            SaveData data = SaveSystem.Load();
            int currentStage = data != null ? data.selectedStage : 1;
            int nextStage = currentStage + 1;
            if (data != null && data.highestUnlockedStage < nextStage)
            {
                data.highestUnlockedStage = nextStage;
                SaveSystem.Save(data);
                Debug.Log($"[SurvivorsGameManager] Stage {currentStage} cleared! Unlocked Stage {nextStage}!");
            }
        }
    }
}
