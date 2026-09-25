using System;
using System.Collections.Generic;

/// <summary>
///     The current sector's threat level: the campaign node's map tier (1-8). Outside a campaign run
///     (a battle scene opened directly) it's 1. The DevConsole can override it for testing.
/// </summary>
public static class SectorThreat
{
    /// <summary>DevConsole override; 0 = use the campaign node.</summary>
    public static int DebugOverride;

    public static int Current
    {
        get
        {
            if (DebugOverride > 0) return DebugOverride;
            CampaignManager campaign = CampaignManager.Instance;
            if (campaign != null && campaign.IsCampaignActive && campaign.CurrentNode != null)
            {
                return Math.Max(1, campaign.CurrentNode.tierIndex);
            }
            return 1;
        }
    }
}

/// <summary>
///     Pure spawn-selection rules shared by <see cref="SurvivorsSpawner" /> and the balance sim, so the
///     sim can't drift from the game. No scene or Unity state in here.
/// </summary>
public static class SectorSpawnRules
{
    /// <summary>Whether a roster row takes part in sector waves at all (<c>minThreat</c> 0 = objective/debug-only).</summary>
    public static bool IsInSectorPool(EnemyDefinition def)
    {
        return def != null && def.enabled && def.minThreat > 0;
    }

    /// <summary>Whether a roster row can spawn on this wave of a sector at this threat level.</summary>
    public static bool IsUnlocked(EnemyDefinition def, int wave, int threat)
    {
        return def != null && IsUnlocked(def.enabled, def.minThreat, def.unlockWave, wave, threat);
    }

    /// <summary><see cref="IsUnlocked(EnemyDefinition,int,int)" /> on raw fields (the balance sim's mutable roster copies).</summary>
    public static bool IsUnlocked(bool enabled, int minThreat, int unlockWave, int wave, int threat)
    {
        return enabled && minThreat > 0 && threat >= minThreat && wave >= unlockWave;
    }

    /// <summary>Shielder ids (bubblers) get the spawner's tighter concurrent cap. The spawner also checks the prefab for a BubbleShield.</summary>
    public static bool IsShielderId(string id)
    {
        return !string.IsNullOrEmpty(id)
            && (id.IndexOf("bubbler", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    ///     The fodder floor: true when the next spawn has to be fodder to keep at least
    ///     <paramref name="fodderShare" /> of this wave's spawns fodder. Otherwise the weighted roll
    ///     (which can also land on fodder) decides.
    /// </summary>
    public static bool MustSpawnFodder(int fodderSpawned, int totalSpawned, float fodderShare)
    {
        // Small epsilon so float error (0.6 * 5 = 3.0000001) doesn't demand an extra goblin.
        int required = (int)Math.Ceiling(fodderShare * (totalSpawned + 1) - 1e-4);
        return fodderSpawned < required;
    }

    /// <summary>Base kill quota grown by <paramref name="growthPerThreat" /> per threat level above 1.</summary>
    public static int ScaledQuota(int baseQuota, int threat, float growthPerThreat)
    {
        return (int)Math.Round(baseQuota * (1.0 + growthPerThreat * Math.Max(0, threat - 1)), MidpointRounding.AwayFromZero);
    }

    /// <summary>Index picked by a weighted roll, <paramref name="roll01" /> in [0,1). Uniform when all weights are 0.</summary>
    public static int PickWeighted(IReadOnlyList<float> weights, double roll01)
    {
        if (weights.Count == 0) return -1;
        double total = 0;
        foreach (float w in weights) total += Math.Max(0f, w);
        if (total <= 0) return Math.Min(weights.Count - 1, (int)(roll01 * weights.Count));

        double target = roll01 * total;
        double cumulative = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += Math.Max(0f, weights[i]);
            if (target < cumulative) return i;
        }
        return weights.Count - 1;
    }
}
