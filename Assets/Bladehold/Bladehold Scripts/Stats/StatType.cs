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
    /// <summary>Chance (0-1) to auto-block an incoming melee hit while facing the attacker (the "Parry" skill line). 0 = locked.</summary>
    ParryChance,
    /// <summary>Fraction of effective sword damage dealt back to the attacker on a successful parry (the "Counterstrike" skill line). 0 = no counterattack.</summary>
    CounterstrikePercent,

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
    /// <summary>Extra gold-drop bonus banked per consecutive "Hold the Line" wave (0.05 = +5% per wave, added as a Percent modifier on GoldDropMultiplier). Base 0.05 registered by HoldTheLineBonus; the Reincarnate "Greedy Stand" node raises it.</summary>
    HoldTheLineGoldPerWave,

    /// <summary>Per-spawn chance (0-1) that a goblin spawns as an Impulse Goblin (drops an Impulse Orb on death). 0 = the Impulse feature locked.</summary>
    ImpulseGoblinChance,
    /// <summary>Seconds of Impulse buff granted per orb picked up (added to any remaining time). 0 = locked; orbs grant nothing.</summary>
    ImpulseOrbDuration,

    /// <summary>Seconds of Chain Lightning buff granted per Lightning Orb picked up. 0 = locked; orbs grant nothing.</summary>
    ChainLightningOrbDuration,
    /// <summary>How many additional enemies a sword hit's chain lightning can bounce to while the buff is active. 0 = locked.</summary>
    ChainLightningBounces,
    /// <summary>Fraction of the triggering hit's damage each chain lightning bounce deals (e.g. 0.5 = 50%). 0 = locked.</summary>
    ChainLightningDamagePercent,
    /// <summary>Chance (0-1) each chain lightning bounce crits, using the sword's CritMultiplier.</summary>
    ChainLightningCritChance,

    /// <summary>
    ///     Unitless multiplier on every damage source the player owns — sword, bow, Death Nova, and
    ///     anything derived from them (chain lightning scales with its triggering hit). Base 1.0
    ///     (registered by <see cref="Player" />), same convention as MoveSpeed.
    /// </summary>
    AllDamageMultiplier,

    /// <summary>Unitless multiplier on the player's maximum health (base 1.0, the MoveSpeed convention).</summary>
    PlayerMaxHealthMultiplier,

    /// <summary>1 = the bow is unlocked and can be drawn (hold aim); 0 = locked (aiming does nothing, sword stays out). Gated by the "Bow" skill node.</summary>
    BowUnlocked,
    /// <summary>Damage of one arrow before charge/crit/multipliers. Base comes from BowSO.baseDamage.</summary>
    BowDamage,
    /// <summary>How many charge levels the bow draw can reach while aiming. Base comes from BowSO (the bow charges out of the box, unlike the sword's Heavy Strike gate).</summary>
    BowMaxChargeLevels,
    /// <summary>Extra arrow damage per charge level held (e.g. 0.5 = +50% per level).</summary>
    BowChargeDamageBonus,
    /// <summary>Base knockback force dealt by arrow hits before charge scaling. Base comes from BowSO.baseKnockback.</summary>
    BowKnockback,
    /// <summary>How many extra arrows each shot fires in an arc alongside the main arrow. 0 = Multi Shot locked.</summary>
    BowMultishotArrows,
    /// <summary>Fraction of the main arrow's damage each extra Multi Shot arrow deals (base 0.25 from BowSO).</summary>
    BowMultishotDamagePercent,
    /// <summary>Chance (0-1) each arrow hit bounces to one additional nearby enemy. 0 = Bounce Shot locked.</summary>
    BowBounceChance,
    /// <summary>1 = arrows are affected by the Impulse Orb buff (impulse-stamped hits + stack damage). 0 = locked.</summary>
    BowImpulseArrows,
    /// <summary>1 = arrow hits chain lightning while the Chain Lightning (storm) buff is active. 0 = locked.</summary>
    BowStormArrows,
    /// <summary>1 = arrows collect gold and power-up orbs they fly past. 0 = locked.</summary>
    BowPickupArrows,
    /// <summary>Extra damage fraction against VulnerableSpot colliders (e.g. 1.0 = +100% = double damage). 0 = Precision Shot locked.</summary>
    BowPrecisionDamageBonus,
    /// <summary>Arrow flight speed in metres per second (base from BowSO.baseArrowSpeed). Faster arrows spend less time falling, so the "Swift Arrows" line flattens the drop arc too.</summary>
    BowArrowSpeed,

    /// <summary>Fraction (0-1) enemies near the player are slowed while the bow is drawn (the "Freezing Draw" line). 0 = locked.</summary>
    FreezingDrawSlowPercent,
    /// <summary>Fraction (0-1) a VulnerableSpot arrow hit slows the target's movement and animation (the "Brain Freeze" line). 0 = locked.</summary>
    BrainFreezeSlowPercent,
    /// <summary>Extra seconds every slow (Freezing Draw or Brain Freeze) lingers (the "Elongated Freeze" line).</summary>
    SlowDurationBonusSeconds,
    /// <summary>Extra damage fraction the player's sword deals to slowed/chilled enemies (e.g. 0.5 = +50%; the "Ice Breaker" line). 0 = locked.</summary>
    IceBreakerDamageBonus,
    /// <summary>Fraction of the arrow's damage a VulnerableSpot hit detonates as an impulse blast at the point of impact (the "Exploding Heads" line). 0 = locked.</summary>
    ExplodingHeadsDamagePercent,
    /// <summary>Chance (0-1) each arrow hit converts a regular enemy into a golden one (the "Arrows of Midas" line). 0 = locked.</summary>
    MidasChance,
    /// <summary>Fraction (0-1) Storm Witch lightning balls' damage to the player is reduced by (the "Conduit" line). 0 = locked.</summary>
    ConduitDamageReductionPercent,
    /// <summary>Chance (0-1) a lightning ball hitting the player procs chain lightning back at nearby enemies (the "Conduit" line). 0 = locked.</summary>
    ConduitChainChance,
    /// <summary>1 = the main arrow detonates Impulse/Lightning Orbs it hits (an impulse blast or chain lightning around the orb; the "Unstable Orbs" node). 0 = locked.</summary>
    BowUnstableOrbs,

    /// <summary>Extra fraction of an arrow's damage dealt as a separate elemental fire hit on the same target (the "Flaming Arrows" line). 0 = locked.</summary>
    FlamingArrowsDamagePercent,
    /// <summary>Chance (0-1) each arrow hit on a Bomber instantly detonates its explosion (the "Flaming Arrows" line — see <see cref="BomberAttack.Detonate" />). 0 = locked.</summary>
    FlamingArrowsBomberDetonateChance,

    /// <summary>Fraction (0-1) of max health a Health Pack pickup restores. Base 0.10 (packs work out of the box); the "Field Medic" line raises it.</summary>
    HealthPackHealPercent,

    /// <summary>
    ///     1 = horse riding unlocked (the gold-tree "Saddle Up" node). Riding is allowed when this OR
    ///     <see cref="StartMounted" /> is at least 1 (the code-side OR lives in <c>PlayerMount.CanRide</c>) —
    ///     Reincarnate wipes the gold tree, so the Cavalier node must grant riding by itself.
    /// </summary>
    HorseRidingUnlocked,
    /// <summary>Unitless multiplier on a player-ridden horse's max health (base 1.0, the MoveSpeed convention). Applied once per horse on first mount, preserving its current health fraction.</summary>
    HorseMaxHealthMultiplier,
    /// <summary>Unitless multiplier on a player-ridden horse's speeds (base 1.0). AI-ridden horses ignore it.</summary>
    HorseSpeedMultiplier,
    /// <summary>1 = Health Packs the ridden horse runs over also heal the horse (the "Stable Diet" node). 0 = locked.</summary>
    HorseHealFromPacks,
    /// <summary>1 = the bow can be drawn while mounted (the "Horse Archer" node). 0 = mounted aiming does nothing (the BowUnlocked gate, mounted edition).</summary>
    HorseArcheryUnlocked,
    /// <summary>1 = each run starts already mounted on a spawned horse (the Reincarnate "Cavalier" node; grants riding by itself — see <see cref="HorseRidingUnlocked" />).</summary>
    StartMounted,
    
    /// <summary>1 = summon mount ability unlocked, 0 = locked.</summary>
    SummonMountUnlocked,
    /// <summary>Base duration of the summoned mount before it despawns.</summary>
    SummonMountDuration,
    /// <summary>Cooldown in seconds before the mount can be summoned again.</summary>
    SummonMountCooldown,
    
    /// <summary>1 = dodge unlocked, 0 = locked.</summary>
    DodgeUnlocked,
    /// <summary>Cooldown in seconds before dodging again.</summary>
    DodgeCooldown,
    /// <summary>Distance the dodge covers in metres.</summary>
    DodgeDistance,
    /// <summary>Multiplier on base sword damage dealt to enemies dashed through. 0 = no damage.</summary>
    DodgeDamageMultiplier,
    /// <summary>Force applied to enemies hit by the dodge dash. 0 = no knockback.</summary>
    DodgeKnockbackForce,
    /// <summary>Seconds of cooldown recovered per enemy killed by the dodge dash.</summary>
    DodgeChainCooldownReduction,

    /// <summary>1 = the Berserker's throwing axe is unlocked and can be wound up (hold aim); 0 = locked (aiming does nothing, melee stays out). Gated by the "Throwing Axe" node — the BowUnlocked convention.</summary>
    AxeThrowUnlocked,
    /// <summary>Damage of one throw per enemy hit, before charge/crit/multipliers. Base comes from ThrownAxeSO.baseDamage.</summary>
    AxeThrowDamage,
    /// <summary>How many charge levels the throw wind-up can reach while aiming. Base comes from ThrownAxeSO (the throw charges out of the box, like the bow's draw).</summary>
    AxeThrowMaxChargeLevels,
    /// <summary>Extra throw damage per charge level held (e.g. 0.5 = +50% per level).</summary>
    AxeThrowChargeDamageBonus,
    /// <summary>Knockback impulse each throw hit shoves its target with (charge amplifies it further via ThrownAxeSO.knockbackPerChargeLevel).</summary>
    AxeThrowKnockback,
    /// <summary>How many enemies one uncharged throw can pierce through in its line (charge adds ThrownAxeSO.piercePerChargeLevel more per level).</summary>
    AxeThrowPierceCount,
    /// <summary>Width of the throw's flight path in metres — the projectile's swept damage diameter; enemies within it count as hit (the "Wide Arc" area line).</summary>
    AxeThrowWidth,
    /// <summary>1 = the thrown axe boomerangs: after striking terrain, spending its pierce, or reaching max range it flies back to the Berserker, damaging enemies on the return leg too (fresh pierce budget). 0 = locked — the axe lodges where it stops. Gated by the "Boomerang" node.</summary>
    AxeBoomerangUnlocked,

    /// <summary>Fraction of damage taken while charging a melee swing or winding up a throw that's banked and added flat to that attack's damage (the Berserker's "Pain into Power" line). 0 = locked.</summary>
    PainIntoPowerPercent,
    /// <summary>Unitless multiplier on how fast the Berserker's rage builds from dealing/taking damage (base 1.0, the MoveSpeed convention — see RageBuff).</summary>
    RageGainMultiplier,
    /// <summary>Unitless multiplier on how long rage lingers: scales the decay grace window up and the drain rate down (base 1.0).</summary>
    RageRetentionMultiplier,

    /// <summary>1 = the Mage's wand is unlocked and can be aimed (hold aim); 0 = locked (aiming does nothing, staff stays out). Gated by the "Wand" node — the BowUnlocked convention.</summary>
    WandUnlocked,
    /// <summary>Damage of one magic missile before charge/crit/multipliers. Base comes from WandSO.baseDamage.</summary>
    WandDamage,
    /// <summary>How many charge levels the wand can reach while aiming. Base comes from WandSO (the wand charges out of the box, like the bow's draw).</summary>
    WandMaxChargeLevels,
    /// <summary>Extra missile damage per charge level held (e.g. 0.5 = +50% per level).</summary>
    WandChargeDamageBonus,
    /// <summary>Knockback impulse each missile hit shoves its target with. Base comes from WandSO.baseKnockback.</summary>
    WandKnockback,

    /// <summary>Seconds the Mage's elemental imbuement lasts per refresh — every node pickup resets the timer to this (the "Lingering Element" line). Base comes from MageImbuementSO.</summary>
    MageImbuementDuration,
    /// <summary>Maximum element charges the imbuement can stack (the "Overflowing Vessel" line). Base comes from MageImbuementSO.</summary>
    MageImbuementMaxCharges,
    /// <summary>Extra elemental damage dealt per held element charge, as a fraction of the triggering hit (0.10 = +10% per charge; the "Elemental Mastery" line). Base comes from MageImbuementSO.</summary>
    MageImbuementBonusPerCharge,
    /// <summary>Element charges granted when the Mage blasts a runestone of a different element (the "Runic Attunement" line). Base 2 — runestones work out of the box.</summary>
    MageRunestoneCharges,

    /// <summary>Extra fraction of the triggering hit dealt while Fire-imbued, on top of the per-charge bonus (the "Searing Focus" line). Base comes from MageImbuementSO.</summary>
    MageFireDamagePercent,
    /// <summary>Fraction of a Fire-imbued hit's damage dealt to enemies around the hit point (the "Combustion" node). 0 = explosions locked.</summary>
    MageFireExplosionDamagePercent,
    /// <summary>Radius in metres of the Fire explosion (the "Greater Fireball" line scales it with Percent modifiers). Base comes from MageImbuementSO.</summary>
    MageFireExplosionRadius,
    /// <summary>Seconds a Fire ground zone burns (the "Scorched Earth" node). 0 = flame zones locked.</summary>
    MageFlameZoneDuration,
    /// <summary>Fraction of the triggering hit's damage each flame-zone tick deals (the "Everburning" line raises it). Base comes from MageImbuementSO; inert until MageFlameZoneDuration is above 0.</summary>
    MageFlameZoneDamagePercent,

    /// <summary>Fraction (0-1) Ice-imbued hits slow their target (the "Deep Chill" line). Base comes from MageImbuementSO — ice slows out of the box.</summary>
    MageIceSlowPercent,
    /// <summary>Seconds an Ice-imbued hit's slow lasts (SlowDurationBonusSeconds adds on top). Base comes from MageImbuementSO.</summary>
    MageIceSlowDurationSeconds,

    /// <summary>1 = Ultimate unlocked, 0 = locked.</summary>
    UltimateUnlocked,
    /// <summary>Base duration of the ultimate in seconds.</summary>
    UltimateDurationSeconds,
    /// <summary>Multiplier on how fast ultimate charges from damage (base 1.0).</summary>
    UltimateChargeMultiplier,
    /// <summary>Ranger Ultimate arrow cooldown in seconds.</summary>
    UltimateRangerFireRate,
    /// <summary>Mage Ultimate meteor damage multiplier relative to base wand damage.</summary>
    UltimateMageMeteorDamageMultiplier,
    /// <summary>Mage Ultimate landing explosion radius.</summary>
    UltimateMageLandingExplosionRadius,
    /// <summary>Berserker Ultimate size multiplier (base 1.5).</summary>
    UltimateBerserkerSizeMultiplier,
    /// <summary>Berserker Ultimate damage reduction fraction (0-1).</summary>
    UltimateBerserkerDamageReduction,
    /// <summary>Amount of ultimate charge gained passively per second.</summary>
    UltimatePassiveChargeRate,

    /// <summary>1 = Fort Arrow Slits unlocked, 0 = locked.</summary>
    FortArrowSlitsUnlocked,
    /// <summary>Fort Arrow Slits arrow damage.</summary>
    FortArrowSlitsDamage,
    /// <summary>Fort Arrow Slits fire interval multiplier.</summary>
    FortArrowSlitsFireRate,

    /// <summary>1 = Fort Boiling Oil unlocked, 0 = locked.</summary>
    FortBurningOilUnlocked,
    /// <summary>Fort Boiling Oil damage per second.</summary>
    FortBurningOilDamage,
    /// <summary>Fort Boiling Oil cooldown reduction.</summary>
    FortBurningOilCooldown,

    /// <summary>1 = Fort Spike Barricades unlocked, 0 = locked.</summary>
    FortSpikesUnlocked,
    /// <summary>Fort Spike Barricades base contact damage.</summary>
    FortSpikesDamage,
    /// <summary>Fort Spike Barricades damage multiplier against ragdolled enemies (base 5.0).</summary>
    FortSpikesRagdollMultiplier,
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
