using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The big top-centre countdown: shown whenever the active objective is on a clock
///     (<see cref="ITimedObjective" />) and during the rout's "hunt them down" window
///     (<see cref="GameLoopManager.IsRouting" />). A caption (the objective's title), a large m:ss clock and a
///     draining bar; in the last <see cref="UrgentSeconds" /> it turns red and punches on every second (green
///     instead when reaching 0 is the win, e.g. surviving a Goblin Rush).
///     <para>
///         Built entirely from code by <see cref="ObjectiveTrackerUI" /> under the HUD's root canvas, so it needs
///         no prefab wiring. It sits behind the rest of the HUD (first sibling) and drops below the boss health
///         bar while that is showing.
///     </para>
/// </summary>
public class ObjectiveCountdownUI : MonoBehaviour
{
    private const float UrgentSeconds = 10f;
    private const float TopMargin = 40f;
    private const float BelowBossBar = 150f;
    private const float Width = 720f;
    private const float Height = 236f;
    private const float FadeSpeed = 6f;
    private const float PunchScale = 1.16f;
    private const float PunchSeconds = 0.3f;

    private static readonly Color CaptionColor = new Color(0.96f, 0.86f, 0.62f, 1f);
    private static readonly Color ClockColor = Color.white;
    private static readonly Color BarColor = new Color(1f, 0.78f, 0.3f, 1f);
    private static readonly Color UrgentColor = new Color(1f, 0.28f, 0.22f, 1f);
    private static readonly Color AlmostDoneColor = new Color(0.55f, 1f, 0.45f, 1f);
    private static readonly Color BackgroundColor = new Color(0.05f, 0.035f, 0.025f, 0.6f);

    private RectTransform root;
    private CanvasGroup group;
    private TMP_Text caption;
    private TMP_Text clock;
    private RectTransform clockRect;
    private RectTransform barFill;
    private Image barFillImage;

    private int lastShownSecond = -1;
    private float punchTimer;
    private float routMax;
    private string lastCaption;

    /// <summary>Builds the countdown under <paramref name="canvas" />'s root, styled after <paramref name="styleSource" />'s font.</summary>
    public static ObjectiveCountdownUI Create(Canvas canvas, TMP_Text styleSource)
    {
        if (canvas == null) return null;
        Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

        var go = new GameObject("ObjectiveCountdown", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = rootCanvas.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(rootCanvas.transform, false);
        rect.SetAsFirstSibling(); // behind every other HUD element and modal
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(Width, Height);
        rect.anchoredPosition = new Vector2(0f, -TopMargin);

        ObjectiveCountdownUI ui = go.AddComponent<ObjectiveCountdownUI>();
        ui.Build(rect, styleSource);
        return ui;
    }

    private void Build(RectTransform rect, TMP_Text styleSource)
    {
        root = rect;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image bg = NewImage("Background", root, BackgroundColor);
        Stretch(bg.rectTransform, Vector2.zero, Vector2.zero);

        caption = NewText("Caption", root, styleSource, 46f, CaptionColor, FontStyles.Bold | FontStyles.UpperCase);
        RectTransform cr = caption.rectTransform;
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(-40f, 60f);
        cr.anchoredPosition = new Vector2(0f, -12f);

        clock = NewText("Clock", root, styleSource, 128f, ClockColor, FontStyles.Bold);
        clockRect = clock.rectTransform;
        clockRect.anchorMin = clockRect.anchorMax = new Vector2(0.5f, 1f);
        clockRect.pivot = new Vector2(0.5f, 0.5f);
        clockRect.sizeDelta = new Vector2(Width - 40f, 130f);
        clockRect.anchoredPosition = new Vector2(0f, -136f);

        Image barBack = NewImage("BarBack", root, new Color(0f, 0f, 0f, 0.65f));
        RectTransform bb = barBack.rectTransform;
        bb.anchorMin = bb.anchorMax = new Vector2(0.5f, 0f);
        bb.pivot = new Vector2(0.5f, 0f);
        bb.sizeDelta = new Vector2(Width - 80f, 18f);
        bb.anchoredPosition = new Vector2(0f, 20f);

        barFillImage = NewImage("BarFill", bb, BarColor);
        barFill = barFillImage.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = Vector2.one;
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.offsetMin = new Vector2(2f, 2f);
        barFill.offsetMax = new Vector2(-2f, -2f);
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_Text style, float size, Color color, FontStyles fontStyle)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (style != null && style.font != null)
        {
            t.font = style.font;
            t.fontSharedMaterial = style.fontSharedMaterial;
        }
        t.fontSize = size;
        t.fontStyle = fontStyle;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform r, Vector2 offsetMin, Vector2 offsetMax)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool show = TryGetCountdown(out string title, out float remaining, out float limit, out bool failsOnTimeout);

        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * FadeSpeed);
        if (!show)
        {
            lastShownSecond = -1;
            return;
        }

        if (title != lastCaption)
        {
            lastCaption = title;
            caption.text = title;
        }

        int second = Mathf.Max(0, Mathf.CeilToInt(remaining));
        bool urgent = remaining <= UrgentSeconds;
        if (second != lastShownSecond)
        {
            if (urgent && lastShownSecond >= 0) punchTimer = PunchSeconds;
            lastShownSecond = second;
            clock.text = ObjectiveCsv.FormatClock(remaining);
        }

        Color accent = urgent ? (failsOnTimeout ? UrgentColor : AlmostDoneColor) : BarColor;
        clock.color = urgent ? Color.Lerp(ClockColor, accent, 0.85f) : ClockColor;
        barFillImage.color = accent;

        float fraction = limit > 0f ? Mathf.Clamp01(remaining / limit) : 0f;
        barFill.anchorMax = new Vector2(fraction, 1f);

        punchTimer = Mathf.Max(0f, punchTimer - dt);
        float k = PunchSeconds > 0f ? punchTimer / PunchSeconds : 0f;
        float scale = Mathf.Lerp(1f, PunchScale, k * k);
        clockRect.localScale = new Vector3(scale, scale, 1f);

        bool bossBar = BossHealthBarUI.Instance != null && BossHealthBarUI.Instance.IsVisible;
        float targetY = bossBar ? -BelowBossBar : -TopMargin;
        Vector2 pos = root.anchoredPosition;
        pos.y = Mathf.MoveTowards(pos.y, targetY, dt * 900f);
        root.anchoredPosition = pos;
    }

    /// <summary>The countdown to show this frame, if any: the timed objective first, then the rout.</summary>
    private bool TryGetCountdown(out string title, out float remaining, out float limit, out bool failsOnTimeout)
    {
        title = null;
        remaining = 0f;
        limit = 0f;
        failsOnTimeout = true;

        SurvivorsObjectiveManager manager = SurvivorsObjectiveManager.Instance;
        ISurvivorsObjective objective = manager != null ? manager.CurrentObjective : null;
        if (objective is ITimedObjective timed && objective.IsActive && !objective.IsComplete && !objective.IsFailed
            && timed.TimeLimit > 0f)
        {
            title = objective.Title;
            remaining = timed.TimeRemaining;
            limit = timed.TimeLimit;
            failsOnTimeout = timed.FailsOnTimeout;
            return true;
        }

        GameLoopManager loop = GameLoopManager.Instance;
        if (loop != null && loop.IsRouting && loop.RoutSecondsLeft > 0f)
        {
            // RoutSecondsLeft starts at the full duration on the rout's first frame.
            routMax = Mathf.Max(routMax, loop.RoutSecondsLeft);
            title = Loc.Get("hud.countdown.rout", "Hunt them down!");
            remaining = loop.RoutSecondsLeft;
            limit = routMax;
            failsOnTimeout = true;
            return true;
        }

        routMax = 0f;
        return false;
    }
}
