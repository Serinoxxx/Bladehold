# Editor to-do: playtest feedback, quick tuning batch (2026-10-08)

Group 1 of Lance's 15-item playtest list, plus the "Ammo Crate" wall upgrade. Tick items off as you go, and delete the file when it's empty.

**Done in code and assets:**
- **Draft and wave card hover:** the hover-enter/exit Scale Spring bump on `UI/Card.prefab` and `UI/WaveCard.prefab` is down from 20–30 to 8–12. The click punch (select feedback) is unchanged.
- **Thunderstorm shake:** the strike feedback's Cinemachine impulse velocity on `WorldEvents/WorldEvents.prefab` is halved (5 → 2.5).
- **Tower build sound:** the piece-landing hammer on `Defenses/DefenseAssemblyAnimation.prefab` is up from 0.7 to 1.0 volume. In a multi-piece build (arrow tower), the last piece now plays the heavy slam feedback (`DefenseAssemblyAnimation.DropPiecesRoutine`). This also gives that build a stronger shake on the final piece.
- **Delay before the draft:** `WaveChoiceConfigSO.draftDelayAfterClearSeconds` (default 1.25 s) runs after the rout ends, or straight away if nothing is left alive, before the wave resolves and the draft cards fade in (`GameLoopManager.RoutRoutine`).
- **Fleeing announcement:** `GameLoopManager.OnRoutStarted` fires when the stragglers break. `WaveClearedBannerUI` follows the Quest Complete/Failed banner (after at most 1.5 s) with a big "THE GOBLINS FLEE!" banner, "Hunt down N before they escape (Xs)", and the new-quest horn. Strings are `wave.rout.banner_header` and `wave.rout.banner_sub` in `Strings.csv`.
- **Wall material upgrade repairs:** current HP goes up by the max-HP gain (a 60/150 wall upgraded to 300 max ends on 210) (`WallStructure.BuildUpgradeOptions`).
- **Ammo Crate wall upgrade:**
  - Supply cost: `FortUpgradeConfigSO.wallAmmoCrateCost` (30).
  - It puts `wallAmmoCratePrefab` (the gate `AmmoChest` prefab) on the wall walk. It goes at the wall's optional `ammoCrateAnchor`, or else is dropped by raycast onto the model's Environment floor beside the gate.
  - It hides when the wall collapses, is refunded on Deconstruct, and is listed on the minimap wall tooltip.
  - The icon falls back to `refillIcon` until `ammoCrateIcon` is set.

**Wiring checklist:**
- [x] `Config/Fort/FortUpgradeConfig.asset`: `wallAmmoCratePrefab` = `Economy/AmmoChest.prefab` (the option stays hidden while this is unset).
- [ ] Optional: set an `ammoCrateIcon` (a quiver or arrow-crate silhouette) on the same asset.
- [ ] Optional: if the automatic placement lands somewhere odd on a hand-fitted wall, add an `AmmoCrateAnchor` child to `Wall.prefab` (or a per-plot override) and assign `ammoCrateAnchor`.

**Manual verification:**
- [ ] Hover the draft cards and the wave choice cards (mouse and gamepad focus): a gentle bump, no wobble.
- [ ] Thunderstorm world event: the strike shake is noticeably softer.
- [ ] Build an arrow tower: the hammer thuds are clearly audible and the last piece lands with the slam. Check that the other towers still sound right.
- [ ] Kill the last enemy of a wave: there's a ~1.25 s beat before the draft cards fade in.
- [ ] Resolve an objective with enemies still alive: Quest Complete shows, then the "THE GOBLINS FLEE!" banner with the live count and the seconds left.
- [ ] Damage a wood wall, then upgrade it to stone: HP goes up by the full 150 (capped at max), and the description says so.
- [ ] Wall upgrade wheel → Ammo Crate:
  - The crate appears on the walkway (not floating, not inside the battlements).
  - [E] on it from the walkway buys arrows.
  - It's not usable from the ground below.
  - It vanishes when the wall falls.
  - Deconstruct refunds the 30 supply.

## Brute and Bulwark attacks that miss

**Done in code:** `AIAttack` checks the target again when the blow lands, but with leeway: `AIAttackSO.apexRangeBonus` (0.75 m extra reach) and `apexConeAngle` (75°, never narrower than the start cone). The start-of-attack check is unchanged.

Why they missed:
- **Brute:** it uses the goblin `AIAttackSO` (2 m range), which equals its NavMeshAgent stopping distance, so it started swings right at the edge and any step back put the player out of range.
- **Bulwark:** 0.9 s wind-up, a 40° cone and slow turning (paused mid-swing), so any sidestep put the player outside the cone.

This applies to every `AIAttack` enemy, so plain goblins also land slightly more often.

**Manual verification:**
- [ ] Stand still or back-pedal slowly in front of a Brute and a Bulwark: their swings land.
- [ ] Sprint clear or roll round the side during the wind-up: you still dodge them.
- [ ] Watch whether the hit lands in time with the clip's impact frame. If the Bulwark's damage lands visibly before or after the shield/weapon connects, retune `windupToApex` on `Enemies/Bulwark/AIAttackSO Bulwark.asset` (0.9). The Brute shares the goblin's 0.4 s on `Enemies/AIAttackSO.asset`; if its heavier clip's impact is later, give it its own SO.
- [ ] Goblin pressure doesn't feel noticeably harsher. If it does, lower `apexRangeBonus` on `AIAttackSO.asset`.
