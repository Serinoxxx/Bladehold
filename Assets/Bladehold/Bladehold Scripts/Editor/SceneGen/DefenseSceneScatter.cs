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
        PlaceRavineWallRocks(ctx, Group(root, "RavineWalls"), pal.ravineWallRocks, density);
        PlacePropClusters(ctx, Group(root, "PropClusters"), pal.propClusters, density);
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
        // Optional roles, last so palettes without them regenerate exactly as before.
        PlaceLanterns(ctx, Group(root, "Lanterns"), pal.lanterns);
        AddLanternLights(ctx, root, pal.lanterns);
        PlaceGroundFog(ctx, Group(root, "GroundFog"), pal.groundFog);
        PlaceRavineGlow(ctx, Group(root, "RavineGlow"));
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

    /// <summary>
    ///     Rocks set shoulder to shoulder into both ravine walls, so the drop reads as a rocky gorge and not a
    ///     stretched terrain cliff. Each rock's long axis runs along the wall; it is sized to the ravine depth,
    ///     buried below the floor, topped out just under the rim and pushed back into the bank so its face
    ///     barely reaches the floor edge. Visual only: colliders are stripped, so the baked floor, the ramps and
    ///     the SpikePit volumes behave exactly as without them. Ramp lanes and bridge footprints stay open.
    /// </summary>
    private static void PlaceRavineWallRocks(DefenseBuildContext ctx, Transform parent, List<GameObject> rocks,
        float density)
    {
        if (rocks.Count == 0) return;
        for (int i = 0; i < ctx.Spec.ravines.Count; i++)
        {
            RavineSpec r = ctx.Spec.ravines[i];
            Vector2 span = ctx.Hf.RavineXSpan(i);
            float halfTop = r.topWidth * 0.5f, halfFloor = r.floorWidth * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = span.x + Rand(ctx, 0f, 1.5f);
                while (x < span.y)
                {
                    GameObject prefab = Pick(ctx, rocks);
                    Bounds lb = PrefabMeasure.LocalBounds(prefab);
                    float scale = r.depth * Rand(ctx, 0.95f, 1.3f) / Mathf.Max(0.1f, lb.size.y);
                    bool longX = lb.size.x >= lb.size.z;
                    float along = (longX ? lb.size.x : lb.size.z) * scale;
                    float across = (longX ? lb.size.z : lb.size.x) * scale;
                    float step = along * Rand(ctx, 0.55f, 0.85f) / Mathf.Max(0.3f, density);

                    float cz = ctx.Hf.RavineCentreZ(i, x);
                    // Face at most ~0.4 m onto the floor; the back disappears into the bank.
                    float dz = Mathf.Max((halfTop + halfFloor) * 0.5f, halfFloor + across * 0.5f - 0.4f);
                    var p = new Vector2(x, cz + side * dz);
                    bool skip = ctx.Rng.NextDouble() < 0.1 || ctx.Hf.RavineExtentMask(i, x) < 0.85f;
                    if (!skip && side < 0) skip = OverRampCut(ctx, i, x, along * 0.5f);
                    foreach (BridgeFootprint b in ctx.Bridges) skip |= b.Contains(p, along * 0.5f);
                    if (!skip)
                    {
                        Vector2 t = ctx.Hf.RavineTangent(i, x);
                        float yaw = Mathf.Atan2(-t.y, t.x) * Mathf.Rad2Deg + (longX ? 0f : 90f)
                                    + (ctx.Rng.NextDouble() < 0.5 ? 180f : 0f) + Rand(ctx, -12f, 12f);
                        float top = -Rand(ctx, 0.2f, 0.8f);
                        float bottom = top - PrefabMeasure.Height(prefab, scale);
                        var pos = new Vector3(p.x, bottom - PrefabMeasure.BaseOffset(prefab, scale), p.y);
                        // Lean back into the bank a little so the faces don't stand dead vertical.
                        Quaternion lean = Quaternion.AngleAxis(Rand(ctx, 3f, 9f) * side, new Vector3(t.x, 0f, t.y));
                        Vector3 rootPos = PrefabMeasure.RootForCentre(prefab, p, yaw, scale);
                        pos.x = rootPos.x;
                        pos.z = rootPos.z;
                        GameObject go = PrefabMeasure.Instantiate(prefab, parent, pos,
                            lean * Quaternion.Euler(0f, yaw, 0f), scale);
                        go.isStatic = true;
                        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    }
                    x += step;
                }
            }

            foreach (RampSpec ramp in r.exitRamps) LineRampWall(ctx, parent, rocks, i, ramp, density);
        }
    }

    /// <summary>True where a ramp cut replaces the gate-side wall at x (from the ramp foot to its top).</summary>
    private static bool OverRampCut(DefenseBuildContext ctx, int ravine, float x, float halfAlong)
    {
        RavineSpec r = ctx.Spec.ravines[ravine];
        foreach (RampSpec ramp in r.exitRamps)
        {
            ctx.Hf.RampLane(ravine, ramp, x, out float along, out _);
            if (along > -halfAlong * 0.3f && along < ctx.Hf.RampLength(ramp, r)) return true;
        }
        return false;
    }

    /// <summary>
    ///     The bank-side wall of a ramp cut, whose height falls from the full ravine depth to nothing as the
    ///     ramp climbs out. Rocks are sized to the local wall height and kept behind the lane edge, so nothing
    ///     stands in the walkway.
    /// </summary>
    private static void LineRampWall(DefenseBuildContext ctx, Transform parent, List<GameObject> rocks, int ravine,
        RampSpec ramp, float density)
    {
        RavineSpec r = ctx.Spec.ravines[ravine];
        float len = ctx.Hf.RampLength(ramp, r);
        float dir = Mathf.Sign(ramp.direction == 0 ? 1 : ramp.direction);
        float along = Rand(ctx, 0f, 1f);
        while (along < len)
        {
            float x = ramp.x + dir * along;
            float wallHeight = r.depth * (1f - along / len);
            if (wallHeight < 1.2f) break;
            GameObject prefab = Pick(ctx, rocks);
            Bounds lb = PrefabMeasure.LocalBounds(prefab);
            float scale = (wallHeight + Rand(ctx, 0.3f, 0.9f)) / Mathf.Max(0.1f, lb.size.y);
            bool longX = lb.size.x >= lb.size.z;
            float size = (longX ? lb.size.x : lb.size.z) * scale;
            float across = (longX ? lb.size.z : lb.size.x) * scale;

            ctx.Hf.RampLane(ravine, ramp, x, out _, out float laneZ);
            // The cut's face is near vertical, so a rock whose face only meets it disappears into the bank:
            // stand it half a metre proud, into the lane edge (still ~3.5 m of walkway, and no collider).
            var p = new Vector2(x, laneZ - ramp.width * 0.5f - across * 0.5f + 0.6f);
            bool skip = false;
            foreach (BridgeFootprint b in ctx.Bridges) skip |= b.Contains(p, size * 0.5f);
            if (!skip)
            {
                Vector2 t = ctx.Hf.RavineTangent(ravine, x);
                float yaw = Mathf.Atan2(-t.y, t.x) * Mathf.Rad2Deg + (longX ? 0f : 90f)
                            + (ctx.Rng.NextDouble() < 0.5 ? 180f : 0f) + Rand(ctx, -10f, 10f);
                float top = -Rand(ctx, 0.2f, 0.6f);
                float bottom = top - PrefabMeasure.Height(prefab, scale);
                Vector3 rootPos = PrefabMeasure.RootForCentre(prefab, p, yaw, scale);
                rootPos.y = bottom - PrefabMeasure.BaseOffset(prefab, scale);
                Quaternion lean = Quaternion.AngleAxis(-Rand(ctx, 0f, 4f), new Vector3(t.x, 0f, t.y));
                GameObject go = PrefabMeasure.Instantiate(prefab, parent, rootPos,
                    lean * Quaternion.Euler(0f, yaw, 0f), scale);
                go.isStatic = true;
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            along += size * Rand(ctx, 0.55f, 0.8f) / Mathf.Max(0.3f, density);
        }
    }

    /// <summary>
    ///     Hand-composed prop groups (see <see cref="DefenseClusterHarvester" />) in the open field between the
    ///     roads: each is spun to a random yaw, its pieces re-grounded one by one, and any piece that would land
    ///     in a gameplay lane dropped. Small pieces lose their colliders like field litter; big ones (rocks, dead
    ///     trees) keep them and so become NavMesh obstacles the validator re-checks.
    /// </summary>
    private static void PlacePropClusters(DefenseBuildContext ctx, Transform parent, List<PropCluster> clusters,
        float density)
    {
        if (clusters.Count == 0) return;
        DefenseSceneSpecSO s = ctx.Spec;
        List<Vector2> cands = Grid(ctx, 11f, p =>
            ctx.Hf.ValleyDistance(p.x, p.y) < -5f && p.y > 12f && p.y < s.fieldEndZ - 12f);
        Shuffle(ctx, cands);
        foreach (Vector2 c in cands)
        {
            if (ctx.Rng.NextDouble() > 0.85 * density) continue;
            PropCluster cluster = clusters[ctx.Rng.Next(clusters.Count)];
            // Only the core must be clear; outlying pieces that land in a lane are dropped one by one below.
            float reach = cluster.radius * 0.4f + 1f;
            if (ctx.KeepClear(c, reach) || ctx.Overlaps(c, reach, 1.4f)) continue;

            var group = new GameObject(cluster.name).transform;
            group.SetParent(parent);
            group.position = new Vector3(c.x, 0f, c.y);
            Quaternion spin = Quaternion.Euler(0f, Rand(ctx, 0f, 360f), 0f);
            foreach (ClusterPiece piece in cluster.pieces)
            {
                if (piece.prefab == null) continue;
                Vector3 o = spin * new Vector3(piece.offset.x, 0f, piece.offset.z);
                var p = new Vector2(c.x + o.x, c.y + o.z);
                float r = PrefabMeasure.FootprintRadius(piece.prefab, piece.scale.x);
                if (ctx.KeepClear(p, r) || ctx.Hf.ValleyDistance(p.x, p.y) > -2f) continue;
                var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(piece.prefab, group);
                go.transform.SetPositionAndRotation(new Vector3(p.x, ctx.Ground(p.x, p.y) + piece.offset.y, p.y),
                    spin * piece.rotation);
                go.transform.localScale = piece.scale;
                go.isStatic = true;
                if (PrefabMeasure.Height(piece.prefab, piece.scale.y) < 1.5f)
                    foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            }
            if (group.childCount == 0) Object.DestroyImmediate(group.gameObject);
            else ctx.Occupy(c, cluster.radius * 0.8f);
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

    /// <summary>
    ///     Lamps down both edges of every road (every <see cref="DefenseBiomePaletteSO.lanternSpacing" /> m), at
    ///     all four corners of each bridge and either side of the gate apron. Visual only (colliders stripped),
    ///     so they can sit right at the lane edge without touching pathing.
    /// </summary>
    private static void PlaceLanterns(DefenseBuildContext ctx, Transform parent, List<GameObject> lanterns)
    {
        if (lanterns.Count == 0) return;
        DefenseBiomePaletteSO pal = ctx.Spec.palette;
        var spots = new List<Vector2>();

        foreach (List<Vector2> road in ctx.Hf.Roads())
        {
            float carry = 0f;
            for (int i = 0; i < road.Count - 1; i++)
            {
                Vector2 a = road[i], b = road[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                Vector2 dir = (b - a) / len;
                var side = new Vector2(-dir.y, dir.x);
                float t = carry;
                for (; t < len; t += pal.lanternSpacing)
                {
                    Vector2 c = a + dir * t;
                    spots.Add(c + side * 4.2f);
                    spots.Add(c - side * 4.2f);
                }
                carry = t - len;
            }
        }
        foreach (BridgeFootprint b in ctx.Bridges)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                spots.Add(b.centre + new Vector2(sx * (b.halfWidth + 0.9f), sz * (b.halfLength + 0.6f)));
        }
        float gateHalf = PrefabMeasure.LocalBounds(pal.gate).size.x * 0.5f;
        spots.Add(new Vector2(-(gateHalf + 2.5f), 6f));
        spots.Add(new Vector2(gateHalf + 2.5f, 6f));

        var placed = new List<Vector2>();
        foreach (Vector2 p in spots)
        {
            if (ctx.Hf.ValleyDistance(p.x, p.y) > -1.5f) continue;
            if (ctx.Hf.NearRavine(p.x, p.y, 0.3f) || ctx.Hf.InRampLane(p.x, p.y, 0.5f)) continue;
            bool onPlot = false;
            foreach (Vector2 t in ctx.Spec.towerPlots) onPlot |= Vector2.Distance(t, p) < 5f;
            foreach (BridgeFootprint br in ctx.Bridges) onPlot |= br.Contains(p, 0.4f);
            bool crowded = false;
            foreach (Vector2 q in placed) crowded |= (q - p).sqrMagnitude < 9f;
            if (onPlot || crowded) continue;
            GameObject prefab = Pick(ctx, lanterns);
            GameObject go = Place(ctx, parent, prefab, p, Rand(ctx, 0f, 360f),
                Rand(ctx, pal.lanternScale.x, pal.lanternScale.y), 0.03f, 0f);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            placed.Add(p);
        }
    }

    /// <summary>A point light at the lamp of every lantern instance under <paramref name="root" />, clusters included.</summary>
    private static void AddLanternLights(DefenseBuildContext ctx, Transform root, List<GameObject> lanterns)
    {
        if (lanterns.Count == 0) return;
        DefenseBiomePaletteSO pal = ctx.Spec.palette;
        var sources = new HashSet<GameObject>(lanterns);
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            GameObject src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            if (src == null || !sources.Contains(src)) continue;
            Renderer[] rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) continue;
            Bounds b = rends[0].bounds;
            foreach (Renderer r in rends) b.Encapsulate(r.bounds);
            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(t, true);
            lightGo.transform.position = new Vector3(b.center.x, b.min.y + b.size.y * pal.lanternLightHeight, b.center.z);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = pal.lanternLightColor;
            light.intensity = pal.lanternLightIntensity * Rand(ctx, 0.85f, 1.1f);
            light.range = pal.lanternLightRange;
            light.shadows = LightShadows.None;
            count++;
        }
        Debug.Log($"[DefenseSceneScatter] {count} lantern lights.");
    }

    /// <summary>Low fog patches on a grid over the field and courtyard, plus a line of them pooled along each ravine floor.</summary>
    private static void PlaceGroundFog(DefenseBuildContext ctx, Transform parent, List<GameObject> fog)
    {
        if (fog.Count == 0) return;
        DefenseBiomePaletteSO pal = ctx.Spec.palette;
        List<Vector2> cands = Grid(ctx, pal.groundFogSpacing, p =>
            ctx.Hf.ValleyDistance(p.x, p.y) < 4f && p.y > -ctx.Spec.courtyardDepth && p.y < ctx.Spec.fieldEndZ + 5f);
        var spots = new List<Vector3>();
        foreach (Vector2 p in cands) spots.Add(new Vector3(p.x, ctx.Ground(p.x, p.y) + 0.3f, p.y));
        for (int i = 0; i < ctx.Spec.ravines.Count; i++)
        {
            Vector2 span = ctx.Hf.RavineXSpan(i);
            for (float x = span.x + 6f; x < span.y - 6f; x += 14f)
            {
                if (ctx.Hf.RavineExtentMask(i, x) < 0.9f) continue;
                float z = ctx.Hf.RavineCentreZ(i, x);
                spots.Add(new Vector3(x, ctx.Ground(x, z) + 0.4f, z));
            }
        }
        foreach (Vector3 pos in spots)
        {
            GameObject prefab = Pick(ctx, fog);
            var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Rand(ctx, 0f, 360f), 0f));
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                // Already settled when the scene loads, rather than drifting in over the first half-minute.
                if (main.loop) main.prewarm = true;
                ParticleSystem.MinMaxGradient c = main.startColor;
                c.color *= pal.groundFogTint;
                c.colorMin *= pal.groundFogTint;
                c.colorMax *= pal.groundFogTint;
                main.startColor = c;
            }
        }
    }

    /// <summary>Lights low in every ravine, so the spike pits read from the field at night.</summary>
    private static void PlaceRavineGlow(DefenseBuildContext ctx, Transform parent)
    {
        DefenseBiomePaletteSO pal = ctx.Spec.palette;
        if (pal.ravineGlowIntensity <= 0f) return;
        for (int i = 0; i < ctx.Spec.ravines.Count; i++)
        {
            Vector2 span = ctx.Hf.RavineXSpan(i);
            for (float x = span.x + 5f; x < span.y - 5f; x += 12f)
            {
                if (ctx.Hf.RavineExtentMask(i, x) < 0.9f) continue;
                float z = ctx.Hf.RavineCentreZ(i, x);
                var go = new GameObject("PitGlow");
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(x, ctx.Ground(x, z) + 1.2f, z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = pal.ravineGlowColor;
                light.intensity = pal.ravineGlowIntensity;
                light.range = pal.ravineGlowRange;
                light.shadows = LightShadows.None;
            }
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
