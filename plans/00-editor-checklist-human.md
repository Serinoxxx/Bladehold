# 00: Editor checklist (Lance)

Things that need your hands, eyes or judgement in the Unity Editor. Items marked *(MCP-able)* could be done by an agent through Unity MCP if it's connected; your call.

**Per-plan Editor work** lives in [`plans/editor/`](editor/), one checklist per plan. Open ones:
- [02: Campaign Map](editor/02-campaign-map.md)
- [03: Elemental drafts](editor/03-elemental-draft.md)
- [04: Fishing Pond](editor/04-fishing.md) (the pond has no exit until this is done)
- [05: Balance Tree Editor](editor/05-balance-tree.md)
- [06: Sector difficulty + roster](editor/06-sector-difficulty.md) (Fraglob captain has no prefab yet)
- [07: Demo gating](editor/07-demo-gating.md) (demo end panel needed in the Campaign Map scene)
- [08: Legacy cleanup](editor/08-legacy-cleanup.md) (run the one-shot cleanup tool first; it also reserializes the binary scenes in §B)
- [09: Visuals + MMF](editor/09-visuals-mmf.md) (tower build feedbacks empty until wired)

## A. Triage first (biggest win)

- [x] ~~Triage `TODO.md`~~: scrapped Sep 2026 (old contents are in git history). `TODO.md` is now your own list, and agents don't write to it.
- [x] **Decide demo scope details for plan 07** (decided 2026-09-25, recorded in plan 07):
  - Which weapons (sword + bow only? + axe?), which armours, how many tier-1 perks.
  - Where the campaign cuts off: which tier, and what the player sees there (a "Thanks for playing / wishlist" screen?).

## B. Scenes and data

- [ ] **Re-serialize the binary scenes as text.** The project is set to Force Text, but the castle scenes, `Necromancer Crypt` and `Princess Sanctuary` are binary, so agents can't read or diff them. Select them → right-click → *Reserialize*, or use `AssetDatabase.ForceReserializeAssets`. *(MCP-able)*
- [ ] **Verify each castle scene has a working sector loop.** Enter Play mode directly in each one: GameLoopManager, SurvivorsSpawner, objectives, TowerPlots, BuildWheelUI, DeathScreen, baked NavMesh. The agents couldn't inspect them.
- [x] ~~**Captain prefabs**~~: moved to [`editor/06-sector-difficulty.md`](editor/06-sector-difficulty.md) (Fraglob needs a prefab; the empty slot now logs an error).
- [ ] **Campaign Map scene**: the prefabs and graph asset are in (`fa185dc7e`). Give the node button and path line prefabs a UI review.
- [ ] **Pacing asset** `Bladehold Config/SurvivorsRoundPacingConfig.asset`: plan 06 moved the enemy mix into `Enemies.csv` (`minThreat` + `unlockWave`). Review that curve with the playtest in `editor/06-sector-difficulty.md`.
- [x] ~~**Meta Area** mount pedestals~~: moved to [`editor/07-demo-gating.md`](editor/07-demo-gating.md).
- [ ] **Necromancer Crypt**: after plan 02 moves the confrontation trigger into runtime code, check that the trigger object and volume sit where you want in the crypt.

## C. Build sanity

- [ ] **Make a real player build** and play the loop in it; several bugs only show up there. There's at least one known case of Editor-only code compiled out of builds (the Necromancer confrontation), and a "fixed draft upgrades not working in build" commit shows this has bitten before. Do this after Phase 1 and again every few weeks.
- [ ] Check the Steam build profile and PC renderer settings; decide whether the Mobile renderer stays.

## D. Human review gates (recurring)

- [ ] **UI sign-off** for every agent-made mockup flagged under "Needs Lance in the Editor": layout, readability at 1080p/Steam Deck, Texturina headers / Grenze body, controller focus order.
- [ ] **Game feel:** tune MMF timings, screenshake and hit feedback after agent migrations (plan 09). Agents wire it up; you make it feel good.
- [ ] **Art/audio picks:** sounds, icons, VFX found via Asset Inventory. Agents can shortlist; you choose.

## E. Playtest checklist (repeat after each plan)

- [ ] Meta → Battle Portal → Campaign Map (after plan 01: goes straight to the map).
- [ ] Each node type once: Combat, Fishing, Rest Area, PreBoss.
- [ ] Prep phase: banner pick, build each of the 6 towers, refill, upgrade.
- [ ] Each objective type once (DevConsole objective select helps).
- [ ] Draft each category: weapon, element (does the weapon glow?), ultimate.
- [ ] Die mid-sector → Meta, run fully wiped, currencies kept.
- [ ] Clear a sector → map → next node: HP, gold, supply, drafts, ultimate carried; towers refunded as supply (after plan 01).
- [ ] Controller-only pass (once Phase 3 starts).
