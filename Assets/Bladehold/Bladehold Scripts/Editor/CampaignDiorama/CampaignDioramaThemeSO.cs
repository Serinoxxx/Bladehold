using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
///     Everything <see cref="CampaignDioramaBuilder" /> dresses the campaign map diorama with: one look per
///     <see cref="CampaignBiome" /> (ground colours, relief, castle colours, Synty scatter), the table/base
///     colours, lighting and the materials. Colours are authored in sRGB; the builder writes them into vertex
///     colours in linear space. Re-run <b>Bladehold > Campaign > Build Map Diorama</b> after editing.
/// </summary>
[CreateAssetMenu(fileName = "CampaignDioramaTheme", menuName = "Scriptable Objects/Campaign/Campaign Diorama Theme")]
public class CampaignDioramaThemeSO : ScriptableObject
{
    public enum Relief
    {
        RollingHills,
        Dunes,
        Mountains,
        Lowland,
        Rocky
    }

    public enum RoofShape
    {
        Cone,
        Dome,
        Battlement
    }

    [System.Serializable]
    public class BiomeLook
    {
        public CampaignBiome biome = CampaignBiome.Grassland;

        [Header("Ground")]
        public Color ground = new Color(0.45f, 0.62f, 0.3f);
        [Tooltip("Second ground colour, blended in by noise.")]
        public Color groundAlt = new Color(0.38f, 0.56f, 0.26f);
        [Tooltip("Steep slopes and cliffs.")]
        public Color slope = new Color(0.5f, 0.47f, 0.42f);
        public Color road = new Color(0.62f, 0.5f, 0.34f);
        public Relief relief = Relief.RollingHills;
        [Tooltip("Height of the relief in world units (the mini castles are about 1.5 tall).")]
        public float reliefHeight = 0.4f;
        [Tooltip("Peaks above this height get a snow cap (0 = never).")]
        public float snowLine = 2.1f;

        [Header("Castle")]
        public Color wall = new Color(0.7f, 0.68f, 0.64f);
        public Color roof = new Color(0.22f, 0.36f, 0.62f);
        public Color trim = new Color(0.45f, 0.33f, 0.22f);
        [Tooltip("Lit windows and fires (emissive).")]
        public Color glow = new Color(1f, 0.72f, 0.35f);
        public RoofShape roofShape = RoofShape.Cone;

        [Header("Trees (procedural, model-railway style)")]
        public List<DioramaFlora.Kind> flora = new List<DioramaFlora.Kind> { DioramaFlora.Kind.Broadleaf };
        public Color leaf = new Color(0.27f, 0.5f, 0.2f);
        public Color leafAlt = new Color(0.36f, 0.58f, 0.22f);
        public Color trunk = new Color(0.4f, 0.27f, 0.17f);
        public Vector2 treeHeight = new Vector2(0.8f, 1.3f);
        [Tooltip("Trees per square world unit where this biome dominates.")]
        public float treeDensity = 0.35f;

        [Header("Scatter (Synty prefabs, scaled to diorama size)")]
        public List<GameObject> rocks = new List<GameObject>();
        public Vector2 rockHeight = new Vector2(0.07f, 0.2f);
        public float rockDensity = 0.06f;
        public List<GameObject> groundCover = new List<GameObject>();
        public Vector2 groundCoverHeight = new Vector2(0.07f, 0.14f);
        public float groundCoverDensity = 0.35f;
        [Tooltip("Landmark props clustered around the node itself (gravestones, crystals, tents...).")]
        public List<GameObject> landmarks = new List<GameObject>();
        public Vector2 landmarkHeight = new Vector2(0.12f, 0.2f);
        public int landmarksPerNode = 6;
    }

    [Header("Layout")]
    [Tooltip("World units per map unit (CampaignNodeSO.mapPosition). 1/32 puts tier columns about 8 units apart.")]
    public float worldPerMapUnit = 1f / 32f;
    [Tooltip("Board margin around the outermost nodes: x = left/right, y = front, z = back (the back holds a mountain backdrop).")]
    public Vector3 boardMargin = new Vector3(9.5f, 6f, 7.5f);
    [Tooltip("Terrain grid spacing; smaller is smoother but heavier.")]
    public float gridStep = 0.4f;
    public int seed = 1207;

    [Header("Biomes")]
    public List<BiomeLook> biomes = new List<BiomeLook>();

    [Header("Base, table & roads")]
    public Color soil = new Color(0.33f, 0.24f, 0.16f);
    public Color strataA = new Color(0.47f, 0.4f, 0.33f);
    public Color strataB = new Color(0.38f, 0.32f, 0.27f);
    public Color plinthWood = new Color(0.24f, 0.15f, 0.09f);
    public Color plinthTrim = new Color(0.78f, 0.6f, 0.28f);
    public Color table = new Color(0.12f, 0.085f, 0.06f);
    public Color snow = new Color(0.93f, 0.95f, 1f);
    public Color water = new Color(0.2f, 0.62f, 0.62f, 0.82f);
    public float roadWidth = 0.34f;

    [Header("Lighting")]
    public Color sunColor = new Color(1f, 0.92f, 0.8f);
    public float sunIntensity = 1.25f;
    public Vector3 sunEuler = new Vector3(36f, -38f, 0f);
    public Color ambientSky = new Color(0.62f, 0.68f, 0.8f);
    public Color ambientEquator = new Color(0.5f, 0.48f, 0.45f);
    public Color ambientGround = new Color(0.22f, 0.18f, 0.15f);
    public Color backgroundColor = new Color(0.07f, 0.055f, 0.045f);
    public Color fogColor = new Color(0.1f, 0.08f, 0.07f);
    [Tooltip("Linear fog start/end distance from the camera (darkens the far table edge).")]
    public Vector2 fogRange = new Vector2(38f, 75f);

    [Header("Camera")]
    public float cameraPitch = 52f;
    public float cameraDistance = 31f;
    public float cameraFov = 30f;

    [Header("Assets")]
    public Material vertexLitMaterial;
    public Material roadGlowMaterial;
    public Material ringGlowMaterial;
    public Material waterMaterial;
    public VolumeProfile volumeProfile;
    public CampaignDioramaLookSO look;

    public BiomeLook Get(CampaignBiome biome)
    {
        foreach (BiomeLook b in biomes)
        {
            if (b != null && b.biome == biome) return b;
        }
        foreach (BiomeLook b in biomes)
        {
            if (b != null && b.biome == CampaignBiome.Grassland) return b;
        }
        return biomes.Count > 0 ? biomes[0] : new BiomeLook();
    }
}
