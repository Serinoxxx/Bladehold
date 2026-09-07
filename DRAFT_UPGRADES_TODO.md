# Draft Upgrades Implementation Tracker

This document tracks the implementation status of all 52 draft cards in `DraftUpgrades.csv`.
Items are executed **one by one in order**, with compilation verification (`dotnet build`) and Unity refresh after each.

---

## Group 1: Bug Fixes & Partial Implementation Completion (High Priority)
- [x] **Task 01: Fix Fort Defense ID Mismatch in FortDefenseManager**
  - **IDs:** `fort_arrow_slits`, `fort_boiling_oil`, `fort_spike_barricades`
  - **Issue:** CSV uses plural/alternate names (`fort_arrow_slits`, `fort_boiling_oil`, `fort_spike_barricades`), but `FortDefenseManager.HandleSkillNodePurchased` only checks `fort_arrow_slit`, `fort_burning_oil`, `fort_spikes`.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/FortDefenseManager.cs`

- [x] **Task 02: Vampire Blade Elemental Vulnerability Penalty**
  - **ID:** `sword_vampire_blade`
  - **Stat:** `SwordVampireExtraElementalDamage` (+50% extra elemental damage taken)
  - **Issue:** Heal (2 HP/hit) works, but elemental penalty is never checked.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/Player.cs`

- [x] **Task 03: Bow Bouncer Multi-Target Bounce Scaling**
  - **ID:** `bow_bouncer`
  - **Stat:** `BowBounceCount` (1|2|3|4)
  - **Issue:** `BowBounceChance` works for 1 bounce, but `BowBounceCount` is ignored.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerBow.cs`

- [x] **Task 04: Gate Deep Freeze Behind Unlock Perk**
  - **ID:** `elem_ice_deep_freeze`
  - **Stat:** `IceDeepFreezeUnlocked`
  - **Issue:** `EnemyStatusManager` freezes enemies solid unconditionally on consecutive ice hits without checking `IceDeepFreezeUnlocked`.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Enemies/EnemyStatusManager.cs`

---

## Group 2: Weapon Upgrades - Sword & Bow
- [x] **Task 05: Sword Nimble Strike**
  - **ID:** `sword_nimble_strike`
  - **Stat:** `SwordNimbleStrike`
  - **Spec:** Dashing performs an automatic attack along the dodge movement path.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerDodge.cs` / `DamageTrigger.cs`

- [x] **Task 06: Sword Lunge Mastery**
  - **ID:** `sword_lunge_mastery`
  - **Stats:** `SwordLungeDamageBonus` (+200%), `SwordLungeCritBonus` (+40%)
  - **Spec:** Attacks performed immediately coming out of a dash gain huge damage & crit bonus.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerAttack.cs` / `DamageTrigger.cs`

- [x] **Task 07: Bow Auto-Shot on Dash**
  - **ID:** `bow_auto_shot`
  - **Stat:** `BowAutoShotOnDash`
  - **Spec:** Dashing automatically fires a shot at the nearest enemy.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerBow.cs` / `PlayerDodge.cs`

- [x] **Task 08: Bow Piercer**
  - **ID:** `bow_piercer`
  - **Stat:** `BowPierceCount` (1|2|3|4)
  - **Spec:** Arrows pierce through up to N additional enemies before despawning.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/ArrowProjectile.cs` / `PlayerBow.cs`

- [x] **Task 09: Bow Desperate Volley**
  - **ID:** `bow_desperate_volley`
  - **Stat:** `BowDesperateVolleyArrows` (8|12|16|20)
  - **Spec:** Fires a radial ring of arrows when player health drops below 50% HP.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerBow.cs`

---

## Group 3: Weapon Upgrades - Axe & Throwing Axe
- [x] **Task 10: Axe Fear on Kill**
  - **ID:** `axe_fear_axe`
  - **Stat:** `AxeFearDuration` (0.2s - 0.5s)
  - **Spec:** Killing an enemy with melee axe freezes nearby enemies in fear briefly.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerWeaponManager.cs`

- [x] **Task 11: Axe Power Dash**
  - **ID:** `axe_power_dash`
  - **Stat:** `AxePowerDashChargeSpeed` (+100% to +400%)
  - **Spec:** Attacks charge significantly faster for 2.5s immediately following a dash.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerAttack.cs`

- [x] **Task 12: Axe Heavy Stance**
  - **ID:** `axe_heavy_stance`
  - **Stat:** `AxeHeavyStanceShield` (2|4|6|8 HP)
  - **Spec:** Fully charged attacks grant a 2s temporary shield absorbing damage.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerAttack.cs`

- [x] **Task 13: Throwing Axe First Strike**
  - **ID:** `taxe_first_strike`
  - **Stat:** `AxeFirstStrikeBonus` (+50% to +80%)
  - **Spec:** Thrown axe deals bonus damage against targets at full health.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/AxeProjectile.cs`

- [x] **Task 14: Throwing Axe Bloodsplosion**
  - **ID:** `taxe_bloodsplosion`
  - **Stat:** `AxeBloodsplosionDamage` (10|20|30|40)
  - **Spec:** Kills trigger an AoE blood explosion damaging nearby enemies.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/AxeProjectile.cs`

- [x] **Task 15: Throwing Axe Spin Top**
  - **ID:** `taxe_spin_top`
  - **Stat:** `AxeSpinTopDPS` (4|8|12|16)
  - **Spec:** Thrown axe flies at slower speed and acts as a traveling AoE damage vortex.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/AxeProjectile.cs`

---

## Group 4: Elemental Upgrades - Fire
- [x] **Task 16: Fire Combustion Scaling**
  - **ID:** `elem_fire_combustion`
  - **Stat:** `FireCombustionDPS` (4|6|8|10)
  - **Spec:** Burn DoT scales with this stat instead of hardcoded percentage.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Enemies/EnemyStatusManager.cs`

- [x] **Task 17: Fire Kindling Damage Amplification**
  - **ID:** `elem_fire_kindling`
  - **Stat:** `FireKindlingDamageBonus` (+50% to +110%)
  - **Spec:** Ignited targets take amplified damage from all sources.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Enemies/EnemyStatusManager.cs`

- [x] **Task 18: Fire Blazing Trail on Dash**
  - **ID:** `elem_fire_blazing_trail`
  - **Stat:** `FireBlazingTrailDPS` (4|6|8|10)
  - **Spec:** Dashing leaves a fire trail dealing DPS to enemies stepping in it.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerDodge.cs`

- [x] **Task 19: Fire Inferno Burst on Ultimate**
  - **ID:** `elem_fire_inferno_burst`
  - **Stat:** `FireInfernoBurstUnlocked`
  - **Spec:** Activating ultimate immediately ignites all nearby enemies.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerUltimateController.cs`

- [x] **Task 20: Fire Fortress Pyre**
  - **ID:** `elem_fire_fortress_pyre`
  - **Stat:** `FireFortressPyreBonus` (+50% to +140%)
  - **Spec:** Fortress attacks deal bonus damage against ignited foes.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/FortArrowProjectile.cs` / `SpikeDefense.cs`

---

## Group 5: Elemental Upgrades - Lightning
- [x] **Task 21: Lightning Static Edge**
  - **ID:** `elem_light_static_edge`
  - **Stat:** `LightningStaticEdgeDamage` (30|50|70|90)
  - **Spec:** First hit of a melee chain deals bonus flat lightning damage.
  - **File:** `Assets/Bladehold/Bladehold Scripts/DamageSystem/DamageTrigger.cs`

- [x] **Task 22: Lightning Eye of the Storm**
  - **ID:** `elem_light_eye_storm`
  - **Stat:** `LightningEyeOfTheStormDamage` (50|100|150|200)
  - **Spec:** Lightning periodically strikes a random enemy during active ultimate.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerUltimateController.cs`

- [x] **Task 23: Lightning Tesla Spire**
  - **ID:** `elem_light_tesla_spire`
  - **Stat:** `LightningTeslaSpireDamage` (50|100|150|200)
  - **Spec:** Fortress discharges a lightning bolt at an enemy every 5s.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/FortDefenseManager.cs`

---

## Group 6: Elemental Upgrades - Ice
- [x] **Task 24: Ice Frost Step**
  - **ID:** `elem_ice_frost_step`
  - **Stat:** `IceFrostStepSlowPercent` (20% to 50%)
  - **Spec:** Dashing emits a chill wave slowing nearby enemies for 3s.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerDodge.cs`

- [x] **Task 25: Ice Shatter Bonus**
  - **ID:** `elem_ice_shatter`
  - **Stat:** `IceShatterBonus` (+200% to +500%)
  - **Spec:** Frozen enemies take massive amplified damage from all attacks.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Enemies/EnemyStatusManager.cs`

- [x] **Task 26: Ice Permafrost Wall Aura**
  - **ID:** `elem_ice_permafrost`
  - **Stat:** `IcePermafrostUnlocked`
  - **Spec:** Fortress walls passively emit an aura chilling adjacent foes.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/FortDefenseManager.cs`

- [x] **Task 27: Ice Shards on Ranged Kill**
  - **ID:** `elem_ice_ice_shards`
  - **Stat:** `IceShardsBurstDamage` (50|60|70|80)
  - **Spec:** Ranged kills shatter enemies into an 8-way projectile burst.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Player/PlayerBow.cs` / `PlayerThrownAxe.cs`

---

## Group 7: Fortress Upgrades - Arrow Slits
- [x] **Task 28: Arrow Slits Reinforced Volleys**
  - **ID:** `fort_reinforced_slits`
  - **Stat:** `FortArrowSlitsCount` (1|2|3|4)
  - **Spec:** Adds extra arrows per volley to arrow slits.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/ArrowSlitDefense.cs`

- [x] **Task 29: Arrow Slits Sniper Nest**
  - **ID:** `fort_sniper_nest`
  - **Stat:** `FortSniperNestUnlocked`
  - **Spec:** Increased range and +200% damage when no foes are near walls.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/ArrowSlitDefense.cs`

- [x] **Task 30: Arrow Slits Focus Fire**
  - **ID:** `fort_focus_fire`
  - **Stat:** `FortFocusFireBonus` (+50% to +110%)
  - **Spec:** Targets hit by player ranged attacks take bonus damage from slits.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/ArrowSlitDefense.cs`

---

## Group 8: Fortress Upgrades - Boiling Oil
- [x] **Task 31: Boiling Oil Fiery Pitch**
  - **ID:** `fort_fiery_pitch`
  - **Stat:** `FortFieryPitchUnlocked`
  - **Spec:** Boiling oil ignites all enemies caught in its puddle with Fire status.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/BurningOilZone.cs`

- [x] **Task 32: Boiling Oil Scalding Heat**
  - **ID:** `fort_scalding_heat`
  - **Stat:** `FortScaldingHeatBonus` (+50% to +110%)
  - **Spec:** Enemies inside boiling oil take increased damage from all sources.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/BurningOilZone.cs`

- [x] **Task 33: Boiling Oil Expanded Vats**
  - **ID:** `fort_expanded_vats`
  - **Stat:** `FortExpandedVatsPercent` (+80% to +200%)
  - **Spec:** Expands oil pool radius.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/BurningOilDefense.cs`

---

## Group 9: Fortress Upgrades - Spike Barricades
- [x] **Task 34: Spike Barricades Concussive Stun**
  - **ID:** `fort_concussive_spikes`
  - **Stat:** `FortConcussiveSpikesDuration` (0.5s - 2.0s)
  - **Spec:** Barricade impacts stun enemies.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/SpikeDefense.cs`

- [x] **Task 35: Spike Barricades Shove**
  - **ID:** `fort_shove`
  - **Stat:** `FortShoveInterval` (7s down to 4s)
  - **Spec:** Barricade periodically knocks back enemies toward spawn.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/SpikeDefense.cs`

- [x] **Task 36: Spike Barricades Vulnerability Field**
  - **ID:** `fort_vulnerability_field`
  - **Stat:** `FortVulnerabilityFieldBonus` (+50% to +200%)
  - **Spec:** Enemies in barricade zone take increased damage from all sources.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/SpikeDefense.cs`

---

## Group 10: Fortress Elemental Combos
- [x] **Task 37: Fortress Electrified Oil**
  - **ID:** `fort_electrified_oil`
  - **Stat:** `FortElectrifiedOil`
  - **Spec:** Combines Boiling Oil with Lightning to shock enemies standing in oil.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/BurningOilZone.cs`

- [x] **Task 38: Fortress Permafrost Spikes**
  - **ID:** `fort_permafrost_spikes`
  - **Stat:** `FortPermafrostSpikes`
  - **Spec:** Combines Spikes with Ice to chill and slow impaled enemies.
  - **File:** `Assets/Bladehold/Bladehold Scripts/Fort/SpikeDefense.cs`
