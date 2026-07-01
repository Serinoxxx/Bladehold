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
    ChargeDamageBonus,
    /// <summary>How many unique enemies a single sword swing can damage before it's blocked. Base comes from the sword's DamageTriggerSO.maxHits.</summary>
    MaxHitsPerSwing,

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
