using UnityEngine;

/// <summary>
///     Marks a menu's root: every <see cref="UIThemedGraphic" /> and theme-aware component below it
///     paints with this menu's theme (looked up in the <see cref="UIThemeRegistrySO" /> by
///     <see cref="menu" />), unless <see cref="themeOverride" /> pins a specific theme.
/// </summary>
public class UIThemeScope : MonoBehaviour
{
    [SerializeField] private UIMenuId menu = UIMenuId.Global;
    [Tooltip("Optional: use this theme here regardless of the registry.")]
    [SerializeField] private UIThemeSO themeOverride;

    public UIMenuId Menu => menu;

    public UIThemeSO Theme => themeOverride != null ? themeOverride : UITheme.For(menu);

    public void Configure(UIMenuId menuId)
    {
        menu = menuId;
    }

    private void OnValidate()
    {
        UITheme.NotifyChanged();
    }
}
