using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The boss / captain / special-enemy announcement banner in the HUD (built by
///     <c>ScreenRestyleBuilder.BuildBossIntro</c>, Bladehold > UI > Restyle). A dark band with gold rules
///     wipes open across the upper screen; a small eyebrow line ("Clan Captain"), the enemy's name in
///     large spaced Texturina with flourishes, difficulty skulls tinted by tier and a subtitle line drop in,
///     hold with a slow drift, then fold away. Cinematic intros (the intro camera fly-to) also slide
///     letterbox bars in from the screen edges. Everything runs on unscaled time.
/// </summary>
public class EnemyIntroUI : MonoBehaviour
{
    public static EnemyIntroUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("CanvasGroup controlling overall visibility and raycast blocking.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Top letterbox bar (anchored to the top edge, pivot at its top). Cinematic intros only.")]
    [SerializeField] private RectTransform topBar;

    [Tooltip("Bottom letterbox bar (anchored to the bottom edge, pivot at its bottom). Cinematic intros only.")]
    [SerializeField] private RectTransform bottomBar;

    [Tooltip("The dark band behind the name. Wipes open horizontally (x scale).")]
    [SerializeField] private RectTransform band;

    [Tooltip("Holds the eyebrow, name, flourishes, skulls and subtitle; slides and drifts as one.")]
    [SerializeField] private RectTransform nameContainer;

    [Tooltip("Small spaced line above the name, e.g. 'Clan Captain'. Hidden when empty.")]
    [SerializeField] private TextMeshProUGUI eyebrowText;

    [Tooltip("The enemy's name.")]
    [SerializeField] private TextMeshProUGUI enemyNameText;

    [Tooltip("Line under the name: tier and rewards for captains. Hidden when empty.")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Tooltip("Flourish left of the name; it spreads outward as the banner opens.")]
    [SerializeField] private RectTransform flourishLeft;

    [Tooltip("Flourish right of the name; it spreads outward as the banner opens.")]
    [SerializeField] private RectTransform flourishRight;

    [Tooltip("Row of difficulty skulls; hidden when the intro has no difficulty.")]
    [SerializeField] private GameObject skullRow;

    [Tooltip("Skull icons, lit up to the tier (1-4) and tinted with the tier colour.")]
    [SerializeField] private Image[] skullIcons = new Image[4];

    [Header("Animation Tuning")]
    [Tooltip("Duration in seconds for the banner opening (unscaled time).")]
    [SerializeField] private float slideInDuration = 0.4f;

    [Tooltip("Duration in seconds for the banner folding away (unscaled time).")]
    [SerializeField] private float slideOutDuration = 0.3f;

    [Tooltip("Horizontal drift of the name block, in pixels, over the hold.")]
    [SerializeField] private float horizontalDriftPixels = 24f;

    [Tooltip("The name starts this much larger and settles to 1.")]
    [SerializeField] private float nameStartScale = 1.18f;

    [Tooltip("Gap (pixels) between the name's edge and each flourish; flourishes follow the name's width.")]
    [SerializeField] private float flourishGap = 36f;

    [Tooltip("How far (pixels) the flourishes travel outward while opening.")]
    [SerializeField] private float flourishSpread = 60f;

    [Tooltip("Alpha of unlit skulls (tiers above this intro's).")]
    [Range(0f, 1f)] [SerializeField] private float unlitSkullAlpha = 0.18f;

    private Coroutine activeIntroRoutine;
    private Vector2 flourishLeftRest;
    private Vector2 flourishRightRest;
    private float topBarHeight;
    private float bottomBarHeight;
    private bool anyError;

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
        if (enemyNameText == null || nameContainer == null || band == null)
        {
            Debug.LogError("[EnemyIntroUI] enemyNameText, nameContainer or band is not assigned. Run Bladehold > UI > Restyle > Boss Intro Banner.", this);
            anyError = true;
        }

        if (topBar != null) topBarHeight = topBar.rect.height;
        if (bottomBar != null) bottomBarHeight = bottomBar.rect.height;

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Cinematic intro for a special enemy (with letterbox bars), no difficulty line.</summary>
    public void ShowIntro(string enemyName, float totalDuration, System.Action onComplete = null)
    {
        ShowIntro(Loc.Get("intro.eyebrow_special", "A mighty foe approaches"), enemyName, 0, "", totalDuration, true, onComplete);
    }

    /// <summary>Banner with difficulty skulls and a subtitle line, no letterbox.</summary>
    public void ShowIntro(string enemyName, int difficultySkulls, string subtitle = "", float totalDuration = 3.5f, System.Action onComplete = null)
    {
        ShowIntro("", enemyName, difficultySkulls, subtitle, totalDuration, false, onComplete);
    }

    /// <summary>
    ///     Plays the banner. <paramref name="eyebrow" /> and <paramref name="subtitle" /> hide when empty;
    ///     <paramref name="difficultySkulls" /> 0 hides the skulls; <paramref name="cinematic" /> adds letterbox bars.
    /// </summary>
    public void ShowIntro(string eyebrow, string enemyName, int difficultySkulls, string subtitle, float totalDuration, bool cinematic, System.Action onComplete = null)
    {
        if (anyError)
        {
            onComplete?.Invoke();
            return;
        }
        if (activeIntroRoutine != null)
        {
            StopCoroutine(activeIntroRoutine);
        }

        SetBannerActive(true);
        enemyNameText.text = enemyName;
        PlaceFlourishes();
        SetLine(eyebrowText, eyebrow);
        SetLine(subtitleText, subtitle);

        int skulls = Mathf.Clamp(difficultySkulls, 0, 4);
        if (skullRow != null)
        {
            skullRow.SetActive(skulls > 0);
        }
        if (skulls > 0 && skullIcons != null)
        {
            Color tierColor = BannerDifficultyHelper.GetTierColor((BannerDifficultyTier)skulls);
            for (int i = 0; i < skullIcons.Length; i++)
            {
                if (skullIcons[i] == null) continue;
                Color c = tierColor;
                c.a = i < skulls ? 1f : unlitSkullAlpha;
                skullIcons[i].color = c;
            }
        }

        if (topBar != null) topBar.gameObject.SetActive(cinematic);
        if (bottomBar != null) bottomBar.gameObject.SetActive(cinematic);

        activeIntroRoutine = StartCoroutine(IntroSequenceRoutine(totalDuration, onComplete));
    }

    /// <summary>
    ///     Just the cinematic letterbox bars sliding in and out, no name band (<see cref="GateFallCinematic" />).
    /// </summary>
    public void ShowLetterbox(float totalDuration, System.Action onComplete = null)
    {
        if (anyError)
        {
            onComplete?.Invoke();
            return;
        }
        if (activeIntroRoutine != null)
        {
            StopCoroutine(activeIntroRoutine);
        }

        // The band, name and flourishes stay hidden; ShowIntro turns them back on.
        SetBannerActive(false);
        if (topBar != null) topBar.gameObject.SetActive(true);
        if (bottomBar != null) bottomBar.gameObject.SetActive(true);

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

    /// <summary>Shows or hides the name banner (band, name block, flourishes); the letterbox bars are separate.</summary>
    private void SetBannerActive(bool active)
    {
        band.gameObject.SetActive(active);
        nameContainer.gameObject.SetActive(active);
        if (flourishLeft != null) flourishLeft.gameObject.SetActive(active);
        if (flourishRight != null) flourishRight.gameObject.SetActive(active);
    }

    /// <summary>Rests the flourishes just outside the name's rendered width.</summary>
    private void PlaceFlourishes()
    {
        float maxWidth = enemyNameText.rectTransform.rect.width;
        float half = Mathf.Min(enemyNameText.GetPreferredValues(enemyNameText.text).x, maxWidth) * 0.5f;
        float y = enemyNameText.rectTransform.anchoredPosition.y;
        flourishLeftRest = new Vector2(-(half + flourishGap), y);
        flourishRightRest = new Vector2(half + flourishGap, y);
    }

    private static void SetLine(TMP_Text text, string value)
    {
        if (text == null) return;
        bool show = !string.IsNullOrEmpty(value);
        text.gameObject.SetActive(show);
        if (show) text.text = value;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    /// <summary>Applies the open amount (0 = folded, 1 = fully open) to every animated part.</summary>
    private void ApplyOpen(float open, float textIn, float drift)
    {
        band.localScale = new Vector3(Mathf.Max(0.001f, open), 1f, 1f);

        float scale = Mathf.Lerp(nameStartScale, 1f, textIn);
        nameContainer.localScale = new Vector3(scale, scale, 1f);
        nameContainer.anchoredPosition = new Vector2(drift, 0f);

        float spread = (1f - textIn) * -flourishSpread;
        if (flourishLeft != null) flourishLeft.anchoredPosition = flourishLeftRest - new Vector2(spread, 0f);
        if (flourishRight != null) flourishRight.anchoredPosition = flourishRightRest + new Vector2(spread, 0f);

        if (topBar != null) topBar.anchoredPosition = new Vector2(0f, (1f - open) * topBarHeight);
        if (bottomBar != null) bottomBar.anchoredPosition = new Vector2(0f, -(1f - open) * bottomBarHeight);
    }

    private void SetTextAlpha(float alpha)
    {
        // The name block fades as one; the band and bars are driven by scale/position instead.
        CanvasGroup group = nameContainer.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = alpha;
    }

    private IEnumerator IntroSequenceRoutine(float totalDuration, System.Action onComplete)
    {
        SetVisible(true);
        float drift = -horizontalDriftPixels * 0.5f;
        ApplyOpen(0f, 0f, drift);
        SetTextAlpha(0f);

        // Phase 1: band wipes open, then the name settles in (ease-out cubic / back).
        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideInDuration);
            float open = 1f - Mathf.Pow(1f - Mathf.Clamp01(t * 1.6f), 3f);
            float textT = Mathf.Clamp01((t - 0.25f) / 0.75f);
            float textIn = 1f - Mathf.Pow(1f - textT, 3f);
            ApplyOpen(open, textIn, drift);
            SetTextAlpha(textIn);
            yield return null;
        }
        ApplyOpen(1f, 1f, drift);
        SetTextAlpha(1f);

        // Phase 2: hold with a slow drift.
        float holdDuration = Mathf.Max(0.1f, totalDuration - slideInDuration - slideOutDuration);
        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / holdDuration);
            drift = Mathf.Lerp(-horizontalDriftPixels * 0.5f, horizontalDriftPixels * 0.5f, t);
            ApplyOpen(1f, 1f, drift);
            yield return null;
        }

        // Phase 3: text fades first, then the band folds shut (ease-in quad).
        elapsed = 0f;
        while (elapsed < slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideOutDuration);
            float ease = t * t;
            SetTextAlpha(1f - Mathf.Clamp01(t * 2f));
            ApplyOpen(1f - ease, 1f, drift);
            yield return null;
        }

        SetVisible(false);
        activeIntroRoutine = null;
        onComplete?.Invoke();
    }
}
