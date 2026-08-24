using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     HUD overlay for Survivors mode: displays the 30-minute countdown timer, player level badge,
///     and gold XP progress bar.
/// </summary>
public class SurvivorsHUDUI : MonoBehaviour
{
    [Header("UI Component References")]
    [Tooltip("Text component displaying the formatted run timer (MM:SS).")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Tooltip("Text component displaying current level (e.g. 'LVL 5').")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Progress bar slider / fill for current gold XP toward next level.")]
    [SerializeField] private Slider xpSlider;

    [Tooltip("Text component displaying XP numerical progress (e.g. '45 / 100 Gold').")]
    [SerializeField] private TextMeshProUGUI xpText;

    private bool anyError = false;

    private void Start()
    {
        if (timerText == null)
        {
            Debug.LogWarning("[SurvivorsHUDUI] Timer text is not assigned.");
        }

        if (SurvivorsGameManager.Instance != null)
        {
            SurvivorsGameManager.Instance.OnTimerUpdated += HandleTimerUpdated;
            HandleTimerUpdated(SurvivorsGameManager.Instance.RunTimer, SurvivorsGameManager.Instance.MaxRunDuration);
        }

        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.OnXPChanged += HandleXPChanged;
            HandleXPChanged(SurvivorsLevelSystem.Instance.CurrentLevelXP, SurvivorsLevelSystem.Instance.TargetXPForNextLevel, SurvivorsLevelSystem.Instance.CurrentLevel);
        }
    }

    private void OnDestroy()
    {
        if (SurvivorsGameManager.Instance != null)
        {
            SurvivorsGameManager.Instance.OnTimerUpdated -= HandleTimerUpdated;
        }

        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.OnXPChanged -= HandleXPChanged;
        }
    }

    private void HandleTimerUpdated(float currentSeconds, float maxSeconds)
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.FloorToInt(currentSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void HandleXPChanged(int currentXP, int targetXP, int level)
    {
        if (levelText != null)
        {
            levelText.text = $"LVL {level}";
        }

        if (xpSlider != null)
        {
            xpSlider.maxValue = targetXP > 0 ? targetXP : 1;
            xpSlider.value = currentXP;
        }

        if (xpText != null)
        {
            xpText.text = $"{currentXP} / {targetXP} XP";
        }
    }
}
