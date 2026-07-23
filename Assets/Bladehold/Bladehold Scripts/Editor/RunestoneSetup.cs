using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunestoneSetup
{
    [MenuItem("Tools/Setup Runestones and Element Nodes")]
    public static void Setup()
    {
        Debug.Log("Starting Runestone and Element Node Setup...");

        // Ensure materials directory exists
        string matDir = "Assets/Bladehold/Materials";
        if (!Directory.Exists(matDir))
        {
            Directory.CreateDirectory(matDir);
            AssetDatabase.Refresh();
        }

        // 1. Create Materials
        Material fireMat = CreateElementalMaterial("Mat_FireElement", new Color(1.0f, 0.25f, 0.0f), new Color(2.0f, 0.5f, 0.0f));
        Material iceMat = CreateElementalMaterial("Mat_IceElement", new Color(0.1f, 0.75f, 1.0f), new Color(0.2f, 1.5f, 2.0f));
        Material lightningMat = CreateElementalMaterial("Mat_LightningElement", new Color(1.0f, 0.85f, 0.1f), new Color(2.0f, 1.7f, 0.2f));
        Material stoneMat = CreateElementalMaterial("Mat_RunestoneBase", new Color(0.2f, 0.2f, 0.22f), Color.black);

        // 2. Create Element Node Prefabs
        ElementNode fireNodePrefab = CreateElementNodePrefab("FireElementNode", ElementType.Fire, fireMat, new Color(1.0f, 0.3f, 0.0f));
        ElementNode iceNodePrefab = CreateElementNodePrefab("IceElementNode", ElementType.Ice, iceMat, new Color(0.2f, 0.8f, 1.0f));
        ElementNode lightningNodePrefab = CreateElementNodePrefab("LightningElementNode", ElementType.Lightning, lightningMat, new Color(1.0f, 0.9f, 0.2f));

        // 3. Create Runestone Prefabs
        GameObject fireRunePrefab = CreateRunestonePrefab("RunestoneFire", ElementType.Fire, fireMat, stoneMat, new Color(1.0f, 0.3f, 0.0f), "FIRE RUNE");
        GameObject iceRunePrefab = CreateRunestonePrefab("RunestoneIce", ElementType.Ice, iceMat, stoneMat, new Color(0.2f, 0.8f, 1.0f), "ICE RUNE");
        GameObject lightningRunePrefab = CreateRunestonePrefab("RunestoneLightning", ElementType.Lightning, lightningMat, stoneMat, new Color(1.0f, 0.9f, 0.2f), "LIGHTNING RUNE");

        AssetDatabase.SaveAssets();

        // 4. Populate current scene (Bladehold Test Scene)
        var waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
        if (waveSpawner == null)
        {
            Debug.LogError("No WaveSpawner found in scene!");
            return;
        }

        // ElementNodeSpawner Setup
        var spawnerObj = GameObject.Find("ElementNodeSpawner");
        if (spawnerObj == null)
        {
            spawnerObj = new GameObject("ElementNodeSpawner");
            Undo.RegisterCreatedObjectUndo(spawnerObj, "Create ElementNodeSpawner");
        }

        ElementNodeSpawner nodeSpawner = spawnerObj.GetComponent<ElementNodeSpawner>();
        if (nodeSpawner == null)
        {
            nodeSpawner = spawnerObj.AddComponent<ElementNodeSpawner>();
        }

        SerializedObject serializedSpawner = new SerializedObject(nodeSpawner);
        serializedSpawner.FindProperty("spawner").objectReferenceValue = waveSpawner;

        SerializedProperty prefabsProp = serializedSpawner.FindProperty("nodePrefabs");
        prefabsProp.arraySize = 3;

        SetNodeEntry(prefabsProp.GetArrayElementAtIndex(0), fireNodePrefab, 1.0f);
        SetNodeEntry(prefabsProp.GetArrayElementAtIndex(1), iceNodePrefab, 1.0f);
        SetNodeEntry(prefabsProp.GetArrayElementAtIndex(2), lightningNodePrefab, 1.0f);

        serializedSpawner.ApplyModifiedProperties();
        Debug.Log("Wired ElementNodeSpawner in scene.");

        // Spawn Runestone instances in scene if not present
        SpawnRunestoneInScene("Runestone_Fire", fireRunePrefab, new Vector3(-8.0f, 0.0f, 8.0f));
        SpawnRunestoneInScene("Runestone_Ice", iceRunePrefab, new Vector3(0.0f, 0.0f, 11.0f));
        SpawnRunestoneInScene("Runestone_Lightning", lightningRunePrefab, new Vector3(8.0f, 0.0f, 8.0f));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("Runestone and Element Node Setup Complete!");
    }

    private static Material CreateElementalMaterial(string name, Color baseColor, Color emissionColor)
    {
        string path = $"Assets/Bladehold/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

        if (emissionColor != Color.black)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static ElementNode CreateElementNodePrefab(string name, ElementType element, Material mat, Color lightColor)
    {
        string prefabPath = $"Assets/Bladehold/Bladehold Prefabs/{name}.prefab";

        GameObject root = new GameObject(name);

        // Sphere collider trigger (added first to satisfy [RequireComponent(typeof(Collider))])
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.8f;

        root.AddComponent<ElementNode>();

        // Sphere Mesh
        GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        meshObj.name = "Mesh";
        meshObj.transform.SetParent(root.transform, false);
        meshObj.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

        Object.DestroyImmediate(meshObj.GetComponent<Collider>());
        meshObj.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Light
        GameObject lightObj = new GameObject("Light");
        lightObj.transform.SetParent(root.transform, false);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = 2.5f;
        light.range = 4f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        // Set Element via SerializedObject on saved prefab
        ElementNode nodeComp = prefab.GetComponent<ElementNode>();
        if (nodeComp != null)
        {
            SerializedObject so = new SerializedObject(nodeComp);
            SerializedProperty prop = so.FindProperty("element");
            if (prop != null)
            {
                prop.enumValueIndex = (int)element;
                so.ApplyModifiedProperties();
            }
        }

        return prefab.GetComponent<ElementNode>();
    }

    private static GameObject CreateRunestonePrefab(string name, ElementType element, Material gemMat, Material stoneMat, Color lightColor, string labelText)
    {
        string prefabPath = $"Assets/Bladehold/Bladehold Prefabs/{name}.prefab";

        GameObject root = new GameObject(name);

        // Solid collider for weapons to hit
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1.1f, 0f);
        col.radius = 0.6f;
        col.height = 2.2f;

        root.AddComponent<Runestone>();

        // Stone Base (Cylinder)
        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.name = "BasePillar";
        baseObj.transform.SetParent(root.transform, false);
        baseObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        baseObj.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);
        Object.DestroyImmediate(baseObj.GetComponent<Collider>());
        baseObj.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;

        // Floating Crystal Gem (Cube rotated 45 deg)
        GameObject gemObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gemObj.name = "CrystalGem";
        gemObj.transform.SetParent(root.transform, false);
        gemObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        gemObj.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
        gemObj.transform.localScale = new Vector3(0.5f, 0.7f, 0.5f);
        Object.DestroyImmediate(gemObj.GetComponent<Collider>());
        gemObj.GetComponent<MeshRenderer>().sharedMaterial = gemMat;

        // Point Light
        GameObject lightObj = new GameObject("RuneLight");
        lightObj.transform.SetParent(root.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = 3.5f;
        light.range = 6.0f;

        // Label (TextMeshPro)
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(root.transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 2.8f, 0f);
        labelObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Facing camera by default

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = labelText;
        tmp.fontSize = 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = lightColor;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        // Set Element via SerializedObject on saved prefab
        Runestone runeComp = prefab.GetComponent<Runestone>();
        if (runeComp != null)
        {
            SerializedObject so = new SerializedObject(runeComp);
            SerializedProperty prop = so.FindProperty("element");
            if (prop != null)
            {
                prop.enumValueIndex = (int)element;
                so.ApplyModifiedProperties();
            }
        }

        return prefab;
    }

    private static void SetNodeEntry(SerializedProperty entryProp, ElementNode prefab, float weight)
    {
        entryProp.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entryProp.FindPropertyRelative("weight").floatValue = weight;
    }

    private static void SpawnRunestoneInScene(string objectName, GameObject prefab, Vector3 position)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = objectName;
        instance.transform.position = position;
        Undo.RegisterCreatedObjectUndo(instance, $"Spawn {objectName}");
    }
}
