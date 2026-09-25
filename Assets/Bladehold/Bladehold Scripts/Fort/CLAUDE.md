# Fort: battlefield tower defences

## Current system

- **`TowerPlot`**: a fixed build spot in each battle scene. `[E]` on an empty plot during the prep phase (`GameLoopManager.IsPrepPhase`) opens `UI/BuildWheelUI`; during a wave it shows "Locked (Wave Active)".
- **Build wheel** (`UI/BuildWheelUI.cs`, `BuildWheelButton`, `BuildWheelSliceButton.prefab`): 6 types, paid in **Supply** via `RunSession.TrySpendInRunSupply`:
  - Arrow Tower, Catapult, Ballista, Net Thrower, Spike Trap, Oil Vat.
- **`DefenseStructure`** base + subclasses (`ArrowTowerDefense`, `CatapultDefense`, `BallistaDefense`, `NetThrowerDefense`, `SpikeTrapDefense`, `OilVatDefense`).
  - Each tower holds its own supply and spends some per shot. At 0 it stays standing but stops firing (a "NO SUPPLY" marker points at it).
  - `[E]` on a tower refills it from the player's pool. Once it's full, `[E]` upgrades it instead (max level 3). Refill and upgrade work any time, not just during prep.
- **`DefenseAssemblyAnimation`**: ghost preview plus the piece-drop / ground-emerge build animation.
- **`TowerPlotManager`**: owns the plots. Towers **never carry between sectors**. On victory, `GameLoopManager.TriggerVictory` calls `DismantleAllForRefund()`, which pays each tower's `DismantleRefund` (remaining supply + supply spent upgrading it) into `RunSession.InRunSupply`; the victory screen shows the total. On player death the towers are just cleared. Every sector starts with empty plots.
  - It also runs the two tower-wide Elemental cards: **Tesla Spire** (a bolt every 5s from the built tower nearest an enemy) and **Permafrost** (a chilling aura around every built tower). Both need a built tower.
- `TargetLead`: shared projectile-leading maths (aim point, target velocity, intercept).

The old socketed fortress (`FortDefense*`, `ArrowSlitDefense`, …) and the Fortress draft category were deleted in plan 08.
