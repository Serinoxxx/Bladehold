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

## Phase 3: Combat ability fixes

**Done in code and assets:**
- **Blazing Trail:** `PlayerDodge` read the static Player root's position, so the trail, dash hits and Frost Step all happened back at the spawn point. They now use the moving body (`PlayerDodge.Body`). Benchmark section 6 covers it.
- **Earthshaker / Colossal Smash:** `MaceShockwaveMMF` on `Player.prefab` spawns `FX_GroundCrack_Blast_01`. `MaceCombatController` places it at the struck enemy's feet, on the ground.

**Wiring checklist:** nothing left.

**Manual verification:**
- [ ] Draft Blazing Trail, walk away from the spawn point, then dash: a line of fire stays along the dash path for about 3 s and burns goblins that walk through it.
- [ ] With Nimble Strike or Frost Step, dash into a pack away from spawn: the hits and chill land where you dashed.
- [ ] Dash with movement input: the character faces the dash direction and doesn't jump sideways.
- [ ] Mace + Earthshaker, fully charge a swing into a goblin: the ground crack opens at its feet (try on a slope and on a bridge), not floating mid-air. Too small or too big? Scale or swap it on `Player.prefab` → `MaceShockwaveMMF`.
- [ ] Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark (from MainMenu, don't save the scene after): section 6 passes.

## Phase 4: HUD and prompt readability

**Done in code and assets:**
- **Ultimate meters** (`Bladehold HUD.prefab` → Bottom/Ult Meter, Ult Meter Ranged): 75% scale, flanking the ability cluster at x ±680 instead of on top of it.
- **Interact prompt** (`InteractionPrompt.prefab`, new `UI/InteractionPromptView`): a dark plate with the Interact glyph and the text. `DeniedFeedback` (shake, red flash, Denied sound) plays when `IAffordableInteractable.CanAfford` is false: wall workbench, ammo chest, tower resupply/Lv-up, gate repair, Meta pedestals.
- **Shift glyph:** the tutorial hint glyphs use the stretch keycap, so "Shift" fits. `InputGlyph` drops "Left"/"Right" from modifier keys on prompts.
- **World event banner** (`WorldEventBanner.prefab`): Banner 0.62 scale at y -92. The chip moved to y -478.
- **Meta currencies:** spends go through `RunSession.SpendGoblinBlood` / `SpendOrcishMetal`, which raise the HUD's change events.
- **Campaign map:** the metal chip's icon is now the iron ingot (`Bladehold Campaign Map Scene.unity` → CurrenciesBar/Chip_Metal/Icon).
- **Currency panel** (`Bladehold HUD.prefab` → ScreenSpace/Currency Panel, new `UI/HudCurrencyPanel`): under the minimap, or in the top-right corner when there's no minimap.

**Wiring checklist:** nothing left.

**Manual verification:**
- [ ] UI review at 16:9 and 16:10/ultrawide: ultimate orbs beside the ability icons, nothing overlapping. Currency panel under the minimap, event chip under the panel. Prompt plate readable on bright snow and dark dungeon floors.
- [ ] Get an ultimate (both slots if you can): the orbs sit left and right of the ability cluster, charge % readable, and the full glow doesn't spill onto the icons.
- [ ] Walk up to a wall workbench, ammo chest, tower and the gate repair spot: the prompt shows the E keycap (gamepad: X/West icon after touching the pad) and no "[E]" text.
- [ ] With too little supply or gold, press E there: soft denied sound, short shake, red pulse, nothing bought. With enough: buys as before. Volume wrong? `InteractionPrompt.prefab` → DeniedFeedback → sound.
- [ ] Meta Area: walk up to a weapon/armour/mount you can't afford: the prompt now shows; E gives the denied pulse.
- [ ] Meta Area: buy a perk (blood) and unlock a pedestal or tier (metal): the HUD counters drop immediately.
- [ ] Tutorial Gate horse steps: "Hold to charge" shows a "Shift" keycap, and the gamepad shows the stick-press icon.
- [ ] Trigger a world event (DevConsole): the banner is smaller, sits at the top, and leaves the middle of the screen clear. The timer chip sits under the currency panel.
- [ ] Campaign map: the third chip shows an ingot and your metal count. If Lance meant something else by "doesn't show metal", follow up.
- [ ] Meta Area / Rest Area (no minimap): the currency panel sits in the top-right corner, not floating below an empty gap.

## Phase 5: Audio state

**Done in code:**
- **Horse gallop behind menus:** `Horse/HorseHoofbeatAudio.cs` pauses the gait loop while time is frozen (every menu sets `Time.timeScale = 0`) and resumes it afterwards. Freezes shorter than `freezeGraceTime` (0.15 s, for hitstop) are ignored.

**Wiring checklist:** nothing left.

**Manual verification:**
- [ ] Ride the horse at a gallop and press Esc: the hoofbeats stop within a moment. Unpause while still riding: they come back at the same pace.
- [ ] Gallop into a level-up/draft card pick, the Rest Area shop and an enemy intro: silent while each is open, back on close.
- [ ] Trample goblins at full charge (hitstop feedbacks): the gait doesn't stutter. If it does, raise `freezeGraceTime` on the horse prefab's `HorseHoofbeatAudio`.

## Phase 6: Flow and screen polish

**Done in code and assets:**
- **Thanks for playing screen:** `DemoEndScreen.prefab` restyled by **Bladehold > UI > Restyle > Demo End Screen** (`Editor/ScreenRestyleBuilder.cs`). The Steam URL is set in `Resources/DemoConfig.asset`, so the Wishlist button shows. A pad starts on Wishlist.
- Rest Area exit door: done by Lance.

**Wiring checklist:** nothing left.

**Manual verification:**
- [ ] UI review at 16:9 and 16:10/ultrawide: the window sits centred over the dimmed campaign map, the flourishes stay inside the frame, and the body text doesn't clip. Also check it in a longer language (DE/FR, if the screen ever gets localised; its text is authored on the prefab today).
- [ ] Clear the demo cutoff tier (or DevConsole): the screen appears and Wishlist on Steam opens the Bladehold store page (Steam overlay in a Steam build, otherwise the browser).
- [ ] Gamepad: Wishlist is highlighted on open, left/right moves to Play Again, A on Play Again returns to the Meta Area. Hover glow shows only on the focused/hovered button.


## Phase 7: Gate-destroyed cinematic

**Done in code and assets:**
- `Waves/GateFallCinematic.cs` sits on `CameraRig.prefab` → `Gate Fall Cinematic`, with its own `Gate Fall Camera` and `SlowMotionMMF` (deep boom and slow whoosh). Tunables are in `Waves/GateFallCinematicConfig.asset`.
- `DeathScreen` plays it before the gate failure screen, and ignores a player death while it runs.
- `EnemyIntroUI.ShowLetterbox` gives the bars without the name band.
- `GateDestructionMMF` now spawns the scaled `FX_Fire_Explosion_Large_01`, so it plays in slow motion.

**Wiring checklist:** nothing left.

**Manual verification:**
- [ ] Let the gate fall in Outer Gate: a hard cut to the gate, the HUD gone, letterbox bars, the explosion in slow motion for about 3.5 s, then the "gate was destroyed" banner and defeat screen. Return to Sanctuary restores normal speed.
- [ ] Graveyard: the green fog washes the fireball out at the default 22 m to 17 m push-in. Judge it, and shorten `startDistance`/`endDistance` on the config if it reads poorly. (The config is shared, so check Outer and Desert Gate after.)
- [ ] Letterbox bars are the boss intro's thin translucent ones. Decide if the gate cinematic wants heavier bars.
- [ ] Die to an enemy during the slow-mo: still the gate failure screen, never "The Hero Has Fallen".

## Phase 7: Horse stall at the gate

**Done in code and assets:**
- `Horse/HorseStall.cs` on `Economy/HorseStall.prefab` (market stall, hitching post, hay; `PurchaseMMF` plays coins and a neigh). Placed outside the gate in Outer Gate, Desert Gate, Graveyard and Tutorial Gate (positions hand-tuned in the Editor by Lance). The generator places it via the `placeHorseStall`/`horseStallPosition` spec fields at (-9, 6); the Tutorial Gate spec still has it off, so a regenerate would drop or move the hand-placed stalls.
- During prep, [E] rebuys a dead warhorse for 500 in-run gold (`RunSession.ReplaceMount`). While the horse lives or gold is short, the press is refused with the denied pulse. Hidden mid-wave and where `SceneAbilityRules` forbid the mount.
- Prompt strings `horse_stall.*` are localised; the fallen-horse HUD hint now says "at the stall or shop".
- Checked in Play mode (Outer Gate) through `execute_code`: the alive, short-of-gold, buy and second-press cases; the prompt on screen; hidden mid-wave. Stall anchor is on the NavMesh in all three scenes.

**Wiring checklist:** nothing left.

**Manual verification:**
- [ ] Let the horse die in a gate battle, collect 500 gold, walk to the stall during prep and press E: coin and neigh sounds, gold drops by 500, the horse bars come back full and you can summon it.
- [ ] Press E with too little gold, and again with the horse alive: red denied pulse, no gold spent.
- [ ] The stall canopy sits close to the player camera when you stand at the counter. Judge whether it blocks the view; if so, nudge `horseStallPosition` or the prefab's anchor.
