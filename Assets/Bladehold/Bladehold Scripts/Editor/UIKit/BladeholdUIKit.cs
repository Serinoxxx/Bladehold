using MoreMountains.Feedbacks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Shared building blocks for Bladehold's generated menus, in the visual language of the settings
///     panel (<see cref="SettingsPanelBuilder" />): a dark window with a gold frame and a faint sheen,
///     a Texturina title flanked by flourishes, parchment / ghost / danger buttons with hover glow and
///     juice, recessed wells, gold section headers and dividers.
///
///     Every graphic is painted by <see cref="UIColorRole" /> through a <see cref="UIThemedGraphic" />, so
///     builders never pick literal colours: call <see cref="Begin" /> with the menu being built, and the
///     result follows that menu's theme in the <see cref="UIThemeRegistrySO" /> (and repaints when it
///     changes). Builders using it: shop, meta upgrades, pedestals.
/// </summary>
public static class BladeholdUIKit
{
    public enum ButtonStyle { Primary, Ghost, Danger }

    private const string HudSprites = "Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_";
    private const string MenuButtonPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/MenuButton.prefab";

    public static Sprite White, FrameLarge, FrameSmall, ParchmentSmall, Line, LineLeft, LineRight, GradientV, GradientH, Diamond;

    /// <summary>The theme the current build paints with (its colours are baked, then kept live by UIThemedGraphic).</summary>
    public static UIThemeSO Theme { get; private set; }

    /// <summary>Hover tick given to every control built after <see cref="SetFeedbacks" />.</summary>
    private static MMF_Player hover;
    private static MMF_Player click;

    /// <summary>Starts a build for <paramref name="menu" />: loads sprites and resolves the menu's theme.</summary>
    public static void Begin(UIMenuId menu)
    {
        White = LoadSprite("Box_White_01");
        FrameLarge = LoadSprite("Frame_Box_Medium_02");
        FrameSmall = LoadSprite("Frame_Box_Small_01");
        ParchmentSmall = LoadSprite("Box_Small_Parchment_01");
        Line = LoadSprite("Line_01");
        LineLeft = LoadSprite("Line_04_Left");
        LineRight = LoadSprite("Line_04_Right");
        GradientV = LoadSprite("Gradient_Vertical_Smooth_01");
        GradientH = LoadSprite("Gradient_Horizontal_Smooth_01");
        Diamond = LoadSprite("Mask_Diamond_01");
        UIThemeTools.EnsureThemeAssets();
        Theme = UITheme.For(menu);
        hover = null;
        click = null;
    }

    /// <summary>Marks <paramref name="root" /> as the menu's theme scope.</summary>
    public static void Scope(GameObject root, UIMenuId menu)
    {
        UIThemeScope scope = root.GetComponent<UIThemeScope>();
        if (scope == null) scope = root.AddComponent<UIThemeScope>();
        scope.Configure(menu);
    }

    // ───────────────────────────────────────────────── primitives

    public static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>A child that layout groups on its parent ignore (frames, sheens, glows).</summary>
    public static RectTransform Decoration(string name, Transform parent)
    {
        RectTransform rt = NewUI(name, parent);
        rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return rt;
    }

    /// <summary>Adds a themed Image. <paramref name="alpha" /> multiplies the role colour's alpha.</summary>
    public static Image Img(Component target, Sprite sprite, UIColorRole role, float alpha = 1f, bool sliced = false, float ppu = 1f, bool raycast = false)
    {
        var image = target.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = ppu;
        image.raycastTarget = raycast;
        Paint(image, role, alpha);
        return image;
    }

    /// <summary>A transparent raycast catcher (the hit area of a button whose visual scales).</summary>
    public static Image HitArea(Component target)
    {
        var image = target.gameObject.AddComponent<Image>();
        image.sprite = White;
        image.color = new Color(0f, 0f, 0f, 0.001f);
        return image;
    }

    public static TextMeshProUGUI Txt(RectTransform rt, string text, UIFontRole font, float size, UIColorRole role, TextAlignmentOptions align)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = Theme.GetFont(font);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        Paint(tmp, role, 1f, font);
        return tmp;
    }

    /// <summary>Colours <paramref name="graphic" /> with a theme role now and keeps it on that role.</summary>
    public static void Paint(Graphic graphic, UIColorRole role, float alpha = 1f, UIFontRole font = UIFontRole.Keep)
    {
        graphic.color = Theme.Get(role, alpha);
        UIThemedGraphic themed = graphic.GetComponent<UIThemedGraphic>();
        if (themed == null) themed = graphic.gameObject.AddComponent<UIThemedGraphic>();
        themed.Configure(role, font);
    }

    /// <summary>Drops the theme binding from a graphic whose colour runtime code sets per state (costs, pips).</summary>
    public static void Unthemed(Graphic graphic)
    {
        UIThemedGraphic themed = graphic.GetComponent<UIThemedGraphic>();
        if (themed != null) Object.DestroyImmediate(themed);
    }

    // ───────────────────────────────────────────────── composites

    /// <summary>The settings-panel window look on <paramref name="window" />: fill, faint accent sheen, outer gold frame.</summary>
    public static void WindowChrome(RectTransform window, bool raycast = true)
    {
        Img(window, White, UIColorRole.Window, raycast: raycast);
        RectTransform sheen = Decoration("Sheen", window);
        Stretch(sheen);
        Img(sheen, GradientV, UIColorRole.Accent, 0.03f);
        RectTransform frame = Decoration("Frame", window);
        Stretch(frame, -10f, -10f, -10f, -10f);
        Img(frame, FrameLarge, UIColorRole.Frame, sliced: true, ppu: 2.2f);
    }

    /// <summary>A recessed panel with a thin muted frame (lists, details, cards).</summary>
    public static void Well(RectTransform rt, float frameAlpha = 0.45f, bool raycast = false)
    {
        Img(rt, White, UIColorRole.Well, raycast: raycast);
        RectTransform frame = Decoration("WellFrame", rt);
        Stretch(frame, -2f, -2f, -2f, -2f);
        Img(frame, FrameSmall, UIColorRole.AccentMuted, frameAlpha, sliced: true, ppu: 4f);
    }

    /// <summary>Uppercase spaced Texturina title with a flourish each side, centred in <paramref name="header" />.</summary>
    public static TextMeshProUGUI Title(RectTransform header, string text, float size = 44f, float flourishGap = 150f)
    {
        TextMeshProUGUI title = Txt(NewUI("Title", header), text, UIFontRole.Header, size, UIColorRole.Accent, TextAlignmentOptions.Center);
        Stretch(title.rectTransform);
        title.characterSpacing = 6f;
        title.fontStyle = FontStyles.UpperCase;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform left = NewUI("FlourishLeft", header);
        Place(left, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-flourishGap, -2f), new Vector2(200f, 40f));
        Img(left, LineLeft, UIColorRole.AccentMuted).preserveAspect = true;
        RectTransform right = NewUI("FlourishRight", header);
        Place(right, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(flourishGap, -2f), new Vector2(200f, 40f));
        Img(right, LineRight, UIColorRole.AccentMuted).preserveAspect = true;
        return title;
    }

    /// <summary>A sliced gold rule (header/footer dividers).</summary>
    public static Image Divider(RectTransform rt, float alpha = 0.5f)
    {
        return Img(rt, Line, UIColorRole.AccentMuted, alpha, sliced: true);
    }

    /// <summary>Section label (spaced uppercase, muted gold) with a hairline rule under it, for a layout list.</summary>
    public static TextMeshProUGUI SectionHeader(RectTransform parent, string text, float height = 46f)
    {
        RectTransform section = NewUI("Section " + text, parent);
        LayoutElement le = section.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        TextMeshProUGUI label = Txt(NewUI("Label", section), text, UIFontRole.Header, 22, UIColorRole.AccentMuted, TextAlignmentOptions.BottomLeft);
        Stretch(label.rectTransform, 4f, 0f, 0f, 8f);
        label.characterSpacing = 4f;
        label.fontStyle = FontStyles.UpperCase;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rule = NewUI("Rule", section);
        rule.anchorMin = new Vector2(0f, 0f);
        rule.anchorMax = new Vector2(1f, 0f);
        rule.pivot = new Vector2(0.5f, 0f);
        rule.sizeDelta = new Vector2(0f, 2f);
        rule.anchoredPosition = new Vector2(0f, 4f);
        Img(rule, White, UIColorRole.AccentMuted, 0.25f);
        return label;
    }

    /// <summary>
    ///     A settings-style button: transparent hit area, a scaling "Visual" child with the fill, a hover
    ///     glow frame and a Texturina label. Primary = parchment, Ghost = dark, Danger = red.
    /// </summary>
    public static Button Button(string name, RectTransform parent, string label, ButtonStyle style, out TextMeshProUGUI labelText, float fontSize = 22f)
    {
        RectTransform rt = NewUI(name, parent);
        HitArea(rt);
        RectTransform visual = NewUI("Visual", rt);
        Stretch(visual);
        Image bg = style == ButtonStyle.Ghost
            ? Img(visual, White, UIColorRole.Ghost)
            : Img(visual, ParchmentSmall, style == ButtonStyle.Danger ? UIColorRole.Danger : UIColorRole.Parchment, sliced: true, ppu: 2f);
        RectTransform glow = NewUI("HoverGlow", visual);
        Stretch(glow, -4f, -4f, -4f, -4f);
        Image glowImage = Img(glow, FrameSmall, UIColorRole.Accent, sliced: true, ppu: 4f);
        UIColorRole textRole = style == ButtonStyle.Primary ? UIColorRole.TextOnParchment : UIColorRole.Text;
        labelText = Txt(NewUI("Label", visual), label, UIFontRole.Header, fontSize, textRole, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform, 8f, 8f, 0f, 0f);
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 12f;
        labelText.fontSizeMax = fontSize;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.colors = Tint(Color.white, Hex("FFF8E8"), Hex("D8CCB0"));
        Juice(button, visual, glowImage, 1.05f, 0.95f);
        return button;
    }

    /// <summary>A square "✕" close button in the ghost style.</summary>
    public static Button CloseButton(RectTransform parent)
    {
        Button close = Button("CloseButton", parent, "X", ButtonStyle.Ghost, out TextMeshProUGUI label, 24f);
        label.characterSpacing = 0f;
        return close;
    }

    // ───────────────────────────────────────────────── juice / feedback

    /// <summary>Copies MenuButton.prefab's click MMF (and a quiet pitched-up hover tick) under <paramref name="parent" />.</summary>
    public static void SetFeedbacks(Transform parent)
    {
        click = CloneClickFeedback(parent, "ClickMMF");
        hover = CloneClickFeedback(parent, "HoverMMF");
        if (hover == null)
        {
            return;
        }
        hover.CooldownDuration = 0.06f;
        foreach (MMF_Feedback feedback in hover.FeedbacksList)
        {
            if (feedback is MMF_Sound sound)
            {
                sound.MinVolume = 0.3f;
                sound.MaxVolume = 0.35f;
                sound.MinPitch = 1.55f;
                sound.MaxPitch = 1.7f;
            }
        }
    }

    public static void Juice(Selectable selectable, RectTransform scaleTarget, Graphic highlight, float hoverScale = 1.04f, float pressScale = 0.96f)
    {
        var juice = selectable.gameObject.AddComponent<UISelectableJuice>();
        var so = new SerializedObject(juice);
        so.FindProperty("selectable").objectReferenceValue = selectable;
        so.FindProperty("scaleTarget").objectReferenceValue = scaleTarget;
        so.FindProperty("highlight").objectReferenceValue = highlight;
        so.FindProperty("clickFeedback").objectReferenceValue = click;
        so.FindProperty("hoverFeedback").objectReferenceValue = hover;
        so.FindProperty("hoverScale").floatValue = hoverScale;
        so.FindProperty("pressScale").floatValue = pressScale;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static MMF_Player CloneClickFeedback(Transform parent, string name)
    {
        GameObject menuButton = AssetDatabase.LoadAssetAtPath<GameObject>(MenuButtonPrefabPath);
        MMF_Player source = menuButton != null ? menuButton.GetComponentInChildren<MMF_Player>(true) : null;
        if (source == null)
        {
            Debug.LogWarning("[BladeholdUIKit] MenuButton.prefab has no click MMF_Player; controls will be silent.");
            return null;
        }
        GameObject copy = Object.Instantiate(source.gameObject, parent);
        copy.name = name;
        return copy.GetComponent<MMF_Player>();
    }

    // ───────────────────────────────────────────────── layout helpers

    public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Full-width strip hanging from the top edge, <paramref name="top" /> down, <paramref name="height" /> tall.</summary>
    public static void AnchorTop(RectTransform rt, float top, float height, float sideInset = 0f)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(sideInset, -top - height);
        rt.offsetMax = new Vector2(-sideInset, -top);
    }

    /// <summary>Full-width strip sitting on the bottom edge, <paramref name="bottom" /> up, <paramref name="height" /> tall.</summary>
    public static void AnchorBottom(RectTransform rt, float bottom, float height, float sideInset = 0f)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(sideInset, bottom);
        rt.offsetMax = new Vector2(-sideInset, bottom + height);
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    public static LayoutElement Size(GameObject go, float width, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
        if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
        return le;
    }

    public static ColorBlock Tint(Color normal, Color highlighted, Color pressed, Color? disabled = null)
    {
        ColorBlock block = ColorBlock.defaultColorBlock;
        block.normalColor = normal;
        block.highlightedColor = highlighted;
        block.selectedColor = highlighted;
        block.pressedColor = pressed;
        block.disabledColor = disabled ?? new Color(1f, 1f, 1f, 0.35f);
        block.fadeDuration = 0.08f;
        return block;
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    public static Sprite LoadSprite(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudSprites + name + ".png");
        if (sprite == null)
        {
            Debug.LogWarning($"[BladeholdUIKit] Missing sprite {name}.");
        }
        return sprite;
    }

    /// <summary>Finds a sprite anywhere in the project by exact file name (icons from other Synty packs).</summary>
    public static Sprite FindSprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return null;
    }
}
