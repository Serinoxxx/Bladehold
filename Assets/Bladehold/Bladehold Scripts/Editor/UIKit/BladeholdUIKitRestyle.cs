using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Restyle helpers for screens that predate <see cref="BladeholdUIKit" />: they reskin an existing
///     hierarchy in place (same objects, same serialized references, same MMF players) into the shared
///     menu look, so wiring survives. Everything they add is a child named <c>Theme…</c>, and
///     <see cref="ClearThemeDecor" /> removes those first, so a restyle can be re-run safely.
///     Used by <see cref="ScreenRestyleBuilder" />.
/// </summary>
public static class BladeholdUIKitRestyle
{
    /// <summary>Destroys the <c>Theme…</c> decoration children a previous restyle added under <paramref name="parent" />.</summary>
    public static void ClearThemeDecor(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith("Theme"))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    /// <summary>Makes an Image a flat theme fill (white sprite, simple) on <paramref name="role" />.</summary>
    public static Image Flat(Image image, UIColorRole role, float alpha = 1f)
    {
        image.sprite = BladeholdUIKit.White;
        image.type = Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
        BladeholdUIKit.Paint(image, role, alpha);
        return image;
    }

    /// <summary>An invisible raycast/mask graphic (keeps a Button's target or a Mask working, shows nothing).</summary>
    public static void Invisible(Image image)
    {
        BladeholdUIKit.Unthemed(image);
        image.sprite = BladeholdUIKit.White;
        image.type = Image.Type.Simple;
        image.color = new Color(1f, 1f, 1f, 0.001f);
    }

    /// <summary>
    ///     The shop-card look on an existing element: a <see cref="UIColorRole.Well" /> fill, a faint accent
    ///     sheen and a thin frame. <paramref name="frameInset" /> &gt; 0 keeps the frame inside the rect
    ///     (needed under a Mask).
    /// </summary>
    public static Image CardChrome(RectTransform card, float frameAlpha = 0.8f, float frameInset = -4f, UIColorRole fill = UIColorRole.Well, bool addFrame = true)
    {
        ClearThemeDecor(card);
        Image image = card.GetComponent<Image>();
        if (image == null) image = card.gameObject.AddComponent<Image>();
        Flat(image, fill);

        RectTransform sheen = BladeholdUIKit.Decoration("ThemeSheen", card);
        BladeholdUIKit.Stretch(sheen);
        BladeholdUIKit.Img(sheen, BladeholdUIKit.GradientV, UIColorRole.Accent, 0.05f);
        sheen.SetSiblingIndex(0);
        if (addFrame)
        {
            RectTransform frame = BladeholdUIKit.Decoration("ThemeFrame", card);
            BladeholdUIKit.Stretch(frame, frameInset, frameInset, frameInset, frameInset);
            BladeholdUIKit.Img(frame, BladeholdUIKit.FrameSmall, UIColorRole.Frame, frameAlpha, sliced: true, ppu: 3f);
            frame.SetSiblingIndex(1);
        }
        return image;
    }

    /// <summary>The window look (fill, sheen, large gold frame) on an existing element.</summary>
    public static Image WindowChrome(RectTransform window)
    {
        ClearThemeDecor(window);
        Image image = window.GetComponent<Image>();
        if (image == null) image = window.gameObject.AddComponent<Image>();
        Flat(image, UIColorRole.Window);

        RectTransform sheen = BladeholdUIKit.Decoration("ThemeSheen", window);
        BladeholdUIKit.Stretch(sheen);
        BladeholdUIKit.Img(sheen, BladeholdUIKit.GradientV, UIColorRole.Accent, 0.03f);
        sheen.SetSiblingIndex(0);
        RectTransform frame = BladeholdUIKit.Decoration("ThemeFrame", window);
        BladeholdUIKit.Stretch(frame, -10f, -10f, -10f, -10f);
        BladeholdUIKit.Img(frame, BladeholdUIKit.FrameLarge, UIColorRole.Frame, sliced: true, ppu: 2.2f);
        frame.SetSiblingIndex(1);
        return image;
    }

    /// <summary>Recolours (and optionally refonts) a text onto a theme role.</summary>
    public static TMP_Text Ink(TMP_Text text, UIColorRole role, UIFontRole font = UIFontRole.Keep, float alpha = 1f)
    {
        if (text == null) return null;
        if (font != UIFontRole.Keep)
        {
            TMP_FontAsset asset = BladeholdUIKit.Theme.GetFont(font);
            if (asset != null)
            {
                text.font = asset;
                // A material from the previous font samples the wrong atlas (scrambled glyphs).
                Material material = text.fontSharedMaterial;
                if (material == null || material.mainTexture != asset.atlasTexture) text.fontSharedMaterial = asset.material;
            }
        }
        BladeholdUIKit.Paint(text, role, alpha, font);
        return text;
    }

    /// <summary>Title look: spaced uppercase Accent Texturina (keeps an underlay font if it already has one).</summary>
    public static TMP_Text TitleInk(TMP_Text text, float spacing = 6f, bool upper = true)
    {
        if (text == null) return null;
        bool underlay = text.font != null && text.font.name.Contains("Underlay");
        Ink(text, UIColorRole.Accent, underlay ? UIFontRole.Keep : UIFontRole.Header);
        text.characterSpacing = spacing;
        text.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        return text;
    }

    /// <summary>Section-header look: spaced uppercase muted gold.</summary>
    public static TMP_Text SectionInk(TMP_Text text, float spacing = 4f)
    {
        if (text == null) return null;
        Ink(text, UIColorRole.AccentMuted, UIFontRole.Header);
        text.characterSpacing = spacing;
        text.fontStyle = FontStyles.UpperCase;
        return text;
    }

    /// <summary>A sliced gold rule on an existing divider Image.</summary>
    public static Image Rule(Image image, float alpha = 0.5f)
    {
        if (image == null) return null;
        image.sprite = BladeholdUIKit.Line;
        image.type = Image.Type.Sliced;
        BladeholdUIKit.Paint(image, UIColorRole.AccentMuted, alpha);
        return image;
    }

    /// <summary>
    ///     Reskins an existing button in place: <paramref name="fill" /> (default its target graphic) becomes a
    ///     parchment / ghost / danger plate, a hover-glow frame is added, and a <see cref="UISelectableJuice" />
    ///     scales it and fades the glow. Synty button Animators are removed (the juice replaces them); click
    ///     sounds stay with the button's existing UIClickFeedback.
    /// </summary>
    public static void Button(Selectable button, BladeholdUIKit.ButtonStyle style, TMP_Text label, Image fill = null, float frameAlpha = 0.7f)
    {
        if (fill == null) fill = button.targetGraphic as Image;
        if (fill == null) fill = button.GetComponent<Image>();
        ClearThemeDecor(fill.transform);
        if (fill.transform != button.transform) ClearThemeDecor(button.transform);

        if (style == BladeholdUIKit.ButtonStyle.Ghost)
        {
            Flat(fill, UIColorRole.Ghost);
            RectTransform frame = BladeholdUIKit.Decoration("ThemeFrame", fill.transform);
            BladeholdUIKit.Stretch(frame, -2f, -2f, -2f, -2f);
            BladeholdUIKit.Img(frame, BladeholdUIKit.FrameSmall, UIColorRole.AccentMuted, frameAlpha, sliced: true, ppu: 4f);
            frame.SetSiblingIndex(0);
        }
        else
        {
            fill.sprite = BladeholdUIKit.ParchmentSmall;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 2f;
            BladeholdUIKit.Paint(fill, style == BladeholdUIKit.ButtonStyle.Danger ? UIColorRole.Danger : UIColorRole.Parchment);
        }

        RectTransform glow = BladeholdUIKit.Decoration("ThemeGlow", fill.transform);
        BladeholdUIKit.Stretch(glow, -4f, -4f, -4f, -4f);
        Image glowImage = BladeholdUIKit.Img(glow, BladeholdUIKit.FrameSmall, UIColorRole.Accent, sliced: true, ppu: 4f);

        if (label != null)
        {
            Ink(label, style == BladeholdUIKit.ButtonStyle.Primary ? UIColorRole.TextOnParchment : UIColorRole.Text, UIFontRole.Header);
        }

        Animator animator = button.GetComponent<Animator>();
        if (animator != null) Object.DestroyImmediate(animator);
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = fill;
        button.colors = BladeholdUIKit.Tint(Color.white, BladeholdUIKit.Hex("FFF8E8"), BladeholdUIKit.Hex("D8CCB0"));

        UISelectableJuice juice = button.GetComponent<UISelectableJuice>();
        if (juice == null) juice = button.gameObject.AddComponent<UISelectableJuice>();
        var so = new SerializedObject(juice);
        so.FindProperty("selectable").objectReferenceValue = button;
        so.FindProperty("scaleTarget").objectReferenceValue = (RectTransform)button.transform;
        so.FindProperty("highlight").objectReferenceValue = glowImage;
        so.FindProperty("hoverScale").floatValue = 1.04f;
        so.FindProperty("pressScale").floatValue = 0.96f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>A flourish image (Line_04 left/right) named <c>Theme…</c>, placed with <see cref="BladeholdUIKit.Place" />.</summary>
    public static RectTransform Flourish(string name, Transform parent, bool left, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rt = BladeholdUIKit.Decoration(name, parent);
        BladeholdUIKit.Place(rt, anchor, new Vector2(left ? 1f : 0f, 0.5f), position, size);
        BladeholdUIKit.Img(rt, left ? BladeholdUIKit.LineLeft : BladeholdUIKit.LineRight, UIColorRole.AccentMuted).preserveAspect = true;
        return rt;
    }

    /// <summary>Finds a descendant by path, logging when it's missing (prefabs drift).</summary>
    public static Transform Need(Transform root, string path)
    {
        Transform t = root.Find(path);
        if (t == null) Debug.LogError($"[Restyle] '{root.name}' has no child '{path}'.");
        return t;
    }

    public static T Need<T>(Transform root, string path) where T : Component
    {
        Transform t = Need(root, path);
        return t != null ? t.GetComponent<T>() : null;
    }
}
