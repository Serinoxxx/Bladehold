using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The single id → prefab mapping for the enemy roster: each entry pairs a roster CSV id
///     (see <see cref="EnemyRosterSO" />) with the enemy prefab that spawns for it. An asset rather
///     than a per-scene inspector list so <see cref="WaveSpawner" /> and the EnemyZoo gallery share
///     one source of truth, and so editor tooling (the enemy prefab generator) can register new
///     enemies by writing this asset instead of editing scenes.
/// </summary>
[CreateAssetMenu(fileName = "EnemyPrefabMap", menuName = "Scriptable Objects/EnemyPrefabMapSO")]
public class EnemyPrefabMapSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Must match an id in the roster CSV.")]
        public string id;
        [Tooltip("The enemy prefab. Must have a Health component (death is tracked via Health.OnDied).")]
        public GameObject prefab;
    }

    [Tooltip("Maps each roster CSV id to its prefab. Roster rows without a mapping here are skipped (with a warning) until one is added.")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>The prefab mapped to a roster id, or null when the id has no entry (callers warn —
    /// designers can author CSV rows ahead of the prefab arriving).</summary>
    public GameObject FindPrefab(string id)
    {
        foreach (Entry entry in entries)
        {
            if (entry != null && entry.id == id)
            {
                return entry.prefab;
            }
        }
        return null;
    }
}
