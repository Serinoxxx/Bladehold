using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Lifts hand-composed prop groups out of a Synty demo scene so a <see cref="DefenseBiomePaletteSO" /> can
///     stamp them: the artist's arrangement (a skeleton among spiky rocks, a cactus with pebbles and scrub)
///     reads far better than props scattered one at a time. Prefab instances whose name starts with an
///     allowed prefix are linked when closer than <c>linkDistance</c>; each linked group of the right size
///     that contains at least one feature piece becomes a <see cref="PropCluster" />, with every piece's
///     height stored relative to the demo terrain under it so it can be re-grounded anywhere.
/// </summary>
public static class DefenseClusterHarvester
{
    private const string Tag = "[DefenseClusterHarvester]";

    public static List<PropCluster> Harvest(string scenePath, string[] allowPrefixes, string[] featurePrefixes,
        float linkDistance = 3.2f, int minPieces = 4, int maxPieces = 30, float maxRadius = 9f)
    {
        var result = new List<PropCluster>();
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            var prefabs = new List<GameObject>();
            var transforms = new List<Transform>();
            var terrains = new List<Terrain>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                terrains.AddRange(root.GetComponentsInChildren<Terrain>(true));
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                    GameObject src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (src == null || !StartsWithAny(src.name, allowPrefixes)) continue;
                    prefabs.Add(src);
                    transforms.Add(t);
                }
            }

            // Single-linkage grouping by XZ distance (union-find).
            int n = transforms.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                Vector3 a = transforms[i].position, b = transforms[j].position;
                if (new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude < linkDistance * linkDistance)
                    parent[Find(i)] = Find(j);
            }

            // Groups in first-appearance order so the harvest is deterministic.
            var order = new List<int>();
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int k = Find(i);
                if (!groups.TryGetValue(k, out List<int> g))
                {
                    groups[k] = g = new List<int>();
                    order.Add(k);
                }
                g.Add(i);
            }

            foreach (int k in order)
            {
                List<int> g = groups[k];
                if (g.Count < minPieces || g.Count > maxPieces) continue;

                string feature = null;
                Vector3 c = Vector3.zero;
                foreach (int i in g)
                {
                    c += transforms[i].position;
                    if (feature == null && StartsWithAny(prefabs[i].name, featurePrefixes)) feature = prefabs[i].name;
                }
                if (feature == null) continue;
                c /= g.Count;

                float radius = 0f;
                foreach (int i in g)
                {
                    Vector3 p = transforms[i].position;
                    radius = Mathf.Max(radius, new Vector2(p.x - c.x, p.z - c.z).magnitude);
                }
                if (radius > maxRadius) continue;

                var cluster = new PropCluster
                {
                    name = $"{scene.name}_{result.Count:00}_{feature.Replace("SM_Env_", "").Replace("SM_Prop_", "")}",
                    radius = radius
                };
                foreach (int i in g)
                {
                    Transform t = transforms[i];
                    Vector3 p = t.position;
                    // Pieces resting on another mesh (a mound, a rock) would float once re-grounded; clamp.
                    float above = Mathf.Clamp(p.y - GroundUnder(terrains, p), -1.5f, 0.15f);
                    cluster.pieces.Add(new ClusterPiece
                    {
                        prefab = prefabs[i],
                        offset = new Vector3(p.x - c.x, above, p.z - c.z),
                        rotation = t.rotation,
                        scale = t.lossyScale
                    });
                }
                result.Add(cluster);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log($"{Tag} Harvested {result.Count} clusters from {scenePath}.");
        return result;
    }

    private static bool StartsWithAny(string s, string[] prefixes)
    {
        foreach (string p in prefixes)
            if (s.StartsWith(p))
                return true;
        return false;
    }

    private static float GroundUnder(List<Terrain> terrains, Vector3 p)
    {
        foreach (Terrain t in terrains)
        {
            Vector3 o = t.transform.position, size = t.terrainData.size;
            if (p.x < o.x || p.z < o.z || p.x > o.x + size.x || p.z > o.z + size.z) continue;
            return t.SampleHeight(p) + o.y;
        }
        return p.y;
    }
}
