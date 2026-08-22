using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Cinematic Letterbox and Enemy Title Overlay for boss / special enemy introductions.
///     Features top and bottom black bars that slide in rapidly and slowly drift horizontally,
///     while the enemy name slides in at the top in white text.
/// </summary>
public class EnemyIntroUI : MonoBehaviour
{
    public static EnemyIntroUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("CanvasGroup controlling overall visibility and raycast blocking.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Top black letterbox bar RectTransform.")]
    [SerializeField] private RectTransform topBar;

    [Tooltip("Bottom black letterbox bar RectTransform.")]
    [SerializeField] private RectTransform bottomBar;

    [Tooltip("Text component displaying the enemy name at the top in white.")]
    [SerializeField] private TextMeshProUGUI enemyNameText;

    [Tooltip("Container or RectTransform for the name text slide animation.")]
    [SerializeField] private RectTransform nameContainer;

    [Header("Animation Tuning")]
    [Tooltip("Duration in seconds for the initial rapid slide-in (unscaled time).")]
    [SerializeField] private float slideInDuration = 0.3f;

    [Tooltip("Duration in seconds for the exit slide-out / fade-out (unscaled time).")]
    [SerializeField] private float slideOutDuration = 0.25f;

    [Tooltip("Horizontal drift distance in pixels over the intro duration.")]
    [SerializeField] private float horizontalDriftPixels = 40f;

    [Tooltip("Target height of each letterbox bar in pixels.")]
    [SerializeField] private float barHeight = 110f;

    private Coroutine activeIntroRoutine;

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

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    ///     Plays the cinematic letterbox and enemy name intro sequence for the specified duration.
    /// </summary>
    public void ShowIntro(string enemyName, float totalDuration, System.Action onComplete = null)
    {
        if (activeIntroRoutine != null)
        {
            StopCoroutine(activeIntroRoutine);
        }

        if (enemyNameText != null)
        {
            enemyNameText.text = enemyName.ToUpper();
        }

        activeIntroRoutine = StartCoroutine(IntroSequenceRoutine(totalDuration, onComplete));
    }

    /// <summary>
    ///     Immediately hides the intro UI and aborts any active sequence.
    /// </summary>
    public void HideImmediate()
    {
        if (activeIntroRoutine != null)
        {
            StopCoroutine(activeIntroRoutine);
            activeIntroRoutine = null;
        }
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private IEnumerator IntroSequenceRoutine(float totalDuration, System.Action onComplete)
    {
        SetVisible(true);

        float topBarHiddenY = barHeight + 20f;
        float bottomBarHiddenY = -(barHeight + 20f);
        float topBarVisibleY = 0f;
        float bottomBarVisibleY = 0f;

        Vector2 nameHiddenPos = new Vector2(0f, 60f);
        Vector2 nameVisiblePos = new Vector2(0f, 0f);

        if (topBar != null)
        {
            topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, barHeight);
            topBar.anchoredPosition = new Vector2(0f, topBarHiddenY);
        }

        if (bottomBar != null)
        {
            bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, barHeight);
            bottomBar.anchoredPosition = new Vector2(0f, bottomBarHiddenY);
        }

        if (nameContainer != null)
        {
            nameContainer.anchoredPosition = nameHiddenPos;
        }

        if (enemyNameText != null)
        {
            enemyNameText.alpha = 0f;
        }

        // Phase 1: Rapid slide-in (0.3s)
        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideInDuration);
            float ease = 1f - Mathf.Pow(1f - t, 3f); // Ease-out cubic

            if (topBar != null)
            {
                topBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(topBarHiddenY, topBarVisibleY, ease));
            }
            if (bottomBar != null)
            {
                bottomBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(bottomBarHiddenY, bottomBarVisibleY, ease));
            }
            if (nameContainer != null)
            {
                nameContainer.anchoredPosition = Vector2.Lerp(nameHiddenPos, nameVisiblePos, ease);
            }
            if (enemyNameText != null)
            {
                enemyNameText.alpha = ease;
            }

            yield return null;
        }

        // Snap to resting position
        if (topBar != null) topBar.anchoredPosition = new Vector2(0f, topBarVisibleY);
        if (bottomBar != null) bottomBar.anchoredPosition = new Vector2(0f, bottomBarVisibleY);
        if (nameContainer != null) nameContainer.anchoredPosition = nameVisiblePos;
        if (enemyNameText != null) enemyNameText.alpha = 1f;

        // Phase 2: Hold & Slow Horizontal Drift
        float holdDuration = Mathf.Max(0.1f, totalDuration - slideInDuration - slideOutDuration);
        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / holdDuration);
            float driftOffset = Mathf.Lerp(-horizontalDriftPixels * 0.5f, horizontalDriftPixels * 0.5f, t);

            if (topBar != null)
            {
                topBar.anchoredPosition = new Vector2(driftOffset, topBarVisibleY);
            }
            if (bottomBar != null)
            {
                bottomBar.anchoredPosition = new Vector2(-driftOffset, bottomBarVisibleY);
            }
            if (nameContainer != null)
            {
                nameContainer.anchoredPosition = new Vector2(driftOffset * 0.5f, nameVisiblePos.y);
            }

            yield return null;
        }

        // Phase 3: Slide-out / Fade-out
        elapsed = 0f;
        while (elapsed < slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideOutDuration);
            float ease = t * t; // Ease-in quad

            if (topBar != null)
            {
                topBar.anchoredPosition = new Vector2(topBar.anchoredPosition.x, Mathf.Lerp(topBarVisibleY, topBarHiddenY, ease));
            }
            if (bottomBar != null)
            {
                bottomBar.anchoredPosition = new Vector2(bottomBar.anchoredPosition.x, Mathf.Lerp(bottomBarVisibleY, bottomBarHiddenY, ease));
            }
            if (nameContainer != null)
            {
                nameContainer.anchoredPosition = Vector2.Lerp(nameVisiblePos, nameHiddenPos, ease);
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - ease;
            }

            yield return null;
        }

        SetVisible(false);
        activeIntroRoutine = null;
        onComplete?.Invoke();
    }
}
