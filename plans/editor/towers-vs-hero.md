# Editor to-do: towers vs hero (blind spots, Sapper, Dome Warden)

No plan file; from the 2026-09-29 design chat (towers and hero doing the same job). Unity MCP was **not** connected, so nothing here has run in the Editor. Tick items off as you go and delete the file when it's empty. **Neither the Sapper nor the Dome Warden spawns until the prefab generator has run (first item under Wiring).**

## Verify first

- [ ] **Scripts import clean.** Focus Unity and check the Console. New files: `Enemies/Sapper/TowerSapper.cs`, `TowerSapperSO.cs`, `Enemies/DomeWarden/ProjectileDome.cs`, `ProjectileDomeSO.cs`. Their `.meta` files are created on import, so commit those afterwards.

## Wiring

- [ ] **Generate the Sapper and Dome Warden prefabs** *(MCP-able)*: **Bladehold > Generate Enemy Prefabs**. This creates `Bladehold Prefabs/Sapper Enemy Variant.prefab`, `Enemies/Sapper/TowerSapperSO.asset` and the `sapper` entry in `EnemyPrefabMap.asset` (plus `Dome Warden Enemy Variant.prefab` and `dome_warden`). `TowerSapper.data`, `health`, `targetSelector` and `animator` are wired by the generator (and auto-wire in `OnValidate` except `data`). Verify: benchmark 19F "Sapper Prefab" passes.
- [ ] **Sapper look** (art pass): it's a plain goblin at 0.9 scale. Give it something readable at a glance (a pickaxe, a satchel, a tint) so players learn "that one goes for my towers".
- [ ] **`TowerSapper.drainFeedback`** (optional `MMF_Player` on the prefab): a hacking thunk + wood chips at the tower each tick. Without it the only sign is the tower's supply bar dropping.
- [ ] **Dome Warden visual check** *(MCP-able)*: the generator creates `Enemies/DomeWarden/ProjectileDomeSO.asset` with `domeVisualPrefab` = `Bladehold Prefabs/VFX/BubbleShieldVisual.prefab` (the Bubbler's sphere), scaled to a 10m-wide dome, half-buried (`visualHeightOffset` 0). It may need its own, more transparent material at that size so it doesn't hide the fight. Its colliders are switched off at runtime. The generator also wires `ProjectileDome.hitNumberPrefab` to the goblin's own damage popup; check it actually shows purple (`hitNumberColor` on the SO), in case that popup prefab forces its own colour. Verify: benchmark 19G "Dome Warden Prefab" passes.
- [ ] **Dome Warden look + `ProjectileDome` feedbacks** (optional `MMF_Player`s): `hitFeedback` (glassy ping at the hit point), `breakFeedback` (shatter), `rebuildFeedback` (the Warden raising it again). Without them the dome just pulses, vanishes and reappears.

## UI review

- [ ] **Build wheel range line.** Hovering a slice now adds `Range: Medium  |  Blind spot: Small` (or `Trap: hits what walks over it`) under the description at 85% size, in `descriptionLabel` on `Bladehold HUD.prefab` → `BuildWheelUI`. Check it fits without overflowing, and with gamepad focus as well as mouse. The buckets are tunable on `BuildWheelUI` (`mediumRangeFrom` 12, `longRangeFrom` 20, `mediumBlindSpotFrom` 5, `largeBlindSpotFrom` 7).

## Tuning

- [ ] **Blind spot sizes**: `minRange` on each `Bladehold Prefabs/Defenses/Defense_*.prefab`. Currently Arrow 3, Net Thrower 4, Ballista 6, Catapult 8.
- [ ] **Sapper numbers**: `TowerSapperSO`. Currently drain 4 supply/s, drain range 2.8m (keep it under the Arrow Tower's 3m blind spot), search 60m. Roster row: 30 HP, speed 4.2, from wave 2 at threat tier 2, 15% weight, max 3 at once.
- [ ] **Dome Warden numbers**: `ProjectileDomeSO`: radius 5m, 150 dome HP, back up 8s after breaking. Stands 9m off its target (`NavMeshAgent` stopping distance on the prefab). Roster row: 80 HP, speed 2.8, from wave 2 at threat tier 3, 12% weight, max 1 at once.

## Playtest

- [ ] **Blind spot**: build an Arrow Tower and a Catapult, and let goblins walk up to each. The tower stops shooting anything hugging it and resumes once they step out. A Spike Trap next to the Catapult catches what the Catapult can't.
- [ ] **Sapper beats**: DevConsole (backquote) → spawn-type picker → `sapper` with a tower built. It runs past you to the nearest tower, hacks until the tower shows NO SUPPLY, then goes for the next one. With no towers it fights you like a normal goblin.
- [ ] **Negative cases**: the tower it's draining never shoots it (it's in the blind spot), but a neighbouring tower does. It never drains a tower after dying. Hitting it doesn't make it turn on you (single-minded by design, decided 2026-09-30). Refilling a tower with [E] while a sapper is on it works and the sapper keeps draining.
- [ ] **Dome beats**: DevConsole spawn picker → `dome_warden` next to a few goblins, with an Arrow Tower and a Catapult built. Arrows and ballista bolts stop on the dome with a purple number and no white one. Catapult boulders burst on the shell. Your bow hits the dome from outside and the goblins once you step in. Your sword works. Nets don't root anything inside, and fire/ice arrows don't ignite or chill anything inside. Enough hits break the dome (everything inside is hittable), and it comes back 8s later.
- [ ] **Dome negative cases**: the dome drops the moment the Warden dies, and nothing stays shielded after. A goblin that walks out of the dome is hittable within a quarter-second. Enemy projectiles are never affected.
- [ ] **Towers keep firing into a dome** by design now: their shots wear it down, so it costs supply but isn't wasted.
