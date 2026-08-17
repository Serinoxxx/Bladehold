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

        // Create decal GameObject
        GameObject decalGO = new GameObject("RagdollBloodDecal");
        decalGO.transform.SetParent(poolParent);
        
        // Position offset above/outside hit surface along normal so the DecalProjector originates above the ground/wall
        float offset = config != null ? Mathf.Max(0.05f, config.decalOffsetFromSurface) : 0.3f;
        decalGO.transform.position = point + normal * offset;

        // Orient projector pointing into the surface (-normal) with random spin around normal
        Quaternion lookRotation = Quaternion.LookRotation(-normal, Vector3.up);
        float randomSpin = Random.Range(0f, 360f);
        decalGO.transform.rotation = lookRotation * Quaternion.Euler(0f, 0f, randomSpin);

        // Configure DecalProjector
        DecalProjector projector = decalGO.AddComponent<DecalProjector>();
        Material mat = config.bloodDecalMaterials[Random.Range(0, config.bloodDecalMaterials.Length)];
        projector.material = mat;
        projector.size = new Vector3(size, size, config.decalProjectionDepth);


        // Add component for lifecycle & fade out
        BloodDecal bloodDecal = decalGO.AddComponent<BloodDecal>();
        bloodDecal.Init(config.decalLifetime, config.decalFadeDuration);

        activeDecals.Add(bloodDecal);
    }

    public static void Unregister(BloodDecal decal)
    {
        if (decal != null)
        {
            activeDecals.Remove(decal);
        }
    }
}
