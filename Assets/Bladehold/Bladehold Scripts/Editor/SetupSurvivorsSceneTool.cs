#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
///     Automated Editor tool to populate any scene with the full Bladehold Survivors game loop.
///     Instantiates true Prefab instances for Player, HUD, Spawner, Objectives, Cameras,
///     and Managers, configures the scene's Gate/Portal, samples the active NavMesh for
///     spawn/objective layout, and binds all cross-object references.
/// </summary>
public class SetupSurvivorsSceneTool : EditorWindow
{
    private GameObject customGateOrPortal;
    private Vector3 customPlayerSpawnPos = Vector3.zero;
    private bool autoDetectPortal = true;
    private bool replaceExistingPlayer = true;

    [MenuItem("Bladehold/Survivors Scene Setup Tool", priority = 10)]
    public static void ShowWindow()
    {
        var window = GetWindow<SetupSurvivorsSceneTool>("Survivors Scene Setup");
        window.minSize = new Vector2(420, 320);
        window.Show();
    }

    [MenuItem("Bladehold/Setup Active Scene for Survivors Mode", priority = 11)]
    public static void RunOnActiveSceneQuick()
    {
        RunSetup(null, true, true);
    }

    private void OnGUI()
    {
        GUILayout.Label("Bladehold Survivors Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool instantiates real Prefab instances (Player, HUD, Spawner, Objectives, Managers) " +
            "into the active scene, configures the Gate/Portal, positions spawnpoints on the NavMesh, " +
            "and automatically wires all cross-references.", MessageType.Info);

        EditorGUILayout.Space();

        autoDetectPortal = EditorGUILayout.Toggle("Auto-Detect Portal / Gate", autoDetectPortal);
        if (!autoDetectPortal)
        {
            customGateOrPortal = (GameObject)EditorGUILayout.ObjectField("Gate / Portal Object", customGateOrPortal, typeof(GameObject), true);
        }

        replaceExistingPlayer = EditorGUILayout.Toggle("Replace Unpacked Player", replaceExistingPlayer);

        EditorGUILayout.Space();

        if (GUILayout.Button("Populate & Wire Active Scene", GUILayout.Height(36)))
        {
            RunSetup(customGateOrPortal, autoDetectPortal, replaceExistingPlayer);
        }
    }

    public static void RunSetup(GameObject designatedPortal, bool autoDetect, bool replacePlayer)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Debug.Log($"[SetupSurvivorsSceneTool] Beginning setup for scene: {activeScene.name}");

        // 1. Locate or validate NavMesh
        Vector3 clearingCenter = Vector3.zero;
        int navSampleCount = 0;
        for (float x = -60f; x <= 60f; x += 10f)
        {
            for (float z = -60f; z <= 60f; z += 10f)
            {
                if (NavMesh.SamplePosition(new Vector3(x, 1f, z), out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    clearingCenter += hit.position;
                    navSampleCount++;
                }
            }
        }
        if (navSampleCount > 0)
        {
            clearingCenter /= navSampleCount;
        }

        // 2. Identify or configure Gate / Portal
        GameObject portalGo = designatedPortal;
        if (portalGo == null && autoDetect)
        {
            portalGo = FindGateOrPortalInScene();
        }

        if (portalGo != null)
        {
            ConfigureGateOrPortal(portalGo);
            Debug.Log($"[SetupSurvivorsSceneTool] Configured Gate/Portal on '{portalGo.name}' at {portalGo.transform.position}");
        }
        else
        {
            Debug.LogWarning("[SetupSurvivorsSceneTool] No Gate or Portal found in scene. Scene will operate in open-arena mode without gate defense.");
        }

        // 3. Setup Player Prefab
        GameObject playerGo = GameObject.Find("Player");
        Vector3 playerPos = new Vector3(0f, 1f, 0f);
        Quaternion playerRot = Quaternion.identity;

        if (playerGo != null)
        {
            playerPos = playerGo.transform.position;
            playerRot = playerGo.transform.rotation;

            bool isConnectedPrefab = PrefabUtility.GetPrefabInstanceStatus(playerGo) == PrefabInstanceStatus.Connected;
            if (replacePlayer && !isConnectedPrefab)
            {
                Debug.Log("[SetupSurvivorsSceneTool] Removing unpacked scene Player and replacing with Player.prefab instance...");
                DestroyImmediate(playerGo);
                playerGo = null;
            }
        }

        if (playerGo == null)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Player.prefab");
            if (playerPrefab != null)
            {
                if (NavMesh.SamplePosition(playerPos, out NavMeshHit pHit, 15f, NavMesh.AllAreas))
                {
                    playerPos = pHit.position;
                }
                playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, activeScene);
                playerGo.name = "Player";
                playerGo.transform.position = playerPos;
                playerGo.transform.rotation = playerRot;
                Debug.Log($"[SetupSurvivorsSceneTool] Instantiated Player.prefab at {playerPos}");
            }
            else
            {
                Debug.LogError("[SetupSurvivorsSceneTool] Player.prefab not found at Assets/Bladehold/Bladehold Prefabs/Player.prefab!");
            }
        }

        // 4. Instantiate Prefabs
        GameObject hudGo = EnsurePrefabInstance("Bladehold HUD", "Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab", activeScene);
        GameObject gameMenuGo = EnsurePrefabInstance("GameMenu", "Assets/Bladehold/Bladehold Prefabs/UI/GameMenu.prefab", activeScene);
        GameObject pauseMenuCanvasGo = EnsurePrefabInstance("PauseMenuCanvas", "Assets/Bladehold/Bladehold Prefabs/UI/PauseMenuCanvas.prefab", activeScene);
        GameObject eventSystemGo = EnsurePrefabInstance("EventSystem", "Assets/Bladehold/Bladehold Prefabs/UI/EventSystem.prefab", activeScene);
        GameObject deathScreenGo = EnsurePrefabInstance("DeathScreen", "Assets/Bladehold/Bladehold Prefabs/UI/DeathScreen.prefab", activeScene);
        GameObject gameAnalyticsGo = EnsurePrefabInstance("GameAnalytics", "Packages/com.gameanalytics.sdk/Runtime/Prefabs/GameAnalytics.prefab", activeScene);

        GameObject cameraRigGo = EnsurePrefabInstance("CameraRig", "Assets/Bladehold/Bladehold Prefabs/Camera/CameraRig.prefab", activeScene);
        GameObject enemySpawnerGo = EnsurePrefabInstance("EnemySpawner", "Assets/Bladehold/Bladehold Prefabs/Waves/EnemySpawner.prefab", activeScene);
        GameObject objectivesGo = EnsurePrefabInstance("SurvivorsObjectives", "Assets/Bladehold/Bladehold Prefabs/Objectives/SurvivorsObjectives.prefab", activeScene);
        GameObject fortSocketsGo = EnsurePrefabInstance("Fort Defense Sockets", "Assets/Bladehold/Bladehold Prefabs/Fort/FortDefenseSockets.prefab", activeScene);

        GameObject gameLoopGo = EnsurePrefabInstance("GameLoopManager", "Assets/Bladehold/Bladehold Prefabs/Managers/GameLoopManager.prefab", activeScene);
        GameObject survivorsGameMgrGo = EnsurePrefabInstance("SurvivorsGameManager", "Assets/Bladehold/Bladehold Prefabs/Managers/SurvivorsGameManager.prefab", activeScene);
        GameObject gameStatsGo = EnsurePrefabInstance("GameStats", "Assets/Bladehold/Bladehold Prefabs/Managers/GameStats.prefab", activeScene);
        GameObject draftServiceGo = EnsurePrefabInstance("DraftUpgradeService", "Assets/Bladehold/Bladehold Prefabs/Managers/DraftUpgradeService.prefab", activeScene);
        GameObject enemyIntroGo = EnsurePrefabInstance("EnemyIntroController", "Assets/Bladehold/Bladehold Prefabs/Managers/EnemyIntroController.prefab", activeScene);
        GameObject fortManagerGo = EnsurePrefabInstance("FortDefenseManager", "Assets/Bladehold/Bladehold Prefabs/Managers/FortDefenseManager.prefab", activeScene);
        GameObject mmTimeMgrGo = EnsurePrefabInstance("MMTimeManager", "Assets/Bladehold/Bladehold Prefabs/Managers/MMTimeManager.prefab", activeScene);

        // 5. Intermission Anchors
        GameObject powerupSpawnGo = EnsureSceneTransform("UpgradePowerupSpawnPoint", activeScene);
        GameObject banner0Go = EnsureSceneTransform("BannerSpawnPoint_0", activeScene);
        GameObject banner1Go = EnsureSceneTransform("BannerSpawnPoint_1", activeScene);
        GameObject banner2Go = EnsureSceneTransform("BannerSpawnPoint_2", activeScene);
        GameObject intermissionCamGo = EnsureIntermissionCamera("IntermissionVirtualCamera", activeScene);

        // 6. NavMesh Layout for Spawner, Objectives, and Intermission
        Vector3 mapCenter = playerGo != null ? playerGo.transform.position : clearingCenter;
        LayoutSpatialPoints(enemySpawnerGo, objectivesGo, portalGo, mapCenter, powerupSpawnGo, banner0Go, banner1Go, banner2Go, intermissionCamGo, fortSocketsGo);

        // 7. Wire Cross-Object References
        WireAllDependencies(playerGo, portalGo, hudGo, gameMenuGo, pauseMenuCanvasGo, deathScreenGo,
            cameraRigGo, enemySpawnerGo, objectivesGo, gameLoopGo, survivorsGameMgrGo,
            enemyIntroGo, powerupSpawnGo, banner0Go, banner1Go, banner2Go, intermissionCamGo);

        // 8. Save
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log($"[SetupSurvivorsSceneTool] Successfully populated and saved {activeScene.name} with connected prefabs!");
    }

    private static GameObject FindGateOrPortalInScene()
    {
        string[] candidates = new string[] {
            "SM_Env_Tree_Portal_01",
            "SM_Bld_Castle_Wall_Gate_L_01 (2)",
            "SM_Prop_Portal_01",
            "Gate",
            "Portal"
        };
        foreach (string name in candidates)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) return go;
        }

        Gate existingGate = Object.FindAnyObjectByType<Gate>();
        if (existingGate != null) return existingGate.gameObject;

        Scene activeScene = SceneManager.GetActiveScene();
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLower();
                if (lower.Contains("portal") || lower.Contains("gate"))
                {
                    return t.gameObject;
                }
            }
        }

        return null;
    }

    private static void ConfigureGateOrPortal(GameObject portalGo)
    {
        HealthSO doorSo = AssetDatabase.LoadAssetAtPath<HealthSO>("Assets/Bladehold/Bladehold Scripts/DamageSystem/DoorSO.asset");
        GameObject explosionVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Fire_Explosion_01.prefab");

        Health portalHealth = portalGo.GetComponent<Health>();
        if (portalHealth == null) portalHealth = portalGo.AddComponent<Health>();
        var pHealthSo = new SerializedObject(portalHealth);
        if (doorSo != null) pHealthSo.FindProperty("healthData").objectReferenceValue = doorSo;
        pHealthSo.FindProperty("currentHealth").floatValue = 200f;
        pHealthSo.ApplyModifiedProperties();

        Transform attackPointT = portalGo.transform.Find("AttackPoint");
        if (attackPointT == null)
        {
            var ap = new GameObject("AttackPoint");
            ap.transform.SetParent(portalGo.transform);
            Vector3 pos = portalGo.transform.position + portalGo.transform.forward * 2.8f;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                pos = hit.position;
            }
            ap.transform.position = pos;
            attackPointT = ap.transform;
        }

        Gate portalGate = portalGo.GetComponent<Gate>();
        if (portalGate == null) portalGate = portalGo.AddComponent<Gate>();
        var pGateSo = new SerializedObject(portalGate);
        pGateSo.FindProperty("health").objectReferenceValue = portalHealth;
        pGateSo.FindProperty("attackPoint").objectReferenceValue = attackPointT;
        if (explosionVfx != null) pGateSo.FindProperty("explosionVfxPrefab").objectReferenceValue = explosionVfx;
        pGateSo.ApplyModifiedProperties();

        Interactable portalInteractable = portalGo.GetComponent<Interactable>();
        if (portalInteractable == null) portalInteractable = portalGo.AddComponent<Interactable>();
        portalInteractable.PromptText = "Rest Area";
        var pInterSo = new SerializedObject(portalInteractable);
        pInterSo.FindProperty("interactionRadius").floatValue = 4.5f;
        pInterSo.FindProperty("isInteractable").boolValue = true;
        pInterSo.ApplyModifiedProperties();
    }

    private static GameObject EnsurePrefabInstance(string name, string prefabPath, Scene targetScene)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            if (PrefabUtility.GetPrefabInstanceStatus(existing) == PrefabInstanceStatus.Connected)
            {
                return existing;
            }
            Debug.Log($"[SetupSurvivorsSceneTool] Replacing unpacked '{name}' with connected prefab instance from '{prefabPath}'...");
            DestroyImmediate(existing);
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"[SetupSurvivorsSceneTool] Prefab asset not found at {prefabPath}");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, targetScene);
        instance.name = name;
        return instance;
    }

    private static GameObject EnsureSceneTransform(string name, Scene scene)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        return go;
    }

    private static GameObject EnsureIntermissionCamera(string name, Scene scene)
    {
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            if (r.name == name) return r;
        }
        var go = new GameObject(name);
        go.SetActive(false);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<Unity.Cinemachine.CinemachineCamera>();
        return go;
    }

    private static void LayoutSpatialPoints(GameObject spawnerGo, GameObject objectivesGo, GameObject portalGo, Vector3 center,
        GameObject powerupSpawnGo, GameObject banner0Go, GameObject banner1Go, GameObject banner2Go, GameObject intermissionCamGo, GameObject fortSocketsGo)
    {
        // 1. Spawner Points
        if (spawnerGo != null)
        {
            Transform spTransform = spawnerGo.transform.Find("Spawnpoints");
            if (spTransform != null)
            {
                int count = spTransform.childCount;
                var spList = new List<Transform>();
                for (int i = 0; i < count; i++)
                {
                    Transform ch = spTransform.GetChild(i);
                    float angle = i * (360f / count) * Mathf.Deg2Rad;
                    float dist = 28f + (i % 3) * 3f;
                    Vector3 rawPos = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                    if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                    {
                        ch.position = hit.position;
                    }
                    spList.Add(ch);
                }

                SurvivorsSpawner spawnerComp = spawnerGo.GetComponent<SurvivorsSpawner>();
                if (spawnerComp != null)
                {
                    var spSo = new SerializedObject(spawnerComp);
                    var spProp = spSo.FindProperty("spawnPoints");
                    spProp.arraySize = spList.Count;
                    for (int i = 0; i < spList.Count; i++)
                    {
                        spProp.GetArrayElementAtIndex(i).objectReferenceValue = spList[i];
                    }
                    spSo.ApplyModifiedProperties();
                }
            }
        }

        // 2. Intermission Anchors
        Vector3 baseIntermission = center + new Vector3(0f, 0f, 6f);
        if (NavMesh.SamplePosition(baseIntermission, out NavMeshHit intHit, 10f, NavMesh.AllAreas))
        {
            baseIntermission = intHit.position;
        }

        if (powerupSpawnGo != null) powerupSpawnGo.transform.position = baseIntermission + new Vector3(0f, 0f, -2f);
        if (banner0Go != null) banner0Go.transform.position = baseIntermission + new Vector3(-2f, 0f, 0f);
        if (banner1Go != null) banner1Go.transform.position = baseIntermission + new Vector3(0f, 0f, 0f);
        if (banner2Go != null) banner2Go.transform.position = baseIntermission + new Vector3(2f, 0f, 0f);
        if (intermissionCamGo != null)
        {
            intermissionCamGo.transform.position = baseIntermission + new Vector3(0f, 3.5f, -6f);
            intermissionCamGo.transform.LookAt(baseIntermission);
        }

        // 3. Objectives Waypoints
        if (objectivesGo != null)
        {
            Transform wagonRouteT = objectivesGo.transform.Find("WagonRoute");
            if (wagonRouteT != null)
            {
                Transform gdp = wagonRouteT.Find("GateDestinationPoint");
                if (gdp != null && portalGo != null)
                {
                    Vector3 destPos = portalGo.transform.position + portalGo.transform.forward * 3.0f;
                    if (NavMesh.SamplePosition(destPos, out NavMeshHit gdpHit, 8f, NavMesh.AllAreas)) destPos = gdpHit.position;
                    gdp.position = destPos;
                }
                Transform wsp = wagonRouteT.Find("WagonSpawnPoint");
                if (wsp != null)
                {
                    Vector3 wPos = center + new Vector3(0f, 0f, 32f);
                    if (NavMesh.SamplePosition(wPos, out NavMeshHit wspHit, 15f, NavMesh.AllAreas)) wPos = wspHit.position;
                    wsp.position = wPos;
                }
            }

            Transform catSpawnsT = objectivesGo.transform.Find("CatapultSpawns");
            if (catSpawnsT != null)
            {
                SetNavPoint(catSpawnsT.Find("CatapultSpawn_1"), center + new Vector3(-15f, 0f, 10f));
                SetNavPoint(catSpawnsT.Find("CatapultSpawn_2"), center + new Vector3(18f, 0f, -5f));
                SetNavPoint(catSpawnsT.Find("CatapultSpawn_3"), center + new Vector3(0f, 0f, 20f));
            }

            Transform cageSpawnsT = objectivesGo.transform.Find("CageSpawns");
            if (cageSpawnsT != null)
            {
                SetNavPoint(cageSpawnsT.Find("CageSpawn_1"), center + new Vector3(-10f, 0f, 5f));
                SetNavPoint(cageSpawnsT.Find("CageSpawn_2"), center + new Vector3(12f, 0f, 5f));
                SetNavPoint(cageSpawnsT.Find("CageSpawn_3"), center + new Vector3(2f, 0f, -10f));
            }

            Transform ggWaypointsT = objectivesGo.transform.Find("GoldenGoblinWaypoints");
            if (ggWaypointsT != null)
            {
                SetNavPoint(ggWaypointsT.Find("Waypoint_0"), center + new Vector3(-12f, 0f, 5f));
                SetNavPoint(ggWaypointsT.Find("Waypoint_1"), center + new Vector3(12f, 0f, 8f));
                SetNavPoint(ggWaypointsT.Find("Waypoint_2"), center + new Vector3(8f, 0f, -10f));
                SetNavPoint(ggWaypointsT.Find("Waypoint_3"), center + new Vector3(-10f, 0f, -8f));
            }
        }

        // 4. Fort Defense Sockets
        if (fortSocketsGo != null && portalGo != null)
        {
            Vector3 pPos = portalGo.transform.position;
            Vector3 pFwd = portalGo.transform.forward;
            Vector3 pRight = portalGo.transform.right;

            Transform sBoil = fortSocketsGo.transform.Find("Socket_Gate_BoilingOil");
            if (sBoil != null) sBoil.position = pPos + Vector3.up * 4.5f + pFwd * 0.5f;

            SetNavPoint(fortSocketsGo.transform.Find("Socket_Ground_Spikes_1"), pPos + pFwd * 3.0f - pRight * 2.5f);
            SetNavPoint(fortSocketsGo.transform.Find("Socket_Ground_Spikes_2"), pPos + pFwd * 3.0f);
            SetNavPoint(fortSocketsGo.transform.Find("Socket_Ground_Spikes_3"), pPos + pFwd * 3.0f + pRight * 2.5f);

            Transform sWall1 = fortSocketsGo.transform.Find("Socket_Wall_ArrowSlit_1");
            if (sWall1 != null) sWall1.position = pPos + Vector3.up * 3.0f - pRight * 4.0f;

            Transform sWall2 = fortSocketsGo.transform.Find("Socket_Wall_ArrowSlit_2");
            if (sWall2 != null) sWall2.position = pPos + Vector3.up * 3.0f + pRight * 4.0f;

            Transform sWall3 = fortSocketsGo.transform.Find("Socket_Wall_ArrowSlit_3");
            if (sWall3 != null) sWall3.position = pPos + Vector3.up * 5.5f;
        }
    }

    private static void SetNavPoint(Transform t, Vector3 targetPos)
    {
        if (t == null) return;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            t.position = hit.position;
        }
        else
        {
            t.position = targetPos;
        }
    }

    private static void WireAllDependencies(GameObject playerGo, GameObject portalGo, GameObject hudGo,
        GameObject gameMenuGo, GameObject pauseMenuCanvasGo, GameObject deathScreenGo,
        GameObject cameraRigGo, GameObject enemySpawnerGo, GameObject objectivesGo,
        GameObject gameLoopGo, GameObject survivorsGameMgrGo, GameObject enemyIntroGo,
        GameObject powerupSpawnGo, GameObject banner0Go, GameObject banner1Go, GameObject banner2Go, GameObject intermissionCamGo)
    {
        Transform sidekickT = playerGo != null ? playerGo.transform.Find("SidekickSyntyCharacter") : null;
        Transform mainCamT = playerGo != null ? playerGo.transform.Find("PF_SyntyCamera/MainCamera") : null;
        Transform camPivotT = playerGo != null ? playerGo.transform.Find("CameraPivot") : null;

        Health portalHealth = portalGo != null ? portalGo.GetComponent<Health>() : null;
        Interactable portalInteractable = portalGo != null ? portalGo.GetComponent<Interactable>() : null;
        SurvivorsSpawner spawnerComp = enemySpawnerGo != null ? enemySpawnerGo.GetComponent<SurvivorsSpawner>() : null;
        HoldTheLineBonus htlBonus = enemySpawnerGo != null ? enemySpawnerGo.GetComponent<HoldTheLineBonus>() : null;
        SurvivorsObjectiveManager objManagerComp = objectivesGo != null ? objectivesGo.GetComponent<SurvivorsObjectiveManager>() : null;

        Light dirLight = Object.FindAnyObjectByType<Light>();
        UnityEngine.Rendering.Volume globalVol = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();

        // 1. Spawner & HoldTheLineBonus
        if (htlBonus != null && sidekickT != null)
        {
            var htlSo = new SerializedObject(htlBonus);
            htlSo.FindProperty("stats").objectReferenceValue = sidekickT.GetComponent<PlayerStats>();
            htlSo.ApplyModifiedProperties();
        }

        // 2. DefeatSlayerObjective
        if (objectivesGo != null && enemySpawnerGo != null)
        {
            var slayerObj = objectivesGo.GetComponent<DefeatSlayerObjective>();
            Transform spTransform = enemySpawnerGo.transform.Find("Spawnpoints");
            if (slayerObj != null && spTransform != null && spTransform.childCount > 8)
            {
                var slSo = new SerializedObject(slayerObj);
                var spsProp = slSo.FindProperty("spawnPoints");
                spsProp.arraySize = 1;
                spsProp.GetArrayElementAtIndex(0).objectReferenceValue = spTransform.GetChild(8);
                slSo.ApplyModifiedProperties();
            }
        }

        // 3. GameLoopManager
        if (gameLoopGo != null)
        {
            GameLoopManager glm = gameLoopGo.GetComponent<GameLoopManager>();
            if (glm != null)
            {
                var glmSo = new SerializedObject(glm);
                glmSo.FindProperty("spawner").objectReferenceValue = spawnerComp;
                glmSo.FindProperty("castleGateInteractable").objectReferenceValue = portalInteractable;
                glmSo.FindProperty("objectiveManager").objectReferenceValue = objManagerComp;
                if (powerupSpawnGo != null) glmSo.FindProperty("upgradePowerupSpawnPoint").objectReferenceValue = powerupSpawnGo.transform;

                var bspProp = glmSo.FindProperty("bannerSpawnPoints");
                bspProp.arraySize = 3;
                bspProp.GetArrayElementAtIndex(0).objectReferenceValue = banner0Go != null ? banner0Go.transform : null;
                bspProp.GetArrayElementAtIndex(1).objectReferenceValue = banner1Go != null ? banner1Go.transform : null;
                bspProp.GetArrayElementAtIndex(2).objectReferenceValue = banner2Go != null ? banner2Go.transform : null;

                if (intermissionCamGo != null)
                {
                    glmSo.FindProperty("intermissionVirtualCamera").objectReferenceValue = intermissionCamGo.GetComponent<Unity.Cinemachine.CinemachineCamera>();
                }
                if (hudGo != null)
                {
                    Transform panelT = hudGo.transform.Find("IntermissionStatsPanel");
                    if (panelT != null) glmSo.FindProperty("intermissionStatsPanel").objectReferenceValue = panelT.gameObject;
                }
                glmSo.ApplyModifiedProperties();
            }
        }

        // 4. Bladehold HUD
        if (hudGo != null)
        {
            var fgHealthBar = hudGo.GetComponentInChildren<FortressGateHealthBarUI>(true);
            if (fgHealthBar != null && portalHealth != null)
            {
                var fgSo = new SerializedObject(fgHealthBar);
                fgSo.FindProperty("health").objectReferenceValue = portalHealth;
                fgSo.ApplyModifiedProperties();
            }

            var objTracker = hudGo.GetComponentInChildren<ObjectiveTrackerUI>(true);
            if (objTracker != null)
            {
                var otSo = new SerializedObject(objTracker);
                otSo.FindProperty("spawner").objectReferenceValue = spawnerComp;
                otSo.FindProperty("objectiveManager").objectReferenceValue = objManagerComp;
                otSo.ApplyModifiedProperties();
            }

            var waveClearedBanner = hudGo.GetComponentInChildren<WaveClearedBannerUI>(true);
            if (waveClearedBanner != null)
            {
                var wcbSo = new SerializedObject(waveClearedBanner);
                wcbSo.FindProperty("spawner").objectReferenceValue = spawnerComp;
                wcbSo.FindProperty("objectiveManager").objectReferenceValue = objManagerComp;
                wcbSo.ApplyModifiedProperties();
            }

            var waveIntermissionUI = hudGo.GetComponentInChildren<WaveIntermissionUI>(true);
            if (waveIntermissionUI != null)
            {
                var wiSo = new SerializedObject(waveIntermissionUI);
                wiSo.FindProperty("spawner").objectReferenceValue = spawnerComp;
                wiSo.FindProperty("holdTheLineBonus").objectReferenceValue = htlBonus;
                wiSo.ApplyModifiedProperties();
            }

            if (sidekickT != null)
            {
                var bc = hudGo.GetComponentInChildren<BowCrosshairUI>(true);
                if (bc != null)
                {
                    var bcSo = new SerializedObject(bc);
                    bcSo.FindProperty("bow").objectReferenceValue = sidekickT.GetComponent("PlayerBow");
                    bcSo.ApplyModifiedProperties();
                }

                var br = hudGo.GetComponentInChildren<BowReloadUI>(true);
                if (br != null)
                {
                    var brSo = new SerializedObject(br);
                    brSo.FindProperty("bow").objectReferenceValue = sidekickT.GetComponent("PlayerBow");
                    brSo.ApplyModifiedProperties();
                }

                var hb = hudGo.GetComponentInChildren<HorseBarGroupUI>(true);
                if (hb != null)
                {
                    var hbSo = new SerializedObject(hb);
                    hbSo.FindProperty("mount").objectReferenceValue = sidekickT.GetComponent("PlayerMount");
                    hbSo.ApplyModifiedProperties();
                }

                var dd = hudGo.GetComponentInChildren<DamageDirectionUI>(true);
                if (dd != null && mainCamT != null)
                {
                    var ddSo = new SerializedObject(dd);
                    ddSo.FindProperty("health").objectReferenceValue = sidekickT.GetComponent<Health>();
                    ddSo.FindProperty("targetCamera").objectReferenceValue = mainCamT.GetComponent<Camera>();
                    ddSo.ApplyModifiedProperties();
                }
            }

            if (playerGo != null)
            {
                var pDodgeUI = hudGo.GetComponentInChildren<PlayerDodgeUI>(true);
                if (pDodgeUI != null)
                {
                    var pdSo = new SerializedObject(pDodgeUI);
                    pdSo.FindProperty("playerDodge").objectReferenceValue = playerGo.GetComponent<PlayerDodge>();
                    pdSo.ApplyModifiedProperties();
                }

                var ultUI = hudGo.GetComponentInChildren<UltimateBarUI>(true);
                if (ultUI != null)
                {
                    var uSo = new SerializedObject(ultUI);
                    uSo.FindProperty("ultimateController").objectReferenceValue = playerGo.GetComponent<PlayerUltimateController>();
                    uSo.ApplyModifiedProperties();
                }

                var summonCast = hudGo.GetComponentInChildren<SummonCastBarUI>(true);
                if (summonCast != null)
                {
                    var scSo = new SerializedObject(summonCast);
                    scSo.FindProperty("playerSummonMount").objectReferenceValue = playerGo.GetComponent<PlayerSummonMount>();
                    scSo.ApplyModifiedProperties();
                }
            }
        }

        // 5. GameMenu
        if (gameMenuGo != null)
        {
            var pauseCtrl = gameMenuGo.GetComponent<PauseMenuController>();
            if (pauseCtrl != null && sidekickT != null && camPivotT != null && mainCamT != null)
            {
                var pcSo = new SerializedObject(pauseCtrl);
                var ctdProp = pcSo.FindProperty("componentsToDisable");
                ctdProp.arraySize = 3;
                ctdProp.GetArrayElementAtIndex(0).objectReferenceValue = sidekickT.GetComponent("InputReader");
                ctdProp.GetArrayElementAtIndex(1).objectReferenceValue = camPivotT.GetComponent("PlayerCameraPivot");
                ctdProp.GetArrayElementAtIndex(2).objectReferenceValue = mainCamT.GetComponent("CinemachineBrain");
                pcSo.ApplyModifiedProperties();
            }

            var ssCtrl = gameMenuGo.GetComponent<ScreenshotModeController>();
            if (ssCtrl != null && mainCamT != null)
            {
                var ssSo = new SerializedObject(ssCtrl);
                ssSo.FindProperty("mainCamera").objectReferenceValue = mainCamT.GetComponent<Camera>();
                ssSo.FindProperty("flyCamera").objectReferenceValue = mainCamT.GetComponent("ScreenshotFlyCamera");
                if (dirLight != null) ssSo.FindProperty("sunLight").objectReferenceValue = dirLight;
                if (globalVol != null) ssSo.FindProperty("globalVolume").objectReferenceValue = globalVol;

                if (pauseMenuCanvasGo != null)
                {
                    var pmv = pauseMenuCanvasGo.GetComponentInChildren<PauseMenuView>(true);
                    if (pmv != null)
                    {
                        var hocProp = ssSo.FindProperty("hideOnCapture");
                        hocProp.arraySize = 1;
                        hocProp.GetArrayElementAtIndex(0).objectReferenceValue = pmv.GetComponent<CanvasGroup>();
                    }
                }
                if (hudGo != null)
                {
                    var cg = hudGo.GetComponent<CanvasGroup>();
                    if (cg != null) ssSo.FindProperty("hudCanvasGroup").objectReferenceValue = cg;
                }
                ssSo.ApplyModifiedProperties();
            }

            var gss = gameMenuGo.GetComponent<GameSettingsService>();
            if (gss != null && globalVol != null)
            {
                var gssSo = new SerializedObject(gss);
                gssSo.FindProperty("globalVolume").objectReferenceValue = globalVol;
                gssSo.ApplyModifiedProperties();
            }
        }

        // 6. DeathScreen
        if (deathScreenGo != null && sidekickT != null)
        {
            var skillTreeViews = deathScreenGo.GetComponentsInChildren<SkillTreeView>(true);
            foreach (var stv in skillTreeViews)
            {
                if (stv != null)
                {
                    var stvSo = new SerializedObject(stv);
                    stvSo.FindProperty("wallet").objectReferenceValue = sidekickT.GetComponent<Wallet>();
                    stvSo.ApplyModifiedProperties();
                }
            }
        }

        // 7. SurvivorsGameManager
        if (survivorsGameMgrGo != null)
        {
            var sgm = survivorsGameMgrGo.GetComponent<SurvivorsGameManager>();
            if (sgm != null)
            {
                var sgmSo = new SerializedObject(sgm);
                if (deathScreenGo != null) sgmSo.FindProperty("deathScreen").objectReferenceValue = deathScreenGo;
                if (enemySpawnerGo != null)
                {
                    Transform spT = enemySpawnerGo.transform.Find("Spawnpoints");
                    if (spT != null && spT.childCount > 8)
                    {
                        sgmSo.FindProperty("bossSpawnPoint").objectReferenceValue = spT.GetChild(8);
                    }
                }
                sgmSo.ApplyModifiedProperties();
            }

            var sls = survivorsGameMgrGo.GetComponent<SurvivorsLevelSystem>();
            if (sls != null && sidekickT != null)
            {
                var slsSo = new SerializedObject(sls);
                slsSo.FindProperty("wallet").objectReferenceValue = sidekickT.GetComponent<Wallet>();
                slsSo.ApplyModifiedProperties();
            }
        }

        // 8. EnemyIntroController
        if (enemyIntroGo != null)
        {
            var introCtrl = enemyIntroGo.GetComponent<EnemyIntroController>();
            if (introCtrl != null)
            {
                var icSo = new SerializedObject(introCtrl);
                if (cameraRigGo != null)
                {
                    Transform introCamT = cameraRigGo.transform.Find("Enemy Intro Camera");
                    if (introCamT != null)
                    {
                        var camComp = introCamT.GetComponent("CinemachineCamera");
                        if (camComp != null) icSo.FindProperty("introCamera").objectReferenceValue = camComp;
                    }
                }
                if (mainCamT != null)
                {
                    var brain = mainCamT.GetComponent("CinemachineBrain");
                    if (brain != null) icSo.FindProperty("cinemachineBrain").objectReferenceValue = brain;
                }
                if (hudGo != null)
                {
                    var introUI = hudGo.GetComponentInChildren<EnemyIntroUI>(true);
                    if (introUI != null) icSo.FindProperty("introUI").objectReferenceValue = introUI;

                    var bossHb = hudGo.GetComponentInChildren<BossHealthBarUI>(true);
                    if (bossHb != null) icSo.FindProperty("bossHealthBar").objectReferenceValue = bossHb;

                    var hideProp = icSo.FindProperty("objectsToHideDuringIntro");
                    hideProp.arraySize = 2;
                    Transform screenAdv = hudGo.transform.Find("Screen_HUD_Adventure_01");
                    Transform survHud = hudGo.transform.Find("Survivors HUD");
                    if (screenAdv != null) hideProp.GetArrayElementAtIndex(0).objectReferenceValue = screenAdv.gameObject;
                    if (survHud != null) hideProp.GetArrayElementAtIndex(1).objectReferenceValue = survHud.gameObject;
                }
                icSo.ApplyModifiedProperties();
            }
        }
    }
}
#endif
