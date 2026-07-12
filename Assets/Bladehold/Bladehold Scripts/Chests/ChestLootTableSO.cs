using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A chest's loot table: it always drops gold (a <see cref="Coin" /> worth a rolled amount) and,
///     on a successful bonus roll, one extra item chosen by weight from a roster of existing pickup
///     prefabs (Health Pack, Lightning Orb, Impulse Orb, a gold bag, …). One asset per chest "tier",
///     so different chest levels drop from different tables. Prefab-agnostic — the chest just
///     Instantiates whatever's listed and each pickup grants itself on trigger-enter as usual.
/// </summary>
[CreateAssetMenu(fileName = "ChestLootTableSO", menuName = "Scriptable Objects/ChestLootTableSO")]
public class ChestLootTableSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Pickup prefab to drop (e.g. HealthPack, LightningOrb, ImpulseOrb, a Coin gold-bag).")]
        public GameObject prefab;
        [Tooltip("Relative weight for the single bonus-item pick. Higher = more likely.")]
        [Min(0f)] public float weight = 1f;
    }

    [Header("Guaranteed gold")]
    [Tooltip("A chest always drops a Coin worth a random amount in this range (inclusive).")]
    [Min(0)] public int minGold = 10;
    [Min(0)] public int maxGold = 25;

    [Header("Bonus item")]
    [Tooltip("Chance (0-1) the chest also drops one bonus item from the weighted roster below.")]
    [Range(0f, 1f)] public float bonusItemChance = 0.6f;
    [Tooltip("The weighted roster the single bonus item is picked from.")]
    public List<Entry> items = new List<Entry>();

    /// <summary>Rolls the gold amount for the guaranteed coin drop.</summary>
    public int RollGold()
    {
        int lo = Mathf.Min(minGold, maxGold);
        int hi = Mathf.Max(minGold, maxGold);
        return UnityEngine.Random.Range(lo, hi + 1);
    }

    /// <summary>
    ///     Rolls the bonus-item slot: null if the bonus roll fails or the roster is empty/zero-weight,
    ///     else one prefab chosen by weight.
    /// </summary>
    public GameObject RollBonusItem()
    {
        if (items == null || items.Count == 0 || UnityEngine.Random.value >= bonusItemChance)
        {
            return null;
        }

        float totalWeight = 0f;
        foreach (Entry entry in items)
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
        foreach (Entry entry in items)
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
}
