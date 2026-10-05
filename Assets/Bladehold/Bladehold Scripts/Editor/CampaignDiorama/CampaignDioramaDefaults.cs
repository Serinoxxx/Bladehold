using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
///     Creates the campaign diorama's assets the first time the builder runs: the theme (one look per biome,
///     scatter picked from the Synty Nature Biomes / Fantasy Kingdom packs), the runtime look, materials and
///     the post-processing profile. After that the assets are the source of truth: tweak them, rebuild.
/// </summary>
public static class CampaignDioramaDefaults
{
    public const string ConfigFolder = "Assets/Bladehold/Config/Campaign";
    public const string ThemePath = ConfigFolder + "/CampaignDioramaTheme.asset";
    public const string LookPath = ConfigFolder + "/CampaignDioramaLook.asset";
    public const string VolumePath = ConfigFolder + "/CampaignDioramaVolume.asset";
    public const string MaterialsFolder = "Assets/Bladehold/Bladehold Materials/Campaign Diorama";

    private const string Alpine = "Assets/Synty/PolygonNatureBiomes/PNB_Alpine_Mountain/";
    private const string Arid = "Assets/Synty/PolygonNatureBiomes/PNB_Arid_Desert/";
    private const string Enchanted = "Assets/Synty/PolygonNatureBiomes/PNB_Enchanted_Forest/";
    private const string Kingdom = "Assets/Synty/PolygonFantasyKingdom/";

    public static CampaignDioramaThemeSO LoadOrCreateTheme()
    {
        EnsureFolder(ConfigFolder);
        EnsureFolder(MaterialsFolder);

        CampaignDioramaThemeSO theme = AssetDatabase.LoadAssetAtPath<CampaignDioramaThemeSO>(ThemePath);
        bool created = false;
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<CampaignDioramaThemeSO>();
            FillBiomes(theme);
            AssetDatabase.CreateAsset(theme, ThemePath);
            created = true;
        }

        if (theme.look == null) theme.look = LoadOrCreate<CampaignDioramaLookSO>(LookPath);
        if (theme.vertexLitMaterial == null) theme.vertexLitMaterial = MakeMaterial("Diorama_VertexLit", "Bladehold/Diorama Vertex Lit", null);
        if (theme.roadGlowMaterial == null)
        {
            theme.roadGlowMaterial = MakeMaterial("Diorama_RoadGlow", "Bladehold/Diorama Glow FX", m =>
            {
                m.SetFloat("_Shape", 0f);
                m.SetColor("_GlowColor", new Color(0.85f, 0.8f, 0.7f, 0.12f)); // the locked look, until the map sets each route
            });
        }
        if (theme.ringGlowMaterial == null)
        {
            theme.ringGlowMaterial = MakeMaterial("Diorama_RingGlow", "Bladehold/Diorama Glow FX", m => m.SetFloat("_Shape", 1f));
        }
        if (theme.waterMaterial == null) theme.waterMaterial = MakeWater();
        if (theme.volumeProfile == null) theme.volumeProfile = MakeVolume();

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        if (created) Debug.Log($"[CampaignDiorama] Created theme at {ThemePath}.");
        return theme;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static Material MakeMaterial(string name, string shaderName, System.Action<Material> setup)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[CampaignDiorama] Shader '{shaderName}' not found.");
            return null;
        }
        mat = new Material(shader) { enableInstancing = true };
        setup?.Invoke(mat);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material MakeWater()
    {
        string path = $"{MaterialsFolder}/Diorama_Water.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // Transparent, glossy water
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Smoothness", 0.92f);
        mat.SetColor("_BaseColor", new Color(0.2f, 0.62f, 0.62f, 0.82f));
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static VolumeProfile MakeVolume()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (profile != null) return profile;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumePath);

        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0f);
        color.contrast.Override(10f);
        color.saturation.Override(2f);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.6f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);

        // Tilt-shift: in focus at the focus plane, the near table edge and the far mountains go soft.
        DepthOfField dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(31f);
        dof.focalLength.Override(120f);
        dof.aperture.Override(5.6f);

        foreach (VolumeComponent component in profile.components)
        {
            component.name = component.GetType().Name;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    // ───────────────────────────────────────────── biome looks

    private static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static void FillBiomes(CampaignDioramaThemeSO theme)
    {
        List<CampaignDioramaThemeSO.BiomeLook> list = theme.biomes;
        list.Clear();

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Grassland,
            ground = C("#6B9A3E"), groundAlt = C("#58873A"), slope = C("#8A8273"), road = C("#B08E5E"),
            relief = CampaignDioramaThemeSO.Relief.RollingHills, reliefHeight = 0.45f,
            wall = C("#B9B3A6"), roof = C("#3D62A8"), trim = C("#6E5038"), glow = C("#FFC066"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.Broadleaf, DioramaFlora.Kind.Broadleaf, DioramaFlora.Kind.Pine), leaf = C("#3F7A2E"), leafAlt = C("#5C9634"),
            treeDensity = 0.3f,
            rocks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03"),
            groundCover = P(Kingdom + "Prefabs/Environments/", "SM_Env_Bush_01", "SM_Env_Bush_03", "SM_Env_Bush_Flowers_02", "SM_Env_Flowers_01"),
            landmarks = P(Kingdom + "Prefabs/Props/", "SM_Prop_Battle_Banner_01"),
            landmarkHeight = new Vector2(0.3f, 0.36f), landmarksPerNode = 1
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Desert,
            ground = C("#DDB06A"), groundAlt = C("#CC9B58"), slope = C("#B9784A"), road = C("#C99B63"),
            relief = CampaignDioramaThemeSO.Relief.Dunes, reliefHeight = 0.55f, snowLine = 0f,
            wall = C("#D9B987"), roof = C("#C2603A"), trim = C("#8C5A32"), glow = C("#FFB04D"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Dome,
            flora = F(DioramaFlora.Kind.Cactus, DioramaFlora.Kind.Cactus, DioramaFlora.Kind.Dead), leaf = C("#5E8A3A"), leafAlt = C("#6F9A40"), trunk = C("#7A5C40"),
            treeHeight = new Vector2(0.35f, 0.65f), treeDensity = 0.12f,
            rocks = P(Arid + "Prefabs/", "SM_Env_Rock_07", "SM_Env_Rock_08", "SM_Env_Rocks_Spikey_01", "SM_Env_Rock_Rough_01"),
            rockHeight = new Vector2(0.1f, 0.3f), rockDensity = 0.1f,
            groundCover = P(Arid + "Prefabs/", "SM_Env_GroundCover_01", "SM_Env_Succulent_01", "SM_Env_Bush_Bramble_01"),
            groundCoverDensity = 0.15f,
            landmarks = P(Arid + "Prefabs/Props/", "SM_Prop_Bones_01", "SM_Prop_Bones_03", "SM_Prop_Tumbleweed_01", "SM_Prop_Tent_01"),
            landmarksPerNode = 4
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Alpine,
            ground = C("#E4ECF2"), groundAlt = C("#CFDCE6"), slope = C("#7C8590"), road = C("#9D8C78"),
            relief = CampaignDioramaThemeSO.Relief.Mountains, reliefHeight = 3.2f, snowLine = 1.2f,
            wall = C("#A9AFB6"), roof = C("#EEF3F8"), trim = C("#5C4A3A"), glow = C("#FFC77A"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.SnowPine, DioramaFlora.Kind.SnowPine, DioramaFlora.Kind.Pine), leaf = C("#2E5A3C"), leafAlt = C("#3A6B45"),
            treeHeight = new Vector2(0.8f, 1.35f), treeDensity = 0.6f,
            rocks = P(Alpine + "Prefabs/", "SM_Env_Rock_01", "SM_Env_Rock_03", "SM_Env_Rock_08", "SM_Env_Rock_Rough_01"),
            rockDensity = 0.08f,
            groundCover = P(Alpine + "Prefabs/", "SM_Env_Snow_Mound_01", "SM_Env_Snow_Mound_02", "SM_Env_Snow_Mound_03"),
            groundCoverDensity = 0.2f,
            landmarks = P(Alpine + "Prefabs/Props/", "SM_Prop_Campfire_01", "SM_Prop_Tent_01"),
            landmarksPerNode = 2
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Snowfield,
            ground = C("#E9EFF4"), groundAlt = C("#D8E2EA"), slope = C("#8C939C"), road = C("#A8957E"),
            relief = CampaignDioramaThemeSO.Relief.RollingHills, reliefHeight = 0.55f, snowLine = 0f,
            wall = C("#B4B0A8"), roof = C("#3B5E9E"), trim = C("#5C4A3A"), glow = C("#FFC77A"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.SnowPine, DioramaFlora.Kind.SnowPine, DioramaFlora.Kind.Dead), leaf = C("#2F5C40"), leafAlt = C("#3B6B48"),
            treeHeight = new Vector2(0.75f, 1.2f), treeDensity = 0.45f,
            rocks = P(Alpine + "Prefabs/", "SM_Env_Rock_01", "SM_Env_Rock_03", "SM_Env_Rock_Rough_01"),
            rockDensity = 0.06f,
            groundCover = P(Alpine + "Prefabs/", "SM_Env_Snow_Mound_01", "SM_Env_Snow_Mound_02", "SM_Env_Snow_Mound_03"),
            groundCoverDensity = 0.25f,
            landmarks = P(Alpine + "Prefabs/Props/", "SM_Prop_Campfire_01", "SM_Prop_Tent_01"),
            landmarksPerNode = 2
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Graveyard,
            ground = C("#4E4A42"), groundAlt = C("#45433E"), slope = C("#57514C"), road = C("#6A5E50"),
            relief = CampaignDioramaThemeSO.Relief.Lowland, reliefHeight = 0.35f,
            wall = C("#6F6C70"), roof = C("#4B3A5E"), trim = C("#3A3036"), glow = C("#9BFF8A"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.Dead, DioramaFlora.Kind.Dead, DioramaFlora.Kind.Pine), leaf = C("#3B4A36"), leafAlt = C("#46523C"), trunk = C("#4A4038"),
            treeDensity = 0.22f,
            rocks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Rock_Chunk_01", "SM_Env_Rock_Chunk_02"),
            groundCover = P(Enchanted + "Prefabs/", "SM_Env_Mushroom_Small_Group_02", "SM_Env_Mushroom_Small_Group_05", "SM_Env_Moss_Lumps_01"),
            groundCoverDensity = 0.2f,
            landmarks = P(Kingdom + "Prefabs/Props/", "SM_Prop_Gravestone_01", "SM_Prop_Gravestone_02", "SM_Prop_Gravestone_03", "SM_Prop_Gravestone_04", "SM_Prop_Gravestone_05", "SM_Prop_Gravestone_Broken_01"),
            landmarkHeight = new Vector2(0.1f, 0.16f), landmarksPerNode = 14
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Lake,
            ground = C("#2F6A3E"), groundAlt = C("#3A7A45"), slope = C("#5E6A58"), road = C("#9C8466"),
            relief = CampaignDioramaThemeSO.Relief.Lowland, reliefHeight = 0.35f,
            wall = C("#B7B7A8"), roof = C("#2A9C8E"), trim = C("#5C4632"), glow = C("#7FFFE0"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.Broadleaf, DioramaFlora.Kind.Broadleaf, DioramaFlora.Kind.Mushroom), leaf = C("#2B7A58"), leafAlt = C("#C2569E"),
            treeDensity = 0.45f,
            rocks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Rock_01", "SM_Env_Rock_02"),
            groundCover = P(Enchanted + "Prefabs/", "SM_Env_Fern_01", "SM_Env_Fern_Koru_01", "SM_Env_Mushroom_Small_Group_04")
                .Concat(P(Kingdom + "Prefabs/Environments/", "SM_Env_Reeds_01", "SM_Env_Reeds_02")),
            groundCoverDensity = 0.35f,
            landmarks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Reeds_01", "SM_Env_Reeds_02", "SM_Env_Reeds_03"),
            landmarksPerNode = 8
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Forest,
            ground = C("#2C5E36"), groundAlt = C("#356B3B"), slope = C("#5A5F50"), road = C("#8E7656"),
            relief = CampaignDioramaThemeSO.Relief.RollingHills, reliefHeight = 0.45f,
            wall = C("#9DA48E"), roof = C("#8A3F7A"), trim = C("#5A4330"), glow = C("#FFB861"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.Broadleaf, DioramaFlora.Kind.Broadleaf, DioramaFlora.Kind.Pine, DioramaFlora.Kind.Mushroom), leaf = C("#25683A"), leafAlt = C("#A8458E"),
            treeHeight = new Vector2(0.9f, 1.45f), treeDensity = 0.9f,
            rocks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Rock_01", "SM_Env_Rock_Chunk_01"),
            rockDensity = 0.04f,
            groundCover = P(Enchanted + "Prefabs/", "SM_Env_Fern_01", "SM_Env_Fern_02", "SM_Env_Mushroom_01", "SM_Env_Mushroom_03", "SM_Env_Moss_Lumps_02"),
            groundCoverDensity = 0.45f,
            landmarks = P(Enchanted + "Prefabs/Props/", "SM_Prop_Crystal_02", "SM_Prop_Crystal_04", "SM_Prop_Lantern_Stand_01"),
            landmarkHeight = new Vector2(0.15f, 0.28f), landmarksPerNode = 4
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.CastleCourt,
            ground = C("#8A8274"), groundAlt = C("#A59D8C"), slope = C("#77726A"), road = C("#BDB39E"),
            relief = CampaignDioramaThemeSO.Relief.Lowland, reliefHeight = 0.2f,
            wall = C("#CFC8B8"), roof = C("#A6372E"), trim = C("#7D5A3C"), glow = C("#FFC066"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.Broadleaf, DioramaFlora.Kind.Pine), leaf = C("#3E7530"), leafAlt = C("#558A36"),
            treeDensity = 0.12f,
            rocks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Rock_02"),
            rockDensity = 0.02f,
            groundCover = P(Kingdom + "Prefabs/Environments/", "SM_Env_Bush_01", "SM_Env_Bush_Flowers_02"),
            groundCoverDensity = 0.15f,
            landmarks = P(Kingdom + "Prefabs/Buildings/Presets/", "SM_Bld_Preset_House_01_A_Optimized", "SM_Bld_Preset_House_03_Optimized",
                "SM_Bld_Preset_House_05_Optimized", "SM_Bld_Preset_House_07_Optimized", "SM_Bld_Preset_House_08_Optimized", "SM_Bld_Preset_Tower_01_Optimized"),
            landmarkHeight = new Vector2(0.7f, 1.0f), landmarksPerNode = 8
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Crypt,
            ground = C("#3E3A45"), groundAlt = C("#35323D"), slope = C("#4A4550"), road = C("#4F4756"),
            relief = CampaignDioramaThemeSO.Relief.Rocky, reliefHeight = 0.9f, snowLine = 0f,
            wall = C("#4A4652"), roof = C("#3B2A52"), trim = C("#2A2430"), glow = C("#B46BFF"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.Dead), leaf = C("#3A3242"), leafAlt = C("#463A50"), trunk = C("#3A3238"),
            treeDensity = 0.12f,
            rocks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Rock_Chunk_01", "SM_Env_Rock_Chunk_02", "SM_Env_Rock_Chunk_03"),
            rockDensity = 0.1f,
            groundCover = P(Enchanted + "Prefabs/", "SM_Env_Mushroom_Small_Group_01", "SM_Env_Mushroom_Small_Group_03"),
            groundCoverDensity = 0.12f,
            landmarks = P(Enchanted + "Prefabs/Props/", "SM_Prop_Crystal_01", "SM_Prop_Crystal_03", "SM_Prop_Crystal_05")
                .Concat(P(Kingdom + "Prefabs/Props/", "SM_Prop_Gravestone_Broken_01", "SM_Prop_Gravestone_Broken_02")),
            landmarkHeight = new Vector2(0.14f, 0.3f), landmarksPerNode = 9
        });

        list.Add(new CampaignDioramaThemeSO.BiomeLook
        {
            biome = CampaignBiome.Sanctuary,
            ground = C("#8DB35C"), groundAlt = C("#9FC06A"), slope = C("#B4AC9C"), road = C("#D8CDB4"),
            relief = CampaignDioramaThemeSO.Relief.RollingHills, reliefHeight = 0.3f,
            wall = C("#F1ECE2"), roof = C("#D98AA8"), trim = C("#D9B25A"), glow = C("#FFE3A8"),
            roofShape = CampaignDioramaThemeSO.RoofShape.Cone,
            flora = F(DioramaFlora.Kind.Blossom, DioramaFlora.Kind.Blossom, DioramaFlora.Kind.Broadleaf), leaf = C("#F2A7C3"), leafAlt = C("#FFD1E0"), trunk = C("#6E5040"),
            treeDensity = 0.22f,
            rocks = P(Kingdom + "Prefabs/Environments/", "SM_Env_Rock_02"),
            rockDensity = 0.04f,
            groundCover = P(Kingdom + "Prefabs/Environments/", "SM_Env_Flowers_01", "SM_Env_Flowers_03", "SM_Env_Bush_Flowers_02"),
            groundCoverDensity = 0.55f,
            landmarks = P(Enchanted + "Prefabs/Props/", "SM_Prop_Crystal_02", "SM_Prop_Crystal_04", "SM_Prop_Lantern_Stand_01"),
            landmarkHeight = new Vector2(0.16f, 0.3f), landmarksPerNode = 5
        });
    }

    /// <summary>Loads prefabs by name from a folder (searching subfolders); missing names are reported, not fatal.</summary>
    private static List<GameObject> P(string folder, params string[] names)
    {
        List<GameObject> found = new List<GameObject>();
        string searchRoot = folder.TrimEnd('/');
        foreach (string n in names)
        {
            GameObject go = null;
            foreach (string guid in AssetDatabase.FindAssets($"{n} t:Prefab", new[] { RootOf(searchRoot) }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != n) continue;
                if (go == null || path.StartsWith(searchRoot)) go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            if (go != null) found.Add(go);
            else Debug.LogWarning($"[CampaignDiorama] Default scatter prefab '{n}' not found under {searchRoot}.");
        }
        return found;
    }

    // Search the whole pack, so 'Prefabs/' vs 'Prefabs/Props/' guesses still resolve.
    private static string RootOf(string folder)
    {
        int prefabs = folder.IndexOf("/Prefabs");
        return prefabs > 0 ? folder.Substring(0, prefabs) : folder;
    }

    private static List<DioramaFlora.Kind> F(params DioramaFlora.Kind[] kinds) => new List<DioramaFlora.Kind>(kinds);

    private static List<GameObject> Concat(this List<GameObject> a, List<GameObject> b)
    {
        a.AddRange(b);
        return a;
    }
}
