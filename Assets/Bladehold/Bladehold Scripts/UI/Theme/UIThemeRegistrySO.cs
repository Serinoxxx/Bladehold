using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The menus that can carry their own theme. Add a value when a new screen wants one.</summary>
public enum UIMenuId
{
    /// <summary>Anything that doesn't name a menu: uses the global theme.</summary>
    Global,
    Settings,
    PauseMenu,
    MainMenu,
    Shop,
    MetaPerks,
    Pedestals
}

/// <summary>
///     The one place UI colour schemes are chosen: a global theme for the whole game plus optional
///     per-menu overrides. Lives at <c>Resources/UIThemeRegistry</c>. To restyle everything, change
///     <see cref="globalTheme" /> (or untick <see cref="useMenuOverrides" /> to put every menu on it);
///     to restyle one menu, point its override at another <see cref="UIThemeSO" />.
/// </summary>
[CreateAssetMenu(fileName = "UIThemeRegistry", menuName = "Scriptable Objects/UI/UI Theme Registry")]
public class UIThemeRegistrySO : ScriptableObject
{
    [Serializable]
    public struct MenuTheme
    {
        public UIMenuId menu;
        public UIThemeSO theme;
    }

    [Tooltip("Used by every menu without an override, and by all menus when overrides are off.")]
    public UIThemeSO globalTheme;

    [Tooltip("Off = every menu uses the global theme (handy for checking one scheme everywhere).")]
    public bool useMenuOverrides = true;

    public List<MenuTheme> menuOverrides = new List<MenuTheme>();

    public UIThemeSO ThemeFor(UIMenuId menu)
    {
        if (useMenuOverrides && menu != UIMenuId.Global)
        {
            foreach (MenuTheme entry in menuOverrides)
            {
                if (entry.menu == menu && entry.theme != null)
                {
                    return entry.theme;
                }
            }
        }
        return globalTheme;
    }

    private void OnValidate()
    {
        UITheme.NotifyChanged();
    }
}
