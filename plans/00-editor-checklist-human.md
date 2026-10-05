# 00: Editor checklist (Lance)

Things that need your hands, eyes or judgement in the Unity Editor. Items marked *(MCP-able)* could be done by an agent through Unity MCP if it's connected; your call.

**Per-plan Editor work** lives in [`plans/editor/`](editor/), one checklist per plan. Open ones:
- [02: Campaign Map](editor/02-campaign-map.md)
- [03: Elemental drafts](editor/03-elemental-draft.md)
- [04: Fishing Pond](editor/04-fishing.md) (the pond has no exit until this is done)
- [05: Balance Tree Editor](editor/05-balance-tree.md)
- [06: Sector difficulty + roster](editor/06-sector-difficulty.md) (Fraglob captain has no prefab yet)
- [07: Demo gating](editor/07-demo-gating.md) (§6: shorter demo (tier-3 cutoff, 3 waves) and first-run gift need a playtest)
- [08: Legacy cleanup](editor/08-legacy-cleanup.md) (cleanup tool already run by an agent, 2026-09-26)
- [09: Visuals + MMF](editor/09-visuals-mmf.md) (tower build feedbacks empty until wired)
- [15: Wave choice draft](editor/15-wave-choice.md) (wave card UI review; flow still uses banners until Phase C)
- [Ammo chest + Hold the Gate top-up](editor/ammo-chest.md) (prefab built and placed in the defense scenes; castle scenes left)
- [Captain Mogra Hexfang + dash i-frames](editor/captain-mogra.md) (built and play-checked; needs your audio, animation and balance pass)
- [Towers vs hero: blind spots, Sapper, Dome Warden](editor/towers-vs-hero.md) (Sapper + Dome Warden prefabs not generated yet)
- [Save slots](editor/save-slots.md) (built and play-checked; needs your UI review)
- [Skeleton crowd enemies](editor/skeleton-crowd-enemies.md) (generated and baked; needs your Play-mode check and weapon poses)
- [Troll sliding / warping](editor/troll-movement.md) (agent double-scale fixed in code; walk-speed sync and a playtest left)
- [Enemy spawn/death prewarm](editor/enemy-prewarm.md) (wired by YAML, not yet opened in the Editor; needs a hitch check and a shader-variant re-collect)
- [17: Walls, upgrade wheel, crystals](editor/17-walls-and-upgrade-wheel.md) (built and play-checked; the wall is now one hand-placed gatehouse model that needs fitting per scene; MMF content and UI are placeholders for your pass)
- [Battlefield minimap](editor/battlefield-minimap.md) (built and play-checked in Outer Gate; needs your UI review, other gate scenes and two decisions)
- [Settings menu restyle](editor/settings-menu-restyle.md) (built and play-checked; needs your UI review and two small decisions)
- [Spearman (anti-cavalry Big Ork)](editor/spearman.md) (generated, poses play-checked; benchmark, attack clip and a mounted playtest left)
- [Campaign map diorama](editor/campaign-diorama.md) (generated and screenshot-checked; needs a real Game-view look, UI review and a playtest)
- [UI theme + menu consistency](editor/ui-theme-consistency.md) (shop, Spirit window and pedestals rebuilt in the settings style with swappable themes; needs your UI review and a playtest; recovered scene backups in `Assets/_Recovery/` to triage)
- [Directional swings + armed arm pose](editor/directional-swings.md) (built and Animator-checked; needs your feel playtest, threshold tuning and a look at the carry pose)
- [Mount charge, horse loss, horse HUD](editor/mount-charge-and-hud.md) (built and play-checked; **decide horse max HP** (12 is fragile now that loss is permanent), plus feedback slots and UI review)
- [18: Biome world events](editor/18-world-events.md) (all six built and play-checked; needs your UI review, sound/art picks and a balance playtest)
- [19: Music](editor/19-music.md) (built and play-checked; needs listening pass and balance check)

## A. Triage first (biggest win)

- [x] ~~Triage `TODO.md`~~: scrapped Sep 2026 (old contents are in git history). `TODO.md` is now your own list, and agents don't write to it.
- [x] **Decide demo scope details for plan 07** (decided 2026-09-25, recorded in plan 07):
  - Which weapons (sword + bow only? + axe?), which armours, how many tier-1 perks.
  - Where the campaign cuts off: which tier, and what the player sees there (a "Thanks for playing / wishlist" screen?).

## B. Scenes and data

- [ ] **Re-serialize the binary scenes as text.** The project is set to Force Text, but the castle scenes, `Necromancer Crypt` and `Princess Sanctuary` are binary, so agents can't read or diff them. Select them → right-click → *Reserialize*. **Note (2026-09-26):** an agent tried `EditorSceneManager.SaveScene` and `AssetDatabase.ForceReserializeAssets` via MCP and the 9 files stayed binary, so this needs a look in the Editor (try the right-click Reserialize, or *Save As* a new file and swap it in).
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
