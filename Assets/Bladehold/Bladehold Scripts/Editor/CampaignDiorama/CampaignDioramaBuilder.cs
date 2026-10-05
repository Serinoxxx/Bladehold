using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
///     Generates the campaign map diorama in the campaign map scene from the campaign graph and
///     <see cref="CampaignDioramaThemeSO" />: a miniature landscape on a wooden plinth whose terrain takes on
///     each node's biome (snowy mountains, dunes, graveyards, lakes...), roads along every graph edge, one
///     mini castle per node, Synty scatter, lighting, post and the camera rig; then rewires the map UI so the
///     node labels hang under the castles. Deterministic from the theme's seed. Never hand-edit the output:
///     change the theme/graph and run <b>Bladehold > Campaign > Build Map Diorama</b> again.
/// </summary>
public static class CampaignDioramaBuilder
{
    public const string ScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Campaign Map Scene.unity";
    private const string GraphPath = "Assets/Bladehold/Resources/CampaignGraph.asset";
    private const string MeshAssetPath = "Assets/Bladehold/Config/Campaign/CampaignDioramaMeshes.asset";
    private const string RootName = "CampaignDiorama";

    private class NodeInfo
    {
        public CampaignNodeSO node;
        public Vector2 pos; // world XZ
        public CampaignBiome biome;
        public CampaignDioramaThemeSO.BiomeLook look;
        public MiniCastleBuilder.Kind kind;
        public float plateau;
        public Vector2? lake;
        public float lakeRadius;
    }

    private class Road
    {
        public NodeInfo from;
        public NodeInfo to;
        public readonly List<Vector2> points = new List<Vector2>();
    }

    private static CampaignDioramaThemeSO theme;
    private static List<NodeInfo> nodes;
    private static List<Road> roads;
    private static Rect board;
    private static float noiseX, noiseZ;
    private static System.Random rng;

    [MenuItem("Bladehold/Campaign/Build Map Diorama", priority = 20)]
    public static void BuildMenu()
    {
        Build();
    }

    public static bool Build()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path != ScenePath)
        {
            if (active.isDirty)
            {
                Debug.LogError($"[CampaignDiorama] Save or discard changes in '{active.path}' first; the builder opens the campaign map scene.");
                return false;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        Scene scene = SceneManager.GetActiveScene();

        theme = CampaignDioramaDefaults.LoadOrCreateTheme();
        CampaignGraphSO graph = AssetDatabase.LoadAssetAtPath<CampaignGraphSO>(GraphPath);
        if (graph == null || graph.allNodes == null || graph.allNodes.Count == 0)
        {
            Debug.LogError($"[CampaignDiorama] No campaign graph at {GraphPath}.");
            return false;
        }

        rng = new System.Random(theme.seed);
        noiseX = 100f + theme.seed % 997;
        noiseZ = 300f + theme.seed % 577;
        PrefabMeasure.ClearCache();

        CollectNodes(graph);
        CollectRoads();
        PlaceLakes();

        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        GameObject root = new GameObject(RootName);
        CampaignDiorama diorama = root.AddComponent<CampaignDiorama>();

        List<Mesh> meshes = new List<Mesh>();
        BuildTerrain(root.transform, meshes);
        BuildWater(root.transform, meshes);
        List<CampaignDioramaSite> sites = BuildCastles(root.transform, meshes);
        List<CampaignDioramaRoad> roadComponents = BuildRoadOverlays(root.transform, meshes);
        Scatter(root.transform, meshes);
        SaveMeshes(meshes);

        CampaignDioramaCamera cameraRig = SetupCameraAndLighting(root.transform);
        float s = theme.worldPerMapUnit;
        diorama.EditorSetup(theme.look, cameraRig, sites.ToArray(), roadComponents.ToArray(), new Vector3(-MinMapX(graph) * s, 0f, 0f), s);
        EditorUtility.SetDirty(diorama);

        CampaignDioramaUIRig.Rewire(diorama);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CampaignDiorama] Built {sites.Count} castles, {roadComponents.Count} roads on a {board.width:0}x{board.height:0} board.");
        return true;
    }

    private static float MinMapX(CampaignGraphSO graph)
    {
        float min = float.MaxValue;
        foreach (CampaignNodeSO n in graph.allNodes) if (n != null) min = Mathf.Min(min, n.mapPosition.x);
        return min;
    }

    // ───────────────────────────────────────────── layout

    private static void CollectNodes(CampaignGraphSO graph)
    {
        nodes = new List<NodeInfo>();
        float s = theme.worldPerMapUnit;
        float minX = MinMapX(graph);
        foreach (CampaignNodeSO n in graph.allNodes)
        {
            if (n == null) continue;
            CampaignBiome biome = CampaignBiomes.Resolve(n);
            NodeInfo info = new NodeInfo
            {
                node = n,
                pos = new Vector2((n.mapPosition.x - minX) * s, n.mapPosition.y * s),
                biome = biome,
                look = theme.Get(biome),
                kind = MiniCastleBuilder.KindFor(n)
            };
            info.plateau = 0.14f + (biome == CampaignBiome.CastleCourt ? 0.18f : 0f) + (biome == CampaignBiome.Alpine ? 0.3f : 0f) + (biome == CampaignBiome.Snowfield ? 0.12f : 0f)
                           + (biome == CampaignBiome.Sanctuary ? 0.12f : 0f) + (biome == CampaignBiome.Crypt ? 0.1f : 0f);
            nodes.Add(info);
        }

        float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
        foreach (NodeInfo n in nodes)
        {
            xMin = Mathf.Min(xMin, n.pos.x);
            xMax = Mathf.Max(xMax, n.pos.x);
            zMin = Mathf.Min(zMin, n.pos.y);
            zMax = Mathf.Max(zMax, n.pos.y);
        }
        Vector3 m = theme.boardMargin;
        board = Rect.MinMaxRect(xMin - m.x, zMin - m.y, xMax + m.x, zMax + m.z);
    }

    private static NodeInfo Find(CampaignNodeSO n)
    {
        foreach (NodeInfo info in nodes) if (info.node == n) return info;
        return null;
    }

    private static void CollectRoads()
    {
        roads = new List<Road>();
        foreach (NodeInfo from in nodes)
        {
            if (from.node.nextNodes == null) continue;
            foreach (CampaignNodeSO next in from.node.nextNodes)
            {
                NodeInfo to = next != null ? Find(next) : null;
                if (to == null) continue;
                Road road = new Road { from = from, to = to };
                // Gentle S-curve: leave and arrive heading along the route (+X).
                Vector2 a = from.pos;
                Vector2 d = to.pos;
                float handle = Mathf.Abs(d.x - a.x) * 0.42f;
                Vector2 b = a + new Vector2(handle, 0f);
                Vector2 c = d - new Vector2(handle, 0f);
                float length = Vector2.Distance(a, d);
                int steps = Mathf.Max(8, Mathf.CeilToInt(length / 0.2f));
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float u = 1f - t;
                    road.points.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
                }
                roads.Add(road);
            }
        }
    }

    /// <summary>Each lake node gets a pond beside its castle, on whichever side is furthest from roads and other castles.</summary>
    private static void PlaceLakes()
    {
        foreach (NodeInfo n in nodes)
        {
            if (n.biome != CampaignBiome.Lake) continue;
            n.lakeRadius = 1.05f + (float)rng.NextDouble() * 0.25f;
            float best = float.MinValue;
            Vector2 bestCenter = n.pos + new Vector2(0f, 1.8f);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                Vector2 c = n.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (n.lakeRadius + 0.95f);
                float clearance = RoadDistance(c) - n.lakeRadius;
                foreach (NodeInfo o in nodes)
                {
                    if (o == n) continue;
                    clearance = Mathf.Min(clearance, Vector2.Distance(c, o.pos) - n.lakeRadius - 1.5f);
                }
                // Prefer the far (back) side a little so the pond doesn't sit under the label.
                float score = Mathf.Min(clearance, 1.2f) + Mathf.Sin(a) * 0.35f;
                if (score > best)
                {
                    best = score;
                    bestCenter = c;
                }
            }
            n.lake = bestCenter;
        }
    }

    // ───────────────────────────────────────────── fields

    private static float Perlin(float x, float z) => Mathf.PerlinNoise(x + noiseX, z + noiseZ) * 2f - 1f;

    private static float Fbm(float x, float z, int octaves = 3)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Perlin(x * freq, z * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.03f;
        }
        return sum / norm;
    }

    private static float Ridged(float x, float z, int octaves = 4)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float r = 1f - Mathf.Abs(Perlin(x * freq + i * 13.1f, z * freq - i * 7.7f));
            sum += r * r * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.1f;
        }
        return sum / norm;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    private static float RoadDistance(Vector2 p)
    {
        return RoadDistance(p, out _, out _);
    }

    private static float RoadDistance(Vector2 p, out Road nearest, out float along)
    {
        float best = float.MaxValue;
        nearest = null;
        along = 0f;
        foreach (Road road in roads)
        {
            int n = road.points.Count;
            for (int i = 0; i < n - 1; i++)
            {
                float d = SegmentDistance(p, road.points[i], road.points[i + 1], out float t);
                if (d < best)
                {
                    best = d;
                    nearest = road;
                    along = (i + t) / (n - 1);
                }
            }
        }
        return best;
    }

    private static float NodeDistance(Vector2 p, out NodeInfo nearest)
    {
        float best = float.MaxValue;
        nearest = null;
        foreach (NodeInfo n in nodes)
        {
            float d = Vector2.Distance(p, n.pos);
            if (d < best)
            {
                best = d;
                nearest = n;
            }
        }
        return best;
    }

    /// <summary>Normalised biome weights at a point (background weight goes to Grassland).</summary>
    private static Dictionary<CampaignBiome, float> Weights(Vector2 p)
    {
        Dictionary<CampaignBiome, float> w = new Dictionary<CampaignBiome, float>();
        float total = 0f;
        foreach (NodeInfo n in nodes)
        {
            float d = Vector2.Distance(p, n.pos);
            float sigma = n.biome == CampaignBiome.Alpine ? 4.8f : 4.1f;
            float k = Mathf.Exp(-(d * d) / (sigma * sigma));
            if (k < 1e-4f) continue;
            w.TryGetValue(n.biome, out float cur);
            w[n.biome] = cur + k;
            total += k;
        }
        const float background = 0.004f;
        w.TryGetValue(CampaignBiome.Grassland, out float g);
        w[CampaignBiome.Grassland] = g + background;
        total += background;

        List<CampaignBiome> keys = new List<CampaignBiome>(w.Keys);
        foreach (CampaignBiome k in keys) w[k] /= total;
        return w;
    }

    private static CampaignBiome Dominant(Dictionary<CampaignBiome, float> w)
    {
        CampaignBiome best = CampaignBiome.Grassland;
        float bestW = -1f;
        foreach (KeyValuePair<CampaignBiome, float> kv in w)
        {
            if (kv.Value > bestW)
            {
                bestW = kv.Value;
                best = kv.Key;
            }
        }
        return best;
    }

    private static float Relief(CampaignDioramaThemeSO.BiomeLook look, Vector2 p, float dNode, float dRoad)
    {
        float x = p.x, z = p.y;
        float flatNear = Mathf.Max(1f - Smooth(1.25f, 2.3f, dNode), 1f - Smooth(theme.roadWidth * 0.6f, theme.roadWidth * 3.2f, dRoad));
        switch (look.relief)
        {
            case CampaignDioramaThemeSO.Relief.Dunes:
            {
                float warp = Fbm(x * 0.1f, z * 0.1f) * 4f;
                float dune = Mathf.Pow(Mathf.Abs(Mathf.Sin(x * 0.75f + z * 0.4f + warp)), 1.6f);
                return look.reliefHeight * (dune * 0.75f + 0.25f * (Fbm(x * 0.3f, z * 0.3f) * 0.5f + 0.5f)) * (1f - flatNear * 0.85f);
            }
            case CampaignDioramaThemeSO.Relief.Mountains:
            {
                float mask = Smooth(1.7f, 4.6f, dNode) * Smooth(0.35f, 2.4f, dRoad);
                float ridge = Mathf.Pow(Ridged(x * 0.16f, z * 0.16f), 1.5f);
                return look.reliefHeight * ridge * mask + 0.25f * (Fbm(x * 0.25f, z * 0.25f) * 0.5f + 0.5f) * (1f - flatNear);
            }
            case CampaignDioramaThemeSO.Relief.Rocky:
            {
                float mask = Smooth(1.4f, 3.2f, dNode) * Smooth(0.3f, 1.6f, dRoad);
                return look.reliefHeight * Mathf.Pow(Ridged(x * 0.3f, z * 0.3f), 2f) * mask + 0.1f;
            }
            case CampaignDioramaThemeSO.Relief.Lowland:
                return look.reliefHeight * 0.6f * (Fbm(x * 0.22f, z * 0.22f) * 0.5f + 0.5f) * (1f - flatNear * 0.8f);
            default:
                return look.reliefHeight * (Fbm(x * 0.17f, z * 0.17f) * 0.5f + 0.5f) * (1f - flatNear * 0.85f);
        }
    }

    private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    private static float Height(Vector2 p)
    {
        Dictionary<CampaignBiome, float> w = Weights(p);
        float dNode = NodeDistance(p, out NodeInfo near);
        float dRoad = RoadDistance(p, out Road road, out float along);

        float h = 0f;
        foreach (KeyValuePair<CampaignBiome, float> kv in w)
        {
            h += Relief(theme.Get(kv.Key), p, dNode, dRoad) * kv.Value;
        }
        h += Fbm(p.x * 0.6f, p.y * 0.6f, 2) * 0.05f;

        // Mountain backdrop along the back edge (and a little around the ends), well clear of the nodes.
        float back = Smooth(board.yMax - 7.5f, board.yMax - 0.8f, p.y);
        float ends = Mathf.Max(Smooth(board.xMin + 4.5f, board.xMin + 0.5f, p.x), Smooth(board.xMax - 4.5f, board.xMax - 0.5f, p.x)) * 0.55f;
        float backdrop = Mathf.Max(back, ends) * Smooth(1.8f, 4.5f, dNode) * Smooth(0.6f, 2.5f, dRoad);
        float massif = Mathf.Pow(Mathf.Clamp01(Fbm(p.x * 0.07f + 40f, p.y * 0.07f, 2) * 0.5f + 0.6f), 1.6f);
        h += backdrop * (0.9f + 3.4f * (0.35f + 0.65f * massif) * Mathf.Pow(Ridged(p.x * 0.12f, p.y * 0.12f), 1.2f));

        // Roads level out between their two castles' plateaus.
        if (road != null)
        {
            float roadH = Mathf.Lerp(road.from.plateau, road.to.plateau, Mathf.SmoothStep(0f, 1f, along)) - 0.02f;
            float k = 1f - Smooth(theme.roadWidth * 0.5f, theme.roadWidth * 2.4f, dRoad);
            h = Mathf.Lerp(h, roadH, k);
        }

        // Castle plateaus
        if (near != null)
        {
            float k = 1f - Smooth(1.25f, 2.1f, dNode);
            h = Mathf.Lerp(h, near.plateau, k);
        }

        // Ponds
        foreach (NodeInfo n in nodes)
        {
            if (!n.lake.HasValue) continue;
            float d = Vector2.Distance(p, n.lake.Value) / n.lakeRadius;
            if (d > 1.6f) continue;
            float water = n.plateau - 0.1f;
            float bed = water - 0.05f - 0.28f * Mathf.Clamp01(1f - d * d);
            float k = 1f - Smooth(0.85f, 1.6f, d);
            h = Mathf.Lerp(h, Mathf.Min(h, bed), k);
        }
        return h;
    }

    private static Color Blend(Dictionary<CampaignBiome, float> w, System.Func<CampaignDioramaThemeSO.BiomeLook, Color> pick)
    {
        Color c = Color.clear;
        foreach (KeyValuePair<CampaignBiome, float> kv in w) c += pick(theme.Get(kv.Key)) * kv.Value;
        c.a = 1f;
        return c;
    }

    private static Color GroundColor(Vector2 p, float h, Vector3 normal)
    {
        Dictionary<CampaignBiome, float> w = Weights(p);
        float n = Fbm(p.x * 0.45f, p.y * 0.45f, 2) * 0.5f + 0.5f;
        Color ground = Color.Lerp(Blend(w, b => b.ground), Blend(w, b => b.groundAlt), Smooth(0.35f, 0.65f, n));

        float slope = 1f - normal.y;
        Color col = Color.Lerp(ground, Blend(w, b => b.slope), Smooth(0.2f, 0.42f, slope));

        // Snow caps, from each biome's snow line (0 = none)
        float snowLine = 0f, snowWeight = 0f;
        foreach (KeyValuePair<CampaignBiome, float> kv in w)
        {
            CampaignDioramaThemeSO.BiomeLook look = theme.Get(kv.Key);
            if (look.snowLine <= 0f) continue;
            snowLine += look.snowLine * kv.Value;
            snowWeight += kv.Value;
        }
        if (snowWeight > 0.15f)
        {
            snowLine /= snowWeight;
            float k = Smooth(snowLine, snowLine + 0.35f, h + Fbm(p.x * 0.9f, p.y * 0.9f) * 0.25f) * (1f - Smooth(0.55f, 0.8f, slope));
            col = Color.Lerp(col, theme.snow, k * Mathf.Clamp01(snowWeight * 1.5f));
        }
        else if (h > 2.2f)
        {
            col = Color.Lerp(col, theme.snow, Smooth(2.4f, 3.2f, h) * (1f - Smooth(0.5f, 0.75f, slope)));
        }

        float dRoad = RoadDistance(p);
        float dNode = NodeDistance(p, out _);
        Color road = Blend(w, b => b.road);
        float edge = Fbm(p.x * 3f, p.y * 3f, 1) * 0.04f;
        if (dRoad < theme.roadWidth * 0.5f + edge) col = road;
        else if (dRoad < theme.roadWidth * 0.85f + edge) col = Color.Lerp(col, road, 0.45f);
        if (dNode < 1.3f) col = Color.Lerp(col, road, 0.3f * (1f - Smooth(1.0f, 1.3f, dNode)));

        foreach (NodeInfo node in nodes)
        {
            if (!node.lake.HasValue) continue;
            float d = Vector2.Distance(p, node.lake.Value) / node.lakeRadius;
            if (d < 1.25f) col = Color.Lerp(col, new Color(0.74f, 0.66f, 0.5f), (1f - Smooth(0.95f, 1.25f, d)) * 0.85f);
        }
        return col;
    }

    // ───────────────────────────────────────────── terrain & base

    private static Vector3 V(float x, float z, float[,] heights, int i, int j) => new Vector3(x, heights[i, j], z);

    private static void BuildTerrain(Transform root, List<Mesh> meshes)
    {
        float step = theme.gridStep;
        int nx = Mathf.CeilToInt(board.width / step);
        int nz = Mathf.CeilToInt(board.height / step);
        float sx = board.width / nx;
        float sz = board.height / nz;

        float[,] h = new float[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++)
        {
            for (int j = 0; j <= nz; j++)
            {
                h[i, j] = Height(new Vector2(board.xMin + i * sx, board.yMin + j * sz));
            }
        }

        DioramaMesh terrain = new DioramaMesh();
        System.Random jitter = new System.Random(theme.seed + 1);
        for (int i = 0; i < nx; i++)
        {
            for (int j = 0; j < nz; j++)
            {
                float x0 = board.xMin + i * sx, x1 = x0 + sx, z0 = board.yMin + j * sz, z1 = z0 + sz;
                Vector3 a = new Vector3(x0, h[i, j], z0);
                Vector3 b = new Vector3(x0, h[i, j + 1], z1);
                Vector3 c = new Vector3(x1, h[i + 1, j + 1], z1);
                Vector3 d = new Vector3(x1, h[i + 1, j], z0);
                bool flip = ((i + j) & 1) == 0;
                if (flip)
                {
                    TerrainTri(terrain, a, b, c, jitter);
                    TerrainTri(terrain, a, c, d, jitter);
                }
                else
                {
                    TerrainTri(terrain, a, b, d, jitter);
                    TerrainTri(terrain, b, c, d, jitter);
                }
            }
        }
        AddRenderer(root, "Terrain", terrain.ToMesh("DioramaTerrain"), theme.vertexLitMaterial, meshes, true);

        // Cut sides showing soil and rock strata, a wooden plinth with a gilt trim, and the table under it.
        DioramaMesh baseMesh = new DioramaMesh();
        const float bottom = -1.5f;
        for (int i = 0; i < nx; i++)
        {
            float x0 = board.xMin + i * sx, x1 = x0 + sx;
            Side(baseMesh, new Vector3(x0, h[i, 0], board.yMin), new Vector3(x1, h[i + 1, 0], board.yMin), bottom, true);
            Side(baseMesh, new Vector3(x1, h[i + 1, nz], board.yMax), new Vector3(x0, h[i, nz], board.yMax), bottom, true);
        }
        for (int j = 0; j < nz; j++)
        {
            float z0 = board.yMin + j * sz, z1 = z0 + sz;
            Side(baseMesh, new Vector3(board.xMin, h[0, j + 1], z1), new Vector3(board.xMin, h[0, j], z0), bottom, true);
            Side(baseMesh, new Vector3(board.xMax, h[nx, j], z0), new Vector3(board.xMax, h[nx, j + 1], z1), bottom, true);
        }
        Vector3 center = new Vector3(board.center.x, 0f, board.center.y);
        baseMesh.Box(center + Vector3.up * (bottom - 0.3f), new Vector3(board.width + 0.7f, 0.6f, board.height + 0.7f), theme.plinthWood, true);
        baseMesh.Box(center + Vector3.up * (bottom + 0.02f), new Vector3(board.width + 0.78f, 0.06f, board.height + 0.78f), theme.plinthTrim);
        baseMesh.Box(center + Vector3.up * (bottom - 0.62f), new Vector3(board.width + 1.1f, 0.08f, board.height + 1.1f), Darken(theme.plinthWood, 0.8f));
        AddRenderer(root, "Base", baseMesh.ToMesh("DioramaBase"), theme.vertexLitMaterial, meshes, true);

        DioramaMesh table = new DioramaMesh();
        float y = bottom - 0.66f;
        table.Quad(new Vector3(center.x - 140f, y, center.z - 90f), new Vector3(center.x - 140f, y, center.z + 90f),
            new Vector3(center.x + 140f, y, center.z + 90f), new Vector3(center.x + 140f, y, center.z - 90f), theme.table);
        AddRenderer(root, "Table", table.ToMesh("DioramaTable"), theme.vertexLitMaterial, meshes, false);
    }

    private static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    private static void TerrainTri(DioramaMesh m, Vector3 a, Vector3 b, Vector3 c, System.Random jitter)
    {
        Vector3 centroid = (a + b + c) / 3f;
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        Color col = GroundColor(new Vector2(centroid.x, centroid.z), centroid.y, normal);
        float k = 1f + ((float)jitter.NextDouble() - 0.5f) * 0.045f;
        m.Tri(a, b, c, new Color(col.r * k, col.g * k, col.b * k, 1f));
    }

    /// <summary>One vertical strip of the cut edge from the terrain surface (a→b, outward on the left of the walk... see winding) down to the base.</summary>
    private static void Side(DioramaMesh m, Vector3 a, Vector3 b, float bottom, bool outwardFacing)
    {
        float[] levels = { -0.18f, -0.55f, -0.9f, -1.2f };
        Color[] bands = { theme.strataA, theme.strataB, theme.strataA, theme.strataB };

        // Topsoil follows the surface
        Vector3 a1 = new Vector3(a.x, a.y - 0.14f, a.z);
        Vector3 b1 = new Vector3(b.x, b.y - 0.14f, b.z);
        m.Quad(a1, a, b, b1, theme.soil);

        float topA = a1.y, topB = b1.y;
        for (int k = 0; k <= levels.Length; k++)
        {
            float lo = k < levels.Length ? levels[k] : bottom;
            float hiA = Mathf.Max(topA, lo), hiB = Mathf.Max(topB, lo);
            if (hiA <= lo + 0.001f && hiB <= lo + 0.001f) continue;
            Color band = k == 0 ? Darken(theme.soil, 1.1f) : bands[(k - 1) % bands.Length];
            m.Quad(new Vector3(a.x, lo, a.z), new Vector3(a.x, hiA, a.z), new Vector3(b.x, hiB, b.z), new Vector3(b.x, lo, b.z), band);
            topA = Mathf.Min(topA, lo);
            topB = Mathf.Min(topB, lo);
        }
    }

    private static void BuildWater(Transform root, List<Mesh> meshes)
    {
        if (theme.waterMaterial != null)
        {
            theme.waterMaterial.SetColor("_BaseColor", theme.water);
            EditorUtility.SetDirty(theme.waterMaterial);
        }
        foreach (NodeInfo n in nodes)
        {
            if (!n.lake.HasValue) continue;
            DioramaMesh water = new DioramaMesh();
            Vector3 c = new Vector3(n.lake.Value.x, n.plateau - 0.1f, n.lake.Value.y);
            int sides = 28;
            float r = n.lakeRadius * 1.08f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                water.Tri(c, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r, c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, theme.water);
            }
            GameObject go = AddRenderer(root, $"Pond_{n.node.nodeId}", water.ToMesh($"DioramaPond_{n.node.nodeId}"), theme.waterMaterial, meshes, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private static GameObject AddRenderer(Transform parent, string name, Mesh mesh, Material material, List<Mesh> meshes, bool castShadows)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        meshes.Add(mesh);
        return go;
    }

    // ───────────────────────────────────────────── castles

    private static List<CampaignDioramaSite> BuildCastles(Transform root, List<Mesh> meshes)
    {
        Transform parent = new GameObject("Castles").transform;
        parent.SetParent(root, false);
        List<CampaignDioramaSite> sites = new List<CampaignDioramaSite>();
        Dictionary<float, Mesh> bannerMeshes = new Dictionary<float, Mesh>();

        foreach (NodeInfo n in nodes)
        {
            System.Random castleRng = new System.Random(theme.seed ^ n.node.nodeId.GetHashCode());
            DioramaMesh body = new DioramaMesh();
            MiniCastleBuilder.Result result = MiniCastleBuilder.Build(body, n.kind, n.look, castleRng);

            GameObject castle = new GameObject($"Castle_{n.node.nodeId}");
            castle.transform.SetParent(parent, false);
            castle.transform.position = new Vector3(n.pos.x, n.plateau, n.pos.y);

            List<Renderer> bodies = new List<Renderer>();
            GameObject bodyGo = AddRenderer(castle.transform, "Body", body.ToMesh($"DioramaCastle_{n.node.nodeId}"), theme.vertexLitMaterial, meshes, true);
            bodies.Add(bodyGo.GetComponent<MeshRenderer>());

            if (n.lake.HasValue)
            {
                DioramaMesh dock = new DioramaMesh();
                Vector2 dir = (n.lake.Value - n.pos).normalized;
                float yaw = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                dock.Matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, -yaw, 0f), Vector3.one);
                MiniCastleBuilder.Dock(dock, (n.lake.Value - n.pos).magnitude - n.lakeRadius * 0.55f);
                GameObject dockGo = AddRenderer(castle.transform, "Dock", dock.ToMesh($"DioramaDock_{n.node.nodeId}"), theme.vertexLitMaterial, meshes, true);
                bodies.Add(dockGo.GetComponent<MeshRenderer>());
            }

            List<Renderer> banners = new List<Renderer>();
            for (int i = 0; i < result.bannerMounts.Count; i++)
            {
                float size = Mathf.Round(result.bannerSizes[i] * 20f) / 20f;
                if (!bannerMeshes.TryGetValue(size, out Mesh bannerMesh))
                {
                    bannerMesh = MiniCastleBuilder.BannerMesh(size);
                    bannerMesh.name = $"DioramaBanner_{size:0.00}";
                    bannerMeshes[size] = bannerMesh;
                    meshes.Add(bannerMesh);
                }
                GameObject flag = new GameObject($"Banner_{i}");
                flag.transform.SetParent(castle.transform, false);
                flag.transform.localPosition = result.bannerMounts[i];
                flag.transform.localRotation = Quaternion.Euler(0f, -25f + (float)castleRng.NextDouble() * 20f, 0f);
                flag.AddComponent<MeshFilter>().sharedMesh = bannerMesh;
                MeshRenderer mr = flag.AddComponent<MeshRenderer>();
                mr.sharedMaterial = theme.vertexLitMaterial;
                banners.Add(mr);
            }

            foreach (MiniCastleBuilder.LightMount l in result.lights)
            {
                GameObject lightGo = new GameObject("Glow");
                lightGo.transform.SetParent(castle.transform, false);
                lightGo.transform.localPosition = l.position;
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = l.color;
                light.intensity = l.intensity;
                light.range = l.range;
                light.shadows = LightShadows.None;
            }

            DioramaMesh ringMesh = new DioramaMesh();
            ringMesh.UvDisc(Vector3.zero, result.footprintRadius * 1.25f);
            GameObject ring = AddRenderer(castle.transform, "OpenRing", ringMesh.ToMesh($"DioramaRing_{n.node.nodeId}"), theme.ringGlowMaterial, meshes, false);
            ring.transform.localPosition = Vector3.up * 0.09f;
            ring.SetActive(false);

            Transform label = new GameObject("LabelAnchor").transform;
            label.SetParent(castle.transform, false);
            label.localPosition = new Vector3(0f, 0.05f, -result.frontExtent);
            Transform marker = new GameObject("MarkerAnchor").transform;
            marker.SetParent(castle.transform, false);
            marker.localPosition = new Vector3(0f, result.topHeight + 0.12f, 0f);

            CampaignDioramaSite site = castle.AddComponent<CampaignDioramaSite>();
            site.EditorSetup(n.node.nodeId, label, marker, bodies.ToArray(), banners.ToArray(), ring.GetComponent<MeshRenderer>());
            sites.Add(site);
        }
        return sites;
    }

    // ───────────────────────────────────────────── routes

    private static List<CampaignDioramaRoad> BuildRoadOverlays(Transform root, List<Mesh> meshes)
    {
        Transform parent = new GameObject("Routes").transform;
        parent.SetParent(root, false);
        List<CampaignDioramaRoad> list = new List<CampaignDioramaRoad>();
        foreach (Road road in roads)
        {
            // Start and end at the castle pads' edges.
            List<Vector3> left = new List<Vector3>();
            List<Vector3> right = new List<Vector3>();
            List<float> along = new List<float>();
            float dist = 0f;
            float halfWidth = theme.roadWidth * 0.32f;
            Vector2 prev = road.points[0];
            for (int i = 0; i < road.points.Count; i++)
            {
                Vector2 p = road.points[i];
                if (Vector2.Distance(p, road.from.pos) < 1.15f || Vector2.Distance(p, road.to.pos) < 1.15f) continue;
                Vector2 tangent = (road.points[Mathf.Min(i + 1, road.points.Count - 1)] - road.points[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                if (left.Count > 0) dist += Vector2.Distance(prev, p);
                prev = p;
                float y = Height(p) + 0.035f;
                left.Add(new Vector3(p.x + normal.x * halfWidth, y, p.y + normal.y * halfWidth));
                right.Add(new Vector3(p.x - normal.x * halfWidth, y, p.y - normal.y * halfWidth));
                along.Add(dist);
            }
            if (left.Count < 2) continue;

            DioramaMesh ribbon = new DioramaMesh();
            ribbon.Ribbon(left, right, along);
            string name = $"Route_{road.from.node.nodeId}_to_{road.to.node.nodeId}";
            GameObject go = AddRenderer(parent, name, ribbon.ToMesh(name), theme.roadGlowMaterial, meshes, false);
            CampaignDioramaRoad component = go.AddComponent<CampaignDioramaRoad>();
            component.EditorSetup(road.from.node.nodeId, road.to.node.nodeId, go.GetComponent<MeshRenderer>());
            list.Add(component);
        }
        return list;
    }

    // ───────────────────────────────────────────── scatter

    private static void Scatter(Transform root, List<Mesh> meshes)
    {
        Transform parent = new GameObject("Scatter").transform;
        parent.SetParent(root, false);
        System.Random r = new System.Random(theme.seed + 7);
        int placed = 0;
        int trees = 0;
        DioramaMesh flora = new DioramaMesh();

        const float cell = 0.55f;
        for (float x = board.xMin + 0.4f; x < board.xMax - 0.4f; x += cell)
        {
            for (float z = board.yMin + 0.4f; z < board.yMax - 0.4f; z += cell)
            {
                Vector2 p = new Vector2(x + (float)r.NextDouble() * cell, z + (float)r.NextDouble() * cell);
                if (p.x > board.xMax - 0.3f || p.y > board.yMax - 0.3f) continue;
                float dNode = NodeDistance(p, out _);
                float dRoad = RoadDistance(p);
                if (dNode < 1.55f || dRoad < theme.roadWidth * 1.1f || InLake(p, 1.05f)) continue;

                Dictionary<CampaignBiome, float> w = Weights(p);
                CampaignDioramaThemeSO.BiomeLook look = theme.Get(Dominant(w));
                float h = Height(p);
                float slope = SlopeAt(p);
                float area = cell * cell;
                double roll = r.NextDouble();
                float grove = Smooth(-0.15f, 0.35f, Fbm(p.x * 0.22f + 17f, p.y * 0.22f - 9f, 2));
                float treeP = look.treeDensity * area * (slope > 0.55f ? 0.15f : 1f) * (0.2f + 2.6f * grove);
                float rockP = look.rockDensity * area * (slope > 0.35f ? 2.5f : 1f);
                float coverP = look.groundCoverDensity * area * (slope > 0.45f ? 0f : 1f);
                if (h > 2.6f) treeP *= 0.2f;

                if (roll < treeP)
                {
                    if (look.flora == null || look.flora.Count == 0) continue;
                    DioramaFlora.Kind kind = look.flora[r.Next(look.flora.Count)];
                    float height = Mathf.Lerp(look.treeHeight.x, look.treeHeight.y, (float)r.NextDouble());
                    DioramaFlora.Tree(flora, kind, new Vector3(p.x, h, p.y), height, look.leaf, look.leafAlt, look.trunk, r);
                    trees++;
                }
                else if (roll < treeP + rockP) placed += Place(parent, look.rocks, look.rockHeight, p, h, r) ? 1 : 0;
                else if (roll < treeP + rockP + coverP) placed += Place(parent, look.groundCover, look.groundCoverHeight, p, h, r) ? 1 : 0;
            }
        }

        // Landmarks clustered around each node (gravestones round the graveyard, crystals round the crypt...)
        foreach (NodeInfo n in nodes)
        {
            CampaignDioramaThemeSO.BiomeLook look = n.look;
            int count = look.landmarksPerNode;
            int tries = 0;
            while (count > 0 && tries++ < 80)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f;
                float d = 1.35f + (float)r.NextDouble() * 1.0f;
                Vector2 p = n.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (Mathf.Sin(a) < -0.5f) continue; // keep the label area in front clear
                // Bigger landmarks (houses) keep proportionally further off the roads.
                if (RoadDistance(p) < theme.roadWidth * 1.1f + look.landmarkHeight.y * 0.6f || InLake(p, 0.95f)) continue;
                if (n.biome == CampaignBiome.Lake && !InLake(p, 1.35f)) continue; // reeds hug the shore
                if (Place(parent, look.landmarks, look.landmarkHeight, p, Height(p), r)) count--;
            }
        }
        GameObject floraGo = AddRenderer(parent, "Trees", flora.ToMesh("DioramaTrees"), theme.vertexLitMaterial, meshes, true);
        floraGo.isStatic = true;
        Debug.Log($"[CampaignDiorama] Scattered {trees} trees and {placed} props.");
    }

    private static bool InLake(Vector2 p, float radiusScale)
    {
        foreach (NodeInfo n in nodes)
        {
            if (n.lake.HasValue && Vector2.Distance(p, n.lake.Value) < n.lakeRadius * radiusScale) return true;
        }
        return false;
    }

    private static float SlopeAt(Vector2 p)
    {
        const float e = 0.2f;
        float hx = Height(p + new Vector2(e, 0f)) - Height(p - new Vector2(e, 0f));
        float hz = Height(p + new Vector2(0f, e)) - Height(p - new Vector2(0f, e));
        Vector3 n = new Vector3(-hx, 2f * e, -hz).normalized;
        return 1f - n.y;
    }

    private static bool Place(Transform parent, List<GameObject> prefabs, Vector2 heightRange, Vector2 p, float groundH, System.Random r)
    {
        if (prefabs == null || prefabs.Count == 0) return false;
        GameObject prefab = prefabs[r.Next(prefabs.Count)];
        if (prefab == null) return false;
        Bounds b = PrefabMeasure.LocalBounds(prefab);
        if (b.size.y < 0.001f) return false;

        float target = Mathf.Lerp(heightRange.x, heightRange.y, (float)r.NextDouble());
        float scale = target / b.size.y;
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f);
        go.transform.position = new Vector3(p.x, groundH - b.min.y * scale - 0.015f, p.y);
        go.isStatic = true;
        return true;
    }

    // ───────────────────────────────────────────── camera, light, post

    private static CampaignDioramaCamera SetupCameraAndLighting(Transform root)
    {
        // Sun
        Light sun = null;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) sun = l;
        }
        if (sun == null)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.SetParent(root, false);
        sun.transform.rotation = Quaternion.Euler(theme.sunEuler);
        sun.color = theme.sunColor;
        sun.intensity = theme.sunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        sun.shadowBias = 0.02f;
        sun.shadowNormalBias = 0.2f;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = theme.ambientSky;
        RenderSettings.ambientEquatorColor = theme.ambientEquator;
        RenderSettings.ambientGroundColor = theme.ambientGround;
        RenderSettings.skybox = null;
        RenderSettings.reflectionIntensity = 0.25f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = theme.fogColor;
        RenderSettings.fogStartDistance = theme.fogRange.x;
        RenderSettings.fogEndDistance = theme.fogRange.y;
        RenderSettings.sun = sun;

        // Post
        GameObject volumeGo = GameObject.Find("CampaignDioramaVolume");
        if (volumeGo == null) volumeGo = new GameObject("CampaignDioramaVolume");
        volumeGo.transform.SetParent(root, false);
        Volume volume = volumeGo.GetComponent<Volume>();
        if (volume == null) volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = theme.volumeProfile;
        if (theme.volumeProfile != null && theme.volumeProfile.TryGet(out DepthOfField dof))
        {
            dof.focusDistance.Override(theme.cameraDistance);
        }

        // Camera: pitched down over the board, panning along X.
        // One camera only: keep the scene's (the one with the AudioListener if there are several), drop the rest.
        Camera cam = null;
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera c in cameras)
        {
            if (cam == null || (c.GetComponent<AudioListener>() != null && cam.GetComponent<AudioListener>() == null)) cam = c;
        }
        foreach (Camera c in cameras)
        {
            if (c != cam) Object.DestroyImmediate(c.gameObject);
        }
        if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.name = "Main Camera";
        cam.tag = "MainCamera";
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        float zCenter = 0f;
        foreach (NodeInfo n in nodes) zCenter += n.pos.y;
        zCenter /= Mathf.Max(1, nodes.Count);
        float pitch = theme.cameraPitch * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * theme.cameraDistance;
        float focusY = 0.3f;
        float startX = nodes.Count > 0 ? nodes[0].pos.x + 7f : 0f;
        cam.transform.position = new Vector3(startX, focusY, zCenter - 0.8f) + offset;
        cam.transform.rotation = Quaternion.Euler(theme.cameraPitch, 0f, 0f);
        cam.orthographic = false;
        cam.fieldOfView = theme.cameraFov;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 140f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.backgroundColor;
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;

        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (NodeInfo n in nodes)
        {
            minX = Mathf.Min(minX, n.pos.x);
            maxX = Mathf.Max(maxX, n.pos.x);
        }
        CampaignDioramaCamera rig = cam.GetComponent<CampaignDioramaCamera>();
        if (rig == null) rig = cam.gameObject.AddComponent<CampaignDioramaCamera>();
        rig.EditorSetup(theme.look, minX + 7f, maxX - 7f, -offset);
        EditorUtility.SetDirty(rig);
        EditorUtility.SetDirty(cam);
        return rig;
    }

    private static void SaveMeshes(List<Mesh> meshes)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(MeshAssetPath) != null) AssetDatabase.DeleteAsset(MeshAssetPath);
        Mesh first = meshes[0];
        AssetDatabase.CreateAsset(first, MeshAssetPath);
        for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], first);
        AssetDatabase.SaveAssets();
    }
}
