using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>How a <see cref="StatType" /> value is presented to the player in the skill tooltip.</summary>
public enum StatFormat
{
    /// <summary>Raw number, trailing ".0" trimmed (e.g. Sword Damage "15", "12.5").</summary>
    Number,

    /// <summary>Whole count (e.g. Charge Levels, extra arrows) — "2".</summary>
    Integer,

    /// <summary>A 0-1 fraction shown as a percent (e.g. Crit Chance 0.05 → "5%").</summary>
    Percent,

    /// <summary>A base-1.0 multiplier shown as a percent of base (e.g. Move Speed 1.05 → "105%").</summary>
    Multiplier,

    /// <summary>Seconds (e.g. Auto-block cooldown "10s").</summary>
    Seconds,
}

/// <summary>
///     Static presentation layer for <see cref="StatType" />: a friendly label plus a "natural unit"
///     formatter for each stat, so the skill tooltip can render live before→after values
///     (e.g. "Sword Damage 10 -> 15", "Crit Chance 5% -> 10%", "Auto-block 10s -> 9s") without every
///     consumer re-deciding how a given stat reads. Stats not in the table degrade to a name split on
///     capital letters and <see cref="StatFormat.Number" /> formatting.
/// </summary>
public static class StatDisplay
{
    private readonly struct Info
    {
        public readonly string label;
        public readonly StatFormat format;

        public Info(string label, StatFormat format)
        {
            this.label = label;
            this.format = format;
        }
    }

    private static readonly Dictionary<StatType, Info> Table = new Dictionary<StatType, Info>
    {
        { StatType.SwordDamage, new Info("Sword Damage", StatFormat.Number) },
        { StatType.SwordRange, new Info("Sword Range", StatFormat.Multiplier) },
        { StatType.MoveSpeed, new Info("Move Speed", StatFormat.Multiplier) },
        { StatType.SprintSpeed, new Info("Sprint Speed", StatFormat.Multiplier) },
        { StatType.CritChance, new Info("Crit Chance", StatFormat.Percent) },
        { StatType.CritMultiplier, new Info("Crit Multiplier", StatFormat.Number) },
        { StatType.KnockbackForce, new Info("Knockback Force", StatFormat.Number) },
        { StatType.ChargeDamageBonus, new Info("Charge Damage / level", StatFormat.Percent) },
        { StatType.MaxChargeLevels, new Info("Charge Levels", StatFormat.Integer) },
        { StatType.MaxHitsPerSwing, new Info("Cut-through", StatFormat.Integer) },
        { StatType.GoldDropMultiplier, new Info("Gold Drop", StatFormat.Multiplier) },
        { StatType.LifeStealPercent, new Info("Life Steal", StatFormat.Percent) },
        { StatType.BlockCooldown, new Info("Auto-block", StatFormat.Seconds) },
        { StatType.ChargeKnockbackBonus, new Info("Charge Knockback / level", StatFormat.Percent) },
        { StatType.ParryChance, new Info("Parry Chance", StatFormat.Percent) },
        { StatType.CounterstrikePercent, new Info("Counterstrike", StatFormat.Percent) },

        { StatType.DeathNovaCharges, new Info("Death Nova Charges", StatFormat.Integer) },
        { StatType.DeathNovaCooldown, new Info("Death Nova Cooldown", StatFormat.Seconds) },
        { StatType.DeathNovaRevivePercent, new Info("Revive Health", StatFormat.Percent) },
        { StatType.GoldenGoblinChance, new Info("Golden Goblin Chance", StatFormat.Percent) },
        { StatType.GoldenGoblinGoldBonusPercent, new Info("Golden Goblin Bonus", StatFormat.Percent) },
        { StatType.GoldOnDeathPickupPercent, new Info("Gold on Death", StatFormat.Percent) },

        { StatType.ImpulseGoblinChance, new Info("Impulse Goblin Chance", StatFormat.Percent) },
        { StatType.ImpulseOrbDuration, new Info("Impulse Duration", StatFormat.Seconds) },

        { StatType.ChainLightningOrbDuration, new Info("Lightning Duration", StatFormat.Seconds) },
        { StatType.ChainLightningBounces, new Info("Lightning Bounces", StatFormat.Integer) },
        { StatType.ChainLightningDamagePercent, new Info("Lightning Damage", StatFormat.Percent) },
        { StatType.ChainLightningCritChance, new Info("Lightning Crit Chance", StatFormat.Percent) },

        { StatType.AllDamageMultiplier, new Info("All Damage", StatFormat.Multiplier) },
        { StatType.PlayerMaxHealthMultiplier, new Info("Max Health", StatFormat.Multiplier) },

        { StatType.BowDamage, new Info("Bow Damage", StatFormat.Number) },
        { StatType.BowMaxChargeLevels, new Info("Bow Charge Levels", StatFormat.Integer) },
        { StatType.BowChargeDamageBonus, new Info("Bow Charge Damage / level", StatFormat.Percent) },
        { StatType.BowMultishotArrows, new Info("Extra Arrows", StatFormat.Integer) },
        { StatType.BowMultishotDamagePercent, new Info("Extra Arrow Damage", StatFormat.Percent) },
        { StatType.BowBounceChance, new Info("Bounce Chance", StatFormat.Percent) },
        { StatType.BowImpulseArrows, new Info("Impulse Arrows", StatFormat.Integer) },
        { StatType.BowStormArrows, new Info("Storm Arrows", StatFormat.Integer) },
        { StatType.BowPickupArrows, new Info("Retriever", StatFormat.Integer) },
        { StatType.BowPrecisionDamageBonus, new Info("Precision Damage", StatFormat.Percent) },
        { StatType.BowArrowSpeed, new Info("Arrow Speed", StatFormat.Number) },

        { StatType.FreezingDrawSlowPercent, new Info("Freezing Draw Slow", StatFormat.Percent) },
        { StatType.BrainFreezeSlowPercent, new Info("Brain Freeze Slow", StatFormat.Percent) },
        { StatType.SlowDurationBonusSeconds, new Info("Slow Duration", StatFormat.Seconds) },
        { StatType.IceBreakerDamageBonus, new Info("Ice Breaker Damage", StatFormat.Percent) },
        { StatType.ExplodingHeadsDamagePercent, new Info("Exploding Heads Damage", StatFormat.Percent) },
        { StatType.MidasChance, new Info("Midas Chance", StatFormat.Percent) },
        { StatType.ConduitDamageReductionPercent, new Info("Damage Reduction", StatFormat.Percent) },
        { StatType.ConduitChainChance, new Info("Chain Chance", StatFormat.Percent) },
        { StatType.BowUnstableOrbs, new Info("Unstable Orbs", StatFormat.Integer) },

        { StatType.FlamingArrowsDamagePercent, new Info("Fire Damage", StatFormat.Percent) },
        { StatType.FlamingArrowsBomberDetonateChance, new Info("Bomber Detonate Chance", StatFormat.Percent) },

        { StatType.HealthPackHealPercent, new Info("Health Pack Heal", StatFormat.Percent) },

        { StatType.WandDamage, new Info("Wand Damage", StatFormat.Number) },
        { StatType.WandMaxChargeLevels, new Info("Wand Charge Levels", StatFormat.Integer) },
        { StatType.WandChargeDamageBonus, new Info("Wand Charge Damage / level", StatFormat.Percent) },
        { StatType.WandKnockback, new Info("Wand Knockback", StatFormat.Number) },

        { StatType.MageImbuementDuration, new Info("Imbuement Duration", StatFormat.Seconds) },
        { StatType.MageImbuementMaxCharges, new Info("Element Charges", StatFormat.Integer) },
        { StatType.MageImbuementBonusPerCharge, new Info("Damage / charge", StatFormat.Percent) },
        { StatType.MageRunestoneCharges, new Info("Runestone Charges", StatFormat.Integer) },

        { StatType.MageFireDamagePercent, new Info("Fire Bonus Damage", StatFormat.Percent) },
        { StatType.MageFireExplosionDamagePercent, new Info("Explosion Damage", StatFormat.Percent) },
        { StatType.MageFireExplosionRadius, new Info("Explosion Radius", StatFormat.Number) },
        { StatType.MageFlameZoneDuration, new Info("Burning Ground Duration", StatFormat.Seconds) },
        { StatType.MageFlameZoneDamagePercent, new Info("Burning Ground Damage", StatFormat.Percent) },

        { StatType.MageIceSlowPercent, new Info("Chill Slow", StatFormat.Percent) },
        { StatType.MageIceSlowDurationSeconds, new Info("Chill Duration", StatFormat.Seconds) },
    };

    /// <summary>
    ///     Friendly label for a stat: the localized <c>stat.&lt;StatType&gt;</c> entry when one exists,
    ///     else this table's English (which doubles as the Strings.csv source text, kept in sync by the
    ///     Localization Sync window), else the enum name split on capital letters.
    /// </summary>
    public static string Label(StatType stat)
    {
        string english = Table.TryGetValue(stat, out Info info) ? info.label : SplitCamelCase(stat.ToString());
        return Loc.Get("stat." + stat, english);
    }

    /// <summary>The untranslated English label — the sync tool's source text for Strings.csv rows.</summary>
    public static string EnglishLabel(StatType stat)
    {
        return Table.TryGetValue(stat, out Info info) ? info.label : SplitCamelCase(stat.ToString());
    }

    /// <summary>Formats a stat's value in its natural unit (number / percent / seconds / multiplier).</summary>
    public static string Value(StatType stat, float value)
    {
        StatFormat format = Table.TryGetValue(stat, out Info info) ? info.format : StatFormat.Number;
        switch (format)
        {
            case StatFormat.Integer:
                return Mathf.RoundToInt(value).ToString();
            case StatFormat.Percent:
            case StatFormat.Multiplier:
                return Mathf.RoundToInt(value * 100f) + "%";
            case StatFormat.Seconds:
                return value.ToString("0.##") + Loc.Get("stat.suffix.seconds", "s");
            default:
                return value.ToString("0.##");
        }
    }

    /// <summary>
    ///     Whether a relative "+X%" delta reads sensibly for this stat. False for whole counts and
    ///     second-based stats (a "+100%" on "cut through 1 more enemy", or on a cooldown reduction, is
    ///     misleading), which show before→after only.
    /// </summary>
    public static bool ShowsPercentDelta(StatType stat)
    {
        StatFormat format = Table.TryGetValue(stat, out Info info) ? info.format : StatFormat.Number;
        return format == StatFormat.Number || format == StatFormat.Percent || format == StatFormat.Multiplier;
    }

    private static string SplitCamelCase(string name)
    {
        StringBuilder sb = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                sb.Append(' ');
            }
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}
