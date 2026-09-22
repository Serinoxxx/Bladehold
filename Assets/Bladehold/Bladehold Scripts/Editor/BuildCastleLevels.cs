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
///     Automated Scene Generator & Level Builder for Part 3 of the Castle Campaign Overhaul.
///     Constructs the 7 campaign combat encounter scenes:
///     1. Bladehold Castle Courtyard (Tier 1 Center: Courtyard Gate)
///     2. Bladehold Castle Ramparts (Tier 2 Option A: High battlements)
///     3. Bladehold Castle Armory (Tier 2 Option B: Weapons depot)
///     4. Bladehold Great Hall (Tier 4 Center Merge: Grand banquet hall)
///     5. Bladehold Castle Dungeons (Tier 5 Option A: Prison oubliette)
///     6. Bladehold Castle Conservatory (Tier 5 Option B: Castle garden/greenhouse)
///     7. Bladehold Throne Antechamber (Tier 7: Pre-final battle / Obsidian Portico)
/// </summary>
public static class BuildCastleLevels
{
    private const string ScenesDir = "Assets/Bladehold/Bladehold Scenes";

    public static readonly string[] AllCastleScenePaths = new string[]
    {
        "Assets/Bladehold/Bladehold Scenes/Bladehold Castle Courtyard.unity",
        "Assets/Bladehold/Bladehold Scenes/Bladehold Castle Ramparts.unity",
        "Assets/Bladehold/Bladehold Scenes/Bladehold Castle Armory.unity",
        "Assets/Bladehold/Bladehold Scenes/Bladehold Great Hall.unity",
        "Assets/Bladehold/Bladehold Scenes/Bladehold Castle Dungeons.unity",
        "Assets/Bladehold/Bladehold Scenes/Bladehold Castle Conservatory.unity",
        "Assets/Bladehold/Bladehold Scenes/Bladehold Throne Antechamber.unity"
    };

    [MenuItem("Bladehold/Build Castle Levels/Build All 7 Levels", priority = 20)]
    public static void BuildAllLevels()
    {
        Debug.Log("[BuildCastleLevels] === Beginning Full Generation of 7 Castle Campaign Levels ===");

        if (!Directory.Exists(ScenesDir))
        {
            Directory.CreateDirectory(ScenesDir);
        }

        BuildCourtyardScene();
        BuildRampartsScene();
        BuildArmoryScene();
        BuildGreatHallScene();
        BuildDungeonsScene();
        BuildConservatoryScene();
        BuildThroneAntechamberScene();

        RegisterAllScenesInBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildCastleLevels] === All 7 Castle Campaign Levels Successfully Built & Registered! ===");
    }

    // =========================================================================
    // 1. TIER 1: CASTLE COURTYARD
    // =========================================================================
    [MenuItem("Bladehold/Build Castle Levels/1. Castle Courtyard", priority = 21)]
    public static void BuildCourtyardScene()
    {
        string scenePath = AllCastleScenePaths[0];
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting & Atmosphere (Warm golden late afternoon)
        SetupAtmosphere(
            ambientColor: new Color(0.24f, 0.22f, 0.26f),
            fogColor: new Color(0.20f, 0.18f, 0.22f),
            fogDensity: 0.012f,
            sunColor: new Color(1f, 0.90f, 0.72f),
            sunIntensity: 1.25f,
            sunRot: Quaternion.Euler(48f, 35f, 0f)
        );

        // Architecture Root
        GameObject envRoot = new GameObject("Environment_Castle_Courtyard");
        Material stoneMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_A_Emmisive.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");

        // Ground Floor (56x56m stone courtyard)
        CreateGround(envRoot.transform, 56f, 56f, stoneMat);

        // Perimeter Walls & Battlements
        CreatePerimeterEnclosure(envRoot.transform, 56f, 56f, 7f, 2.5f, wallMat, gateOpeningWidth: 10f);

        // Castle Gate at North (Z = 24)
        GameObject gateGo = CreateCastleGate(envRoot.transform, new Vector3(0f, 0f, 24f), Quaternion.Euler(0f, 180f, 0f));

        // Themed Props (Training Dummies, Weapon Racks, Braziers)
        CreateTorchOrBrazier("Brazier_NW", new Vector3(-18f, 0f, 18f), envRoot.transform, new Color(1f, 0.55f, 0.2f), 2.2f, 14f);
        CreateTorchOrBrazier("Brazier_NE", new Vector3(18f, 0f, 18f), envRoot.transform, new Color(1f, 0.55f, 0.2f), 2.2f, 14f);
        CreateTorchOrBrazier("Brazier_SW", new Vector3(-18f, 0f, -18f), envRoot.transform, new Color(1f, 0.55f, 0.2f), 2.2f, 14f);
        CreateTorchOrBrazier("Brazier_SE", new Vector3(18f, 0f, -18f), envRoot.transform, new Color(1f, 0.55f, 0.2f), 2.2f, 14f);
        CreateTorchOrBrazier("GateTorch_L", new Vector3(-6f, 3.5f, 23.5f), envRoot.transform, new Color(1f, 0.65f, 0.25f), 1.8f, 10f);
        CreateTorchOrBrazier("GateTorch_R", new Vector3(6f, 3.5f, 23.5f), envRoot.transform, new Color(1f, 0.65f, 0.25f), 1.8f, 10f);

        // Flanking Corner Bastions / Watchtowers
        CreateStoneColumn(envRoot.transform, new Vector3(-26f, 0f, 26f), new Vector3(5f, 10f, 5f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(26f, 0f, 26f), new Vector3(5f, 10f, 5f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(-26f, 0f, -26f), new Vector3(5f, 10f, 5f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(26f, 0f, -26f), new Vector3(5f, 10f, 5f), wallMat);

        // Bake NavMesh
        BakeNavMesh(envRoot);

        // Instantiate Gameplay Prefabs & Wire Connections
        SetupSurvivorsSceneTool.RunSetup(gateGo, false, true);

        // Setup 6 TowerPlots at Courtyard Choke Points
        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(-6f, 0f, 20f),  // Gate Left
            new Vector3(6f, 0f, 20f),   // Gate Right
            new Vector3(-15f, 0f, 5f),  // Midfield West
            new Vector3(15f, 0f, 5f),   // Midfield East
            new Vector3(-10f, 0f, -12f),// Outer Approach West
            new Vector3(10f, 0f, -12f)  // Outer Approach East
        };
        SetupBattlefieldTowerPlots(plotPositions);

        // Ensure Defenses UI (BuildWheelUI, SupplyUI)
        EnsureDefensesHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BuildCastleLevels] Saved: {scenePath}");
    }

    // =========================================================================
    // 2. TIER 2A: CASTLE RAMPARTS
    // =========================================================================
    [MenuItem("Bladehold/Build Castle Levels/2. Castle Ramparts", priority = 22)]
    public static void BuildRampartsScene()
    {
        string scenePath = AllCastleScenePaths[1];
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting & Atmosphere (Windy, high-elevation cool daylight with silver-blue fog)
        SetupAtmosphere(
            ambientColor: new Color(0.20f, 0.22f, 0.28f),
            fogColor: new Color(0.18f, 0.20f, 0.25f),
            fogDensity: 0.015f,
            sunColor: new Color(0.92f, 0.95f, 1.0f),
            sunIntensity: 1.15f,
            sunRot: Quaternion.Euler(55f, -25f, 0f)
        );

        // Architecture Root (Elongated High Battlement Platform 32m x 64m)
        GameObject envRoot = new GameObject("Environment_Castle_Ramparts");
        Material stoneMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_A_Emmisive.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");

        CreateGround(envRoot.transform, 34f, 66f, stoneMat);
        CreatePerimeterEnclosure(envRoot.transform, 34f, 66f, 6f, 2.0f, wallMat, gateOpeningWidth: 9f);

        // Gate at North End (Z = 28)
        GameObject gateGo = CreateCastleGate(envRoot.transform, new Vector3(0f, 0f, 28f), Quaternion.Euler(0f, 180f, 0f));

        // Parapets and Crenellations along the Bastion sides
        for (float z = -25f; z <= 25f; z += 10f)
        {
            CreateStoneColumn(envRoot.transform, new Vector3(-15.5f, 0f, z), new Vector3(1.5f, 4.5f, 3f), wallMat);
            CreateStoneColumn(envRoot.transform, new Vector3(15.5f, 0f, z), new Vector3(1.5f, 4.5f, 3f), wallMat);
        }

        // Rampart Watchtowers
        CreateStoneColumn(envRoot.transform, new Vector3(-15f, 0f, 30f), new Vector3(5f, 9f, 5f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(15f, 0f, 30f), new Vector3(5f, 9f, 5f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(-15f, 0f, -30f), new Vector3(5f, 9f, 5f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(15f, 0f, -30f), new Vector3(5f, 9f, 5f), wallMat);

        // Braziers along the rampart bastions
        CreateTorchOrBrazier("RampartBrazier_NW", new Vector3(-13f, 0f, 20f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 2.0f, 12f);
        CreateTorchOrBrazier("RampartBrazier_NE", new Vector3(13f, 0f, 20f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 2.0f, 12f);
        CreateTorchOrBrazier("RampartBrazier_MidW", new Vector3(-13f, 0f, 0f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 2.0f, 12f);
        CreateTorchOrBrazier("RampartBrazier_MidE", new Vector3(13f, 0f, 0f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 2.0f, 12f);
        CreateTorchOrBrazier("RampartBrazier_SW", new Vector3(-13f, 0f, -20f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 2.0f, 12f);
        CreateTorchOrBrazier("RampartBrazier_SE", new Vector3(13f, 0f, -20f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 2.0f, 12f);

        BakeNavMesh(envRoot);
        SetupSurvivorsSceneTool.RunSetup(gateGo, false, true);

        // 5 TowerPlots along Rampart bastions and choke points
        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(-6f, 0f, 22f),  // Bastion North Left
            new Vector3(6f, 0f, 22f),   // Bastion North Right
            new Vector3(0f, 0f, 6f),    // Midfield Choke Center
            new Vector3(-8f, 0f, -12f), // South Bastion Left
            new Vector3(8f, 0f, -12f)   // South Bastion Right
        };
        SetupBattlefieldTowerPlots(plotPositions);
        EnsureDefensesHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BuildCastleLevels] Saved: {scenePath}");
    }

    // =========================================================================
    // 3. TIER 2B: CASTLE ARMORY
    // =========================================================================
    [MenuItem("Bladehold/Build Castle Levels/3. Castle Armory", priority = 23)]
    public static void BuildArmoryScene()
    {
        string scenePath = AllCastleScenePaths[2];
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting & Atmosphere (Enclosed forge glow, warm embers, dusty light shafts)
        SetupAtmosphere(
            ambientColor: new Color(0.26f, 0.18f, 0.14f),
            fogColor: new Color(0.18f, 0.12f, 0.10f),
            fogDensity: 0.02f,
            sunColor: new Color(1f, 0.70f, 0.40f),
            sunIntensity: 0.95f,
            sunRot: Quaternion.Euler(45f, -45f, 0f)
        );

        // Architecture Root (48m x 48m Fortified Armory Storehouse)
        GameObject envRoot = new GameObject("Environment_Castle_Armory");
        Material stoneMat = LoadMaterial("Assets/Synty/PolygonDungeon/Materials/Dungeon_Material_01.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");

        CreateGround(envRoot.transform, 50f, 50f, stoneMat);
        CreatePerimeterEnclosure(envRoot.transform, 50f, 50f, 8f, 3.0f, wallMat, gateOpeningWidth: 9f);

        // Gate at North End (Z = 21)
        GameObject gateGo = CreateCastleGate(envRoot.transform, new Vector3(0f, 0f, 21f), Quaternion.Euler(0f, 180f, 0f));

        // Interior Armory Heavy Support Columns
        Vector3[] columnPositions = new Vector3[]
        {
            new Vector3(-12f, 0f, 12f), new Vector3(12f, 0f, 12f),
            new Vector3(-12f, 0f, -4f), new Vector3(12f, 0f, -4f),
            new Vector3(-12f, 0f, -16f), new Vector3(12f, 0f, -16f)
        };
        foreach (Vector3 colPos in columnPositions)
        {
            CreateStoneColumn(envRoot.transform, colPos, new Vector3(3f, 8f, 3f), wallMat);
            CreateArmoryCrateCluster(envRoot.transform, colPos + new Vector3(2f, 0f, 0f), stoneMat);
        }

        // Burning Forge Fireplaces & Torches
        CreateTorchOrBrazier("Forge_West", new Vector3(-20f, 0f, 5f), envRoot.transform, new Color(1f, 0.45f, 0.1f), 3.0f, 16f);
        CreateTorchOrBrazier("Forge_East", new Vector3(20f, 0f, 5f), envRoot.transform, new Color(1f, 0.45f, 0.1f), 3.0f, 16f);
        CreateTorchOrBrazier("ArmoryTorch_N1", new Vector3(-6f, 3.5f, 20f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 1.8f, 10f);
        CreateTorchOrBrazier("ArmoryTorch_N2", new Vector3(6f, 3.5f, 20f), envRoot.transform, new Color(1f, 0.6f, 0.2f), 1.8f, 10f);

        BakeNavMesh(envRoot);
        SetupSurvivorsSceneTool.RunSetup(gateGo, false, true);

        // 5 TowerPlots stationed at weapon cache corners and gate threshold
        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(-6f, 0f, 16f),  // Armory Vault Left
            new Vector3(6f, 0f, 16f),   // Armory Vault Right
            new Vector3(0f, 0f, 3f),    // Forge Choke Center
            new Vector3(-15f, 0f, -10f),// Supply Row West
            new Vector3(15f, 0f, -10f)  // Supply Row East
        };
        SetupBattlefieldTowerPlots(plotPositions);
        EnsureDefensesHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BuildCastleLevels] Saved: {scenePath}");
    }

    // =========================================================================
    // 4. TIER 4: GREAT HALL
    // =========================================================================
    [MenuItem("Bladehold/Build Castle Levels/4. Great Hall", priority = 24)]
    public static void BuildGreatHallScene()
    {
        string scenePath = AllCastleScenePaths[3];
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting & Atmosphere (Grand banquet chandeliers, warm hearth flames, noble gold)
        SetupAtmosphere(
            ambientColor: new Color(0.24f, 0.20f, 0.22f),
            fogColor: new Color(0.16f, 0.14f, 0.18f),
            fogDensity: 0.012f,
            sunColor: new Color(1f, 0.88f, 0.65f),
            sunIntensity: 1.2f,
            sunRot: Quaternion.Euler(60f, 20f, 0f)
        );

        // Architecture Root (42m wide x 66m long Grand Hall)
        GameObject envRoot = new GameObject("Environment_Great_Hall");
        Material floorMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_A_Emmisive.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");
        Material carpetMat = LoadMaterial("Assets/Synty/PolygonGeneric/Materials/Generic_Carpet.mat", "Standard");

        CreateGround(envRoot.transform, 44f, 68f, floorMat);
        CreatePerimeterEnclosure(envRoot.transform, 44f, 68f, 9f, 2.5f, wallMat, gateOpeningWidth: 10f);

        // Exit Gate / Throne Archway at North End (Z = 28)
        GameObject gateGo = CreateCastleGate(envRoot.transform, new Vector3(0f, 0f, 28f), Quaternion.Euler(0f, 180f, 0f));

        // Royal Dais / Throne Platform in front of gate (Z = 23)
        GameObject dais = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dais.name = "Royal_Dais";
        dais.transform.SetParent(envRoot.transform);
        dais.transform.position = new Vector3(0f, 0.35f, 23f);
        dais.transform.localScale = new Vector3(14f, 0.7f, 6f);
        dais.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Central Red Carpet leading down the hall
        GameObject carpet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carpet.name = "Central_Carpet_Runner";
        carpet.transform.SetParent(envRoot.transform);
        carpet.transform.position = new Vector3(0f, 0.03f, 0f);
        carpet.transform.localScale = new Vector3(6f, 0.06f, 48f);
        carpet.GetComponent<Renderer>().sharedMaterial = carpetMat;

        // Grand Stone Colonnade down both flanks
        for (float z = -24f; z <= 16f; z += 8f)
        {
            CreateStoneColumn(envRoot.transform, new Vector3(-9f, 0f, z), new Vector3(2.5f, 9f, 2.5f), wallMat);
            CreateStoneColumn(envRoot.transform, new Vector3(9f, 0f, z), new Vector3(2.5f, 9f, 2.5f), wallMat);

            // Overhead Chandeliers
            CreateTorchOrBrazier($"Chandelier_{z}", new Vector3(0f, 7.5f, z), envRoot.transform, new Color(1f, 0.75f, 0.35f), 2.5f, 16f);

            // Banquet Feast Tables along outer aisles
            CreateBanquetTable(envRoot.transform, new Vector3(-14f, 0f, z), wallMat);
            CreateBanquetTable(envRoot.transform, new Vector3(14f, 0f, z), wallMat);
        }

        BakeNavMesh(envRoot);
        SetupSurvivorsSceneTool.RunSetup(gateGo, false, true);

        // 6 TowerPlots flanking the Dais, central aisle, and hall entrance
        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(-8f, 0f, 20f),  // Dais Flank Left
            new Vector3(8f, 0f, 20f),   // Dais Flank Right
            new Vector3(-13f, 0f, 4f),  // Feast Table Aisle West
            new Vector3(13f, 0f, 4f),   // Feast Table Aisle East
            new Vector3(-9f, 0f, -14f), // Entrance Colonnade Left
            new Vector3(9f, 0f, -14f)   // Entrance Colonnade Right
        };
        SetupBattlefieldTowerPlots(plotPositions);
        EnsureDefensesHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BuildCastleLevels] Saved: {scenePath}");
    }

    // =========================================================================
    // 5. TIER 5A: CASTLE DUNGEONS
    // =========================================================================
    [MenuItem("Bladehold/Build Castle Levels/5. Castle Dungeons", priority = 25)]
    public static void BuildDungeonsScene()
    {
        string scenePath = AllCastleScenePaths[4];
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting & Atmosphere (Dark subterranean gloom, sickly greenish-teal fog, dim flickering braziers)
        SetupAtmosphere(
            ambientColor: new Color(0.12f, 0.16f, 0.18f),
            fogColor: new Color(0.08f, 0.12f, 0.14f),
            fogDensity: 0.025f,
            sunColor: new Color(0.55f, 0.75f, 0.85f),
            sunIntensity: 0.45f,
            sunRot: Quaternion.Euler(70f, -15f, 0f)
        );

        // Architecture Root (48m x 48m Dungeon Oubliette)
        GameObject envRoot = new GameObject("Environment_Castle_Dungeons");
        Material floorMat = LoadMaterial("Assets/Synty/PolygonDungeon/Materials/Dungeon_Material_01.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonDungeon/Materials/PolygonDungeon_02.mat", "Standard");

        CreateGround(envRoot.transform, 50f, 50f, floorMat);
        CreatePerimeterEnclosure(envRoot.transform, 50f, 50f, 7.5f, 3.0f, wallMat, gateOpeningWidth: 8f);

        // Heavy Portcullis Exit Gate at North End (Z = 21)
        GameObject gateGo = CreateCastleGate(envRoot.transform, new Vector3(0f, 0f, 21f), Quaternion.Euler(0f, 180f, 0f));

        // Subterranean Prison Cell Partitions along perimeter
        for (float x = -18f; x <= 18f; x += 12f)
        {
            if (Mathf.Abs(x) < 4f) continue;
            CreateStoneColumn(envRoot.transform, new Vector3(x, 0f, 18f), new Vector3(8f, 5f, 1.5f), wallMat);
            CreateStoneColumn(envRoot.transform, new Vector3(x, 0f, -18f), new Vector3(8f, 5f, 1.5f), wallMat);
        }

        // Dungeon Heavy Pillars
        CreateStoneColumn(envRoot.transform, new Vector3(-8f, 0f, 6f), new Vector3(3f, 7.5f, 3f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(8f, 0f, 6f), new Vector3(3f, 7.5f, 3f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(-8f, 0f, -6f), new Vector3(3f, 7.5f, 3f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(8f, 0f, -6f), new Vector3(3f, 7.5f, 3f), wallMat);

        // Ominous Braziers (Teal-green & ember-orange)
        CreateTorchOrBrazier("DungeonLight_NW", new Vector3(-15f, 2.5f, 12f), envRoot.transform, new Color(0.2f, 0.9f, 0.7f), 1.8f, 12f);
        CreateTorchOrBrazier("DungeonLight_NE", new Vector3(15f, 2.5f, 12f), envRoot.transform, new Color(0.2f, 0.9f, 0.7f), 1.8f, 12f);
        CreateTorchOrBrazier("DungeonLight_SW", new Vector3(-15f, 2.5f, -12f), envRoot.transform, new Color(1f, 0.45f, 0.15f), 1.8f, 12f);
        CreateTorchOrBrazier("DungeonLight_SE", new Vector3(15f, 2.5f, -12f), envRoot.transform, new Color(1f, 0.45f, 0.15f), 1.8f, 12f);
        CreateTorchOrBrazier("GateTorch_Dungeon", new Vector3(0f, 4f, 20.5f), envRoot.transform, new Color(0.3f, 0.9f, 0.6f), 2.0f, 10f);

        BakeNavMesh(envRoot);
        SetupSurvivorsSceneTool.RunSetup(gateGo, false, true);

        // 5 TowerPlots guarding cell junctions and center oubliette
        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(-6f, 0f, 16f),  // Cell Gate Left
            new Vector3(6f, 0f, 16f),   // Cell Gate Right
            new Vector3(0f, 0f, 0f),    // Oubliette Pit Center
            new Vector3(-14f, 0f, -10f),// Warden Station West
            new Vector3(14f, 0f, -10f)  // Executioner Station East
        };
        SetupBattlefieldTowerPlots(plotPositions);
        EnsureDefensesHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BuildCastleLevels] Saved: {scenePath}");
    }

    // =========================================================================
    // 6. TIER 5B: CASTLE CONSERVATORY
    // =========================================================================
    [MenuItem("Bladehold/Build Castle Levels/6. Castle Conservatory", priority = 26)]
    public static void BuildConservatoryScene()
    {
        string scenePath = AllCastleScenePaths[5];
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting & Atmosphere (Shattered glass greenhouse, lush emerald daylight, soft mist)
        SetupAtmosphere(
            ambientColor: new Color(0.18f, 0.25f, 0.22f),
            fogColor: new Color(0.14f, 0.20f, 0.18f),
            fogDensity: 0.015f,
            sunColor: new Color(0.85f, 0.95f, 0.88f),
            sunIntensity: 1.1f,
            sunRot: Quaternion.Euler(45f, -30f, 0f)
        );

        // Architecture Root (48m x 48m Botanical Greenhouse Hall)
        GameObject envRoot = new GameObject("Environment_Castle_Conservatory");
        Material floorMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_A_Emmisive.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");
        Material planterMat = LoadMaterial("Assets/Synty/PolygonGeneric/Materials/Generic_Grass.mat", "Standard");

        CreateGround(envRoot.transform, 50f, 50f, floorMat);
        CreatePerimeterEnclosure(envRoot.transform, 50f, 50f, 7.5f, 2.5f, wallMat, gateOpeningWidth: 9f);

        // Gate at North End (Z = 21)
        GameObject gateGo = CreateCastleGate(envRoot.transform, new Vector3(0f, 0f, 21f), Quaternion.Euler(0f, 180f, 0f));

        // Central Botanical Stone Fountain / Basin Plaza (Z = 1)
        GameObject basin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basin.name = "Central_Fountain_Basin";
        basin.transform.SetParent(envRoot.transform);
        basin.transform.position = new Vector3(0f, 0.35f, 1f);
        basin.transform.localScale = new Vector3(7f, 0.7f, 7f);
        basin.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Raised Stone Planter Beds with Flora
        Vector3[] planterPositions = new Vector3[]
        {
            new Vector3(-14f, 0.25f, 12f), new Vector3(14f, 0.25f, 12f),
            new Vector3(-14f, 0.25f, -8f), new Vector3(14f, 0.25f, -8f)
        };
        foreach (Vector3 pPos in planterPositions)
        {
            GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "Planter_Bed";
            bed.transform.SetParent(envRoot.transform);
            bed.transform.position = pPos;
            bed.transform.localScale = new Vector3(8f, 0.5f, 12f);
            bed.GetComponent<Renderer>().sharedMaterial = planterMat;
        }

        // Arched Glass Lattice Columns along greenhouse ceiling
        CreateStoneColumn(envRoot.transform, new Vector3(-8f, 0f, 14f), new Vector3(2f, 7.5f, 2f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(8f, 0f, 14f), new Vector3(2f, 7.5f, 2f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(-8f, 0f, -12f), new Vector3(2f, 7.5f, 2f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(8f, 0f, -12f), new Vector3(2f, 7.5f, 2f), wallMat);

        // Soft Greenish-Cyan Glowing Flora Point Lights
        CreateTorchOrBrazier("FloraLight_NW", new Vector3(-14f, 2f, 12f), envRoot.transform, new Color(0.4f, 1.0f, 0.6f), 1.8f, 12f);
        CreateTorchOrBrazier("FloraLight_NE", new Vector3(14f, 2f, 12f), envRoot.transform, new Color(0.4f, 1.0f, 0.6f), 1.8f, 12f);
        CreateTorchOrBrazier("FloraLight_SW", new Vector3(-14f, 2f, -8f), envRoot.transform, new Color(0.4f, 1.0f, 0.6f), 1.8f, 12f);
        CreateTorchOrBrazier("FloraLight_SE", new Vector3(14f, 2f, -8f), envRoot.transform, new Color(0.4f, 1.0f, 0.6f), 1.8f, 12f);
        CreateTorchOrBrazier("FountainGlow", new Vector3(0f, 2.5f, 1f), envRoot.transform, new Color(0.3f, 0.8f, 1.0f), 2.2f, 14f);

        BakeNavMesh(envRoot);
        SetupSurvivorsSceneTool.RunSetup(gateGo, false, true);

        // 5 TowerPlots flanking garden pathways and fountain plaza
        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(-6f, 0f, 16f),  // Garden Arch Left
            new Vector3(6f, 0f, 16f),   // Garden Arch Right
            new Vector3(0f, 0f, -6f),   // Central Pathway Plaza
            new Vector3(-14f, 0f, 0f),  // Flower Bed West
            new Vector3(14f, 0f, 0f)    // Trellis Walk East
        };
        SetupBattlefieldTowerPlots(plotPositions);
        EnsureDefensesHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BuildCastleLevels] Saved: {scenePath}");
    }

    // =========================================================================
    // 7. TIER 7: THRONE ANTECHAMBER
    // =========================================================================
    [MenuItem("Bladehold/Build Castle Levels/7. Throne Antechamber", priority = 27)]
    public static void BuildThroneAntechamberScene()
    {
        string scenePath = AllCastleScenePaths[6];
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting & Atmosphere (The Obsidian Portico: deep royal crimson & dark gold shadows)
        SetupAtmosphere(
            ambientColor: new Color(0.25f, 0.14f, 0.18f),
            fogColor: new Color(0.18f, 0.10f, 0.12f),
            fogDensity: 0.015f,
            sunColor: new Color(1.0f, 0.65f, 0.45f),
            sunIntensity: 1.1f,
            sunRot: Quaternion.Euler(50f, 15f, 0f)
        );

        // Architecture Root (38m wide x 64m long Obsidian Portico)
        GameObject envRoot = new GameObject("Environment_Throne_Antechamber");
        Material floorMat = LoadMaterial("Assets/Synty/PolygonGeneric/Materials/Generic_Rock.mat", "Standard");
        Material wallMat = LoadMaterial("Assets/Synty/PolygonFantasyKingdom/Materials/Alts/PolygonFantasyKingdom_Mat_01_B.mat", "Standard");
        Material carpetMat = LoadMaterial("Assets/Synty/PolygonGeneric/Materials/Generic_Carpet.mat", "Standard");

        CreateGround(envRoot.transform, 40f, 66f, floorMat);
        CreatePerimeterEnclosure(envRoot.transform, 40f, 66f, 10f, 3.0f, wallMat, gateOpeningWidth: 10f);

        // Massive Throne Room Double Doors / Gate at North End (Z = 26)
        GameObject gateGo = CreateCastleGate(envRoot.transform, new Vector3(0f, 0f, 26f), Quaternion.Euler(0f, 180f, 0f));

        // Broad Red Carpet Runner leading to the Throne
        GameObject carpet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carpet.name = "Royal_Crimson_Carpet";
        carpet.transform.SetParent(envRoot.transform);
        carpet.transform.position = new Vector3(0f, 0.03f, 0f);
        carpet.transform.localScale = new Vector3(7f, 0.06f, 50f);
        carpet.GetComponent<Renderer>().sharedMaterial = carpetMat;

        // Massive Obsidian Colonnades
        for (float z = -22f; z <= 18f; z += 10f)
        {
            CreateStoneColumn(envRoot.transform, new Vector3(-8.5f, 0f, z), new Vector3(3f, 10f, 3f), wallMat);
            CreateStoneColumn(envRoot.transform, new Vector3(8.5f, 0f, z), new Vector3(3f, 10f, 3f), wallMat);

            CreateTorchOrBrazier($"CeremonialBrazier_L_{z}", new Vector3(-7f, 3.5f, z), envRoot.transform, new Color(1.0f, 0.35f, 0.15f), 2.2f, 12f);
            CreateTorchOrBrazier($"CeremonialBrazier_R_{z}", new Vector3(7f, 3.5f, z), envRoot.transform, new Color(1.0f, 0.35f, 0.15f), 2.2f, 12f);
        }

        // Heavy Iron Barricades along outer flanking wings
        CreateStoneColumn(envRoot.transform, new Vector3(-13f, 0f, 5f), new Vector3(4f, 2.5f, 1.5f), wallMat);
        CreateStoneColumn(envRoot.transform, new Vector3(13f, 0f, 5f), new Vector3(4f, 2.5f, 1.5f), wallMat);

        BakeNavMesh(envRoot);
        SetupSurvivorsSceneTool.RunSetup(gateGo, false, true);

        // 6 TowerPlots flanking the red carpet and the throne doors
        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(-6f, 0f, 21f),  // Throne Threshold Left
            new Vector3(6f, 0f, 21f),   // Throne Threshold Right
            new Vector3(-10f, 0f, 5f),  // Midfield Left Flank
            new Vector3(10f, 0f, 5f),   // Midfield Right Flank
            new Vector3(-7f, 0f, -12f), // Royal Guard Post Left
            new Vector3(7f, 0f, -12f)   // Royal Guard Post Right
        };
        SetupBattlefieldTowerPlots(plotPositions);
        EnsureDefensesHUD();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BuildCastleLevels] Saved: {scenePath}");
    }

    // =========================================================================
    // SHARED BUILDER HELPERS
    // =========================================================================

    private static void SetupAtmosphere(Color ambientColor, Color fogColor, float fogDensity, Color sunColor, float sunIntensity, Quaternion sunRot)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogColor = fogColor;

        GameObject dirLightGo = new GameObject("Directional Light (Sun)");
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
        floor.name = "Floor_Castle_Stone";
        floor.transform.SetParent(parent);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(width, 1f, length);
        if (mat != null) floor.GetComponent<Renderer>().sharedMaterial = mat;
        return floor;
    }

    private static void CreatePerimeterEnclosure(Transform parent, float width, float length, float height, float thickness, Material mat, float gateOpeningWidth = 8f)
    {
        float halfW = width * 0.5f;
        float halfL = length * 0.5f;
        float halfGate = gateOpeningWidth * 0.5f;

        // South Wall (Complete)
        CreateWallSection("Wall_South", new Vector3(0f, height * 0.5f, -halfL), new Vector3(width, height, thickness), parent, mat);

        // West Wall (Complete)
        CreateWallSection("Wall_West", new Vector3(-halfW, height * 0.5f, 0f), new Vector3(thickness, height, length), parent, mat);

        // East Wall (Complete)
        CreateWallSection("Wall_East", new Vector3(halfW, height * 0.5f, 0f), new Vector3(thickness, height, length), parent, mat);

        // North Wall (Left & Right of gate opening)
        float northSegmentWidth = (width - gateOpeningWidth) * 0.5f;
        float northSegmentCenter = halfGate + northSegmentWidth * 0.5f;

        CreateWallSection("Wall_North_Left", new Vector3(-northSegmentCenter, height * 0.5f, halfL), new Vector3(northSegmentWidth, height, thickness), parent, mat);
        CreateWallSection("Wall_North_Right", new Vector3(northSegmentCenter, height * 0.5f, halfL), new Vector3(northSegmentWidth, height, thickness), parent, mat);
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

    private static void CreateTorchOrBrazier(string name, Vector3 pos, Transform parent, Color color, float intensity, float range)
    {
        GameObject torch = new GameObject(name);
        torch.transform.SetParent(parent);
        torch.transform.position = pos;

        Light light = torch.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateBanquetTable(Transform parent, Vector3 pos, Material mat)
    {
        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Banquet_Table";
        table.transform.SetParent(parent);
        table.transform.position = pos + new Vector3(0f, 0.5f, 0f);
        table.transform.localScale = new Vector3(2.2f, 1.0f, 6.0f);
        if (mat != null) table.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static void CreateArmoryCrateCluster(Transform parent, Vector3 pos, Material mat)
    {
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Armory_Crate_Stack";
        crate.transform.SetParent(parent);
        crate.transform.position = pos + new Vector3(0f, 0.75f, 0f);
        crate.transform.localScale = new Vector3(1.6f, 1.5f, 1.6f);
        if (mat != null) crate.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static GameObject CreateCastleGate(Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject gateGo = null;
        string gatePrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Castle_Wall_Gate_L_01.prefab";
        GameObject gateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(gatePrefabPath);

        if (gateAsset != null)
        {
            gateGo = (GameObject)PrefabUtility.InstantiatePrefab(gateAsset, parent);
            gateGo.name = "SM_Bld_Castle_Wall_Gate_L_01";
            gateGo.transform.position = pos;
            gateGo.transform.rotation = rot;
        }
        else
        {
            gateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateGo.name = "Castle_Gate_Structure";
            gateGo.transform.SetParent(parent);
            gateGo.transform.position = pos + new Vector3(0f, 3.5f, 0f);
            gateGo.transform.localScale = new Vector3(9f, 7f, 2f);
            gateGo.transform.rotation = rot;
        }

        // Ensure BoxCollider on root
        BoxCollider col = gateGo.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gateGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 3f, 0f);
            col.size = new Vector3(8f, 6f, 2f);
        }

        // Configure Gate Components (Health, Gate, Interactable)
        HealthSO doorSo = AssetDatabase.LoadAssetAtPath<HealthSO>("Assets/Bladehold/Bladehold Scripts/DamageSystem/DoorSO.asset");
        GameObject explosionVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Fire_Explosion_01.prefab");

        Health portalHealth = gateGo.GetComponent<Health>() ?? gateGo.AddComponent<Health>();
        var pHealthSo = new SerializedObject(portalHealth);
        if (doorSo != null) pHealthSo.FindProperty("healthData").objectReferenceValue = doorSo;
        pHealthSo.FindProperty("currentHealth").floatValue = 200f;
        pHealthSo.ApplyModifiedProperties();

        Transform attackPointT = gateGo.transform.Find("AttackPoint");
        if (attackPointT == null)
        {
            var ap = new GameObject("AttackPoint");
            ap.transform.SetParent(gateGo.transform);
            ap.transform.localPosition = new Vector3(0f, 0f, -2.5f);
            attackPointT = ap.transform;
        }

        Gate portalGate = gateGo.GetComponent<Gate>() ?? gateGo.AddComponent<Gate>();
        var pGateSo = new SerializedObject(portalGate);
        pGateSo.FindProperty("health").objectReferenceValue = portalHealth;
        pGateSo.FindProperty("attackPoint").objectReferenceValue = attackPointT;
        if (explosionVfx != null) pGateSo.FindProperty("explosionVfxPrefab").objectReferenceValue = explosionVfx;
        pGateSo.ApplyModifiedProperties();

        Interactable portalInteractable = gateGo.GetComponent<Interactable>() ?? gateGo.AddComponent<Interactable>();
        portalInteractable.PromptText = "View Campaign Map";
        var pInterSo = new SerializedObject(portalInteractable);
        pInterSo.FindProperty("interactionRadius").floatValue = 5.0f;
        pInterSo.FindProperty("isInteractable").boolValue = true;
        pInterSo.ApplyModifiedProperties();

        return gateGo;
    }

    private static void BakeNavMesh(GameObject envRoot)
    {
        NavMeshSurface surface = envRoot.GetComponent<NavMeshSurface>() ?? envRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Debug.Log($"[BuildCastleLevels] NavMesh baked successfully for {envRoot.name}!");
    }

    private static void SetupBattlefieldTowerPlots(Vector3[] plotPositions)
    {
        string plotPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Defenses/TowerPlot.prefab";
        GameObject plotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(plotPrefabPath);

        GameObject plotsRoot = GameObject.Find("Battlefield Tower Plots");
        if (plotsRoot == null)
        {
            plotsRoot = new GameObject("Battlefield Tower Plots");
        }

        TowerPlotManager manager = plotsRoot.GetComponent<TowerPlotManager>() ?? plotsRoot.AddComponent<TowerPlotManager>();

        for (int i = 0; i < plotPositions.Length; i++)
        {
            string plotName = $"TowerPlot_{i + 1}";
            Transform existing = plotsRoot.transform.Find(plotName);
            GameObject plotObj = null;

            if (existing != null)
            {
                plotObj = existing.gameObject;
            }
            else if (plotPrefab != null)
            {
                plotObj = (GameObject)PrefabUtility.InstantiatePrefab(plotPrefab, plotsRoot.transform);
                plotObj.name = plotName;
            }
            else
            {
                plotObj = new GameObject(plotName);
                plotObj.transform.SetParent(plotsRoot.transform);
            }

            Vector3 targetPos = plotPositions[i];
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }
            plotObj.transform.position = targetPos;
            plotObj.transform.rotation = Quaternion.identity;

            TowerPlot tp = plotObj.GetComponent<TowerPlot>() ?? plotObj.AddComponent<TowerPlot>();
            tp.PlotIndex = i;
            manager.RegisterPlot(tp);
        }

        manager.RefreshPlots();
        EditorUtility.SetDirty(plotsRoot);
        Debug.Log($"[BuildCastleLevels] Setup {plotPositions.Length} TowerPlots with TowerPlotManager.");
    }

    private static void EnsureDefensesHUD()
    {
        GameObject hud = GameObject.Find("Bladehold HUD");
        if (hud == null) return;

        // 1. SupplyUI in Currencies
        GameObject currenciesRoot = GameObject.Find("Bladehold HUD/Screen_HUD_Adventure_01/ScreenSpace/Top Left/Currencies");
        if (currenciesRoot != null)
        {
            Transform existingSupply = currenciesRoot.transform.Find("SupplyUI");
            if (existingSupply == null)
            {
                Transform metalUI = currenciesRoot.transform.Find("MetalUI");
                GameObject supplyObj = metalUI != null ? UnityEngine.Object.Instantiate(metalUI.gameObject, currenciesRoot.transform) : new GameObject("SupplyUI");
                supplyObj.name = "SupplyUI";

                Component oldComp = supplyObj.GetComponent("OrcishMetalUI");
                if (oldComp != null) UnityEngine.Object.DestroyImmediate(oldComp);

                SupplyUI sui = supplyObj.GetComponent<SupplyUI>() ?? supplyObj.AddComponent<SupplyUI>();

                Image iconImg = supplyObj.transform.Find("ICON")?.GetComponent<Image>();
                Sprite hammerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Resources/ICON_SM_Item_Hammer_01.png");
                if (iconImg != null && hammerSprite != null) iconImg.sprite = hammerSprite;

                TMP_Text label = supplyObj.transform.Find("Label_CurrencyValue")?.GetComponent<TMP_Text>();
                SetFieldValue(sui, "label", label);

                EditorUtility.SetDirty(supplyObj);
            }
        }

        // 2. BuildWheelUI in Bladehold HUD
        Transform existingWheel = hud.transform.Find("BuildWheelModal");
        if (existingWheel == null)
        {
            GameObject wheelModal = new GameObject("BuildWheelModal", typeof(RectTransform));
            wheelModal.transform.SetParent(hud.transform, false);

            RectTransform modalRect = wheelModal.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;

            Image bg = wheelModal.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            GameObject centerObj = new GameObject("CenterContainer", typeof(RectTransform));
            centerObj.transform.SetParent(wheelModal.transform, false);
            RectTransform centerRect = centerObj.GetComponent<RectTransform>();
            centerRect.sizeDelta = new Vector2(600f, 600f);

            GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform));
            headerObj.transform.SetParent(centerObj.transform, false);
            RectTransform headRect = headerObj.GetComponent<RectTransform>();
            headRect.anchoredPosition = new Vector2(0f, 260f);
            headRect.sizeDelta = new Vector2(500f, 50f);
            TMP_Text headText = headerObj.AddComponent<TextMeshProUGUI>();
            headText.text = "SELECT DEFENCE TO CONSTRUCT";
            headText.fontSize = 26;
            headText.alignment = TextAlignmentOptions.Center;
            headText.color = new Color(1f, 0.85f, 0.3f);

            GameObject supplyObj = new GameObject("SupplyTotal", typeof(RectTransform));
            supplyObj.transform.SetParent(centerObj.transform, false);
            RectTransform supRect = supplyObj.GetComponent<RectTransform>();
            supRect.anchoredPosition = new Vector2(0f, 220f);
            supRect.sizeDelta = new Vector2(400f, 35f);
            TMP_Text supText = supplyObj.AddComponent<TextMeshProUGUI>();
            supText.text = "Available Supply: 60";
            supText.fontSize = 20;
            supText.alignment = TextAlignmentOptions.Center;

            GameObject descObj = new GameObject("DescriptionText", typeof(RectTransform));
            descObj.transform.SetParent(centerObj.transform, false);
            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchoredPosition = new Vector2(0f, -220f);
            descRect.sizeDelta = new Vector2(500f, 60f);
            TMP_Text descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = "Choose a defense structure to protect the gates.";
            descText.fontSize = 16;
            descText.alignment = TextAlignmentOptions.Center;

            GameObject closeBtnObj = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtnObj.transform.SetParent(centerObj.transform, false);
            RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
            closeRect.anchoredPosition = new Vector2(0f, -270f);
            closeRect.sizeDelta = new Vector2(140f, 36f);
            closeBtnObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            Button closeBtn = closeBtnObj.GetComponent<Button>();

            GameObject closeLabelObj = new GameObject("Label", typeof(RectTransform));
            closeLabelObj.transform.SetParent(closeBtnObj.transform, false);
            TMP_Text closeLbl = closeLabelObj.AddComponent<TextMeshProUGUI>();
            closeLbl.text = "Cancel [Esc]";
            closeLbl.fontSize = 16;
            closeLbl.alignment = TextAlignmentOptions.Center;
            closeLabelObj.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 36f);

            List<Button> buttons = new List<Button>();
            string[] sliceNames = new string[]
            {
                "Arrow Tower", "Catapult", "Ballista",
                "Net Thrower", "Spike Trap", "Oil Vat"
            };
            int[] costs = new int[] { 30, 45, 50, 35, 25, 30 };
            float radius = 160f;

            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 90f - i * 60f;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(angleRad) * radius, Mathf.Sin(angleRad) * radius);

                GameObject btnObj = new GameObject($"Slice_{i}_{sliceNames[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(centerObj.transform, false);
                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.anchoredPosition = pos;
                btnRect.sizeDelta = new Vector2(130f, 75f);
                btnObj.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 0.95f);

                Button btn = btnObj.GetComponent<Button>();
                buttons.Add(btn);

                GameObject btnTxtObj = new GameObject("Text", typeof(RectTransform));
                btnTxtObj.transform.SetParent(btnObj.transform, false);
                RectTransform txtRect = btnTxtObj.GetComponent<RectTransform>();
                txtRect.sizeDelta = new Vector2(125f, 70f);
                TMP_Text txt = btnTxtObj.AddComponent<TextMeshProUGUI>();
                txt.text = $"<b>{sliceNames[i]}</b>\n<color=#FFD700>{costs[i]} Supply</color>";
                txt.fontSize = 14;
                txt.alignment = TextAlignmentOptions.Center;
            }

            BuildWheelUI bwUI = wheelModal.AddComponent<BuildWheelUI>();
            SetFieldValue(bwUI, "wheelPanel", wheelModal);
            SetFieldValue(bwUI, "headerText", headText);
            SetFieldValue(bwUI, "supplyLabel", supText);
            SetFieldValue(bwUI, "descriptionLabel", descText);
            SetFieldValue(bwUI, "closeButton", closeBtn);
            SetFieldValue(bwUI, "sliceButtons", buttons);

            wheelModal.SetActive(false);
            EditorUtility.SetDirty(hud);
        }
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
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

    public static void RegisterAllScenesInBuildSettings()
    {
        var scenesList = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (string scenePath in AllCastleScenePaths)
        {
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
                Debug.Log($"[BuildCastleLevels] Added {scenePath} to EditorBuildSettings!");
            }
        }

        EditorBuildSettings.scenes = scenesList.ToArray();
    }
}
#endif
