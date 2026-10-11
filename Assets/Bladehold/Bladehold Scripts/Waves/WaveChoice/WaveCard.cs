using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
///     One between-wave choice (plan 15): an objective with its stance, a difficulty of 1-3 skulls with a
///     clan modifier, and the reward bundle paid on success. Built by <see cref="WaveCardGenerator" />;
///     everything the card shows and the wave applies is resolved here, so they can't disagree.
/// </summary>
[Serializable]
public class WaveCard
{
    public int wave;
    public WaveObjectiveDefinition objective;
    public WaveStance stance;
    [Range(1, 3)] public int skulls = 1;

    [Header("Enemy modifier")]
    public WarBannerClanSO clan;
    /// <summary>The clan's base effect × <see cref="modifierScale" />, in <see cref="EnemyBuffController" />'s units.</summary>
    public float modifierMagnitude;
    public float modifierScale = 1f;
    public float enemyHealthMultiplier = 1f;
    public float killQuotaMultiplier = 1f;
    public bool hasCaptain;
    public string captainName;

    [Header("Enemy composition (plan 22)")]
    /// <summary>
    ///     The enemies this wave spawns (plan 22): the card chooses who you fight. Rolled by
    ///     <see cref="WaveCompositionRoller" /> from the roster's threat/wave gating. Empty = fall back
    ///     to the old threat-gated kill quota (dev scenes, the captain wave, missing roster).
    /// </summary>
    public List<WaveEnemyEntry> composition = new List<WaveEnemyEntry>();

    public bool HasComposition => composition != null && composition.Count > 0;

    /// <summary>Total enemies the composition spawns (0 when empty).</summary>
    public int TotalEnemies
    {
        get
        {
            int total = 0;
            if (composition != null)
            {
                foreach (WaveEnemyEntry e in composition)
                {
                    if (e != null) total += Mathf.Max(0, e.count);
                }
            }
            return total;
        }
    }

    /// <summary>"28 enemies" headline for the card (plan 22).</summary>
    public string EnemiesTitle => Loc.Get("wave.card.enemies_title", "{0} enemies").Replace("{0}", TotalEnemies.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    ///     "24× Goblin, 4× Brute, 1× Battering Ram", largest group first. Names come from the roster's
    ///     localized display name, falling back to the id when the roster or row is missing.
    /// </summary>
    public string CompositionText(EnemyRosterSO roster)
    {
        if (!HasComposition) return "";
        var entries = new List<WaveEnemyEntry>();
        foreach (WaveEnemyEntry e in composition)
        {
            if (e != null && e.count > 0 && !string.IsNullOrEmpty(e.enemyId)) entries.Add(e);
        }
        entries.Sort((a, b) => b.count.CompareTo(a.count));
        var parts = new List<string>(entries.Count);
        foreach (WaveEnemyEntry e in entries)
        {
            EnemyDefinition def = roster != null ? roster.Find(e.enemyId) : null;
            string name = def != null && !string.IsNullOrEmpty(def.displayName) ? def.LocalizedDisplayName : e.enemyId;
            parts.Add($"{e.count.ToString(CultureInfo.InvariantCulture)}× {name}");
        }
        return string.Join(", ", parts);
    }

    [Header("Reward")]
    public float rewardMultiplier = 1f;
    public bool isFresh;
    public int gold;
    public int supply;
    public WaveBonusType bonusType;
    public int bonusAmount;
    /// <summary>Rerolls on the weapon draft this wave pays on success (from <see cref="WaveChoiceConfigSO.draftRerollsBySkulls" />).</summary>
    public int draftRerolls;

    public string ObjectiveId => objective != null ? objective.id : "";
    public bool HasClan => clan != null && clan.buffType != BannerBuffType.None;
    public BannerBuffType BuffType => HasClan ? clan.buffType : BannerBuffType.None;
    public bool CanFail => objective != null && objective.failRule != WaveFailRule.None;

    /// <summary>Skulls mapped onto the old banner tiers so the captain scaling code stays untouched.</summary>
    public BannerDifficultyTier CaptainTier => skulls >= 3
        ? BannerDifficultyTier.Nightmare
        : skulls == 2 ? BannerDifficultyTier.Enraged : BannerDifficultyTier.Standard;

    public string SkullLabel => skulls switch
    {
        3 => Loc.Get("wave.skulls.hard", "HARD"),
        2 => Loc.Get("wave.skulls.medium", "MEDIUM"),
        _ => Loc.Get("wave.skulls.easy", "EASY")
    };

    public string StanceLabel => stance switch
    {
        WaveStance.Offence => Loc.Get("wave.stance.offence", "OFFENCE"),
        WaveStance.Defence => Loc.Get("wave.stance.defence", "DEFENCE"),
        _ => ""
    };

    public string ClanName => clan == null ? "" : Loc.Get($"clan.{ClanLocKey(clan)}.name", clan.clanName);

    /// <summary>The modifier in plain words with this card's magnitude, e.g. "Enemies take 15% less damage".</summary>
    public string ModifierEffectText => HasClan ? ClanModifierMath.EffectText(clan.buffType, modifierMagnitude) : "";

    public string TimerText => objective != null && objective.HasTimer
        ? Loc.Get("wave.card.timer", "{0} time limit").Replace("{0}", FormatSeconds(objective.timerSeconds))
        : Loc.Get("wave.card.no_timer", "No time limit");

    public string BonusText => bonusType switch
    {
        WaveBonusType.DraftPick => bonusAmount > 1
            ? Loc.Get("wave.reward.draft_many", "{0} draft picks").Replace("{0}", bonusAmount.ToString(CultureInfo.InvariantCulture))
            : Loc.Get("wave.reward.draft_one", "1 draft pick"),
        WaveBonusType.GoblinBlood => Loc.Get("wave.reward.blood", "{0} Goblin Blood").Replace("{0}", bonusAmount.ToString(CultureInfo.InvariantCulture)),
        WaveBonusType.OrcishMetal => Loc.Get("wave.reward.metal", "{0} Orcish Metal").Replace("{0}", bonusAmount.ToString(CultureInfo.InvariantCulture)),
        WaveBonusType.TrollHeart => Loc.Get("wave.reward.troll_heart", "Troll Heart: +{0} max HP").Replace("{0}", bonusAmount.ToString(CultureInfo.InvariantCulture)),
        _ => ""
    };

    /// <summary>"+1 draft reroll" for the weapon draft this wave pays; blank when the card has none.</summary>
    public string RerollText => draftRerolls <= 0
        ? ""
        : draftRerolls > 1
            ? Loc.Get("wave.reward.reroll_many", "+{0} draft rerolls").Replace("{0}", draftRerolls.ToString(CultureInfo.InvariantCulture))
            : Loc.Get("wave.reward.reroll_one", "+1 draft reroll");

    public static string FormatSeconds(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    /// <summary>Clan loc key: the asset name minus its "Clan_" prefix, lower-cased (Clan_StoneHide → stonehide).</summary>
    public static string ClanLocKey(WarBannerClanSO clan)
    {
        if (clan == null) return "";
        if (!string.IsNullOrEmpty(clan.locKey)) return clan.locKey;
        string name = clan.name ?? "";
        if (name.StartsWith("Clan_", StringComparison.OrdinalIgnoreCase)) name = name.Substring(5);
        return name.ToLowerInvariant();
    }
}

/// <summary>One enemy type and how many of it a <see cref="WaveCard" /> spawns (plan 22).</summary>
[Serializable]
public class WaveEnemyEntry
{
    public string enemyId;
    [Min(0)] public int count;

    public WaveEnemyEntry() { }

    public WaveEnemyEntry(string enemyId, int count)
    {
        this.enemyId = enemyId;
        this.count = count;
    }
}

/// <summary>
///     Scales a clan's base magnitude by skulls and describes it. Units follow
///     <see cref="EnemyBuffController" />: Shield and Armor are fractions (0.25), Haste and Berserk are
///     multipliers (1.35), Regen is HP per second. Only the bonus part of a multiplier is scaled, so
///     0.6 × Haste 1.35 is 1.21, not 0.81.
/// </summary>
public static class ClanModifierMath
{
    public static float Scale(BannerBuffType type, float baseMagnitude, float scale)
    {
        switch (type)
        {
            case BannerBuffType.Haste:
            case BannerBuffType.Berserk:
                float bonus = baseMagnitude > 1f ? baseMagnitude - 1f : baseMagnitude;
                return 1f + bonus * scale;
            case BannerBuffType.Armor:
                return Mathf.Clamp(baseMagnitude * scale, 0f, 0.9f);
            default:
                return baseMagnitude * scale;
        }
    }

    public static string EffectText(BannerBuffType type, float magnitude)
    {
        string pct = Percent(type == BannerBuffType.Haste || type == BannerBuffType.Berserk ? magnitude - 1f : magnitude);
        switch (type)
        {
            case BannerBuffType.Shield:
                return Loc.Get("wave.mod.shield", "Enemies spawn with a +{0}% max HP shield").Replace("{0}", pct);
            case BannerBuffType.Haste:
                return Loc.Get("wave.mod.haste", "Enemies move and attack {0}% faster").Replace("{0}", pct);
            case BannerBuffType.Berserk:
                return Loc.Get("wave.mod.berserk", "Enemies deal {0}% more damage but have 30% less HP").Replace("{0}", pct);
            case BannerBuffType.Regen:
                return Loc.Get("wave.mod.regen", "Enemies heal {0} HP per second").Replace("{0}", magnitude.ToString("0.#", CultureInfo.InvariantCulture));
            case BannerBuffType.Armor:
                return Loc.Get("wave.mod.armor", "Enemies take {0}% less damage").Replace("{0}", pct);
            default:
                return "";
        }
    }

    private static string Percent(float fraction) =>
        Mathf.RoundToInt(fraction * 100f).ToString(CultureInfo.InvariantCulture);
}
