# Editor to-do: plan 08 (Legacy cleanup)

From [plan 08](../08-legacy-cleanup.md), 2026-09-25. Unity MCP wasn't connected, so nothing was opened in the Editor or play-tested. **Do section 1 first:** until it runs, the build scenes and prefabs carry "missing script" components and old fortress/skill-node prefab instances (warnings only, nothing breaks). Tick items off as you go and delete the file when it's empty.

## 1. Run the cleanup tool (5 min, could be run by an agent via MCP)

- [ ] **Let Unity recompile** (lots of scripts were deleted), then check the Console has no compile errors.
- [ ] **Run `Bladehold/Maintenance/Plan 08 Legacy Cleanup`.** It opens every build scene and every prefab under `Assets/Bladehold`, then:
  - strips nested instances of `FortDefenseSockets`, `FortDefenseManager`, `Fort_ArrowSlit`/`Fort_BoilingOil`/`Fort_Spikes` and `SkillNode`/`SkillNode Reincarnate`/`SkillNodeConnector`, plus every missing-script component;
  - adds a `TowerPlotManager` to any scene that has tower plots but no manager (the `Bladehold Survivors Scene` has none, so Tesla Spire, Permafrost and the victory supply refund don't run there);
  - re-saves each build scene, which also rewrites the binary castle/crypt/sanctuary scenes as text. That ticks off 00 §B "Re-serialize the binary scenes" for the build scenes;
  - then deletes those legacy prefab files.
  - Verify: the Console report ends "Done, no problems." Any `!!` line names an object to fix by hand (usually a legacy instance nested inside another prefab instance). Then commit and **delete `Editor/Plan08LegacyCleanup.cs`**.
- [ ] **`Bladehold Demo Scene`, `Bladehold Test Scene`, `Assets/_Recovery/0.unity`** aren't in the build and still reference deleted scripts (WaveSpawner, SkillTreeService…). Delete them unless you still use them (they won't run as they are).

## 2. Scene wiring

- [ ] **Frozen Pass is now a tier-3 campaign node (inside the demo):** `Bladehold Frozen Pass Scene` needs 6 `TowerPlot` prefab instances (`Bladehold Prefabs/Defenses/TowerPlot.prefab`), a `TowerPlotManager`, and the build wheel + supply HUD. The castle scenes got these from `BuildCastleLevels.SetupBattlefieldTowerPlots` / `EnsureDefensesHUD`, which an agent could reuse via an editor script.
  - Verify: prep phase, `[E]` on a plot opens the build wheel, and clearing wave 5 shows a supply refund.
  - Its old Rest Area exit gate (`Station_5_ExitGate (Alpine)`) is switched off (Interactable + RestAreaGate disabled). Delete the object if it's not scenery you want.
- [ ] **Ancient Garden is now a tier-6 node:** same wiring as Frozen Pass in `Bladehold Ancient Garden`.
- [ ] **Both scenes:** check `GameLoopManager` has a `captainSpawnPoint`/`bossSpawnPoint` so Captain Kombusta spawns somewhere sensible (both nodes name him).

## 3. UI review

- [ ] **Death screen "Level Reached" row now shows the wave reached.** In `DeathScreen.prefab` → `SurvivorsStatsPanelUI`, relabel the `Level Reached` row's label text to "Wave Reached" (or hide the row).
- [ ] **Empty panels left behind:** `DeathScreen.prefab` still has the old gold/Reincarnate skill-tree panel objects (now script-less and hidden), and `Bladehold HUD.prefab` still has the XP bar / level badge objects that `SurvivorsHUDUI` drove. Delete them if they're still visible or just dead weight.
- [ ] **Sidebar tooltips** (pause-menu / death-screen acquired-skills list) now actually show on hover: name + card description. Check they read well.

## 4. Decisions

- [ ] **Pause → Quit** loads a scene named `MainMenu` (`PauseMenuCanvas.prefab` → `PauseMenuView.mainMenuSceneName`). That scene was never in the build and is now deleted, so the button fails. Options: quit to desktop (`Application.Quit`), or go to `Bladehold Meta Area Scene` (which breaks "no voluntary exit" unless it wipes the run like death does).
- [ ] **Banner difficulty never escalates:** `SaveData.runsAttempted` gates Enraged/Nightmare/Omega banner rolls (`BannerDifficultyHelper.RollTierForBanner`) but nothing increments it, so every banner is Standard. Wire `runsAttempted++` on Battle Portal, or drop the gate?
- [ ] **Enemy coin pickups pay into `Wallet` → `SaveData.totalGold`,** a permanent total nothing spends, not into `RunSession.InRunGold` (the Rest Area shop's gold). Intended?

## 5. Playtest

- [ ] **Full loop:** Meta → Battle Portal → map shows Frozen Pass at tier 3 and Ancient Garden at tier 6 → a castle sector → die → Meta. No missing-script warnings in the Console.
- [ ] **Tesla Spire / Permafrost** (DevConsole draft cards `elem_light_tesla_spire`, `elem_ice_permafrost`): with no towers built, nothing happens. With a tower built, a bolt every 5 s from the tower nearest an enemy, and enemies near towers get slowed + Ice status.
- [ ] **Never:** an `[E] View Campaign Map` / `Rest Area` prompt on the castle gate; a "Siegebreaker" objective line after a long sector; a Fortress category button in the DevConsole draft list.
- [ ] **DevConsole wave panel:** shows `Wave N (kills/quota)` and "Wipe Wave" kills everything alive (counts toward the quota).
- [ ] **RunTelemetry:** after a sector, the run CSV in `persistentDataPath/Telemetry/` now has `wave_clear` rows (it had none in live sectors before).
