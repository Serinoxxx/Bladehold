using System;
using TMPro;
using UnityEngine;

/// <summary>
///     Colour token a themed graphic paints with. Builders and runtime UI pick a role ("accent",
///     "text on dark") instead of a literal colour, so a whole menu restyles by swapping its
///     <see cref="UIThemeSO" />.
/// </summary>
public enum UIColorRole
{
    /// <summary>Full-screen modal backdrop.</summary>
    Dimmer,
    /// <summary>Main window fill.</summary>
    Window,
    /// <summary>The window's outer frame.</summary>
    Frame,
    /// <summary>Recessed panels inside a window (lists, details, cards).</summary>
    Well,
    /// <summary>Titles, selection, hover glow, underlines.</summary>
    Accent,
    /// <summary>Flourishes, dividers, control frames.</summary>
    AccentMuted,
    /// <summary>Body text on dark panels.</summary>
    Text,
    /// <summary>Secondary text on dark panels.</summary>
    TextDim,
    /// <summary>Light fill for primary buttons and the selected tab.</summary>
    Parchment,
    /// <summary>Text drawn on <see cref="Parchment" />.</summary>
    TextOnParchment,
    /// <summary>Dark fill for secondary buttons and unselected tabs.</summary>
    Ghost,
    /// <summary>Prices and currency amounts.</summary>
    Cost,
    /// <summary>Destructive actions, can't-afford, damage.</summary>
    Danger,
    /// <summary>Owned, equipped, maxed.</summary>
    Success
}

/// <summary>Which of the theme's fonts a themed text uses.</summary>
public enum UIFontRole
{
    /// <summary>Leave the text's font alone.</summary>
    Keep,
    /// <summary>Titles, section headers, buttons (Texturina).</summary>
    Header,
    /// <summary>Everything else (Grenze).</summary>
    Body
}

/// <summary>
///     One UI colour scheme: a colour per <see cref="UIColorRole" /> plus the header/body fonts. Every
///     menu reads its theme through <see cref="UITheme" />, which maps menus to themes via the
///     <see cref="UIThemeRegistrySO" />. Editing a theme asset in Play mode restyles open UI live.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "Scriptable Objects/UI/UI Theme")]
public class UIThemeSO : ScriptableObject
{
    [Header("Surfaces")]
    public Color dimmer = Hex("0D0D14B8");
    public Color window = Hex("16120EF5");
    public Color frame = Hex("B8925AE6");
    public Color well = Hex("0B0907D9");

    [Header("Accents")]
    public Color accent = Hex("FFD170");
    public Color accentMuted = Hex("C9A35E");

    [Header("Text")]
    public Color text = Hex("D9D1BF");
    public Color textDim = Hex("A3978A");

    [Header("Buttons and tabs")]
    public Color parchment = Hex("D4C6A3");
    public Color textOnParchment = Hex("54483D");
    public Color ghost = Hex("2A241ECC");

    [Header("States")]
    public Color cost = Hex("FFB648");
    public Color danger = Hex("A45845");
    public Color success = Hex("8FC27A");

    [Header("Fonts")]
    public TMP_FontAsset headerFont;
    public TMP_FontAsset bodyFont;

    public Color Get(UIColorRole role)
    {
        switch (role)
        {
            case UIColorRole.Dimmer: return dimmer;
            case UIColorRole.Window: return window;
            case UIColorRole.Frame: return frame;
            case UIColorRole.Well: return well;
            case UIColorRole.Accent: return accent;
            case UIColorRole.AccentMuted: return accentMuted;
            case UIColorRole.Text: return text;
            case UIColorRole.TextDim: return textDim;
            case UIColorRole.Parchment: return parchment;
            case UIColorRole.TextOnParchment: return textOnParchment;
            case UIColorRole.Ghost: return ghost;
            case UIColorRole.Cost: return cost;
            case UIColorRole.Danger: return danger;
            case UIColorRole.Success: return success;
            default: throw new ArgumentOutOfRangeException(nameof(role), role, null);
        }
    }

    /// <summary>The role's colour with its alpha multiplied by <paramref name="alpha" />.</summary>
    public Color Get(UIColorRole role, float alpha)
    {
        Color c = Get(role);
        c.a *= alpha;
        return c;
    }

    public TMP_FontAsset GetFont(UIFontRole role)
    {
        return role == UIFontRole.Header ? headerFont : role == UIFontRole.Body ? bodyFont : null;
    }

    /// <summary>"#RRGGBB" for rich-text tags, e.g. <c>&lt;color=#FFD170&gt;</c>.</summary>
    public string HexOf(UIColorRole role) => "#" + ColorUtility.ToHtmlStringRGB(Get(role));

    private void OnValidate()
    {
        UITheme.NotifyChanged();
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
