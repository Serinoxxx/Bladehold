using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
///     Global manager for spawning and pooling ragdoll blood decals.
///     Ensures the scene active decal count stays within <see cref="RagdollConfigSO.maxGlobalDecals"/>.
/// </summary>
public static class BloodDecalManager
{
    private static readonly List<BloodDecal> activeDecals = new List<BloodDecal>();
    private static readonly Queue<BloodDecal> decalPool = new Queue<BloodDecal>();
    private static Transform poolParent;

    public static void SpawnDecal(Vector3 point, Vector3 normal, float size, RagdollConfigSO config)
    {
        if (config == null || config.bloodDecalMaterials == null || config.bloodDecalMaterials.Length == 0)
        {
            return;
        }

        // Ensure parent container exists
        if (poolParent == null)
        {
            GameObject holder = GameObject.Find("BloodDecalsHolder");
            if (holder == null)
            {
                holder = new GameObject("BloodDecalsHolder");
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(holder);
                }
            }
            poolParent = holder.transform;
        }

        // Enforce max global cap
        while (activeDecals.Count >= config.maxGlobalDecals && activeDecals.Count > 0)
        {
            BloodDecal oldest = activeDecals[0];
            activeDecals.RemoveAt(0);
            if (oldest != null)
            {
                oldest.EvictEarly();
            }
        }

        BloodDecal bloodDecal = null;
        while (decalPool.Count > 0 && bloodDecal == null)
        {
            bloodDecal = decalPool.Dequeue();
        }

        GameObject decalGO;
        DecalProjector projector;
        if (bloodDecal == null)
        {
            decalGO = new GameObject("RagdollBloodDecal");
            decalGO.transform.SetParent(poolParent);
            projector = decalGO.AddComponent<DecalProjector>();
            bloodDecal = decalGO.AddComponent<BloodDecal>();
        }
        else
        {
            decalGO = bloodDecal.gameObject;
            decalGO.SetActive(true);
            projector = decalGO.GetComponent<DecalProjector>();
        }

        // Position offset above/outside hit surface along normal so the DecalProjector originates above the ground/wall
        float offset = config != null ? Mathf.Max(0.05f, config.decalOffsetFromSurface) : 0.3f;
        decalGO.transform.position = point + normal * offset;

        // Orient projector pointing into the surface (-normal) with random spin around normal
        Vector3 forward = -normal;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        Quaternion lookRotation = Quaternion.LookRotation(forward, up);
        float randomSpin = Random.Range(0f, 360f);
        decalGO.transform.rotation = lookRotation * Quaternion.Euler(0f, 0f, randomSpin);

        // Configure DecalProjector
        Material mat = config.bloodDecalMaterials[Random.Range(0, config.bloodDecalMaterials.Length)];
        projector.material = mat;
        projector.size = new Vector3(size, size, config.decalProjectionDepth);

        // Add component for lifecycle & fade out
        bloodDecal.Init(config.decalLifetime, config.decalFadeDuration);

        activeDecals.Add(bloodDecal);
    }

    public static void Unregister(BloodDecal decal)
    {
        if (decal != null)
        {
            activeDecals.Remove(decal);
            decal.gameObject.SetActive(false);
            if (!decalPool.Contains(decal))
            {
                decalPool.Enqueue(decal);
            }
        }
    }
}
