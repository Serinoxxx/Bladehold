using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Fades in a death screen when the player dies, showing goblins killed and gold earned this run
///     (from <see cref="GameStats" />) plus the player's total gold (from <see cref="Wallet" />), and
///     offers two restart options: from wave 1, or from the wave the player died on (via
///     <see cref="RunState" />). Both reload the scene. Listens to the player's <see cref="Health.OnDied" />
///     through the <see cref="Player" /> singleton.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DeathScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text goblinsKilledText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text totalGoldText;
    [Tooltip("Restarts the run from wave 1.")]
    [SerializeField] private Button tryAgainButton;
    [Tooltip("Optional: restarts from the wave the player died on. Leave unassigned to offer only a wave-1 restart.")]
    [SerializeField] private Button restartCurrentWaveButton;
    [Tooltip("Optional label on the restart-current-wave button, set to e.g. \"Restart Wave 3\".")]
    [SerializeField] private TMP_Text restartCurrentWaveLabel;
    [Tooltip("Seconds to fade the screen in.")]
    [SerializeField] private float fadeDuration = 1f;

    private Health playerHealth;
    private bool anyError = false;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (tryAgainButton == null)
        {
            Debug.LogError("Try Again button is not assigned in the inspector.");
            anyError = true;
        }

        Player player = Player.Instance;
        if (player == null || player.Health == null)
        {
            Debug.LogError("No player Health found for DeathScreen to listen to.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Hidden and non-interactive until the player dies.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        tryAgainButton.onClick.AddListener(RestartFromLevelOne);
        if (restartCurrentWaveButton != null)
        {
            restartCurrentWaveButton.onClick.AddListener(RestartFromCurrentWave);
        }

        playerHealth = player.Health;
        playerHealth.OnDied += HandlePlayerDied;
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.RemoveListener(RestartFromLevelOne);
        }
        if (restartCurrentWaveButton != null)
        {
            restartCurrentWaveButton.onClick.RemoveListener(RestartFromCurrentWave);
        }
    }

    private void HandlePlayerDied()
    {
        int killed = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        int earned = GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0;
        int total = Player.Instance != null && Player.Instance.Wallet != null ? Player.Instance.Wallet.Coins : 0;

        if (goblinsKilledText != null)
        {
            goblinsKilledText.text = $"Goblins Slain: {killed}";
        }
        if (goldEarnedText != null)
        {
            goldEarnedText.text = $"Gold Earned: {earned}";
        }
        if (totalGoldText != null)
        {
            totalGoldText.text = $"Total Gold: {total}";
        }

        // Only offer "restart from current wave" if there's a wave in progress to return to.
        if (restartCurrentWaveButton != null)
        {
            bool hasWave = WaveSpawner.Instance != null;
            restartCurrentWaveButton.gameObject.SetActive(hasWave);
            if (hasWave && restartCurrentWaveLabel != null)
            {
                restartCurrentWaveLabel.text = $"Restart Wave {WaveSpawner.Instance.CurrentWave}";
            }
        }

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            // Unscaled so the fade still runs if the game pauses time on death.
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }

    private void RestartFromLevelOne()
    {
        RunState.StartingWave = 1;
        Reload();
    }

    private void RestartFromCurrentWave()
    {
        // WaveSpawner keeps RunState.StartingWave at the current wave, but read it back explicitly in case
        // execution order ever changes.
        if (WaveSpawner.Instance != null)
        {
            RunState.StartingWave = WaveSpawner.Instance.CurrentWave;
        }
        Reload();
    }

    private void Reload()
    {
        // Reload the active scene; scene-scoped singletons (GameStats, Wallet) reset naturally, while the
        // wave to resume from rides across the reload in the static RunState.
        Time.timeScale = 1f; // ensure normal speed resumes even if something paused time on death.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
