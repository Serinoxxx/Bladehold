using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Scatters loot chests around the arena at the start of each wave
///     (<see cref="WaveSpawner.WaveStarted" />). Each chest is picked from a weighted list of chest
///     prefabs (different "levels" = different models / health / loot tables), spawn points are snapped
///     to the NavMesh, and chests keep a minimum distance from the player — the
///     <see cref="WaveSpawner" /> placement idiom. Chests are then left to be smashed; a pure spawner
///     that never tracks or depends on chest destruction.
/// </summary>
public class ChestSpawner : MonoBehaviour
{
    [Serializable]
    private class ChestEntry
    {
        [Tooltip("A chest prefab (a given level/model).")]
        public GameObject prefab;
        [Tooltip("Relative weight for picking this chest level.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("First wave this chest level can appear.")]
        [Min(1)] public int unlockWave = 1;
    }

    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private ChestEntry[] chestPrefabs;

    [Header("How many per wave")]
    [SerializeField] private int minPerWave = 1;
    [SerializeField] private int maxPerWave = 3;

    [Header("Where")]
    [Tooltip("Optional explicit spawn points. If empty, chests scatter around this object within Spawn Radius.")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnRadius = 12f;
    [Tooltip("Spawn positions are snapped to the nearest NavMesh point within this distance.")]
    [SerializeField] private float navMeshSampleDistance = 3f;
    [Tooltip("Chests never spawn closer to the player than this.")]
    [SerializeField] private float minPlayerDistance = 5f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (spawner == null)
        {
            spawner = FindObjectOfType<WaveSpawner>();
        }
    }

    private void Start()
    {
        if (spawner == null)
        {
            Debug.LogError("ChestSpawner has no WaveSpawner to listen to.");
            anyError = true;
        }
        if (chestPrefabs == null || chestPrefabs.Length == 0)
        {
            Debug.LogError("ChestSpawner has no chest prefabs assigned.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        spawner.WaveStarted += HandleWaveStarted;
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.WaveStarted -= HandleWaveStarted;
        }
    }

    private void HandleWaveStarted(int wave)
    {
        int lo = Mathf.Min(minPerWave, maxPerWave);
        int hi = Mathf.Max(minPerWave, maxPerWave);
        int count = UnityEngine.Random.Range(lo, hi + 1);

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = SelectChest(wave);
            if (prefab == null)
            {
                continue;
            }
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            Instantiate(prefab, ResolveSpawnPosition(), rotation);
        }
    }

    private GameObject SelectChest(int wave)
    {
        float totalWeight = 0f;
        foreach (ChestEntry entry in chestPrefabs)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0f && wave >= entry.unlockWave)
            {
                totalWeight += entry.weight;
            }
        }
        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        foreach (ChestEntry entry in chestPrefabs)
        {
            if (entry == null || entry.prefab == null || entry.weight <= 0f || wave < entry.unlockWave)
            {
                continue;
            }
            roll -= entry.weight;
            if (roll <= 0f)
            {
                return entry.prefab;
            }
        }
        return null;
    }

    /// <summary>
    ///     Picks a spawn position at least <see cref="minPlayerDistance" /> from the player, re-rolling a
    ///     handful of NavMesh-snapped candidates; falls back to the farthest if every roll is too close.
    ///     Mirrors <see cref="WaveSpawner" />'s placement so chests land on walkable ground.
    /// </summary>
    private Vector3 ResolveSpawnPosition()
    {
        const int attempts = 8;
        Vector3 playerPos = Player.Instance != null ? Player.Instance.transform.position : transform.position;
        bool checkDistance = Player.Instance != null && minPlayerDistance > 0f;

        Vector3 best = transform.position;
        float bestDistance = -1f;
        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate = RollCandidate();
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                candidate = hit.position;
            }

            if (!checkDistance)
            {
                return candidate;
            }

            float distance = Vector3.Distance(candidate, playerPos);
            if (distance >= minPlayerDistance)
            {
                return candidate;
            }
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
        return best;
    }

    private Vector3 RollCandidate()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            return point != null ? point.position : transform.position;
        }
        Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(offset.x, 0f, offset.y);
    }
}
