using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Animates the assembly sequence for a newly constructed battlefield defense.
///     Displays a transparent blueprint ghost of the structure.
///     For multi-piece models, individual pieces drop sequentially from the sky with
///     wood impact sounds, dust puffs, and screenshakes.
///     For 1-2 piece models, the structure smoothly rises out of the ground with
///     rumbling tremors, ground dust, and a heavy lock-in slam.
/// </summary>
public class DefenseAssemblyAnimation : MonoBehaviour
{
    [Header("Assembly VFX & SFX")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private AudioClip impactWoodSfx;
    [SerializeField] private GameObject impactPuffPrefab;
    [SerializeField] private GameObject bigPuffPrefab;
    [SerializeField] private float dropHeight = 12f;
    [SerializeField] private float dropDuration = 0.32f;
    [SerializeField] private float staggerInterval = 0.16f;
    [SerializeField] private float riseDuration = 1.35f;

    private void Awake()
    {
        ResolveFallbacks();
    }

    private void ResolveFallbacks()
    {
#if UNITY_EDITOR
        if (ghostMaterial == null)
        {
            ghostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Bladehold/Materials/MAT_TowerGhost.mat");
        }
        if (impactWoodSfx == null)
        {
            impactWoodSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/HAMMER_Hit_Wood_Shield_stereo.wav");
            if (impactWoodSfx == null)
            {
                impactWoodSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/Generic Wood Item Break A.wav");
            }
        }
        if (impactPuffPrefab == null)
        {
            impactPuffPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Dust_Small_01.prefab");
            if (impactPuffPrefab == null)
            {
                impactPuffPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Impact_Wood_01.prefab");
            }
        }
        if (bigPuffPrefab == null)
        {
            bigPuffPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Dust_Big_01.prefab");
        }
#endif
    }

    public void PlayAssembly(Vector3 plotPosition, Quaternion rotation, GameObject defensePrefab, Action<DefenseStructure> onComplete)
    {
        ResolveFallbacks();
        StartCoroutine(AssemblyRoutine(plotPosition, rotation, defensePrefab, onComplete));
    }

    private IEnumerator AssemblyRoutine(Vector3 plotPos, Quaternion rotation, GameObject defensePrefab, Action<DefenseStructure> onComplete)
    {
        if (defensePrefab == null)
        {
            onComplete?.Invoke(null);
            Destroy(gameObject);
            yield break;
        }

        // 1. Instantiate final structure (initially inactive while assembly animates)
        GameObject finalStructureObj = Instantiate(defensePrefab, plotPos, rotation);
        finalStructureObj.SetActive(false);
        DefenseStructure structure = finalStructureObj.GetComponent<DefenseStructure>();

        // 2. Inspect mesh renderers on the prefab to determine assembly mode
        MeshRenderer[] renderers = defensePrefab.GetComponentsInChildren<MeshRenderer>(true);

        if (renderers.Length <= 2)
        {
            // --- Mode A: 1 or 2 pieces -> Rise out of the ground with tremor & dust ---
            yield return StartCoroutine(RiseFromGroundRoutine(finalStructureObj, plotPos, rotation));
        }
        else
        {
            // --- Mode B: > 2 pieces -> Drop pieces sequentially from the sky ---
            yield return StartCoroutine(DropPiecesRoutine(defensePrefab, plotPos, rotation));
        }

        // 3. Reveal & activate final operational structure
        finalStructureObj.SetActive(true);

        // Brief settle delay
        yield return new WaitForSeconds(0.15f);

        onComplete?.Invoke(structure);
        Destroy(gameObject);
    }

    private IEnumerator RiseFromGroundRoutine(GameObject structureObj, Vector3 targetPos, Quaternion rotation)
    {
        float riseDepth = 3.5f;
        Vector3 startPos = targetPos - Vector3.up * riseDepth;

        // Enable structure object but disable gameplay scripts while rising
        structureObj.transform.position = startPos;
        structureObj.transform.rotation = rotation;
        structureObj.SetActive(true);

        MonoBehaviour[] scripts = structureObj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var s in scripts)
        {
            s.enabled = false;
        }

        Collider[] cols = structureObj.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            c.enabled = false;
        }

        float elapsed = 0f;
        float dustTimer = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);

            // Smooth ease-out sine curve
            float curve = Mathf.Sin(t * Mathf.PI * 0.5f);

            // Tremor jitter diminishes as it nears the surface
            Vector3 tremor = new Vector3(
                Mathf.Sin(Time.time * 50f) * 0.035f,
                0f,
                Mathf.Cos(Time.time * 45f) * 0.035f
            ) * (1f - t);

            structureObj.transform.position = Vector3.Lerp(startPos, targetPos, curve) + tremor;

            // Emit periodic dust puffs at ground level
            dustTimer += Time.deltaTime;
            if (dustTimer >= 0.22f)
            {
                dustTimer = 0f;
                Vector3 dustPos = targetPos + new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 0.1f, UnityEngine.Random.Range(-0.8f, 0.8f));
                SpawnDust(dustPos, false);
            }

            yield return null;
        }

        structureObj.transform.position = targetPos;

        // Heavy ground slam into place
        PlayWoodImpact(targetPos);
        SpawnDust(targetPos, true);
        TriggerCameraShake(0.28f, 0.38f);

        // Re-enable colliders and scripts
        foreach (var c in cols)
        {
            if (c != null) c.enabled = true;
        }
        foreach (var s in scripts)
        {
            if (s != null) s.enabled = true;
        }
    }

    private IEnumerator DropPiecesRoutine(GameObject defensePrefab, Vector3 plotPos, Quaternion rotation)
    {
        // Collect piece definitions from prefab
        List<PieceData> pieces = CollectPieces(defensePrefab, plotPos, rotation);

        // Sort ascending by target Y position (base pieces first, top/arm/barrel pieces last)
        pieces.Sort((a, b) => a.targetWorldPos.y.CompareTo(b.targetWorldPos.y));

        List<GameObject> spawnedProxies = new List<GameObject>();

        for (int i = 0; i < pieces.Count; i++)
        {
            PieceData piece = pieces[i];
            GameObject proxy = new GameObject($"AssemblyProxy_{i}_{piece.name}");
            proxy.transform.rotation = piece.targetWorldRot;
            proxy.transform.localScale = piece.lossyScale;

            MeshFilter mf = proxy.AddComponent<MeshFilter>();
            mf.sharedMesh = piece.mesh;

            MeshRenderer mr = proxy.AddComponent<MeshRenderer>();
            mr.sharedMaterials = piece.materials;

            Vector3 startP = piece.targetWorldPos + Vector3.up * dropHeight;
            Vector3 endP = piece.targetWorldPos;
            proxy.transform.position = startP;
            spawnedProxies.Add(proxy);

            // Animate falling down with accelerating ease-in gravity curve
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float curve = t * t; // heavy gravity slam
                proxy.transform.position = Vector3.Lerp(startP, endP, curve);
                yield return null;
            }

            proxy.transform.position = endP;

            // Wood impact, dust puff, and screen shake on landing
            PlayWoodImpact(endP);
            SpawnDust(endP, false);
            TriggerCameraShake(0.14f, 0.20f);

            yield return new WaitForSeconds(staggerInterval);
        }

        // Clean up temporary proxies
        foreach (var p in spawnedProxies)
        {
            if (p != null) Destroy(p);
        }
    }

    private struct PieceData
    {
        public string name;
        public Mesh mesh;
        public Material[] materials;
        public Vector3 targetWorldPos;
        public Quaternion targetWorldRot;
        public Vector3 lossyScale;
    }

    private List<PieceData> CollectPieces(GameObject prefab, Vector3 plotPos, Quaternion rotation)
    {
        List<PieceData> pieces = new List<PieceData>();

        // Temporarily instantiate a sample to extract exact world transforms relative to plotPos and rotation
        GameObject sample = Instantiate(prefab, plotPos, rotation);
        sample.SetActive(false);

        MeshRenderer[] renderers = sample.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                pieces.Add(new PieceData
                {
                    name = r.gameObject.name,
                    mesh = mf.sharedMesh,
                    materials = r.sharedMaterials,
                    targetWorldPos = r.transform.position,
                    targetWorldRot = r.transform.rotation,
                    lossyScale = r.transform.lossyScale
                });
            }
        }

        Destroy(sample);
        return pieces;
    }

    private GameObject CreateGhost(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        GameObject ghost = Instantiate(prefab, pos, rot);
        ghost.name = $"{prefab.name}_GhostPreview";

        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Destroy(mb);
        }
        foreach (var col in ghost.GetComponentsInChildren<Collider>(true))
        {
            Destroy(col);
        }
        foreach (var rb in ghost.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(rb);
        }

        if (ghostMaterial != null)
        {
            foreach (var r in ghost.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] ghostMats = new Material[r.sharedMaterials.Length];
                for (int m = 0; m < ghostMats.Length; m++)
                {
                    ghostMats[m] = ghostMaterial;
                }
                r.sharedMaterials = ghostMats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        return ghost;
    }

    private void PlayWoodImpact(Vector3 pos)
    {
        if (impactWoodSfx != null)
        {
            AudioSource.PlayClipAtPoint(impactWoodSfx, pos, 0.95f);
        }
    }

    private void SpawnDust(Vector3 pos, bool isBig)
    {
        GameObject prefab = (isBig && bigPuffPrefab != null) ? bigPuffPrefab : impactPuffPrefab;
        if (prefab != null)
        {
            GameObject dust = Instantiate(prefab, pos, Quaternion.identity);
            Destroy(dust, 2.5f);
        }
    }

    private void TriggerCameraShake(float duration, float amplitude)
    {
        MMCameraShakeEvent.Trigger(duration, amplitude, 35f, amplitude * 0.7f, amplitude * 0.7f, amplitude * 0.7f);
    }
}
