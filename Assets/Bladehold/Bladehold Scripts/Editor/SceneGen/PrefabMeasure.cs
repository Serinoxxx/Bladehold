using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Measures prefabs from their meshes so placement is computed, not eyeballed: local bounds relative
///     to the prefab root (Synty pivots are often at a corner or the base, not the centre), rotated
///     footprints, and the ground height a prefab must sit at on uneven terrain.
/// </summary>
public static class PrefabMeasure
{
    private static readonly Dictionary<GameObject, Bounds> cache = new Dictionary<GameObject, Bounds>();

    public static void ClearCache() => cache.Clear();

    /// <summary>Mesh bounds of the prefab in its root's local space, at scale 1.</summary>
    public static Bounds LocalBounds(GameObject prefab)
    {
        if (cache.TryGetValue(prefab, out Bounds cached)) return cached;

        Matrix4x4 rootInv = prefab.transform.worldToLocalMatrix;
        bool has = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<MeshRenderer>() == null) continue;
            Encapsulate(ref b, ref has, mf.sharedMesh.bounds, rootInv * mf.transform.localToWorldMatrix);
        }
        foreach (SkinnedMeshRenderer smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            Encapsulate(ref b, ref has, smr.sharedMesh.bounds, rootInv * smr.transform.localToWorldMatrix);
        }
        if (!has) Debug.LogWarning($"[PrefabMeasure] {prefab.name} has no meshes; using a zero-size bounds.");
        cache[prefab] = b;
        return b;
    }

    private static void Encapsulate(ref Bounds b, ref bool has, Bounds mb, Matrix4x4 m)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = m.MultiplyPoint3x4(corner);
            if (!has)
            {
                b = new Bounds(p, Vector3.zero);
                has = true;
            }
            else b.Encapsulate(p);
        }
    }

    /// <summary>World XZ of the four footprint corners for a prefab at pos/yaw/scale.</summary>
    public static Vector2[] FootprintCorners(GameObject prefab, Vector3 pos, float yaw, float scale, float shrink = 1f)
    {
        Bounds b = LocalBounds(prefab);
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        var corners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 local = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x, 0f,
                (i & 2) == 0 ? -b.extents.z : b.extents.z) * shrink;
            Vector3 w = pos + r * (local * scale);
            corners[i] = new Vector2(w.x, w.z);
        }
        return corners;
    }

    /// <summary>Radius of a circle roughly covering the footprint (mean of the half extents).</summary>
    public static float FootprintRadius(GameObject prefab, float scale)
    {
        Bounds b = LocalBounds(prefab);
        return (b.extents.x + b.extents.z) * 0.5f * scale;
    }

    /// <summary>
    ///     Root position that puts the prefab's rotated bounds centre over <paramref name="centreXZ" />.
    ///     Needed for corner-pivot pieces (walls, bridge tiles) so layouts can be written in centres.
    /// </summary>
    public static Vector3 RootForCentre(GameObject prefab, Vector2 centreXZ, float yaw, float scale)
    {
        Bounds b = LocalBounds(prefab);
        Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * (b.center * scale);
        return new Vector3(centreXZ.x - offset.x, 0f, centreXZ.y - offset.z);
    }

    /// <summary>Bottom of the mesh relative to the root at the given scale (negative when the mesh dips below the pivot).</summary>
    public static float BaseOffset(GameObject prefab, float scale) => LocalBounds(prefab).min.y * scale;

    public static float Height(GameObject prefab, float scale) => LocalBounds(prefab).size.y * scale;

    public static GameObject Instantiate(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot, float scale)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }
}
