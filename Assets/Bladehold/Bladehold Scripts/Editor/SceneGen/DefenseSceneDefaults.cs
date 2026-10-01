using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    public const string AridRockWallLayerPath = "Assets/Bladehold/Config/SceneGen/Arid_RockWall.terrainlayer";
    public const string KingdomPalettePath = "Assets/Bladehold/Config/SceneGen/Kingdom_DefensePalette.asset";
    // Generated once, then hand-edited (the tutorial layer is placed on top): never regenerate it.
    public const string TutorialGateSpecPath = "Assets/Bladehold/Config/SceneGen/TutorialGate_DefenseSpec_GENERATED_ONCE.asset";
    public const string GraveyardPalettePath = "Assets/Bladehold/Config/SceneGen/Graveyard_DefensePalette.asset";
    public const string GraveyardSpecPath = "Assets/Bladehold/Config/SceneGen/Graveyard_DefenseSpec.asset";
    public const string GraveyardSkyboxPath = "Assets/Bladehold/Config/SceneGen/Graveyard_Skybox.mat";
    public const string GraveyardVolumePath = "Assets/Bladehold/Config/SceneGen/Graveyard_Volume.asset";

    private const string Alpine = "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain";
    private const string Kingdom = "Assets/Synty/PolygonFantasyKingdom/Prefabs";
    private const string Dungeon = "Assets/Synty/PolygonDungeon/Prefabs";
    private const string Arid = "Assets/Synty/PolygonNatureBiomes/PNB_Arid_Desert";
    private const string Enchanted = "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest";

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

    /// <summary>
    ///     Plan 16's tutorial field (T3). Generated once, then edited as a normal scene: the tutorial director,
    ///     steps and waypoints are placed on top, so running the generator again would wipe them.
    /// </summary>
    [MenuItem("Bladehold/Scene Gen/Create or Refresh Tutorial Gate Assets (Kingdom)")]
    public static void CreateTutorialGate()
    {
        EnsureFolder(ConfigFolder);
        EnsureSpikePitConfig();
        DefenseBiomePaletteSO palette = CreateKingdomPalette();
        CreateTutorialGateSpec(palette);
        AssetDatabase.SaveAssets();
        Debug.Log("[DefenseSceneDefaults] Tutorial Gate scene-gen assets written to " + ConfigFolder);
    }

    /// <summary>The skeleton graveyard: night, green fog, lantern-lit roads, grave plots between the lanes.</summary>
    [MenuItem("Bladehold/Scene Gen/Create or Refresh Graveyard Assets")]
    public static void CreateGraveyard()
    {
        EnsureFolder(ConfigFolder);
        EnsureSpikePitConfig();
        DefenseBiomePaletteSO palette = CreateGraveyardPalette();
        CreateGraveyardSpec(palette);
        AssetDatabase.SaveAssets();
        Debug.Log("[DefenseSceneDefaults] Graveyard scene-gen assets written to " + ConfigFolder);
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
    ///     The PNB Arid Desert biome: sand floor with pale drifts, red-sand slopes, faces, trails and pit floors (the
    ///     main layers of the pack's own demo terrain), its triplanar rock cliffs as-is, dead trees and cacti, bones and
    ///     tumbleweed for litter. Ravine walls get the pack's rock-wall texture and a lining of tall boulders, and
    ///     the field gets prop clusters harvested from the pack's demo scene. Castle, bridge and battlefield props stay Fantasy Kingdom, like Alpine.
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
        p.ravineFloor = Layer("RedSand", Arid);
        p.courtyard = Layer("Sand_02", Arid);
        p.ravineWall = AridRockWallLayer();
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
        // Tall, narrow boulders stood shoulder to shoulder make the ravine walls read as rock.
        p.ravineWallRocks = Prefabs(ap, "SM_Env_Rock_07", "SM_Env_Rock_08", "SM_Env_Rock_09", "SM_Env_Rock_10",
            "SM_Env_Rock_11", "SM_Env_Rock_12", "SM_Env_Rock_13");
        // The artist's own groupings from the pack demo: skeletons among spiky rocks, cacti with pebbles and scrub.
        p.propClusters = DefenseClusterHarvester.Harvest($"{Arid}/Scene/Demo_01.unity",
            new[]
            {
                "SM_Prop_Bones", "SM_Env_Rocks_Spikey", "SM_Env_Rock_Pebbles", "SM_Env_Succulent", "SM_Env_Cactus",
                "SM_Env_Rock_Small", "SM_Env_Bush_Bramble", "SM_Env_GroundCover", "SM_Env_Tree_Dead", "SM_Env_Rock_0",
                "SM_Env_Rock_1", "SM_Prop_Tumbleweed"
            },
            new[] { "SM_Prop_Bones", "SM_Env_Rocks_Spikey", "SM_Env_Rock_0", "SM_Env_Rock_1", "SM_Env_Tree_Dead", "SM_Env_Cactus_03" },
            minPieces: 5);

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

    /// <summary>The pack's rock-wall texture as a terrain layer (the pack ships it only as a mesh material).</summary>
    private static TerrainLayer AridRockWallLayer()
    {
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AridRockWallLayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, AridRockWallLayerPath);
        }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Arid}/Textures/RockWall_Texture_01.png");
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Arid}/Textures/RockWall_Normals_01.png");
        layer.tileSize = new Vector2(6f, 6f);
        // A touch darker than the lit field so the walls read as shade and depth.
        layer.diffuseRemapMax = new Vector4(0.78f, 0.74f, 0.72f, 1f);
        EditorUtility.SetDirty(layer);
        return layer;
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
    ///     Green Fantasy Kingdom farmland for the tutorial: the pack's grass textures as terrain layers, its round
    ///     and thin trees, bushes and flowers, and prop clusters harvested from its exterior demo. Castle, bridge
    ///     and battlefield props are the same Kingdom pieces every palette uses.
    /// </summary>
    private static DefenseBiomePaletteSO CreateKingdomPalette()
    {
        var p = LoadOrCreate<DefenseBiomePaletteSO>(KingdomPalettePath);
        string env = $"{Kingdom}/Environments";

        p.ground = KingdomLayer("Kingdom_Grass_01", "Grass_01", new Vector2(8f, 8f));
        p.groundVariant = KingdomLayer("Kingdom_Grass_02", "Grass_02", new Vector2(10f, 10f));
        p.slope = KingdomLayer("Kingdom_Grass_Dark", "Grass_01_Dark", new Vector2(8f, 8f));
        p.cliff = Layer("RiverRocks_04");
        p.road = Layer("Pine_Dirt");
        p.ravineFloor = Layer("RiverRocks_01");
        p.courtyard = KingdomLayer("Kingdom_Mud_01", "Mud_01", new Vector2(6f, 6f));
        p.terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");

        p.cliffs = Prefabs(env, "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_02", "SM_Env_Rock_Cliff_03",
            "SM_Env_Rock_Cliff_04", "SM_Env_Rock_Cliff_05");
        p.cliffMaterial = null;
        p.largeRocks = Prefabs(env, "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_Chunk_03");
        p.mediumRocks = Prefabs(env, "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_Chunk_01", "SM_Env_Rock_Chunk_02");
        p.smallRocks = Prefabs(env, "SM_Env_Rock_Chunk_01", "SM_Env_Rock_Chunk_02");
        // The "snow" role is growth banked against rocks and walls: bushes and ferns here.
        p.snowMounds = Prefabs(env, "SM_Env_Bush_01", "SM_Env_Bush_03", "SM_Env_Bush_Flowers_02", "SM_Env_Fern_01",
            "SM_Env_Fern_03");
        p.trees = Prefabs(env, "SM_Env_Tree_Round_01", "SM_Env_Tree_Round_02", "SM_Env_Tree_Round_03",
            "SM_Env_Tree_Thin_01", "SM_Env_Tree_Thin_03", "SM_Env_Tree_Large_01");
        p.deadTrees = Prefabs(env, "SM_Env_Tree_Thin_05", "SM_Env_Bush_Cluster_02", "SM_Env_Tree_Dead_01");
        p.ravineWall = null;
        p.ravineWallRocks = new List<GameObject>();
        p.propClusters = DefenseClusterHarvester.Harvest("Assets/Synty/PolygonFantasyKingdom/Scenes/Demo_ExteriorOnly_Optimized.unity",
            new[]
            {
                "SM_Env_Bush", "SM_Env_Flowers", "SM_Env_Fern", "SM_Env_Grass_Tuft", "SM_Env_Rock", "SM_Env_Tree_Thin",
                "SM_Env_Sunflower", "SM_Prop_Log", "SM_Env_Reeds"
            },
            new[] { "SM_Env_Rock", "SM_Env_Bush_Cluster", "SM_Env_Tree_Thin", "SM_Prop_Log" },
            minPieces: 5);

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
        p.fieldLitter.AddRange(Prefabs(env, "SM_Env_Grass_Tuft_01", "SM_Env_Grass_Tuft_02", "SM_Env_Flowers_01",
            "SM_Env_Flowers_03", "SM_Env_Rock_Chunk_01"));
        p.courtyardProps = Prefabs(Kingdom, "SM_Prop_Barrel_01", "SM_Prop_Camp_Brazier_01", "SM_Prop_Crate_01",
            "SM_Prop_Cart_01", "SM_Prop_Banner_01");

        p.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        p.volumeProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(
            $"{Alpine}/Scene/Demo_01/AlpineGlobalVolume.asset");
        // A warm, clear morning: the first look at the outside world.
        p.sunColor = new Color(1f, 0.93f, 0.8f);
        p.sunIntensity = 1.45f;
        p.sunEuler = new Vector3(40f, -30f, 0f);
        p.fogColor = new Color(0.66f, 0.78f, 0.86f);
        p.fogDensity = 0.0028f;
        p.ambientSky = new Color(0.6f, 0.72f, 0.85f);
        p.ambientEquator = new Color(0.5f, 0.6f, 0.52f);
        p.ambientGround = new Color(0.28f, 0.3f, 0.22f);

        EditorUtility.SetDirty(p);
        return p;
    }

    /// <summary>A Fantasy Kingdom ground texture as a terrain layer (the pack ships them only as mesh materials).</summary>
    private static TerrainLayer KingdomLayer(string assetName, string texture, Vector2 tile)
    {
        string path = $"{ConfigFolder}/{assetName}.terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Synty/PolygonFantasyKingdom/Textures/Ground/{texture}.png");
        layer.tileSize = tile;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    /// <summary>
    ///     The tutorial field: a short, open valley with no ravines, two plots by the gate and one wave's worth
    ///     of spawns straight up the middle. Kept small so the first fight is readable.
    /// </summary>
    private static void CreateTutorialGateSpec(DefenseBiomePaletteSO palette)
    {
        var s = LoadOrCreate<DefenseSceneSpecSO>(TutorialGateSpecPath);
        s.scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Tutorial Gate.unity";
        s.seed = 1601;
        s.palette = palette;
        s.fieldHalfWidth = 55f;
        s.fieldEndZ = 125f;
        s.flareLength = 30f;
        s.mountainHeight = 55f;
        s.ravines = new List<RavineSpec>();
        s.towerPlots = new List<Vector2> { new Vector2(-11f, 15f), new Vector2(11f, 15f) };
        s.enemySpawns = new List<Vector2>
        {
            new Vector2(-32f, 104f), new Vector2(-24f, 110f), new Vector2(-16f, 104f), new Vector2(-8f, 112f),
            new Vector2(0f, 106f), new Vector2(8f, 112f), new Vector2(16f, 104f), new Vector2(24f, 110f),
            new Vector2(32f, 104f)
        };
        s.objectiveZRange = new Vector2(62f, 100f);
        s.castleBuildings = LayoutCastle(s);
        EditorUtility.SetDirty(s);
    }

    /// <summary>
    ///     Night in a graveyard: dark mud, moss and leaf litter (the Enchanted Forest terrain layers), grey Fantasy
    ///     Kingdom rock, dead pines on the mountains, roots and mushrooms banked against the rocks. Grave plots are
    ///     procedural prop clusters (rows of stones and mounds, an angel plot, a crypt) stamped between the lanes;
    ///     stone lanterns and street lamps light every road and bridge head; green fog hangs over the field and pools
    ///     in the spike ravines.
    /// </summary>
    private static DefenseBiomePaletteSO CreateGraveyardPalette()
    {
        var p = LoadOrCreate<DefenseBiomePaletteSO>(GraveyardPalettePath);
        string env = $"{Kingdom}/Environments";
        string props = $"{Kingdom}/Props";
        string ef = $"{Enchanted}/Prefabs";

        p.ground = Layer("Mud_01", Enchanted);
        p.groundVariant = Layer("Leaves_Terrain_01", Enchanted);
        p.slope = Layer("Moss_01", Enchanted);
        p.cliff = Layer("RiverRocks_04");
        p.road = Layer("Dirt_01", Enchanted);
        p.ravineFloor = Layer("RiverRocks_01");
        p.courtyard = KingdomLayer("Kingdom_Mud_01", "Mud_01", new Vector2(6f, 6f));
        p.ravineWall = null;
        p.terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");

        p.cliffs = Prefabs(env, "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_02", "SM_Env_Rock_Cliff_03",
            "SM_Env_Rock_Cliff_04", "SM_Env_Rock_Cliff_05");
        p.cliffMaterial = null;
        p.largeRocks = Prefabs(env, "SM_Env_Rock_03", "SM_Env_Rock_04", "SM_Env_Rock_Chunk_03");
        p.mediumRocks = Prefabs(env, "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_Chunk_01", "SM_Env_Rock_Chunk_02");
        p.smallRocks = Prefabs(env, "SM_Env_Rock_Chunk_01", "SM_Env_Rock_Chunk_02");
        // The "snow" role is growth banked against rocks and walls: roots, moss and mushrooms here.
        p.snowMounds = Prefabs(ef, "SM_Env_Roots_Small_01", "SM_Env_Roots_Small_03", "SM_Env_Moss_Lumps_01",
            "SM_Env_Moss_Lumps_03", "SM_Env_Mushroom_Small_Group_02", "SM_Env_Mushroom_Small_Group_05");
        p.trees = Prefabs($"{Alpine}/Prefabs", "SM_Env_Pine_NoLeaves_01");
        p.trees.AddRange(Prefabs($"{Arid}/Prefabs", "SM_Env_Tree_Dead_01", "SM_Env_Tree_Dead_02"));
        p.deadTrees = Prefabs($"{Arid}/Prefabs", "SM_Env_Tree_Dead_01", "SM_Env_Tree_Dead_02");
        p.deadTrees.AddRange(Prefabs($"{Alpine}/Prefabs", "SM_Env_Pine_NoLeaves_01", "SM_Env_Pine_Stump_01"));
        // Tall, narrow dungeon rock piles stand shoulder to shoulder in the pit walls.
        p.ravineWallRocks = Prefabs($"{Dungeon}/Environments/Rocks", "SM_Env_RockPile_Pillar_01", "SM_Env_RockPile_Pillar_02",
            "SM_Env_RockPile_Pillar_03", "SM_Env_RockPile_Rounded_01", "SM_Env_RockPile_Rounded_02");

        p.lanterns = Prefabs(Dungeon, "SM_Env_Stone_Lantern_01");
        p.lanterns.AddRange(Prefabs(props, "SM_Prop_Street_Lamp_01", "SM_Prop_Street_Lamp_02"));
        p.lanternScale = new Vector2(1f, 1.15f);
        p.lanternSpacing = 13f;
        p.lanternLightColor = new Color(1f, 0.74f, 0.42f);
        p.lanternLightIntensity = 3.5f;
        p.lanternLightRange = 10f;
        p.lanternLightHeight = 0.78f;
        p.groundFog = Prefabs("Assets/Synty/PolygonGeneric/Prefabs/FX", "FX_Fog_01");
        p.groundFogTint = new Color(0.45f, 1f, 0.55f, 1.6f);
        p.groundFogSpacing = 30f;
        p.ravineGlowIntensity = 4f;
        p.ravineGlowColor = new Color(0.35f, 1f, 0.42f);
        p.ravineGlowRange = 11f;

        p.propClusters = GraveyardClusters(p.lanterns);

        p.gate = Prefab(Kingdom, "SM_Bld_Castle_Wall_Gate_L_01");
        p.wall = Prefab(Kingdom, "SM_Bld_Castle_Wall_01");
        p.wallTower = Prefab(Kingdom, "SM_Bld_Castle_Wall_Tower_M_01");
        p.bridgeTile = Prefab(Kingdom, "SM_Bld_Bridge_01");
        p.bridgePillar = Prefab(Kingdom, "SM_Bld_Bridge_Pillars_01");
        // Arched stone bridges (20 m span), widened so a horde fits across.
        p.bridgeSpan = Prefab(env, "SM_Env_Bridge_Stone_01");
        p.bridgeSpanWidthScale = 1.6f;
        p.pitSpikes = Prefabs(Dungeon, "SM_Env_Trap_Spikes_01");
        p.defenseStakes = Prefabs(Kingdom, "SM_Prop_Spike_Fortification_01", "SM_Prop_Spike_Fortification_02",
            "SM_Prop_Spike_Fortification_03");
        p.banners = Prefabs(Kingdom, "SM_Prop_Battle_Banner_01", "SM_Prop_Battle_Banner_02", "SM_Prop_Battle_Banner_03");
        p.fieldLitter = Prefabs(props, "SM_Prop_Gravestone_Broken_01", "SM_Prop_Gravestone_Broken_02",
            "SM_Prop_Gravestone_03");
        p.fieldLitter.AddRange(Prefabs(ef, "SM_Env_Branch_01", "SM_Env_Branch_03", "SM_Env_Leaves_01",
            "SM_Env_Mushroom_Small_Group_01", "SM_Env_Roots_Small_02"));
        p.fieldLitter.AddRange(Prefabs($"{Arid}/Prefabs", "SM_Prop_Bones_01", "SM_Prop_Bones_03"));
        p.courtyardProps = Prefabs(Kingdom, "SM_Prop_Barrel_01", "SM_Prop_Camp_Brazier_01", "SM_Prop_Crate_01",
            "SM_Prop_Cart_01", "SM_Prop_Banner_01");

        p.skybox = GraveyardSkybox();
        p.volumeProfile = GraveyardVolume();
        // Moonlight: dim, cold and green-tinged, low in the sky so the lanterns carry the scene.
        p.sunColor = new Color(0.55f, 0.72f, 0.68f);
        p.sunIntensity = 0.6f;
        p.sunEuler = new Vector3(28f, -50f, 0f);
        p.fogColor = new Color(0.17f, 0.32f, 0.21f);
        p.fogDensity = 0.0065f;
        p.ambientSky = new Color(0.2f, 0.32f, 0.27f);
        p.ambientEquator = new Color(0.14f, 0.22f, 0.17f);
        p.ambientGround = new Color(0.07f, 0.08f, 0.07f);

        EditorUtility.SetDirty(p);
        return p;
    }

    /// <summary>
    ///     Grave plots as prop clusters (fixed seed, so a refresh rebuilds the same ones): rows of headstones with
    ///     dirt mounds, a dense old plot of leaning and broken stones, an angel statue ringed by graves, a crypt
    ///     entrance flanked by graves and lamps, and a scatter of broken stones round an open coffin.
    /// </summary>
    private static List<PropCluster> GraveyardClusters(List<GameObject> lanterns)
    {
        string props = $"{Kingdom}/Props";
        List<GameObject> stones = Prefabs(props, "SM_Prop_Gravestone_01", "SM_Prop_Gravestone_02", "SM_Prop_Gravestone_03",
            "SM_Prop_Gravestone_04", "SM_Prop_Gravestone_05");
        List<GameObject> broken = Prefabs(props, "SM_Prop_Gravestone_Broken_01", "SM_Prop_Gravestone_Broken_02");
        GameObject mound = Prefab(props, "SM_Prop_Gravestone_Dirt_01");
        GameObject angel = Prefab(props, "SM_Prop_Statue_Angel_01");
        GameObject crypt = Prefab($"{Dungeon}/Environments", "SM_Env_Entrance_Crypt_01");
        GameObject coffin = Prefab(Dungeon, "SM_Prop_Coffin_01");
        GameObject stoneLantern = lanterns.Count > 0 ? lanterns[0] : null;
        var rng = new System.Random(4031);
        const float StoneScale = 1.2f; // the Kingdom headstones read small next to a horde
        var clusters = new List<PropCluster>();

        clusters.Add(Rows("GraveRows_Mounded", 3, 2, 2.4f, 4.6f, 0.75f, 0f));
        clusters.Add(Rows("GraveRows_Wide", 4, 2, 2.3f, 4.6f, 0.6f, 0.1f));
        clusters.Add(Rows("GraveRows_Old", 4, 3, 1.9f, 2.6f, 0f, 0.35f));
        clusters.Add(Rows("GraveRows_Small", 2, 2, 2.4f, 4.6f, 1f, 0f));

        // Angel statue ringed by graves facing it.
        if (angel != null)
        {
            var c = new PropCluster { name = "AngelPlot" };
            Add(c, angel, Vector3.zero, 0f, 0f, 1f);
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.PI * 2f + 0.3f;
                var o = new Vector3(Mathf.Cos(a) * 4.6f, -0.05f, Mathf.Sin(a) * 4.6f);
                Add(c, Pick(rng.NextDouble() < 0.25 ? broken : stones), o, -a * Mathf.Rad2Deg - 90f, 7f, StoneScale);
            }
            if (stoneLantern != null)
            {
                Add(c, stoneLantern, new Vector3(-6.4f, -0.03f, 1.5f), 0f, 0f, 1f);
                Add(c, stoneLantern, new Vector3(6.4f, -0.03f, -1.5f), 0f, 0f, 1f);
            }
            clusters.Add(Finish(c));
        }

        // Crypt entrance with graves either side and a lamp on each flank.
        if (crypt != null)
        {
            var c = new PropCluster { name = "CryptPlot" };
            Add(c, crypt, new Vector3(0f, -0.1f, 0f), 0f, 0f, 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 3; k++)
                    Add(c, Pick(stones), new Vector3(side * (4.2f + k * 1.9f), -0.05f, 2.4f + Jit(0.3f)), Jit(8f), 5f, StoneScale);
                if (stoneLantern != null) Add(c, stoneLantern, new Vector3(side * 2.8f, -0.03f, 4.2f), 0f, 0f, 1f);
            }
            clusters.Add(Finish(c));
        }

        // Broken stones round an open coffin.
        {
            var c = new PropCluster { name = "Desecrated" };
            if (coffin != null) Add(c, coffin, new Vector3(0f, -0.05f, 0f), (float)rng.NextDouble() * 360f, 4f, 1f);
            if (mound != null) Add(c, mound, new Vector3(1.6f, -0.12f, 0.2f), 10f, 0f, 1f);
            for (int k = 0; k < 6; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = 2.2f + (float)rng.NextDouble() * 2.6f;
                Add(c, Pick(k % 2 == 0 ? broken : stones), new Vector3(Mathf.Cos(a) * d, -0.08f, Mathf.Sin(a) * d),
                    (float)rng.NextDouble() * 360f, 14f, StoneScale);
            }
            clusters.Add(Finish(c));
        }
        return clusters;

        // A plot of cols x rows headstones, all facing one way, with dirt mounds on some and a lamp at one corner.
        PropCluster Rows(string name, int cols, int rows, float dx, float dz, float moundChance, float brokenChance)
        {
            var c = new PropCluster { name = name };
            for (int r = 0; r < rows; r++)
            for (int k = 0; k < cols; k++)
            {
                if (rng.NextDouble() < 0.08) continue; // a missing stone reads older
                float x = (k - (cols - 1) * 0.5f) * dx + Jit(0.2f);
                float z = (r - (rows - 1) * 0.5f) * dz + Jit(0.2f);
                Add(c, Pick(rng.NextDouble() < brokenChance ? broken : stones), new Vector3(x, -0.05f, z), Jit(7f),
                    brokenChance > 0.2f ? 9f : 3f, StoneScale);
                if (mound != null && rng.NextDouble() < moundChance)
                    Add(c, mound, new Vector3(x, -0.14f, z + 1.75f), Jit(4f), 0f, 1f);
            }
            if (stoneLantern != null)
                Add(c, stoneLantern, new Vector3((cols * 0.5f + 0.4f) * dx, -0.03f, -(rows - 1) * 0.5f * dz - 1.2f), 0f, 0f, 1f);
            return Finish(c);
        }

        void Add(PropCluster c, GameObject prefab, Vector3 offset, float yaw, float lean, float scale)
        {
            if (prefab == null) return;
            c.pieces.Add(new ClusterPiece
            {
                prefab = prefab,
                offset = offset,
                rotation = Quaternion.Euler(Jit(lean), yaw, Jit(lean)),
                scale = Vector3.one * scale
            });
        }

        PropCluster Finish(PropCluster c)
        {
            foreach (ClusterPiece piece in c.pieces)
                c.radius = Mathf.Max(c.radius, new Vector2(piece.offset.x, piece.offset.z).magnitude);
            return c;
        }

        GameObject Pick(List<GameObject> list) => list[rng.Next(list.Count)];
        float Jit(float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;
    }

    /// <summary>The Synty gradient sky, recoloured to a near-black green night.</summary>
    private static Material GraveyardSkybox()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GraveyardSkyboxPath);
        if (mat == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PNB_Core/Materials/Skybox_Mat_01.mat");
            if (src == null) return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            mat = new Material(src);
            AssetDatabase.CreateAsset(mat, GraveyardSkyboxPath);
        }
        var top = new Color(0.01f, 0.03f, 0.025f);
        var bottom = new Color(0.12f, 0.24f, 0.16f);
        foreach (string prop in new[] { "_Top_Color", "_ColorTop" }) if (mat.HasProperty(prop)) mat.SetColor(prop, top);
        foreach (string prop in new[] { "_Bottom_Color", "_ColorBottom" }) if (mat.HasProperty(prop)) mat.SetColor(prop, bottom);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Post: ACES, bloom so the lamps glow, desaturated with a green cast, and a heavy vignette.</summary>
    private static VolumeProfile GraveyardVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GraveyardVolumePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GraveyardVolumePath);
        }

        var tone = Ensure<Tonemapping>();
        tone.mode.Override(TonemappingMode.ACES);
        var bloom = Ensure<Bloom>();
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(0.85f);
        bloom.scatter.Override(0.7f);
        bloom.tint.Override(new Color(1f, 0.92f, 0.75f));
        var grade = Ensure<ColorAdjustments>();
        grade.postExposure.Override(0.35f);
        grade.contrast.Override(12f);
        grade.saturation.Override(-22f);
        grade.colorFilter.Override(new Color(0.86f, 1f, 0.9f));
        var vignette = Ensure<Vignette>();
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0f, 0.04f, 0.02f));
        EditorUtility.SetDirty(profile);
        return profile;

        T Ensure<T>() where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T c = profile.Add<T>();
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }
    }

    /// <summary>
    ///     The Graveyard: one spike ravine crossed by two stone bridges, so the dead funnel onto two decks. Plots:
    ///     two by the gate, two on the outer flanks of the bridges and two right at the bridge exits (spike trap /
    ///     oil vat country). Skeletons only, via the spec's enemy list.
    /// </summary>
    private static void CreateGraveyardSpec(DefenseBiomePaletteSO palette)
    {
        var s = LoadOrCreate<DefenseSceneSpecSO>(GraveyardSpecPath);
        s.scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Graveyard.unity";
        s.seed = 6613;
        s.palette = palette;
        s.mountainHeight = 70f;

        s.towerPlots = new List<Vector2>
        {
            new Vector2(-12f, 14f), new Vector2(12f, 14f),
            new Vector2(-48f, 44f), new Vector2(48f, 44f),
            new Vector2(-26f, 39f), new Vector2(26f, 39f)
        };

        s.ravines = new List<RavineSpec>
        {
            new RavineSpec
            {
                z = 58f, topWidth = 12f, floorWidth = 6.5f, depth = 6.5f, meanderAmplitude = 2.5f, meanderWavelength = 130f,
                bridges = new List<BridgeSpec>
                {
                    new BridgeSpec { x = -26f, tilesWide = 1 },
                    new BridgeSpec { x = 26f, tilesWide = 1 }
                },
                exitRamps = new List<RampSpec>
                {
                    new RampSpec { x = -7f, direction = 1 },
                    new RampSpec { x = -57f, direction = -1 },
                    new RampSpec { x = 57f, direction = 1 }
                }
            }
        };

        s.enemySpawns = new List<Vector2>
        {
            new Vector2(-60f, 148f), new Vector2(-45f, 158f), new Vector2(-30f, 150f), new Vector2(-15f, 160f),
            new Vector2(0f, 152f), new Vector2(15f, 160f), new Vector2(30f, 150f), new Vector2(45f, 158f),
            new Vector2(60f, 148f), new Vector2(0f, 164f)
        };
        s.objectiveZRange = new Vector2(82f, 145f);

        s.enemyRosterIds = new List<string>
        {
            "skeleton_soldier", "skeleton_soldier_shield", "skeleton_knight", "skeleton_knight_shield"
        };
        s.fodderEnemyId = "skeleton_soldier";

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
