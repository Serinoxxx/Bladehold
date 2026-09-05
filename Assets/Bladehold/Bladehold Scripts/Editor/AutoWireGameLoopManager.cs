using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DamageNumbersPro;

[InitializeOnLoad]
public static class AutoWireGameLoopManager
{
    static AutoWireGameLoopManager()
    {
        EditorApplication.delayCall += Wire;
    }

    [MenuItem("Bladehold/Wire GameLoopManager")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = EditorSceneManager.GetSceneByName("Bladehold Survivors Scene");
        if (!scene.isLoaded) return;

        var manager = Object.FindAnyObjectByType<GameLoopManager>();
        if (manager == null) return;

        bool changed = false;

        if (manager.goldPopupPrefab == null)
        {
            manager.goldPopupPrefab = AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Gold.prefab");
            if (manager.goldPopupPrefab != null) changed = true;
        }
        if (manager.metalPopupPrefab == null)
        {
            manager.metalPopupPrefab = AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Clear.prefab");
            if (manager.metalPopupPrefab != null) changed = true;
        }
        if (manager.bloodPopupPrefab == null)
        {
            manager.bloodPopupPrefab = AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Blood Text.prefab");
            if (manager.bloodPopupPrefab != null) changed = true;
        }
        if (manager.rewardSfx == null)
        {
            manager.rewardSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Fantasy_Game_Item_Organic_Coin_Collect_A.wav");
            if (manager.rewardSfx == null) manager.rewardSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Coins.wav");
            if (manager.rewardSfx != null) changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AutoWire] Wired GameLoopManager resource popups!");
        }
    }
}
