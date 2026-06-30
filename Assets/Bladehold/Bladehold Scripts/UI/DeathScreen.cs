using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Fades in a death screen when the player dies, showing goblins killed and gold earned this run
///     (from <see cref="GameStats" />) plus the player's total gold (from <see cref="Wallet" />), and
///     offers a Try Again button that reloads the scene. Listens to the player's
///     <see cref="Health.OnDied" /> through the <see cref="Player" /> singleton.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DeathScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text goblinsKilledText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text totalGoldText;
    [SerializeField] private Button tryAgainButton;
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

        tryAgainButton.onClick.AddListener(TryAgain);

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
            tryAgainButton.onClick.RemoveListener(TryAgain);
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

    private void TryAgain()
    {
        // Reload the active scene; scene-scoped singletons (GameStats, Wallet) reset naturally.
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
