using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Scatters elemental nodes (<see cref="ElementNode" />) around the arena at the start of each
///     wave — the <see cref="ChestSpawner" /> idiom: weighted prefab pick, NavMesh-snapped positions,
///     minimum distance from the player, a pure spawner that never tracks its spawns. Class-conditional
///     by design: when the player has no enabled <see cref="MageImbuement" /> (any non-Mage class),
///     it disables itself in Start — that's the feature working, not an error (the
///     <see cref="RageBarUI" /> self-hide precedent), so other classes' arenas stay clean.
/// </summary>
public class ElementNodeSpawner : MonoBehaviour
{
    [Serializable]
    private class NodeEntry
    {
        [Tooltip("An ElementNode prefab (one element).")]
        public ElementNode prefab;
        [Tooltip("Relative weight for picking this element.")]
        [Min(0f)] public float weight = 1f;
    }

    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private NodeEntry[] nodePrefabs;

    [Header("How many per wave")]
    [SerializeField] private int minPerWave = 2;
    [SerializeField] private int maxPerWave = 4;

    [Header("Where")]
    [Tooltip("Optional explicit spawn points. If empty, nodes scatter around this object within Spawn Radius.")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnRadius = 12f;
    [Tooltip("Spawn positions are snapped to the nearest NavMesh point within this distance.")]
    [SerializeField] private float navMeshSampleDistance = 3f;
    [Tooltip("Nodes never spawn closer to the player than this.")]
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
        // Non-Mage run: the imbuement component exists on the player but is disabled by the class
        // controller — no nodes should litter the arena. Silent by design.
        MageImbuement imbuement = Player.Instance != null ? Player.Instance.GetComponentInChildren<MageImbuement>() : null;
        if (imbuement == null || !imbuement.isActiveAndEnabled)
        {
            enabled = false;
            return;
        }

        if (spawner == null)
        {
            Debug.LogError("ElementNodeSpawner has no WaveSpawner to listen to.");
            anyError = true;
        }
        if (nodePrefabs == null || nodePrefabs.Length == 0)
        {
            Debug.LogError("ElementNodeSpawner has no node prefabs assigned.");
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
            ElementNode prefab = SelectNode();
            if (prefab == null)
            {
                continue;
            }
            Instantiate(prefab, ResolveSpawnPosition(), Quaternion.identity);
        }
    }

    private ElementNode SelectNode()
    {
        float totalWeight = 0f;
        foreach (NodeEntry entry in nodePrefabs)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0f)
            {
                totalWeight += entry.weight;
            }
        }
        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        foreach (NodeEntry entry in nodePrefabs)
        {
            if (entry == null || entry.prefab == null || entry.weight <= 0f)
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
    ///     handful of NavMesh-snapped candidates; falls back to the farthest if every roll is too close
    ///     (the <see cref="ChestSpawner" /> placement idiom).
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
