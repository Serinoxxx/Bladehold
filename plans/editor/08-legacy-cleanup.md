# Editor to-do: plan 08 (Legacy cleanup)

From [plan 08](../08-legacy-cleanup.md), 2026-09-25. Unity MCP wasn't connected, so nothing was opened in the Editor or play-tested. Tick items off as you go and delete the file when it's empty.

## 1. Cleanup

- [x] ~~Run the cleanup tool~~: done by an agent via MCP on 2026-09-26 (plan 09 session 2 needed it: Unity won't save a prefab with missing scripts). No problems reported. Prefabs: HUD, DeathScreen (-3 legacy skill-tree instances), Player, SurvivorsGameManager, EnemySpawner. Scenes: MainMenu lost its 5 dead screens, Survivors Scene got a `TowerPlotManager`, each castle scene lost 2 legacy fort instances. Legacy prefabs and `Plan08LegacyCleanup.cs` are deleted. The binary scenes did **not** turn into text (see 00 §B).
- [ ] **`Bladehold Demo Scene`, `Bladehold Test Scene`, `Assets/_Recovery/0.unity`** aren't in the build and still reference deleted scripts (WaveSpawner, SkillTreeService…). Delete them unless you still use them (they won't run as they are).

## 2. Scene wiring

- [x] **Frozen Pass is now a tier-3 campaign node (inside the demo):** done by an agent via MCP 2026-09-26: `Battlefield Tower Plots` root with `TowerPlotManager` + 6 `TowerPlot` prefab instances, spread along the approach from the enemy spawns to the gate and snapped to the NavMesh; the HUD prefab already carries the build wheel + supply counter. Play-checked: all 6 register, building works with no errors. **Check the placement** (agent guess). Original item: `Bladehold Frozen Pass Scene` needs 6 `TowerPlot` prefab instances (`Bladehold Prefabs/Defenses/TowerPlot.prefab`), a `TowerPlotManager`, and the build wheel + supply HUD. The castle scenes got these from `BuildCastleLevels.SetupBattlefieldTowerPlots` / `EnsureDefensesHUD`, which an agent could reuse via an editor script.
  - Verify: prep phase, `[E]` on a plot opens the build wheel, and clearing wave 5 shows a supply refund.
  - Its old Rest Area exit gate (`Station_5_ExitGate (Alpine)`) is switched off (Interactable + RestAreaGate disabled). Delete the object if it's not scenery you want.
- [x] **Ancient Garden is now a tier-6 node:** done the same way 2026-09-26. **Check the placement:** plots 1 and 3 landed on raised NavMesh (y 4.3 and 7.4). The scene also has a second, inactive `Bladehold HUD (1)`; delete it. Original item: same wiring as Frozen Pass in `Bladehold Ancient Garden`.
- [x] **Both scenes:** done 2026-09-26: new `CaptainSpawnPoint` at the enemy spawn centroid, facing the gate, assigned to `GameLoopManager.captainSpawnPoint` (the manager sits at the origin, so the old fallback spawned him far off the map). Original: check `GameLoopManager` has a `captainSpawnPoint`/`bossSpawnPoint` so Captain Kombusta spawns somewhere sensible (both nodes name him).

## 3. UI review

- [x] **Death screen "Level Reached" row now shows the wave reached.** Relabelled to "Wave Reached" 2026-09-26 (the row object keeps its name; code finds it by name). In `DeathScreen.prefab` → `SurvivorsStatsPanelUI`, relabel the `Level Reached` row's label text to "Wave Reached" (or hide the row).
- [ ] **Empty panels left behind:** `DeathScreen.prefab` still has the old gold/Reincarnate skill-tree panel objects (now script-less and hidden), and `Bladehold HUD.prefab` still has the XP bar / level badge objects that `SurvivorsHUDUI` drove. Delete them if they're still visible or just dead weight.
- [ ] **Sidebar tooltips** (pause-menu / death-screen acquired-skills list) now actually show on hover: name + card description. Check they read well.

- [ ] **Main menu review** (`MainMenu`, now build index 0): Start → loading bar → Meta Area, Settings opens the settings panel, Quit quits. The old character rotunda (`CharacterRotundaRoot`) lost its script (the class system is gone), so no character model spawns behind the title. Keep the empty stage, place a static Sidekick model, or delete it. Synty art, Texturina headers / Grenze body, gamepad focus on Start.

## 4. Decisions

- [x] ~~**Pause → Quit**~~: decided 2026-09-26, the main menu is back as build index 0, so Quit returns there.
- [ ] **Banner difficulty never escalates:** `SaveData.runsAttempted` gates Enraged/Nightmare/Omega banner rolls (`BannerDifficultyHelper.RollTierForBanner`) but nothing increments it, so every banner is Standard. Wire `runsAttempted++` on Battle Portal, or drop the gate?
- [x] ~~**Enemy coin pickups pay into `Wallet`**~~: decided 2026-09-26, one gold currency. Coins now pay `RunSession.InRunGold`; `Wallet` and `SaveData.totalGold` are gone.

## 5. Playtest

- [ ] **One gold:** kill enemies → the HUD gold counter rises (it didn't before: kills paid a hidden permanent total) → spend it in the Rest Area shop. Die → gold resets with the run.
- [ ] **Boot a player build:** it starts on the main menu (it used to boot into the Rest Area).

- [ ] **Full loop:** Meta → Battle Portal → map shows Frozen Pass at tier 3 and Ancient Garden at tier 6 → a castle sector → die → Meta. No missing-script warnings in the Console.
- [ ] **Tesla Spire / Permafrost** (DevConsole draft cards `elem_light_tesla_spire`, `elem_ice_permafrost`): with no towers built, nothing happens. With a tower built, a bolt every 5 s from the tower nearest an enemy, and enemies near towers get slowed + Ice status.
- [ ] **Never:** an `[E] View Campaign Map` / `Rest Area` prompt on the castle gate; a "Siegebreaker" objective line after a long sector; a Fortress category button in the DevConsole draft list.
- [ ] **DevConsole wave panel:** shows `Wave N (kills/quota)` and "Wipe Wave" kills everything alive (counts toward the quota).
- [ ] **RunTelemetry:** after a sector, the run CSV in `persistentDataPath/Telemetry/` now has `wave_clear` rows (it had none in live sectors before).
