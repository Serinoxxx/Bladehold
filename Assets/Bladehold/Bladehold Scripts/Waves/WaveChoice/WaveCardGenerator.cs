using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the generator needs to know about the moment a draw happens.</summary>
public struct WaveCardRollContext
{
    public int wave;
    public int threat;
    public string lastObjectiveId;
    /// <summary>The campaign node's clan; weighted up by <see cref="WaveChoiceConfigSO.nodeClanWeight" />.</summary>
    public WarBannerClanSO nodeClan;
    /// <summary>Shown on 3-skull cards; blank = a generic "clan captain" line.</summary>
    public string captainName;
    public bool isDemo;

    // Enemy composition (plan 22): the roster the card's enemy list is rolled from, and the gating inputs.
    /// <summary>The parsed enemy roster (<see cref="EnemyRosterSO.Enemies" />); null disables composition.</summary>
    public System.Collections.Generic.IReadOnlyList<EnemyDefinition> roster;
    /// <summary>The sector's fodder enemy id (scene override, else the pacing asset's).</summary>
    public string fodderId;
    /// <summary>Fraction of the crowd guaranteed to be fodder (the pacing asset's <c>fodderShare</c>).</summary>
    public float fodderShare;
    /// <summary>The wave's base kill quota (pacing × threat) before the card's skull multiplier; the composition's size.</summary>
    public int baseKillQuota;
    /// <summary>A scene's <see cref="SceneEnemyRoster" /> allow-list (null = threat gating decides eligibility).</summary>
    public System.Collections.Generic.IReadOnlyList<string> allowedEnemyIds;
}

/// <summary>
///     Rolls the between-wave draw (plan 15). Pure logic over <see cref="WaveChoiceConfigSO" /> and its
///     objective catalog with an injected <see cref="System.Random" />, so the benchmark can check the mix
///     rules over thousands of seeded rolls.
///     Rules: at least one Defence and one Offence card (when both pools have a row), no duplicate
///     objective in a draw, at most <see cref="WaveChoiceConfigSO.maxOffenceCards" /> Offence cards, the last
///     wave's objective down-weighted, and rows gated by minWave / minThreat / draftable / demoEnabled.
/// </summary>
public static class WaveCardGenerator
{
    public static List<WaveCard> Roll(WaveChoiceConfigSO config, WaveCardRollContext ctx, System.Random rng)
    {
        List<WaveCard> cards = new List<WaveCard>();
        if (config == null || rng == null) return cards;

        List<WaveObjectiveDefinition> pool = EligibleObjectives(config, ctx);
        List<WaveObjectiveDefinition> picked = new List<WaveObjectiveDefinition>();
        int target = Mathf.Max(1, config.cardsPerDraw);

        // Guarantee one of each stance first, then fill from whatever's left.
        TryPick(pool, picked, ctx, config, rng, WaveStance.Defence);
        if (picked.Count < target) TryPick(pool, picked, ctx, config, rng, WaveStance.Offence);
        while (picked.Count < target)
        {
            int offence = picked.FindAll(o => o.stance == WaveStance.Offence).Count;
            WaveStance? only = offence >= config.maxOffenceCards ? WaveStance.Defence : (WaveStance?)null;
            if (!TryPick(pool, picked, ctx, config, rng, only)) break;
        }

        Shuffle(picked, rng);

        List<WarBannerClanSO> usedClans = new List<WarBannerClanSO>();
        foreach (WaveObjectiveDefinition objective in picked)
        {
            int skulls = RollSkulls(config, ctx.wave, ctx.threat, rng);
            WarBannerClanSO clan = RollClan(config, ctx.nodeClan, usedClans, rng);
            if (clan != null) usedClans.Add(clan);
            cards.Add(Build(config, objective, skulls, clan, WaveBonusType.None, rollBonus: true, ctx, rng));
        }
        return cards;
    }

    /// <summary>The fixed card for a wave that isn't drafted (wave 1's intro, wave 5's captain).</summary>
    public static WaveCard BuildFixed(WaveChoiceConfigSO config, FixedWaveDefinition fixedWave, WaveCardRollContext ctx, System.Random rng)
    {
        if (config == null || fixedWave == null) return null;
        WaveObjectiveDefinition objective = config.Catalog.Get(fixedWave.objectiveId);
        if (objective == null)
        {
            Debug.LogError($"[WaveCardGenerator] Fixed wave {ctx.wave} objective '{fixedWave.objectiveId}' has no WaveObjectives.csv row.");
            return null;
        }
        WarBannerClanSO clan = fixedWave.rollClan ? RollClan(config, ctx.nodeClan, null, rng) : null;
        return Build(config, objective, Mathf.Clamp(fixedWave.skulls, 1, WaveChoiceConfigSO.MaxSkulls), clan, fixedWave.bonus, rollBonus: false, ctx, rng);
    }

    /// <summary>
    ///     Resolves every number on a card from the config. Public so DevConsole can force a card
    ///     (<c>wavecard &lt;objectiveId&gt; &lt;skulls&gt;</c>).
    /// </summary>
    public static WaveCard Build(WaveChoiceConfigSO config, WaveObjectiveDefinition objective, int skulls, WarBannerClanSO clan,
        WaveBonusType bonus, bool rollBonus, WaveCardRollContext ctx, System.Random rng)
    {
        skulls = Mathf.Clamp(skulls, 1, WaveChoiceConfigSO.MaxSkulls);
        WaveStance stance = objective.stance == WaveStance.None ? WaveStance.Defence : objective.stance;

        WaveCard card = new WaveCard
        {
            wave = ctx.wave,
            objective = objective,
            stance = objective.stance,
            skulls = skulls,
            clan = clan,
            modifierScale = WaveChoiceConfigSO.BySkulls(config.modifierMagnitudeBySkulls, skulls),
            enemyHealthMultiplier = WaveChoiceConfigSO.BySkulls(config.enemyHealthMultiplierBySkulls, skulls),
            killQuotaMultiplier = WaveChoiceConfigSO.BySkulls(config.killQuotaMultiplierBySkulls, skulls),
            draftRerolls = Mathf.Max(0, WaveChoiceConfigSO.BySkulls(config.draftRerollsBySkulls, skulls)),
            hasCaptain = config.captainOnThreeSkulls && skulls >= 3,
            captainName = RollCaptainName(config, objective, skulls, ctx, rng),
            isFresh = config.varietyBonusPercent > 0f && !string.IsNullOrEmpty(ctx.lastObjectiveId) &&
                      !string.Equals(ctx.lastObjectiveId, objective.id, StringComparison.OrdinalIgnoreCase)
        };

        if (clan != null)
        {
            card.modifierMagnitude = ClanModifierMath.Scale(clan.buffType, clan.buffMagnitude, card.modifierScale);
        }

        float multiplier = config.StanceMultiplier(stance) * WaveChoiceConfigSO.BySkulls(config.rewardMultiplierBySkulls, skulls);
        if (card.isFresh) multiplier *= 1f + config.varietyBonusPercent / 100f;
        card.rewardMultiplier = multiplier;

        WaveRewardBase rewardBase = config.RewardBaseForWave(ctx.wave);
        card.gold = Mathf.RoundToInt(rewardBase.gold * multiplier);
        card.supply = Mathf.RoundToInt(rewardBase.supply * multiplier);

        WaveBonusOption option = rollBonus ? RollBonus(config, rng) : FindBonus(config, bonus);
        card.bonusType = option != null ? option.type : WaveBonusType.None;
        card.bonusAmount = BonusAmount(config, option, skulls, multiplier);

        // Enemy composition (plan 22): the card chooses who you fight, rolled from the roster's threat/wave
        // gating and scaled by the card's skull quota multiplier. The captain wave keeps its objective-driven
        // spawns (empty composition), as do dev scenes / scenes with no roster passed in.
        bool suppressComposition = objective != null &&
            string.Equals(objective.id, DefeatCaptainObjective.Id, StringComparison.OrdinalIgnoreCase);
        if (!suppressComposition && ctx.roster != null && ctx.roster.Count > 0 && ctx.baseKillQuota > 0)
        {
            int total = Mathf.Max(1, Mathf.RoundToInt(ctx.baseKillQuota * card.killQuotaMultiplier));
            card.composition = WaveCompositionRoller.Roll(
                ctx.roster, ctx.fodderId, ctx.fodderShare, ctx.wave, ctx.threat, total, ctx.allowedEnemyIds, rng);
        }
        return card;
    }

    /// <summary>
    ///     The captain a card names: the node's, or (at <see cref="WaveChoiceConfigSO.wanderingCaptainChance" />)
    ///     the wandering captain, on any card that brings one (3 skulls, or the Captain Assault). Rolled here so
    ///     the card and the spawn can't disagree.
    /// </summary>
    public static string RollCaptainName(WaveChoiceConfigSO config, WaveObjectiveDefinition objective, int skulls, WaveCardRollContext ctx, System.Random rng)
    {
        bool bringsCaptain = (config.captainOnThreeSkulls && skulls >= 3) ||
                             (objective != null && string.Equals(objective.id, DefeatCaptainObjective.Id, StringComparison.OrdinalIgnoreCase));
        if (!bringsCaptain || rng == null || string.IsNullOrEmpty(config.wanderingCaptainName) || config.wanderingCaptainChance <= 0f)
        {
            return ctx.captainName;
        }
        return rng.NextDouble() < config.wanderingCaptainChance ? config.wanderingCaptainName : ctx.captainName;
    }

    public static List<WaveObjectiveDefinition> EligibleObjectives(WaveChoiceConfigSO config, WaveCardRollContext ctx)
    {
        List<WaveObjectiveDefinition> pool = new List<WaveObjectiveDefinition>();
        foreach (WaveObjectiveDefinition def in config.Catalog.All)
        {
            if (!def.draftable || def.stance == WaveStance.None || def.weight <= 0f) continue;
            if (ctx.wave < def.minWave || ctx.threat < def.minThreat) continue;
            if (ctx.isDemo && !def.demoEnabled) continue;
            pool.Add(def);
        }
        return pool;
    }

    public static int RollSkulls(WaveChoiceConfigSO config, int wave, int threat, System.Random rng)
    {
        WaveSkullRollRow row = config.SkullRowFor(wave, threat);
        if (row == null) return 1;
        float total = row.oneSkull + row.twoSkulls + row.threeSkulls;
        if (total <= 0f) return 1;
        double roll = rng.NextDouble() * total;
        if (roll < row.oneSkull) return 1;
        if (roll < row.oneSkull + row.twoSkulls) return 2;
        return 3;
    }

    private static bool TryPick(List<WaveObjectiveDefinition> pool, List<WaveObjectiveDefinition> picked,
        WaveCardRollContext ctx, WaveChoiceConfigSO config, System.Random rng, WaveStance? stance)
    {
        float total = 0f;
        foreach (WaveObjectiveDefinition def in pool)
        {
            if (IsCandidate(def, picked, stance)) total += ObjectiveWeight(def, ctx, config);
        }
        if (total <= 0f) return false;

        double roll = rng.NextDouble() * total;
        foreach (WaveObjectiveDefinition def in pool)
        {
            if (!IsCandidate(def, picked, stance)) continue;
            roll -= ObjectiveWeight(def, ctx, config);
            if (roll <= 0d)
            {
                picked.Add(def);
                return true;
            }
        }
        // Float rounding: take the last candidate.
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (IsCandidate(pool[i], picked, stance) && ObjectiveWeight(pool[i], ctx, config) > 0f)
            {
                picked.Add(pool[i]);
                return true;
            }
        }
        return false;
    }

    private static bool IsCandidate(WaveObjectiveDefinition def, List<WaveObjectiveDefinition> picked, WaveStance? stance) =>
        !picked.Contains(def) && (!stance.HasValue || def.stance == stance.Value);

    private static float ObjectiveWeight(WaveObjectiveDefinition def, WaveCardRollContext ctx, WaveChoiceConfigSO config)
    {
        bool isLast = !string.IsNullOrEmpty(ctx.lastObjectiveId) &&
                      string.Equals(def.id, ctx.lastObjectiveId, StringComparison.OrdinalIgnoreCase);
        return isLast ? def.weight * config.lastObjectiveWeight : def.weight;
    }

    private static WarBannerClanSO RollClan(WaveChoiceConfigSO config, WarBannerClanSO nodeClan, List<WarBannerClanSO> avoid, System.Random rng)
    {
        List<WarBannerClanSO> candidates = new List<WarBannerClanSO>();
        foreach (WarBannerClanSO clan in config.clans)
        {
            if (clan != null && (avoid == null || !avoid.Contains(clan))) candidates.Add(clan);
        }
        if (candidates.Count == 0)
        {
            foreach (WarBannerClanSO clan in config.clans)
            {
                if (clan != null) candidates.Add(clan);
            }
        }
        if (candidates.Count == 0) return null;

        float Weight(WarBannerClanSO c) => c == nodeClan ? config.nodeClanWeight : 1f;
        float total = 0f;
        foreach (WarBannerClanSO c in candidates) total += Weight(c);
        double roll = rng.NextDouble() * total;
        foreach (WarBannerClanSO c in candidates)
        {
            roll -= Weight(c);
            if (roll <= 0d) return c;
        }
        return candidates[candidates.Count - 1];
    }

    private static WaveBonusOption RollBonus(WaveChoiceConfigSO config, System.Random rng)
    {
        float total = 0f;
        foreach (WaveBonusOption o in config.bonusPool)
        {
            if (o != null && o.type != WaveBonusType.None) total += o.weight;
        }
        if (total <= 0f) return null;
        double roll = rng.NextDouble() * total;
        WaveBonusOption last = null;
        foreach (WaveBonusOption o in config.bonusPool)
        {
            if (o == null || o.type == WaveBonusType.None || o.weight <= 0f) continue;
            last = o;
            roll -= o.weight;
            if (roll <= 0d) return o;
        }
        return last;
    }

    private static WaveBonusOption FindBonus(WaveChoiceConfigSO config, WaveBonusType type)
    {
        if (type == WaveBonusType.None) return null;
        WaveBonusOption found = config.bonusPool.Find(o => o != null && o.type == type);
        return found ?? new WaveBonusOption { type = type, weight = 0f, baseAmount = 1 };
    }

    public static int BonusAmount(WaveChoiceConfigSO config, WaveBonusOption option, int skulls, float multiplier)
    {
        if (option == null || option.type == WaveBonusType.None) return 0;
        if (option.type == WaveBonusType.DraftPick) return Mathf.Max(1, WaveChoiceConfigSO.BySkulls(config.draftPicksBySkulls, skulls));
        return Mathf.Max(1, Mathf.RoundToInt(option.baseAmount * multiplier));
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
