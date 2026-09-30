using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Rule-based Synty set dressing. Each rule states where a role may go (distance from the valley
///     edge, slope, what it must keep clear of), and every placement is grounded from measured bounds:
///     the prefab sits on the lowest terrain under its footprint and is sunk by a fraction of its own
///     height, so nothing floats on slopes. Big pieces are placed first and small ones cluster around them.
/// </summary>
public static class DefenseSceneScatter
{
    private struct Placed
    {
        public Vector2 p;
        public float radius;
    }

    public static void Scatter(DefenseBuildContext ctx, Transform env)
    {
        float density = ctx.Spec.scatterDensity;
        if (density <= 0f) return;
        DefenseBiomePaletteSO pal = ctx.Spec.palette;
        var root = new GameObject("Scatter").transform;
        root.SetParent(env);

        var anchors = new List<Placed>();
        PlaceCliffs(ctx, Group(root, "Cliffs"), pal.cliffs, Mathf.RoundToInt(110 * density), anchors);
        PlaceOutcrops(ctx, Group(root, "MountainOutcrops"), pal.cliffs, Mathf.RoundToInt(90 * density));
        PlaceLargeRocks(ctx, Group(root, "LargeRocks"), pal.largeRocks, Mathf.RoundToInt(110 * density), anchors);
        PlaceRimRocks(ctx, Group(root, "RavineRims"), pal.mediumRocks, density, anchors);
        PlaceClusters(ctx, Group(root, "MediumRocks"), pal.mediumRocks, anchors, 1, 3, 0.25f);

        var smallRoot = Group(root, "SmallRocks");
        var snowRoot = Group(root, "SnowMounds");
        PlaceClusters(ctx, smallRoot, pal.smallRocks, anchors, 1, 3, 0.2f);
        PlaceClusters(ctx, snowRoot, pal.snowMounds, anchors, 0, 2, 0.35f);
        PlaceWallAndBuildingSnow(ctx, snowRoot, pal.snowMounds);
        PlaceEdgeLitter(ctx, smallRoot, pal.smallRocks, pal.snowMounds, density);
        PlaceDeadTrees(ctx, Group(root, "DeadTrees"), pal.deadTrees, Mathf.RoundToInt(28 * density));
        PlaceStakeLines(ctx, Group(root, "DefenseStakes"), pal.defenseStakes);
        PlaceBanners(ctx, Group(root, "Banners"), pal.banners);
        PlaceFieldLitter(ctx, Group(root, "FieldLitter"), pal.fieldLitter, density);
        PlaceCourtyardProps(ctx, Group(root, "CourtyardProps"), pal.courtyardProps, Mathf.RoundToInt(24 * density));
    }

    private static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent);
        return t;
    }

    private static float Rand(DefenseBuildContext ctx, float a, float b) => a + (float)ctx.Rng.NextDouble() * (b - a);

    private static GameObject Pick(DefenseBuildContext ctx, List<GameObject> list) =>
        list.Count == 0 ? null : list[ctx.Rng.Next(list.Count)];

    /// <summary>Instantiates grounded: lowest terrain under the footprint, sunk by a fraction of height, tilted towards the normal.</summary>
    private static GameObject Place(DefenseBuildContext ctx, Transform parent, GameObject prefab, Vector2 p, float yaw,
        float scale, float sink, float align)
    {
        var rootPos = new Vector3(p.x, 0f, p.y);
        float ground = ctx.MinGroundUnder(prefab, rootPos, yaw, scale, 0.7f);
        rootPos.y = ground - PrefabMeasure.BaseOffset(prefab, scale) - sink * PrefabMeasure.Height(prefab, scale);
        Vector3 n = ctx.Normal(p.x, p.y);
        Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, align));
        GameObject go = PrefabMeasure.Instantiate(prefab, parent, rootPos, tilt * Quaternion.Euler(0f, yaw, 0f), scale);
        go.isStatic = true;
        return go;
    }

    private static bool FootprintIntrudes(DefenseBuildContext ctx, GameObject prefab, Vector2 p, float yaw, float scale,
        float maxIntrusion)
    {
        foreach (Vector2 c in PrefabMeasure.FootprintCorners(prefab, new Vector3(p.x, 0f, p.y), yaw, scale, 0.7f))
            if (ctx.Hf.ValleyDistance(c.x, c.y) < -maxIntrusion) return true;
        return false;
    }

    private static List<Vector2> Grid(DefenseBuildContext ctx, float cell, System.Func<Vector2, bool> accept)
    {
        DefenseSceneSpecSO s = ctx.Spec;
        var pts = new List<Vector2>();
        for (float z = s.terrainMin.y + cell; z < s.terrainMin.y + s.terrainSize - cell; z += cell)
        for (float x = s.terrainMin.x + cell; x < s.terrainMin.x + s.terrainSize - cell; x += cell)
        {
            var p = new Vector2(x + Rand(ctx, -0.45f, 0.45f) * cell, z + Rand(ctx, -0.45f, 0.45f) * cell);
            if (accept(p)) pts.Add(p);
        }
        return pts;
    }

    private static void Shuffle<T>(DefenseBuildContext ctx, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = ctx.Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---------------------------------------------------------------- rules

    /// <summary>Cliff faces on the steep ground just outside the valley, facing in; densest around the gate notch.</summary>
    private static void PlaceCliffs(DefenseBuildContext ctx, Transform parent, List<GameObject> cliffs, int max,
        List<Placed> anchors)
    {
        if (cliffs.Count == 0) return;
        List<Vector2> cands = Grid(ctx, 3f, p =>
        {
            float d = ctx.Hf.ValleyDistance(p.x, p.y);
            return d > 0.5f && d < 26f && ctx.Steepness(p.x, p.y) > 24f;
        });
        // Mostly random so the edge doesn't read as a regular ring; the notch beside the gate goes first.
        var keys = new Dictionary<Vector2, float>();
        foreach (Vector2 c in cands)
            keys[c] = ctx.Hf.ValleyDistance(c.x, c.y) * 0.25f + Rand(ctx, 0f, 10f)
                      - (Mathf.Abs(c.y) < 25f ? 12f : 0f) + (c.y < -20f ? 8f : 0f);
        cands.Sort((a, b) => keys[a].CompareTo(keys[b]));

        int placed = 0;
        foreach (Vector2 p in cands)
        {
            if (placed >= max) break;
            float d = ctx.Hf.ValleyDistance(p.x, p.y);
            GameObject prefab = Pick(ctx, cliffs);
            float scale = Mathf.Lerp(0.9f, 2f, Mathf.Clamp01(d / 26f)) * Rand(ctx, 0.8f, 1.25f);
            float r = PrefabMeasure.FootprintRadius(prefab, scale);
            if (ctx.Overlaps(p, r * 0.55f, 0.8f)) continue;
            Vector2 outward = ctx.Hf.ValleyOutward(p.x, p.y);
            float yaw = Mathf.Atan2(-outward.x, -outward.y) * Mathf.Rad2Deg + Rand(ctx, -35f, 35f);
            if (FootprintIntrudes(ctx, prefab, p, yaw, scale, 0.8f)) continue;
            ApplyCliffMaterial(ctx, Place(ctx, parent, prefab, p, yaw, scale, 0.18f, 0.15f));
            ctx.Occupy(p, r * 0.7f);
            anchors.Add(new Placed { p = p, radius = r });
            placed++;
        }
    }

    private static void ApplyCliffMaterial(DefenseBuildContext ctx, GameObject go)
    {
        Material m = ctx.Spec.palette.cliffMaterial;
        if (m == null) return;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    /// <summary>Big rock outcrops breaking up the mountain slopes: steep ground, well back from the valley, widely spaced.</summary>
    private static void PlaceOutcrops(DefenseBuildContext ctx, Transform parent, List<GameObject> cliffs, int max)
    {
        if (cliffs.Count == 0) return;
        Vector2 range = ctx.Spec.palette.outcropScale;
        List<Vector2> cands = Grid(ctx, 7f, p =>
        {
            float d = ctx.Hf.ValleyDistance(p.x, p.y);
            return d > 22f && d < 130f && ctx.Steepness(p.x, p.y) > 28f;
        });
        Shuffle(ctx, cands);
        int placed = 0;
        foreach (Vector2 p in cands)
        {
            if (placed >= max) break;
            GameObject prefab = Pick(ctx, cliffs);
            float scale = Rand(ctx, range.x, range.y);
            float r = PrefabMeasure.FootprintRadius(prefab, scale);
            if (ctx.Overlaps(p, r, 1.6f)) continue;
            Vector2 outward = ctx.Hf.ValleyOutward(p.x, p.y);
            float yaw = Mathf.Atan2(-outward.x, -outward.y) * Mathf.Rad2Deg + Rand(ctx, -60f, 60f);
            ApplyCliffMaterial(ctx, Place(ctx, parent, prefab, p, yaw, scale, 0.42f, 0.5f));
            ctx.Occupy(p, r);
            placed++;
        }
    }

    /// <summary>Boulders on the foothills and the very edge of the field, never in a gameplay lane.</summary>
    private static void PlaceLargeRocks(DefenseBuildContext ctx, Transform parent, List<GameObject> rocks, int max,
        List<Placed> anchors)
    {
        if (rocks.Count == 0) return;
        List<Vector2> cands = Grid(ctx, 4f, p =>
        {
            float d = ctx.Hf.ValleyDistance(p.x, p.y);
            return d > -3f && d < 18f && ctx.Steepness(p.x, p.y) < 38f && p.y > -ctx.Spec.courtyardDepth;
        });
        Shuffle(ctx, cands);
        int placed = 0;
        foreach (Vector2 p in cands)
        {
            if (placed >= max) break;
            GameObject prefab = Pick(ctx, rocks);
            float scale = Rand(ctx, 0.8f, 1.4f);
            float r = PrefabMeasure.FootprintRadius(prefab, scale);
            if (ctx.Overlaps(p, r, 1.1f)) continue;
            if (ctx.Hf.ValleyDistance(p.x, p.y) < 3f && ctx.KeepClear(p, r)) continue;
            Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), scale, 0.25f, 0.5f);
            ctx.Occupy(p, r);
            anchors.Add(new Placed { p = p, radius = r });
            placed++;
        }
    }

    /// <summary>Rocks lining both ravine rims so the drop reads from a distance; clear of bridges and ramps.</summary>
    private static void PlaceRimRocks(DefenseBuildContext ctx, Transform parent, List<GameObject> rocks, float density,
        List<Placed> anchors)
    {
        if (rocks.Count == 0) return;
        for (int i = 0; i < ctx.Spec.ravines.Count; i++)
        {
            RavineSpec r = ctx.Spec.ravines[i];
            Vector2 span = ctx.Hf.RavineXSpan(i);
            for (float x = span.x; x < span.y; x += 4.5f / Mathf.Max(0.2f, density))
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (ctx.Rng.NextDouble() > 0.55) continue;
                    GameObject prefab = Pick(ctx, rocks);
                    float scale = Rand(ctx, 0.7f, 1.2f);
                    float rad = PrefabMeasure.FootprintRadius(prefab, scale);
                    float jx = x + Rand(ctx, -1.5f, 1.5f);
                    float z = ctx.Hf.RavineCentreZ(i, jx) + side * (r.topWidth * 0.5f + rad + Rand(ctx, 0.3f, 1.8f));
                    var p = new Vector2(jx, z);
                    bool blocked = false;
                    foreach (BridgeFootprint b in ctx.Bridges) blocked |= b.Contains(p, 5f + rad);
                    if (blocked || ctx.Hf.InRampLane(p.x, p.y, 2.5f + rad) || ctx.DistanceToRoad(p) < 3f + rad) continue;
                    if (ctx.Overlaps(p, rad, 1f)) continue;
                    Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), scale, 0.2f, 0.4f);
                    ctx.Occupy(p, rad);
                    anchors.Add(new Placed { p = p, radius = rad });
                }
            }
        }
    }

    /// <summary>Smaller pieces huddled around the bases of already-placed anchors.</summary>
    private static void PlaceClusters(DefenseBuildContext ctx, Transform parent, List<GameObject> pieces,
        List<Placed> anchors, int minEach, int maxEach, float sink)
    {
        if (pieces.Count == 0) return;
        int anchorCount = anchors.Count;
        for (int a = 0; a < anchorCount; a++)
        {
            Placed anchor = anchors[a];
            int n = ctx.Rng.Next(minEach, maxEach + 1);
            for (int k = 0; k < n; k++)
            {
                GameObject prefab = Pick(ctx, pieces);
                float scale = Rand(ctx, 0.8f, 1.3f);
                float r = PrefabMeasure.FootprintRadius(prefab, scale);
                float ang = Rand(ctx, 0f, Mathf.PI * 2f);
                float dist = anchor.radius * 0.8f + r + Rand(ctx, 0f, 2.5f);
                Vector2 p = anchor.p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                if (ctx.Hf.ValleyDistance(p.x, p.y) < 2f && ctx.KeepClear(p, r)) continue;
                if (ctx.Hf.InRavine(p.x, p.y, out _)) continue;
                if (ctx.Overlaps(p, r, 0.9f)) continue;
                Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), scale, sink, 0.6f);
                ctx.Occupy(p, r);
            }
        }
    }

    /// <summary>Snow drifts against both faces of the castle wall and around building bases, to seat them in the snow.</summary>
    private static void PlaceWallAndBuildingSnow(DefenseBuildContext ctx, Transform parent, List<GameObject> mounds)
    {
        if (mounds.Count == 0) return;
        float half = ctx.Spec.gateNotchHalfWidth;
        for (float x = -half; x <= half; x += 2.6f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (Mathf.Abs(x) < 6f && side > 0) continue; // keep the gate apron clean
                if (ctx.Rng.NextDouble() > 0.7) continue;
                GameObject prefab = Pick(ctx, mounds);
                float scale = Rand(ctx, 0.9f, 1.5f);
                var p = new Vector2(x + Rand(ctx, -0.8f, 0.8f), side * Rand(ctx, 0.9f, 1.6f));
                float yaw = side > 0 ? 180f : 0f;
                Place(ctx, parent, prefab, p, yaw + Rand(ctx, -20f, 20f), scale, 0.3f, 0.3f);
            }
        }

        foreach (GameObject b in ctx.Buildings)
        {
            Bounds lb = PrefabMeasure.LocalBounds(PrefabUtilityBase(b));
            Vector3 c = b.transform.TransformPoint(lb.center);
            float r = (lb.extents.x + lb.extents.z) * 0.5f;
            int n = ctx.Rng.Next(3, 6);
            for (int k = 0; k < n; k++)
            {
                GameObject prefab = Pick(ctx, mounds);
                float ang = Rand(ctx, 0f, Mathf.PI * 2f);
                var p = new Vector2(c.x, c.z) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r + Rand(ctx, 0f, 1.2f));
                Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), Rand(ctx, 1f, 1.6f), 0.35f, 0.3f);
            }
        }
    }

    private static GameObject PrefabUtilityBase(GameObject instance)
    {
        GameObject src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(instance);
        return src != null ? src : instance;
    }

    /// <summary>Loose stones and drifts along the valley edge so the field fades into the foothills.</summary>
    private static void PlaceEdgeLitter(DefenseBuildContext ctx, Transform parent, List<GameObject> stones,
        List<GameObject> mounds, float density)
    {
        List<Vector2> cands = Grid(ctx, 5f, p =>
        {
            float d = ctx.Hf.ValleyDistance(p.x, p.y);
            return d > -7f && d < 3f && p.y > 2f;
        });
        foreach (Vector2 p in cands)
        {
            if (ctx.Rng.NextDouble() > 0.4 * density) continue;
            bool stone = ctx.Rng.NextDouble() < 0.5;
            GameObject prefab = Pick(ctx, stone ? stones : mounds);
            if (prefab == null) continue;
            float scale = Rand(ctx, 0.8f, 1.4f);
            float r = PrefabMeasure.FootprintRadius(prefab, scale);
            if (ctx.KeepClear(p, r) || ctx.Overlaps(p, r, 1f)) continue;
            Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), scale, 0.25f, 0.7f);
            ctx.Occupy(p, r);
        }
    }

    /// <summary>Short staggered lines of stakes on the gate-side flanks, points towards the attackers.</summary>
    private static void PlaceStakeLines(DefenseBuildContext ctx, Transform parent, List<GameObject> stakes)
    {
        if (stakes.Count == 0) return;
        for (float z = 8f; z <= 44f; z += 9f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float edgeX = side * (ctx.Hf.HalfWidth(z) - Rand(ctx, 7f, 12f));
                int n = ctx.Rng.Next(2, 5);
                for (int k = 0; k < n; k++)
                {
                    GameObject prefab = Pick(ctx, stakes);
                    float w = PrefabMeasure.LocalBounds(prefab).size.x;
                    var p = new Vector2(edgeX - side * k * (w + 0.3f), z + Rand(ctx, -0.6f, 0.6f) + k * 0.8f * side);
                    float r = PrefabMeasure.FootprintRadius(prefab, 1f);
                    if (ctx.KeepClear(p, r) || ctx.Overlaps(p, r, 0.8f)) continue;
                    Place(ctx, parent, prefab, p, Rand(ctx, -12f, 12f), 1f, 0.05f, 0.2f);
                    ctx.Occupy(p, r);
                }
            }
        }
    }

    /// <summary>Banners either side of the gate apron and along the wall front.</summary>
    private static void PlaceBanners(DefenseBuildContext ctx, Transform parent, List<GameObject> banners)
    {
        if (banners.Count == 0) return;
        float gateHalf = PrefabMeasure.LocalBounds(ctx.Spec.palette.gate).size.x * 0.5f;
        var spots = new List<Vector2>
        {
            new Vector2(-(gateHalf + 5f), 3.5f), new Vector2(gateHalf + 5f, 3.5f),
            new Vector2(-(gateHalf + 11f), 2f), new Vector2(gateHalf + 11f, 2f)
        };
        foreach (Vector2 p in spots)
        {
            GameObject prefab = Pick(ctx, banners);
            Place(ctx, parent, prefab, p, 180f, 1f, 0.02f, 0f);
        }
    }

    /// <summary>Sparse collider-free litter across the open field so it doesn't read as a blank plane.</summary>
    private static void PlaceFieldLitter(DefenseBuildContext ctx, Transform parent, List<GameObject> litter, float density)
    {
        if (litter.Count == 0) return;
        List<Vector2> cands = Grid(ctx, 10f, p =>
            ctx.Hf.ValleyDistance(p.x, p.y) < -6f && p.y > 6f && p.y < ctx.Spec.fieldEndZ - 10f);
        foreach (Vector2 p in cands)
        {
            if (ctx.Rng.NextDouble() > 0.35 * density) continue;
            GameObject prefab = Pick(ctx, litter);
            float scale = Rand(ctx, 0.8f, 1.2f);
            float r = PrefabMeasure.FootprintRadius(prefab, scale);
            if (ctx.KeepClear(p, r) || ctx.Overlaps(p, r, 1f)) continue;
            GameObject go = Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), scale, 0.2f, 0.8f);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }
    }

    private static void PlaceDeadTrees(DefenseBuildContext ctx, Transform parent, List<GameObject> trees, int max)
    {
        if (trees.Count == 0) return;
        List<Vector2> cands = Grid(ctx, 6f, p =>
        {
            float d = ctx.Hf.ValleyDistance(p.x, p.y);
            return d > -8f && d < 6f && p.y > 10f && ctx.Steepness(p.x, p.y) < 25f;
        });
        Shuffle(ctx, cands);
        int placed = 0;
        foreach (Vector2 p in cands)
        {
            if (placed >= max) break;
            GameObject prefab = Pick(ctx, trees);
            float scale = Rand(ctx, 0.8f, 1.2f);
            if (ctx.KeepClear(p, 1f) || ctx.Overlaps(p, 1.5f, 1f)) continue;
            Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), scale, 0.02f, 0f);
            ctx.Occupy(p, 1.5f);
            placed++;
        }
    }

    private static void PlaceCourtyardProps(DefenseBuildContext ctx, Transform parent, List<GameObject> props, int max)
    {
        if (props.Count == 0) return;
        DefenseSceneSpecSO s = ctx.Spec;
        int placed = 0;
        for (int attempt = 0; attempt < max * 20 && placed < max; attempt++)
        {
            var p = new Vector2(Rand(ctx, -s.courtyardHalfWidth + 6f, s.courtyardHalfWidth - 6f),
                Rand(ctx, -s.courtyardDepth + 8f, -4f));
            if (ctx.Hf.ValleyDistance(p.x, p.y) > -3f) continue;
            if (Mathf.Abs(p.x) < 4f) continue; // keep the lane from the gate to the keep open
            GameObject prefab = Pick(ctx, props);
            float r = PrefabMeasure.FootprintRadius(prefab, 1f);
            if (ctx.Overlaps(p, r + 0.5f, 1f)) continue;
            Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f), 1f, 0.02f, 0f);
            ctx.Occupy(p, r);
            placed++;
        }
    }
}
