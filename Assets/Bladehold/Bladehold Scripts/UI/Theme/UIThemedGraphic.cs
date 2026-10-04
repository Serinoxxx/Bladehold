using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Paints a sibling <see cref="Graphic" /> (Image, TMP text, …) with a <see cref="UIColorRole" />
///     of its menu's theme, and optionally sets a TMP text's font. Only the colour's RGB is replaced:
///     the graphic's alpha stays as authored, because hover glows and row highlights fade it at
///     runtime. Repaints on enable and whenever <see cref="UITheme.Changed" /> fires, so editing a theme
///     asset in Play mode restyles open menus live. In Edit mode, use Bladehold > UI > Apply UI Themes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class UIThemedGraphic : MonoBehaviour
{
    [SerializeField] private UIColorRole role = UIColorRole.Text;
    [SerializeField] private UIFontRole font = UIFontRole.Keep;

    private Graphic graphic;

    public UIColorRole Role => role;

    public void Configure(UIColorRole colorRole, UIFontRole fontRole = UIFontRole.Keep)
    {
        role = colorRole;
        font = fontRole;
    }

    private void OnEnable()
    {
        UITheme.Changed -= Apply;
        UITheme.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        UITheme.Changed -= Apply;
    }

    /// <summary>Paints the graphic from its current theme.</summary>
    public void Apply()
    {
        Apply(UITheme.For(this));
    }

    public void Apply(UIThemeSO theme)
    {
        if (theme == null)
        {
            return;
        }
        if (graphic == null)
        {
            graphic = GetComponent<Graphic>();
        }
        if (graphic == null)
        {
            return;
        }

        Color c = theme.Get(role);
        c.a = graphic.color.a;
        graphic.color = c;

        if (font != UIFontRole.Keep && graphic is TMP_Text text)
        {
            TMP_FontAsset fontAsset = theme.GetFont(font);
            if (fontAsset != null && text.font != fontAsset)
            {
                text.font = fontAsset;
            }
        }
    }
}
