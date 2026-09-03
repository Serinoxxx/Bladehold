using System;
using System.Collections;
using UnityEngine;

/// <summary>
///     Ground telegraph indicator spawned 3 seconds prior to an enemy appearing.
///     Provides juicy visual warning (pulsing red circle / ground marker), then triggers
///     the enemy instantiation callback and cleans itself up.
/// </summary>
public class SpawnIndicator : MonoBehaviour
{
    private Action onSpawnCallback;
    private float duration = 3.0f;
    private GameObject visualInstance;

    public static SpawnIndicator Create(Vector3 position, float duration, GameObject prefab, Action onSpawn)
    {
        GameObject go = new GameObject("SpawnIndicator");
        go.transform.position = position;
        SpawnIndicator indicator = go.AddComponent<SpawnIndicator>();
        indicator.Initialize(duration, prefab, onSpawn);
        return indicator;
    }

    public void Initialize(float durationSeconds, GameObject customPrefab, Action onSpawn)
    {
        duration = Mathf.Max(0.5f, durationSeconds);
        onSpawnCallback = onSpawn;

        if (customPrefab != null)
        {
            visualInstance = Instantiate(customPrefab, transform.position, Quaternion.identity, transform);
        }
        else
        {
            CreateFallbackVisual();
        }

        StartCoroutine(IndicatorRoutine());
    }

    private void CreateFallbackVisual()
    {
        // Fallback: simple flat red ring
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sphere.name = "Telegraph_Visual";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        sphere.transform.localScale = new Vector3(2.5f, 0.02f, 2.5f);

        Collider col = sphere.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = sphere.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            rend.material.color = new Color(1f, 0.15f, 0.15f, 0.6f);
        }

        visualInstance = sphere;
    }

    private IEnumerator IndicatorRoutine()
    {
        float elapsed = 0f;
        Vector3 baseScale = visualInstance != null ? visualInstance.transform.localScale : Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Pulse effect
            if (visualInstance != null)
            {
                float pulse = 1.0f + Mathf.Sin(t * Mathf.PI * 4f) * 0.12f;
                visualInstance.transform.localScale = baseScale * pulse;
            }

            yield return null;
        }

        try
        {
            onSpawnCallback?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SpawnIndicator] Error during spawn callback: {ex}");
        }

        Destroy(gameObject);
    }
}
