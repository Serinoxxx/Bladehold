using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Editor side of the UI theme system: creates the theme assets and the registry
///     (<c>Resources/UIThemeRegistry</c>), repaints every themed prefab and the open scene from the
///     current themes in Edit mode, and tags hand-coloured hierarchies (the settings panel) with
///     <see cref="UIThemedGraphic" />s by matching their colours to a theme's palette.
/// </summary>
public static class UIThemeTools
{
    public const string ThemeFolder = "Assets/Bladehold/UI Themes";
    private const string RegistryPath = "Assets/Bladehold/Resources/UIThemeRegistry.asset";
    private const string HeaderFontPath = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset";
    private const string BodyFontPath = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Grenze/Grenze-SemiBold SDF.asset";

    /// <summary>Creates any missing theme asset and the registry, wired to their menus. Never overwrites existing assets.</summary>
    [MenuItem("Bladehold/UI/Themes/Create Missing Theme Assets")]
    public static void EnsureThemeAssets()
    {
        if (!AssetDatabase.IsValidFolder(ThemeFolder))
        {
            AssetDatabase.CreateFolder("Assets/Bladehold", "UI Themes");
        }

        // Ember Gold is the settings panel's palette (UIThemeSO's field defaults); the others share its
        // structure and shift the hue so each place reads as its own while staying one family.
        UIThemeSO ember = EnsureTheme("UITheme_EmberGold", null);
        UIThemeSO spirit = EnsureTheme("UITheme_Spirit", t =>
        {
            t.window = Hex("0E1417F5");
            t.frame = Hex("7FB0BBE6");
            t.well = Hex("070C0ED9");
            t.accent = Hex("A9E4EE");
            t.accentMuted = Hex("6FA2AD");
            t.text = Hex("D5DFE0");
            t.textDim = Hex("8E9FA3");
            t.parchment = Hex("C7D8D9");
            t.textOnParchment = Hex("2D4147");
            t.ghost = Hex("1C282CCC");
            t.cost = Hex("F0C878");
        });
        UIThemeSO merchant = EnsureTheme("UITheme_Merchant", t =>
        {
            t.window = Hex("1B130CF5");
            t.frame = Hex("C38D52E6");
            t.well = Hex("0E0905D9");
            t.accent = Hex("FFC870");
            t.accentMuted = Hex("C68A4C");
            t.text = Hex("E4D7C2");
            t.textDim = Hex("A9957D");
            t.parchment = Hex("DEC59C");
            t.textOnParchment = Hex("4F3A27");
            t.ghost = Hex("2F2216CC");
            t.cost = Hex("FFC870");
        });
        UIThemeSO armoury = EnsureTheme("UITheme_Armoury", t =>
        {
            t.window = Hex("121518EB");
            t.frame = Hex("9DA8B2E6");
            t.well = Hex("0A0C0ED9");
            t.accent = Hex("F2DAA2");
            t.accentMuted = Hex("8F9BA5");
            t.text = Hex("D9DDE0");
            t.textDim = Hex("98A1A8");
            t.parchment = Hex("CBD1D5");
            t.textOnParchment = Hex("394048");
            t.ghost = Hex("22272CCC");
        });

        UIThemeRegistrySO registry = AssetDatabase.LoadAssetAtPath<UIThemeRegistrySO>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<UIThemeRegistrySO>();
            registry.globalTheme = ember;
            registry.menuOverrides = new List<UIThemeRegistrySO.MenuTheme>
            {
                new UIThemeRegistrySO.MenuTheme { menu = UIMenuId.MetaPerks, theme = spirit },
                new UIThemeRegistrySO.MenuTheme { menu = UIMenuId.Shop, theme = merchant },
                new UIThemeRegistrySO.MenuTheme { menu = UIMenuId.Pedestals, theme = armoury },
            };
            AssetDatabase.CreateAsset(registry, RegistryPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIThemeTools] Created {RegistryPath}.");
        }
    }

    private static UIThemeSO EnsureTheme(string name, System.Action<UIThemeSO> configure)
    {
        string path = $"{ThemeFolder}/{name}.asset";
        UIThemeSO theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(path);
        if (theme != null)
        {
            return theme;
        }
        theme = ScriptableObject.CreateInstance<UIThemeSO>();
        theme.headerFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeaderFontPath);
        theme.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        configure?.Invoke(theme);
        AssetDatabase.CreateAsset(theme, path);
        Debug.Log($"[UIThemeTools] Created {path}.");
        return theme;
    }

    /// <summary>
    ///     Repaints every prefab and the open scene from the current themes. Play mode repaints live on its
    ///     own; run this in Edit mode after editing a theme or the registry so saved prefabs match.
    /// </summary>
    [MenuItem("Bladehold/UI/Themes/Apply UI Themes (prefabs + open scene)")]
    public static void ApplyAll()
    {
        int prefabs = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Bladehold" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<UIThemedGraphic>(true) == null)
            {
                continue;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ApplyIn(root) > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                    if (!ok) Debug.LogError($"[UIThemeTools] Failed to save {path}.");
                    prefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        int sceneGraphics = 0;
        foreach (UIThemedGraphic themed in Object.FindObjectsByType<UIThemedGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(themed))
            {
                continue; // repainted through its prefab above.
            }
            Graphic graphic = themed.GetComponent<Graphic>();
            Undo.RecordObject(graphic, "Apply UI Theme");
            themed.Apply();
            PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
            sceneGraphics++;
        }
        if (sceneGraphics > 0)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        Debug.Log($"[UIThemeTools] Repainted {prefabs} prefabs and {sceneGraphics} scene graphics.");
    }

    private static int ApplyIn(GameObject root)
    {
        int count = 0;
        foreach (UIThemedGraphic themed in root.GetComponentsInChildren<UIThemedGraphic>(true))
        {
            themed.Apply();
            count++;
        }
        return count;
    }

    /// <summary>
    ///     Adds a <see cref="UIThemedGraphic" /> to every graphic under <paramref name="root" /> whose RGB
    ///     matches one of <paramref name="palette" />'s colours, so a hierarchy coloured from literal
    ///     palette values joins the theme system. Graphics animated by a component that sets their full
    ///     colour (row/tab highlights) are skipped: those components read roles themselves.
    /// </summary>
    public static int TagByPalette(Transform root, UIThemeSO palette)
    {
        var driven = new HashSet<Graphic>();
        foreach (SettingsTabButton tab in root.GetComponentsInChildren<SettingsTabButton>(true))
        {
            var so = new SerializedObject(tab);
            AddDriven(driven, so, "background");
            AddDriven(driven, so, "label");
        }
        foreach (SettingsRowHighlight row in root.GetComponentsInChildren<SettingsRowHighlight>(true))
        {
            AddDriven(driven, new SerializedObject(row), "label");
        }

        // Most specific roles first: when two roles share a colour the earlier one wins.
        UIColorRole[] order =
        {
            UIColorRole.Accent, UIColorRole.AccentMuted, UIColorRole.Text, UIColorRole.TextDim,
            UIColorRole.Parchment, UIColorRole.TextOnParchment, UIColorRole.Danger, UIColorRole.Success,
            UIColorRole.Cost, UIColorRole.Frame, UIColorRole.Window, UIColorRole.Well, UIColorRole.Ghost,
            UIColorRole.Dimmer
        };

        int tagged = 0;
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (driven.Contains(graphic) || graphic.GetComponent<UIThemedGraphic>() != null)
            {
                continue;
            }
            foreach (UIColorRole role in order)
            {
                if (SameRgb(graphic.color, palette.Get(role)))
                {
                    UIFontRole font = UIFontRole.Keep;
                    if (graphic is TMP_Text text)
                    {
                        font = text.font == palette.headerFont ? UIFontRole.Header : text.font == palette.bodyFont ? UIFontRole.Body : UIFontRole.Keep;
                    }
                    graphic.gameObject.AddComponent<UIThemedGraphic>().Configure(role, font);
                    tagged++;
                    break;
                }
            }
        }
        return tagged;
    }

    private static void AddDriven(HashSet<Graphic> set, SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && p.objectReferenceValue is Graphic g)
        {
            set.Add(g);
        }
    }

    private static bool SameRgb(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.006f && Mathf.Abs(a.g - b.g) < 0.006f && Mathf.Abs(a.b - b.b) < 0.006f;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
