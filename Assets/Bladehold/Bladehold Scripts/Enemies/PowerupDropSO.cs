using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The shared rare-powerup drop table every enemy's <see cref="PowerupDropper" /> rolls on death.
///     One asset tunes drop rates for the whole roster; adding a new powerup (e.g. beyond the
///     <see cref="HealthPack" />) is just another entry, no code change. Each entry rolls
///     independently per kill, so two powerups can technically drop from one enemy.
/// </summary>
[CreateAssetMenu(fileName = "PowerupDropSO", menuName = "Scriptable Objects/PowerupDropSO")]
public class PowerupDropSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Powerup pickup prefab to spawn (e.g. the Health Pack).")]
        public GameObject prefab;
        [Tooltip("Per-kill chance (0-1) this powerup drops.")]
        [Range(0f, 1f)] public float chance = 0.03f;
    }

    [Tooltip("Each entry is rolled independently on every enemy death.")]
    public List<Entry> entries = new List<Entry>();
}
