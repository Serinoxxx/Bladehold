#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
///     Automated Scene Generator & Level Builder for Part 5 of the Castle Campaign Overhaul:
///     Constructs "Bladehold Princess Sanctuary.unity" (Tier 8 Royal Sanctuary Encounter).
///     Features:
///     - Regal royal sanctuary hall: ornate elevated throne dais, royal colonnades, velvet carpets, chandeliers
///     - Warm golden sunlight streaming in with soft atmospheric sanctuary fog
///     - Princess Boss GameObject with PrincessBossController, Health, NavMeshAgent, and 5s revival spell
///     - 4 Armored Knights with ArmoredKnightAI, Health, NavMeshAgent, Sword & Shield, and downed soul beacons
///     - Fully integrated Player, CameraRig, HUD, EventSystem, MMTimeManager, GameLoopManager, and baked NavMeshSurface
/// </summary>
public static class BuildPrincessSanctuaryScene
{
    public const string ScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Princess Sanctuary.unity";

    [MenuItem("Bladehold/Build Castle Levels/9. Princess Sanctuary Scene", priority = 29)]
    public static void BuildScene()
    {
        Debug.Log("[BuildPrincessSanctuaryScene] === Building Bladehold Princess Sanctuary Scene ===");

        string dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Sanctuary Atmosphere
        SetupAtmosphere(
            ambientColor: new Color(0.32f, 0.28f, 0.35f),
            fogColor: new Color(0.38f, 0.34f, 0.40f),
            fogDensity: 0.012f,
            sunColor: new Color(1.0f, 0.92f, 0.78f),
            sunIntensity: 1.15f,
            sunRot: Quaternion.Euler(50f, 40f, 0f)
        );

        // 2. Environment Root
        GameObject envRoot = new GameObject("Environment_Princess_Sanctuary");
        Material floorMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_A_Emmisive.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");

        // Ground Floor (44m wide x 64m long)
        CreateGround(envRoot.transform, 44f, 64f, floorMat);

        // Perimeter Stone Enclosure (44m x 64m, 9m tall, 2.5m thick)
        CreatePerimeterEnclosure(envRoot.transform, 44f, 64f, 9f, 2.5f, wallMat);

        // Royal Colonnades along both aisles (X = -12m and +12m)
        for (float z = -22f; z <= 22f; z += 11f)
        {
            CreateStoneColumn(envRoot.transform, new Vector3(-12f, 0f, z), new Vector3(2.8f, 8.5f, 2.8f), wallMat);
            CreateStoneColumn(envRoot.transform, new Vector3(12f, 0f, z), new Vector3(2.8f, 8.5f, 2.8f), wallMat);

            // Ceiling cross-beams connecting columns to side walls
            CreateWallSection($"Ceiling_Beam_L_{z}", new Vector3(-17f, 8.2f, z), new Vector3(10f, 1.2f, 2.0f), envRoot.transform, wallMat);
            CreateWallSection($"Ceiling_Beam_R_{z}", new Vector3(17f, 8.2f, z), new Vector3(10f, 1.2f, 2.0f), envRoot.transform, wallMat);
        }

        // Royal Chandeliers hanging along center aisle
        PlaceChandeliers(envRoot.transform);

        // Velvet Carpets along the central nave
        PlaceRoyalCarpets(envRoot.transform);

        // Central Raised Throne Dais at Z = 16m
        GameObject throneDais = GameObject.CreatePrimitive(PrimitiveType.Cube);
        throneDais.name = "Throne_Dais_Platform";
        throneDais.transform.SetParent(envRoot.transform);
        throneDais.transform.position = new Vector3(0f, 0.5f, 16f);
        throneDais.transform.localScale = new Vector3(14f, 1.0f, 10f);
        if (floorMat != null) throneDais.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Steps leading up to Throne Dais
        GameObject daisSteps = GameObject.CreatePrimitive(PrimitiveType.Cube);
        daisSteps.name = "Throne_Dais_Steps";
        daisSteps.transform.SetParent(envRoot.transform);
        daisSteps.transform.position = new Vector3(0f, 0.25f, 10.5f);
        daisSteps.transform.localScale = new Vector3(8f, 0.5f, 2f);
        if (wallMat != null) daisSteps.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Place Ornate Throne
        PlaceThrone(throneDais.transform);

        // 3. Bake NavMeshSurface
        BakeNavMesh(envRoot);

        // 4. Instantiate Standard Gameplay Prefabs (Player, CameraRig, HUD, EventSystem, MMTimeManager)
        GameObject playerGo = SetupPlayer(new Vector3(0f, 0f, -20f), scene);
        GameObject cameraRigGo = EnsurePrefabInstance("CameraRig", "Assets/Bladehold/Bladehold Prefabs/Camera/CameraRig.prefab", scene);
        GameObject hudGo = EnsurePrefabInstance("Bladehold HUD", "Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab", scene);
        GameObject eventSystemGo = EnsurePrefabInstance("EventSystem", "Assets/Bladehold/Bladehold Prefabs/UI/EventSystem.prefab", scene);
        GameObject mmTimeMgrGo = EnsurePrefabInstance("MMTimeManager", "Assets/Bladehold/Bladehold Prefabs/Managers/MMTimeManager.prefab", scene);
        GameObject gameLoopGo = EnsurePrefabInstance("GameLoopManager", "Assets/Bladehold/Bladehold Prefabs/Managers/GameLoopManager.prefab", scene);

        // 5. Setup 4 Armored Knights
        List<ArmoredKnightAI> knights = SetupArmoredKnights(scene);

        // 6. Setup Princess Boss GameObject
        GameObject princessGo = SetupPrincessBoss(new Vector3(0f, 1.0f, 14f), knights, scene);

        // 7. Register in EditorBuildSettings
        RegisterSceneInBuildSettings(ScenePath);

        // 8. Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BuildPrincessSanctuaryScene] === Bladehold Princess Sanctuary Successfully Created at {ScenePath}! ===");
    }

    private static void SetupAtmosphere(Color ambientColor, Color fogColor, float fogDensity, Color sunColor, float sunIntensity, Quaternion sunRot)
    {
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogColor = fogColor;

        GameObject dirLightGo = new GameObject("Directional Light (Sanctuary Sun)");
        Light light = dirLightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = sunColor;
        light.intensity = sunIntensity;
        light.shadows = LightShadows.Soft;
        dirLightGo.transform.rotation = sunRot;
    }

    private static GameObject CreateGround(Transform parent, float width, float length, Material mat)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor_Sanctuary_Stone";
        floor.transform.SetParent(parent);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(width, 1f, length);
        if (mat != null) floor.GetComponent<Renderer>().sharedMaterial = mat;
        return floor;
    }

    private static void CreatePerimeterEnclosure(Transform parent, float width, float length, float height, float thickness, Material mat)
    {
        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        CreateWallSection("Wall_South", new Vector3(0f, height * 0.5f, -halfL), new Vector3(width, height, thickness), parent, mat);
        CreateWallSection("Wall_North", new Vector3(0f, height * 0.5f, halfL), new Vector3(width, height, thickness), parent, mat);
        CreateWallSection("Wall_West", new Vector3(-halfW, height * 0.5f, 0f), new Vector3(thickness, height, length), parent, mat);
        CreateWallSection("Wall_East", new Vector3(halfW, height * 0.5f, 0f), new Vector3(thickness, height, length), parent, mat);
    }

    private static GameObject CreateWallSection(string name, Vector3 pos, Vector3 size, Transform parent, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position = pos;
        wall.transform.localScale = size;
        if (mat != null) wall.GetComponent<Renderer>().sharedMaterial = mat;
        return wall;
    }

    private static GameObject CreateStoneColumn(Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cube);
        col.name = "Stone_Pillar";
        col.transform.SetParent(parent);
        col.transform.position = pos + new Vector3(0f, size.y * 0.5f, 0f);
        col.transform.localScale = size;
        if (mat != null) col.GetComponent<Renderer>().sharedMaterial = mat;
        return col;
    }

    private static void PlaceThrone(Transform dais)
    {
        string thronePrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/Furniture/SM_Prop_Throne_01.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(thronePrefab);
        if (asset == null)
        {
            asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/Furniture/SM_Prop_Throne_02.prefab");
        }

        if (asset != null)
        {
            GameObject throne = (GameObject)PrefabUtility.InstantiatePrefab(asset, dais);
            throne.transform.localPosition = new Vector3(0f, 0.5f, 2.5f);
            throne.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            throne.transform.localScale = Vector3.one * 1.3f;
        }
        else
        {
            GameObject throne = GameObject.CreatePrimitive(PrimitiveType.Cube);
            throne.name = "Procedural_Throne";
            throne.transform.SetParent(dais);
            throne.transform.localPosition = new Vector3(0f, 1.2f, 2.5f);
            throne.transform.localScale = new Vector3(2.5f, 2.5f, 1.5f);
        }
    }

    private static void PlaceRoyalCarpets(Transform parent)
    {
        string rugPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Rug_01.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(rugPrefab);

        float[] zPositions = new float[] { -15f, -8f, -1f, 6f, 13f };
        for (int i = 0; i < zPositions.Length; i++)
        {
            Vector3 pos = new Vector3(0f, 0.02f, zPositions[i]);
            if (asset != null)
            {
                GameObject rug = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                rug.transform.position = pos;
                rug.transform.localScale = new Vector3(2.2f, 1f, 3.2f);
            }
            else
            {
                GameObject rug = GameObject.CreatePrimitive(PrimitiveType.Quad);
                rug.name = $"Carpet_{i}";
                rug.transform.SetParent(parent);
                rug.transform.position = pos;
                rug.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                rug.transform.localScale = new Vector3(4f, 6f, 1f);

                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(s);
                mat.color = new Color(0.65f, 0.12f, 0.15f);
                rug.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
    }

    private static void PlaceChandeliers(Transform parent)
    {
        string chPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Chandelier_01.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(chPrefab);

        float[] zPositions = new float[] { -14f, 0f, 14f };
        for (int i = 0; i < zPositions.Length; i++)
        {
            Vector3 pos = new Vector3(0f, 7.5f, zPositions[i]);
            GameObject chGo = null;
            if (asset != null)
            {
                chGo = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                chGo.transform.position = pos;
            }
            else
            {
                chGo = new GameObject($"Chandelier_{i}");
                chGo.transform.SetParent(parent);
                chGo.transform.position = pos;
            }

            Light light = chGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.88f, 0.65f);
            light.intensity = 2.8f;
            light.range = 16f;
            light.shadows = LightShadows.Soft;
        }
    }

    private static void BakeNavMesh(GameObject envRoot)
    {
        NavMeshSurface surface = envRoot.GetComponent<NavMeshSurface>() ?? envRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Debug.Log("[BuildPrincessSanctuaryScene] NavMesh baked successfully for Princess Sanctuary!");
    }

    private static GameObject SetupPlayer(Vector3 spawnPos, Scene scene)
    {
        string playerPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[BuildPrincessSanctuaryScene] Player.prefab not found!");
            return null;
        }

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        player.name = "Player";
        player.transform.position = spawnPos;
        player.transform.rotation = Quaternion.identity;
        return player;
    }

    private static List<ArmoredKnightAI> SetupArmoredKnights(Scene scene)
    {
        List<ArmoredKnightAI> list = new List<ArmoredKnightAI>();

        string knightModelPath = "Assets/Synty/PolygonDungeon/Prefabs/Characters/SM_Chr_Hero_Knight_Male_01.prefab";
        GameObject knightAsset = AssetDatabase.LoadAssetAtPath<GameObject>(knightModelPath);

        string swordPrefabPath = "Assets/Synty/Weapons/SM_Wep_Sword_01/SM_Wep_Sword_01.prefab";
        string shieldPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Wep_Shield_01.prefab";
        GameObject swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(swordPrefabPath);
        GameObject shieldAsset = AssetDatabase.LoadAssetAtPath<GameObject>(shieldPrefabPath);

        RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Bladehold/Bladehold Animations/SimplifiedEnemyAC.controller");
        HealthSO healthSo = AssetDatabase.LoadAssetAtPath<HealthSO>("Assets/Bladehold/Bladehold Scripts/DamageSystem/HealthSO.asset");

        Vector3[] spawnPositions = new Vector3[]
        {
            new Vector3(-4.5f, 0f, 8f),
            new Vector3(4.5f, 0f, 8f),
            new Vector3(-6.5f, 0f, -2f),
            new Vector3(6.5f, 0f, -2f)
        };

        string[] knightNames = new string[]
        {
            "ArmoredKnight_HonorGuard_Left",
            "ArmoredKnight_HonorGuard_Right",
            "ArmoredKnight_Vanguard_Left",
            "ArmoredKnight_Vanguard_Right"
        };

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            Vector3 pos = spawnPositions[i];
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            GameObject knightGo = null;
            if (knightAsset != null)
            {
                knightGo = UnityEngine.Object.Instantiate(knightAsset);
                SceneManager.MoveGameObjectToScene(knightGo, scene);
            }
            else
            {
                knightGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            }

            knightGo.name = knightNames[i];
            knightGo.transform.position = pos;
            knightGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Collider
            CapsuleCollider col = knightGo.GetComponent<CapsuleCollider>();
            if (col == null) col = knightGo.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.0f, 0f);
            col.height = 2.0f;
            col.radius = 0.5f;

            // Health
            Health h = knightGo.GetComponent<Health>();
            if (h == null) h = knightGo.AddComponent<Health>();
            var hSo = new SerializedObject(h);
            if (healthSo != null) hSo.FindProperty("healthData").objectReferenceValue = healthSo;
            hSo.FindProperty("currentHealth").floatValue = 180f;
            hSo.ApplyModifiedProperties();

            // NavMeshAgent
            NavMeshAgent agent = knightGo.GetComponent<NavMeshAgent>();
            if (agent == null) agent = knightGo.AddComponent<NavMeshAgent>();
            agent.speed = 4.2f;
            agent.stoppingDistance = 2.0f;
            agent.radius = 0.5f;
            agent.height = 2.0f;

            // Animator
            Animator anim = knightGo.GetComponentInChildren<Animator>();
            if (anim != null && animController != null)
            {
                anim.runtimeAnimatorController = animController;
            }

            // ArmoredKnightAI
            ArmoredKnightAI knightAi = knightGo.GetComponent<ArmoredKnightAI>();
            if (knightAi == null) knightAi = knightGo.AddComponent<ArmoredKnightAI>();

            var aiSo = new SerializedObject(knightAi);
            aiSo.FindProperty("attackDamage").floatValue = 22f;
            aiSo.FindProperty("attackRange").floatValue = 2.2f;
            aiSo.FindProperty("attackCooldown").floatValue = 2.0f;
            aiSo.FindProperty("moveSpeed").floatValue = 4.2f;
            aiSo.FindProperty("maxHealth").floatValue = 180f;

            AudioClip swingSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Blade Impacts/Sword_Whoosh_01.wav");
            AudioClip hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/Metal/Sword_Clash_01.wav");
            AudioClip downedClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Thuds/Body_Fall_Armor_01.wav");
            AudioClip reviveClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Ultimate/Fantasy_Game_Magic_Light Magic_5_Blast_Holy_Priest_Spell.wav");

            if (swingSfx != null) aiSo.FindProperty("attackSwingSfx").objectReferenceValue = swingSfx;
            if (hitClip != null) aiSo.FindProperty("hitSfx").objectReferenceValue = hitClip;
            if (downedClip != null) aiSo.FindProperty("downedSfx").objectReferenceValue = downedClip;
            if (reviveClip != null) aiSo.FindProperty("reviveSfx").objectReferenceValue = reviveClip;

            // Downed soul beacon (authored prefab, hidden until the knight is downed)
            GameObject beaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/VFX/HolySoulBeacon.prefab");
            if (beaconPrefab != null)
            {
                GameObject beacon = (GameObject)PrefabUtility.InstantiatePrefab(beaconPrefab, knightGo.transform);
                beacon.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                beacon.SetActive(false);
                aiSo.FindProperty("holySoulBeaconVisual").objectReferenceValue = beacon;
            }
            else Debug.LogError("[BuildPrincessSanctuaryScene] VFX/HolySoulBeacon.prefab not found!");

            aiSo.ApplyModifiedProperties();

            // Equip Sword & Shield
            AttachKnightWeapons(knightGo, swordAsset, shieldAsset);

            list.Add(knightAi);
        }

        return list;
    }

    private static void AttachKnightWeapons(GameObject knightGo, GameObject swordPrefab, GameObject shieldPrefab)
    {
        if (swordPrefab != null)
        {
            GameObject sword = UnityEngine.Object.Instantiate(swordPrefab, knightGo.transform);
            sword.name = "Equipped_Sword";
            sword.transform.localPosition = new Vector3(0.35f, 0.9f, 0.35f);
            sword.transform.localRotation = Quaternion.Euler(0f, 90f, 30f);
        }

        if (shieldPrefab != null)
        {
            GameObject shield = UnityEngine.Object.Instantiate(shieldPrefab, knightGo.transform);
            shield.name = "Equipped_Shield";
            shield.transform.localPosition = new Vector3(-0.35f, 0.9f, 0.25f);
            shield.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        }
    }

    private static GameObject SetupPrincessBoss(Vector3 pos, List<ArmoredKnightAI> knights, Scene scene)
    {
        string princessPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Princess_01.prefab";
        GameObject princessAsset = AssetDatabase.LoadAssetAtPath<GameObject>(princessPrefabPath);

        GameObject princessGo = null;
        if (princessAsset != null)
        {
            princessGo = UnityEngine.Object.Instantiate(princessAsset);
            SceneManager.MoveGameObjectToScene(princessGo, scene);
        }
        else
        {
            princessGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        }

        princessGo.name = "Princess_Boss_Katherine";
        princessGo.transform.position = pos;
        princessGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // CapsuleCollider
        CapsuleCollider col = princessGo.GetComponent<CapsuleCollider>();
        if (col == null) col = princessGo.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.45f;

        // Health
        Health health = princessGo.GetComponent<Health>();
        if (health == null) health = princessGo.AddComponent<Health>();
        HealthSO healthSo = AssetDatabase.LoadAssetAtPath<HealthSO>("Assets/Bladehold/Bladehold Scripts/DamageSystem/HealthSO.asset");
        var hSo = new SerializedObject(health);
        if (healthSo != null) hSo.FindProperty("healthData").objectReferenceValue = healthSo;
        hSo.FindProperty("currentHealth").floatValue = 350f;
        hSo.ApplyModifiedProperties();

        // NavMeshAgent
        NavMeshAgent agent = princessGo.GetComponent<NavMeshAgent>();
        if (agent == null) agent = princessGo.AddComponent<NavMeshAgent>();
        agent.speed = 6.2f;
        agent.stoppingDistance = 0.5f;
        agent.radius = 0.45f;
        agent.height = 1.8f;

        // Animator
        RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Bladehold/Bladehold Animations/SimplifiedEnemyAC.controller");
        Animator anim = princessGo.GetComponentInChildren<Animator>();
        if (anim != null && animController != null)
        {
            anim.runtimeAnimatorController = animController;
        }

        // PrincessBossController
        PrincessBossController bossCtrl = princessGo.GetComponent<PrincessBossController>();
        if (bossCtrl == null) bossCtrl = princessGo.AddComponent<PrincessBossController>();

        var pSo = new SerializedObject(bossCtrl);
        pSo.FindProperty("bossDisplayName").stringValue = "Princess Katherine";
        pSo.FindProperty("bossTitle").stringValue = "Heir to the Throne";
        pSo.FindProperty("channelDuration").floatValue = 5.0f;
        pSo.FindProperty("hitDelayPenalty").floatValue = 1.5f;
        pSo.FindProperty("reviveRange").floatValue = 3.5f;
        pSo.FindProperty("fleeDistanceThreshold").floatValue = 11.0f;
        pSo.FindProperty("fleeSpeed").floatValue = 6.2f;
        pSo.FindProperty("walkToKnightSpeed").floatValue = 5.0f;
        pSo.FindProperty("goldReward").intValue = 500;
        pSo.FindProperty("bloodReward").intValue = 30;
        pSo.FindProperty("metalReward").intValue = 10;

        // Serialized Knights
        var kProp = pSo.FindProperty("knights");
        kProp.arraySize = knights.Count;
        for (int i = 0; i < knights.Count; i++)
        {
            kProp.GetArrayElementAtIndex(i).objectReferenceValue = knights[i];
        }

        // Audio & FX
        AudioClip completeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Ultimate/Fantasy_Game_Magic_Light Magic_5_Blast_Holy_Priest_Spell.wav");
        AudioClip victoryClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/Triumphant Victory.wav");
        AudioClip interruptClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/Metal/Sword_Clash_01.wav");

        if (completeClip != null) pSo.FindProperty("spellCompleteSfx").objectReferenceValue = completeClip;
        if (victoryClip != null) pSo.FindProperty("victoryMusicSfx").objectReferenceValue = victoryClip;
        if (interruptClip != null) pSo.FindProperty("spellHitInterruptSfx").objectReferenceValue = interruptClip;

        // Revive-channel magic circle (authored prefab, hidden until she channels)
        GameObject circlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/VFX/PrincessMagicCircle.prefab");
        if (circlePrefab != null)
        {
            GameObject circle = (GameObject)PrefabUtility.InstantiatePrefab(circlePrefab, princessGo.transform);
            circle.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            circle.SetActive(false);
            pSo.FindProperty("magicCircleVisual").objectReferenceValue = circle;
        }
        else Debug.LogError("[BuildPrincessSanctuaryScene] VFX/PrincessMagicCircle.prefab not found!");

        // Overhead revive cast bar (authored prefab, hidden until she channels)
        GameObject castBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/BossCastBar.prefab");
        if (castBarPrefab != null)
        {
            GameObject castBar = (GameObject)PrefabUtility.InstantiatePrefab(castBarPrefab, princessGo.transform);
            castBar.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            castBar.SetActive(false);
            pSo.FindProperty("floatingCastBarRoot").objectReferenceValue = castBar;
            pSo.FindProperty("castBarFillImage").objectReferenceValue = castBar.transform.Find("CastBar_Fill").GetComponent<UnityEngine.UI.Image>();
            pSo.FindProperty("castBarTimeText").objectReferenceValue = castBar.transform.Find("CastBar_Text").GetComponent<TMPro.TMP_Text>();
        }
        else
        {
            Debug.LogError("[BuildPrincessSanctuaryScene] UI/BossCastBar.prefab not found!");
        }

        pSo.ApplyModifiedProperties();

        return princessGo;
    }

    private static GameObject EnsurePrefabInstance(string name, string prefabPath, Scene scene)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) return existing;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            return instance;
        }

        return null;
    }

    private static Material LoadMaterial(string path, string fallbackShader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find(fallbackShader);
            mat = new Material(shader);
        }
        return mat;
    }

    public static void RegisterSceneInBuildSettings(string scenePath)
    {
        var scenesList = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool exists = false;
        for (int i = 0; i < scenesList.Count; i++)
        {
            if (scenesList[i].path.Equals(scenePath, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                scenesList[i].enabled = true;
                break;
            }
        }

        if (!exists)
        {
            scenesList.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenesList.ToArray();
            Debug.Log($"[BuildPrincessSanctuaryScene] Added {scenePath} to EditorBuildSettings!");
        }
    }
}
#endif
