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
using UnityEngine.UI;
using TMPro;

/// <summary>
///     Automated Scene Generator & Level Builder for Part 4 of the Castle Campaign Overhaul:
///     Constructs "Bladehold Necromancer Crypt.unity" (Tier 8 Boss Arena).
///     Features:
///     - Colossal indoor crypt with vaulted stone arches, colonnades, sarcophagi, and central dark ritual altar
///     - Gloomy purple/teal atmospheric lighting, eerie braziers, and crypt fog
///     - Necromancer Boss with NecromancerBossController, Scythe weapon, BubbleShield, and skeleton summon points
///     - Cinematic Revelation dialogue UI (NecromancerConfrontationUI) with Obey/Defy choices
///     - Fully integrated Player, CameraRig, HUD, EventSystem, MMTimeManager, and baked NavMeshSurface
/// </summary>
public static class BuildNecromancerCryptScene
{
    public const string ScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Necromancer Crypt.unity";

    [MenuItem("Bladehold/Build Castle Levels/8. Necromancer Crypt Scene", priority = 28)]
    public static void BuildScene()
    {
        Debug.Log("[BuildNecromancerCryptScene] === Building Bladehold Necromancer Crypt Scene ===");

        string dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Crypt Atmosphere (Gloomy purple/teal fog & eerie crypt lighting)
        SetupAtmosphere(
            ambientColor: new Color(0.14f, 0.09f, 0.20f),
            fogColor: new Color(0.12f, 0.08f, 0.18f),
            fogDensity: 0.022f,
            sunColor: new Color(0.45f, 0.38f, 0.65f),
            sunIntensity: 0.65f,
            sunRot: Quaternion.Euler(55f, 30f, 0f)
        );

        // 2. Environment Root
        GameObject envRoot = new GameObject("Environment_Necromancer_Crypt");
        Material floorMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_A_Emmisive.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");

        // Ground Floor (50m wide x 70m long)
        CreateGround(envRoot.transform, 50f, 70f, floorMat);

        // Perimeter Stone Enclosure (50m x 70m, 9m tall, 2.5m thick)
        CreatePerimeterEnclosure(envRoot.transform, 50f, 70f, 9f, 2.5f, wallMat);

        // Crypt Colonnades & Vaulted Pillars along both aisles (X = -13m and +13m)
        for (float z = -25f; z <= 25f; z += 10f)
        {
            CreateStoneColumn(envRoot.transform, new Vector3(-13f, 0f, z), new Vector3(3.2f, 8.5f, 3.2f), wallMat);
            CreateStoneColumn(envRoot.transform, new Vector3(13f, 0f, z), new Vector3(3.2f, 8.5f, 3.2f), wallMat);

            // Ceiling cross-arches connecting columns to side walls
            CreateWallSection($"Ceiling_Beam_L_{z}", new Vector3(-18.5f, 8f, z), new Vector3(11f, 1.2f, 2.2f), envRoot.transform, wallMat);
            CreateWallSection($"Ceiling_Beam_R_{z}", new Vector3(18.5f, 8f, z), new Vector3(11f, 1.2f, 2.2f), envRoot.transform, wallMat);
        }

        // Sarcophagi & Tombs lining the side alcoves
        PlaceCryptTombs(envRoot.transform);

        // Eerie Braziers with Purple/Cyan lighting
        PlaceBraziers(envRoot.transform);

        // Central Raised Ritual Altar Dais at Z = 16m
        GameObject altarDais = GameObject.CreatePrimitive(PrimitiveType.Cube);
        altarDais.name = "Altar_Dais_Platform";
        altarDais.transform.SetParent(envRoot.transform);
        altarDais.transform.position = new Vector3(0f, 0.4f, 16f);
        altarDais.transform.localScale = new Vector3(14f, 0.8f, 10f);
        if (floorMat != null) altarDais.GetComponent<Renderer>().sharedMaterial = floorMat;

        // Steps leading up to the Altar
        GameObject steps = GameObject.CreatePrimitive(PrimitiveType.Cube);
        steps.name = "Altar_Steps";
        steps.transform.SetParent(envRoot.transform);
        steps.transform.position = new Vector3(0f, 0.2f, 10.5f);
        steps.transform.localScale = new Vector3(8f, 0.4f, 2f);
        if (wallMat != null) steps.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Ritual Altar Table
        GameObject altarTable = GameObject.CreatePrimitive(PrimitiveType.Cube);
        altarTable.name = "Ritual_Altar_Table";
        altarTable.transform.SetParent(altarDais.transform);
        altarTable.transform.localPosition = new Vector3(0f, 0.9f, 2f);
        altarTable.transform.localScale = new Vector3(4f, 1f, 2f);
        if (wallMat != null) altarTable.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Ritual Summoning Circles VFX
        GameObject summonCirclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonDungeon/Prefabs/FX/FX_SkeletonSpawn_01.prefab");
        if (summonCirclePrefab != null)
        {
            GameObject circleOnAltar = (GameObject)PrefabUtility.InstantiatePrefab(summonCirclePrefab, altarDais.transform);
            circleOnAltar.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        }

        // 3. Bake NavMeshSurface
        BakeNavMesh(envRoot);

        // 4. Instantiate Standard Gameplay Prefabs (Player, CameraRig, HUD, EventSystem, MMTimeManager)
        GameObject playerGo = SetupPlayer(new Vector3(0f, 0f, -22f), scene);
        GameObject cameraRigGo = EnsurePrefabInstance("CameraRig", "Assets/Bladehold/Bladehold Prefabs/Camera/CameraRig.prefab", scene);
        GameObject hudGo = EnsurePrefabInstance("Bladehold HUD", "Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab", scene);
        GameObject eventSystemGo = EnsurePrefabInstance("EventSystem", "Assets/Bladehold/Bladehold Prefabs/UI/EventSystem.prefab", scene);
        GameObject mmTimeMgrGo = EnsurePrefabInstance("MMTimeManager", "Assets/Bladehold/Bladehold Prefabs/Managers/MMTimeManager.prefab", scene);
        GameObject gameLoopGo = EnsurePrefabInstance("GameLoopManager", "Assets/Bladehold/Bladehold Prefabs/Managers/GameLoopManager.prefab", scene);

        // 5. Setup Skeleton Summoning Points (8 points around crypt)
        Transform[] spawnPoints = CreateSkeletonSpawnPoints(envRoot.transform, new Vector3(0f, 0f, 10f), 8);

        // 6. Setup Necromancer Boss GameObject
        GameObject bossGo = SetupNecromancerBoss(new Vector3(0f, 0.8f, 16f), spawnPoints, summonCirclePrefab, scene);

        // 7. Setup Cinematic Revelation Confrontation UI
        SetupConfrontationUI(bossGo != null ? bossGo.GetComponent<NecromancerBossController>() : null, scene);

        // 8. Register in EditorBuildSettings
        RegisterSceneInBuildSettings(ScenePath);

        // 9. Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BuildNecromancerCryptScene] === Bladehold Necromancer Crypt Successfully Created at {ScenePath}! ===");
    }

    private static void SetupAtmosphere(Color ambientColor, Color fogColor, float fogDensity, Color sunColor, float sunIntensity, Quaternion sunRot)
    {
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogColor = fogColor;

        GameObject dirLightGo = new GameObject("Directional Light (Crypt Sun)");
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
        floor.name = "Floor_Crypt_Stone";
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

    private static void PlaceCryptTombs(Transform parent)
    {
        string[] tombPrefabPaths = new string[]
        {
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Tomb_01.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Tomb_Royal_01.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Tomb_Skeleton_01.prefab"
        };

        float[] zPositions = new float[] { -20f, -10f, 0f, 10f, 20f };
        int idx = 0;

        foreach (float z in zPositions)
        {
            SpawnTomb(tombPrefabPaths[idx % tombPrefabPaths.Length], new Vector3(-20f, 0f, z), Quaternion.Euler(0f, 90f, 0f), parent);
            SpawnTomb(tombPrefabPaths[(idx + 1) % tombPrefabPaths.Length], new Vector3(20f, 0f, z), Quaternion.Euler(0f, -90f, 0f), parent);
            idx++;
        }
    }

    private static void SpawnTomb(string prefabPath, Vector3 pos, Quaternion rot, Transform parent)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset != null)
        {
            GameObject tomb = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            tomb.transform.position = pos;
            tomb.transform.rotation = rot;
        }
        else
        {
            GameObject tomb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tomb.name = "Crypt_Sarcophagus";
            tomb.transform.SetParent(parent);
            tomb.transform.position = pos + Vector3.up * 0.5f;
            tomb.transform.localScale = new Vector3(2.5f, 1f, 1.2f);
            tomb.transform.rotation = rot;
        }
    }

    private static void PlaceBraziers(Transform parent)
    {
        string brazierPrefab = "Assets/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Brazier_01.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(brazierPrefab);

        Vector3[] positions = new Vector3[]
        {
            new Vector3(-6f, 0f, -15f),
            new Vector3(6f, 0f, -15f),
            new Vector3(-6f, 0f, 0f),
            new Vector3(6f, 0f, 0f),
            new Vector3(-8f, 0.8f, 14f),
            new Vector3(8f, 0.8f, 14f),
            new Vector3(-4f, 0.8f, 20f),
            new Vector3(4f, 0.8f, 20f)
        };

        Color purpleColor = new Color(0.7f, 0.2f, 1f);
        Color cyanColor = new Color(0.2f, 0.85f, 0.95f);

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 pos = positions[i];
            Color lightCol = (i % 2 == 0) ? purpleColor : cyanColor;

            GameObject brazierGo = null;
            if (asset != null)
            {
                brazierGo = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                brazierGo.transform.position = pos;
            }
            else
            {
                brazierGo = new GameObject($"Brazier_{i}");
                brazierGo.transform.SetParent(parent);
                brazierGo.transform.position = pos;
            }

            Light light = brazierGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightCol;
            light.intensity = 2.2f;
            light.range = 10f;
            light.shadows = LightShadows.Soft;
        }
    }

    private static void BakeNavMesh(GameObject envRoot)
    {
        NavMeshSurface surface = envRoot.GetComponent<NavMeshSurface>() ?? envRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Debug.Log("[BuildNecromancerCryptScene] NavMesh baked successfully for Crypt!");
    }

    private static GameObject SetupPlayer(Vector3 spawnPos, Scene scene)
    {
        string playerPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[BuildNecromancerCryptScene] Player.prefab not found!");
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

    private static Transform[] CreateSkeletonSpawnPoints(Transform parent, Vector3 center, int count)
    {
        GameObject pointsRoot = new GameObject("Skeleton_Summon_Points");
        pointsRoot.transform.SetParent(parent);
        pointsRoot.transform.position = center;

        Transform[] points = new Transform[count];
        float radius = 12f;

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            GameObject ptObj = new GameObject($"SpawnPoint_{i + 1}");
            ptObj.transform.SetParent(pointsRoot.transform);
            ptObj.transform.position = pos;
            points[i] = ptObj.transform;
        }

        return points;
    }

    private static GameObject SetupNecromancerBoss(Vector3 pos, Transform[] spawnPoints, GameObject summonCirclePrefab, Scene scene)
    {
        string bossModelPath = "Assets/Synty/PolygonFantasyRivals/Prefabs/Characters/SM_Chr_EvilGod_01.prefab";
        GameObject bossModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(bossModelPath);
        if (bossModelAsset == null)
        {
            bossModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Mage_01.prefab");
        }

        GameObject bossGo = null;
        if (bossModelAsset != null)
        {
            bossGo = UnityEngine.Object.Instantiate(bossModelAsset);
            SceneManager.MoveGameObjectToScene(bossGo, scene);
        }
        else
        {
            bossGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        }

        bossGo.name = "Necromancer_Boss_Malakor";
        bossGo.transform.position = pos;
        bossGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Components: CapsuleCollider
        CapsuleCollider col = bossGo.GetComponent<CapsuleCollider>();
        if (col == null) col = bossGo.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1.1f, 0f);
        col.height = 2.2f;
        col.radius = 0.6f;

        // Components: Health
        Health health = bossGo.GetComponent<Health>();
        if (health == null) health = bossGo.AddComponent<Health>();
        HealthSO healthSo = AssetDatabase.LoadAssetAtPath<HealthSO>("Assets/Bladehold/Bladehold Scripts/DamageSystem/HealthSO.asset");
        var hSo = new SerializedObject(health);
        if (healthSo != null) hSo.FindProperty("healthData").objectReferenceValue = healthSo;
        hSo.FindProperty("currentHealth").floatValue = 500f;
        hSo.ApplyModifiedProperties();

        // Components: NavMeshAgent
        NavMeshAgent agent = bossGo.GetComponent<NavMeshAgent>();
        if (agent == null) agent = bossGo.AddComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = 4.5f;
            agent.stoppingDistance = 2.5f;
            agent.radius = 0.6f;
            agent.height = 2.2f;
        }

        // Components: Animator Controller
        RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Bladehold/Bladehold Animations/SimplifiedEnemyAC.controller");
        Animator anim = bossGo.GetComponentInChildren<Animator>();
        if (anim != null && animController != null)
        {
            anim.runtimeAnimatorController = animController;
        }

        // Attach Scythe Weapon
        string scythePrefabPath = "Assets/Synty/Weapons/SM_Wep_Staff_DoubleBlade_01/SM_Wep_Staff_DoubleBlade_01.prefab";
        GameObject scytheAsset = AssetDatabase.LoadAssetAtPath<GameObject>(scythePrefabPath);
        GameObject scytheGo = null;
        if (scytheAsset != null)
        {
            scytheGo = UnityEngine.Object.Instantiate(scytheAsset, bossGo.transform);
            scytheGo.name = "SM_Wep_Staff_DoubleBlade_01";
            scytheGo.transform.localPosition = new Vector3(0.35f, 1.2f, 0.45f);
            scytheGo.transform.localRotation = Quaternion.Euler(0f, 90f, 45f);
        }

        // Skeletons Prefabs array
        GameObject skelKnight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonDungeon/Prefabs/Characters/SM_Chr_Skeleton_Knight_01.prefab");
        GameObject skelSoldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonDungeon/Prefabs/Characters/SM_Chr_Skeleton_Soldier_01.prefab");
        List<GameObject> skelList = new List<GameObject>();
        if (skelKnight != null) skelList.Add(skelKnight);
        if (skelSoldier != null) skelList.Add(skelSoldier);

        // NecromancerBossController
        NecromancerBossController bossCtrl = bossGo.GetComponent<NecromancerBossController>();
        if (bossCtrl == null) bossCtrl = bossGo.AddComponent<NecromancerBossController>();
        var bSo = new SerializedObject(bossCtrl);
        bSo.FindProperty("bossDisplayName").stringValue = "Malakor, The Necromancer";
        bSo.FindProperty("bossTitle").stringValue = "Architect of the Siege";
        bSo.FindProperty("targetSkeletonCount").intValue = 8;
        bSo.FindProperty("normalMoveSpeed").floatValue = 4.5f;
        bSo.FindProperty("chargeMoveSpeed").floatValue = 8.5f;
        bSo.FindProperty("sweepAttackRange").floatValue = 3.5f;
        bSo.FindProperty("sweepAttackDamage").floatValue = 40f;
        bSo.FindProperty("goldReward").intValue = 150;
        bSo.FindProperty("bloodReward").intValue = 20;
        bSo.FindProperty("metalReward").intValue = 6;

        if (scytheGo != null) bSo.FindProperty("scytheWeaponObject").objectReferenceValue = scytheGo;
        if (summonCirclePrefab != null) bSo.FindProperty("summoningCirclePrefab").objectReferenceValue = summonCirclePrefab;

        // Audio Clips
        AudioClip laughClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/voice_male_b_laugh_short_02.wav");
        AudioClip whooshClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Wooshes/whoosh_slow_deep_06.wav");
        AudioClip bubbleClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/GORE_Splat_Hit_Bubbles_mono.wav");
        AudioClip victoryClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/Triumphant Victory.wav");
        GameObject shatterVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Fire_Explosion_01.prefab");

        if (laughClip != null) bSo.FindProperty("laughVoiceSfx").objectReferenceValue = laughClip;
        if (whooshClip != null) bSo.FindProperty("sweepWhooshSfx").objectReferenceValue = whooshClip;
        if (bubbleClip != null) bSo.FindProperty("bubbleDeflectSfx").objectReferenceValue = bubbleClip;
        if (victoryClip != null) bSo.FindProperty("victoryMusicSfx").objectReferenceValue = victoryClip;
        if (shatterVfx != null) bSo.FindProperty("shieldShatterVfxPrefab").objectReferenceValue = shatterVfx;

        // Serialized Spawn Points
        var spProp = bSo.FindProperty("skeletonSpawnPoints");
        spProp.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            spProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }

        // Serialized Skeleton Prefabs
        var skProp = bSo.FindProperty("skeletonPrefabs");
        skProp.arraySize = skelList.Count;
        for (int i = 0; i < skelList.Count; i++)
        {
            skProp.GetArrayElementAtIndex(i).objectReferenceValue = skelList[i];
        }

        bSo.ApplyModifiedProperties();

        return bossGo;
    }

    private static void SetupConfrontationUI(NecromancerBossController bossCtrl, Scene scene)
    {
        GameObject canvasGo = new GameObject("Necromancer_Confrontation_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        CanvasGroup cg = canvasGo.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // Dialogue Modal Panel
        GameObject panelObj = new GameObject("DialogueModalPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 60f);
        panelRect.sizeDelta = new Vector2(1100f, 320f);
        panelObj.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.12f, 0.92f);

        // Header Title (MALAKOR)
        GameObject speakerObj = new GameObject("SpeakerName", typeof(RectTransform), typeof(TextMeshProUGUI));
        speakerObj.transform.SetParent(panelObj.transform, false);
        RectTransform sRect = speakerObj.GetComponent<RectTransform>();
        sRect.anchoredPosition = new Vector2(-360f, 115f);
        sRect.sizeDelta = new Vector2(300f, 40f);
        TextMeshProUGUI sText = speakerObj.GetComponent<TextMeshProUGUI>();
        sText.text = "MALAKOR";
        sText.fontSize = 28;
        sText.fontStyle = FontStyles.Bold;
        sText.color = new Color(0.85f, 0.45f, 1.0f);

        // Subtitle (Architect of the Siege)
        GameObject titleObj = new GameObject("SpeakerTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(panelObj.transform, false);
        RectTransform tRect = titleObj.GetComponent<RectTransform>();
        tRect.anchoredPosition = new Vector2(-360f, 85f);
        tRect.sizeDelta = new Vector2(300f, 30f);
        TextMeshProUGUI tText = titleObj.GetComponent<TextMeshProUGUI>();
        tText.text = "Architect of the Siege";
        tText.fontSize = 16;
        tText.fontStyle = FontStyles.Italic;
        tText.color = new Color(0.7f, 0.7f, 0.8f);

        // Dialogue Body Text
        GameObject bodyObj = new GameObject("DialogueBody", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObj.transform.SetParent(panelObj.transform, false);
        RectTransform bRect = bodyObj.GetComponent<RectTransform>();
        bRect.anchoredPosition = new Vector2(0f, 10f);
        bRect.sizeDelta = new Vector2(1000f, 120f);
        TextMeshProUGUI bText = bodyObj.GetComponent<TextMeshProUGUI>();
        bText.text = "Ah... my greatest masterpiece stands before me. Look at you—tempered in the blood of ten thousand goblins.";
        bText.fontSize = 20;
        bText.color = Color.white;
        bText.textWrappingMode = TextWrappingModes.Normal;

        // Prompt Continue Text
        GameObject promptObj = new GameObject("PromptContinue", typeof(RectTransform), typeof(TextMeshProUGUI));
        promptObj.transform.SetParent(panelObj.transform, false);
        RectTransform pRect = promptObj.GetComponent<RectTransform>();
        pRect.anchoredPosition = new Vector2(0f, -110f);
        pRect.sizeDelta = new Vector2(400f, 30f);
        TextMeshProUGUI pText = promptObj.GetComponent<TextMeshProUGUI>();
        pText.text = "Press [Space] or [Click] to continue...";
        pText.fontSize = 15;
        pText.alignment = TextAlignmentOptions.Center;
        pText.color = new Color(1f, 0.85f, 0.3f);

        // Choice Buttons Container
        GameObject choicesObj = new GameObject("ChoicesContainer", typeof(RectTransform));
        choicesObj.transform.SetParent(panelObj.transform, false);
        RectTransform cRect = choicesObj.GetComponent<RectTransform>();
        cRect.anchoredPosition = new Vector2(0f, -95f);
        cRect.sizeDelta = new Vector2(850f, 70f);

        // Button 1: [OBEY] Slay the Princess
        GameObject obeyBtnObj = new GameObject("Button_Obey", typeof(RectTransform), typeof(Image), typeof(Button));
        obeyBtnObj.transform.SetParent(choicesObj.transform, false);
        RectTransform obeyRect = obeyBtnObj.GetComponent<RectTransform>();
        obeyRect.anchoredPosition = new Vector2(-220f, 0f);
        obeyRect.sizeDelta = new Vector2(380f, 55f);
        obeyBtnObj.GetComponent<Image>().color = new Color(0.45f, 0.35f, 0.15f, 0.95f);
        Button obeyBtn = obeyBtnObj.GetComponent<Button>();

        GameObject obeyTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        obeyTxtObj.transform.SetParent(obeyBtnObj.transform, false);
        obeyTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 50f);
        TextMeshProUGUI obeyText = obeyTxtObj.GetComponent<TextMeshProUGUI>();
        obeyText.text = "<b>[OBEY]</b> Slay the Princess\n<size=12><color=#FFD700>Claim the throne as dark executioner</color></size>";
        obeyText.fontSize = 16;
        obeyText.alignment = TextAlignmentOptions.Center;

        // Button 2: [DEFY] Slay the Necromancer
        GameObject defyBtnObj = new GameObject("Button_Defy", typeof(RectTransform), typeof(Image), typeof(Button));
        defyBtnObj.transform.SetParent(choicesObj.transform, false);
        RectTransform defyRect = defyBtnObj.GetComponent<RectTransform>();
        defyRect.anchoredPosition = new Vector2(220f, 0f);
        defyRect.sizeDelta = new Vector2(380f, 55f);
        defyBtnObj.GetComponent<Image>().color = new Color(0.55f, 0.15f, 0.25f, 0.95f);
        Button defyBtn = defyBtnObj.GetComponent<Button>();

        GameObject defyTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        defyTxtObj.transform.SetParent(defyBtnObj.transform, false);
        defyTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 50f);
        TextMeshProUGUI defyText = defyTxtObj.GetComponent<TextMeshProUGUI>();
        defyText.text = "<b>[DEFY]</b> Slay the Necromancer\n<size=12><color=#FF6688>Cleanse the crypt of his dark evil</color></size>";
        defyText.fontSize = 16;
        defyText.alignment = TextAlignmentOptions.Center;

        choicesObj.SetActive(false);

        // NecromancerConfrontationUI Component
        NecromancerConfrontationUI confrontationUI = canvasGo.AddComponent<NecromancerConfrontationUI>();
        var uiSo = new SerializedObject(confrontationUI);
        uiSo.FindProperty("dialogueCanvasGroup").objectReferenceValue = cg;
        uiSo.FindProperty("dialoguePanel").objectReferenceValue = panelObj;
        uiSo.FindProperty("choicesContainer").objectReferenceValue = choicesObj;
        uiSo.FindProperty("speakerNameText").objectReferenceValue = sText;
        uiSo.FindProperty("speakerTitleText").objectReferenceValue = tText;
        uiSo.FindProperty("dialogueBodyText").objectReferenceValue = bText;
        uiSo.FindProperty("promptContinueText").objectReferenceValue = pText;
        uiSo.FindProperty("obeyButton").objectReferenceValue = obeyBtn;
        uiSo.FindProperty("defyButton").objectReferenceValue = defyBtn;
        uiSo.FindProperty("obeyButtonText").objectReferenceValue = obeyText;
        uiSo.FindProperty("defyButtonText").objectReferenceValue = defyText;
        // The UI opens itself on scene start (openOnSceneStart) and needs the boss to hand over to.
        if (bossCtrl != null) uiSo.FindProperty("boss").objectReferenceValue = bossCtrl;

        AudioClip laughClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/voice_male_b_laugh_short_02.wav");
        if (laughClip != null) uiSo.FindProperty("defyLaughSfx").objectReferenceValue = laughClip;

        uiSo.ApplyModifiedProperties();
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
                scenesList[i].enabled = true;
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            scenesList.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenesList.ToArray();
            Debug.Log($"[BuildNecromancerCryptScene] Added {scenePath} to EditorBuildSettings!");
        }
    }
}
#endif
