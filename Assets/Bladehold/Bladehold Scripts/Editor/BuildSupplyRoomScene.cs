#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
///     Automated builder for Part 2 of Castle Campaign: Bladehold Supply Room.
///     Constructs the non-hostile underground storehouse / vault scene:
///     - Indoor stone architecture (floors, walls, archways, pillars)
///     - 20 varied smashable supply crates, barrels, and metal boxes with SupplyBox & Health
///     - Player spawn point, CameraRig, Bladehold HUD, EventSystem, MMTimeManager
///     - Exit Gate / Door with Interactable returning to Castle Map
///     - SupplyRoomController managing crate count and player rehydration
///     - Baked NavMeshSurface
///     - Registers in EditorBuildSettings.
/// </summary>
public static class BuildSupplyRoomScene
{
    public const string ScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Supply Room.unity";

    [MenuItem("Bladehold/Build Supply Room Scene")]
    public static void Execute()
    {
        Debug.Log("[BuildSupplyRoomScene] Starting Supply Room scene construction...");

        // Ensure directory exists
        string dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // 1. Create a brand new scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Setup Lighting & Environment
        SetupEnvironmentAndLighting();

        // 3. Setup Architecture (Floor, Walls, Pillars, Archway)
        GameObject envRoot = new GameObject("Environment_Storehouse");
        SetupStorehouseGeometry(envRoot.transform);

        // 4. Setup Supply Containers (20 varied crates, barrels, metal boxes)
        GameObject supplyRoot = new GameObject("Supply_Containers");
        SetupSupplyContainers(supplyRoot.transform);

        // 5. Setup Exit Gate / Door with Interactable
        GameObject exitDoor = SetupExitDoor(envRoot.transform);

        // 6. Setup Player, CameraRig, HUD, EventSystem, MMTimeManager
        SetupGameplayPrefabs(scene);

        // 7. Setup SupplyRoomController
        GameObject controllerGo = new GameObject("SupplyRoomController");
        var controller = controllerGo.AddComponent<SupplyRoomController>();
        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("exitDoorInteractable").objectReferenceValue = exitDoor.GetComponent<Interactable>();
        controllerSo.FindProperty("exitPromptText").stringValue = "[E] Return to Castle Map";
        controllerSo.ApplyModifiedProperties();

        // 8. Setup & Bake NavMeshSurface
        SetupNavMesh(envRoot);

        // 9. Save Scene
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[BuildSupplyRoomScene] Scene saved successfully to {ScenePath}");

        // 10. Register in EditorBuildSettings
        RegisterInBuildSettings();

        Debug.Log("[BuildSupplyRoomScene] Supply Room scene setup complete!");
    }

    private static void SetupEnvironmentAndLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.16f, 0.22f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.015f;
        RenderSettings.fogColor = new Color(0.12f, 0.11f, 0.15f, 1f);

        // Warm ambient torch / overhead directional light
        GameObject dirLightGo = new GameObject("Directional Light (Vault Torches)");
        Light dirLight = dirLightGo.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1f, 0.85f, 0.65f, 1f);
        dirLight.intensity = 0.55f;
        dirLight.shadows = LightShadows.Soft;
        dirLightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Ambient Point Lights (Torches)
        CreateTorchLight("Torch_North", new Vector3(0f, 3.5f, 8f), new Color(1f, 0.6f, 0.2f), 1.8f, 12f);
        CreateTorchLight("Torch_South", new Vector3(0f, 3.5f, -8f), new Color(1f, 0.6f, 0.2f), 1.8f, 12f);
        CreateTorchLight("Torch_West", new Vector3(-8f, 3.5f, 0f), new Color(1f, 0.6f, 0.2f), 1.8f, 12f);
        CreateTorchLight("Torch_East", new Vector3(8f, 3.5f, 0f), new Color(1f, 0.6f, 0.2f), 1.8f, 12f);
    }

    private static void CreateTorchLight(string name, Vector3 pos, Color color, float intensity, float range)
    {
        GameObject torch = new GameObject(name);
        torch.transform.position = pos;
        Light light = torch.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    private static void SetupStorehouseGeometry(Transform parent)
    {
        // 1. Stone Floor (20x20m walkable area)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor_Stone_Vault";
        floor.transform.SetParent(parent);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(24f, 1f, 24f);
        ApplyMaterial(floor, "Assets/Synty/PolygonDungeon/Materials/Dungeon_Material_01.mat");

        // 2. Vault Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling_Stone_Vault";
        ceiling.transform.SetParent(parent);
        ceiling.transform.position = new Vector3(0f, 6.5f, 0f);
        ceiling.transform.localScale = new Vector3(24f, 1f, 24f);
        ApplyMaterial(ceiling, "Assets/Synty/PolygonDungeon/Materials/Dungeon_Material_01.mat");

        // 3. Perimeter Stone Walls (North, South, East, West)
        CreateWall(parent, "Wall_North", new Vector3(0f, 3f, 12f), new Vector3(24f, 6f, 1f));
        CreateWall(parent, "Wall_South", new Vector3(0f, 3f, -12f), new Vector3(24f, 6f, 1f));
        CreateWall(parent, "Wall_West", new Vector3(-12f, 3f, 0f), new Vector3(1f, 6f, 24f));
        CreateWall(parent, "Wall_East", new Vector3(12f, 3f, 0f), new Vector3(1f, 6f, 24f));

        // 4. Stately Vault Pillars
        CreatePillar(parent, "Pillar_NW", new Vector3(-5f, 3f, 5f));
        CreatePillar(parent, "Pillar_NE", new Vector3(5f, 3f, 5f));
        CreatePillar(parent, "Pillar_SW", new Vector3(-5f, 3f, -5f));
        CreatePillar(parent, "Pillar_SE", new Vector3(5f, 3f, -5f));
    }

    private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        ApplyMaterial(wall, "Assets/Synty/PolygonDungeon/Materials/Dungeon_Material_01.mat");
    }

    private static void CreatePillar(Transform parent, string name, Vector3 pos)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = name;
        pillar.transform.SetParent(parent);
        pillar.transform.position = pos;
        pillar.transform.localScale = new Vector3(1.4f, 3f, 1.4f);
        ApplyMaterial(pillar, "Assets/Synty/PolygonDungeon/Materials/Dungeon_Material_01.mat");
    }

    private static void ApplyMaterial(GameObject go, string matPath)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bladehold/Materials/Stone.mat");
        }
        if (mat != null)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;
        }
    }

    private static void SetupSupplyContainers(Transform parent)
    {
        // Roster of Synty crate & barrel prefabs
        string[] prefabPaths = new string[]
        {
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Wood_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Wood_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Wood_03.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Metal_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Metal_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Barrel_01.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Barrel_02.prefab",
            "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Barrel_Large_01.prefab"
        };

        // Shared Health config
        HealthSO crateHealthSo = AssetDatabase.LoadAssetAtPath<HealthSO>("Assets/Bladehold/Bladehold Scripts/Chests/LootChestHealth.asset");

        // 20 distinct spawn points clustered in corners and alcoves of the storehouse
        Vector3[] spawnPositions = new Vector3[]
        {
            // Northwest cluster
            new Vector3(-8.5f, 0f, 8.5f),
            new Vector3(-7.2f, 0f, 9.2f),
            new Vector3(-9.2f, 0f, 7.2f),
            new Vector3(-7.5f, 0f, 7.5f),
            new Vector3(-6.0f, 0f, 8.8f),

            // Northeast cluster
            new Vector3(8.5f, 0f, 8.5f),
            new Vector3(7.2f, 0f, 9.2f),
            new Vector3(9.2f, 0f, 7.2f),
            new Vector3(7.5f, 0f, 7.5f),
            new Vector3(6.0f, 0f, 8.8f),

            // Southwest cluster
            new Vector3(-8.5f, 0f, -8.5f),
            new Vector3(-7.2f, 0f, -9.2f),
            new Vector3(-9.2f, 0f, -7.2f),
            new Vector3(-7.5f, 0f, -7.5f),
            new Vector3(-6.0f, 0f, -8.8f),

            // Southeast cluster
            new Vector3(8.5f, 0f, -8.5f),
            new Vector3(7.2f, 0f, -9.2f),
            new Vector3(9.2f, 0f, -7.2f),
            new Vector3(7.5f, 0f, -7.5f),
            new Vector3(6.0f, 0f, -8.8f)
        };

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            Vector3 pos = spawnPositions[i];
            string prefabPath = prefabPaths[i % prefabPaths.Length];
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            GameObject crateGo;
            if (prefabAsset != null)
            {
                crateGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            }
            else
            {
                crateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crateGo.transform.localScale = Vector3.one * 1.2f;
            }

            crateGo.name = $"SupplyContainer_{i + 1:D2}";
            crateGo.transform.SetParent(parent);
            crateGo.transform.position = pos;
            crateGo.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            // Ensure Collider
            Collider col = crateGo.GetComponent<Collider>();
            if (col == null)
            {
                col = crateGo.AddComponent<BoxCollider>();
            }

            // Add Health
            Health health = crateGo.GetComponent<Health>();
            if (health == null)
            {
                health = crateGo.AddComponent<Health>();
            }
            SerializedObject hSo = new SerializedObject(health);
            if (crateHealthSo != null)
            {
                hSo.FindProperty("healthData").objectReferenceValue = crateHealthSo;
            }
            hSo.FindProperty("currentHealth").floatValue = 30f;
            hSo.ApplyModifiedProperties();

            // Add SupplyBox
            SupplyBox supplyBox = crateGo.GetComponent<SupplyBox>();
            if (supplyBox == null)
            {
                supplyBox = crateGo.AddComponent<SupplyBox>();
            }

            SerializedObject boxSo = new SerializedObject(supplyBox);
            boxSo.FindProperty("health").objectReferenceValue = health;
            boxSo.FindProperty("goldRange").vector2IntValue = new Vector2Int(15, 40);
            boxSo.FindProperty("supplyRange").vector2IntValue = new Vector2Int(10, 25);
            boxSo.FindProperty("bloodChance").floatValue = 0.40f;
            boxSo.FindProperty("bloodRange").vector2IntValue = new Vector2Int(1, 3);
            boxSo.FindProperty("metalChance").floatValue = 0.20f;
            boxSo.FindProperty("metalRange").vector2IntValue = new Vector2Int(1, 2);
            boxSo.FindProperty("destroyDelay").floatValue = 1.0f;
            boxSo.FindProperty("sinkOnDestroy").boolValue = true;

            GameObject impactVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Impact_Wood_01.prefab");
            if (impactVfx != null) boxSo.FindProperty("breakVfxPrefab").objectReferenceValue = impactVfx;

            AudioClip breakSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/Generic Wood Item Break A.wav");
            if (breakSound != null) boxSo.FindProperty("breakSfx").objectReferenceValue = breakSound;

            boxSo.ApplyModifiedProperties();
        }
    }

    private static GameObject SetupExitDoor(Transform parent)
    {
        // Archway / Doorway frame at North Wall
        GameObject doorGo = new GameObject("ExitGate_ToCampaignMap");
        doorGo.transform.SetParent(parent);
        doorGo.transform.position = new Vector3(0f, 0f, 11.2f);
        doorGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Visual frame
        GameObject doorFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorFrame.name = "Doorway_Arch";
        doorFrame.transform.SetParent(doorGo.transform, false);
        doorFrame.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        doorFrame.transform.localScale = new Vector3(3.2f, 3.6f, 0.6f);
        ApplyMaterial(doorFrame, "Assets/Synty/PolygonDungeon/Materials/Dungeon_Material_01.mat");

        // Trigger Collider
        BoxCollider boxCol = doorGo.AddComponent<BoxCollider>();
        boxCol.isTrigger = true;
        boxCol.size = new Vector3(4f, 4f, 4f);
        boxCol.center = new Vector3(0f, 2f, 0f);

        // Interactable Component
        Interactable interactable = doorGo.AddComponent<Interactable>();
        SerializedObject iSo = new SerializedObject(interactable);
        iSo.FindProperty("promptText").stringValue = "[E] Return to Castle Map";
        iSo.FindProperty("interactionRadius").floatValue = 4.0f;
        iSo.FindProperty("isInteractable").boolValue = true;
        iSo.ApplyModifiedProperties();

        // Atmospheric glowing lantern / portal glow above exit
        GameObject lightGo = new GameObject("ExitGlow");
        lightGo.transform.SetParent(doorGo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.5f, -0.5f);
        Light glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.3f, 0.8f, 1f);
        glow.intensity = 2.5f;
        glow.range = 7f;

        return doorGo;
    }

    private static void SetupGameplayPrefabs(Scene scene)
    {
        // 1. Player Prefab at Vault Center
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Player.prefab");
        if (playerPrefab != null)
        {
            GameObject playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            playerGo.name = "Player";
            playerGo.transform.position = new Vector3(0f, 0.1f, -4f);
            playerGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Debug.Log("[BuildSupplyRoomScene] Instantiated Player.prefab at (0, 0.1, -4)");
        }

        // 2. CameraRig Prefab
        GameObject cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Camera/CameraRig.prefab");
        if (cameraPrefab != null)
        {
            GameObject camGo = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
            camGo.name = "CameraRig";
            Debug.Log("[BuildSupplyRoomScene] Instantiated CameraRig.prefab");
        }

        // 3. Bladehold HUD Prefab
        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab");
        if (hudPrefab != null)
        {
            GameObject hudGo = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            hudGo.name = "Bladehold HUD";
            Debug.Log("[BuildSupplyRoomScene] Instantiated Bladehold HUD.prefab");
        }

        // 4. EventSystem Prefab
        GameObject eventSysPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/EventSystem.prefab");
        if (eventSysPrefab != null)
        {
            GameObject esGo = (GameObject)PrefabUtility.InstantiatePrefab(eventSysPrefab, scene);
            esGo.name = "EventSystem";
            Debug.Log("[BuildSupplyRoomScene] Instantiated EventSystem.prefab");
        }

        // 5. MMTimeManager Prefab
        GameObject mmTimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Managers/MMTimeManager.prefab");
        if (mmTimePrefab != null)
        {
            GameObject timeGo = (GameObject)PrefabUtility.InstantiatePrefab(mmTimePrefab, scene);
            timeGo.name = "MMTimeManager";
            Debug.Log("[BuildSupplyRoomScene] Instantiated MMTimeManager.prefab");
        }
    }

    private static void SetupNavMesh(GameObject envRoot)
    {
        NavMeshSurface surface = envRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Debug.Log("[BuildSupplyRoomScene] NavMeshSurface baked successfully!");
    }

    public static void RegisterInBuildSettings()
    {
        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < currentScenes.Length; i++)
        {
            if (currentScenes[i].path.Equals(ScenePath, System.StringComparison.OrdinalIgnoreCase))
            {
                currentScenes[i].enabled = true;
                EditorBuildSettings.scenes = currentScenes;
                Debug.Log("[BuildSupplyRoomScene] Scene already in EditorBuildSettings, enabled verified.");
                return;
            }
        }

        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
        System.Array.Copy(currentScenes, newScenes, currentScenes.Length);
        newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettings.scenes = newScenes;
        Debug.Log($"[BuildSupplyRoomScene] Added {ScenePath} to EditorBuildSettings!");
    }
}
#endif
