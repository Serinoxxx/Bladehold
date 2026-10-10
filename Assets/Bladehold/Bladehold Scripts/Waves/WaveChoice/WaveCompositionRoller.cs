using System;
using System.Collections.Generic;

/// <summary>
///     Rolls a wave's enemy composition (plan 22): which enemy types, and how many of each, a
///     <see cref="WaveCard" /> spawns. Pure logic over the parsed enemy roster
///     (<see cref="EnemyDefinition" />) and the same <see cref="SectorSpawnRules" /> gating the live
///     <see cref="SurvivorsSpawner" /> uses, with an injected <see cref="System.Random" /> so a benchmark
///     can check the mix over many seeded rolls.
///
///     The composition replaces the old threat-gated kill quota as the wave's spawn list: the card
///     chooses the fight. A fodder floor (<paramref name="fodderShare" />) guarantees the sector's basic
///     enemy still makes up most of the crowd; the remainder is a weighted draw (by each type's
///     <see cref="EnemyDefinition.spawnChance" />) over the other unlocked types.
/// </summary>
public static class WaveCompositionRoller
{
    public static List<WaveEnemyEntry> Roll(
        IReadOnlyList<EnemyDefinition> roster, string fodderId, float fodderShare,
        int wave, int threat, int totalEnemies, IReadOnlyList<string> allowedIds, System.Random rng)
    {
        List<WaveEnemyEntry> result = new List<WaveEnemyEntry>();
        if (roster == null || roster.Count == 0 || totalEnemies <= 0 || rng == null) return result;

        // Eligible types: a scene/objective allow-list (only enabled + unlockWave gate it), else threat gating.
        List<EnemyDefinition> eligible = new List<EnemyDefinition>();
        EnemyDefinition fodderDef = null;
        foreach (EnemyDefinition def in roster)
        {
            if (def == null) continue;
            if (fodderDef == null && !string.IsNullOrEmpty(fodderId) &&
                string.Equals(def.id, fodderId, StringComparison.OrdinalIgnoreCase))
            {
                fodderDef = def;
            }

            bool ok;
            if (allowedIds != null && allowedIds.Count > 0)
            {
                ok = def.enabled && def.unlockWave <= wave && Contains(allowedIds, def.id);
            }
            else
            {
                ok = SectorSpawnRules.IsUnlocked(def, wave, threat);
            }
            if (ok) eligible.Add(def);
        }

        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Fodder floor: the sector's basic enemy makes up at least fodderShare of the crowd.
        int fodderCount = 0;
        if (fodderDef != null)
        {
            fodderCount = (int)Math.Ceiling(Math.Max(0f, fodderShare) * totalEnemies - 1e-4);
            fodderCount = Math.Min(Math.Max(0, fodderCount), totalEnemies);
            if (fodderCount > 0) counts[fodderDef.id] = fodderCount;
        }

        int remaining = totalEnemies - fodderCount;

        // The remainder is the variety: a weighted draw over the eligible types minus the fodder type.
        List<EnemyDefinition> pool = new List<EnemyDefinition>();
        foreach (EnemyDefinition def in eligible)
        {
            if (fodderDef != null && string.Equals(def.id, fodderDef.id, StringComparison.OrdinalIgnoreCase)) continue;
            pool.Add(def);
        }

        if (remaining > 0)
        {
            if (pool.Count == 0)
            {
                // No variety unlocked: the rest is fodder, or the first roster row if there's no fodder either.
                EnemyDefinition fill = fodderDef ?? roster[0];
                if (fill != null) counts[fill.id] = counts.TryGetValue(fill.id, out int c) ? c + remaining : remaining;
            }
            else
            {
                List<float> weights = new List<float>(pool.Count);
                foreach (EnemyDefinition def in pool) weights.Add(def.spawnChance > 0f ? def.spawnChance : 0.0001f);
                for (int i = 0; i < remaining; i++)
                {
                    int idx = SectorSpawnRules.PickWeighted(weights, rng.NextDouble() * 0.999999);
                    if (idx < 0) idx = 0;
                    string id = pool[idx].id;
                    counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
                }
            }
        }

        foreach (KeyValuePair<string, int> kv in counts)
        {
            if (kv.Value > 0) result.Add(new WaveEnemyEntry(kv.Key, kv.Value));
        }
        return result;
    }

    private static bool Contains(IReadOnlyList<string> ids, string id)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
