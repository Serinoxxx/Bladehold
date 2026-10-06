# Editor to-do: plan 21, external tester feedback (2026-10-07)

Editor leftovers from `plans/21-playtest-feedback-tester.md`. Tick items off as you go, and delete the file when it's empty.

## Phase 1: Quick fixes

Unity MCP was down for this phase. `dotnet build` is clean for runtime and Editor, but nothing has been checked in Unity yet.

**Done in code and assets:**
- **Mouse sensitivity:**
  - Default 0.1, range 0.01–1, and the typed field takes 3 decimals.
  - Old saves on 0.5 move to 0.1. Other values are clamped into the new range.
  - The slider range is forced in code, so the scene and prefab copies of the settings panel don't need editing.
- **Build wheel:** no more second cost line on each option.
- **Wall health bar:**
  - The minimum height is 7.5 (was 5.8).
  - The bar also auto-raises 1.5 m above the top of the wall's tallest mesh, refitted each time it fades in.
- **Tutorial gate music:** prep and scene music now use `Music_Prep` (your Tavern LOOP SLOW edit).
- **Black main menu:** not reproduced. Added a safety net: on the title screen, `Time.timeScale` and the Feel time-scale stack are reset, and a stuck loading screen is force-hidden after 3 s. Both log when they fire.

**Wiring checklist:**
- [ ] Commit your `Music_Prep.asset` change (Tavern LOOP SLOW). The tutorial gate now relies on it being the calm track.
- [ ] Optional: re-run the settings panel builder, so the authored slider in `SettingsPanel.prefab` / `PauseMenuCanvas.prefab` also says 0.01–1 (the code already overrides it at runtime).

**Manual verification:**
- [ ] Settings → Controls → Sensitivity:
  - The slider starts at 0.100 on a fresh save, and 0.075 typed in sticks.
  - Typing 0 clamps to 0.010.
  - Dragging to max gives 1.000.
  - Check this in both the main menu settings and the pause menu settings.
- [ ] An existing save that was on 0.5 opens at 0.100. One set to a custom value (e.g. 2.0) opens clamped at 1.000.
- [ ] Build wheel at a tower plot: each option shows its name once and its cost once, in red when unaffordable. Clicking an affordable option still builds it; an unaffordable one does nothing. Check with a gamepad too.
- [ ] Build a wall, upgrade it through every tier, and look at it from the prep camera and up close:
  - The bar sits clear above the model, including on the gatehouse.
  - It's not absurdly high.
  - If it is too high, lower `clearanceAboveModel` on `Wall.prefab`'s health bar.
- [ ] Tutorial gate prep phase: the calm tavern track plays, not the dark dungeon one.
- [ ] **Black main menu, in a player build:**
  - Quit to the main menu from the pause menu in a battle, in the tutorial gate, during the gate-fall slow-mo, and from the death and victory screens.
  - The title screen should show its background and buttons.
  - Check `Player.log` for `[MainMenuManager]` warnings or errors. Either one names the cause; report it so the real fix can replace the safety net.

## Phase 2: Horse summon feedback

**Done in code and assets** (checked in Play mode via MCP):
- The first 0.5 s of a summon roots the player. Moving after that cancels it.
- A cancelled summon keeps the cast bar up in red with "SUMMONING CANCELLED" and the reason, then fades it.
- The cast bar's label now actually shows. It had been hidden in the prefab.
- The tutorial gate's horse step says "Stand still while summoning".
- Summoning plays the large wave (`A_MOD_EMOT_Greet_Wave_Masc`) on a new upper-body `Summon` layer in `Player AC`.

**Wiring checklist:**
- [ ] Decide whether the **Basic Warhorse** (0.5 s cast) should still be cancellable by moving. With the 0.5 s lock it can't be. Either lengthen its `castTime` or lower `castMovementLockSeconds` on `Mount_BasicWarhorse.asset`.

**Manual verification:**
- [ ] Sprint, press X: the player stops and the summon completes. Keep holding a direction on a longer-cast mount: "SUMMONING CANCELLED / You moved. Stand still to summon" shows in red for about 1 s, then fades.
- [ ] Get hit mid-cast ("You were hit") and press X twice ("You cancelled it"). Both show the message.
- [ ] UI review: the cast bar label sits above the bar, and the small reason line is readable at 1080p.
- [ ] The wave looks right with each weapon in hand. The sword stays in the waving hand. On the 0.5 s Basic Warhorse cast you only see the start of the wave.
- [ ] Tutorial gate horse step shows the "Stand still while summoning" second line.


## Phase 3: Tutorial gating and clarity (T3)

**Done in code and assets** (checked in Play mode via MCP in the Tutorial Gate):
- Every tutorial step except a wave-event step (Ready, Ready2, Wave1, Wave) holds the Ready hold shut. While it's shut, the prep prompt hides the hold row and bar and shows "Finish the lesson first". Pressing T shakes the prompt, flashes that line red and plays the denied sound (`WavePrepPrompt/DeniedFeedback` in `Bladehold HUD.prefab`).
- The build and wall lessons open Ready again only if you can't afford the build, and then finish when the wave starts.
- During each lesson only its plot keeps a BUILD marker: TowerPlot_1 for Build, WallPlot_2 for Wall, none for the horse lessons. The minimap hides the empty-plot rings to match. Ready and the waves show every plot again.
- `StaminaKills` and `ChargeThrough` have `suspendDefenses` on, so towers and spike rings hold fire. Tower and wall kills no longer refill the horse's charge meter in tutorial encounters.

**Wiring checklist:**
- [ ] UI review: the "Finish the lesson first" line uses the objective line's small style. Make it bigger or brighter if it's too easy to miss at 1080p.

**Manual verification:**
- [ ] Play T3 end to end with a real keyboard and pad. Hold T in every prep step from Mount to Wall: the wave must not start, and each press plays the denied shake, red flash and sound. Ready and Ready2 start the wave as before.
- [ ] Build two towers, then do both charge lessons: the goblins survive until you kill them, and only your own kills fill the charge meter.
- [ ] During Build, only TowerPlot_1 has a BUILD marker. During Wall, only the marked workbench does. The horse lessons show no plot markers on screen or on the minimap.
- [ ] Spend your supply below a wall's cost before the Wall step (e.g. with the dev console): Ready opens, and starting the wave moves the tutorial on.

## Phase 4: Valley Stronghold

**Done in code and assets** (checked in Play mode via MCP):
- `Bladehold Tutorial Gate.unity` is renamed to `Bladehold Valley Stronghold.unity`, with the same GUID. Build settings, T2's exit door, the scene-gen spec and the docs all point at the new name.
- New root node `Resources/CampaignNodes/Node_tier1_valley_stronghold.asset` ("Valley Stronghold", threat 1, +50 gold), leading to the Alpine Keep. It has its own map column, "The Valley" (tier 0, no numeral), and is mirrored in `BuildDefaultGraph()`. The map diorama is rebuilt.
- `TutorialSceneMode` (root object of the same name) picks the mode before every other Awake:
  - **Campaign deploy:** switches off `Tutorial`, `TutorialDirector` and the tutorial `SceneAbilityRules` (so ultimates are allowed). The loop and spawner get `ValleyStrongholdRoundPacingConfig`: 5 waves, 30–46 kills (the Alpine Keep has 40–60), 70% fodder, cap 30, drafts and clans on.
  - **Tutorial (from T2, or opened from the Editor):** the scene runs as before.
- `AmmoChest` moved out of the `Tutorial` root, so the campaign mode keeps it.
- Tutorial victory keeps the run (gold, supply, drafts, HP) and starts the campaign with the Valley Stronghold cleared and its reward paid. Later runs start on it as a normal sector.
- Telemetry: `run_start` has `scene=` and `mode=tutorial|campaign|standalone`, and the end-of-run payload and GameAnalytics carry `runMode`.

**Wiring checklist:**
- [ ] Commit `Bladehold Campaign Map Scene.unity`. The diorama rebuild for the new node was saved into it **alongside your uncommitted settings-button work**, so it wasn't committed with phase 4. A pre-rebuild copy is in this session's scratchpad (`CampaignMap_before_diorama.unity`).
- [ ] Run **Bladehold > Campaign > Capture Level Previews** for the Valley Stronghold tooltip screenshot. It wasn't run because it re-renders every node.
- [ ] Map icon: the node uses the standard combat sword like the Alpine Keep. Decide whether the starting castle wants something different.
- [ ] UI review of the map: the "The Valley" header (blank numeral line), and the Alpine Keep's "I" header, which wasn't visible over the snow in the MCP screenshot.
- [ ] `Combined Character` (an Animator + SkinnedMeshRenderer at the scene root) is Synty Sidekick junk committed in the Valley Stronghold scene in `dbead25ae`. Delete it if nothing uses it.
- [ ] Balance: playtest the campaign-mode Valley Stronghold. Spawn points, objectives (wagon route, cages, catapults) and the 6-plot layout were set up for the tutorial's 3 waves. Check that waves 3–5 and the wave-card objectives work in this layout.

**Manual verification:**
- [ ] New save → full tutorial → victory → "Proceed to Campaign Map": the map shows Valley Stronghold completed with the you-are-here flag, Alpine Keep available, and gold and drafts kept.
- [ ] Die in the Alpine Keep → Meta → portal → map: Valley Stronghold is the only available node. Deploying plays it with no tutorial hints, Ready works straight away, wave drafts open between waves and the ultimate works.
- [ ] Pause → Replay Tutorial from a campaign run still runs T1 → T2 → the Valley Stronghold in tutorial mode.
