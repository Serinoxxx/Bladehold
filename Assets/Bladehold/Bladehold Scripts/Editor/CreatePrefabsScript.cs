using UnityEngine;
using UnityEditor;

public static class CreatePrefabsScript
{
    [MenuItem("Tools/Temp/Create Golems")]
    public static void CreateAndGenerate()
    {
        // 1. Create ArrowBarrageZone
        GameObject barrageObj = new GameObject("ArrowBarrageZone");
        var zone = barrageObj.AddComponent<ArrowBarrageZone>();
        
        // Load vfx_RoundMarker02_Red
        GameObject markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Third Party/GabrielAguiarProductions/UniqueMarkersPointersVol_1/Prefabs/Markers/vfx_RoundMarker02_Red.prefab");
        if (markerPrefab != null) {
            GameObject markerInstance = PrefabUtility.InstantiatePrefab(markerPrefab) as GameObject;
            markerInstance.transform.SetParent(barrageObj.transform);
            markerInstance.transform.localPosition = Vector3.zero;
        } else {
            Debug.LogError("Could not find vfx_RoundMarker02_Red.prefab");
        }
        
        PrefabUtility.SaveAsPrefabAsset(barrageObj, "Assets/Bladehold/Bladehold Prefabs/ArrowBarrageZone.prefab");
        GameObject.DestroyImmediate(barrageObj);

        // 2. Create BoulderProjectile
        GameObject boulderObj = new GameObject("BoulderProjectile");
        var proj = boulderObj.AddComponent<BoulderProjectile>(); // Requires Rigidbody
        
        var col = boulderObj.AddComponent<SphereCollider>(); 
        col.radius = 0.5f;
        
        // Load FeelRock mesh
        GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Third Party/Feel/FeelDemosHDRP/Falcon/Models/FeelRock.fbx");
        if (rockPrefab != null) {
            GameObject rockInstance = PrefabUtility.InstantiatePrefab(rockPrefab) as GameObject;
            rockInstance.transform.SetParent(boulderObj.transform);
            rockInstance.transform.localPosition = Vector3.zero;
        } else {
            Debug.LogError("Could not find FeelRock.fbx");
        }
        
        PrefabUtility.SaveAsPrefabAsset(boulderObj, "Assets/Bladehold/Bladehold Prefabs/BoulderProjectile.prefab");
        GameObject.DestroyImmediate(boulderObj);
        
        Debug.Log("Prefabs created.");
    }
}
