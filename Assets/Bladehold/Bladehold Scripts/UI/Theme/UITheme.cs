using System;
using UnityEngine;

/// <summary>
///     Static access to UI themes. Resolves a <see cref="UIThemeSO" /> for a menu (through the
///     <see cref="UIThemeRegistrySO" /> in Resources) or for a transform (through its nearest
///     <see cref="UIThemeScope" />), and raises <see cref="Changed" /> whenever a theme or the registry
///     changes so themed UI repaints. Runtime code that colours state (can't afford, equipped, …) asks
///     here instead of hardcoding colours.
/// </summary>
public static class UITheme
{
    private const string RegistryResourcePath = "UIThemeRegistry";

    private static UIThemeRegistrySO registry;
    private static UIThemeSO fallback;
    private static UIThemeSO runtimeOverride;

    /// <summary>Raised when a theme asset, the registry, or a runtime override changes.</summary>
    public static event Action Changed;

    public static UIThemeRegistrySO Registry
    {
        get
        {
            if (registry == null)
            {
                registry = Resources.Load<UIThemeRegistrySO>(RegistryResourcePath);
            }
            return registry;
        }
    }

    /// <summary>The theme for <paramref name="menu" />. Never null: falls back to the built-in default colours.</summary>
    public static UIThemeSO For(UIMenuId menu)
    {
        UIThemeSO theme = runtimeOverride != null ? runtimeOverride : Registry != null ? Registry.ThemeFor(menu) : null;
        return theme != null ? theme : Fallback;
    }

    /// <summary>The theme of the nearest <see cref="UIThemeScope" /> above <paramref name="target" />, else the global theme.</summary>
    public static UIThemeSO For(Component target)
    {
        UIThemeScope scope = target != null ? target.GetComponentInParent<UIThemeScope>(true) : null;
        return scope != null ? scope.Theme : For(UIMenuId.Global);
    }

    /// <summary>
    ///     Puts every menu on <paramref name="theme" /> at runtime (e.g. a high-contrast option) and repaints;
    ///     null goes back to the registry. Doesn't touch the registry asset. Scopes with their own
    ///     theme override keep it.
    /// </summary>
    public static void SetRuntimeOverride(UIThemeSO theme)
    {
        runtimeOverride = theme;
        NotifyChanged();
    }

    public static void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private static UIThemeSO Fallback
    {
        get
        {
            if (fallback == null)
            {
                fallback = ScriptableObject.CreateInstance<UIThemeSO>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
            }
            return fallback;
        }
    }
}
