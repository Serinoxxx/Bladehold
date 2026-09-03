using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Configurable pacing, enemy roster progression, and wave settings for the 4-round Survivors game loop.
/// </summary>
[CreateAssetMenu(fileName = "RoundPacingConfigSO", menuName = "Scriptable Objects/RoundPacingConfigSO")]
public class RoundPacingConfigSO : ScriptableObject
{
    [System.Serializable]
    public class RoundDefinition
    {
        public int roundNumber = 1;
        [Tooltip("Enemy type IDs permitted to spawn in this round.")]
        public string[] allowedEnemyIds = new string[] { "goblin", "goblin_brute" };
        [Tooltip("Required kills per wave for waves in this round.")]
        public int requiredKillsPerWave = 15;
    }

    [Header("Rounds & Enemy Roster Progression")]
    public List<RoundDefinition> rounds = new List<RoundDefinition>
    {
        new RoundDefinition { roundNumber = 1, allowedEnemyIds = new string[] { "goblin", "goblin_brute" }, requiredKillsPerWave = 15 },
        new RoundDefinition { roundNumber = 2, allowedEnemyIds = new string[] { "goblin", "goblin_brute", "big_ork" }, requiredKillsPerWave = 20 },
        new RoundDefinition { roundNumber = 3, allowedEnemyIds = new string[] { "goblin", "goblin_brute", "big_ork", "bubbler" }, requiredKillsPerWave = 25 },
        new RoundDefinition { roundNumber = 4, allowedEnemyIds = new string[] { "goblin", "goblin_brute", "big_ork", "bubbler", "bomber" }, requiredKillsPerWave = 30 }
    };

    [Header("Wave Pacing & Spawner Caps")]
    [Tooltip("Maximum enemies permitted alive simultaneously on the field.")]
    public int maxConcurrentEnemies = 20;

    [Tooltip("Telegraph indicator duration on the ground before an enemy spawns in seconds.")]
    public float spawnTelegraphDuration = 3.0f;

    [Tooltip("Delay in seconds between spawning individual enemies in a wave batch.")]
    public float spawnStaggerInterval = 0.35f;

    [Tooltip("Intermission countdown duration between non-rest waves in seconds.")]
    public float intermissionDuration = 30.0f;

    [Tooltip("Waves per round before a rest break occurs.")]
    public int wavesPerRound = 3;

    [Tooltip("Total rounds in a level to achieve victory.")]
    public int totalRounds = 4;

    [Header("Endgame Boss")]
    [Tooltip("The boss enemy roster id or prefab to spawn in Round 4.")]
    public string bossEnemyId = "slayer";

    [Tooltip("Wave number where the Slayer / Siegebreaker boss appears (default: Wave 10, start of Round 4).")]
    public int bossSpawnWave = 10;

    [Header("Spawn Indicator Visuals")]
    [Tooltip("Prefab spawned as the ground telegraph indicator.")]
    public GameObject indicatorPrefab;

    [Tooltip("Ground indicator scale/radius.")]
    public float indicatorRadius = 1.5f;

    [Header("End-of-Wave Drop Rewards")]
    [Tooltip("Drop weight for Troll Heart (+25 Max HP for run).")]
    public int weightTrollHeart = 15;

    [Tooltip("Drop weight for 1-2 Orcish Metal.")]
    public int weightOrcishMetal = 25;

    [Tooltip("Drop weight for 2-3 Goblin Blood.")]
    public int weightGoblinBlood = 30;

    [Tooltip("Drop weight for 50-100 Gold.")]
    public int weightGold = 20;

    [Tooltip("Drop weight for Instant Upgrade Draft.")]
    public int weightInstantDraft = 10;

    public RoundDefinition GetRound(int roundNumber)
    {
        if (rounds == null || rounds.Count == 0) return null;
        int clamped = Mathf.Clamp(roundNumber, 1, rounds.Count);
        return rounds.Find(r => r.roundNumber == clamped) ?? rounds[0];
    }
}
