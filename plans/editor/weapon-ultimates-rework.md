# Editor to-do: weapon ultimates rework (Moonlight Edge, Seismic Quake, Axe Storm)

2026-10-04. Sword, mace and throwing-axe ultimates reworked so each owns a different job next to the axe's Whirlwind (sustained AoE around you):

- **Moonlight Edge** (sword, card `sword_blade_tempest`): every swing fires a `MoonlightCrescent` wave in a line; charged swings fire bigger, harder crescents. `Player/SwordMoonlightEdgeUltimate.cs`, `Player/MoonlightCrescent.cs`, `Config/Ultimates/MoonlightEdgeConfig.asset`.
- **Seismic Quake** (mace): leap toward the camera's facing (damage-immune in the air), slam, a shockwave ring that spreads outward to 10 m (hits, launches, stuns 2.5 s with stars), then aftershocks on every swing. `Player/MaceUltimate.cs`, `Config/Ultimates/SeismicQuakeConfig.asset`.
- **Axe Storm** (throwing axe, card `taxe_vortex_ult`): fast three-axe fans whose axes ricochet to 2 more enemies (9 m search). The circling axes are gone. `Player/ThrowingAxeUltimate.cs`, ricochet in `AxeProjectile.TryRicochet`, `Config/Ultimates/AxeStormConfig.asset`.

New upgradeable stats: `UltimateMoonlightCrescentDamage`, `UltimateMaceAftershockDamage`, `UltimateAxeRicochetCount`. `DamageTrigger.OnActivated` (fires once per swing) is the new hook for "do X on every swing".

## Done by an agent via MCP

- [x] **Assets:** the three config SOs (the mace and axe ones were converted in place from the old configs, so the GUIDs are unchanged), `Bladehold Prefabs/VFX/Ultimates/` (`MoonlightCrescent` and timed one-shot bursts built from POLYGON Particle FX: `MoonlightActivateBurst`, `MoonlightHitSpark`, `QuakeGroundCrack`, `AxeRicochetSpark`, `AxeStormActivateBurst`; looping auras `MoonlightBladeAura`, `QuakeAura`, `AxeStormAura`, `AxeRicochetTrail`).
- [x] **Player.prefab:** `SwordMoonlightEdgeUltimate` added (disabled); `MaceUltimate` now disabled like the other handlers. New MMF players under `SidekickSyntyCharacter`: `MoonlightActivateMMF`, `MoonlightCrescentMMF`, `MoonlightCrescentHitMMF`, `MoonlightEndMMF`, `MaceLeapMMF`, `MaceQuakeEndMMF`, `AxeStormActivateMMF`, `AxeRicochetMMF`, `AxeStormEndMMF`. `MaceSlamMMF` gained a ground-crack burst; aftershocks reuse `MaceShockwaveMMF`. Auras: blade aura on `prop_r`, quake and storm auras on the character.
- [x] **AxeProjectile.prefab:** red `AxeRicochetTrail` child shown only on ultimate throws.
- [x] **Play-checked (Graveyard):** crescent hits goblins 5 m and 10 m ahead and misses one off to the side; the leap covers 6 m with a 2.3 m arc; the ring hits, stuns and puts stars on goblins in range; a swing during the buff makes a 30-damage aftershock; an axe hits, bounces to a goblin 5 m away and won't bounce to one 15 m away. Console clean.

## Art and audio pass (placeholders chosen from what's in the project)

- [ ] **Watch each one in Play.** DevConsole → Ultimate ◄/► → **Unlock Ult** → **Fill Charge 100%** → Q (aim first for Axe Storm). Equip the matching weapon first.
- [ ] **Crescent look.** `MoonlightCrescent.prefab`: two flat swipe quads (`Visual`, `VisualCore`, rotation (90, 50, 0)) plus a sparkle trail, pale moon blue. Check it reads from the gameplay camera; it's flat at chest height (`height` on the config).
- [ ] **Sound picks.** Moonlight: `magic_flame_of_light_04` start, sword whooshes per crescent, blade impacts on hit. Quake: heavy armour foley on take-off, existing slam rock impact. Axe Storm: magic poof start, blade impacts on ricochet. Worth proper assets: a **metallic ricochet ping** (Asset Inventory: Universal Sound FX `IMPACT_Incoming_Kinetic_Ricochet_01`), a **ringing moon chime** for Moonlight Edge, an **earthquake rumble** for the slam (`EXPLOSION_Medium_Debris_Rumble`). *`/find-and-import-assets` can import these on your OK.*
- [ ] **Screenshake.** `MoonlightActivateMMF` and `AxeStormActivateMMF` were cloned from the mace slam, so they carry its full Cinemachine impulse. Probably too strong for the sword and axe; lower or remove it.
- [ ] **Leap animation.** The leap uses the Synty fall/land poses (locomotion is paused mid-air). A proper jump-slam clip would sell it: add a trigger to `Player AC` and put its name in `MaceUltimate.slamAnimTrigger`.

## Playtest and balance

- [ ] **Crescent damage:** 1.2 × SwordDamage, up to 2× at full charge, every swing for 8 s, piercing. Check it doesn't outclass Whirlwind on crowds.
- [ ] **Quake:** 90 slam damage and 2.5 s stun in 10 m, then 30-damage aftershocks (4 m, 0.6 s stun). The leap goes where the camera faces, not toward enemies; check that feels right.
- [ ] **Axe Storm:** each bounce starts a fresh leg (full pierce budget). A ricochet fires on the first hit, so charged throws bounce instead of piercing during the storm.
- [ ] **Old asset:** `orbitBladePrefab` (the old circling blade prefab) is now unused; delete it if nothing else wants it.
