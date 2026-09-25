using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
///     Sector-level run state for a battle scene: the sector timer (shown as "time survived" on the
///     death screen), game over on player death, and pausing while a draft card pick is open. Waves,
///     kill quotas and victory belong to <see cref="GameLoopManager" />.
/// </summary>
public class SurvivorsGameManager : MonoBehaviour
{
    public static SurvivorsGameManager Instance { get; private set; }

    private float runTimer;
    private bool isGameActive = true;
    private bool isPausedForLevelUp = false;
    private bool isGameOver = false;

    /// <summary>Seconds of unpaused play in this sector.</summary>
    public float RunTimer => runTimer;
    public bool IsGameActive => isGameActive && !isPausedForLevelUp;

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

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied += HandlePlayerDeath;
        }
        else
        {
            Debug.LogError("[SurvivorsGameManager] Player.Instance or Player.Health not found at Start; game over won't be detected.");
        }
    }

    private void Update()
    {
        if (!isGameActive || isPausedForLevelUp || isGameOver)
        {
            return;
        }

        runTimer += Time.deltaTime;
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
    }

    public void ResumeFromCardSelection()
    {
        isPausedForLevelUp = false;
        MMTimeScaleEvent.Reset();
        Time.timeScale = GameSettingsService.TargetTimeScale;
        CursorLockManager.SetUnlock("SurvivorsLevelUp", false);
    }

    private void HandlePlayerDeath()
    {
        if (isGameOver) return;

        isGameOver = true;
        isGameActive = false;
    }
}
