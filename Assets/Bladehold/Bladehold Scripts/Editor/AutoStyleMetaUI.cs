using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class AutoStyleMetaUI
{
    static AutoStyleMetaUI()
    {
        EditorApplication.delayCall += ApplyStyle;
    }

    [MenuItem("Bladehold/Style Meta UI")]
    public static void ApplyStyle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = EditorSceneManager.GetSceneByName("Bladehold Meta Area Scene");
        if (!scene.isLoaded) return;

        var ui = Object.FindAnyObjectByType<MetaUpgradesUI>();
        if (ui == null) return;

        bool changed = false;

        var windowRoot = ui.transform.Find("WindowRoot");
        if (windowRoot != null)
        {
            var bgImage = windowRoot.GetComponent<Image>();
            if (bgImage == null) bgImage = windowRoot.gameObject.AddComponent<Image>();
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Large_Parchment_01.png");
            if (bgSprite != null) {
                bgImage.sprite = bgSprite;
                bgImage.type = Image.Type.Sliced;
            }
            ColorUtility.TryParseHtmlString("#C5BEA8", out Color parchmentTint);
            bgImage.color = parchmentTint;
            changed = true;
        }

        // Apply Font to all texts
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
        ColorUtility.TryParseHtmlString("#63564B", out Color brownText);

        foreach (var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            if (font != null && text.font != font)
            {
                text.font = font;
                text.color = brownText;
                changed = true;
            }
        }

        // Style buttons
        var buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Small_Parchment_01.png");
        foreach (var btn in ui.GetComponentsInChildren<Button>(true))
        {
            var img = btn.GetComponent<Image>();
            if (img != null && buttonSprite != null && img.sprite != buttonSprite)
            {
                img.sprite = buttonSprite;
                img.type = Image.Type.Sliced;
                ColorUtility.TryParseHtmlString("#D4C6A3", out Color tint);
                img.color = tint;
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(ui.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AutoStyleMetaUI] Applied Bladehold design system to MetaUpgradesUI!");
        }
    }
}
