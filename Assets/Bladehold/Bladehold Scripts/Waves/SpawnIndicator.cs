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
            // The enemy still spawns on time, just without a telegraph.
            Debug.LogError("[SpawnIndicator] No indicator prefab: assign RoundPacingConfigSO.indicatorPrefab (or SurvivorsSpawner.spawnIndicatorPrefab).");
        }

        StartCoroutine(IndicatorRoutine());
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
