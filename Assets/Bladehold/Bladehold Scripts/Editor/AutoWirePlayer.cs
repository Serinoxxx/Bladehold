using UnityEditor;
using UnityEngine;
using System.IO;

public static class AutoWirePlayer
{
    [MenuItem("Tools/Auto Wire Player")]
    public static void Wire()
    {
        string dir = "Assets/Bladehold/Config/Armour Sets";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string path = dir + "/HeroArmourSet.asset";
        var armourSO = AssetDatabase.LoadAssetAtPath<ArmourSetSO>(path);
        if (armourSO == null)
        {
            armourSO = ScriptableObject.CreateInstance<ArmourSetSO>();
            AssetDatabase.CreateAsset(armourSO, path);
        }

        armourSO.id = "hero";
        armourSO.displayName = "Hero Armour";
        armourSO.characterModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Third Party/Synty/AnimationBaseLocomotion/Samples/Prefabs/PF_SidekickPlayer.prefab");
        EditorUtility.SetDirty(armourSO);
        AssetDatabase.SaveAssets();

        string prefabPath = "Assets/Bladehold/Bladehold Prefabs/Player.prefab";
        using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            var prefab = editScope.prefabContentsRoot;
            
            var pam = prefab.GetComponent<PlayerArmourManager>();
            if (pam == null)
            {
                pam = prefab.AddComponent<PlayerArmourManager>();
                Debug.Log("Added PlayerArmourManager to Player prefab");
            }

            if (pam != null)
            {
                pam.availableArmourSets = new ArmourSetSO[] { armourSO };
                Debug.Log("Wired availableArmourSets");
            }
        }
        
        Debug.Log("Player prefab auto-wiring complete!");
    }
}
