#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;

public static class BatteringRamSetup
{
    private const string SyntyRammerPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/SiegeEngines/SM_Wep_Rammer_01.prefab";
    private const string TargetBatteringRamPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Objectives/BatteringRam.prefab";
    private const string SurvivorsObjectivesPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Objectives/SurvivorsObjectives.prefab";
    private const string SurvivorsScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity";

    [MenuItem("Bladehold/Setup Battering Ram Objective")]
    public static void Execute()
    {
        Debug.Log("[BatteringRamSetup] Starting Battering Ram setup...");
        GameObject ramPrefab = CreateBatteringRamPrefab();
        if (ramPrefab != null)
        {
            SetupSurvivorsObjectivesPrefab(ramPrefab);
            SetupSurvivorsScene(ramPrefab);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BatteringRamSetup] Finished Battering Ram setup successfully!");
    }

    public static GameObject CreateBatteringRamPrefab()
    {
        string dir = Path.GetDirectoryName(TargetBatteringRamPrefabPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SyntyRammerPrefabPath);
        if (sourcePrefab == null)
        {
            Debug.LogError($"[BatteringRamSetup] Source prefab not found at {SyntyRammerPrefabPath}");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        instance.name = "BatteringRam";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) instance.layer = enemyLayer;

        // 1. NavMeshAgent
        NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
        if (agent == null) agent = instance.AddComponent<NavMeshAgent>();
        agent.speed = 1.5f;
        agent.stoppingDistance = 4.5f;
        agent.radius = 1.5f;
        agent.height = 2.5f;
        agent.avoidancePriority = 10;
        agent.autoBraking = true;

        // 2. BoxCollider for player targeting & weapon hits
        BoxCollider boxCol = instance.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = instance.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0f, 1.5f, 0f);
        boxCol.size = new Vector3(3.5f, 3.0f, 5.5f);
        boxCol.isTrigger = false;

        // 3. Health component
        Health health = instance.GetComponent<Health>();
        if (health == null) health = instance.AddComponent<Health>();
        health.SetMaxHealth(1000f);
        health.ImmuneToPlayerDamage = false;

        // 4. Ram Log transform
        Transform logTransform = instance.transform.Find("SM_Wep_Rammer_Log_01");
        if (logTransform == null)
        {
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Log"))
                {
                    logTransform = child;
                    break;
                }
            }
        }

        // 5. Impact Point transform at the head of the log
        Transform impactPoint = null;
        if (logTransform != null)
        {
            Transform existingImpact = logTransform.Find("ImpactPoint");
            if (existingImpact != null)
            {
                impactPoint = existingImpact;
            }
            else
            {
                GameObject ipGo = new GameObject("ImpactPoint");
                ipGo.transform.SetParent(logTransform, false);
                ipGo.transform.localPosition = new Vector3(0f, 0f, 2.5f);
                impactPoint = ipGo.transform;
            }
        }

        // 6. Wheels transforms
        List<Transform> wheels = new List<Transform>();
        string[] wheelNames = new string[]
        {
            "SM_Wep_Rammer_Wheel_fl",
            "SM_Wep_Rammer_Wheel_fr",
            "SM_Wep_Rammer_Wheel_rl_01",
            "SM_Wep_Rammer_Wheel_rl_02",
            "SM_Wep_Rammer_Wheel_rr_01",
            "SM_Wep_Rammer_Wheel_rr_02"
        };
        foreach (string wName in wheelNames)
        {
            Transform w = instance.transform.Find(wName);
            if (w != null) wheels.Add(w);
        }

        // 7. Range Circle
        Transform rangeCircle = instance.transform.Find("RangeCircle");
        Renderer rangeCircleRenderer = null;
        if (rangeCircle == null)
        {
            GameObject circleGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            circleGo.name = "RangeCircle";
            circleGo.transform.SetParent(instance.transform, false);
            circleGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            circleGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            circleGo.transform.localScale = new Vector3(12f, 12f, 1f);

            // Destroy collider on circle
            Collider cCol = circleGo.GetComponent<Collider>();
            if (cCol != null) Object.DestroyImmediate(cCol);

            rangeCircle = circleGo.transform;
            rangeCircleRenderer = circleGo.GetComponent<Renderer>();

            Material circleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bladehold/Art/Materials/M_RangeCircle.mat");
            if (circleMat == null)
            {
                // Fallback to guid
                string circleMatPath = AssetDatabase.GUIDToAssetPath("990564def3ee92f44bff0ba2576e544e");
                if (!string.IsNullOrEmpty(circleMatPath))
                {
                    circleMat = AssetDatabase.LoadAssetAtPath<Material>(circleMatPath);
                }
            }
            if (circleMat != null && rangeCircleRenderer != null)
            {
                rangeCircleRenderer.sharedMaterial = circleMat;
            }
        }
        else
        {
            rangeCircleRenderer = rangeCircle.GetComponent<Renderer>();
        }

        // 8. Movement Audio
        Transform rollingTrans = instance.transform.Find("Rolling");
        AudioSource audioSource = null;
        if (rollingTrans == null)
        {
            GameObject rollingGo = new GameObject("Rolling");
            rollingGo.transform.SetParent(instance.transform, false);
            audioSource = rollingGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 40f;
            audioSource.volume = 0.8f;

            string rollSoundPath = AssetDatabase.GUIDToAssetPath("647cfb442b9d81a4db736d40199691cb");
            if (!string.IsNullOrEmpty(rollSoundPath))
            {
                audioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(rollSoundPath);
            }
        }
        else
        {
            audioSource = rollingTrans.GetComponent<AudioSource>();
        }

        // 9. MMF_Player for camera impulse & shake
        MMF_Player mmf = instance.GetComponent<MMF_Player>();
        if (mmf == null) mmf = instance.AddComponent<MMF_Player>();

        if (mmf.FeedbacksList == null)
        {
            mmf.FeedbacksList = new List<MMF_Feedback>();
        }
        else
        {
            mmf.FeedbacksList.Clear();
        }

        var impulseFeedback = new MMF_CinemachineImpulse();
        impulseFeedback.Label = "Impact Cinemachine Impulse";
        impulseFeedback.Timing = new MMFeedbackTiming();
        impulseFeedback.Velocity = new Vector3(0f, -1.2f, 0.5f);
        impulseFeedback.Owner = mmf;
        mmf.FeedbacksList.Add(impulseFeedback);

        var shakeFeedback = new MMF_CameraShake();
        shakeFeedback.Label = "Impact Camera Shake";
        shakeFeedback.CameraShakeProperties = new MMCameraShakeProperties(0.35f, 0.6f, 25f);
        shakeFeedback.Owner = mmf;
        mmf.FeedbacksList.Add(shakeFeedback);

        // 10. Load sound & VFX assets
        AudioClip impactSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("886dd441091c1974ead974fbbefa1324")); // cinematic_deep_boom_impact_01
        GameObject impactVfx = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("2b28f2fdc5a65964f96ea4d2b9d6ca63")); // FX_Impact_Wood_01
        AudioClip deathSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("456c4d2a40621f847ac0204e9ab2d521")); // Wood Break Large A
        GameObject deathVfx = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("101d62f0a3e4fef4eab61adb79c845df")); // FX_Impact_Large_01

        // 11. BatteringRam component
        BatteringRam ramComp = instance.GetComponent<BatteringRam>();
        if (ramComp == null) ramComp = instance.AddComponent<BatteringRam>();

        SerializedObject so = new SerializedObject(ramComp);
        so.FindProperty("maxHealth").floatValue = 1000f;
        so.FindProperty("moveSpeed").floatValue = 1.5f;
        so.FindProperty("pushRadius").floatValue = 6.0f;
        so.FindProperty("arrivalThreshold").floatValue = 4.5f;
        so.FindProperty("gateDamage").floatValue = 50f;
        so.FindProperty("ramInterval").floatValue = 5.0f;
        so.FindProperty("ramLogTransform").objectReferenceValue = logTransform;
        so.FindProperty("impactPoint").objectReferenceValue = impactPoint;
        so.FindProperty("impactFeedback").objectReferenceValue = mmf;
        so.FindProperty("impactSound").objectReferenceValue = impactSfx;
        so.FindProperty("impactVfxPrefab").objectReferenceValue = impactVfx;
        so.FindProperty("rangeCircleTransform").objectReferenceValue = rangeCircle;
        so.FindProperty("rangeCircleRenderer").objectReferenceValue = rangeCircleRenderer;
        so.FindProperty("wheelRotationSpeed").floatValue = 120f;
        so.FindProperty("movementAudioSource").objectReferenceValue = audioSource;
        so.FindProperty("deathVfxPrefab").objectReferenceValue = deathVfx;
        so.FindProperty("deathSound").objectReferenceValue = deathSfx;
        so.FindProperty("destroyDelay").floatValue = 0.5f;

        SerializedProperty wheelsProp = so.FindProperty("wheels");
        wheelsProp.arraySize = wheels.Count;
        for (int i = 0; i < wheels.Count; i++)
        {
            wheelsProp.GetArrayElementAtIndex(i).objectReferenceValue = wheels[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, TargetBatteringRamPrefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log($"[BatteringRamSetup] Created BatteringRam prefab at {TargetBatteringRamPrefabPath}");
        return savedPrefab;
    }

    public static void SetupSurvivorsObjectivesPrefab(GameObject ramPrefab)
    {
        using (var scope = new PrefabUtility.EditPrefabContentsScope(SurvivorsObjectivesPrefabPath))
        {
            GameObject root = scope.prefabContentsRoot;
            if (root == null)
            {
                Debug.LogError("[BatteringRamSetup] Failed to load SurvivorsObjectives prefab contents");
                return;
            }

            ProtectWagonObjective wagonObj = root.GetComponentInChildren<ProtectWagonObjective>();
            Transform spawnPoint = null;
            Transform gateDest = null;
            Sprite destIcon = null;

            if (wagonObj != null)
            {
                SerializedObject wagonSo = new SerializedObject(wagonObj);
                spawnPoint = wagonSo.FindProperty("wagonSpawnPoint").objectReferenceValue as Transform;
                gateDest = wagonSo.FindProperty("gateDestinationPoint").objectReferenceValue as Transform;
                destIcon = wagonSo.FindProperty("destinationWaypointIcon").objectReferenceValue as Sprite;
            }

            Sprite ramIcon = null;
            DestroySiegeEnginesObjective siegeObj = root.GetComponentInChildren<DestroySiegeEnginesObjective>();
            if (siegeObj != null)
            {
                SerializedObject siegeSo = new SerializedObject(siegeObj);
                ramIcon = siegeSo.FindProperty("siegeEngineWaypointIcon").objectReferenceValue as Sprite;
            }

            StopBatteringRamObjective ramObjective = root.GetComponentInChildren<StopBatteringRamObjective>();
            if (ramObjective == null)
            {
                // Attach to same GameObject as the other objectives
                GameObject targetHolder = wagonObj != null ? wagonObj.gameObject : root;
                ramObjective = targetHolder.AddComponent<StopBatteringRamObjective>();
            }

            SerializedObject ramObjSo = new SerializedObject(ramObjective);
            ramObjSo.FindProperty("objectiveId").stringValue = "stop_battering_ram";
            ramObjSo.FindProperty("title").stringValue = "Stop the Battering Ram";
            ramObjSo.FindProperty("description").stringValue = "Destroy the battering ram before it breaches the gate!";
            ramObjSo.FindProperty("batteringRamPrefab").objectReferenceValue = ramPrefab;
            if (spawnPoint != null) ramObjSo.FindProperty("ramSpawnPoint").objectReferenceValue = spawnPoint;
            if (gateDest != null) ramObjSo.FindProperty("gateDestinationPoint").objectReferenceValue = gateDest;
            if (destIcon != null) ramObjSo.FindProperty("destinationWaypointIcon").objectReferenceValue = destIcon;
            if (ramIcon != null) ramObjSo.FindProperty("ramWaypointIcon").objectReferenceValue = ramIcon;
            ramObjSo.ApplyModifiedPropertiesWithoutUndo();

            // Add to SurvivorsObjectiveManager repeatingObjectiveComponents
            SurvivorsObjectiveManager objManager = root.GetComponentInChildren<SurvivorsObjectiveManager>();
            if (objManager != null)
            {
                SerializedObject managerSo = new SerializedObject(objManager);
                SerializedProperty repeatingList = managerSo.FindProperty("repeatingObjectiveComponents");
                bool alreadyInList = false;
                for (int i = 0; i < repeatingList.arraySize; i++)
                {
                    if (repeatingList.GetArrayElementAtIndex(i).objectReferenceValue == ramObjective)
                    {
                        alreadyInList = true;
                        break;
                    }
                }
                if (!alreadyInList)
                {
                    repeatingList.InsertArrayElementAtIndex(repeatingList.arraySize);
                    repeatingList.GetArrayElementAtIndex(repeatingList.arraySize - 1).objectReferenceValue = ramObjective;
                    managerSo.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[BatteringRamSetup] Added StopBatteringRamObjective to SurvivorsObjectiveManager pool in prefab");
                }
            }
        }
        Debug.Log("[BatteringRamSetup] Successfully updated SurvivorsObjectives.prefab");
    }

    public static void SetupSurvivorsScene(GameObject ramPrefab)
    {
        if (!File.Exists(SurvivorsScenePath)) return;

        Scene scene = EditorSceneManager.OpenScene(SurvivorsScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) return;

        SurvivorsObjectiveManager manager = Object.FindFirstObjectByType<SurvivorsObjectiveManager>();
        if (manager != null)
        {
            StopBatteringRamObjective ramObjective = manager.GetComponentInChildren<StopBatteringRamObjective>();
            if (ramObjective == null)
            {
                ProtectWagonObjective wagonObj = manager.GetComponentInChildren<ProtectWagonObjective>();
                GameObject holder = wagonObj != null ? wagonObj.gameObject : manager.gameObject;
                ramObjective = holder.AddComponent<StopBatteringRamObjective>();
            }

            ProtectWagonObjective wagon = manager.GetComponentInChildren<ProtectWagonObjective>();
            Transform spawnPoint = null;
            Transform gateDest = null;
            Sprite destIcon = null;

            if (wagon != null)
            {
                SerializedObject wagonSo = new SerializedObject(wagon);
                spawnPoint = wagonSo.FindProperty("wagonSpawnPoint").objectReferenceValue as Transform;
                gateDest = wagonSo.FindProperty("gateDestinationPoint").objectReferenceValue as Transform;
                destIcon = wagonSo.FindProperty("destinationWaypointIcon").objectReferenceValue as Sprite;
            }

            Sprite ramIcon = null;
            DestroySiegeEnginesObjective siegeObj = manager.GetComponentInChildren<DestroySiegeEnginesObjective>();
            if (siegeObj != null)
            {
                SerializedObject siegeSo = new SerializedObject(siegeObj);
                ramIcon = siegeSo.FindProperty("siegeEngineWaypointIcon").objectReferenceValue as Sprite;
            }

            SerializedObject ramObjSo = new SerializedObject(ramObjective);
            ramObjSo.FindProperty("objectiveId").stringValue = "stop_battering_ram";
            ramObjSo.FindProperty("title").stringValue = "Stop the Battering Ram";
            ramObjSo.FindProperty("description").stringValue = "Destroy the battering ram before it breaches the gate!";
            ramObjSo.FindProperty("batteringRamPrefab").objectReferenceValue = ramPrefab;
            if (spawnPoint != null) ramObjSo.FindProperty("ramSpawnPoint").objectReferenceValue = spawnPoint;
            if (gateDest != null) ramObjSo.FindProperty("gateDestinationPoint").objectReferenceValue = gateDest;
            if (destIcon != null) ramObjSo.FindProperty("destinationWaypointIcon").objectReferenceValue = destIcon;
            if (ramIcon != null) ramObjSo.FindProperty("ramWaypointIcon").objectReferenceValue = ramIcon;
            ramObjSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject managerSo = new SerializedObject(manager);
            SerializedProperty repeatingList = managerSo.FindProperty("repeatingObjectiveComponents");
            bool alreadyInList = false;
            for (int i = 0; i < repeatingList.arraySize; i++)
            {
                if (repeatingList.GetArrayElementAtIndex(i).objectReferenceValue == ramObjective)
                {
                    alreadyInList = true;
                    break;
                }
            }
            if (!alreadyInList)
            {
                repeatingList.InsertArrayElementAtIndex(repeatingList.arraySize);
                repeatingList.GetArrayElementAtIndex(repeatingList.arraySize - 1).objectReferenceValue = ramObjective;
                managerSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[BatteringRamSetup] Added StopBatteringRamObjective to SurvivorsObjectiveManager in scene");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BatteringRamSetup] Successfully updated and saved Bladehold Survivors Scene.unity");
        }
    }
}
#endif
