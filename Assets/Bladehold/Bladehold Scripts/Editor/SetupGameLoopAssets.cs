#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupGameLoopAssets
{
    [MenuItem("Bladehold/Setup Game Loop Assets & Scenes")]
    public static void Execute()
    {
        CreateScriptableObjects();
        SetupBuildSettings();
        Debug.Log("[SetupGameLoopAssets] Completed setup of assets and build settings!");
    }

    public static void CreateScriptableObjects()
    {
        string baseDir = "Assets/Bladehold/Bladehold Config";
        if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

        string weaponsDir = baseDir + "/Weapons";
        if (!Directory.Exists(weaponsDir)) Directory.CreateDirectory(weaponsDir);

        string metaDir = baseDir + "/MetaPerks";
        if (!Directory.Exists(metaDir)) Directory.CreateDirectory(metaDir);

        string shopDir = baseDir + "/ShopItems";
        if (!Directory.Exists(shopDir)) Directory.CreateDirectory(shopDir);

        // 1. RoundPacingConfigSO
        string pacingPath = baseDir + "/SurvivorsRoundPacingConfig.asset";
        RoundPacingConfigSO pacing = AssetDatabase.LoadAssetAtPath<RoundPacingConfigSO>(pacingPath);
        if (pacing == null)
        {
            pacing = ScriptableObject.CreateInstance<RoundPacingConfigSO>();
            AssetDatabase.CreateAsset(pacing, pacingPath);
        }
        pacing.maxConcurrentEnemies = 20;
        pacing.spawnTelegraphDuration = 3.0f;
        pacing.spawnStaggerInterval = 0.35f;
        pacing.intermissionDuration = 30.0f;
        pacing.wavesPerRound = 3;
        pacing.totalRounds = 4;
        pacing.bossSpawnWave = 10;
        pacing.bossEnemyId = "slayer";

        string[] vfxGuids = AssetDatabase.FindAssets("vfx_RoundMarker02_Red");
        if (vfxGuids.Length > 0)
        {
            pacing.indicatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(vfxGuids[0]));
        }
        EditorUtility.SetDirty(pacing);

        // 2. WeaponDefinitionSO assets
        CreateWeaponAsset(weaponsDir, "sword", "Iron Longsword", WeaponCategory.Melee, true, 0, false, "Balanced melee blade delivering rapid sweeps.", "Hold attack to charge a sweeping 3-stage slash.", 0, 0.33f, "1H_Sword");
        CreateWeaponAsset(weaponsDir, "axe", "Battleaxe", WeaponCategory.Melee, false, 10, false, "Heavy two-handed axe that cleaves wide arcs.", "Hold attack to charge a crushing overhead cleave.", 1, 0.45f, "2H_Axe");
        CreateWeaponAsset(weaponsDir, "bow", "Recurve Bow", WeaponCategory.Ranged, true, 0, false, "Fast firing bow with piercing arrows.", "RMB Aim + LMB Fire.", 0, 0.25f, "Wep_RecurveBow_01");
        CreateWeaponAsset(weaponsDir, "throwing_axe", "Throwing Axe", WeaponCategory.Ranged, false, 10, false, "Heavy throwing axe with lethal velocity.", "RMB Aim + LMB to hurl spinning axe.", 0, 0.35f, "SM_Wep_Axe_01");
        CreateWeaponAsset(weaponsDir, "staff", "Arcane Staff", WeaponCategory.Ranged, false, 0, true, "Staff of primal magic.", "Locked for demo.", 0, 0.5f, "");
        CreateWeaponAsset(weaponsDir, "wand", "Crystal Wand", WeaponCategory.Ranged, false, 0, true, "Focusing wand of raw arcana.", "Locked for demo.", 0, 0.2f, "");

        // 3. MetaPerkDefinitionSO assets
        CreatePerkAsset(metaDir, "backstab", "Backstab", 1, 10, "Deal +20% bonus damage when striking enemies from behind.");
        CreatePerkAsset(metaDir, "agility", "Agility", 1, 10, "Increases maximum dash charges by +1.");
        CreatePerkAsset(metaDir, "regeneration", "Regeneration", 1, 10, "Restores +5 HP upon successfully surviving each wave.");
        CreatePerkAsset(metaDir, "second_wind", "Second Wind", 2, 25, "Revive once per run with 50% max HP upon death.");
        CreatePerkAsset(metaDir, "greed", "Greed", 2, 25, "Earn +10% more Gold from all combat and reward drops.");
        CreatePerkAsset(metaDir, "executioner", "Executioner", 2, 25, "Deal +50% bonus damage against enemies with under 50% HP.");
        CreatePerkAsset(metaDir, "master_tactician", "Master Tactician", 3, 50, "Gain 1 free card reroll per Rest Area draft.");
        CreatePerkAsset(metaDir, "war_chest", "War Chest", 3, 50, "Begin every combat run with 75 starting Gold.");
        CreatePerkAsset(metaDir, "deep_pockets", "Deep Pockets", 3, 50, "Rest Area Merchant offers 4 item slots instead of 3.");

        // 4. ShopItemSO assets
        CreateShopItemAsset(shopDir, "maggoty_bread", "Maggoty Bread", 5, "Instant snack restoring +5 HP.", ShopItemEffectType.HealInstant, 5f, 0);
        CreateShopItemAsset(shopDir, "troll_heart", "Troll Heart", 50, "Increases Max HP by +25 for the remainder of this run.", ShopItemEffectType.MaxHealthRun, 25f, 0);
        CreateShopItemAsset(shopDir, "crystal_water", "Crystal Water", 25, "Grants +20% movement speed for the next 5 waves.", ShopItemEffectType.MoveSpeedTemporary, 0.2f, 5);
        CreateShopItemAsset(shopDir, "special_herbs", "Special Herbs", 40, "Restores +5 HP at the end of each wave for 5 waves.", ShopItemEffectType.WaveEndHealTemporary, 5f, 5);

        AssetDatabase.SaveAssets();
    }

    private static void CreateWeaponAsset(string dir, string id, string name, WeaponCategory cat, bool defaultUnlocked, int metalCost, bool demoLocked, string desc, string chargeDesc, int animType, float chargeTime, string modelSearch)
    {
        string path = $"{dir}/{id}.asset";
        WeaponDefinitionSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(path);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
            AssetDatabase.CreateAsset(weapon, path);
        }
        weapon.id = id;
        weapon.displayName = name;
        weapon.category = cat;
        weapon.isUnlockedByDefault = defaultUnlocked;
        weapon.orcishMetalUnlockCost = metalCost;
        weapon.isLockedForDemo = demoLocked;
        weapon.description = desc;
        weapon.chargeDescription = chargeDesc;
        weapon.animatorWeaponType = animType;
        weapon.chargeTimePerLevel = chargeTime;

        if (!string.IsNullOrEmpty(modelSearch))
        {
            string[] guids = AssetDatabase.FindAssets(modelSearch + " t:Prefab t:Model");
            if (guids.Length > 0)
            {
                weapon.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
        EditorUtility.SetDirty(weapon);
    }

    private static void CreatePerkAsset(string dir, string id, string name, int tier, int cost, string desc)
    {
        string path = $"{dir}/{id}.asset";
        MetaPerkDefinitionSO perk = AssetDatabase.LoadAssetAtPath<MetaPerkDefinitionSO>(path);
        if (perk == null)
        {
            perk = ScriptableObject.CreateInstance<MetaPerkDefinitionSO>();
            AssetDatabase.CreateAsset(perk, path);
        }
        perk.id = id;
        perk.displayName = name;
        perk.tier = tier;
        perk.goblinBloodCost = cost;
        perk.description = desc;
        EditorUtility.SetDirty(perk);
    }

    private static void CreateShopItemAsset(string dir, string id, string name, int cost, string desc, ShopItemEffectType type, float val, int dur)
    {
        string path = $"{dir}/{id}.asset";
        ShopItemSO item = AssetDatabase.LoadAssetAtPath<ShopItemSO>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ShopItemSO>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.itemId = id;
        item.displayName = name;
        item.goldCost = cost;
        item.description = desc;
        item.effectType = type;
        item.effectValue = val;
        item.durationWaves = dur;
        EditorUtility.SetDirty(item);
    }

    public static void SetupBuildSettings()
    {
        string battleScene = "Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity";
        string restScene = "Assets/Bladehold/Bladehold Scenes/Bladehold Rest Area Scene.unity";
        string metaScene = "Assets/Bladehold/Bladehold Scenes/Bladehold Meta Area Scene.unity";

        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(battleScene, true),
            new EditorBuildSettingsScene(restScene, true),
            new EditorBuildSettingsScene(metaScene, true)
        };

        EditorBuildSettings.scenes = scenes;
        Debug.Log("[SetupGameLoopAssets] EditorBuildSettings scenes configured!");
    }

    [MenuItem("Bladehold/Configure Player Prefab Loadout")]
    public static void ConfigurePlayerPrefab()
    {
        string path = "Assets/Bladehold/Bladehold Prefabs/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("[SetupGameLoopAssets] Player.prefab not found at " + path);
            return;
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            GameObject root = scope.prefabContentsRoot;

            if (root.GetComponent<PlayerWeaponManager>() == null)
            {
                root.AddComponent<PlayerWeaponManager>();
            }

            if (root.GetComponent<PlayerInteraction>() == null)
            {
                root.AddComponent<PlayerInteraction>();
            }
        }

        Debug.Log("[SetupGameLoopAssets] Player.prefab updated with PlayerWeaponManager and PlayerInteraction!");
    }

    [MenuItem("Bladehold/Configure Survivors Scene GameLoop")]
    public static void ConfigureSurvivorsScene()
    {
        string scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        RoundPacingConfigSO pacing = AssetDatabase.LoadAssetAtPath<RoundPacingConfigSO>("Assets/Bladehold/Bladehold Config/SurvivorsRoundPacingConfig.asset");

        SurvivorsSpawner spawner = Object.FindAnyObjectByType<SurvivorsSpawner>();
        if (spawner != null)
        {
            var so = new SerializedObject(spawner);
            so.FindProperty("pacingConfig").objectReferenceValue = pacing;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
        }

        Gate gate = Object.FindAnyObjectByType<Gate>();
        Interactable gateInteractable = null;
        if (gate != null)
        {
            gateInteractable = gate.GetComponent<Interactable>();
            if (gateInteractable == null)
            {
                gateInteractable = gate.gameObject.AddComponent<Interactable>();
            }
            gateInteractable.PromptText = "Rest Area";
            gateInteractable.CanInteract = false;
            EditorUtility.SetDirty(gateInteractable);
        }

        GameObject managerGo = GameObject.Find("GameLoopManager");
        if (managerGo == null)
        {
            managerGo = new GameObject("GameLoopManager");
        }

        GameLoopManager loop = managerGo.GetComponent<GameLoopManager>();
        if (loop == null)
        {
            loop = managerGo.AddComponent<GameLoopManager>();
        }

        SurvivorsObjectiveManager objManager = Object.FindAnyObjectByType<SurvivorsObjectiveManager>();

        var loopSo = new SerializedObject(loop);
        loopSo.FindProperty("pacingConfig").objectReferenceValue = pacing;
        loopSo.FindProperty("spawner").objectReferenceValue = spawner;
        loopSo.FindProperty("castleGateInteractable").objectReferenceValue = gateInteractable;
        if (objManager != null)
        {
            loopSo.FindProperty("objectiveManager").objectReferenceValue = objManager;
        }
        loopSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(loop);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupGameLoopAssets] Bladehold Survivors Scene configured with GameLoopManager!");
    }

    [MenuItem("Bladehold/Create Rest Area Scene")]
    public static void CreateRestAreaScene()
    {
        string scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Rest Area Scene.unity";
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Environment
        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.92f, 0.82f);
        light.intensity = 1.3f;
        lightGo.transform.rotation = Quaternion.Euler(45f, 30f, 0f);

        // Ground Courtyard
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "RestArea_Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        Renderer groundRend = ground.GetComponent<Renderer>();
        if (groundRend != null)
        {
            groundRend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundRend.sharedMaterial.color = new Color(0.35f, 0.33f, 0.3f);
        }

        // 2. Player
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Player.prefab");
        if (playerPrefab != null)
        {
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0f, -8f);
            player.transform.rotation = Quaternion.identity;
        }

        // 3. Station 1: The Well
        GameObject wellGo = new GameObject("Station_1_Well");
        wellGo.transform.position = new Vector3(-8f, 0f, 0f);
        string[] wellGuids = AssetDatabase.FindAssets("SM_Prop_Well_01 t:Prefab");
        if (wellGuids.Length > 0)
        {
            GameObject wellMeshPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(wellGuids[0]));
            if (wellMeshPrefab != null)
            {
                GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(wellMeshPrefab, wellGo.transform);
                mesh.transform.localPosition = Vector3.zero;
            }
        }
        else
        {
            GameObject fallbackWell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fallbackWell.transform.SetParent(wellGo.transform, false);
            fallbackWell.transform.localScale = new Vector3(2f, 0.8f, 2f);
        }
        Interactable wellInteractable = wellGo.AddComponent<Interactable>();
        wellInteractable.PromptText = "Drink from Well (+20 HP)";
        wellGo.AddComponent<WellStation>();

        // 4. Station 2: The Shop
        GameObject shopGo = new GameObject("Station_2_Shop");
        shopGo.transform.position = new Vector3(8f, 0f, 0f);
        shopGo.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        string[] merchGuids = AssetDatabase.FindAssets("SM_Chr_Merchant_01 t:Prefab");
        if (merchGuids.Length > 0)
        {
            GameObject merchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(merchGuids[0]));
            if (merchPrefab != null)
            {
                GameObject mesh = (GameObject)PrefabUtility.InstantiatePrefab(merchPrefab, shopGo.transform);
                mesh.transform.localPosition = Vector3.zero;
            }
        }
        else
        {
            GameObject fallbackMerch = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallbackMerch.transform.SetParent(shopGo.transform, false);
            fallbackMerch.transform.localPosition = new Vector3(0f, 1f, 0f);
        }
        Interactable shopInteractable = shopGo.AddComponent<Interactable>();
        shopInteractable.PromptText = "Open Shop";
        shopGo.AddComponent<ShopStation>();

        // 5. Station 3: Draft Station
        GameObject draftGo = new GameObject("Station_3_DraftStation");
        draftGo.transform.position = new Vector3(0f, 0f, 8f);
        GameObject plinthDraft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plinthDraft.transform.SetParent(draftGo.transform, false);
        plinthDraft.transform.localScale = new Vector3(1.5f, 0.4f, 1.5f);
        Interactable draftInteractable = draftGo.AddComponent<Interactable>();
        draftInteractable.PromptText = "Draft Upgrades";
        draftGo.AddComponent<DraftStation>();

        // 6. Station 4: Exit Gate
        GameObject gateGo = new GameObject("Station_4_ExitGate");
        gateGo.transform.position = new Vector3(0f, 0f, 16f);
        GameObject gateMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gateMesh.transform.SetParent(gateGo.transform, false);
        gateMesh.transform.localScale = new Vector3(6f, 5f, 0.8f);
        gateMesh.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        Interactable gateInteractable = gateGo.AddComponent<Interactable>();
        gateInteractable.PromptText = "Return to Battle";
        gateGo.AddComponent<RestAreaGate>();

        // 7. ShopUI Modal Canvas
        CreateShopUICanvas();

        // 8. Common UI (Bladehold HUD, EventSystem, PauseMenuCanvas, GameMenu)
        AddCommonUI();

        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[SetupGameLoopAssets] Bladehold Rest Area Scene created and saved!");
    }

    private static void CreateShopUICanvas()
    {
        GameObject shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/ShopUI.prefab");
        if (shopPrefab != null)
        {
            PrefabUtility.InstantiatePrefab(shopPrefab);
        }
        else
        {
            Debug.LogWarning("[SetupGameLoopAssets] ShopUI.prefab not found at Assets/Bladehold/Bladehold Prefabs/UI/ShopUI.prefab");
        }
    }

    private static void AddCommonUI()
    {
        string[] prefabs = new string[]
        {
            "Assets/Bladehold/Bladehold Prefabs/UI/EventSystem.prefab",
            "Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab",
            "Assets/Bladehold/Bladehold Prefabs/UI/PauseMenuCanvas.prefab",
            "Assets/Bladehold/Bladehold Prefabs/UI/GameMenu.prefab"
        };

        foreach (var p in prefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab != null)
            {
                PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                Debug.LogWarning("[SetupGameLoopAssets] Common UI prefab not found at " + p);
            }
        }
    }

    [MenuItem("Bladehold/Create Meta Area Scene")]
    public static void CreateMetaAreaScene()
    {
        string scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Meta Area Scene.unity";
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Environment
        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.7f, 0.8f, 1.0f);
        light.intensity = 1.0f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -45f, 0f);

        // Ethereal Stone Courtyard
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "MetaArea_Courtyard";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        Renderer groundRend = ground.GetComponent<Renderer>();
        if (groundRend != null)
        {
            groundRend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundRend.sharedMaterial.color = new Color(0.2f, 0.22f, 0.28f);
        }

        // 2. Player
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Player.prefab");
        if (playerPrefab != null)
        {
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0f, -10f);
            player.transform.rotation = Quaternion.identity;
        }

        // 3. Spirit NPC (Shrine/Statue)
        GameObject spiritGo = new GameObject("Spirit_NPC");
        spiritGo.transform.position = new Vector3(0f, 0f, 10f);
        GameObject statuePlinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        statuePlinth.transform.SetParent(spiritGo.transform, false);
        statuePlinth.transform.localScale = new Vector3(2f, 1.2f, 2f);
        Interactable spiritInteractable = spiritGo.AddComponent<Interactable>();
        spiritInteractable.PromptText = "Commune with Spirit (Meta Upgrades)";
        spiritGo.AddComponent<SpiritNPC>();

        // 4. Weapon Pedestals (Sword, Axe, Bow, Throwing Axe)
        CreatePedestal("Pedestal_Sword", new Vector3(-6f, 0f, 0f), "Assets/Bladehold/Bladehold Config/Weapons/sword.asset");
        CreatePedestal("Pedestal_Axe", new Vector3(-2f, 0f, 0f), "Assets/Bladehold/Bladehold Config/Weapons/axe.asset");
        CreatePedestal("Pedestal_Bow", new Vector3(2f, 0f, 0f), "Assets/Bladehold/Bladehold Config/Weapons/bow.asset");
        CreatePedestal("Pedestal_ThrowingAxe", new Vector3(6f, 0f, 0f), "Assets/Bladehold/Bladehold Config/Weapons/throwing_axe.asset");

        // 5. Battle Portal
        GameObject portalGo = new GameObject("Battle_Portal");
        portalGo.transform.position = new Vector3(0f, 0f, 18f);
        GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arch.transform.SetParent(portalGo.transform, false);
        arch.transform.localScale = new Vector3(5f, 6f, 0.5f);
        arch.transform.localPosition = new Vector3(0f, 3f, 0f);
        Interactable portalInteractable = portalGo.AddComponent<Interactable>();
        portalInteractable.PromptText = "Begin Run (Enter Battle)";
        portalGo.AddComponent<BattlePortal>();

        // 6. MetaUpgradesUI Modal Canvas
        CreateMetaUpgradesUICanvas();

        // 7. Common UI (Bladehold HUD, EventSystem, PauseMenuCanvas, GameMenu)
        AddCommonUI();

        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[SetupGameLoopAssets] Bladehold Meta Area Scene created and saved!");
    }

    private static void CreatePedestal(string name, Vector3 pos, string weaponAssetPath)
    {
        GameObject pedGo = new GameObject(name);
        pedGo.transform.position = pos;

        // Plinth stone
        GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plinth.name = "Plinth";
        plinth.transform.SetParent(pedGo.transform, false);
        plinth.transform.localScale = new Vector3(1.6f, 0.8f, 1.6f);
        plinth.transform.localPosition = new Vector3(0f, 0.4f, 0f);

        // Mount point for 3D spinning weapon model
        GameObject mount = new GameObject("ModelMountPoint");
        mount.transform.SetParent(pedGo.transform, false);
        mount.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        // World-space canvas for labels
        GameObject worldCanvasGo = new GameObject("WorldUI", typeof(RectTransform));
        worldCanvasGo.transform.SetParent(pedGo.transform, false);
        worldCanvasGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        Canvas wc = worldCanvasGo.AddComponent<Canvas>();
        wc.renderMode = RenderMode.WorldSpace;
        RectTransform wcRect = worldCanvasGo.GetComponent<RectTransform>();
        wcRect.sizeDelta = new Vector2(250f, 120f);
        wcRect.localScale = Vector3.one * 0.01f;

        GameObject nameGo = new GameObject("NameLabel", typeof(RectTransform));
        nameGo.transform.SetParent(worldCanvasGo.transform, false);
        TMPro.TextMeshProUGUI nameTxt = nameGo.AddComponent<TMPro.TextMeshProUGUI>();
        nameTxt.text = "Weapon";
        nameTxt.fontSize = 26;
        nameTxt.alignment = TMPro.TextAlignmentOptions.Center;

        GameObject costGo = new GameObject("CostLabel", typeof(RectTransform));
        costGo.transform.SetParent(worldCanvasGo.transform, false);
        TMPro.TextMeshProUGUI costTxt = costGo.AddComponent<TMPro.TextMeshProUGUI>();
        costTxt.text = "";
        costTxt.fontSize = 20;
        costTxt.alignment = TMPro.TextAlignmentOptions.Center;
        costGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -30f);

        GameObject statusGo = new GameObject("StatusLabel", typeof(RectTransform));
        statusGo.transform.SetParent(worldCanvasGo.transform, false);
        TMPro.TextMeshProUGUI statusTxt = statusGo.AddComponent<TMPro.TextMeshProUGUI>();
        statusTxt.text = "";
        statusTxt.fontSize = 20;
        statusTxt.alignment = TMPro.TextAlignmentOptions.Center;
        statusGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -55f);

        Interactable interactable = pedGo.AddComponent<Interactable>();
        WeaponPedestal ped = pedGo.AddComponent<WeaponPedestal>();

        var so = new SerializedObject(ped);
        so.FindProperty("weaponData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(weaponAssetPath);
        so.FindProperty("modelMountPoint").objectReferenceValue = mount.transform;
        so.FindProperty("nameLabel").objectReferenceValue = nameTxt;
        so.FindProperty("costLabel").objectReferenceValue = costTxt;
        so.FindProperty("statusLabel").objectReferenceValue = statusTxt;
        so.ApplyModifiedProperties();
    }

    private static void CreateMetaUpgradesUICanvas()
    {
        GameObject canvasGo = new GameObject("MetaUpgradesCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        MetaUpgradesUI metaUI = canvasGo.AddComponent<MetaUpgradesUI>();

        GameObject root = new GameObject("WindowRoot", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        UnityEngine.UI.Image rootImg = root.AddComponent<UnityEngine.UI.Image>();
        rootImg.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1000f, 650f);

        // Currency text
        GameObject bloodGo = new GameObject("GoblinBloodText", typeof(RectTransform));
        bloodGo.transform.SetParent(root.transform, false);
        TMPro.TextMeshProUGUI bloodTxt = bloodGo.AddComponent<TMPro.TextMeshProUGUI>();
        bloodTxt.text = "Goblin Blood: 0";
        bloodTxt.fontSize = 26;
        bloodGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-300f, 275f);

        GameObject metalGo = new GameObject("OrcishMetalText", typeof(RectTransform));
        metalGo.transform.SetParent(root.transform, false);
        TMPro.TextMeshProUGUI metalTxt = metalGo.AddComponent<TMPro.TextMeshProUGUI>();
        metalTxt.text = "Orcish Metal: 0";
        metalTxt.fontSize = 26;
        metalGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(50f, 275f);

        // Close button
        GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform));
        closeGo.transform.SetParent(root.transform, false);
        UnityEngine.UI.Button closeBtn = closeGo.AddComponent<UnityEngine.UI.Button>();
        closeGo.AddComponent<UnityEngine.UI.Image>().color = new Color(0.8f, 0.2f, 0.2f);
        closeGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(450f, 275f);
        closeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 40f);

        // Tier rows
        GameObject t1 = new GameObject("Tier1_Row", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        t1.transform.SetParent(root.transform, false);
        t1.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 150f);
        t1.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing = 30f;

        GameObject t2 = new GameObject("Tier2_Row", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        t2.transform.SetParent(root.transform, false);
        t2.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -20f);
        t2.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing = 30f;

        GameObject t3 = new GameObject("Tier3_Row", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        t3.transform.SetParent(root.transform, false);
        t3.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -190f);
        t3.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing = 30f;

        var so = new SerializedObject(metaUI);
        so.FindProperty("windowRoot").objectReferenceValue = root;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("goblinBloodText").objectReferenceValue = bloodTxt;
        so.FindProperty("orcishMetalText").objectReferenceValue = metalTxt;
        so.FindProperty("tier1RowContainer").objectReferenceValue = t1.transform;
        so.FindProperty("tier2RowContainer").objectReferenceValue = t2.transform;
        so.FindProperty("tier3RowContainer").objectReferenceValue = t3.transform;

        SerializedProperty perksProp = so.FindProperty("allPerks");
        perksProp.ClearArray();
        string[] perkGuids = AssetDatabase.FindAssets("t:MetaPerkDefinitionSO");
        for (int i = 0; i < perkGuids.Length; i++)
        {
            perksProp.InsertArrayElementAtIndex(i);
            perksProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<MetaPerkDefinitionSO>(AssetDatabase.GUIDToAssetPath(perkGuids[i]));
        }

        so.ApplyModifiedProperties();
    }

    [MenuItem("Bladehold/Run Full Game Loop Integration Test")]
    public static string RunFullGameLoopIntegrationTest()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== BLADEHOLD FULL GAME LOOP INTEGRATION TEST ===");

        try
        {
            // -------------------------------------------------------------
            // PHASE 1: Fresh Run & Multi-Wave Pacing in Battle Scene
            // -------------------------------------------------------------
            sb.AppendLine("\n[PHASE 1: Battle Scene - Multi-Wave Pacing]");
            RunSession.StartNewRun();
            sb.AppendLine($"- Initialized fresh run: CurrentWave={RunSession.CurrentWave}, CurrentRound={RunSession.CurrentRound}, InRunGold={RunSession.InRunGold}");

            string battleScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity";
            EditorSceneManager.OpenScene(battleScenePath, OpenSceneMode.Single);

            GameLoopManager glm = Object.FindAnyObjectByType<GameLoopManager>();
            if (glm == null) throw new System.Exception("GameLoopManager not found in Survivors Scene!");

            Gate gate = Object.FindAnyObjectByType<Gate>();
            Interactable gateInteractable = gate != null ? gate.GetComponent<Interactable>() : null;
            if (gateInteractable == null) throw new System.Exception("Gate Interactable not found in Survivors Scene!");

            // Wave 1
            glm.StartWave(1);
            glm.DebugCompleteObjective();
            sb.AppendLine($"- Wave 1 Started. Target Kills: {glm.TargetKillsThisWave}");
            // Simulate kills
            for (int k = 0; k < glm.TargetKillsThisWave; k++) glm.OnEnemyKilled(null);
            sb.AppendLine($"- Wave 1 Kill Quota Satisfied ({glm.KillsThisWave}/{glm.TargetKillsThisWave}). InRunGold earned: {RunSession.InRunGold}");

            // Wave 2
            glm.StartWave(2);
            glm.DebugCompleteObjective();
            for (int k = 0; k < glm.TargetKillsThisWave; k++) glm.OnEnemyKilled(null);
            sb.AppendLine($"- Wave 2 Cleared. CurrentWave={glm.CurrentWave}, Round={glm.CurrentRound}");

            // Wave 3 (Round 1 Rest Wave)
            glm.StartWave(3);
            glm.DebugCompleteObjective();
            for (int k = 0; k < glm.TargetKillsThisWave; k++) glm.OnEnemyKilled(null);
            bool isRestGateOpen = gateInteractable.CanInteract;
            sb.AppendLine($"- Wave 3 (Round 1 Finale) Cleared. Rest Gate Interactable: {isRestGateOpen} (Expected: True)");
            if (!isRestGateOpen) throw new System.Exception("Castle Gate was not unlocked on Rest Wave 3!");

            // -------------------------------------------------------------
            // PHASE 2: Area Transition & Rest Area Stations
            // -------------------------------------------------------------
            sb.AppendLine("\n[PHASE 2: Rest Area Scene & Stations]");
            string restScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Rest Area Scene.unity";
            EditorSceneManager.OpenScene(restScenePath, OpenSceneMode.Single);
            RunSession.RestVisitsCount++;

            Player restPlayer = Object.FindAnyObjectByType<Player>();
            if (restPlayer == null) throw new System.Exception("Player not found in Rest Area Scene!");

            // Test Well Station
            WellStation well = Object.FindAnyObjectByType<WellStation>();
            if (well == null) throw new System.Exception("WellStation not found in Rest Area Scene!");
            well.Initialize();
            Interactable wellInteractable = well.GetComponent<Interactable>();
            if (wellInteractable == null) throw new System.Exception("Well Interactable not found in Rest Area Scene!");

            Health restHealth = restPlayer.GetComponent<Health>();
            restHealth.SetMaxHealth(100f);
            restHealth.Revive(100f);
            Damage testDmg = new Damage { value = 40f, isPlayerDamage = false };
            restHealth.ReceiveDamage(testDmg);
            float hpBeforeWell = restHealth.CurrentHealth; // 60
            wellInteractable.Interact(restPlayer);
            float hpAfterWell = restHealth.CurrentHealth; // 80
            bool wellDepleted = !wellInteractable.CanInteract;
            sb.AppendLine($"- Well: Before={hpBeforeWell} HP, After={hpAfterWell} HP (Expected: 80 HP). Depleted={wellDepleted}");
            if (hpAfterWell != 80f || !wellDepleted) throw new System.Exception("Well did not heal or deplete properly!");

            // Test Shop Station
            ShopUI shopUI = Object.FindAnyObjectByType<ShopUI>();
            if (shopUI == null) throw new System.Exception("ShopUI not found in Rest Area Scene!");
            RunSession.AddInRunGold(100);
            shopUI.OpenShop();
            shopUI.RefreshUI();
            int goldBeforeBuy = RunSession.InRunGold;
            // Purchase slot 0
            var buyMethod = typeof(ShopUI).GetMethod("HandleBuyAttempt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (buyMethod != null) buyMethod.Invoke(shopUI, new object[] { 0, null });
            int goldAfterBuy = RunSession.InRunGold;
            sb.AppendLine($"- Shop: Gold before purchase={goldBeforeBuy}, after purchase={goldAfterBuy} (Deducted={goldBeforeBuy > goldAfterBuy})");
            shopUI.CloseShop();

            // Test Exit Gate
            RestAreaGate exitGate = Object.FindAnyObjectByType<RestAreaGate>();
            if (exitGate == null) throw new System.Exception("RestAreaGate not found in Rest Area Scene!");
            exitGate.Initialize();
            exitGate.ReturnToBattle(restPlayer);
            sb.AppendLine($"- Exit Gate Interacted. Next Wave Target: {RunSession.CurrentWave} (Expected: 4, Round 2)");

            // -------------------------------------------------------------
            // PHASE 3: Round 2 Enemy Progression (Big Ork unlocked)
            // -------------------------------------------------------------
            sb.AppendLine("\n[PHASE 3: Battle Scene - Round 2 Enemy Roster]");
            EditorSceneManager.OpenScene(battleScenePath, OpenSceneMode.Single);
            GameLoopManager glmRound2 = Object.FindAnyObjectByType<GameLoopManager>();
            glmRound2.StartWave(4);
            sb.AppendLine($"- Wave 4 Started. CurrentRound: {glmRound2.CurrentRound} (Expected: 2)");

            RoundPacingConfigSO pacing = AssetDatabase.LoadAssetAtPath<RoundPacingConfigSO>("Assets/Bladehold/Bladehold Config/SurvivorsRoundPacingConfig.asset");
            RoundPacingConfigSO.RoundDefinition round2Def = pacing != null ? pacing.GetRound(2) : null;
            bool hasBigOrk = round2Def != null && System.Array.IndexOf(round2Def.allowedEnemyIds, "big_ork") >= 0;
            sb.AppendLine($"- Round 2 Allowed Enemy Roster contains 'big_ork': {hasBigOrk} (Expected: True)");
            if (!hasBigOrk) throw new System.Exception("Round 2 does not contain big_ork in allowed roster!");

            // -------------------------------------------------------------
            // PHASE 4: Death Sequence & Run Clear
            // -------------------------------------------------------------
            sb.AppendLine("\n[PHASE 4: Death Sequence]");
            Player battlePlayer = Object.FindAnyObjectByType<Player>();
            int goldBeforeDeath = RunSession.InRunGold;
            RunSession.ClearRun();
            sb.AppendLine($"- Run Session Cleared: InRunGold={RunSession.InRunGold} (was {goldBeforeDeath}), CurrentWave={RunSession.CurrentWave}");

            // -------------------------------------------------------------
            // PHASE 5: Meta Progression Area & Weapon Loadout
            // -------------------------------------------------------------
            sb.AppendLine("\n[PHASE 5: Meta Progression Area Scene]");
            string metaScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Meta Area Scene.unity";
            EditorSceneManager.OpenScene(metaScenePath, OpenSceneMode.Single);

            // Grant test meta currencies
            SaveData save = SaveSystem.Load();
            save.goblinBlood = 50;
            save.orcishMetal = 20;
            save.unlockedMetaTier = 1;
            save.unlockedWeapons = new System.Collections.Generic.List<string> { "sword", "bow" };
            save.equippedMeleeWeapon = "sword";
            save.purchasedMetaPerks.Clear();
            SaveSystem.Save(save);

            // Test Meta Upgrades UI
            MetaUpgradesUI metaUI = Object.FindAnyObjectByType<MetaUpgradesUI>();
            if (metaUI == null) throw new System.Exception("MetaUpgradesUI not found in Meta Area Scene!");
            metaUI.Open();

            // Purchase Perk: backstab (10 Blood)
            MetaPerkDefinitionSO backstabPerk = AssetDatabase.LoadAssetAtPath<MetaPerkDefinitionSO>("Assets/Bladehold/Bladehold Config/MetaPerks/backstab.asset");
            var purchaseMethod = typeof(MetaUpgradesUI).GetMethod("PurchasePerk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (purchaseMethod != null && backstabPerk != null) purchaseMethod.Invoke(metaUI, new object[] { backstabPerk });

            SaveData postPerkSave = SaveSystem.Load();
            bool hasBackstab = postPerkSave.purchasedMetaPerks.Contains("backstab");
            sb.AppendLine($"- Purchased 'backstab' perk: Owned={hasBackstab}, Remaining Blood={postPerkSave.goblinBlood} (Expected: 40)");

            // Unlock Tier 2 (5 Metal)
            var unlockTierMethod = typeof(MetaUpgradesUI).GetMethod("UnlockTier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (unlockTierMethod != null) unlockTierMethod.Invoke(metaUI, new object[] { 2, 5 });
            SaveData postTierSave = SaveSystem.Load();
            sb.AppendLine($"- Unlocked Meta Tier 2: CurrentTier={postTierSave.unlockedMetaTier}, Remaining Metal={postTierSave.orcishMetal} (Expected: 15)");
            metaUI.Close();

            // Test Weapon Pedestal (Axe)
            WeaponPedestal[] pedestals = Object.FindObjectsByType<WeaponPedestal>(FindObjectsSortMode.None);
            WeaponPedestal axePedestal = System.Array.Find(pedestals, p => p.WeaponData != null && p.WeaponData.id == "axe");
            if (axePedestal == null) throw new System.Exception("Axe Pedestal not found in Meta Area Scene!");

            Player metaPlayer = Object.FindAnyObjectByType<Player>();
            axePedestal.Initialize();
            axePedestal.RefreshPedestal();

            // Unlock Axe (10 Metal)
            axePedestal.OnPedestalInteracted(metaPlayer);
            SaveData postAxeUnlock = SaveSystem.Load();
            bool isAxeUnlocked = postAxeUnlock.unlockedWeapons.Contains("axe");
            sb.AppendLine($"- Unlocked Battleaxe on Pedestal: Unlocked={isAxeUnlocked}, Remaining Metal={postAxeUnlock.orcishMetal} (Expected: 5)");

            // Equip Axe
            axePedestal.OnPedestalInteracted(metaPlayer);
            SaveData postAxeEquip = SaveSystem.Load();
            sb.AppendLine($"- Equipped Battleaxe on Pedestal: Equipped Melee={postAxeEquip.equippedMeleeWeapon} (Expected: 'axe')");

            // Test Battle Portal
            BattlePortal portal = Object.FindAnyObjectByType<BattlePortal>();
            if (portal == null) throw new System.Exception("BattlePortal not found in Meta Area Scene!");
            portal.Initialize();
            portal.EnterBattle(metaPlayer);
            sb.AppendLine($"- Battle Portal Activated: Next Run Started. RunSession InRunGold={RunSession.InRunGold}");

            // Return to Battle Scene to confirm active loadout reflects new equipped axe
            EditorSceneManager.OpenScene(battleScenePath, OpenSceneMode.Single);
            Player newRunPlayer = Object.FindAnyObjectByType<Player>();
            PlayerWeaponManager pwm = newRunPlayer != null ? newRunPlayer.GetComponent<PlayerWeaponManager>() : null;
            if (pwm != null)
            {
                pwm.ApplySavedLoadout();
                sb.AppendLine($"- New Run Loaded in Battle Scene. Active Melee Weapon: {pwm.CurrentMeleeId} (Expected: 'axe')");
            }

            sb.AppendLine("\n>>> ALL TESTS PASSED SUCCESSFULLY! <<<");
        }
        catch (System.Exception ex)
        {
            sb.AppendLine($"\n>>> TEST FAILED: {ex.Message} <<<\nStack: {ex.StackTrace}");
        }

        Debug.Log(sb.ToString());
        return sb.ToString();
    }

    [MenuItem("Bladehold/Create Training Dummy Goblin & Place in Scenes")]
    public static void CreateTrainingDummyPrefabAndPlaceInScenes()
    {
        string goblinPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Goblin Enemy Variant.prefab";
        string dummyPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Training Dummy Goblin.prefab";

        GameObject sourceGoblin = AssetDatabase.LoadAssetAtPath<GameObject>(goblinPrefabPath);
        if (sourceGoblin == null) throw new System.Exception("Source Goblin prefab not found!");

        GameObject dummyGo = PrefabUtility.InstantiatePrefab(sourceGoblin) as GameObject;
        PrefabUtility.UnpackPrefabInstance(dummyGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        dummyGo.name = "Training Dummy Goblin";

        // Remove offensive / economy / despawn components
        Object.DestroyImmediate(dummyGo.GetComponent<AIAttack>());
        Object.DestroyImmediate(dummyGo.GetComponent<AIMovement>());
        Object.DestroyImmediate(dummyGo.GetComponent<AITargetSelector>());
        Object.DestroyImmediate(dummyGo.GetComponent<CoinDropper>());
        Object.DestroyImmediate(dummyGo.GetComponent<PowerupDropper>());
        Object.DestroyImmediate(dummyGo.GetComponent<GoldenGoblin>());
        Object.DestroyImmediate(dummyGo.GetComponent<ImpulseGoblin>());
        Object.DestroyImmediate(dummyGo.GetComponent<CorpseDespawner>());

        // Disable Enemy component so it doesn't count towards wave/objective tallies
        Enemy enemyComp = dummyGo.GetComponent<Enemy>();
        if (enemyComp != null) enemyComp.enabled = false;

        // Configure Health
        Health health = dummyGo.GetComponent<Health>();
        if (health != null)
        {
            var hSo = new SerializedObject(health);
            var maxHpProp = hSo.FindProperty("maxHealth");
            if (maxHpProp != null) maxHpProp.floatValue = 1000f;
            hSo.ApplyModifiedProperties();
            health.SetMaxHealth(1000f);
            health.Revive(1000f);
        }

        // Add TrainingDummy component
        TrainingDummy dummyComp = dummyGo.AddComponent<TrainingDummy>();

        // Create HealthText child
        GameObject textGo = new GameObject("HealthText");
        textGo.transform.SetParent(dummyGo.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        TMPro.TextMeshPro tmp = textGo.AddComponent<TMPro.TextMeshPro>();
        tmp.fontSize = 2.8f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.text = "<b>Training Dummy</b>\n<color=#FF5555>1000</color> / 1000 HP";

        // Wire TrainingDummy serialized fields
        SerializedObject so = new SerializedObject(dummyComp);
        so.FindProperty("maxHealth").floatValue = 1000f;
        so.FindProperty("resetIdleDelay").floatValue = 10f;
        so.FindProperty("poofVfxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Smoke_White_Small_01.prefab");
        so.FindProperty("poofSfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Audio/Attacks/Fantasy_Game_Magic_Organic_Poof_Buff_Hit_5.wav");
        so.FindProperty("healthText").objectReferenceValue = tmp;
        so.ApplyModifiedProperties();

        // Save as prefab
        PrefabUtility.SaveAsPrefabAsset(dummyGo, dummyPrefabPath);
        Object.DestroyImmediate(dummyGo);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupGameLoopAssets] Training Dummy Goblin prefab created at: " + dummyPrefabPath);

        GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dummyPrefabPath);

        // Place in Rest Area Scene
        string restScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Rest Area Scene.unity";
        var restScene = EditorSceneManager.OpenScene(restScenePath, OpenSceneMode.Single);
        GameObject existingRestDummy = GameObject.Find("TrainingDummy_RestArea");
        if (existingRestDummy != null) Object.DestroyImmediate(existingRestDummy);

        GameObject restInstance = PrefabUtility.InstantiatePrefab(dummyPrefab) as GameObject;
        restInstance.name = "TrainingDummy_RestArea";
        restInstance.transform.position = new Vector3(-5f, 0f, -3f);
        restInstance.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        EditorUtility.SetDirty(restInstance);
        EditorSceneManager.MarkSceneDirty(restScene);
        EditorSceneManager.SaveScene(restScene);
        Debug.Log("[SetupGameLoopAssets] Placed Training Dummy in Rest Area Scene at (-5, 0, -3)");

        // Place in Meta Area Scene
        string metaScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Meta Area Scene.unity";
        var metaScene = EditorSceneManager.OpenScene(metaScenePath, OpenSceneMode.Single);
        GameObject existingMetaDummy = GameObject.Find("TrainingDummy_MetaArea");
        if (existingMetaDummy != null) Object.DestroyImmediate(existingMetaDummy);

        GameObject metaInstance = PrefabUtility.InstantiatePrefab(dummyPrefab) as GameObject;
        metaInstance.name = "TrainingDummy_MetaArea";
        metaInstance.transform.position = new Vector3(-6f, 0f, -4f);
        metaInstance.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        EditorUtility.SetDirty(metaInstance);
        EditorSceneManager.MarkSceneDirty(metaScene);
        EditorSceneManager.SaveScene(metaScene);
        Debug.Log("[SetupGameLoopAssets] Placed Training Dummy in Meta Area Scene at (-6, 0, -4)");
    }
}
#endif
