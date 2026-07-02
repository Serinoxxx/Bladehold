/// <summary>
///     Every player stat the upgrade system can modify. Bases are registered at runtime by the system
///     that owns each value (e.g. the sword's <see cref="DamageTrigger" /> registers
///     <see cref="SwordDamage" />, the move-speed binder registers <see cref="MoveSpeed" />), so this
///     enum stays the single shared vocabulary between upgrades and the systems they affect.
/// </summary>
public enum StatType
{
    SwordDamage,
    SwordRange,
    MoveSpeed,
    SprintSpeed,
    CritChance,
    CritMultiplier,
    KnockbackForce,
    /// <summary>Extra damage per charge level held (e.g. 0.5 = +50% per level). Does nothing until MaxChargeLevels is at least 1.</summary>
    ChargeDamageBonus,
    /// <summary>How many charge levels the attack hold can reach. 0 = hold-to-charge locked (the default until the Heavy Strike node is bought).</summary>
    MaxChargeLevels,
    /// <summary>How many unique enemies a single sword swing can damage before it's blocked. Base comes from the sword's DamageTriggerSO.maxHits.</summary>
    MaxHitsPerSwing,
    /// <summary>Unitless multiplier on the gold enemies drop (base 1.0, same convention as MoveSpeed).</summary>
    GoldDropMultiplier,
    /// <summary>Fraction of sword damage dealt returned to the player as health (e.g. 0.01 = 1% lifesteal). 0 = none.</summary>
    LifeStealPercent,
    /// <summary>Seconds between automatic damage blocks (the "Solid" skill line). 0 = blocking locked.</summary>
    BlockCooldown,
    /// <summary>Extra knockback per charge level held (e.g. 0.25 = +25% per level). Does nothing until MaxChargeLevels is at least 1.</summary>
    ChargeKnockbackBonus,

    /// <summary>0 = Death Nova locked, 1 = unlocked (a future node could grant a 2nd charge).</summary>
    DeathNovaCharges,
    /// <summary>Seconds before the Death Nova charge is available again after triggering.</summary>
    DeathNovaCooldown,
    /// <summary>Fraction (0-1) of max health the player revives with when the Death Nova triggers. 0 = blast-only, no revive.</summary>
    DeathNovaRevivePercent,
    /// <summary>Per-spawn chance (0-1) that a goblin spawns as a Golden Goblin.</summary>
    GoldenGoblinChance,
    /// <summary>Extra fraction of gold a Golden Goblin's bonus coin drops on top of its normal drop.</summary>
    GoldenGoblinGoldBonusPercent,
    /// <summary>Fraction (0-1) of the gold currently on the ground that's auto-collected when the player dies.</summary>
    GoldOnDeathPickupPercent,
}

/// <summary>
///     How a <see cref="StatType" /> modifier combines into the final value. The aggregation formula is
///     <c>final = (base + Σflat) × (1 + Σpercent)</c>, so a +1 flat and a +5% percent stack as you'd
///     expect, and duplicate nodes simply add another modifier of the same kind.
/// </summary>
public enum ModifierKind
{
    Flat,
    Percent,
}
