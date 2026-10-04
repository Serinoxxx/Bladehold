# Editor to-do: Spearman (anti-cavalry Big Ork)

Not from a plan; added 2026-10-04 with Unity MCP connected. Tick items off as you go and delete the file when it's empty.

What's done: `spearman` row in `Config/Enemies.csv` (T2+, wave 2+, 20% weight, max 3); prefab `Bladehold Prefabs/Spearman Enemy Variant.prefab` generated from the `EnemyManifest` entry (Big Ork base, Dungeon `SM_Wep_Spear_01` on `Hand_R`, hammer hidden) and registered in `EnemyPrefabMap`; poses, mask and controllers built by **Bladehold > Spearman > Build Pose Animations** into `Bladehold Animations/Spearman/`. Both poses checked in Play mode (scratch scene). Riding into its front (±70°) rears the horse like the Bulwark and deals 2–4 damage to the horse, scaled by speed (`MountStopperSO Spearman`).

## Verify

- [ ] **Run the benchmark** from `MainMenu`: **Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark**, section **9S. SPEARMAN ANTI-CAVALRY** should show 2 PASSED. Don't save the scene it ran in.

## Tuning / art (your call)

- [ ] **Pose look**: tweak the muscle tables in `Editor/SpearmanPoseBuilder.cs` (`SpearsHigh` / `SpearsForward`) and re-run the builder menu; GUIDs are kept so the prefab doesn't need regenerating. If you move the spear grip, change `SpearGripPosition`/`SpearGripEuler` there and re-run **Bladehold > Generate Enemy Prefabs** (or `GenerateById("spearman")`) *(MCP-able)*.
- [ ] **Attack swing**: it uses the stock `AIAttack` with the goblin controller's "Standard Axe Attack" on the Attack layer (that overrides the spear pose during the swing). A spear thrust clip on `Spearman Override.overrideController` would read better.
- [ ] **Horse impact feedback**: `HorseMotor.mountStopFeedback` on `Resources/Horse.prefab` plays on every stop (Bulwark too). If you want a distinct "spear stab" beat (blood, horse scream), that needs a second MMF slot; tell an agent.
- [ ] **Horse impact damage vs horse HP**: impact is `horseImpactDamage` 4 at full charge (×0.5 at the slowest rear speed) on `Enemies/Spearman/MountStopperSO Spearman.asset`, against horse max HP 12 (still an open decision in [mount-charge-and-hud](mount-charge-and-hud.md)). Iron Barding's charge damage reduction applies.
- [ ] **Brace ranges**: `Enemies/Spearman/SpearmanStanceSO.asset`: `braceRangeMounted` 14 m, `braceRangeOnFoot` 4 m.

## Playtest

- [ ] **Spawning**: DevConsole (backquote) → threat ▲ to 2 → Spearmen appear from wave 2; or spawn one with the picker / Enemy Manager Zoo tab.
- [ ] **Stance**: spears high while marching; levelled forward when you're mounted within ~14 m, or on foot within ~4 m; arms go limp (layer fades) on death and knockdown, and the cheer still plays when you die.
- [ ] **Charge into its front**: horse rears and stops, takes damage (damage number on the horse / horse HUD drops), charge ends. Can't push forward into it afterwards.
- [ ] **Negative cases**: charging into its **back or side (beyond 70°)** tramples through as normal; a **dead or knocked-down** Spearman never stops the horse; the **Bulwark** still stops the horse with **no** damage.
- [ ] **Death accounting**: coins drop, kill counts toward the wave quota.
