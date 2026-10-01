# Editor to-do: plan 16 (Tutorial level)

From [`plans/16-tutorial.md`](../16-tutorial.md), session 1, 2026-09-30. Unity MCP was connected, and everything compiles clean (Unity + `dotnet build`). Tick items off as you go and delete the file when it's empty.

**Done by an agent via MCP** (all built and wired, and the whole flow was play-tested by driving it from code):
- **Scenes:** `Bladehold Tutorial Dungeon` (T1), `Bladehold Tutorial Arena` (T2) and `Bladehold Tutorial Gate` (T3). All three are in Build Settings.
  - T1/T2 are 5 m Synty dungeon kit shells (floors, walls, ceilings, doorframes). Their dressing is copied from the dungeon demo's Cellar, Barracks, Torture Room and Center Hall props.
  - Whole demo rooms weren't copied: they overlap across floors and levels, so they don't lift out cleanly.
  - T3 was generated **once** from `Config/SceneGen/TutorialGate_DefenseSpec_GENERATED_ONCE.asset` + `Kingdom_DefensePalette.asset` (validator PASSED), then the tutorial layer was placed on top. **Don't regenerate it.**
- **HUD prefab:** `TutorialHint` panel (`TutorialHintUI` + `FirstTimeHintsWatcher`) with Show/Complete/Counter MMF players (cloned from existing HUD feedbacks).
- **PauseMenuCanvas:** `SkipTutorialButton` (clone of Quit), wired to `PauseMenuView.skipTutorialButton`.
- **MainMenu:** `Button_ReplayTutorial` (clone of Settings) calls `OnReplayTutorialClicked`.
- **Assets:**
  - `Resources/TutorialConfig.asset`;
  - `Config/Tutorial/` (banner/door/rope HealthSOs, `TutorialRoundPacingConfig`, `TutorialDungeonVolume`);
  - `BowSO.asset` `chargeTimePerLevel` 0 → 0.333.

**Verified in Play mode (driven from code):**
- T1: the move trigger works. Taps cut the banners. The door refuses taps, arrows and a 60% charge, and breaks at 85%. Quick-release arrows cut the ropes, the bridge drops, and the walk collider and barrier swap. The exit unlocks and loads T2.
- T2: goblins spawn on the NavMesh and rush you. Death reloads the arena (no run-over screen). Both dead → the portcullis opens → T3.
- T3: `tutorialCompleted` is set and supply is 60. The build step completes on a build. Ready → exactly **20 plain goblins** (1 skull, no clan). Victory → Campaign Map with a fresh run (tutorial gold dropped).

## Play it yourself (the part code can't judge)

- [ ] **New Game from `MainMenu` with a fresh save** (Settings → Delete Save): it should land in T1. Your current save predates the tutorial, so it counts as completed.
- [ ] **T1 by hand:**
  - Does the heavy-door threshold (80% charge) feel right?
  - Is the rope shot readable?
  - Try to jump or dodge across the gap before the bridge drops. The invisible `GapBarrier` should stop you, and arrows pass through it.
  - Empty the quiver: the "take more from the crate" line and the crate waypoint should appear.
- [ ] **T2:** is the fight fair for a first-timer? The dodge hint shows **Space / B**; confirm those are the real binds on your pad.
- [ ] **T3:**
  - Build on the marked plot and hold T. Does a single arrow tower + you hold 20 goblins comfortably?
  - Die once: you should go to the Meta Area, and the Goblin Blood tip should point at the Spirit.
- [ ] **Skip Tutorial** from the pause menu in each scene → Meta Area. **Replay Tutorial** from the title screen.
- [ ] **Bow feel game-wide:** the 1 s full draw is a global change. Play a normal sector and see if the bow still feels good.

## Art / feel pass (placeholder picks, all yours to tune)

- [ ] **Lighting:**
  - T1/T2 use warm torches (intensity 2, range 10), flat ambient 0.2, and **skybox reflections off**; the default skybox reflection turned every surface flat grey.
  - The volume is `TutorialDungeonVolume` (the demo profile with bloom cut to 0.6 @ threshold 1 and exposure 0). The demo's own profile, bloom 2.5 @ 0.4, washed everything out.
  - Still reads a little pale. Tune to taste.
- [ ] **Torch flames:** `FX_Fire_01` at 0.25 scale. Check the size and position on the sconces.
- [ ] **Dressing:** R1 and C1 are sparse (3 and 0 props) and T2's right side is thin. Add props by hand; the scenes are normal scenes now.
- [ ] **Banners:** `SM_Prop_Wall_Banner_01` at 0.8. The torn stub is the same banner squashed to 20% height. Swap in a better broken piece if you have one.
- [ ] **Locked door:** two `SM_Env_Door_01` leaves + a plank bar. The broken state is rubble planks.
- [ ] **Drawbridge:** `SM_Env_Wood_Bridge_01` swings down over 1 s (ease-in). Its legs poke toward the player while upright. Check the drop timing against the thud.
- [ ] **MMF sounds (2026-10-01):** 8 clips from the Sonniss GDC 2024 bundles (via `sfx-browser-finder`) are in `Bladehold Audio/SFX/Tutorial/` and wired in, headless with Unity closed. Listen to each in Play:
  - Banner cut: `CLOTHRip_CottonRips44`
  - Door refused: `WOODImpt_Drops20` (wood thunk)
  - Door smashed: `WOODCrsh_Designed Wood Crash And Debris 13`
  - Rope snap: `WOODBrk_Snap09`, pitched ×1.3. The library has no rope sound, so this is a wood snap.
  - Bridge drop: `WOODBrk_Tree Crack and Fall-13` (4.6 s, crack → fall → ground hit), plus the old deep thud 1 s in. **The two may double up**: trim the clip or drop the thud.
  - Portcullis lift: `METLFric_Screeching Metal Rub-11` (3.3 s, longer than the 1.6 s lift). The library has no chain or winch sound.
  - Portcullis slam: `METLImpt_Metal Impact-03`
  - Step-complete chime: `UIMisc_Feedback 36 up` (replaces the ultimate's "Legendary Tier Item A")
- [ ] **Still placeholders:**
  - goblin laugh (arena spawn);
  - whoosh (scene exits);
  - metal gear rattle / wood shield (ammo crate take / denied).
- [ ] **Import settings:** the new wavs are 96 kHz / 24-bit. Their metas are set to Vorbis quality 0.7 with an optimised sample rate. Check they import cleanly when Unity opens.
- [ ] Impulse strengths run 0.3 to 3.5.
- [ ] **T3 scatter:** the field has a lot of boulders for a first battle. Thin it by hand if you want a cleaner view to the spawns.

## UI review (agent-built)

- [ ] **Hint panel** (`Bladehold HUD` → `TutorialHint`):
  - Placement: bottom centre, y 560, scale 1.35.
  - Style: dark Box sprite at 72%, Grenze labels, Texturina counter, glyph + text rows.
  - It must never cover the weapon icons or the WavePrepPrompt at 1080p.
- [ ] **Keys without dedicated glyph art** (E, Space) show a blank keycap with a dark label. Check it's readable.
- [ ] **Replay Tutorial** button position on the title screen. **Skip Tutorial** in the pause menu (shows only during the tutorial).
- [ ] **First-encounter tips** reuse the same panel as an 8 s overlay. When one fires on top of a tutorial step, the step hint comes back afterwards.

## Known gaps

- [ ] No `GateRepairStation` exists in the generated gate scenes, so the gate-repair tip only fires in scenes that have one.
- [ ] Build Settings index 0 is `Bladehold Fishing Pond`, not `MainMenu`. This was already the case; worth fixing before a build.
