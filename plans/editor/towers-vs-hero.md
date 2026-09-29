# Editor to-do: towers vs hero (blind spots, Sapper)

No plan file; from the 2026-09-29 design chat (towers and hero doing the same job). Unity MCP was **not** connected, so nothing here has run in the Editor. Tick items off as you go and delete the file when it's empty. **The Sapper never spawns until its prefab is generated (first item under Wiring).**

## Verify first

- [ ] **Scripts import clean.** Focus Unity and check the Console. New files: `Enemies/Sapper/TowerSapper.cs`, `Enemies/Sapper/TowerSapperSO.cs`. Their `.meta` files are created on import, so commit those afterwards.
- [ ] **Reload `Config/Enemies.csv` in LibreOffice before you save it.** It was open (lock file) when the `sapper` row was appended. Saving the old buffer drops the row. Your uncommitted Bulwark tuning is still in the working copy.

## Wiring

- [ ] **Generate the Sapper prefab** *(MCP-able)*: **Bladehold > Generate Enemy Prefabs**. This creates `Bladehold Prefabs/Sapper Enemy Variant.prefab`, `Enemies/Sapper/TowerSapperSO.asset` and the `sapper` entry in `EnemyPrefabMap.asset`. `TowerSapper.data`, `health`, `targetSelector` and `animator` are wired by the generator (and auto-wire in `OnValidate` except `data`). Verify: benchmark 19F "Sapper Prefab" passes.
- [ ] **Sapper look** (art pass): it's a plain goblin at 0.9 scale. Give it something readable at a glance (a pickaxe, a satchel, a tint) so players learn "that one goes for my towers".
- [ ] **`TowerSapper.drainFeedback`** (optional `MMF_Player` on the prefab): a hacking thunk + wood chips at the tower each tick. Without it the only sign is the tower's supply bar dropping.

## UI review

- [ ] **Build wheel range line.** Hovering a slice now adds `Range: Medium  |  Blind spot: Small` (or `Trap: hits what walks over it`) under the description at 85% size, in `descriptionLabel` on `Bladehold HUD.prefab` → `BuildWheelUI`. Check it fits without overflowing, and with gamepad focus as well as mouse. The buckets are tunable on `BuildWheelUI` (`mediumRangeFrom` 12, `longRangeFrom` 20, `mediumBlindSpotFrom` 5, `largeBlindSpotFrom` 7).

## Tuning

- [ ] **Blind spot sizes**: `minRange` on each `Bladehold Prefabs/Defenses/Defense_*.prefab`. Currently Arrow 3, Net Thrower 4, Ballista 6, Catapult 8.
- [ ] **Sapper numbers**: `TowerSapperSO`. Currently drain 4 supply/s, drain range 2.8m (keep it under the Arrow Tower's 3m blind spot), search 60m. Roster row: 30 HP, speed 4.2, from wave 2 at threat tier 2, 15% weight, max 3 at once.

## Playtest

- [ ] **Blind spot**: build an Arrow Tower and a Catapult, and let goblins walk up to each. The tower stops shooting anything hugging it and resumes once they step out. A Spike Trap next to the Catapult catches what the Catapult can't.
- [ ] **Sapper beats**: DevConsole (backquote) → spawn-type picker → `sapper` with a tower built. It runs past you to the nearest tower, hacks until the tower shows NO SUPPLY, then goes for the next one. With no towers it fights you like a normal goblin.
- [ ] **Negative cases**: the tower it's draining never shoots it (it's in the blind spot), but a neighbouring tower does. It never drains a tower after dying. Hitting it doesn't make it turn on you (it's single-minded by design; say if you'd rather it retaliates). Refilling a tower with [E] while a sapper is on it works and the sapper keeps draining.
