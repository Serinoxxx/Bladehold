using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Creates (or refreshes) the stock scene-gen assets: the shared SpikePit config, the Alpine palette
///     and the Outer Gate spec (campaign tier 1). Other defense scenes start by duplicating the spec and
///     editing its numbers; a new biome starts by duplicating the palette and swapping its prefabs.
/// </summary>
public static class DefenseSceneDefaults
{
    public const string ConfigFolder = "Assets/Bladehold/Config/SceneGen";
    public const string SpikePitConfigPath = "Assets/Bladehold/Config/SceneGen/SpikePitConfig.asset";
    public const string AlpinePalettePath = "Assets/Bladehold/Config/SceneGen/Alpine_DefensePalette.asset";
    public const string OuterGateSpecPath = "Assets/Bladehold/Config/SceneGen/OuterGate_DefenseSpec.asset";
    public const string AridPalettePath = "Assets/Bladehold/Config/SceneGen/Arid_DefensePalette.asset";
    public const string DesertGateSpecPath = "Assets/Bladehold/Config/SceneGen/DesertGate_DefenseSpec.asset";

    private const string Alpine = "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain";
    private const string Kingdom = "Assets/Synty/PolygonFantasyKingdom/Prefabs";
    private const string Dungeon = "Assets/Synty/PolygonDungeon/Prefabs";
    private const string Arid = "Assets/Synty/PolygonNatureBiomes/PNB_Arid_Desert";

    [MenuItem("Bladehold/Scene Gen/Create or Refresh Stock Assets (Outer Gate + Alpine)")]
    public static void CreateAll()
    {
        EnsureFolder(ConfigFolder);
        EnsureSpikePitConfig();
        DefenseBiomePaletteSO palette = CreateAlpinePalette();
        CreateOuterGateSpec(palette);
        AssetDatabase.SaveAssets();
        Debug.Log("[DefenseSceneDefaults] Stock scene-gen assets written to " + ConfigFolder);
    }

    [MenuItem("Bladehold/Scene Gen/Create or Refresh Desert Gate Assets (Arid)")]
    public static void CreateDesertGate()
    {
        EnsureFolder(ConfigFolder);
        EnsureSpikePitConfig();
        DefenseBiomePaletteSO palette = CreateAridPalette();
        CreateDesertGateSpec(palette);
        AssetDatabase.SaveAssets();
        Debug.Log("[DefenseSceneDefaults] Desert Gate scene-gen assets written to " + ConfigFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureSpikePitConfig() => LoadOrCreate<SpikePitConfigSO>(SpikePitConfigPath);

    /// <summary>Finds a prefab by exact file name under a folder; logs and returns null when missing.</summary>
    private static GameObject Prefab(string folder, string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        Debug.LogWarning($"[DefenseSceneDefaults] Prefab '{name}' not found under {folder}.");
        return null;
    }

    private static List<GameObject> Prefabs(string folder, params string[] names)
    {
        var list = new List<GameObject>();
        foreach (string n in names)
        {
            GameObject p = Prefab(folder, n);
            if (p != null) list.Add(p);
        }
        return list;
    }

    private static TerrainLayer Layer(string name, string biome = Alpine) =>
        AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{biome}/Terrain/{name}.terrainlayer");

    private static DefenseBiomePaletteSO CreateAlpinePalette()
    {
        var p = LoadOrCreate<DefenseBiomePaletteSO>(AlpinePalettePath);
        string ap = $"{Alpine}/Prefabs";

        p.ground = Layer("layer_snow_albedosnow_normalb05780438ad4031");
        p.groundVariant = Layer("Ice_02");
        p.slope = Layer("SnowCliff");
        p.cliff = Layer("RiverRocks_04");
        p.road = Layer("Pine_Dirt");
        p.ravineFloor = Layer("RiverRocks_01");
        p.courtyard = Layer("Mud_01");
        p.terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");

        // Alpine's own Rock_Cliff prefabs are refractive glacier ice, not rock. The Arid Desert cliffs are the
        // same family of meshes with a triplanar rock material, re-skinned with Alpine's snow-capped rock.
        p.cliffs = Prefabs($"{Arid}/Prefabs", "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_02", "SM_Env_Rock_Cliff_04",
            "SM_Env_Rock_Cliff_05", "SM_Env_Rock_Cliff_06", "SM_Env_Rock_Cliff_08", "SM_Env_Rock_Cliff_09",
            "SM_Env_Rock_Cliff_10", "SM_Env_Rock_Cliff_12", "SM_Env_Rock_Cliff_14");
        p.cliffMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Alpine}/Materials/Snow_Rock_Tri.mat");
        p.largeRocks = Prefabs(ap, "SM_Env_Rock_08", "SM_Env_Rock_09", "SM_Env_Rock_010", "SM_Env_Rock_Rough_01");
        p.mediumRocks = Prefabs(ap, "SM_Env_Rock_01", "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_06",
            "SM_Env_Rock_07", "SM_Env_Rock_Rough_02");
        p.smallRocks = Prefabs(ap, "SM_Env_Rock_Small_01", "SM_Env_Rock_Small_02", "SM_Env_Rock_Small_03",
            "SM_Env_Rock_02", "SM_Env_Rock_05");
        p.snowMounds = Prefabs(ap, "SM_Env_Snow_Mound_01", "SM_Env_Snow_Mound_02", "SM_Env_Snow_Mound_03",
            "SM_Env_Snow_Mound_04");
        p.trees = Prefabs(ap, "SM_Env_Pine_01", "SM_Env_Pine_02", "SM_Env_Pine_03", "SM_Env_Pine_04", "SM_Env_Pine_05");
        p.deadTrees = Prefabs(ap, "SM_Env_Pine_NoLeaves_01", "SM_Env_Pine_Stump_01");

        p.gate = Prefab(Kingdom, "SM_Bld_Castle_Wall_Gate_L_01");
        p.wall = Prefab(Kingdom, "SM_Bld_Castle_Wall_01");
        p.wallTower = Prefab(Kingdom, "SM_Bld_Castle_Wall_Tower_M_01");
        p.bridgeTile = Prefab(Kingdom, "SM_Bld_Bridge_01");
        p.bridgePillar = Prefab(Kingdom, "SM_Bld_Bridge_Pillars_01");
        p.pitSpikes = Prefabs(Dungeon, "SM_Env_Trap_Spikes_01");
        p.defenseStakes = Prefabs(Kingdom, "SM_Prop_Spike_Fortification_01", "SM_Prop_Spike_Fortification_02",
            "SM_Prop_Spike_Fortification_03");
        p.banners = Prefabs(Kingdom, "SM_Prop_Battle_Banner_01", "SM_Prop_Battle_Banner_02", "SM_Prop_Battle_Banner_03");
        p.fieldLitter = Prefabs(Kingdom, "SM_Prop_Log_01", "SM_Prop_Log_02", "SM_Prop_Ground_Logs_01");
        p.fieldLitter.AddRange(Prefabs(ap, "SM_Env_Rock_Small_01", "SM_Env_Rock_Small_02", "SM_Env_Snow_Mound_03",
            "SM_Env_Rock_Pebbles_05"));
        p.fogDensity = 0.0035f;
        p.courtyardProps = Prefabs(Kingdom, "SM_Prop_Barrel_01", "SM_Prop_Camp_Brazier_01", "SM_Prop_Crate_01",
            "SM_Prop_Cart_01", "SM_Prop_Banner_01");

        p.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        p.volumeProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
            $"{Alpine}/Scene/Demo_01/AlpineGlobalVolume.asset");

        EditorUtility.SetDirty(p);
        return p;
    }

    private static void CreateOuterGateSpec(DefenseBiomePaletteSO palette)
    {
        var s = LoadOrCreate<DefenseSceneSpecSO>(OuterGateSpecPath);
        s.scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Outer Gate.unity";
        s.palette = palette;

        // Two rows of plots fanning out from the gate; lanes between them carry the roads.
        s.towerPlots = new List<Vector2>
        {
            new Vector2(-30f, 20f), new Vector2(-12f, 15f), new Vector2(12f, 15f), new Vector2(30f, 20f),
            new Vector2(-22f, 36f), new Vector2(22f, 36f)
        };

        // One ravine across the approach, just inside back-row tower range so the bridges are kill zones.
        s.ravines = new List<RavineSpec>
        {
            new RavineSpec
            {
                z = 58f, topWidth = 11f, floorWidth = 6f, depth = 6f, meanderAmplitude = 3.5f, meanderWavelength = 110f,
                bridges = new List<BridgeSpec>
                {
                    new BridgeSpec { x = -45f, tilesWide = 1 },
                    new BridgeSpec { x = 0f, tilesWide = 2 },
                    new BridgeSpec { x = 45f, tilesWide = 1 }
                },
                exitRamps = new List<RampSpec>
                {
                    new RampSpec { x = -20f, direction = -1 },
                    new RampSpec { x = 20f, direction = 1 }
                }
            }
        };

        // Spawns across the far end of the valley ("from afar").
        s.enemySpawns = new List<Vector2>
        {
            new Vector2(-60f, 148f), new Vector2(-45f, 158f), new Vector2(-30f, 150f), new Vector2(-15f, 160f),
            new Vector2(0f, 152f), new Vector2(15f, 160f), new Vector2(30f, 150f), new Vector2(45f, 158f),
            new Vector2(60f, 148f), new Vector2(0f, 164f)
        };
        s.objectiveZRange = new Vector2(84f, 140f);

        s.castleBuildings = LayoutCastle(s);
        EditorUtility.SetDirty(s);
    }

    /// <summary>
    ///     The PNB Arid Desert biome: sand floor with pale drifts, red-sand slopes, faces and trails (the
    ///     main layers of the pack's own demo terrain), its triplanar rock cliffs as-is, dead trees and cacti, bones and
    ///     tumbleweed for litter. Castle, bridge and battlefield props stay Fantasy Kingdom, like Alpine.
    /// </summary>
    private static DefenseBiomePaletteSO CreateAridPalette()
    {
        var p = LoadOrCreate<DefenseBiomePaletteSO>(AridPalettePath);
        string ap = $"{Arid}/Prefabs";

        p.ground = Layer("Sand_01", Arid);
        p.groundVariant = Layer("Sand_04", Arid);
        p.slope = Layer("RedSand", Arid);
        p.cliff = Layer("RedSand", Arid);
        p.road = Layer("RedSand", Arid);
        p.ravineFloor = Layer("SaltCracks", Arid);
        p.courtyard = Layer("Sand_02", Arid);
        p.terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");

        p.cliffs = Prefabs(ap, "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_02", "SM_Env_Rock_Cliff_04",
            "SM_Env_Rock_Cliff_05", "SM_Env_Rock_Cliff_06", "SM_Env_Rock_Cliff_08", "SM_Env_Rock_Cliff_09",
            "SM_Env_Rock_Cliff_10", "SM_Env_Rock_Cliff_12", "SM_Env_Rock_Cliff_14");
        p.cliffMaterial = null;
        p.largeRocks = Prefabs(ap, "SM_Env_Rock_07", "SM_Env_Rock_08", "SM_Env_Rock_09", "SM_Env_Rock_Rough_01",
            "SM_Env_Rocks_Spikey_01");
        p.mediumRocks = Prefabs(ap, "SM_Env_Rock_01", "SM_Env_Rock_03", "SM_Env_Rock_06", "SM_Env_Rock_10",
            "SM_Env_Rock_11", "SM_Env_Rock_Rough_02");
        p.smallRocks = Prefabs(ap, "SM_Env_Rock_Small_01", "SM_Env_Rock_Small_04", "SM_Env_Rock_02",
            "SM_Env_Rock_04", "SM_Env_Rock_05");
        // The "snow" role is drifts banked against rocks and walls: low scrub and succulents here.
        p.snowMounds = Prefabs(ap, "SM_Env_GroundCover_01", "SM_Env_GroundCover_02", "SM_Env_Succulent_01",
            "SM_Env_Bush_Bramble_01");
        p.trees = Prefabs(ap, "SM_Env_Tree_Dead_01", "SM_Env_Tree_Dead_02", "SM_Env_Cactus_03");
        p.deadTrees = Prefabs(ap, "SM_Env_Tree_Dead_02", "SM_Env_Cactus_03", "SM_Env_Bush_Bramble_02");

        p.gate = Prefab(Kingdom, "SM_Bld_Castle_Wall_Gate_L_01");
        p.wall = Prefab(Kingdom, "SM_Bld_Castle_Wall_01");
        p.wallTower = Prefab(Kingdom, "SM_Bld_Castle_Wall_Tower_M_01");
        p.bridgeTile = Prefab(Kingdom, "SM_Bld_Bridge_01");
        p.bridgePillar = Prefab(Kingdom, "SM_Bld_Bridge_Pillars_01");
        p.pitSpikes = Prefabs(Dungeon, "SM_Env_Trap_Spikes_01");
        p.defenseStakes = Prefabs(Kingdom, "SM_Prop_Spike_Fortification_01", "SM_Prop_Spike_Fortification_02",
            "SM_Prop_Spike_Fortification_03");
        p.banners = Prefabs(Kingdom, "SM_Prop_Battle_Banner_01", "SM_Prop_Battle_Banner_02", "SM_Prop_Battle_Banner_03");
        p.fieldLitter = Prefabs(ap, "SM_Prop_Bones_01", "SM_Prop_Bones_03", "SM_Prop_Bones_05",
            "SM_Prop_Tumbleweed_01", "SM_Env_Rock_Small_04", "SM_Env_Rock_Pebbles_03", "SM_Env_Cactus_01",
            "SM_Env_Cactus_02");
        p.courtyardProps = Prefabs(Kingdom, "SM_Prop_Barrel_01", "SM_Prop_Camp_Brazier_01", "SM_Prop_Crate_01",
            "SM_Prop_Cart_01", "SM_Prop_Banner_01");

        p.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        p.volumeProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
            $"{Arid}/Scene/Demo_01/Global Volume Profile.asset");
        p.sunColor = new Color(1f, 0.86f, 0.68f);
        p.sunIntensity = 1.5f;
        p.sunEuler = new Vector3(42f, -40f, 0f);
        p.fogColor = new Color(0.82f, 0.62f, 0.46f);
        p.fogDensity = 0.003f;
        p.ambientSky = new Color(1f, 0.83f, 0.7f);
        p.ambientEquator = new Color(0.62f, 0.55f, 0.55f);
        p.ambientGround = new Color(0.25f, 0.15f, 0.1f);

        EditorUtility.SetDirty(p);
        return p;
    }

    /// <summary>
    ///     Campaign tier 2: the Outer Gate's shape pushed harder. A second, wider ravine far out with only
    ///     two crossings, offset from the inner bridges so the warband zigzags under fire.
    /// </summary>
    private static void CreateDesertGateSpec(DefenseBiomePaletteSO palette)
    {
        var s = LoadOrCreate<DefenseSceneSpecSO>(DesertGateSpecPath);
        s.scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Desert Gate.unity";
        s.seed = 2202;
        s.palette = palette;
        s.fieldHalfWidth = 80f;
        s.fieldEndZ = 190f;

        s.towerPlots = new List<Vector2>
        {
            new Vector2(-32f, 18f), new Vector2(-12f, 14f), new Vector2(12f, 14f), new Vector2(32f, 18f),
            new Vector2(-24f, 38f), new Vector2(24f, 38f)
        };

        s.ravines = new List<RavineSpec>
        {
            new RavineSpec
            {
                z = 60f, topWidth = 11f, floorWidth = 6f, depth = 6f, meanderAmplitude = 4f, meanderWavelength = 95f,
                bridges = new List<BridgeSpec>
                {
                    new BridgeSpec { x = -42f, tilesWide = 1 },
                    new BridgeSpec { x = 0f, tilesWide = 2 },
                    new BridgeSpec { x = 42f, tilesWide = 1 }
                },
                exitRamps = new List<RampSpec>
                {
                    new RampSpec { x = -20f, direction = -1 },
                    new RampSpec { x = 20f, direction = 1 }
                }
            },
            new RavineSpec
            {
                z = 112f, topWidth = 12f, floorWidth = 7f, depth = 7f, meanderAmplitude = 5f, meanderWavelength = 130f,
                bridges = new List<BridgeSpec>
                {
                    new BridgeSpec { x = -24f, tilesWide = 1 },
                    new BridgeSpec { x = 26f, tilesWide = 1 }
                },
                exitRamps = new List<RampSpec>
                {
                    new RampSpec { x = 0f, direction = 1 },
                    new RampSpec { x = -52f, direction = -1 },
                    new RampSpec { x = 52f, direction = 1 }
                }
            }
        };

        s.enemySpawns = new List<Vector2>
        {
            new Vector2(-60f, 162f), new Vector2(-45f, 172f), new Vector2(-30f, 164f), new Vector2(-15f, 174f),
            new Vector2(0f, 166f), new Vector2(15f, 174f), new Vector2(30f, 164f), new Vector2(45f, 172f),
            new Vector2(60f, 162f), new Vector2(0f, 178f)
        };
        s.objectiveZRange = new Vector2(76f, 165f);

        s.castleBuildings = LayoutCastle(s);
        EditorUtility.SetDirty(s);
    }

    /// <summary>
    ///     A keep at the back of the courtyard and houses packed in two arcs in front of it, laid out from
    ///     measured footprints with a 2 m gap and an open lane from the gate to the keep.
    /// </summary>
    private static List<StructurePlacement> LayoutCastle(DefenseSceneSpecSO s)
    {
        var list = new List<StructurePlacement>();
        var occupied = new List<Vector3>();
        GameObject keep = Prefab(Kingdom, "SM_Bld_Preset_Tower_01_Optimized");
        if (keep != null) Add(keep, new Vector2(0f, -s.courtyardDepth + 16f), 180f);

        List<GameObject> houses = Prefabs(Kingdom, "SM_Bld_Preset_Tavern_01_Optimized",
            "SM_Bld_Preset_Blacksmith_01_Optimized", "SM_Bld_Preset_House_01_A_Optimized",
            "SM_Bld_Preset_House_03_Optimized", "SM_Bld_Preset_House_05_Optimized",
            "SM_Bld_Preset_House_07_Optimized", "SM_Bld_Preset_Church_01_A_Optimized",
            "SM_Bld_Preset_Stables_01_Optimized", "SM_Bld_Preset_House_04_Optimized",
            "SM_Bld_Preset_House_08_Optimized");

        var rng = new System.Random(s.seed + 7);
        foreach (GameObject h in houses)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * (s.courtyardHalfWidth - 8f);
                float z = -12f - (float)rng.NextDouble() * (s.courtyardDepth - 26f);
                if (Mathf.Abs(x) < 9f) continue; // lane gate -> keep
                // Face roughly towards the lane so fronts read from the gate.
                float yaw = (x < 0f ? 90f : -90f) + (float)(rng.NextDouble() - 0.5) * 30f;
                if (Add(h, new Vector2(x, z), yaw)) break;
            }
        }
        return list;

        bool Add(GameObject prefab, Vector2 centre, float yaw)
        {
            float r = PrefabMeasure.FootprintRadius(prefab, 1f) + 1f;
            foreach (Vector3 o in occupied)
                if (Vector2.Distance(new Vector2(o.x, o.y), centre) < o.z + r) return false;
            occupied.Add(new Vector3(centre.x, centre.y, r));
            list.Add(new StructurePlacement { prefab = prefab, position = centre, yaw = yaw });
            return true;
        }
    }
}
