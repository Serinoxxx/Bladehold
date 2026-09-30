using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Turns a <see cref="DefenseHeightfield" /> into a Unity Terrain: heights, a rule-based splat map
///     (slope, ravine floor, roads, courtyard, noise variation) and terrain-tree forests on the lower
///     mountain slopes. Trees are terrain instances, not GameObjects, so a big map stays cheap.
/// </summary>
public static class DefenseTerrainBuilder
{
    private const int Ground = 0, Variant = 1, Slope = 2, Cliff = 3, Road = 4, Floor = 5, Court = 6, Wall = 7;

    public static Terrain Build(DefenseSceneSpecSO spec, DefenseHeightfield hf, Transform parent, string dataFolder,
        System.Random rng)
    {
        DefenseBiomePaletteSO p = spec.palette;
        int res = spec.heightmapResolution;
        float size = spec.terrainSize;

        var td = new TerrainData
        {
            heightmapResolution = res,
            size = new Vector3(size, spec.terrainHeight, size),
            alphamapResolution = spec.alphamapResolution,
            baseMapResolution = 1024
        };
        td.SetDetailResolution(512, 16);

        // Save the asset before painting: CreateAsset re-initialises the splat textures, so anything
        // painted into an unsaved TerrainData comes back as 100% layer 0.
        string path = $"{dataFolder}/Terrain.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(td, path);

        var heights = new float[res, res];
        for (int iz = 0; iz < res; iz++)
        {
            float wz = spec.terrainMin.y + iz / (float)(res - 1) * size;
            for (int ix = 0; ix < res; ix++)
            {
                float wx = spec.terrainMin.x + ix / (float)(res - 1) * size;
                heights[iz, ix] = Mathf.Clamp01((hf.Height(wx, wz) + spec.playfieldElevation) / spec.terrainHeight);
            }
        }
        td.SetHeights(0, 0, heights);

        // The ravine-wall layer is optional; palettes without one keep the original seven layers.
        td.terrainLayers = p.ravineWall != null
            ? new[] { p.ground, p.groundVariant, p.slope, p.cliff, p.road, p.ravineFloor, p.courtyard, p.ravineWall }
            : new[] { p.ground, p.groundVariant, p.slope, p.cliff, p.road, p.ravineFloor, p.courtyard };
        PaintSplat(spec, hf, td);
        EditorUtility.SetDirty(td);

        GameObject go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(spec.terrainMin.x, -spec.playfieldElevation, spec.terrainMin.y);
        var terrain = go.GetComponent<Terrain>();
        if (p.terrainMaterial != null) terrain.materialTemplate = p.terrainMaterial;
        terrain.heightmapPixelError = 4f;
        terrain.basemapDistance = 250f;
        terrain.treeDistance = 900f;
        terrain.treeBillboardDistance = 180f;
        terrain.drawInstanced = true;
        go.isStatic = true;
        Physics.SyncTransforms();

        PlantTrees(spec, hf, terrain, rng);
        return terrain;
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static void PaintSplat(DefenseSceneSpecSO spec, DefenseHeightfield hf, TerrainData td)
    {
        int ares = td.alphamapResolution;
        int layerCount = td.terrainLayers.Length;
        bool walls = layerCount > Wall;
        var maps = new float[ares, ares, layerCount];
        List<List<Vector2>> roads = hf.Roads();
        var w = new float[layerCount];

        for (int iz = 0; iz < ares; iz++)
        {
            float v = (iz + 0.5f) / ares;
            float wz = spec.terrainMin.y + v * spec.terrainSize;
            for (int ix = 0; ix < ares; ix++)
            {
                float u = (ix + 0.5f) / ares;
                float wx = spec.terrainMin.x + u * spec.terrainSize;
                float steep = td.GetSteepness(u, v);
                float h = td.GetInterpolatedHeight(u, v) - spec.playfieldElevation;
                float d = hf.ValleyDistance(wx, wz);

                System.Array.Clear(w, 0, layerCount);
                w[Ground] = 1f;

                Over(w, Variant, Smooth(0.45f, 0.75f, hf.Fbm(wx, wz, 0.035f, 3, 5)) * 0.7f);

                if (wz < -3f)
                    Over(w, Court, (1f - Smooth(-2f, 3f, d)) * (0.35f + 0.5f * hf.Fbm(wx, wz, 0.08f, 2, 6)));

                if (d < 4f)
                {
                    float rd = float.MaxValue;
                    var pnt = new Vector2(wx, wz);
                    foreach (List<Vector2> r in roads) rd = Mathf.Min(rd, DefenseHeightfield.DistanceToPolyline(pnt, r));
                    float edge = 1.3f + hf.Fbm(wx, wz, 0.15f, 2, 7) * 1.4f;
                    Over(w, Road, (1f - Smooth(edge - 0.9f, edge + 1.1f, rd)) * 0.72f);
                }

                Over(w, Floor, Smooth(-1.2f, -3f, h));
                // Snow clings up to ~40 degrees; only the steepest faces show bare rock.
                Over(w, Slope, Smooth(24f, 36f, steep));
                Over(w, Cliff, Smooth(40f, 50f, steep) * (0.75f + 0.25f * hf.Fbm(wx, wz, 0.05f, 2, 6)));
                if (walls) PaintRavineWall(spec, hf, w, wx, wz, h);

                for (int l = 0; l < layerCount; l++) maps[iz, ix, l] = w[l];
            }
        }
        td.SetAlphamaps(0, 0, maps);
    }

    /// <summary>
    ///     Rock paint on the ravine walls: from just under the rim down to the floor, which (and the ramp lanes)
    ///     keep the floor layer. Height-based rather than slope-based, so the whole wall changes material at the
    ///     rim instead of blending through the field's slope layer.
    /// </summary>
    private static void PaintRavineWall(DefenseSceneSpecSO spec, DefenseHeightfield hf, float[] w, float x, float z,
        float h)
    {
        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            float extent = hf.RavineExtentMask(i, x);
            if (extent <= 0f) continue;
            // The band covers the ravine and the ramp cuts in its gate-side bank (their side walls too).
            bool band = Mathf.Abs(z - hf.RavineCentreZ(i, x)) < r.topWidth * 0.5f + 0.8f || hf.InRampLane(x, z, 1.5f);
            if (!band) continue;
            Over(w, Wall, Smooth(-0.1f, -0.7f, h) * extent);
            float floor = Smooth(-r.depth + 1.1f, -r.depth + 0.5f, h);
            if (hf.InRampLane(x, z, -0.3f)) floor = Mathf.Max(floor, Smooth(-0.2f, -0.8f, h));
            Over(w, Floor, floor * extent);
            return;
        }
    }

    /// <summary>Composites a layer over the current weights (weights stay normalised).</summary>
    private static void Over(float[] w, int layer, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha <= 0f) return;
        for (int i = 0; i < w.Length; i++) w[i] *= 1f - alpha;
        w[layer] += alpha;
    }

    private static void PlantTrees(DefenseSceneSpecSO spec, DefenseHeightfield hf, Terrain terrain, System.Random rng)
    {
        DefenseBiomePaletteSO p = spec.palette;
        if (p.trees.Count == 0 || spec.scatterDensity <= 0f) return;
        TerrainData td = terrain.terrainData;

        var protos = new List<TreePrototype>();
        foreach (GameObject t in p.trees) protos.Add(new TreePrototype { prefab = t });
        td.treePrototypes = protos.ToArray();

        var instances = new List<TreeInstance>();
        const float cell = 5.5f;
        float treeline = spec.mountainHeight * 0.45f;
        for (float z = spec.terrainMin.y; z < spec.terrainMin.y + spec.terrainSize; z += cell)
        {
            for (float x = spec.terrainMin.x; x < spec.terrainMin.x + spec.terrainSize; x += cell)
            {
                float jx = x + (float)rng.NextDouble() * cell;
                float jz = z + (float)rng.NextDouble() * cell;
                float d = hf.ValleyDistance(jx, jz);
                if (d < 7f) continue;

                float u = (jx - spec.terrainMin.x) / spec.terrainSize;
                float v = (jz - spec.terrainMin.y) / spec.terrainSize;
                if (td.GetSteepness(u, v) > 33f) continue;
                float h = td.GetInterpolatedHeight(u, v) - spec.playfieldElevation;
                float line = treeline + (hf.Fbm(jx, jz, 0.02f, 2, 6) - 0.5f) * 20f;
                if (h > line) continue;

                // Forest patches, denser low down and at the valley edge.
                float density = hf.Fbm(jx, jz, 0.022f, 3, 7) + Mathf.Lerp(0.18f, -0.1f, Mathf.Clamp01(h / line));
                if (density < 0.55f / Mathf.Max(0.1f, spec.scatterDensity)) continue;

                float scale = 0.8f + (float)rng.NextDouble() * 0.55f;
                instances.Add(new TreeInstance
                {
                    prototypeIndex = rng.Next(protos.Count),
                    position = new Vector3(u, td.GetInterpolatedHeight(u, v) / td.size.y, v),
                    widthScale = scale,
                    heightScale = scale * (0.9f + (float)rng.NextDouble() * 0.2f),
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }
        }
        td.SetTreeInstances(instances.ToArray(), true);
    }
}
