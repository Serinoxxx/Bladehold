using System.Collections.Generic;
using System.IO;
using System.Text;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Builds the plan-17 fort assets: wall art wrappers (Synty pieces re-pivoted to base-centre, 5 m
///     segments), <see cref="WallConfigSO" />, <see cref="FortUpgradeConfigSO" />,
///     <see cref="CrystalConfigSO" />, and the Wall, WallPlot, TowerSpikeRing, WallIcyWaterZone,
///     CrystalPickup and FortRules prefabs. The Wall holds one hand-editable model (ShortWallWithGate when it
///     exists, else a row of stone segments at <see cref="ReferenceWidth" />) plus rubble, spikes and fixtures,
///     and is nested in WallPlot.prefab as the plot's template; fit it per scene by hand from there. Tiers are
///     material swaps (Castle_Wall_01/02/03 on <see cref="WallConfigSO.tiers" />). These are **placeholders** for Lance to re-skin: a re-run only
///     creates what's missing and never overwrites an existing prefab (swapped models survive) unless
///     <c>Build(rebuild: true)</c>. Config assets are created once and only have empty references filled. MMF players are cloned from the Arrow Tower's (repair/upgrade/break) and the ammo pickup's
///     as placeholders; their content is a human-taste item (plans/editor/17-walls-and-upgrade-wheel.md).
///
///     Menu: Bladehold/Fort/Build Wall & Upgrade Assets. Agents: <c>FortWallAssetsBuilder.Build()</c>.
/// </summary>
public static class FortWallAssetsBuilder
{
    private const string PrefabDir = "Assets/Bladehold/Bladehold Prefabs/Defenses/Walls";
    private const string ArtDir = PrefabDir + "/Art";
    private const string ConfigDir = "Assets/Bladehold/Config/Fort";
    public const string FortUpgradeConfigPath = ConfigDir + "/FortUpgradeConfig.asset";
    public const string WallConfigPath = ConfigDir + "/WallConfig.asset";
    public const string CrystalConfigPath = ConfigDir + "/CrystalConfig.asset";

    private const string Fk = "Assets/Synty/PolygonFantasyKingdom/Prefabs/";
    private const string Dn = "Assets/Synty/PolygonDungeon/Prefabs/";
    private const string ArrowTowerPath = "Assets/Bladehold/Bladehold Prefabs/Defenses/Defense_ArrowTower.prefab";
    private const string AmmoPickupPath = "Assets/Bladehold/Bladehold Prefabs/Powerups/AmmoPickup.prefab";
    private const string GoldPopupPath = "Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Gold.prefab";
    private const string SpreadPopupPath = "Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Spread Up 3.prefab";

    private const float SegmentLength = 5f;
    private const float DoorWidth = 3f;
    private const float WallHeight = 5f;
    /// <summary>Width the fallback model is laid out at (WallPlot's default); each scene's walls are then fitted by hand.</summary>
    private const float ReferenceWidth = 10f;
    private const string ModelPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Buildings/ShortWallWithGate.prefab";
    private const string PortcullisName = "SM_Bld_Castle_Wall_Gate_Portcullis_01";
    private const string TierMaterialDir = "Assets/Synty/PolygonFantasyKingdom/Materials/Walls/";
    private const string WallPrefabPath = PrefabDir + "/Wall.prefab";
    private const string WallPlotPrefabPath = PrefabDir + "/WallPlot.prefab";
    private const string TowerPlotPath = "Assets/Bladehold/Bladehold Prefabs/Defenses/TowerPlot.prefab";

    /// <summary>The wrapped art pieces the wall is assembled from.</summary>
    private class WallArt
    {
        public GameObject[] seg = new GameObject[3], light = new GameObject[3], medium = new GameObject[3], heavy = new GameObject[3];
        public GameObject[] rubble = new GameObject[3], door = new GameObject[3];
        public GameObject spikes, fireFixture, iceFixture, stormFixture;
        public GameObject vfxLight, vfxMedium, vfxHeavy;
        public static readonly string[] TierNames = { "Wood", "Stone", "Metal" };
    }

    private struct Part
    {
        public string path;
        public Vector3 centre;   // where the piece's bounds centre lands (y = bounds min)
        public Vector3 euler;
        public Vector3 scale;
        public Part(string p, Vector3 c, Vector3 e, Vector3 s) { path = p; centre = c; euler = e; scale = s; }
    }

    private static Part P(string path, float x = 0f, float y = 0f, float z = 0f) => new Part(path, new Vector3(x, y, z), Vector3.zero, Vector3.one);
    private static Part P(string path, Vector3 centre, Vector3 euler, Vector3 scale) => new Part(path, centre, euler, scale);

    private static bool rebuildExisting;

    [MenuItem("Bladehold/Fort/Build Wall & Upgrade Assets")]
    private static void BuildMenu()
    {
        Debug.Log(Build());
    }

    /// <param name="rebuild">Overwrite existing prefabs (loses hand edits). Default: only create missing ones.</param>
    public static string Build(bool rebuild = false)
    {
        rebuildExisting = rebuild;
        var log = new StringBuilder("[FortWallAssetsBuilder]\n");
        EnsureFolder(PrefabDir);
        EnsureFolder(ArtDir);
        EnsureFolder(ConfigDir);

        WallArt art = EnsureWallArt();
        string logSpike = Dn + "Props/SM_Prop_Log_Spike_01.prefab";
        string trapSpikes = Dn + "Environments/Misc/SM_Env_Trap_Spikes_01.prefab";
        string crystal = Fk + "Items/SM_Item_Crystal_03.prefab";
        string workbench = Fk + "Props/Furniture/SM_Prop_Workbench_01.prefab";
        log.AppendLine("- Art wrappers ensured in " + ArtDir + (rebuild ? " (rebuilt)" : " (existing kept)"));

        // ---- Wall config ----------------------------------------------------------------------
        bool newConfig = AssetDatabase.LoadAssetAtPath<WallConfigSO>(WallConfigPath) == null;
        WallConfigSO wallConfig = LoadOrCreate<WallConfigSO>(WallConfigPath);
        if (newConfig)
        {
            wallConfig.doorWidth = DoorWidth;
            wallConfig.wallHeight = WallHeight;
        }
        FillTierLooks(wallConfig);
        if (wallConfig.lightDamageVfx == null) wallConfig.lightDamageVfx = art.vfxLight;
        if (wallConfig.mediumDamageVfx == null) wallConfig.mediumDamageVfx = art.vfxMedium;
        if (wallConfig.heavyDamageVfx == null) wallConfig.heavyDamageVfx = art.vfxHeavy;
        EditorUtility.SetDirty(wallConfig);

        // ---- Feedback sources ------------------------------------------------------------------
        GameObject arrowContents = PrefabUtility.LoadPrefabContents(ArrowTowerPath);
        GameObject ammoContents = PrefabUtility.LoadPrefabContents(AmmoPickupPath);
        try
        {
            Transform fb = arrowContents.transform.Find("Feedbacks");
            GameObject repairMmf = fb != null && fb.Find("RepairMMF") != null ? fb.Find("RepairMMF").gameObject : null;
            GameObject upgradeMmf = fb != null && fb.Find("UpgradeMMF") != null ? fb.Find("UpgradeMMF").gameObject : null;
            GameObject breakMmf = fb != null && fb.Find("BreakMMF") != null ? fb.Find("BreakMMF").gameObject : null;
            if (repairMmf == null || upgradeMmf == null || breakMmf == null) log.AppendLine("! Arrow tower feedbacks not found; prefabs get empty MMF players.");
            var gold = AssetDatabase.LoadAssetAtPath<DamageNumbersPro.DamageNumber>(GoldPopupPath);
            var spread = AssetDatabase.LoadAssetAtPath<DamageNumbersPro.DamageNumber>(SpreadPopupPath);

            // ---- Tower spike ring --------------------------------------------------------------
            TowerSpikeRing spikeRing;
            {
                var root = new GameObject("TowerSpikeRing");
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    var pos = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.4f;
                    PlaceArt(trapSpikes, root.transform, pos, new Vector3(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one * 0.7f);
                }
                var ring = root.AddComponent<TowerSpikeRing>();
                Set(ring, "stabFeedback", CloneMmf(breakMmf, root.transform, "StabMMF"));
                spikeRing = SavePrefab(root, PrefabDir + "/TowerSpikeRing.prefab").GetComponent<TowerSpikeRing>();
            }

            // ---- Icy water zone ----------------------------------------------------------------
            WallIcyWaterZone icyWater;
            {
                var root = new GameObject("WallIcyWaterZone");
                var ice = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Fort/SlipperyIceZone.prefab");
                if (ice != null && ice.transform.childCount > 0)
                {
                    var vis = Object.Instantiate(ice.transform.GetChild(0).gameObject, root.transform);
                    vis.name = "Visual";
                }
                var zone = root.AddComponent<WallIcyWaterZone>();
                Set(zone, "splashFeedback", CloneMmf(repairMmf, root.transform, "SplashMMF"));
                icyWater = SavePrefab(root, PrefabDir + "/WallIcyWaterZone.prefab").GetComponent<WallIcyWaterZone>();
            }

            // ---- Wall -------------------------------------------------------------------------
            {
                var root = new GameObject("Wall");
                root.AddComponent<Health>();
                var wall = root.AddComponent<WallStructure>();
                var doorGo = new GameObject("Door");
                doorGo.transform.SetParent(root.transform, false);
                var door = doorGo.AddComponent<WallDoor>();
                var feedbacks = new GameObject("Feedbacks").transform;
                feedbacks.SetParent(root.transform, false);
                Set(door, "openFeedback", CloneMmf(repairMmf, feedbacks, "DoorOpenMMF"));
                Set(door, "closeFeedback", CloneMmf(breakMmf, feedbacks, "DoorCloseMMF"));
                Set(door, "blockedFeedback", CloneMmf(breakMmf, feedbacks, "DoorBlockedMMF"));
                Set(wall, "door", door);
                Set(wall, "health", root.GetComponent<Health>());
                Set(wall, "hitFeedback", CloneMmf(repairMmf, feedbacks, "HitMMF"));
                Set(wall, "stageDropFeedback", CloneMmf(breakMmf, feedbacks, "StageDropMMF"));
                Set(wall, "collapseFeedback", CloneMmf(breakMmf, feedbacks, "CollapseMMF"));
                Set(wall, "upgradeFeedback", CloneMmf(upgradeMmf, feedbacks, "UpgradeMMF"));
                Set(wall, "lightningArcFeedback", CloneMmf(repairMmf, feedbacks, "LightningArcMMF"));
                Set(wall, "popupPrefab", gold);
                BuildHealthBar(root.transform, wall);
                AddAuthoredArt(root.transform, wall, art, wallConfig);
                SavePrefab(root, WallPrefabPath);
            }
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);

            // ---- Wall plot --------------------------------------------------------------------
            {
                var root = new GameObject("WallPlot");
                var plot = root.AddComponent<WallPlot>();
                var stationGo = new GameObject("CraftingStation");
                stationGo.transform.SetParent(root.transform, false);
                stationGo.transform.localPosition = new Vector3(7f, 0f, -6f);
                PlaceArt(workbench, stationGo.transform, Vector3.zero, Vector3.zero, Vector3.one);
                var station = stationGo.AddComponent<WallCraftingStation>();
                var marker = new GameObject("EmptyMarker");
                marker.transform.SetParent(root.transform, false);
                Transform left = PlaceArt(logSpike, marker.transform, new Vector3(-5f, 0.9f, 0f), new Vector3(0f, 0f, 8f), new Vector3(0.6f, 0.6f, 0.6f)).transform;
                Transform right = PlaceArt(logSpike, marker.transform, new Vector3(5f, 0.9f, 0f), new Vector3(0f, 0f, -8f), new Vector3(0.6f, 0.6f, 0.6f)).transform;
                left.name = "Stake_L";
                right.name = "Stake_R";
                NestWallTemplate(root, plot, wallPrefab);
                Set(plot, "wallConfig", wallConfig);
                Set(plot, "station", station);
                Set(plot, "emptyMarker", marker);
                Set(plot, "markerLeft", left);
                Set(plot, "markerRight", right);
                Set(plot, "buildFeedback", CloneMmf(upgradeMmf, root.transform, "BuildMMF"));
                Set(plot, "popupPrefab", gold);
                SavePrefab(root, WallPlotPrefabPath);
            }

            // ---- Crystal pickup ---------------------------------------------------------------
            CrystalPickup pickupPrefab;
            {
                var root = new GameObject("CrystalPickup");
                var col = root.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.9f;
                GameObject vis = PlaceArt(crystal, root.transform, Vector3.zero, Vector3.zero, Vector3.one * 5f);
                var lightGo = new GameObject("Glow");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                var glow = lightGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.range = 3.5f;
                glow.intensity = 2f;
                glow.shadows = LightShadows.None;
                var pickup = root.AddComponent<CrystalPickup>();
                var renderers = vis.GetComponentsInChildren<Renderer>();
                var so = new SerializedObject(pickup);
                SerializedProperty arr = so.FindProperty("tintRenderers");
                arr.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                so.FindProperty("glowLight").objectReferenceValue = glow;
                so.FindProperty("pickupPopup").objectReferenceValue = spread;
                so.ApplyModifiedPropertiesWithoutUndo();
                MMF_Player ammoFb = ammoContents.GetComponent<MMF_Player>();
                if (ammoFb != null)
                {
                    var fbGo = new GameObject("PickupMMF");
                    fbGo.transform.SetParent(root.transform, false);
                    UnityEditorInternal.ComponentUtility.CopyComponent(ammoFb);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(fbGo);
                    Set(pickup, "pickupFeedback", fbGo.GetComponent<MMF_Player>());
                }
                pickupPrefab = SavePrefab(root, PrefabDir + "/CrystalPickup.prefab").GetComponent<CrystalPickup>();
            }

            // ---- Configs ----------------------------------------------------------------------
            FortUpgradeConfigSO upgradeConfig = LoadOrCreate<FortUpgradeConfigSO>(FortUpgradeConfigPath);
            if (upgradeConfig.towerSpikeRingPrefab == null) upgradeConfig.towerSpikeRingPrefab = spikeRing;
            if (upgradeConfig.icyWaterPrefab == null) upgradeConfig.icyWaterPrefab = icyWater;
            if (upgradeConfig.boilingOilPrefab == null)
                upgradeConfig.boilingOilPrefab = AssetDatabase.LoadAssetAtPath<BurningOilZone>("Assets/Bladehold/Bladehold Prefabs/Fort/BurningOilZone.prefab");
            FillIcon(ref upgradeConfig.refillIcon, "Assets/Bladehold/Art/Icons/Skills/Base/refill.png");
            FillIcon(ref upgradeConfig.fireRateIcon, "Assets/Bladehold/Art/Icons/Skills/Base/fire_rate.png");
            FillIcon(ref upgradeConfig.spikesIcon, "Assets/Bladehold/Art/Icons/Skills/Base/tower_spikes.png");
            FillIcon(ref upgradeConfig.repairIcon, "Assets/Bladehold/Art/Icons/Skills/Base/repair.png");
            FillIcon(ref upgradeConfig.materialIcon, "Assets/Bladehold/Art/Icons/Skills/Base/wall_material.png");
            FillIcon(ref upgradeConfig.deconstructIcon, "Assets/Bladehold/Art/Icons/Skills/Base/deconstruct.png");
            FillIcon(ref upgradeConfig.fireIcon, "Assets/Bladehold/Art/Icons/Skills/Base/crystal_fire.png");
            FillIcon(ref upgradeConfig.iceIcon, "Assets/Bladehold/Art/Icons/Skills/Base/crystal_ice.png");
            FillIcon(ref upgradeConfig.lightningIcon, "Assets/Bladehold/Art/Icons/Skills/Base/crystal_lightning.png");
            EditorUtility.SetDirty(upgradeConfig);

            CrystalConfigSO crystalConfig = LoadOrCreate<CrystalConfigSO>(CrystalConfigPath);
            if (crystalConfig.pickupPrefab == null) crystalConfig.pickupPrefab = pickupPrefab;
            EditorUtility.SetDirty(crystalConfig);

            // ---- Fort rules -------------------------------------------------------------------
            {
                var root = new GameObject("FortRules");
                var rules = root.AddComponent<DefenseSceneRules>();
                Set(rules, "upgradeConfig", upgradeConfig);
                var rewards = root.AddComponent<CrystalRewards>();
                Set(rewards, "config", crystalConfig);
                Set(rewards, "rewardPopup", spread);
                root.AddComponent<SceneCrystalBias>();
                SavePrefab(root, "Assets/Bladehold/Bladehold Prefabs/Defenses/FortRules.prefab");
            }
            log.AppendLine("- Prefabs: Wall, WallPlot, TowerSpikeRing, WallIcyWaterZone, CrystalPickup (in " + PrefabDir + "), FortRules");
            log.AppendLine("- Configs: " + FortUpgradeConfigPath + ", " + WallConfigPath + ", " + CrystalConfigPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(arrowContents);
            PrefabUtility.UnloadPrefabContents(ammoContents);
        }

        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>Ensures the wall's art wrappers exist (kept unless rebuilding) and returns them.</summary>
    private static WallArt EnsureWallArt()
    {
        string stone1 = Fk + "Castle/SM_Bld_Castle_Wall_01.prefab";
        string stone2 = Fk + "Castle/SM_Bld_Castle_Wall_02.prefab";
        string stone3 = Fk + "Castle/SM_Bld_Castle_Wall_03.prefab";
        string stone4 = Fk + "Castle/SM_Bld_Castle_Wall_04.prefab";
        string dTop1 = Fk + "Castle/SM_Bld_Castle_DestroyedWall_Top_01.prefab";
        string dTop2 = Fk + "Castle/SM_Bld_Castle_DestroyedWall_Top_02.prefab";
        string dMid1 = Fk + "Castle/SM_Bld_Castle_DestroyedWall_Middle_01.prefab";
        string dMid2 = Fk + "Castle/SM_Bld_Castle_DestroyedWall_Middle_02.prefab";
        string dBot1 = Fk + "Castle/SM_Bld_Castle_DestroyedWall_Bottom_01.prefab";
        string dBot2 = Fk + "Castle/SM_Bld_Castle_DestroyedWall_Bottom_02.prefab";
        string rubblePile = Fk + "Castle/SM_Bld_Castle_DestroyedWall_RubblePile_01.prefab";
        string metalSpikes = Dn + "Environments/Misc/SM_Env_Fence_Metal_Spikes_01.prefab";
        string metalFence = Dn + "Props/SM_Prop_Metal_Fence_01.prefab";
        string goblinSpikes = Dn + "Props/SM_Prop_Goblin_Spikes_01.prefab";
        string logSpike = Dn + "Props/SM_Prop_Log_Spike_01.prefab";
        string cauldron = Dn + "Props/SM_Prop_Cauldron_01.prefab";
        string barrel = Dn + "Props/SM_Prop_Barrel_02.prefab";
        string crystal = Fk + "Items/SM_Item_Crystal_03.prefab";
        string fire = Fk + "FX/FX_Fire_01.prefab";
        string smokeLight = Fk + "FX/FX_Smoke_Light_01.prefab";
        string smokeDark = Fk + "FX/FX_Smoke_Dark_01.prefab";

        float half = SegmentLength * 0.25f;
        var crown = new Vector3(0f, WallHeight, 0f);
        // Wood is a palisade of pointed logs (the Synty wood-beam walls are open frames).
        GameObject woodSeg = Wrap("Wall_Wood_Segment", Palisade(logSpike, SegmentLength, 0, 0));
        GameObject woodLight = Wrap("Wall_Wood_Segment_LightDamage", Palisade(logSpike, SegmentLength, 1, 3));
        GameObject woodMedium = Wrap("Wall_Wood_Segment_MediumDamage", Palisade(logSpike, SegmentLength, 2, 5));
        GameObject woodHeavy = Wrap("Wall_Wood_Segment_HeavyDamage", Palisade(logSpike, SegmentLength, 3, 7));
        GameObject woodRubble = Wrap("Wall_Wood_Rubble",
            P(logSpike, new Vector3(-1.2f, 0f, 1.2f), new Vector3(0f, 20f, 88f), new Vector3(1f, 1.3f, 1f)),
            P(logSpike, new Vector3(0.4f, 0f, 1.8f), new Vector3(0f, -15f, 86f), new Vector3(1f, 1.2f, 1f)),
            P(logSpike, new Vector3(1.5f, 0.3f, 1.0f), new Vector3(0f, 60f, 80f), new Vector3(1f, 1.4f, 1f)),
            P(logSpike, new Vector3(-0.2f, 0.35f, 0.9f), new Vector3(0f, -70f, 84f), new Vector3(1f, 1.1f, 1f)));
        GameObject stoneSeg = Wrap("Wall_Stone_Segment", P(stone1));
        GameObject stoneLight = Wrap("Wall_Stone_Segment_LightDamage", P(stone2));
        GameObject stoneMedium = Wrap("Wall_Stone_Segment_MediumDamage", P(dTop1, -half), P(dTop2, half));
        GameObject stoneHeavy = Wrap("Wall_Stone_Segment_HeavyDamage", P(dBot1, -half), P(dBot2, half));
        GameObject stoneRubble = Wrap("Wall_Stone_Rubble", P(rubblePile, 0f, 0f, 0.8f));
        GameObject metalSeg = Wrap("Wall_Metal_Segment", P(stone3), P(metalSpikes, crown, Vector3.zero, Vector3.one));
        GameObject metalLight = Wrap("Wall_Metal_Segment_LightDamage", P(stone4), P(metalSpikes, crown, Vector3.zero, Vector3.one));
        GameObject metalMedium = Wrap("Wall_Metal_Segment_MediumDamage", P(dTop1, -half), P(dTop2, half));
        GameObject metalHeavy = Wrap("Wall_Metal_Segment_HeavyDamage", P(dMid1, -half), P(dMid2, half));

        // Doors: pivot at the left jamb's base, leaf along +X to the door width (sinks straight down to open).
        GameObject woodDoor = Wrap("Wall_Wood_Door", Palisade(logSpike, DoorWidth, 0, 0, DoorWidth * 0.5f));
        GameObject stoneDoor = Wrap("Wall_Stone_Door", Palisade(logSpike, DoorWidth, 0, 0, DoorWidth * 0.5f, 1.1f));
        GameObject metalDoor = Wrap("Wall_Metal_Door", P(metalFence, new Vector3(DoorWidth * 0.5f, 0f, 0f), Vector3.zero, new Vector3(DoorWidth / 2.5f, WallHeight / 2.44f, 3f)));

        GameObject spikesProp = Wrap("Wall_Spikes", P(goblinSpikes, Vector3.zero, Vector3.zero, new Vector3(SegmentLength / 4.28f, 0.5f, 0.8f)));
        GameObject fireFixture = Wrap("Wall_Fixture_BoilingOil", P(cauldron), P(fire, new Vector3(0f, 0.2f, 0f), Vector3.zero, Vector3.one * 0.6f));
        GameObject iceFixture = Wrap("Wall_Fixture_IcyWater", P(barrel, -0.5f), P(barrel, 0.5f));
        GameObject stormFixture = Wrap("Wall_Fixture_LightningRod", P(logSpike, Vector3.zero, Vector3.zero, new Vector3(0.5f, 1f, 0.5f)),
            P(crystal, new Vector3(0f, 3.1f, 0f), Vector3.zero, Vector3.one * 6f));

        GameObject vfxLight = Wrap("Wall_Damage_Light_VFX", P(smokeLight));
        GameObject vfxMedium = Wrap("Wall_Damage_Medium_VFX", P(smokeDark));
        GameObject vfxHeavy = Wrap("Wall_Damage_Heavy_VFX", P(fire), P(smokeDark, 0f, 0.6f));

        var a = new WallArt();
        a.seg[0] = woodSeg; a.light[0] = woodLight; a.medium[0] = woodMedium; a.heavy[0] = woodHeavy; a.rubble[0] = woodRubble; a.door[0] = woodDoor;
        a.seg[1] = stoneSeg; a.light[1] = stoneLight; a.medium[1] = stoneMedium; a.heavy[1] = stoneHeavy; a.rubble[1] = stoneRubble; a.door[1] = stoneDoor;
        a.seg[2] = metalSeg; a.light[2] = metalLight; a.medium[2] = metalMedium; a.heavy[2] = metalHeavy; a.rubble[2] = stoneRubble; a.door[2] = metalDoor;
        a.spikes = spikesProp;
        a.fireFixture = fireFixture;
        a.iceFixture = iceFixture;
        a.stormFixture = stormFixture;
        a.vfxLight = vfxLight;
        a.vfxMedium = vfxMedium;
        a.vfxHeavy = vfxHeavy;
        return a;
    }

    // ---- Authored wall art ---------------------------------------------------------------------

    /// <summary>Nests the Wall prefab under a plot as its build template and wires the towers' rise animation.</summary>
    private static void NestWallTemplate(GameObject plotRoot, WallPlot plot, GameObject wallPrefab)
    {
        var wallGo = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, plotRoot.transform);
        wallGo.name = "Wall";
        wallGo.transform.localPosition = Vector3.zero;
        wallGo.transform.localRotation = Quaternion.identity;
        Set(plot, "wallTemplate", wallGo.GetComponent<WallStructure>());

        GameObject towerPlot = PrefabUtility.LoadPrefabContents(TowerPlotPath);
        try
        {
            TowerPlot tp = towerPlot.GetComponent<TowerPlot>();
            Object anim = tp != null ? new SerializedObject(tp).FindProperty("assemblyAnimationPrefab").objectReferenceValue : null;
            if (anim == null) Debug.LogError("[FortWallAssetsBuilder] TowerPlot has no assemblyAnimationPrefab to share with walls.");
            else Set(plot, "assemblyAnimationPrefab", anim);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(towerPlot);
        }
    }

    /// <summary>Fills each tier's name and its Castle_Wall material where they're empty.</summary>
    private static void FillTierLooks(WallConfigSO config)
    {
        if (config.tiers == null || config.tiers.Length != 3) config.tiers = new WallConfigSO.TierLook[3];
        for (int t = 0; t < 3; t++)
        {
            config.tiers[t] ??= new WallConfigSO.TierLook();
            if (string.IsNullOrEmpty(config.tiers[t].displayName) || config.tiers[t].displayName == "Wood") config.tiers[t].displayName = WallArt.TierNames[t];
            if (config.tiers[t].material == null)
                config.tiers[t].material = AssetDatabase.LoadAssetAtPath<Material>($"{TierMaterialDir}Castle_Wall_0{t + 1}.mat");
        }
    }

    /// <summary>
    ///     Lays the wall's art out as editable children of <paramref name="root" />: Model (ShortWallWithGate, or a
    ///     row of stone segments with a door leaf if that prefab is missing), Rubble, Spikes and the three element
    ///     fixtures, all wired onto <paramref name="wall" />. Walkable colliders in the model (floors, stairs) go on
    ///     Environment, the rest on Fortification; the wall is ignored by NavMesh builds.
    /// </summary>
    private static void AddAuthoredArt(Transform root, WallStructure wall, WallArt art, WallConfigSO config)
    {
        float doorWidth = config != null ? config.doorWidth : DoorWidth;
        float height = config != null ? config.wallHeight : WallHeight;
        float thickness = config != null ? config.wallThickness : 0.8f;
        float sideLength = (ReferenceWidth - doorWidth) * 0.5f;
        int perSide = Mathf.Max(1, Mathf.RoundToInt(sideLength / SegmentLength));
        float scaleX = sideLength / (perSide * SegmentLength);
        var xs = new List<float>();
        foreach (int side in new[] { -1, 1 })
            for (int k = 0; k < perSide; k++) xs.Add(side * (doorWidth * 0.5f + (k + 0.5f) * SegmentLength * scaleX));

        var model = new GameObject("Model").transform;
        model.SetParent(root, false);
        GameObject leaf = null;
        var building = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
        if (building != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(building, model);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == PortcullisName) leaf = t.gameObject;
        }
        else
        {
            Row("Segments", model, art.seg[1], xs, 0f, scaleX, true);
            leaf = Piece(art.door[1], model, new Vector3(-doorWidth * 0.5f, 0f, 0f), Vector3.one);
            leaf.name = "DoorLeaf";
        }

        GameObject rubble = Row("Rubble", root, art.rubble[1], xs, 0f, 1f, false);
        GameObject spikes = Row("Spikes", root, art.spikes, xs, thickness * 0.5f + 0.4f, scaleX, false);
        GameObject fire = Piece(art.fireFixture, root, new Vector3(0f, height, 0f), Vector3.one);
        GameObject ice = Piece(art.iceFixture, root, new Vector3(0f, height, 0f), Vector3.one);
        GameObject storm = Piece(art.stormFixture, root, new Vector3(0f, height, 0f), Vector3.one);
        fire.name = "Fixture_BoilingOil";
        ice.name = "Fixture_IcyWater";
        storm.name = "Fixture_LightningRod";
        foreach (GameObject f in new[] { fire, ice, storm }) f.SetActive(false);

        int fort = LayerMask.NameToLayer(PlayerBarrier.FortificationLayerName);
        SetLayer(root.gameObject, fort);
        SetModelLayers(model, fort);
        // Decor never collides (Synty props carry their own colliders); the model keeps its.
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            if (!c.transform.IsChildOf(model) || (leaf != null && c.transform.IsChildOf(leaf.transform))) Object.DestroyImmediate(c);
        // The template sits visible in edit mode; NavMesh builds must never bake it in.
        NavMeshModifier modifier = root.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = root.gameObject.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;

        var wallSo = new SerializedObject(wall);
        wallSo.FindProperty("model").objectReferenceValue = model;
        wallSo.FindProperty("doorLeaf").objectReferenceValue = leaf;
        wallSo.FindProperty("rubble").objectReferenceValue = rubble;
        wallSo.FindProperty("spikes").objectReferenceValue = spikes;
        wallSo.FindProperty("fireFixture").objectReferenceValue = fire;
        wallSo.FindProperty("iceFixture").objectReferenceValue = ice;
        wallSo.FindProperty("lightningFixture").objectReferenceValue = storm;
        wallSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Walkable pieces of the model (floors, stairs) on Environment so the player can climb and stand on them; the rest on Fortification.</summary>
    public static void SetModelLayers(Transform model, int fortLayer)
    {
        int ground = LayerMask.NameToLayer("Environment");
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            bool walkable = t.name.Contains("Floor") || t.name.Contains("Stairs");
            int layer = walkable ? ground : fortLayer;
            if (layer >= 0) t.gameObject.layer = layer;
        }
    }

    /// <summary>A child holding one <paramref name="piece" /> at each X in <paramref name="xs" />, stretched along X by <paramref name="scaleX" />.</summary>
    private static GameObject Row(string name, Transform parent, GameObject piece, List<float> xs, float z, float scaleX, bool active)
    {
        var row = new GameObject(name);
        row.transform.SetParent(parent, false);
        if (piece != null)
            foreach (float x in xs) Piece(piece, row.transform, new Vector3(x, 0f, z), new Vector3(scaleX, 1f, 1f));
        row.SetActive(active);
        return row;
    }

    private static GameObject Piece(GameObject prefab, Transform parent, Vector3 localPos, Vector3 scale)
    {
        if (prefab == null) return new GameObject("MISSING");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        return go;
    }

    // ---- World-space HP bar -------------------------------------------------------------------

    private static void BuildHealthBar(Transform wallRoot, WallStructure wall)
    {
        var go = new GameObject("HealthBar", typeof(RectTransform));
        go.transform.SetParent(wallRoot, false);
        go.transform.localPosition = new Vector3(0f, WallHeight + 0.8f, 0f);
        go.transform.localScale = Vector3.one * 0.01f;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(320f, 60f);
        var group = go.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Image bg = UiImage("Background", go.transform, sprite, new Color(0.08f, 0.07f, 0.06f, 0.85f), new Vector2(0f, 0f), new Vector2(1f, 0.55f), Image.Type.Sliced);
        Image accent = UiImage("ElementAccent", go.transform, sprite, Color.white, new Vector2(0f, 0f), new Vector2(1f, 0.55f), Image.Type.Sliced);
        accent.fillCenter = false;
        Image delayed = UiImage("DelayedBar", bg.transform, sprite, new Color(1f, 0.55f, 0.15f, 1f), new Vector2(0.02f, 0.12f), new Vector2(0.98f, 0.88f), Image.Type.Filled);
        Image fill = UiImage("Fill", bg.transform, sprite, new Color(0.55f, 0.85f, 0.4f, 1f), new Vector2(0.02f, 0.12f), new Vector2(0.98f, 0.88f), Image.Type.Filled);
        foreach (Image img in new[] { delayed, fill })
        {
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = new Vector2(0f, 0.55f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 24f;
        label.text = "Wood Wall";

        var bar = go.AddComponent<MMProgressBar>();
        bar.ForegroundBar = fill.transform;
        bar.DelayedBarDecreasing = delayed.transform;
        bar.FillMode = MMProgressBar.FillModes.FillAmount;

        var hb = go.AddComponent<WallHealthBar>();
        var so = new SerializedObject(hb);
        so.FindProperty("wall").objectReferenceValue = wall;
        so.FindProperty("progressBar").objectReferenceValue = bar;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("elementAccent").objectReferenceValue = accent;
        so.FindProperty("height").floatValue = WallHeight + 0.8f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image UiImage(string name, Transform parent, Sprite sprite, Color color, Vector2 min, Vector2 max, Image.Type type)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = type;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ---- Helpers ------------------------------------------------------------------------------

    /// <summary>
    ///     Pointed logs side by side across <paramref name="length" /> (centred on <paramref name="centreX" />).
    ///     Damage: every <paramref name="leanEvery" />-th log leans (light) and every
    ///     <paramref name="missingEvery" />-th is gone; heavier stages also shorten the rest.
    /// </summary>
    private static Part[] Palisade(string log, float length, int stage, int missingEvery, float centreX = 0f, float heightScale = 1.35f)
    {
        const float pitch = 0.42f;
        int count = Mathf.Max(2, Mathf.RoundToInt(length / pitch));
        float step = length / count;
        var parts = new List<Part>();
        var rng = new System.Random(1700 + stage * 31 + count);
        for (int i = 0; i < count; i++)
        {
            if (missingEvery > 0 && i % missingEvery == missingEvery / 2) continue;
            float x = centreX - length * 0.5f + step * (i + 0.5f);
            float h = heightScale * (stage >= 2 ? 0.55f + (float)rng.NextDouble() * 0.35f : 0.92f + (float)rng.NextDouble() * 0.12f);
            float lean = stage >= 1 && i % 3 == 1 ? 8f + (float)rng.NextDouble() * 14f : (float)rng.NextDouble() * 3f;
            parts.Add(P(log, new Vector3(x, 0f, 0f), new Vector3(lean, (float)rng.NextDouble() * 360f, 0f), new Vector3(1f, h, 1f)));
        }
        return parts.ToArray();
    }

    /// <summary>A prefab whose root is the base centre, holding each part with its bounds centred at the part's target.</summary>
    private static GameObject Wrap(string name, params Part[] parts)
    {
        var root = new GameObject(name);
        foreach (Part part in parts) PlaceArt(part.path, root.transform, part.centre, part.euler, part.scale);
        return SavePrefab(root, ArtDir + "/" + name + ".prefab");
    }

    /// <summary>Instantiates a prefab under <paramref name="parent" /> so its renderer bounds sit centred on X/Z at <paramref name="centre" />, base at centre.y.</summary>
    private static GameObject PlaceArt(string path, Transform parent, Vector3 centre, Vector3 euler, Vector3 scale)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null)
        {
            Debug.LogError($"[FortWallAssetsBuilder] Missing art prefab {path}");
            return new GameObject("MISSING_" + Path.GetFileNameWithoutExtension(path));
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        go.transform.SetParent(parent, false);
        // Keep the source's own root rotation/scale (Synty FBX roots are often rotated -90 on X).
        go.transform.localRotation = Quaternion.Euler(euler) * src.transform.localRotation;
        go.transform.localScale = Vector3.Scale(scale, src.transform.localScale);
        go.transform.localPosition = Vector3.zero;
        Bounds b = LocalBounds(go, parent);
        go.transform.localPosition = new Vector3(centre.x - b.center.x, centre.y - b.min.y, centre.z - b.center.z);
        return go;
    }

    private static Bounds LocalBounds(GameObject go, Transform space)
    {
        bool any = false;
        var b = new Bounds();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds wb = r.bounds;
            Vector3 c = wb.center, e = wb.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(c.x + ((i & 1) == 0 ? -e.x : e.x), c.y + ((i & 2) == 0 ? -e.y : e.y), c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 local = space.InverseTransformPoint(corner);
                if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                else b.Encapsulate(local);
            }
        }
        return any ? b : new Bounds(Vector3.zero, Vector3.zero);
    }

    private static MMF_Player CloneMmf(GameObject source, Transform parent, string name)
    {
        GameObject go = source != null ? Object.Instantiate(source, parent) : new GameObject();
        go.name = name;
        go.transform.SetParent(parent, false);
        MMF_Player player = go.GetComponent<MMF_Player>();
        if (player == null) player = go.AddComponent<MMF_Player>();
        return player;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !rebuildExisting)
        {
            Object.DestroyImmediate(root);
            return existing;
        }
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
        Object.DestroyImmediate(root);
        if (!ok) Debug.LogError($"[FortWallAssetsBuilder] Failed to save {path}");
        return saved;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void FillIcon(ref Sprite slot, string path)
    {
        if (slot != null) return;
        slot = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (slot == null) Debug.LogWarning($"[FortWallAssetsBuilder] No sprite at {path}");
    }

    private static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[FortWallAssetsBuilder] {target.GetType().Name} has no field '{field}'.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayer(GameObject go, int layer)
    {
        if (layer < 0) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
