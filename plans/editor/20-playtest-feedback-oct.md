# Editor to-do: plan 20, playtest feedback (2026-10-06)

Editor leftovers from `plans/20-playtest-feedback-oct.md`. Unity MCP was connected for Phase 2, and everything compiles clean (Unity + `dotnet build`). Tick items off as you go and delete the file when it's empty.

## Phase 2: Quick text, data and audio fixes

**Done in code and assets:**
- **Death text:** `DeathScreen.HandlePlayerDied` no longer hardcodes "YOU DIDN'T HOLD THE DOOR" in gate battles. Every player death uses `death.player_title`, now "The Hero Has Fallen" in all 9 languages. The gate-destroyed title and reason are unchanged.
- **Golden Goblin:** parked, not deleted. Its `WaveObjectives.csv` row is `draftable=false, demoEnabled=false`, so wave cards never offer it. The component, its waypoints and the script stay in place.
- **Tutorial hint (`Bladehold HUD.prefab` → TutorialHintUI):**
  - The Show and Complete scale pop peaked at 2.5x (RemapCurveOne 2). It's now 1.1, so the peak is about 1.15x.
  - The complete chime is at volume 0.35 and pitch 0.9 (was 1 / 1).
  - The counter bump is 15–20 (was 40–60).
- **Rope snap:** imported `FABRIC_Tear_05_Quick_mono.wav` (Universal Sound FX) into `Bladehold Audio/SFX/Tutorial/`. It replaces the wood snap on Rope_1–3's SnapMMF in `Bladehold Tutorial Dungeon.unity`, with ±5% pitch.
- **Fishing level-up:** new `FishingManager.levelUpFeedback`. It plays once per catch that crosses a level. Wired to a new `FishingMinigameManagers/LevelUpMMF` (copied from FrenzyStartMMF, `SuccessBig.wav` at 0.8, unscaled) in `Bladehold Fishing Pond.unity`.
- **Hover sound only once:** every generated HoverMMF had a 0.06 s cooldown on *scaled* time. Menus run at timeScale 0, so the cooldown never expired after the first hover. Set to unscaled (player + feedbacks):
  - prefabs: MetaPerkCard, RebindRow, ShopSlotPrefab, PauseMenuCanvas, ShopUI
  - scenes: Meta Area, MainMenu, Demo, Test, Enemy Zoo
  - `BladeholdUIKit.SetFeedbacks` and `SettingsPanelBuilder.CloneHoverFeedback` now generate them unscaled.

**Wiring checklist:**
- [ ] Optional: remove `GoldenGoblinObjective` from `SurvivorsObjectives.prefab`'s `repeatingObjectiveComponents`. It's still in the random fallback pool, which only runs when a wave has no card. Or delete the component, its `GoldenGoblinWaypoints` child, the copy in `Bladehold Survivors Scene.unity` and the script, if it's gone for good.

**Manual verification:**
- [ ] Die in a gate battle: the title reads "The Hero Has Fallen" and the subtitle "The hero has fallen. All hope is lost." Let the gate fall: still "The Gate Has Fallen".
- [ ] Draft a few waves: the Golden Goblin card never shows up.
- [ ] Tutorial: the hint panel pops gently when a hint appears or completes, and the complete chime is soft. Too quiet? Raise `CompleteMMF`'s sound volume on the HUD prefab.
- [ ] Tutorial Dungeon: shooting each rope plays a quick tear, not a wood crack.
- [ ] Fishing Pond: a level-up plays the sting once, including when a Fishsploshion chain crosses two levels. It isn't drowned out by the draft cards' sound.
- [ ] Meta Area → Spirit (Sanctuary of Spirits): every hover ticks, not just the first. Same in the pause menu's Settings, the shop and the main menu settings.
