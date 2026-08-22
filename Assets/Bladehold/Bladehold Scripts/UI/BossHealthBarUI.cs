using System.Collections;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;

/// <summary>
///     Top-center screen Boss / Special Enemy Health Bar for the HUD.
///     Modeled after <see cref="PlayerHealthBarUI"/> with MMProgressBar integration,
///     supporting dynamic binding to any active boss Health component, name display,
///     and smooth canvas group fade in/out.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("CanvasGroup controlling overall visibility and alpha fading.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("The MMProgressBar visualising boss health.")]
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("Label displaying boss display name (e.g. 'SLAYER').")]
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Tooltip("Optional text field to display exact numeric health (e.g. 200 / 200).")]
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Animation Tuning")]
    [Tooltip("Fade-in duration when boss appears.")]
    [SerializeField] private float fadeInDuration = 0.4f;

    [Tooltip("Delay after boss death before the health bar fades out.")]
    [SerializeField] private float deathFadeDelay = 1.5f;

    [Tooltip("Fade-out duration when boss dies or is hidden.")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    private Health currentBossHealth;
    private Coroutine fadeRoutine;
    private bool isVisible = false;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        Unsubscribe();
    }

    /// <summary>
    ///     Binds the health bar to an active boss Health component and begins displaying it.
    /// </summary>
    public void Show(Health bossHealth, string bossName)
    {
        if (bossHealth == null) return;

        Unsubscribe();

        currentBossHealth = bossHealth;
        currentBossHealth.OnHealthChanged += Refresh;
        currentBossHealth.OnDied += HandleBossDied;

        if (bossNameText != null)
        {
            bossNameText.text = bossName.ToUpper();
        }

        isVisible = true;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(FadeRoutine(1f, fadeInDuration));

        Refresh();
    }

    /// <summary>
    ///     Hides the boss health bar with a smooth fade-out.
    /// </summary>
    public void Hide()
    {
        Unsubscribe();
        isVisible = false;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(FadeRoutine(0f, fadeOutDuration));
    }

    /// <summary>
    ///     Immediately hides the boss health bar with zero alpha.
    /// </summary>
    public void HideImmediate()
    {
        Unsubscribe();
        isVisible = false;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Unsubscribe()
    {
        if (currentBossHealth != null)
        {
            currentBossHealth.OnHealthChanged -= Refresh;
            currentBossHealth.OnDied -= HandleBossDied;
            currentBossHealth = null;
        }
    }

    private void Refresh()
    {
        if (currentBossHealth == null || progressBar == null) return;

        progressBar.UpdateBar(currentBossHealth.CurrentHealth, 0f, currentBossHealth.MaxHealth);

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentBossHealth.CurrentHealth)} / {Mathf.CeilToInt(currentBossHealth.MaxHealth)}";
        }
    }

    private void HandleBossDied()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(DeathFadeOutRoutine());
    }

    private IEnumerator DeathFadeOutRoutine()
    {
        Refresh();
        yield return new WaitForSeconds(deathFadeDelay);
        yield return FadeRoutine(0f, fadeOutDuration);
        HideImmediate();
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = targetAlpha > 0.01f;
        canvasGroup.interactable = targetAlpha > 0.01f;
    }
}
