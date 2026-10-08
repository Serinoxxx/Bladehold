using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Inspect tooltip for the acquired-skill rows of <see cref="SurvivorsPlayerInfoSidebarUI" /> (draft modal
///     and death screen): icon, name, level line, the card's description and its per-level upgrade text.
///     It is built in code by <see cref="Create" /> under the sidebar's root canvas, reusing the sidebar's own
///     chrome (fill image plus its ignore-layout <c>Theme…</c> decorations), so it needs no prefab wiring and
///     always matches the panel it explains. It sits beside the row it describes rather than under the cursor,
///     so mouse hover and gamepad focus place it the same way. Its own canvas sorts it above every UI layer;
///     nothing in it takes raycasts. The fade-in runs on unscaled time (the draft modal pauses the game).
/// </summary>
public class SidebarSkillTooltipUI : MonoBehaviour
{
    /// <summary>What one skill's tooltip shows.</summary>
    public struct Content
    {
        public string title;
        public string subtitle;
        public string description;
        public string upgradeLine;
        public Sprite icon;
        public Color iconColor;
    }

    private const int SortingOrder = 30000;
    private const float FadeInSpeed = 12f;

    // Sizes in 1080p canvas units; multiplied by the root canvas's reference height / 1080.
    private const float Width = 340f;
    private const float Padding = 14f;
    private const float Spacing = 6f;
    private const float IconSize = 56f;
    private const float TitleSize = 24f;
    private const float SubtitleSize = 17f;
    private const float DescriptionSize = 18f;
    private const float UpgradeSize = 16f;
    private const float GapFromRow = 12f;
    private const float ScreenMargin = 16f;

    private RectTransform rect;
    private RectTransform canvasRect;
    private CanvasGroup group;
    private Image iconImage;
    private TMP_Text titleText;
    private TMP_Text subtitleText;
    private TMP_Text descriptionText;
    private TMP_Text upgradeText;
    private float unit = 1f;
    private Object owner;

    /// <summary>
    ///     Builds a hidden tooltip under <paramref name="host" />'s root canvas, skinned like
    ///     <paramref name="chromeSource" /> and painted from <paramref name="theme" />.
    /// </summary>
    public static SidebarSkillTooltipUI Create(RectTransform chromeSource, UIThemeSO theme, TMP_FontAsset headerFont, TMP_FontAsset bodyFont)
    {
        Canvas hostCanvas = chromeSource != null ? chromeSource.GetComponentInParent<Canvas>(true) : null;
        if (hostCanvas == null)
        {
            Debug.LogError("[SidebarSkillTooltipUI] The sidebar has no parent Canvas; can't build the skill tooltip.");
            return null;
        }
        Canvas root = hostCanvas.rootCanvas;

        GameObject go = new GameObject("SidebarSkillTooltip (runtime)", typeof(RectTransform));
        go.layer = root.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(root.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        SidebarSkillTooltipUI tooltip = go.AddComponent<SidebarSkillTooltipUI>();
        tooltip.Build(root, chromeSource, theme, headerFont, bodyFont);
        go.SetActive(false);
        return tooltip;
    }

    private void Build(Canvas root, RectTransform chromeSource, UIThemeSO theme, TMP_FontAsset headerFont, TMP_FontAsset bodyFont)
    {
        rect = (RectTransform)transform;
        canvasRect = (RectTransform)root.transform;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution.y > 0f)
        {
            unit = scaler.referenceResolution.y / 1080f;
        }

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
        canvas.sortingLayerID = root.sortingLayerID;
        canvas.additionalShaderChannels = root.additionalShaderChannels;

        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // Chrome: copy the sidebar's fill, then clone its decoration children (sheen, gold frame).
        Image fill = gameObject.AddComponent<Image>();
        Image sourceFill = chromeSource.GetComponent<Image>();
        if (sourceFill != null)
        {
            fill.sprite = sourceFill.sprite;
            fill.type = sourceFill.type;
            fill.material = sourceFill.material;
            fill.pixelsPerUnitMultiplier = sourceFill.pixelsPerUnitMultiplier;
            fill.color = new Color(sourceFill.color.r, sourceFill.color.g, sourceFill.color.b, Mathf.Max(sourceFill.color.a, 0.96f));
        }
        else
        {
            fill.color = theme.Get(UIColorRole.Window);
        }
        fill.raycastTarget = false;

        for (int i = 0; i < chromeSource.childCount; i++)
        {
            Transform child = chromeSource.GetChild(i);
            LayoutElement le = child.GetComponent<LayoutElement>();
            if (le == null || !le.ignoreLayout || child.GetComponent<Image>() == null) continue;
            GameObject deco = Instantiate(child.gameObject, transform, false);
            deco.name = child.name;
            foreach (Graphic g in deco.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.RoundToInt(Padding * unit);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = Spacing * unit;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rect.sizeDelta = new Vector2(Width * unit, 0f);

        // Header: icon beside name + level line.
        RectTransform header = NewChild("Header", transform);
        HorizontalLayoutGroup headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 12f * unit;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = false;

        RectTransform iconRect = NewChild("Icon", header);
        iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        LayoutElement iconLe = iconRect.gameObject.AddComponent<LayoutElement>();
        iconLe.minWidth = iconLe.preferredWidth = IconSize * unit;
        iconLe.minHeight = iconLe.preferredHeight = IconSize * unit;

        RectTransform titles = NewChild("Titles", header);
        VerticalLayoutGroup titlesLayout = titles.gameObject.AddComponent<VerticalLayoutGroup>();
        titlesLayout.spacing = 2f * unit;
        titlesLayout.childAlignment = TextAnchor.MiddleLeft;
        titlesLayout.childControlWidth = true;
        titlesLayout.childControlHeight = true;
        titlesLayout.childForceExpandWidth = true;
        titlesLayout.childForceExpandHeight = false;
        titles.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        titleText = NewText("Title", titles, headerFont, TitleSize, theme.Get(UIColorRole.Accent));
        subtitleText = NewText("Subtitle", titles, bodyFont, SubtitleSize, theme.Get(UIColorRole.TextDim));

        RectTransform rule = NewChild("Rule", transform);
        Image ruleImage = rule.gameObject.AddComponent<Image>();
        ruleImage.color = theme.Get(UIColorRole.Frame, 0.45f);
        ruleImage.raycastTarget = false;
        LayoutElement ruleLe = rule.gameObject.AddComponent<LayoutElement>();
        ruleLe.minHeight = ruleLe.preferredHeight = Mathf.Max(1f, 2f * unit);

        descriptionText = NewText("Description", transform, bodyFont, DescriptionSize, theme.Get(UIColorRole.Text));
        upgradeText = NewText("Upgrade", transform, bodyFont, UpgradeSize, theme.Get(UIColorRole.AccentMuted));
    }

    private static RectTransform NewChild(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        RectTransform rt = NewChild(name, parent);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size * unit;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    /// <summary>Shows <paramref name="content" /> beside <paramref name="anchor" />, on behalf of <paramref name="requester" />.</summary>
    public void Show(Content content, RectTransform anchor, Object requester)
    {
        owner = requester;

        iconImage.sprite = content.icon;
        iconImage.color = content.iconColor;
        iconImage.gameObject.SetActive(content.icon != null);
        titleText.text = content.title ?? "";
        subtitleText.text = content.subtitle ?? "";
        subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(content.subtitle));
        descriptionText.text = content.description ?? "";
        upgradeText.text = content.upgradeLine ?? "";
        upgradeText.gameObject.SetActive(!string.IsNullOrEmpty(content.upgradeLine));

        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (!wasActive) group.alpha = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        PlaceBeside(anchor);
    }

    /// <summary>Hides the tooltip if <paramref name="requester" /> is the one showing it (or null to force).</summary>
    public void Hide(Object requester)
    {
        if (requester != null && requester != owner) return;
        owner = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (group.alpha < 1f)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, FadeInSpeed * Time.unscaledDeltaTime);
        }
    }

    /// <summary>Opens toward the side of the screen with more room, top-aligned with the row and clamped on screen.</summary>
    private void PlaceBeside(RectTransform anchor)
    {
        if (anchor == null) return;

        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector2 min = canvasRect.InverseTransformPoint(corners[0]);
        Vector2 max = canvasRect.InverseTransformPoint(corners[2]);
        Rect canvasBounds = canvasRect.rect;

        bool openLeft = (min.x + max.x) * 0.5f > canvasBounds.center.x;
        float gap = GapFromRow * unit;
        float margin = ScreenMargin * unit;
        float width = rect.rect.width;
        float height = rect.rect.height;

        rect.pivot = new Vector2(openLeft ? 1f : 0f, 1f);
        float x = openLeft ? min.x - gap : max.x + gap;
        float top = Mathf.Clamp(max.y, canvasBounds.yMin + margin + height, canvasBounds.yMax - margin);
        x = openLeft
            ? Mathf.Max(x, canvasBounds.xMin + margin + width)
            : Mathf.Min(x, canvasBounds.xMax - margin - width);

        rect.localPosition = new Vector3(x, top, 0f);
    }
}
