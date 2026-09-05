using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AutoWireHUD
{
    static AutoWireHUD()
    {
        EditorApplication.delayCall += WireHUD;
    }

    [MenuItem("Bladehold/Wire HUD Currencies")]
    public static void WireHUD()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scenes = new string[] { "Bladehold Survivors Scene", "Bladehold Rest Area Scene", "Bladehold Meta Area Scene" };

        foreach (var sceneName in scenes)
        {
            var scene = EditorSceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) continue;

            var coinUI = Object.FindAnyObjectByType<CoinUI>();
            if (coinUI == null) continue;

            var parent = coinUI.transform.parent;

            // Make sure parent is a Horizontal or Vertical Layout group, otherwise they'll overlap.
            // If they are manually anchored, we can just space them out.
            var group = parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() ?? parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() as UnityEngine.UI.LayoutGroup;
            if (group == null)
            {
                group = parent.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                ((UnityEngine.UI.HorizontalLayoutGroup)group).spacing = 50f;
                ((UnityEngine.UI.HorizontalLayoutGroup)group).childControlWidth = false;
                ((UnityEngine.UI.HorizontalLayoutGroup)group).childControlHeight = false;
            }

            bool changed = false;
            var bloodUI = parent.Find("BloodUI");
            if (bloodUI == null)
            {
                var inst = Object.Instantiate(coinUI.gameObject, parent);
                inst.name = "BloodUI";
                var bloodCoin = inst.GetComponent<CoinUI>();
                Object.DestroyImmediate(bloodCoin); // Replace with BloodUI
                var bloodScript = inst.AddComponent<GoblinBloodUI>();
                bloodScript.label = inst.GetComponent<TMPro.TMP_Text>();
                changed = true;
            }

            var metalUI = parent.Find("MetalUI");
            if (metalUI == null)
            {
                var inst = Object.Instantiate(coinUI.gameObject, parent);
                inst.name = "MetalUI";
                var metalCoin = inst.GetComponent<CoinUI>();
                Object.DestroyImmediate(metalCoin);
                var metalScript = inst.AddComponent<OrcishMetalUI>();
                metalScript.label = inst.GetComponent<TMPro.TMP_Text>();
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AutoWireHUD] Wired currencies for {sceneName}");
            }
        }
    }
}
