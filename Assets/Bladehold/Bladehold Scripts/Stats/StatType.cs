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
