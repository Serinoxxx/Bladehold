using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Animates the assembly sequence for a newly constructed battlefield defense.
///     For multi-piece models, individual pieces drop sequentially from the sky with
///     a landing feedback each (wood impact, dust puff, screenshake).
///     For 1-2 piece models, the structure smoothly rises out of the ground with
///     rumbling tremors, ground dust, and a heavy lock-in slam.
///     Spawned by <see cref="TowerPlot"/> from an authored prefab; all audio/VFX/shake live in its MMF players.
/// </summary>
public class DefenseAssemblyAnimation : MonoBehaviour
{
    [Header("Feedbacks (MMF)")]
    [Tooltip("Played at each dropped piece's landing point: wood impact, small dust puff, light screenshake.")]
    [SerializeField] private MMF_Player pieceLandFeedback;
    [Tooltip("Played repeatedly at ground level while a 1-2 piece structure rises: small dust puff.")]
    [SerializeField] private MMF_Player riseDustFeedback;
    [Tooltip("Played when a rising structure locks into place: heavy impact, big dust, strong screenshake.")]
    [SerializeField] private MMF_Player slamFeedback;

    [Header("Timing")]
    [SerializeField] private float dropHeight = 12f;
    [SerializeField] private float dropDuration = 0.32f;
    [SerializeField] private float staggerInterval = 0.16f;
    [SerializeField] private float riseDuration = 1.35f;

    private void Start()
    {
        if (pieceLandFeedback == null) Debug.LogError($"{name}: DefenseAssemblyAnimation.pieceLandFeedback is not assigned.", this);
        if (riseDustFeedback == null) Debug.LogError($"{name}: DefenseAssemblyAnimation.riseDustFeedback is not assigned.", this);
        if (slamFeedback == null) Debug.LogError($"{name}: DefenseAssemblyAnimation.slamFeedback is not assigned.", this);
    }

    public void PlayAssembly(Vector3 plotPosition, Quaternion rotation, GameObject defensePrefab, Action<DefenseStructure> onComplete)
    {
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
                PlayAt(riseDustFeedback, dustPos);
            }

            yield return null;
        }

        structureObj.transform.position = targetPos;

        // Heavy ground slam into place
        PlayAt(slamFeedback, targetPos);

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
            PlayAt(pieceLandFeedback, endP);

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

    private static void PlayAt(MMF_Player feedback, Vector3 position)
    {
        if (feedback != null)
        {
            feedback.PlayFeedbacks(position);
        }
    }
}
