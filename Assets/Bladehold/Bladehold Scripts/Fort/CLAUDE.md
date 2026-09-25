# Fort: battlefield tower defences

## Current system

- **`TowerPlot`**: a fixed build spot in each battle scene. `[E]` on an empty plot during the prep phase (`GameLoopManager.IsPrepPhase`) opens `UI/BuildWheelUI`; during a wave it shows "Locked (Wave Active)".
- **Build wheel** (`UI/BuildWheelUI.cs`, `BuildWheelButton`, `BuildWheelSliceButton.prefab`): 6 types, paid in **Supply** via `RunSession.TrySpendInRunSupply`:
  - Arrow Tower, Catapult, Ballista, Net Thrower, Spike Trap, Oil Vat.
- **`DefenseStructure`** base + subclasses (`ArrowTowerDefense`, `CatapultDefense`, `BallistaDefense`, `NetThrowerDefense`, `SpikeTrapDefense`, `OilVatDefense`).
  - Each tower holds its own supply and spends some per shot. At 0 it stays standing but stops firing (a "NO SUPPLY" marker points at it).
  - `[E]` on a tower refills it from the player's pool. Once it's full, `[E]` upgrades it instead (max level 3). Refill and upgrade work any time, not just during prep.
- **`DefenseAssemblyAnimation`**: ghost preview plus the piece-drop / ground-emerge build animation.
- **`TowerPlotManager`**: owns the plots. It currently saves towers to `RunSession.SavedDefenses` on every wave clear, restores them by plot index in the next scene, and wipes them on death.

## Decided change

Towers should **not** carry between sectors. When leaving a sector, refund each tower's remaining supply to the player, and start the next sector with empty plots. That means removing the `SavedDefenses` transfer. It was also fragile, since it restored towers by plot index into a different castle layout.

## Legacy

`FortDefense`, `FortDefenseManager`, `FortDefenseSocket`, `ArrowSlitDefense`, `BurningOilDefense`, `SpikeDefense` are the old socketed fortress. They're still in scenes, and some draft cards still apply to them (`DraftUpgradeService`, `SurvivorsCardSelectUI`), but plots replaced them. The Fortress draft category is excluded from drafts for the same reason.
