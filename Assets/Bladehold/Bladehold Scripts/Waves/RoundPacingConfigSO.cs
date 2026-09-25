using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Per-sector wave pacing for the 5-wave battle loop: kill quotas per wave, how they grow with the
///     campaign threat level (see <see cref="SectorThreat" />), the goblin fodder floor, and spawner caps.
///     Which enemy types spawn is decided by the roster CSV (<c>minThreat</c> + <c>unlockWave</c>), not here.
/// </summary>
[CreateAssetMenu(fileName = "RoundPacingConfigSO", menuName = "Scriptable Objects/RoundPacingConfigSO")]
public class RoundPacingConfigSO : ScriptableObject
{
    [System.Serializable]
    public class RoundDefinition
    {
        [Tooltip("Wave number within the sector (1-based).")]
        public int roundNumber = 1;
        [Tooltip("Kill quota for this wave at threat level 1. Scaled up by quotaGrowthPerThreat deeper into the campaign.")]
        public int requiredKillsPerWave = 15;
    }

    [Header("Waves")]
    [Tooltip("One entry per wave in a sector, indexed by wave number.")]
    public List<RoundDefinition> rounds = new List<RoundDefinition>
    {
        new RoundDefinition { roundNumber = 1, requiredKillsPerWave = 15 },
        new RoundDefinition { roundNumber = 2, requiredKillsPerWave = 20 },
        new RoundDefinition { roundNumber = 3, requiredKillsPerWave = 25 },
        new RoundDefinition { roundNumber = 4, requiredKillsPerWave = 30 },
        new RoundDefinition { roundNumber = 5, requiredKillsPerWave = 35 }
    };

    [Tooltip("Waves per sector before victory.")]
    public int wavesPerRound = 5;

    [Header("Threat Scaling")]
    [Tooltip("Kill quota growth per threat level above 1 (0.1 = +10% per level, so threat 5 is +40%).")]
    [Min(0f)] public float quotaGrowthPerThreat = 0.1f;

    [Tooltip("Roster id of the basic fodder enemy that always makes up the bulk of a wave.")]
    public string fodderEnemyId = "goblin";

    [Tooltip("Minimum share of each wave's spawns that are fodder. Elites fill the rest by weighted roll.")]
    [Range(0f, 1f)] public float fodderShare = 0.6f;

    [Header("Wave Pacing & Spawner Caps")]
    [Tooltip("Maximum enemies permitted alive simultaneously on the field.")]
    public int maxConcurrentEnemies = 20;

    [Tooltip("Telegraph indicator duration on the ground before an enemy spawns in seconds.")]
    public float spawnTelegraphDuration = 3.0f;

    [Header("Objective Trickle")]
    [Tooltip("Wagon/Ram objectives: once the kill quota has spawned, keep at least this many enemies alive until the objective resolves.")]
    [Min(0)] public int objectiveTrickleMinAlive = 6;

    [Tooltip("Seconds between trickle spawns while topping the field back up.")]
    [Min(0.1f)] public float objectiveTrickleInterval = 2f;

    [Header("Endgame Boss")]
    [Tooltip("The boss enemy roster id or prefab to spawn in Wave 5.")]
    public string bossEnemyId = "slayer";

    [Header("Spawn Indicator Visuals")]
    [Tooltip("Prefab spawned as the ground telegraph indicator.")]
    public GameObject indicatorPrefab;

    [Tooltip("Ground indicator scale/radius.")]
    public float indicatorRadius = 1.5f;

    public RoundDefinition GetRound(int roundNumber)
    {
        if (rounds == null || rounds.Count == 0) return null;
        int clamped = Mathf.Clamp(roundNumber, 1, rounds.Count);
        return rounds.Find(r => r.roundNumber == clamped) ?? rounds[0];
    }

    /// <summary>Kill quota for a wave at the given threat level.</summary>
    public int GetKillQuota(int waveNumber, int threat)
    {
        RoundDefinition round = GetRound(waveNumber);
        int baseQuota = round != null ? round.requiredKillsPerWave : 15 + (Mathf.Max(1, waveNumber) - 1) * 5;
        return SectorSpawnRules.ScaledQuota(baseQuota, threat, quotaGrowthPerThreat);
    }
}
