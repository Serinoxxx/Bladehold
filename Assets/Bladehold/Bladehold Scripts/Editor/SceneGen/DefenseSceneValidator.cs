using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Checks a generated defense scene against Bladehold's gameplay rules and reports each failure by
///     name, so layout problems are fixed in the spec rather than found in a play-test:
///     every spawn reaches the gate for every agent type and does so over a bridge, the wagon/ram reach
///     the gate as Large Enemies, pits have a way out, plots are flat and baked, and every objective
///     marker is outside the longest tower range.
/// </summary>
public static class DefenseSceneValidator
{
    private const string TowerPlotPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Defenses/TowerPlot.prefab";

    public static string Validate(DefenseSceneSpecSO spec, DefenseHeightfield hf)
    {
        var fails = new List<string>();
        var passes = new List<string>();

        var gate = Object.FindAnyObjectByType<Gate>();
        if (gate == null)
        {
            fails.Add("No Gate in the scene.");
            return Report(passes, fails);
        }
        Vector3 gateTarget = gate.transform.position + gate.transform.forward * 3f;

        // 1. Spawns reach the gate, for every agent type, crossing ravines only on bridges.
        var spawner = Object.FindAnyObjectByType<SurvivorsSpawner>();
        var spawnPts = new List<Transform>();
        if (spawner != null)
        {
            SerializedProperty arr = new SerializedObject(spawner).FindProperty("spawnPoints");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue is Transform t) spawnPts.Add(t);
        }
        if (spawnPts.Count < 9) fails.Add($"Spawner has {spawnPts.Count} spawn points; DefeatSlayer needs at least 9.");

        int agents = NavMesh.GetSettingsCount();
        for (int a = 0; a < agents; a++)
        {
            int id = NavMesh.GetSettingsByIndex(a).agentTypeID;
            string agentName = NavMesh.GetSettingsNameFromID(id);
            int ok = 0;
            foreach (Transform sp in spawnPts)
            {
                NavMeshPath path = CalcPath(sp.position, gateTarget, id);
                if (path == null || path.status != NavMeshPathStatus.PathComplete)
                {
                    fails.Add($"[{agentName}] spawn {sp.name} at {Fmt(sp.position)} has no complete path to the gate.");
                    continue;
                }
                string bypass = RavineBypass(spec, hf, path);
                if (bypass != null) fails.Add($"[{agentName}] spawn {sp.name}: {bypass}");
                else ok++;
            }
            passes.Add($"[{agentName}] {ok}/{spawnPts.Count} spawns path to the gate over bridges.");
        }

        // 2. Ravine floors: baked, and walk out to the gate (the exit ramps work).
        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            Vector2 span = hf.RavineXSpan(i);
            int samples = 0, reachable = 0;
            for (float x = span.x + 4f; x < span.y - 4f; x += 12f)
            {
                var p = new Vector3(x, -r.depth, hf.RavineCentreZ(i, x));
                // Past the play-area boundary the gorge is sealed off; nothing can land there.
                if (hf.ValleyDistance(p.x, p.z) > 2f) continue;
                samples++;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    fails.Add($"Ravine {i}: no floor NavMesh near {Fmt(p)} (a flung goblin would die instead of recovering).");
                    continue;
                }
                if (PathComplete(hit.position, gateTarget, 0)) reachable++;
                else fails.Add($"Ravine {i}: floor at {Fmt(hit.position)} has no way out to the gate (check exit ramps).");
            }
            passes.Add($"Ravine {i}: {reachable}/{samples} floor samples walk out to the gate.");
            if (r.exitRamps.Count == 0) fails.Add($"Ravine {i} has no exit ramps.");
        }

        // 3. Plots: on NavMesh and flat.
        var plots = Object.FindObjectsByType<TowerPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (plots.Length != spec.towerPlots.Count) fails.Add($"{plots.Length} TowerPlots in scene, spec has {spec.towerPlots.Count}.");
        foreach (TowerPlot tp in plots)
        {
            Vector3 p = tp.transform.position;
            if (!NavMesh.SamplePosition(p, out _, 0.6f, NavMesh.AllAreas)) fails.Add($"{tp.name} is off the NavMesh.");
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k < 12; k++)
            {
                float ang = k * Mathf.PI / 6f;
                float h = hf.Height(p.x + Mathf.Cos(ang) * 3f, p.z + Mathf.Sin(ang) * 3f);
                lo = Mathf.Min(lo, h);
                hi = Mathf.Max(hi, h);
            }
            if (hi - lo > 0.25f) fails.Add($"{tp.name} ground varies {hi - lo:F2} m within 3 m (towers will float or clip).");
            if (!PathComplete(p, gateTarget, 0)) fails.Add($"{tp.name} is cut off from the gate.");
        }
        passes.Add($"{plots.Length} tower plots checked for NavMesh and flatness.");

        // 4. Objective markers outside tower range and reachable.
        float maxRange = MaxTowerRange();
        var markers = new List<Transform>();
        var objectives = GameObject.Find("SurvivorsObjectives");
        if (objectives != null)
        {
            foreach (string group in new[] { "CageSpawns", "CatapultSpawns" })
            {
                Transform g = objectives.transform.Find(group);
                if (g != null) foreach (Transform c in g) markers.Add(c);
            }
            AddIf(markers, objectives.transform.Find("WagonRoute/WagonSpawnPoint"));
            AddIf(markers, objectives.transform.Find("RamRoute/RamSpawnPoint"));
        }
        var player = GameObject.Find("Player");
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        int largeId = LargestAgentTypeId();
        foreach (Transform m in markers)
        {
            float d = MinDistanceToPlots(spec, new Vector2(m.position.x, m.position.z));
            if (d < maxRange) fails.Add($"Objective {m.parent.name}/{m.name} is {d:F1} m from a plot, inside tower range ({maxRange} m).");
            if (!PathComplete(m.position, playerPos, 0)) fails.Add($"Objective {m.name} can't be reached from the player spawn.");
            bool large = m.name == "WagonSpawnPoint" || m.name == "RamSpawnPoint";
            if (large && !PathComplete(m.position, gateTarget, largeId))
                fails.Add($"{m.name} has no Large Enemy path to the gate (wagon/ram would stall).");
        }
        passes.Add($"{markers.Count} objective markers checked against tower range {maxRange} m (+{spec.objectiveRangeMargin} m spec margin).");

        // 5. Bridges carry the Large agent.
        foreach (RavineSpec r in spec.ravines)
        {
            int ri = spec.ravines.IndexOf(r);
            foreach (BridgeSpec b in r.bridges)
            {
                var p = new Vector3(b.x, 0f, hf.RavineCentreZ(ri, b.x));
                var filter = new NavMeshQueryFilter { agentTypeID = largeId, areaMask = NavMesh.AllAreas };
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, filter) || hit.position.y < -1f)
                    fails.Add($"Bridge at x={b.x} on ravine {ri} has no Large Enemy NavMesh on its deck.");
            }
        }

        return Report(passes, fails);
    }

    private static void AddIf(List<Transform> list, Transform t)
    {
        if (t != null) list.Add(t);
    }

    private static string Report(List<string> passes, List<string> fails)
    {
        var sb = new StringBuilder();
        sb.AppendLine(fails.Count == 0 ? "VALIDATION PASSED" : $"VALIDATION FAILED ({fails.Count} problems)");
        foreach (string f in fails) sb.AppendLine("  FAIL " + f);
        foreach (string p in passes) sb.AppendLine("  ok   " + p);
        string s = sb.ToString();
        if (fails.Count > 0) Debug.LogWarning("[DefenseSceneValidator] " + s);
        else Debug.Log("[DefenseSceneValidator] " + s);
        return s;
    }

    private static string Fmt(Vector3 p) => $"({p.x:F0},{p.y:F1},{p.z:F0})";

    /// <summary>Returns a message if the path crosses a ravine anywhere but on a bridge (i.e. walks around its end).</summary>
    private static string RavineBypass(DefenseSceneSpecSO spec, DefenseHeightfield hf, NavMeshPath path)
    {
        Vector3[] c = path.corners;
        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            for (int k = 0; k < c.Length - 1; k++)
            {
                float za = c[k].z - hf.RavineCentreZ(i, c[k].x);
                float zb = c[k + 1].z - hf.RavineCentreZ(i, c[k + 1].x);
                if (Mathf.Sign(za) == Mathf.Sign(zb)) continue;
                float t = za / (za - zb);
                Vector3 cross = Vector3.Lerp(c[k], c[k + 1], t);
                bool onBridge = false;
                foreach (BridgeSpec b in r.bridges)
                    onBridge |= Mathf.Abs(cross.x - b.x) <= 2.5f * b.tilesWide + 0.5f;
                if (!onBridge) return $"crosses ravine {i} at x={cross.x:F0} without a bridge.";
            }
        }
        return null;
    }

    private static NavMeshPath CalcPath(Vector3 from, Vector3 to, int agentTypeId)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(from, out NavMeshHit a, 3f, filter)) return null;
        if (!NavMesh.SamplePosition(to, out NavMeshHit b, 4f, filter)) return null;
        var path = new NavMeshPath();
        NavMesh.CalculatePath(a.position, b.position, filter, path);
        return path;
    }

    public static bool PathComplete(Vector3 from, Vector3 to, int agentTypeId)
    {
        NavMeshPath p = CalcPath(from, to, agentTypeId);
        return p != null && p.status == NavMeshPathStatus.PathComplete;
    }

    public static float MinDistanceToPlots(DefenseSceneSpecSO spec, Vector2 p)
    {
        float best = float.MaxValue;
        foreach (Vector2 t in spec.towerPlots) best = Mathf.Min(best, Vector2.Distance(t, p));
        return best;
    }

    /// <summary>Agent type with the largest radius (the "Large Enemy" type used by trolls, wagons and rams).</summary>
    public static int LargestAgentTypeId()
    {
        int best = 0;
        float bestR = -1f;
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
            if (s.agentRadius > bestR)
            {
                bestR = s.agentRadius;
                best = s.agentTypeID;
            }
        }
        return best;
    }

    /// <summary>Longest MaxRange among the defences a TowerPlot can build, read from the prefabs.</summary>
    public static float MaxTowerRange()
    {
        float best = 0f;
        var plot = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPlotPrefabPath);
        if (plot == null) return 24f;
        TowerPlot tp = plot.GetComponent<TowerPlot>();
        if (tp == null) return 24f;
        SerializedProperty it = new SerializedObject(tp).GetIterator();
        while (it.NextVisible(true))
        {
            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (!(it.objectReferenceValue is GameObject go)) continue;
            var ds = go.GetComponentInChildren<DefenseStructure>(true);
            if (ds != null) best = Mathf.Max(best, ds.MaxRange);
        }
        return best > 0f ? best : 24f;
    }
}
