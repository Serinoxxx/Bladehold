# Editor to-do: weapon draft cards (fixes + feedback)

Not from a plan; added 2026-10-04 with Unity MCP connected. Tick items off as you go and delete the file when it's empty.

What's done: all 27 non-ultimate Weapon cards in `Resources/DraftUpgrades.csv` were audited. Bugs fixed in C# (see the commit), and every card now has feedback. Each new MMF is a clone of an existing player under `Player.prefab › SidekickSyntyCharacter/CardFeedbacks/`, already wired:

| Card | Feedback (clone of) |
|---|---|
| Nimble Strike | `NimbleStrikeMMF` (sword woosh) |
| Lunge Mastery, ShieldBreaker | Lunge / shield hits now play the weapon's `CritHitMMF` |
| Vampire Blade | the existing `LifestealMMF` (heal moved into `VampiricBlade`) |
| Desperate Volley | `DesperateVolleyMMF` (Axe Storm activate: sound, particles, impulse, crack) |
| Bouncer | `BowBounceMMF` (axe ricochet) |
| Celebratory Spin | Axe ultimate's whirlwind VFX + root spin, `CelebratorySpinMMF` (axe woosh) |
| Fear the Axe | `FearTheAxeMMF` (mace leap), plus the status pop over each feared enemy |
| Power Dash | `PowerDashChargeMMF` (axe ChargeMMF 4: sound + weapon flicker) |
| Heavy Stance | `HeavyStanceShieldMMF` (armour equip), `HeavyStanceAbsorbMMF` (block) |
| Bloodsplosion | `BloodsplosionMMF` (Impulse blast blood burst + Death Nova blast sound) |
| First Strike | `FirstStrikeMMF` (bow crit hit), plus crit-style damage numbers |
| Boomerang | `BoomerangTurnMMF` (axe woosh), `BoomerangCatchMMF` (ricochet) |
| Spin Top | the projectile's `ricochetVisual` turns on |
| Throwing axe base | `ThrowingAxeDrawMMF` (bow draw), `ThrowingAxeThrowMMF` (axe woosh); the throw was silent before |
| Concussive Impact | `MaceStunMMF` (status applied) |
| Armor Shatter | `ArmorShatterMMF` (mace inanimate hit) |
| Colossal Force | `KnockbackReceiver`'s own fling/knockdown feedback (it now actually knocks back) |
| Frost Wake | `FrostWakeMMF` (frost step), only when a pulse catches an enemy |
| Bloodlust | `BloodlustMMF` (lifesteal) |
| Blazing Hooves | `fireTrailVfxPrefab` = `FX_Fire_Small_01` (the dash Blazing Trail segment) |

Checked: `dotnet build` both assemblies clean; **Draft Catalog Loading** PASS (46 rows, no warnings); **Draft Weapon Charges (Play Mode)** PASS (26 cases).

## Sound / VFX picks (your call)

- [ ] Listen to each clone above. They're placeholders borrowed from neighbours, so swap any clip or particle you don't like on the clone. Each clone has its own GameObject, so changing it doesn't affect the original.
- [ ] **Fear the Axe** uses the mace-leap sound and particles. A shout or "terror" sting would read better.
- [ ] **Bloodsplosion** plays the Death Nova blast sound. Consider a wetter gib sound.
- [ ] **Battering Ram** still uses the horse's generic trample hit (intensity not scaled). Tell an agent if you want a heavier hit at high levels.

## Playtest (DevConsole → grant card, set level)

- [ ] **Nimble Strike**: one dash = one hit per enemy (damage numbers appear once, not twice). A normal attack right after a dash still deals damage.
- [ ] **Desperate Volley**: drop below 50% HP and get hit; the ring arrows do full charged damage and can bounce.
- [ ] **Bouncer**: quick-tap shots now bounce at least half the time; full draws always bounce.
- [ ] **Vampire Blade**: heals on hits (lifesteal particles) but not when the Bulwark's shield blocks, and not at full HP.
- [ ] **ShieldBreaker / Armor Shatter**: bonus now applies to Bulwark shields too (they break faster). Armor Shatter "heavy" now = MaxHealth ≥ 40 or knockback resistance ≥ 3 (brute, big ork, bulwark, spearman, knight, dome warden).
- [ ] **Celebratory Spin**: axe kill → short visible spin with whirlwind VFX. Pop the axe ultimate mid-spin: the ultimate runs its full duration (it used to be cut short). Not while mounted.
- [ ] **Fear the Axe**: enemies near an axe kill freeze 0.6–1.5 s and **don't swing** while frozen. A chilled enemy that gets feared un-freezes after the fear, then stays chilled (no more 3 s lock).
- [ ] **Power Dash**: dash, then hold attack; the charge bar fills faster and never runs backwards when the 2.5 s window closes mid-hold.
- [ ] **Like Butter** (reworked): cleaving 3–5 goblins, the later targets take noticeably more damage than without the card.
- [ ] **Concussive Impact / Frozen**: stunned enemies never land the hit they were winding up.
- [ ] **Colossal Force**: mace hits fling goblins; at high levels big orks get knocked down. Try knocking enemies off a bridge into a spike ravine.
- [ ] **Earthshaker**: one shockwave per fully charged swing, even when it cleaves 5 enemies. It no longer damages the gate.
- [ ] **Spin Top**: a lodged axe keeps spinning about 1.5 s; vortex kills trigger Bloodsplosion.
- [ ] **Mount cards**: after the horse is permanently lost, no mount card is offered.
- [ ] **Blazing Hooves**: the horse no longer takes fire damage when it stops on its own trail.
