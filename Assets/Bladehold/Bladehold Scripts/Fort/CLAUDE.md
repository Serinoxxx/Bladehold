# Fort: battlefield tower defences and walls

## Towers

- **`TowerPlot`**: a fixed build spot in each battle scene. `[E]` on an empty plot during the prep phase (`GameLoopManager.IsPrepPhase`) opens `UI/BuildWheelUI`; during a wave it shows "Locked (Wave Active)".
- **Build wheel** (`UI/BuildWheelUI.cs`, `BuildWheelButton`): 4 types paid in **Supply** via `RunSession.TrySpendInRunSupply`: Arrow Tower, Catapult, Ballista, Net Thrower.
  - Slices are laid out round the authored ring for any option count. Extra slices are cloned from the first authored button.
  - The paid cost is stamped on the tower (`BuildCostPaid`).
  - The Oil Vat and Spike Trap towers were removed in plan 17; their jobs moved onto wall upgrades.
  - **`FortDefenseType` values are explicit ints.** 1 and 2 were the removed towers; never reuse or renumber them.
- **`DefenseStructure`** base + subclasses (`ArrowTowerDefense`, `CatapultDefense`, `BallistaDefense`, `NetThrowerDefense`).
  - Each tower holds its own supply and spends some per shot. At 0 it stays standing but stops firing (a "NO SUPPLY" marker points at it).
  - **Blind spot**: `minRange` (flat distance, serialized per prefab) is a radius the turret can't target, so enemies hugging a tower are the hero's job (or the Spikes upgrade's). Arrow 3m, Net Thrower 4m, Ballista 6m, Catapult 8m.
  - **Sapper** (`Enemies/Sapper/TowerSapper`): an enemy that runs to the nearest supplied tower via `AITargetSelector.SetTowerTarget` and drains it with `ConsumeSupply` from inside the blind spot. If a wall claims it first, it hacks the wall's HP instead.
  - **Dome Warden** (`Enemies/DomeWarden/ProjectileDome`): catches tower projectiles aimed at enemies inside its dome and takes the damage itself until it breaks. `NetThrowerDefense` won't root anything inside one. See `DamageSystem/CLAUDE.md`.
  - **Hexer** (`Enemies/Hexer/TowerHexer`): channels on a tower; while `IsHexed` it can't fire (spike rings too). Every turret checks `IsHexed` in its fire path, so a new defence must too.
- **Two interaction modes**:
  - **Defense scenes** (a `DefenseSceneRules` with a `FortUpgradeConfigSO` in the scene: Outer Gate, Desert Gate, Graveyard, Tutorial Gate). `[E]` on a tower opens the **upgrade wheel** (`BuildWheelUI.OpenUpgrades(IUpgradeable)`) with these slices: Refill (supply), Fire Rate I–III, Spikes (`TowerSpikeRing`), Fire / Ice / Storm (crystals, one element per tower, locked once bought) and Deconstruct.
    - The tower stays level 1; purchases live in `StructureUpgradeState`.
    - Subclasses read `FireRateMultiplier` and `Element`/`ApplyElementTo`. Arrow: ignite / frost / shock. Catapult: rolling fireball / slippery ground / storm cloud. Ballista bolts and net roots apply the status.
  - **Older scenes** (no rules component): `[E]` refills, then auto-levels to Lv3, as before.
- **Refunds**:
  - **Deconstruct** (`DeconstructRefund`): build cost + every upgrade + any paid refill ammo still unfired, plus every crystal. It never returns more than was paid.
  - **Sector-end dismantle** (`DismantleRefund`): remaining ammo + upgrade spend, plus crystals.
- `DefenseAssemblyAnimation`: ghost preview plus the piece-drop / ground-emerge build animation.
- `TowerPlotManager`: owns tower plots **and wall plots**. On victory `DismantleAllForRefund()` refunds both; on player death everything is cleared. Towers and walls never carry between sectors.
- `TargetLead`: shared projectile-leading maths.

## Walls (plan 17, `Fort/Walls/`)

- **`WallPlot`** sits across a bridge deck (local +Z faces the enemy). The generator puts one on every bridge (up to 8). In the Tutorial Gate they're hand-placed between big rocks.
  - Its footprint is baked onto its own NavMesh area `WallPlot{index}` (areas 3–10, via a `NavMeshModifierVolume` child).
  - Build, rebuild and upgrade happen at its **`WallCraftingStation`** (castle side): build/rebuild in prep, upgrade any time.
- **`WallStructure`**: `Health` (immune to the player), tier HP from `FortUpgradeConfigSO`.
  - Segments are tiled across the width from `WallConfigSO` art, with a doorway in the middle. The two side sections carve the NavMesh.
  - **Damage stages**: segments swap to damaged art and looping smoke/fire starts at 75/50/25% HP. At 0 HP it collapses to rubble and stops blocking.
  - **Upgrades** (`IUpgradeable`):
    - Material wood → stone → metal (HP fraction preserved).
    - Repair +10.
    - Spikes: 1 damage back per **melee** hit.
    - One element: boiling oil (`BurningOilZone`), icy water (`WallIcyWaterZone`) or lightning arcs, each triggered when the wall is hit, on a cooldown.
    - Deconstruct (100% refund).
- **`WallDoor`**: `[E]` opens/shuts it. It won't shut on anything in the doorway.
- **Routing (`WallNavCost`)**: a standing wall with its door shut prices its area at `wallAreaCost`, so enemies route over another bridge unless the detour is longer.
  - Costs are applied **per agent** by `AIMovement` when `WallNavCost.Version` changes. Never `NavMesh.SetAreaCost`.
  - Siege units (`WallNavCost.IsSiege`: `SiegeUnit` marker, troll, sapper, ram, captains) keep cost 1 and walk straight in.
- **What actually stops enemies is targeting**, because NavMeshAgents ignore colliders.
  - Every 0.25 s a blocking wall gives each enemy in a box on its outside face `AITargetSelector.SetWallTarget(this)`, and does the same for enemies within 10 m of any siege unit there (escorts clear the path with it).
  - The wall target beats everything except a player on the enemy's side of the wall. The wall target clears when the wall falls or the door opens.
- **Battering ram**: stops at a blocking wall within 4 m ahead (`WallStructure.FindBlockingAhead`), rams it until it falls, then rolls on. Its escorts form up at the wall and get claimed.
- **Layers**: wall colliders are on **Fortification** (layer 14). `PlayerBarrier.Exclude` strips it, so player and tower shots fly over walls.

## Crystals (plan 17)

`RunSession` Fire / Ice / Storm (`StructureElement.Lightning`) crystals, reset per run, kept across sectors. Sources:
- `Economy/Crystals/CrystalRewards` (on the FortRules prefab):
  - elite drops via `CrystalPickup` (fodder never drops);
  - siege and captain drops;
  - +1 per wave survived, +1 more if its objective succeeded.
- `FishingManager`: Fire/Frost/Spark buff fish.
- Elements are rolled through the scene's `SceneCrystalBias`, copied from the palette's `crystalBias` (Desert → Fire, Alpine → Ice, Graveyard → Storm, even elsewhere).
