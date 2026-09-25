# Editor to-do: plan 09 (Code-built visuals + MMF audit)

From [plan 09](../09-visuals-and-mmf-audit.md), session 1 (2026-09-26, the runtime `LoadAssetAtPath` batch). Unity MCP wasn't connected, so nothing was opened in the Editor or play-tested. **Until §2's first item is done, building a tower plays no sound, dust or shake** and logs 3 errors per build. Tick items off as you go and delete the file when it's empty.

## 1. Verify first (5 min)

- [ ] **Let Unity import.** Check the Console for compile errors, and that `Bladehold Prefabs/Defenses/DefenseAssemblyAnimation.prefab` opens: a root with `DefenseAssemblyAnimation` and three children (`PieceLandFeedback`, `RiseDustFeedback`, `SlamFeedback`), each with an empty `MMF_Player`. It was written by hand as YAML. If it fails to import, delete it and recreate it the same way, then drag it into `TowerPlot.prefab` → `TowerPlot.assemblyAnimationPrefab`.
- [ ] **Run the benchmark** (**Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark**). Check 20D now reads "TowerPlot prefab references an authored assembly prefab. [PASSED]".
- [ ] **Binary castle scenes don't override the new wiring.** In any castle scene, select the HUD's `BuildWheelUI`: `supplyPopupPrefab` = Gold, `buildSfx` = HAMMER_Hit_Wood_Shield_stereo, `buildVfxPrefab` = FX_Impact_Wood_01, none of them bold (overridden). Likewise `ObjectiveWaypointTrackerUI.noSupplyIcon` / `cleanupEnemySkullIcon`. *(MCP-able)*

## 2. Prefab wiring

- [ ] **Tower build feedbacks** in `DefenseAssemblyAnimation.prefab`. Fill the three MMF players. The values below are what the old code did; tune to taste:
  - `PieceLandFeedback`: MMSoundManager Sound `HAMMER_Hit_Wood_Shield_stereo`, Particles Instantiation `FX_Dust_Small_01`, Camera Shake 0.14 s / amplitude 0.2.
  - `RiseDustFeedback`: Particles Instantiation `FX_Dust_Small_01` (plays every 0.22 s while a 1-2 piece tower rises).
  - `SlamFeedback`: Sound `HAMMER_Hit_Wood_Shield_stereo`, Particles Instantiation `FX_Dust_Big_01`, Camera Shake 0.28 s / 0.38.
  - Code calls `PlayFeedbacks(position)`, so feedbacks should use the passed position. The object is destroyed 0.15 s after the last landing, so **don't parent spawned particles to the feedback**, or they vanish mid-puff.
  - Verify: build an arrow tower (drop mode) and a spike trap (rise mode). Thuds, dust and shake play, and no `DefenseAssemblyAnimation ... not assigned` errors appear.
- [ ] **Horse summon sound:** `Player.prefab` → `PlayerSummonMount.spawnFeedback` (and `despawnFeedback`) are empty. The neigh was an Editor-only hack and is gone. Add an MMF_Player with Sound `Bladehold Audio/SFX/Horse/Horse_Neigh_01.wav` and assign it. Verify: summon the mount (DevConsole) and hear the neigh.
- [ ] **Main menu click sounds:** the 10 `UIClickFeedback` buttons in `MainMenu` and `UI/MenuButton.prefab` have no `clickFeedback`, so they're silent and log an error on Start. Copy the click `MMF_Player` from any button in `UI/PauseMenuCanvas.prefab` and assign it. *(MCP-able)*

## 3. Playtest

- [ ] **Pickups play one sound, not two:** coin, health pack, Impulse orb, Lightning orb. Each used to play its MMF sound plus a second, Editor-only one.
- [ ] **Ammo pickup now has a sound** ("New Item A"). Its MMF player existed but wasn't wired.
- [ ] **Towers:** building shows the wood-impact VFX from the build wheel. Let enemies wreck a tower: it now shows the wood break VFX in a build too.
- [ ] **Objectives:** supply wagon arrival spawns gold bags; the cleanup phase shows skull markers; the "no supply" waypoint shows the hammer icon; the `[E]` interaction prompt appears.
- [ ] **Must never happen:** any `... is not assigned` error in the Console during a normal run. Each one names the exact field to wire.
