using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
///     Builds a complete gate-defense battle scene from a <see cref="DefenseSceneSpecSO" />: sculpted and
///     painted terrain, castle wall + gate + buildings, ravines with bridges/spikes/exit ramps, measured
///     Synty scatter, an invisible play-area boundary, NavMesh for every agent type, then the standard
///     Survivors wiring (SetupSurvivorsSceneTool) with plots/spawns/objectives laid out by rule, and
///     finally <see cref="DefenseSceneValidator" />. Deterministic for a given spec + seed, and always
///     regenerates the whole scene, so tune the spec and re-run rather than hand-editing the output.
/// </summary>
public static class DefenseSceneGenerator
{
    private const string Tag = "[DefenseSceneGenerator]";
    private const float BoundaryInset = 4f;
    private const float DeckRise = 0.18f;

    [MenuItem("Bladehold/Scene Gen/Generate Selected Defense Scene Spec")]
    private static void GenerateSelected()
    {
        var spec = Selection.activeObject as DefenseSceneSpecSO;
        if (spec == null)
        {
            Debug.LogError($"{Tag} Select a DefenseSceneSpecSO asset first.");
            return;
        }
        Generate(spec);
    }

    [MenuItem("Bladehold/Scene Gen/Validate Open Defense Scene (Selected Spec)")]
    private static void ValidateSelected()
    {
        var spec = Selection.activeObject as DefenseSceneSpecSO;
        if (spec == null)
        {
            Debug.LogError($"{Tag} Select the DefenseSceneSpecSO the open scene was generated from.");
            return;
        }
        DefenseSceneValidator.Validate(spec, new DefenseHeightfield(spec));
    }

    /// <summary>Entry point for agents (execute_code): regenerates the spec's scene and returns the validator report.</summary>
    public static string Generate(DefenseSceneSpecSO spec)
    {
        string problem = CheckSpec(spec);
        if (problem != null)
        {
            Debug.LogError($"{Tag} {problem}");
            return "FAILED: " + problem;
        }

        PrefabMeasure.ClearCache();
        var hf = new DefenseHeightfield(spec);
        var rng = new System.Random(spec.seed);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string dataFolder = EnsureSceneDataFolder(spec.scenePath);
        EditorSceneManager.SaveScene(scene, spec.scenePath);

        var env = new GameObject("Environment").transform;
        SetupLighting(spec.palette, env);

        Terrain terrain = DefenseTerrainBuilder.Build(spec, hf, env, dataFolder, rng);
        var ctx = new DefenseBuildContext(spec, hf, terrain, rng);

        GameObject gate = BuildCastle(ctx, env);
        BuildRavines(ctx, env);
        DefenseSceneScatter.Scatter(ctx, env);
        BuildPlayAreaBoundary(ctx, env);

        BakeNavMesh(ctx, env.gameObject, dataFolder);
        EditorSceneManager.SaveScene(scene);

        SetupSurvivorsSceneTool.RunSetup(gate, false, false);
        LayoutGameplay(ctx, gate);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string report = DefenseSceneValidator.Validate(spec, hf);
        Debug.Log($"{Tag} Generated {spec.scenePath}\n{report}");
        return report;
    }

    private static string CheckSpec(DefenseSceneSpecSO spec)
    {
        if (spec == null) return "No spec.";
        if (spec.palette == null) return $"{spec.name} has no palette.";
        DefenseBiomePaletteSO p = spec.palette;
        if (p.ground == null || p.slope == null || p.cliff == null || p.road == null || p.ravineFloor == null ||
            p.courtyard == null || p.groundVariant == null)
            return $"{p.name} is missing a terrain layer.";
        if (p.gate == null || p.wall == null || p.bridgeTile == null) return $"{p.name} is missing a gate/wall/bridge prefab.";
        if (spec.towerPlots.Count == 0) return "Spec has no tower plots.";
        if (spec.enemySpawns.Count < 9) return "Spec needs at least 9 enemy spawns (DefeatSlayer uses index 8).";
        if (!Mathf.IsPowerOfTwo(spec.heightmapResolution - 1)) return "heightmapResolution must be 2^n + 1.";
        foreach (RavineSpec r in spec.ravines)
        {
            if (r.depth >= spec.playfieldElevation) return "A ravine is deeper than playfieldElevation.";
            if (r.bridges.Count == 0) return "Every ravine needs at least one bridge or enemies can't reach the gate.";
            foreach (RampSpec ramp in r.exitRamps)
                if (ramp.slopeDegrees >= 40f) return "Exit ramps must stay under 40 degrees (Humanoid max slope is 45).";
        }
        return null;
    }

    private static string EnsureSceneDataFolder(string scenePath)
    {
        string dir = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(scenePath);
        string folder = $"{dir}/{name}";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(dir, name);
        return folder;
    }

    // ---------------------------------------------------------------- lighting

    private static void SetupLighting(DefenseBiomePaletteSO p, Transform env)
    {
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(env);
        sunGo.transform.rotation = Quaternion.Euler(p.sunEuler);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = p.sunColor;
        sun.intensity = p.sunIntensity;
        sun.shadows = LightShadows.Soft;

        RenderSettings.sun = sun;
        RenderSettings.skybox = p.skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = p.fogColor;
        RenderSettings.fogDensity = p.fogDensity;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;

        if (p.volumeProfile != null)
        {
            var volGo = new GameObject("Global Volume");
            volGo.transform.SetParent(env);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = p.volumeProfile;
        }
    }

    // ---------------------------------------------------------------- castle

    private static GameObject BuildCastle(DefenseBuildContext ctx, Transform env)
    {
        DefenseSceneSpecSO spec = ctx.Spec;
        DefenseBiomePaletteSO p = spec.palette;
        var castle = new GameObject("Castle").transform;
        castle.SetParent(env);

        // Gate root faces +Z (towards the attackers) so SetupSurvivorsSceneTool's AttackPoint lands in front.
        var gateRoot = new GameObject("Gate");
        gateRoot.transform.SetParent(castle);
        gateRoot.transform.position = new Vector3(0f, ctx.Ground(0f, 0f), 0f);
        Vector3 gateModelPos = PrefabMeasure.RootForCentre(p.gate, Vector2.zero, p.gateModelYaw, 1f);
        GameObject gateModel = PrefabMeasure.Instantiate(p.gate, gateRoot.transform,
            new Vector3(gateModelPos.x, gateRoot.transform.position.y - 0.05f, gateModelPos.z),
            Quaternion.Euler(0f, p.gateModelYaw, 0f), 1f);
        gateModel.name = "GateModel";

        Bounds gb = PrefabMeasure.LocalBounds(p.gate);
        float gateHalf = gb.size.x * 0.5f;
        var col = gateRoot.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 3f, 0f);
        col.size = new Vector3(Mathf.Min(gb.size.x, 8f), 6f, 2f);
        ctx.Occupy(Vector2.zero, gateHalf + 1f);

        // Wall modules outwards from the gate until buried in the notch rock on both sides.
        Bounds wb = PrefabMeasure.LocalBounds(p.wall);
        float pitch = wb.size.x;
        float wallEnd = spec.gateNotchHalfWidth + spec.wallEmbed;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; ; k++)
            {
                float cx = side * (gateHalf + pitch * (k + 0.5f));
                if (Mathf.Abs(cx) - pitch * 0.5f > wallEnd) break;
                float ground = Mathf.Min(ctx.Ground(cx - pitch * 0.5f, 0f), ctx.Ground(cx + pitch * 0.5f, 0f),
                    ctx.Ground(cx, 0f));
                Vector3 root = PrefabMeasure.RootForCentre(p.wall, new Vector2(cx, 0f), p.wallYaw, 1f);
                root.y = ground - PrefabMeasure.BaseOffset(p.wall, 1f) - 0.1f;
                GameObject w = PrefabMeasure.Instantiate(p.wall, castle, root, Quaternion.Euler(0f, p.wallYaw, 0f), 1f);
                w.name = $"Wall_{(side < 0 ? "L" : "R")}{k}";
            }

            if (p.wallTower != null)
            {
                float tr = PrefabMeasure.FootprintRadius(p.wallTower, 1f);
                float tx = side * (gateHalf + tr * 0.6f);
                Vector3 troot = PrefabMeasure.RootForCentre(p.wallTower, new Vector2(tx, 0f), 0f, 1f);
                troot.y = ctx.Ground(tx, 0f);
                PrefabMeasure.Instantiate(p.wallTower, castle, troot, Quaternion.identity, 1f).name =
                    $"WallTower_{(side < 0 ? "L" : "R")}";
            }
        }
        ctx.WallLine = new Vector2(-wallEnd, wallEnd);

        foreach (StructurePlacement b in spec.castleBuildings)
        {
            if (b.prefab == null) continue;
            Vector3 root = PrefabMeasure.RootForCentre(b.prefab, b.position, b.yaw, 1f);
            root.y = ctx.MinGroundUnder(b.prefab, root, b.yaw, 1f, 0.9f) - PrefabMeasure.BaseOffset(b.prefab, 1f) - 0.15f;
            GameObject go = PrefabMeasure.Instantiate(b.prefab, castle, root, Quaternion.Euler(0f, b.yaw, 0f), 1f);
            ctx.Occupy(b.position, PrefabMeasure.FootprintRadius(b.prefab, 1f));
            ctx.Buildings.Add(go);
        }

        return gateRoot;
    }

    // ---------------------------------------------------------------- ravines

    private static void BuildRavines(DefenseBuildContext ctx, Transform env)
    {
        DefenseSceneSpecSO spec = ctx.Spec;
        DefenseBiomePaletteSO p = spec.palette;
        SpikePitConfigSO pitConfig = AssetDatabase.LoadAssetAtPath<SpikePitConfigSO>(DefenseSceneDefaults.SpikePitConfigPath);
        if (pitConfig == null) Debug.LogError($"{Tag} No SpikePitConfigSO at {DefenseSceneDefaults.SpikePitConfigPath}.");

        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            var root = new GameObject($"Ravine_{i}").transform;
            root.SetParent(env);
            Vector2 span = ctx.Hf.RavineXSpan(i);

            if (p.bridgeSpan != null)
            {
                for (int bi = 0; bi < r.bridges.Count; bi++) BuildSpanBridge(ctx, root, i, bi);
            }

            // Bridges: tiles laid along Z across the gap, resting on the rims.
            Bounds tb = PrefabMeasure.LocalBounds(p.bridgeTile);
            float pitchZ = Mathf.Floor(tb.size.z);
            float pitchX = Mathf.Floor(tb.size.x);
            for (int bi = 0; p.bridgeSpan == null && bi < r.bridges.Count; bi++)
            {
                BridgeSpec b = r.bridges[bi];
                var bridgeRoot = new GameObject($"Bridge_{bi}").transform;
                bridgeRoot.SetParent(root);
                float zc = ctx.Hf.RavineCentreZ(i, b.x);
                float width = pitchX * b.tilesWide;
                float slant = Mathf.Abs(ctx.Hf.RavineCentreZ(i, b.x + width * 0.5f) - ctx.Hf.RavineCentreZ(i, b.x - width * 0.5f));
                float span2 = r.topWidth + slant + 2f * 2.5f;
                int tilesLong = Mathf.CeilToInt(span2 / pitchZ);
                float startZ = zc - tilesLong * pitchZ * 0.5f;
                float rimY = Mathf.Max(ctx.Ground(b.x, startZ + 0.5f), ctx.Ground(b.x, startZ + tilesLong * pitchZ - 0.5f));
                float deckTop = tb.max.y;

                for (int tx = 0; tx < b.tilesWide; tx++)
                {
                    float cx = b.x - width * 0.5f + pitchX * (tx + 0.5f);
                    for (int tz = 0; tz < tilesLong; tz++)
                    {
                        float cz = startZ + pitchZ * (tz + 0.5f);
                        Vector3 pos = PrefabMeasure.RootForCentre(p.bridgeTile, new Vector2(cx, cz), 0f, 1f);
                        // Deck sits a little proud of the rims so terrain never pokes through the end planks;
                        // the step is well under both agents' climb height.
                        pos.y = rimY - deckTop + DeckRise;
                        PrefabMeasure.Instantiate(p.bridgeTile, bridgeRoot, pos, Quaternion.identity, 1f);
                    }

                    if (p.bridgePillar != null)
                    {
                        // Pillars under the tile joints nearest each rim, reaching down to the floor.
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float rimZ = zc + side * (r.topWidth * 0.5f - 0.5f);
                            float jz = startZ + Mathf.Round((rimZ - startZ) / pitchZ) * pitchZ;
                            Vector3 pp = PrefabMeasure.RootForCentre(p.bridgePillar, new Vector2(cx, jz), 0f, 1f);
                            pp.y = rimY - deckTop + DeckRise;
                            PrefabMeasure.Instantiate(p.bridgePillar, bridgeRoot, pp, Quaternion.identity, 1f);
                        }
                    }
                }

                ctx.Bridges.Add(new BridgeFootprint
                {
                    ravine = i,
                    centre = new Vector2(b.x, zc),
                    halfWidth = width * 0.5f,
                    halfLength = tilesLong * pitchZ * 0.5f
                });
                ctx.Occupy(new Vector2(b.x, zc), 0f);
            }

            // Spikes on the floor, clear of the ramp lanes. Colliders are stripped: they are visual only,
            // the SpikePit volumes do the damage, and they must not punch holes in the floor NavMesh.
            if (p.pitSpikes.Count > 0)
            {
                var spikes = new GameObject("Spikes").transform;
                spikes.SetParent(root);
                float halfFloor = r.floorWidth * 0.5f - 0.6f;
                for (float x = span.x + 1.5f; x < span.y - 1.5f; x += 2.2f)
                {
                    for (float o = -halfFloor; o <= halfFloor; o += 2.2f)
                    {
                        float jx = x + (float)(ctx.Rng.NextDouble() - 0.5) * 1.4f;
                        float z = ctx.Hf.RavineCentreZ(i, jx) + o + (float)(ctx.Rng.NextDouble() - 0.5) * 1.0f;
                        if (ctx.Rng.NextDouble() > 0.6) continue;
                        if (ctx.Hf.InRampLane(jx, z, 1.5f)) continue;
                        if (ctx.Hf.RavineExtentMask(i, jx) < 0.95f) continue;
                        GameObject prefab = p.pitSpikes[ctx.Rng.Next(p.pitSpikes.Count)];
                        float s = 0.9f + (float)ctx.Rng.NextDouble() * 0.5f;
                        var pos = new Vector3(jx, ctx.Ground(jx, z) - PrefabMeasure.BaseOffset(prefab, s) - 0.15f, z);
                        GameObject go = PrefabMeasure.Instantiate(prefab, spikes, pos,
                            Quaternion.Euler(0f, (float)ctx.Rng.NextDouble() * 360f, 0f), s);
                        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    }
                }
            }

            // Hazard volumes: one box per ~10 m segment, following the meander.
            var pits = new GameObject("SpikePits").transform;
            pits.SetParent(root);
            const float segment = 10f;
            for (float x = span.x; x < span.y; x += segment)
            {
                float cx = Mathf.Min(x + segment * 0.5f, span.y);
                float len = Mathf.Min(segment, span.y - x);
                if (len < 1f) continue;
                Vector2 tan = ctx.Hf.RavineTangent(i, cx);
                float cz = ctx.Hf.RavineCentreZ(i, cx);
                var go = new GameObject($"SpikePit_{pits.childCount}");
                go.transform.SetParent(pits);
                go.transform.position = new Vector3(cx, -r.depth + 1.3f, cz);
                go.transform.rotation = Quaternion.LookRotation(new Vector3(tan.x, 0f, tan.y), Vector3.up);
                var pit = go.AddComponent<SpikePit>();
                var so = new SerializedObject(pit);
                so.FindProperty("config").objectReferenceValue = pitConfig;
                so.FindProperty("halfExtents").vector3Value = new Vector3(r.floorWidth * 0.5f + 0.6f, 1.6f, len * 0.5f + 0.3f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    /// <summary>Half the deck width of a bridge: span pieces at their scaled width, else 5 m tiles.</summary>
    public static float BridgeHalfWidth(DefenseBiomePaletteSO p, BridgeSpec b)
    {
        if (p.bridgeSpan != null)
            return PrefabMeasure.LocalBounds(p.bridgeSpan).size.x * p.bridgeSpanWidthScale * b.tilesWide * 0.5f;
        return Mathf.Floor(PrefabMeasure.LocalBounds(p.bridgeTile).size.x) * b.tilesWide * 0.5f;
    }

    /// <summary>
    ///     One arched span per tile column, centred on the ravine. The deck is found by raycasting the
    ///     instance's own colliders, then the whole piece is dropped so its higher end sits
    ///     <see cref="DeckRise" /> above the rim: no step onto it, and the arch stays proud of the gap.
    /// </summary>
    private static void BuildSpanBridge(DefenseBuildContext ctx, Transform ravineRoot, int ravine, int bi)
    {
        DefenseBiomePaletteSO p = ctx.Spec.palette;
        BridgeSpec b = ctx.Spec.ravines[ravine].bridges[bi];
        var bridgeRoot = new GameObject($"Bridge_{bi}").transform;
        bridgeRoot.SetParent(ravineRoot);
        Bounds lb = PrefabMeasure.LocalBounds(p.bridgeSpan);
        float pitchX = lb.size.x * p.bridgeSpanWidthScale;
        float halfWidth = BridgeHalfWidth(p, b);
        float zc = ctx.Hf.RavineCentreZ(ravine, b.x);
        float halfLen = lb.size.z * 0.5f;

        for (int tx = 0; tx < b.tilesWide; tx++)
        {
            float cx = b.x - halfWidth + pitchX * (tx + 0.5f);
            Vector3 pos = PrefabMeasure.RootForCentre(p.bridgeSpan, new Vector2(cx, zc), 0f, 1f);
            pos.y = 0f;
            GameObject go = PrefabMeasure.Instantiate(p.bridgeSpan, bridgeRoot, pos, Quaternion.identity, 1f);
            go.transform.localScale = new Vector3(p.bridgeSpanWidthScale, 1f, 1f);
            Physics.SyncTransforms();

            float lift = float.MinValue;
            for (int end = -1; end <= 1; end += 2)
            {
                float z = zc + end * (halfLen - 0.4f);
                float deck = DeckHeight(go, cx, z);
                if (float.IsNaN(deck)) continue;
                lift = Mathf.Max(lift, ctx.Ground(cx, z) + DeckRise - deck);
            }
            if (lift == float.MinValue) Debug.LogError($"{Tag} Bridge {bi} on ravine {ravine}: no deck collider under the span ends.");
            else go.transform.position += Vector3.up * lift;
        }
        Physics.SyncTransforms();

        ctx.Bridges.Add(new BridgeFootprint
        {
            ravine = ravine,
            centre = new Vector2(b.x, zc),
            halfWidth = halfWidth,
            halfLength = halfLen
        });
        ctx.Occupy(new Vector2(b.x, zc), 0f);
    }

    private static float DeckHeight(GameObject bridge, float x, float z)
    {
        float best = float.NaN;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f))
            if (h.collider.transform.IsChildOf(bridge.transform) && (float.IsNaN(best) || h.point.y > best)) best = h.point.y;
        return best;
    }

    // ---------------------------------------------------------------- boundary

    /// <summary>
    ///     Invisible double-sided collider ribbon just inside the foothills. It keeps the player in the
    ///     valley and, because NavMesh bakes from physics colliders, stops enemies routing over the
    ///     foothills around a ravine's end instead of using the bridges.
    /// </summary>
    private static void BuildPlayAreaBoundary(DefenseBuildContext ctx, Transform env)
    {
        DefenseHeightfield hf = ctx.Hf;
        DefenseSceneSpecSO spec = ctx.Spec;
        var pts = new List<Vector2>();

        float capCentreZ = spec.fieldEndZ - spec.cornerRadius;
        var right = new List<Vector2>();
        for (float z = -1f; z < capCentreZ; z += 2f)
            right.Add(new Vector2(Bisect(t => hf.ValleyDistance(t, z), 0f, spec.terrainSize), z));
        pts.AddRange(right);
        var capC = new Vector2(0f, capCentreZ);
        for (float a = 0f; a <= 180.01f; a += 3f)
        {
            var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            float t = Bisect(s => hf.ValleyDistance(capC.x + dir.x * s, capC.y + dir.y * s), 0f, spec.terrainSize);
            pts.Add(capC + dir * t);
        }
        for (int i = right.Count - 1; i >= 0; i--) pts.Add(new Vector2(-right[i].x, right[i].y));

        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (Vector2 pnt in pts)
        {
            float g = ctx.Ground(pnt.x, pnt.y);
            verts.Add(new Vector3(pnt.x, g - 12f, pnt.y));
            verts.Add(new Vector3(pnt.x, g + 30f, pnt.y));
        }
        for (int i = 0; i < pts.Count - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.AddRange(new[] { a, b, c, b, d, c });
            tris.AddRange(new[] { a, c, b, b, c, d });
        }

        var mesh = new Mesh { name = "PlayAreaBoundary" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, $"{EnsureSceneDataFolder(spec.scenePath)}/PlayAreaBoundary.asset");

        var go = new GameObject("PlayAreaBoundary");
        go.transform.SetParent(env);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        ctx.BoundaryPolyline = pts;

        float Bisect(System.Func<float, float> dist, float lo, float hi)
        {
            // Walk out until the distance crosses the inset, then bisect.
            float target = BoundaryInset;
            float step = 2f, prev = lo;
            for (float s = lo; s < hi; s += step)
            {
                if (dist(s) >= target)
                {
                    float a = prev, b = s;
                    for (int k = 0; k < 20; k++)
                    {
                        float m = (a + b) * 0.5f;
                        if (dist(m) >= target) b = m;
                        else a = m;
                    }
                    return (a + b) * 0.5f;
                }
                prev = s;
            }
            return hi;
        }
    }

    // ---------------------------------------------------------------- navmesh

    private static void BakeNavMesh(DefenseBuildContext ctx, GameObject envRoot, string dataFolder)
    {
        DefenseSceneSpecSO spec = ctx.Spec;
        var navRoot = new GameObject("NavMesh");
        navRoot.transform.SetParent(envRoot.transform);

        // Only the valley (plus margin) is baked: the mountains and courtyard need no NavMesh.
        float halfX = spec.fieldHalfWidth + spec.edgeNoise + 20f;
        float zMin = -8f, zMax = spec.fieldEndZ + 20f;
        var centre = new Vector3(0f, 5f, (zMin + zMax) * 0.5f);
        var size = new Vector3(halfX * 2f, 60f, zMax - zMin);

        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            string agentName = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
            var surface = navRoot.AddComponent<NavMeshSurface>();
            surface.agentTypeID = settings.agentTypeID;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = centre;
            surface.size = size;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // A freshly created TerrainCollider isn't in the physics scene until synced; without this the
            // first surface bakes only the prefab colliders (wall tops) and misses the ground entirely.
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            Vector3 probe = new Vector3(spec.playerSpawn.x, ctx.Ground(spec.playerSpawn.x, spec.playerSpawn.y), spec.playerSpawn.y);
            if (!NavMesh.SamplePosition(probe, out _, 2f, filter))
                Debug.LogError($"{Tag} '{NavMesh.GetSettingsNameFromID(settings.agentTypeID)}' NavMesh missing at the player spawn after baking.");
            if (surface.navMeshData != null)
            {
                string path = $"{dataFolder}/NavMesh-{agentName.Replace(' ', '_')}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            }
            Debug.Log($"{Tag} Baked NavMesh for agent '{agentName}'.");
        }
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- gameplay

    private static void LayoutGameplay(DefenseBuildContext ctx, GameObject gate)
    {
        DefenseSceneSpecSO spec = ctx.Spec;

        // Player
        GameObject player = GameObject.Find("Player");
        Vector3 playerPos = ctx.OnNavMesh(spec.playerSpawn);
        if (player != null)
        {
            player.transform.SetPositionAndRotation(playerPos + Vector3.up * 0.1f, Quaternion.identity);
        }

        // Tower plots
        PlaceTowerPlots(ctx);

        // Enemy spawns
        GameObject spawner = GameObject.Find("EnemySpawner");
        if (spawner != null)
        {
            Transform sp = spawner.transform.Find("Spawnpoints");
            if (sp == null)
            {
                sp = new GameObject("Spawnpoints").transform;
                sp.SetParent(spawner.transform);
            }
            SetChildPoints(sp, "Spawnpoint_", spec.enemySpawns.ConvertAll(ctx.OnNavMesh));
            var comp = spawner.GetComponent<SurvivorsSpawner>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                SerializedProperty arr = so.FindProperty("spawnPoints");
                arr.arraySize = sp.childCount;
                for (int i = 0; i < sp.childCount; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = sp.GetChild(i);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Objective markers: everything must sit outside tower range.
        GameObject objectives = GameObject.Find("SurvivorsObjectives");
        if (objectives != null) LayoutObjectives(ctx, objectives, gate);

        // Per-scene enemy list (e.g. skeletons only), replacing the threat curve.
        if (spec.enemyRosterIds.Count > 0)
        {
            var rosterGo = new GameObject("SceneEnemyRoster");
            rosterGo.AddComponent<SceneEnemyRoster>().EditorSet(spec.enemyRosterIds.ToArray(), spec.fodderEnemyId);
        }

        // Intermission anchors next to the player spawn.
        Vector3 baseInt = ctx.OnNavMesh(spec.playerSpawn + new Vector2(0f, 5f));
        SetPos("UpgradePowerupSpawnPoint", baseInt + new Vector3(0f, 0f, -2f));
        SetPos("BannerSpawnPoint_0", baseInt + new Vector3(-2f, 0f, 0f));
        SetPos("BannerSpawnPoint_1", baseInt);
        SetPos("BannerSpawnPoint_2", baseInt + new Vector3(2f, 0f, 0f));
        GameObject cam = GameObject.Find("IntermissionVirtualCamera");
        if (cam == null)
        {
            foreach (GameObject r in SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == "IntermissionVirtualCamera") cam = r;
        }
        if (cam != null)
        {
            cam.transform.position = baseInt + new Vector3(0f, 3.5f, 7f);
            cam.transform.LookAt(baseInt);
        }
    }

    private static void SetPos(string name, Vector3 pos)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) go.transform.position = pos;
    }

    private static void PlaceTowerPlots(DefenseBuildContext ctx)
    {
        var plotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Defenses/TowerPlot.prefab");
        GameObject rootGo = GameObject.Find("Battlefield Tower Plots");
        if (rootGo == null) rootGo = new GameObject("Battlefield Tower Plots");
        TowerPlotManager manager = rootGo.GetComponent<TowerPlotManager>();
        if (manager == null) manager = rootGo.AddComponent<TowerPlotManager>();

        for (int i = 0; i < ctx.Spec.towerPlots.Count; i++)
        {
            GameObject plot = plotPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(plotPrefab, rootGo.transform)
                : new GameObject();
            plot.name = $"TowerPlot_{i + 1}";
            plot.transform.SetParent(rootGo.transform);
            plot.transform.SetPositionAndRotation(ctx.OnNavMesh(ctx.Spec.towerPlots[i]), Quaternion.identity);
            TowerPlot tp = plot.GetComponent<TowerPlot>();
            if (tp == null) tp = plot.AddComponent<TowerPlot>();
            tp.PlotIndex = i;
            manager.RegisterPlot(tp);
        }
        manager.RefreshPlots();
        EditorUtility.SetDirty(rootGo);
    }

    private static void LayoutObjectives(DefenseBuildContext ctx, GameObject objectives, GameObject gate)
    {
        DefenseSceneSpecSO spec = ctx.Spec;
        float clearance = DefenseSceneValidator.MaxTowerRange() + spec.objectiveRangeMargin;
        Vector3 playerPos = ctx.OnNavMesh(spec.playerSpawn);
        Vector3 gateTarget = gate.transform.position + gate.transform.forward * 3f;
        int largeAgent = DefenseSceneValidator.LargestAgentTypeId();

        // Candidates: a grid over the objective band, filtered by every rule, reachable from the player.
        var candidates = new List<Vector3>();
        for (float z = spec.objectiveZRange.x; z <= spec.objectiveZRange.y; z += 4f)
        {
            for (float x = -spec.fieldHalfWidth; x <= spec.fieldHalfWidth; x += 4f)
            {
                var p2 = new Vector2(x, z);
                if (ctx.Hf.ValleyDistance(x, z) > -10f) continue;
                if (ctx.Hf.NearRavine(x, z, 8f)) continue;
                if (ctx.DistanceToRoad(p2) < 5f) continue;
                if (DefenseSceneValidator.MinDistanceToPlots(spec, p2) < clearance) continue;
                bool nearSpawn = false;
                foreach (Vector2 s in spec.enemySpawns) nearSpawn |= Vector2.Distance(s, p2) < 10f;
                if (nearSpawn) continue;
                if (!NavMesh.SamplePosition(new Vector3(x, ctx.Ground(x, z), z), out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                if (!DefenseSceneValidator.PathComplete(hit.position, playerPos, 0)) continue;
                candidates.Add(hit.position);
            }
        }
        if (candidates.Count < 8)
        {
            Debug.LogError($"{Tag} Only {candidates.Count} objective candidates satisfy the rules; widen objectiveZRange or the field.");
            if (candidates.Count == 0) return;
        }

        List<Vector3> picked = FarthestPointPick(candidates, 8, ctx.Rng);
        // Siege engines and cages spread across the band; the large-agent routes (wagon, ram) need a
        // Large Enemy path to the gate, so pick those from candidates that have one.
        var largeOk = candidates.FindAll(c => DefenseSceneValidator.PathComplete(c, gateTarget, largeAgent));
        Vector3 wagon = largeOk.Count > 0 ? Farthest(largeOk, gateTarget, -0.5f) : picked[0];
        Vector3 ram = largeOk.Count > 0 ? Farthest(largeOk, wagon, 0f) : picked[1];

        var cages = picked.GetRange(0, Mathf.Min(3, picked.Count));
        var cats = picked.GetRange(Mathf.Min(3, picked.Count), Mathf.Min(3, Mathf.Max(0, picked.Count - 3)));

        Transform cageRoot = EnsureChild(objectives.transform, "CageSpawns");
        SetChildPoints(cageRoot, "CageSpawn_", cages);
        Transform catRoot = EnsureChild(objectives.transform, "CatapultSpawns");
        SetChildPoints(catRoot, "CatapultSpawn_", cats);

        Transform wagonRoute = EnsureChild(objectives.transform, "WagonRoute");
        Transform gdp = EnsureChild(wagonRoute, "GateDestinationPoint");
        gdp.position = ctx.OnNavMesh(new Vector2(gateTarget.x, gateTarget.z));
        Transform wsp = EnsureChild(wagonRoute, "WagonSpawnPoint");
        wsp.position = wagon;
        Transform ramRoute = EnsureChild(objectives.transform, "RamRoute");
        Transform rsp = EnsureChild(ramRoute, "RamSpawnPoint");
        rsp.position = ram;

        // Golden goblin loops the middle of the field, inside tower range.
        Transform gg = EnsureChild(objectives.transform, "GoldenGoblinWaypoints");
        var ggPts = new List<Vector3>
        {
            ctx.OnNavMesh(new Vector2(-35f, 28f)), ctx.OnNavMesh(new Vector2(35f, 30f)),
            ctx.OnNavMesh(new Vector2(20f, 46f)), ctx.OnNavMesh(new Vector2(-22f, 44f))
        };
        SetChildPoints(gg, "Waypoint_", ggPts);

        foreach (var c in objectives.GetComponentsInChildren<FreePrisonersObjective>(true))
            SetTransformArray(c, "spawnPoints", cageRoot);
        foreach (var c in objectives.GetComponentsInChildren<DestroySiegeEnginesObjective>(true))
            SetTransformArray(c, "spawnPoints", catRoot);
        foreach (var c in objectives.GetComponentsInChildren<ProtectWagonObjective>(true))
            SetRefs(c, ("wagonSpawnPoint", wsp), ("gateDestinationPoint", gdp));
        foreach (var c in objectives.GetComponentsInChildren<StopBatteringRamObjective>(true))
            SetRefs(c, ("ramSpawnPoint", rsp), ("gateDestinationPoint", gdp));
    }

    private static List<Vector3> FarthestPointPick(List<Vector3> pool, int n, System.Random rng)
    {
        var picked = new List<Vector3> { pool[rng.Next(pool.Count)] };
        while (picked.Count < n && picked.Count < pool.Count)
        {
            Vector3 best = pool[0];
            float bestD = -1f;
            foreach (Vector3 c in pool)
            {
                float d = float.MaxValue;
                foreach (Vector3 p in picked) d = Mathf.Min(d, (c - p).sqrMagnitude);
                if (d > bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            picked.Add(best);
        }
        return picked;
    }

    /// <summary>Candidate farthest from <paramref name="from" /> (lean &lt; 0 = prefer nearer the middle X).</summary>
    private static Vector3 Farthest(List<Vector3> pool, Vector3 from, float lean)
    {
        Vector3 best = pool[0];
        float bestScore = float.MinValue;
        foreach (Vector3 c in pool)
        {
            float score = (c - from).magnitude + lean * Mathf.Abs(c.x);
            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        return best;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static void SetChildPoints(Transform parent, string prefix, List<Vector3> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            Transform ch = i < parent.childCount ? parent.GetChild(i) : null;
            if (ch == null)
            {
                ch = new GameObject($"{prefix}{i + 1}").transform;
                ch.SetParent(parent, false);
            }
            ch.position = points[i];
        }
        for (int i = parent.childCount - 1; i >= points.Count; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static void SetTransformArray(Object target, string prop, Transform parent)
    {
        var so = new SerializedObject(target);
        SerializedProperty arr = so.FindProperty(prop);
        if (arr == null) return;
        arr.arraySize = parent.childCount;
        for (int i = 0; i < parent.childCount; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = parent.GetChild(i);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRefs(Object target, params (string prop, Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (prop, value) in refs)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"{Tag} {target.GetType().Name} has no field '{prop}'.");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

public class BridgeFootprint
{
    public int ravine;
    public Vector2 centre;
    public float halfWidth;
    public float halfLength;

    public bool Contains(Vector2 p, float margin) =>
        Mathf.Abs(p.x - centre.x) <= halfWidth + margin && Mathf.Abs(p.y - centre.y) <= halfLength + margin;
}

/// <summary>Shared state passed between generator stages.</summary>
public class DefenseBuildContext
{
    public readonly DefenseSceneSpecSO Spec;
    public readonly DefenseHeightfield Hf;
    public readonly Terrain Terrain;
    public readonly System.Random Rng;
    public readonly List<BridgeFootprint> Bridges = new List<BridgeFootprint>();
    public readonly List<GameObject> Buildings = new List<GameObject>();
    public List<Vector2> BoundaryPolyline;
    public Vector2 WallLine;

    private readonly List<Vector3> occupied = new List<Vector3>(); // x, z, radius
    private readonly List<List<Vector2>> roads;

    public DefenseBuildContext(DefenseSceneSpecSO spec, DefenseHeightfield hf, Terrain terrain, System.Random rng)
    {
        Spec = spec;
        Hf = hf;
        Terrain = terrain;
        Rng = rng;
        roads = hf.Roads();
    }

    public float Ground(float x, float z) => Terrain.SampleHeight(new Vector3(x, 0f, z)) + Terrain.transform.position.y;

    public Vector3 Normal(float x, float z)
    {
        TerrainData td = Terrain.terrainData;
        Vector3 tp = Terrain.transform.position;
        return td.GetInterpolatedNormal((x - tp.x) / td.size.x, (z - tp.z) / td.size.z);
    }

    public float Steepness(float x, float z)
    {
        TerrainData td = Terrain.terrainData;
        Vector3 tp = Terrain.transform.position;
        return td.GetSteepness((x - tp.x) / td.size.x, (z - tp.z) / td.size.z);
    }

    public float MinGroundUnder(GameObject prefab, Vector3 root, float yaw, float scale, float shrink)
    {
        float min = Ground(root.x, root.z);
        foreach (Vector2 c in PrefabMeasure.FootprintCorners(prefab, root, yaw, scale, shrink))
            min = Mathf.Min(min, Ground(c.x, c.y));
        return min;
    }

    public float DistanceToRoad(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (List<Vector2> r in roads) best = Mathf.Min(best, DefenseHeightfield.DistanceToPolyline(p, r));
        return best;
    }

    public void Occupy(Vector2 p, float radius) => occupied.Add(new Vector3(p.x, p.y, radius));

    public bool Overlaps(Vector2 p, float radius, float gapScale = 1f)
    {
        foreach (Vector3 o in occupied)
        {
            float min = (o.z + radius) * gapScale;
            if ((new Vector2(o.x, o.y) - p).sqrMagnitude < min * min) return true;
        }
        return false;
    }

    public Vector3 OnNavMesh(Vector2 p)
    {
        var raw = new Vector3(p.x, Ground(p.x, p.y), p.y);
        return NavMesh.SamplePosition(raw, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : raw;
    }

    /// <summary>
    ///     True where gameplay needs open ground: plots, spawns, the gate apron, roads, bridge approaches,
    ///     ramp lanes and the ravine itself. Scatter with colliders must stay out.
    /// </summary>
    public bool KeepClear(Vector2 p, float radius)
    {
        float clear = Spec.gameplayClearRadius + radius;
        foreach (Vector2 t in Spec.towerPlots) if (Vector2.Distance(t, p) < clear) return true;
        foreach (Vector2 s in Spec.enemySpawns) if (Vector2.Distance(s, p) < clear) return true;
        if (Vector2.Distance(Spec.playerSpawn, p) < clear + 4f) return true;
        if (p.y > -2f && p.y < 22f && Mathf.Abs(p.x) < Spec.gateNotchHalfWidth - 2f) return true;
        if (DistanceToRoad(p) < 3.5f + radius) return true;
        foreach (BridgeFootprint b in Bridges) if (b.Contains(p, 6f + radius)) return true;
        if (Hf.InRampLane(p.x, p.y, 2f + radius)) return true;
        if (Hf.NearRavine(p.x, p.y, 0.5f + radius * 0.5f)) return true;
        return false;
    }
}
