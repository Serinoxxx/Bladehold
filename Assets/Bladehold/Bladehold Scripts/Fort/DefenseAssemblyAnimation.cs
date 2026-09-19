using System;
using System.Collections;
using UnityEngine;

/// <summary>
///     Animates the dramatic assembly sequence for a newly constructed battlefield defense.
///     Emits a holy light beam while 3 structural pieces fall sequentially from the sky,
///     slamming into place with wood impact sound effects and dust puffs before activating the structure.
/// </summary>
public class DefenseAssemblyAnimation : MonoBehaviour
{
    [Header("Assembly VFX & SFX")]
    [SerializeField] private GameObject holyLightPrefab;
    [SerializeField] private AudioClip impactWoodSfx;
    [SerializeField] private GameObject impactPuffPrefab;
    [SerializeField] private float dropHeight = 14f;
    [SerializeField] private float dropDuration = 0.35f;
    [SerializeField] private float staggerInterval = 0.22f;

    public void PlayAssembly(Vector3 plotPosition, Quaternion rotation, GameObject defensePrefab, Action<DefenseStructure> onComplete)
    {
        StartCoroutine(AssemblyRoutine(plotPosition, rotation, defensePrefab, onComplete));
    }

    private IEnumerator AssemblyRoutine(Vector3 plotPos, Quaternion rotation, GameObject defensePrefab, Action<DefenseStructure> onComplete)
    {
        // 1. Holy Light Beam
        GameObject beamObj = null;
        if (holyLightPrefab != null)
        {
            beamObj = Instantiate(holyLightPrefab, plotPos, Quaternion.identity);
        }

        // 2. Create 3 temporary falling visual pieces (Bottom/Base, Mid, Top)
        // If the prefab has child renderers, we can use proxy pieces or instantiate the prefab disabled and reveal layers
        GameObject[] proxies = new GameObject[3];
        Vector3[] targetOffsets = new Vector3[]
        {
            Vector3.zero,
            Vector3.up * 0.8f,
            Vector3.up * 1.6f
        };

        for (int i = 0; i < 3; i++)
        {
            proxies[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxies[i].name = $"Assembly_Piece_{i + 1}";
            proxies[i].transform.position = plotPos + targetOffsets[i] + Vector3.up * dropHeight;
            proxies[i].transform.rotation = rotation;
            
            // Adjust scale to resemble wooden timber blocks
            if (i == 0) proxies[i].transform.localScale = new Vector3(1.8f, 0.4f, 1.8f);
            else if (i == 1) proxies[i].transform.localScale = new Vector3(1.2f, 0.9f, 1.2f);
            else proxies[i].transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);

            // Disable colliders on proxies
            Collider c = proxies[i].GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        // 3. Drop pieces sequentially
        for (int i = 0; i < 3; i++)
        {
            GameObject piece = proxies[i];
            Vector3 startP = piece.transform.position;
            Vector3 endP = plotPos + targetOffsets[i];

            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                // Accelerating ease-in curve for heavy slamming gravity feel
                float curve = t * t;
                piece.transform.position = Vector3.Lerp(startP, endP, curve);
                yield return null;
            }

            piece.transform.position = endP;

            // Wood Slam Impact
            if (impactWoodSfx != null)
            {
                AudioSource.PlayClipAtPoint(impactWoodSfx, endP, 1.0f);
            }

            if (impactPuffPrefab != null)
            {
                Instantiate(impactPuffPrefab, endP, Quaternion.identity);
            }

            yield return new WaitForSeconds(staggerInterval);
        }

        // 4. Clean up temporary proxies
        for (int i = 0; i < 3; i++)
        {
            if (proxies[i] != null) Destroy(proxies[i]);
        }

        // 5. Instantiate final operational defense structure
        DefenseStructure structure = null;
        if (defensePrefab != null)
        {
            GameObject defObj = Instantiate(defensePrefab, plotPos, rotation);
            structure = defObj.GetComponent<DefenseStructure>();
        }

        // 6. Fade beam after brief settle
        yield return new WaitForSeconds(0.4f);
        if (beamObj != null)
        {
            Destroy(beamObj, 1.0f);
        }

        onComplete?.Invoke(structure);
        Destroy(gameObject);
    }
}
