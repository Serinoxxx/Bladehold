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
using MoreMountains.Tools;

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

        // 10. Health Bar setup (MMHealthBar + HealthBarUI)
        Transform healthBarTrans = instance.transform.Find("HealthBar");
        if (healthBarTrans == null)
        {
            GameObject hbGo = new GameObject("HealthBar");
            hbGo.transform.SetParent(instance.transform, false);
            hbGo.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            healthBarTrans = hbGo.transform;
        }

        MMHealthBar mmHealthBar = healthBarTrans.GetComponent<MMHealthBar>();
        if (mmHealthBar == null) mmHealthBar = healthBarTrans.gameObject.AddComponent<MMHealthBar>();
        mmHealthBar.HealthBarType = MMHealthBar.HealthBarTypes.Drawn;
        mmHealthBar.Size = new Vector2(2.5f, 0.35f);
        mmHealthBar.BackgroundPadding = new Vector2(0.02f, 0.02f);
        mmHealthBar.Billboard = true;
        mmHealthBar.AlwaysVisible = true;
        mmHealthBar.HideBarAtZero = true;
        mmHealthBar.HideBarAtZeroDelay = 0.2f;
        mmHealthBar.LerpFrontBar = true;
        mmHealthBar.LerpFrontBarSpeed = 15f;
        mmHealthBar.LerpDelayedBar = true;
        mmHealthBar.LerpDelayedBarSpeed = 15f;

        HealthBarUI healthBarUI = healthBarTrans.GetComponent<HealthBarUI>();
        if (healthBarUI == null) healthBarUI = healthBarTrans.gameObject.AddComponent<HealthBarUI>();
        SerializedObject hbSo = new SerializedObject(healthBarUI);
        hbSo.FindProperty("health").objectReferenceValue = health;
        hbSo.FindProperty("healthBar").objectReferenceValue = mmHealthBar;
        hbSo.FindProperty("followHead").boolValue = false;
        hbSo.ApplyModifiedPropertiesWithoutUndo();

        // 11. MMF_Player for hit feedback (flicker red, shield bash sfx, wood splinters)
        Transform hitFeedbackTrans = instance.transform.Find("HitFeedback");
        if (hitFeedbackTrans == null)
        {
            GameObject hitGo = new GameObject("HitFeedback");
            hitGo.transform.SetParent(instance.transform, false);
            hitFeedbackTrans = hitGo.transform;
        }

        MMF_Player hitMmf = hitFeedbackTrans.GetComponent<MMF_Player>();
        if (hitMmf == null) hitMmf = hitFeedbackTrans.gameObject.AddComponent<MMF_Player>();

        if (hitMmf.FeedbacksList == null)
        {
            hitMmf.FeedbacksList = new List<MMF_Feedback>();
        }
        else
        {
            hitMmf.FeedbacksList.Clear();
        }

        // 11a. Red Mesh Flicker
        Renderer mainRenderer = instance.GetComponentInChildren<MeshRenderer>();
        var flickerFeedback = new MMF_Flicker();
        flickerFeedback.Label = "Hit Red Flicker";
        flickerFeedback.Timing = new MMFeedbackTiming();
        flickerFeedback.BoundRenderer = mainRenderer;
        flickerFeedback.Mode = MMF_Flicker.Modes.PropertyName;
        flickerFeedback.PropertyName = "_BaseColor";
        flickerFeedback.FlickerDuration = 0.18f;
        flickerFeedback.FlickerPeriod = 0.04f;
        flickerFeedback.FlickerColor = new Color(1f, 0.1f, 0.1f, 1f);
        flickerFeedback.Owner = hitMmf;

        List<Renderer> extraRenderers = new List<Renderer>();
        foreach (var r in instance.GetComponentsInChildren<MeshRenderer>())
        {
            if (r != mainRenderer && r.gameObject.name != "RangeCircle")
            {
                extraRenderers.Add(r);
            }
        }
        flickerFeedback.ExtraBoundRenderers = extraRenderers;
        hitMmf.FeedbacksList.Add(flickerFeedback);

        // 11b. Shield Bash SFX
        AudioClip shieldHitSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("9bfa76be563d5e244926ba2641b4a5d7")); // shield_hit_001.wav
        if (shieldHitSfx == null)
        {
            shieldHitSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/shield_hit_001.wav");
        }
        if (shieldHitSfx != null)
        {
            var soundFeedback = new MMF_MMSoundManagerSound();
            soundFeedback.Label = "Shield Bash Sound";
            soundFeedback.Timing = new MMFeedbackTiming();
            soundFeedback.Sfx = shieldHitSfx;
            soundFeedback.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            soundFeedback.MinVolume = 0.9f;
            soundFeedback.MaxVolume = 1.0f;
            soundFeedback.MinPitch = 0.92f;
            soundFeedback.MaxPitch = 1.08f;
            soundFeedback.Owner = hitMmf;
            hitMmf.FeedbacksList.Add(soundFeedback);
        }

        // 11c. Wood Splinter VFX
        GameObject woodVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("2b28f2fdc5a65964f96ea4d2b9d6ca63")); // FX_Impact_Wood_01
        if (woodVfxPrefab != null)
        {
            var particlesFeedback = new MMF_ParticlesInstantiation();
            particlesFeedback.Label = "Wood Splinter VFX";
            particlesFeedback.Timing = new MMFeedbackTiming();
            particlesFeedback.Mode = MMF_ParticlesInstantiation.Modes.Cached;
            particlesFeedback.PositionMode = MMF_ParticlesInstantiation.PositionModes.Script;
            particlesFeedback.ParticlesPrefab = woodVfxPrefab.GetComponent<ParticleSystem>() ?? woodVfxPrefab.GetComponentInChildren<ParticleSystem>();
            particlesFeedback.NestParticles = false;
            particlesFeedback.Owner = hitMmf;
            hitMmf.FeedbacksList.Add(particlesFeedback);
        }

        // Wire damageFeedback on Health
        SerializedObject healthSo = new SerializedObject(health);
        SerializedProperty dmgFeedbackProp = healthSo.FindProperty("damageFeedback");
        if (dmgFeedbackProp != null)
        {
            dmgFeedbackProp.objectReferenceValue = hitMmf;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // 12. Load sound & VFX assets

        // 13. BatteringRam component
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
        so.FindProperty("hitFeedback").objectReferenceValue = hitMmf;
        so.FindProperty("rangeCircleTransform").objectReferenceValue = rangeCircle;
        so.FindProperty("rangeCircleRenderer").objectReferenceValue = rangeCircleRenderer;
        so.FindProperty("wheelRotationSpeed").floatValue = 120f;
        so.FindProperty("movementAudioSource").objectReferenceValue = audioSource;
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
