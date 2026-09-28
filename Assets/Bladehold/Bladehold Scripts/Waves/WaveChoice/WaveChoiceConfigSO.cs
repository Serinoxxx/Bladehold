using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The bonus third line of a wave card's reward bundle.</summary>
public enum WaveBonusType
{
    None,
    DraftPick,
    GoblinBlood,
    OrcishMetal,
    TrollHeart
}

[Serializable]
public class WaveBonusOption
{
    public WaveBonusType type = WaveBonusType.GoblinBlood;

    [Min(0f)] public float weight = 1f;

    [Tooltip("Amount before stance/skull scaling: Blood or Metal count, Troll Heart max HP. Ignored for DraftPick (see draftPicks).")]
    [Min(0)] public int baseAmount = 2;
}

[Serializable]
public class WaveRewardBase
{
    [Min(0)] public int gold = 80;
    [Min(0)] public int supply = 40;
}

/// <summary>Skull odds for one drafted wave at one sector-threat band (the row with the highest minThreat ≤ threat wins).</summary>
[Serializable]
public class WaveSkullRollRow
{
    [Range(2, 4)] public int wave = 2;
    [Min(1)] public int minThreat = 1;
    [Min(0f)] public float oneSkull = 60f;
    [Min(0f)] public float twoSkulls = 35f;
    [Min(0f)] public float threeSkulls = 5f;
}

/// <summary>A wave that isn't drafted (wave 1's intro and wave 5's captain): its objective, skulls and reward.</summary>
[Serializable]
public class FixedWaveDefinition
{
    public string objectiveId = "kill_enemies";
    [Range(1, 3)] public int skulls = 1;
    public WaveBonusType bonus = WaveBonusType.None;
    [Tooltip("Rolls a clan modifier like a drafted card.")]
    public bool rollClan = true;
}

/// <summary>
///     Tunables for the between-wave choice (plan 15): reward bases and multipliers, skull scaling and
///     odds, the fixed first and last waves, and gate repair. Objective rows (stance, timer, fail rule,
///     weights, gating) live in <see cref="objectivesCsv" />. Loaded from <c>Resources/WaveChoiceConfig</c>.
/// </summary>
[CreateAssetMenu(fileName = "WaveChoiceConfig", menuName = "Scriptable Objects/Waves/Wave Choice Config")]
public class WaveChoiceConfigSO : ScriptableObject
{
    private const string ResourcePath = "WaveChoiceConfig";
    public const int MaxSkulls = 3;

    [Header("Data")]
    [Tooltip("Config/WaveObjectives.csv: one row per objective.")]
    public TextAsset objectivesCsv;

    [Tooltip("Clans a card can roll (the five War Banner clans).")]
    public List<WarBannerClanSO> clans = new List<WarBannerClanSO>();

    [Tooltip("Weight multiplier for the campaign node's own clan.")]
    [Min(1f)] public float nodeClanWeight = 2f;

    [Header("Draw")]
    [Min(1)] public int cardsPerDraw = 3;

    [Tooltip("Weight multiplier on the objective played last wave (0 = never repeat).")]
    [Range(0f, 1f)] public float lastObjectiveWeight = 0.25f;

    [Tooltip("Most Offence cards in one draw when the pool is small.")]
    [Min(1)] public int maxOffenceCards = 2;

    [Header("Rewards")]
    [Tooltip("Gold and supply before multipliers, indexed by wave (element 0 = wave 1).")]
    public WaveRewardBase[] rewardBaseByWave =
    {
        new WaveRewardBase(), new WaveRewardBase(), new WaveRewardBase(), new WaveRewardBase(), new WaveRewardBase()
    };

    public List<WaveBonusOption> bonusPool = new List<WaveBonusOption>
    {
        new WaveBonusOption { type = WaveBonusType.DraftPick, weight = 3f, baseAmount = 1 },
        new WaveBonusOption { type = WaveBonusType.GoblinBlood, weight = 2f, baseAmount = 2 },
        new WaveBonusOption { type = WaveBonusType.OrcishMetal, weight = 2f, baseAmount = 2 },
        new WaveBonusOption { type = WaveBonusType.TrollHeart, weight = 1f, baseAmount = 25 }
    };

    [Min(0f)] public float defenceRewardMultiplier = 1f;
    [Min(0f)] public float offenceRewardMultiplier = 1.5f;

    [Tooltip("Draft picks granted by a DraftPick bonus, per skull (element 0 = 1 skull).")]
    public int[] draftPicksBySkulls = { 1, 1, 2 };

    [Tooltip("Reward bonus in percent when the objective wasn't played last wave (0 = off).")]
    [Min(0f)] public float varietyBonusPercent = 10f;

    [Header("Skull Scaling (element 0 = 1 skull)")]
    public float[] rewardMultiplierBySkulls = { 1f, 1.5f, 2.25f };
    public float[] enemyHealthMultiplierBySkulls = { 1f, 1.15f, 1.3f };
    public float[] killQuotaMultiplierBySkulls = { 1f, 1.15f, 1.3f };
    [Tooltip("Scales the clan's base effect (e.g. Stone-Hide's 25% damage reduction).")]
    public float[] modifierMagnitudeBySkulls = { 0.6f, 1f, 1.4f };
    [Tooltip("A 3-skull card brings a clan captain.")]
    public bool captainOnThreeSkulls = true;

    [Header("Skull Odds")]
    public List<WaveSkullRollRow> skullRollTable = new List<WaveSkullRollRow>
    {
        new WaveSkullRollRow { wave = 2, minThreat = 1, oneSkull = 60f, twoSkulls = 35f, threeSkulls = 5f },
        new WaveSkullRollRow { wave = 2, minThreat = 3, oneSkull = 45f, twoSkulls = 40f, threeSkulls = 15f },
        new WaveSkullRollRow { wave = 2, minThreat = 5, oneSkull = 30f, twoSkulls = 45f, threeSkulls = 25f },
        new WaveSkullRollRow { wave = 3, minThreat = 1, oneSkull = 45f, twoSkulls = 40f, threeSkulls = 15f },
        new WaveSkullRollRow { wave = 3, minThreat = 3, oneSkull = 35f, twoSkulls = 45f, threeSkulls = 20f },
        new WaveSkullRollRow { wave = 3, minThreat = 5, oneSkull = 20f, twoSkulls = 45f, threeSkulls = 35f },
        new WaveSkullRollRow { wave = 4, minThreat = 1, oneSkull = 35f, twoSkulls = 45f, threeSkulls = 20f },
        new WaveSkullRollRow { wave = 4, minThreat = 3, oneSkull = 25f, twoSkulls = 45f, threeSkulls = 30f },
        new WaveSkullRollRow { wave = 4, minThreat = 5, oneSkull = 15f, twoSkulls = 45f, threeSkulls = 40f }
    };

    [Header("Fixed Waves")]
    public FixedWaveDefinition firstWave = new FixedWaveDefinition { objectiveId = "kill_enemies", skulls = 1 };
    public FixedWaveDefinition finalWave = new FixedWaveDefinition { objectiveId = "defeat_captain", skulls = 1, bonus = WaveBonusType.None, rollClan = false };
    [Tooltip("Waves from here to the final wave minus one are drafted.")]
    [Min(1)] public int firstDraftedWave = 2;
    [Min(2)] public int finalWaveNumber = 5;

    [Header("Resolution")]
    [Tooltip("Seconds stragglers spend fleeing after the objective resolves before they despawn.")]
    [Min(0f)] public float routDurationSeconds = 4f;

    [Header("Gate Repair (prep phase)")]
    [Min(0.01f)] public float supplyPerGateHp = 1f;
    [Min(1f)] public float gateRepairHpPerSecond = 20f;
    [Tooltip("Most gate HP repaired per prep phase (0 = unlimited).")]
    [Min(0f)] public float maxGateRepairPerPrep = 0f;

    [NonSerialized] private WaveObjectiveCatalog catalog;

    public WaveObjectiveCatalog Catalog
    {
        get
        {
            if (catalog == null)
            {
                catalog = WaveObjectiveCatalog.Parse(objectivesCsv != null ? objectivesCsv.text : null);
            }
            return catalog;
        }
    }

    public bool IsDraftedWave(int wave) => wave >= firstDraftedWave && wave < finalWaveNumber;

    public static float BySkulls(float[] table, int skulls)
    {
        if (table == null || table.Length == 0) return 1f;
        return table[Mathf.Clamp(skulls, 1, table.Length) - 1];
    }

    public static int BySkulls(int[] table, int skulls)
    {
        if (table == null || table.Length == 0) return 1;
        return table[Mathf.Clamp(skulls, 1, table.Length) - 1];
    }

    public WaveRewardBase RewardBaseForWave(int wave)
    {
        if (rewardBaseByWave == null || rewardBaseByWave.Length == 0) return new WaveRewardBase();
        return rewardBaseByWave[Mathf.Clamp(wave, 1, rewardBaseByWave.Length) - 1] ?? new WaveRewardBase();
    }

    public float StanceMultiplier(WaveStance stance) =>
        stance == WaveStance.Offence ? offenceRewardMultiplier : defenceRewardMultiplier;

    /// <summary>The odds row for this wave: the one with the highest minThreat not above <paramref name="threat" />.</summary>
    public WaveSkullRollRow SkullRowFor(int wave, int threat)
    {
        WaveSkullRollRow best = null;
        WaveSkullRollRow fallback = null;
        foreach (WaveSkullRollRow row in skullRollTable)
        {
            if (row == null || row.wave != wave) continue;
            if (fallback == null || row.minThreat < fallback.minThreat) fallback = row;
            if (row.minThreat <= threat && (best == null || row.minThreat > best.minThreat)) best = row;
        }
        return best ?? fallback;
    }

    private static WaveChoiceConfigSO cached;
    private static bool loaded;

    /// <summary>The config at <c>Resources/WaveChoiceConfig</c>, or null (logged once) if it's missing.</summary>
    public static WaveChoiceConfigSO Load()
    {
        if (!loaded)
        {
            loaded = true;
            cached = Resources.Load<WaveChoiceConfigSO>(ResourcePath);
            if (cached == null)
            {
                Debug.LogError($"[WaveChoiceConfigSO] No WaveChoiceConfig asset at Resources/{ResourcePath}.");
            }
        }
        return cached;
    }

    private void OnValidate()
    {
        catalog = null; // re-parse after CSV reassignment in the Inspector
    }

#if UNITY_EDITOR
    // Domain reload may be off: re-read the asset (and a re-imported CSV) on every Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        if (cached != null) cached.catalog = null;
        cached = null;
        loaded = false;
    }
#endif
}
