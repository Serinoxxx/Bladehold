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

    [Header("Run Config")]
    [Tooltip("Maximum duration of a run in seconds (default: 1800s = 30 minutes).")]
    [SerializeField] private float maxRunDuration = 1800f;

    [Header("UI Canvas / Screen References")]
    [Tooltip("Optional reference to the victory UI / clearing banner. Can be wired in Inspector.")]
    [SerializeField] private GameObject victoryBanner;
    [Tooltip("Optional reference to death screen controller.")]
    [SerializeField] private DeathScreen deathScreen;

    private float runTimer;
    private bool isGameActive = true;
    private bool isPausedForLevelUp = false;
    private bool isVictory = false;
    private bool isGameOver = false;

    public event Action<float, float> OnTimerUpdated; // (currentTimer, maxTimer)
    public event Action OnVictory;
    public event Action OnGameOver;
    public event Action<bool> OnPauseStateChanged; // (isPaused)

    public float RunTimer => runTimer;
    public float MaxRunDuration => maxRunDuration;
    public bool IsGameActive => isGameActive && !isPausedForLevelUp;
    public bool IsPausedForLevelUp => isPausedForLevelUp;
    public bool IsVictory => isVictory;
    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Shader.WarmupAllShaders();
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

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied += HandlePlayerDeath;
        }
        else
        {
            Debug.LogWarning("[SurvivorsGameManager] Player.Instance or Player.Health not found at Start. Will attempt listener hookup when available.");
        }
    }

    private void Update()
    {
        if (!isGameActive || isPausedForLevelUp || isVictory || isGameOver)
        {
            return;
        }

        runTimer += Time.deltaTime;
        OnTimerUpdated?.Invoke(runTimer, maxRunDuration);

        if (runTimer >= maxRunDuration)
        {
            TriggerVictory();
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
        Time.timeScale = 1f;
        CursorLockManager.SetUnlock("SurvivorsLevelUp", false);
        OnPauseStateChanged?.Invoke(false);
    }

    private void TriggerVictory()
    {
        if (isVictory || isGameOver) return;

        isVictory = true;
        isGameActive = false;
        Debug.Log("[SurvivorsGameManager] 30 Minute Timer Reached! VICTORY!");

        if (victoryBanner != null)
        {
            victoryBanner.SetActive(true);
        }

        OnVictory?.Invoke();
    }

    private void HandlePlayerDeath()
    {
        if (isVictory || isGameOver) return;

        isGameOver = true;
        isGameActive = false;
        Debug.Log("[SurvivorsGameManager] Player died! GAME OVER!");

        OnGameOver?.Invoke();
    }
}
