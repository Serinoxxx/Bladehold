using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DefensesRevampSetup
{
    private const string DEFENSES_FOLDER = "Assets/Bladehold/Bladehold Prefabs/Defenses";

    [MenuItem("Bladehold/Defenses/Run Complete Defenses Setup")]
    public static void RunSetupMenuItem()
    {
        RunSetup();
    }

    public static string RunSetup()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Running Complete Defenses Revamp Setup ===");

        if (!AssetDatabase.IsValidFolder(DEFENSES_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Bladehold/Bladehold Prefabs", "Defenses");
            sb.AppendLine("- Created folder: " + DEFENSES_FOLDER);
        }

        AudioClip woodImpactSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/HAMMER_Hit_Wood_Shield_stereo.wav");
        AudioClip woodBreakSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/HAMMER_Hit_Wood_Shield_Break_stereo.wav");
        AudioClip repairSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Skill Tree/Upgrade Mid Tier Item A.wav");
        AudioClip upgradeSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Skill Tree/Upgrade Legendary Tier Item A.wav");
        GameObject holyLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piloto Studio/Ultimate Loot VFX Pack/VFX Prefabs/Thematic Lootbeams/Lootbeam_Holy.prefab");

        // 1. Create Catapult Boulder Projectile
        string catProjPath = $"{DEFENSES_FOLDER}/CatapultBoulder.prefab";
        GameObject catProjPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(catProjPath);
        if (catProjPrefab == null)
        {
            GameObject boulderObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boulderObj.name = "CatapultBoulder";
            boulderObj.transform.localScale = Vector3.one * 0.8f;
            CatapultProjectile cp = boulderObj.AddComponent<CatapultProjectile>();
            catProjPrefab = PrefabUtility.SaveAsPrefabAsset(boulderObj, catProjPath);
            Object.DestroyImmediate(boulderObj);
            sb.AppendLine("- Created CatapultBoulder prefab");
        }

        // 2. Create Ballista Bolt Projectile
        string balProjPath = $"{DEFENSES_FOLDER}/BallistaBolt.prefab";
        GameObject balProjPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(balProjPath);
        if (balProjPrefab == null)
        {
            GameObject boltSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/SiegeEngines/SM_Wep_Ballista_Projectile_01.prefab");
            GameObject boltObj = boltSource != null ? Object.Instantiate(boltSource) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boltObj.name = "BallistaBolt";
            BallistaBoltProjectile bp = boltObj.AddComponent<BallistaBoltProjectile>();
            balProjPrefab = PrefabUtility.SaveAsPrefabAsset(boltObj, balProjPath);
            Object.DestroyImmediate(boltObj);
            sb.AppendLine("- Created BallistaBolt prefab");
        }

        // 3. Create Arrow Tower Prefab
        string arrowTowerPath = $"{DEFENSES_FOLDER}/Defense_ArrowTower.prefab";
        GameObject arrowTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(arrowTowerPath);
        if (arrowTowerPrefab == null)
        {
            GameObject towerModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/Buildings/SM_Bld_Wooden_Tower_01.prefab");
            GameObject towerObj = towerModel != null ? Object.Instantiate(towerModel) : new GameObject("Defense_ArrowTower");
            towerObj.name = "Defense_ArrowTower";
            ArrowTowerDefense atd = towerObj.AddComponent<ArrowTowerDefense>();
            SetSerializedField(atd, "repairSfx", repairSfx);
            SetSerializedField(atd, "upgradeSfx", upgradeSfx);
            SetSerializedField(atd, "breakSfx", woodBreakSfx);
            GameObject arrowProj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Fort/FortArrowProjectile.prefab");
            SetSerializedField(atd, "arrowPrefab", arrowProj);
            arrowTowerPrefab = PrefabUtility.SaveAsPrefabAsset(towerObj, arrowTowerPath);
            Object.DestroyImmediate(towerObj);
            sb.AppendLine("- Created Defense_ArrowTower prefab");
        }

        // 4. Create Catapult Prefab
        string catapultPath = $"{DEFENSES_FOLDER}/Defense_Catapult.prefab";
        GameObject catapultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(catapultPath);
        if (catapultPrefab == null)
        {
            GameObject catModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/SiegeEngines/SM_Wep_Catapult_01.prefab");
            GameObject catObj = catModel != null ? Object.Instantiate(catModel) : new GameObject("Defense_Catapult");
            catObj.name = "Defense_Catapult";
            CatapultDefense cd = catObj.AddComponent<CatapultDefense>();
            SetSerializedField(cd, "repairSfx", repairSfx);
            SetSerializedField(cd, "upgradeSfx", upgradeSfx);
            SetSerializedField(cd, "breakSfx", woodBreakSfx);
            SetSerializedField(cd, "projectilePrefab", catProjPrefab);
            catapultPrefab = PrefabUtility.SaveAsPrefabAsset(catObj, catapultPath);
            Object.DestroyImmediate(catObj);
            sb.AppendLine("- Created Defense_Catapult prefab");
        }

        // 5. Create Ballista Prefab
        string ballistaPath = $"{DEFENSES_FOLDER}/Defense_Ballista.prefab";
        GameObject ballistaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ballistaPath);
        if (ballistaPrefab == null)
        {
            GameObject balModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/SiegeEngines/SM_Wep_Ballista_Mounted_01.prefab");
            GameObject balObj = balModel != null ? Object.Instantiate(balModel) : new GameObject("Defense_Ballista");
            balObj.name = "Defense_Ballista";
            BallistaDefense bd = balObj.AddComponent<BallistaDefense>();
            SetSerializedField(bd, "repairSfx", repairSfx);
            SetSerializedField(bd, "upgradeSfx", upgradeSfx);
            SetSerializedField(bd, "breakSfx", woodBreakSfx);
            SetSerializedField(bd, "boltPrefab", balProjPrefab);
            ballistaPrefab = PrefabUtility.SaveAsPrefabAsset(balObj, ballistaPath);
            Object.DestroyImmediate(balObj);
            sb.AppendLine("- Created Defense_Ballista prefab");
        }

        // 6. Create Net Thrower Prefab
        string netThrowerPath = $"{DEFENSES_FOLDER}/Defense_NetThrower.prefab";
        GameObject netThrowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(netThrowerPath);
        if (netThrowerPrefab == null)
        {
            GameObject mortarModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/SiegeEngines/SM_Wep_Mortar_01.prefab");
            GameObject netObj = mortarModel != null ? Object.Instantiate(mortarModel) : new GameObject("Defense_NetThrower");
            netObj.name = "Defense_NetThrower";
            NetThrowerDefense ntd = netObj.AddComponent<NetThrowerDefense>();
            SetSerializedField(ntd, "repairSfx", repairSfx);
            SetSerializedField(ntd, "upgradeSfx", upgradeSfx);
            SetSerializedField(ntd, "breakSfx", woodBreakSfx);
            netThrowerPrefab = PrefabUtility.SaveAsPrefabAsset(netObj, netThrowerPath);
            Object.DestroyImmediate(netObj);
            sb.AppendLine("- Created Defense_NetThrower prefab");
        }

        // 7. Create Spike Trap Prefab
        string spikeTrapPath = $"{DEFENSES_FOLDER}/Defense_SpikeTrap.prefab";
        GameObject spikeTrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spikeTrapPath);
        if (spikeTrapPrefab == null)
        {
            GameObject spikesModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Fort/Fort_Spikes.prefab");
            GameObject spikeObj = spikesModel != null ? Object.Instantiate(spikesModel) : new GameObject("Defense_SpikeTrap");
            spikeObj.name = "Defense_SpikeTrap";
            // Remove legacy FortDefense / SpikeDefense
            FortDefense legacyFd = spikeObj.GetComponent<FortDefense>();
            if (legacyFd != null) Object.DestroyImmediate(legacyFd);
            SpikeTrapDefense std = spikeObj.AddComponent<SpikeTrapDefense>();
            SetSerializedField(std, "repairSfx", repairSfx);
            SetSerializedField(std, "upgradeSfx", upgradeSfx);
            SetSerializedField(std, "breakSfx", woodBreakSfx);
            SetSerializedField(std, "impaleSfx", woodImpactSfx);
            spikeTrapPrefab = PrefabUtility.SaveAsPrefabAsset(spikeObj, spikeTrapPath);
            Object.DestroyImmediate(spikeObj);
            sb.AppendLine("- Created Defense_SpikeTrap prefab");
        }

        // 8. Create Oil Vat Prefab
        string oilVatPath = $"{DEFENSES_FOLDER}/Defense_OilVat.prefab";
        GameObject oilVatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(oilVatPath);
        if (oilVatPrefab == null)
        {
            GameObject oilModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Fort/Fort_BoilingOil.prefab");
            GameObject oilObj = oilModel != null ? Object.Instantiate(oilModel) : new GameObject("Defense_OilVat");
            oilObj.name = "Defense_OilVat";
            FortDefense legacyFd = oilObj.GetComponent<FortDefense>();
            if (legacyFd != null) Object.DestroyImmediate(legacyFd);
            OilVatDefense ovd = oilObj.AddComponent<OilVatDefense>();
            SetSerializedField(ovd, "repairSfx", repairSfx);
            SetSerializedField(ovd, "upgradeSfx", upgradeSfx);
            SetSerializedField(ovd, "breakSfx", woodBreakSfx);
            GameObject oilZone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Fort/BurningOilZone.prefab");
            SetSerializedField(ovd, "oilPoolVfxPrefab", oilZone);
            oilVatPrefab = PrefabUtility.SaveAsPrefabAsset(oilObj, oilVatPath);
            Object.DestroyImmediate(oilObj);
            sb.AppendLine("- Created Defense_OilVat prefab");
        }

        // 9. Create Tower Plot Prefab
        string plotPrefabPath = $"{DEFENSES_FOLDER}/TowerPlot.prefab";
        GameObject plotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(plotPrefabPath);
        if (plotPrefab == null)
        {
            GameObject plotObj = new GameObject("TowerPlot");
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "PlotFoundationPad";
            pad.transform.SetParent(plotObj.transform, false);
            pad.transform.localScale = new Vector3(3.2f, 0.08f, 3.2f);
            pad.transform.localPosition = new Vector3(0f, 0.04f, 0f);

            // Give it a stone / wood material
            Material stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonFantasyKingdom/Materials/PolygonFantasyKingdom_Mat_01_A.mat");
            if (stoneMat != null && pad.TryGetComponent(out Renderer rend))
            {
                rend.sharedMaterial = stoneMat;
            }

            TowerPlot tp = plotObj.AddComponent<TowerPlot>();
            tp.SetPrefabs(arrowTowerPrefab, catapultPrefab, ballistaPrefab, netThrowerPrefab, spikeTrapPrefab, oilVatPrefab);
            SetSerializedField(tp, "holyLightVfxPrefab", holyLightPrefab);
            SetSerializedField(tp, "woodImpactSfx", woodImpactSfx);

            plotPrefab = PrefabUtility.SaveAsPrefabAsset(plotObj, plotPrefabPath);
            Object.DestroyImmediate(plotObj);
            sb.AppendLine("- Created TowerPlot prefab");
        }
        else
        {
            TowerPlot tp = plotPrefab.GetComponent<TowerPlot>();
            if (tp != null)
            {
                tp.SetPrefabs(arrowTowerPrefab, catapultPrefab, ballistaPrefab, netThrowerPrefab, spikeTrapPrefab, oilVatPrefab);
                SetSerializedField(tp, "holyLightVfxPrefab", holyLightPrefab);
                SetSerializedField(tp, "woodImpactSfx", woodImpactSfx);
                EditorUtility.SetDirty(plotPrefab);
            }
        }

        AssetDatabase.SaveAssets();

        // 10. Configure Scene (SupplyUI, BuildWheelUI, 6 TowerPlots)
        SetupSceneObjects(arrowTowerPrefab, catapultPrefab, ballistaPrefab, netThrowerPrefab, spikeTrapPrefab, oilVatPrefab, plotPrefab, sb);

        sb.AppendLine("=== Setup Complete! ===");
        return sb.ToString();
    }

    private static void SetupSceneObjects(GameObject arrowTower, GameObject catapult, GameObject ballista, GameObject netThrower, GameObject spikeTrap, GameObject oilVat, GameObject plotPrefab, System.Text.StringBuilder sb)
    {
        // 10A. Setup SupplyUI in HUD
        CoinUI coinUI = Object.FindAnyObjectByType<CoinUI>(FindObjectsInactive.Include);
        Transform currenciesRoot = coinUI != null ? coinUI.transform.parent : GameObject.Find("Bladehold HUD/Screen_HUD_Adventure_01/ScreenSpace/Top Left/Currencies")?.transform;
        if (currenciesRoot != null)
        {
            Transform existingSupply = currenciesRoot.Find("SupplyUI");
            if (existingSupply == null)
            {
                Transform metalUI = currenciesRoot.Find("MetalUI") ?? coinUI.transform;
                GameObject supplyObj = metalUI != null ? Object.Instantiate(metalUI.gameObject, currenciesRoot) : new GameObject("SupplyUI");
                supplyObj.name = "SupplyUI";

                // Remove OrcishMetalUI or other currency scripts
                Component oldComp = supplyObj.GetComponent("OrcishMetalUI");
                if (oldComp != null) Object.DestroyImmediate(oldComp);

                SupplyUI sui = supplyObj.GetComponent<SupplyUI>() ?? supplyObj.AddComponent<SupplyUI>();

                // Set Hammer Icon
                Image iconImg = supplyObj.transform.Find("ICON")?.GetComponent<Image>();
                Sprite hammerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Resources/ICON_SM_Item_Hammer_01.png");
                if (iconImg != null && hammerSprite != null)
                {
                    iconImg.sprite = hammerSprite;
                }

                TMP_Text label = supplyObj.transform.Find("Label_CurrencyValue")?.GetComponent<TMP_Text>()
                    ?? supplyObj.GetComponentInChildren<TMP_Text>(true);
                SetSerializedField(sui, "label", label);

                EditorUtility.SetDirty(supplyObj);
                sb.AppendLine("- Added SupplyUI to HUD Top Left Currencies");
            }
        }

        // 10B. Setup BuildWheelUI in Bladehold HUD
        GameObject hud = GameObject.Find("Bladehold HUD");
        if (hud != null)
        {
            Transform existingWheel = hud.transform.Find("BuildWheelModal");
            if (existingWheel == null)
            {
                GameObject wheelModal = new GameObject("BuildWheelModal", typeof(RectTransform));
                wheelModal.transform.SetParent(hud.transform, false);

                RectTransform modalRect = wheelModal.GetComponent<RectTransform>();
                modalRect.anchorMin = Vector2.zero;
                modalRect.anchorMax = Vector2.one;
                modalRect.sizeDelta = Vector2.zero;

                // Dark Backdrop Image
                Image bg = wheelModal.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.65f);

                // Center Container
                GameObject centerObj = new GameObject("CenterContainer", typeof(RectTransform));
                centerObj.transform.SetParent(wheelModal.transform, false);
                RectTransform centerRect = centerObj.GetComponent<RectTransform>();
                centerRect.sizeDelta = new Vector3(600f, 600f);

                // Header Text
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

                // Supply Total Label
                GameObject supplyObj = new GameObject("SupplyTotal", typeof(RectTransform));
                supplyObj.transform.SetParent(centerObj.transform, false);
                RectTransform supRect = supplyObj.GetComponent<RectTransform>();
                supRect.anchoredPosition = new Vector2(0f, 220f);
                supRect.sizeDelta = new Vector2(400f, 35f);
                TMP_Text supText = supplyObj.AddComponent<TextMeshProUGUI>();
                supText.text = "Available Supply: 60";
                supText.fontSize = 20;
                supText.alignment = TextAlignmentOptions.Center;

                // Description Label
                GameObject descObj = new GameObject("DescriptionText", typeof(RectTransform));
                descObj.transform.SetParent(centerObj.transform, false);
                RectTransform descRect = descObj.GetComponent<RectTransform>();
                descRect.anchoredPosition = new Vector2(0f, -220f);
                descRect.sizeDelta = new Vector2(500f, 60f);
                TMP_Text descText = descObj.AddComponent<TextMeshProUGUI>();
                descText.text = "Choose a defense structure to protect the gates.";
                descText.fontSize = 16;
                descText.alignment = TextAlignmentOptions.Center;

                // Close / Cancel Button
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

                // 6 Radial Slice Buttons
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
                    float angleDeg = 90f - i * 60f; // Top clockwise
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
                SetSerializedField(bwUI, "wheelPanel", wheelModal);
                SetSerializedField(bwUI, "headerText", headText);
                SetSerializedField(bwUI, "supplyLabel", supText);
                SetSerializedField(bwUI, "descriptionLabel", descText);
                SetSerializedField(bwUI, "closeButton", closeBtn);
                SetSerializedField(bwUI, "sliceButtons", buttons);

                wheelModal.SetActive(false);
                EditorUtility.SetDirty(hud);
                sb.AppendLine("- Created BuildWheelModal in Bladehold HUD");
            }
        }

        // 10C. Setup Battlefield Tower Plots
        GameObject plotsRoot = GameObject.Find("Battlefield Tower Plots");
        if (plotsRoot == null)
        {
            plotsRoot = new GameObject("Battlefield Tower Plots");
            plotsRoot.AddComponent<TowerPlotManager>();
            sb.AppendLine("- Created Battlefield Tower Plots root with TowerPlotManager");
        }

        TowerPlotManager manager = plotsRoot.GetComponent<TowerPlotManager>() ?? plotsRoot.AddComponent<TowerPlotManager>();

        Vector3[] plotPositions = new Vector3[]
        {
            new Vector3(45.0f, -5.98f, 32.0f),  // Plot 0: Gate left
            new Vector3(60.0f, -5.69f, 32.0f),  // Plot 1: Gate right
            new Vector3(38.0f, -5.88f, 18.0f),  // Plot 2: West midfield
            new Vector3(67.0f, -6.00f, 18.0f),  // Plot 3: East midfield
            new Vector3(48.0f, -6.00f, 5.0f),   // Plot 4: Forward approach left
            new Vector3(58.0f, -5.92f, 5.0f)    // Plot 5: Forward approach right
        };

        for (int i = 0; i < plotPositions.Length; i++)
        {
            string plotName = $"TowerPlot_{i + 1}";
            Transform existingPlot = plotsRoot.transform.Find(plotName);
            if (existingPlot == null)
            {
                GameObject newPlot = plotPrefab != null ? PrefabUtility.InstantiatePrefab(plotPrefab) as GameObject : new GameObject(plotName);
                newPlot.name = plotName;
                newPlot.transform.SetParent(plotsRoot.transform, true);
                newPlot.transform.position = plotPositions[i];
                newPlot.transform.rotation = Quaternion.identity;

                TowerPlot tp = newPlot.GetComponent<TowerPlot>() ?? newPlot.AddComponent<TowerPlot>();
                tp.PlotIndex = i;
                tp.SetPrefabs(arrowTower, catapult, ballista, netThrower, spikeTrap, oilVat);

                manager.RegisterPlot(tp);
                sb.AppendLine($"- Positioned {plotName} at {plotPositions[i]}");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        sb.AppendLine("- Saved modified Bladehold Survivors Scene");
    }

    private static void SetSerializedField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
