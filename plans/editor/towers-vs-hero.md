# Editor to-do: towers vs hero (blind spots, Sapper, Dome Warden, Hexer)

No plan file; from the 2026-09-29 design chat (towers and hero doing the same job). Unity MCP was connected on 2026-09-30 for the prefab generation and benchmark; the play-mode checks below haven't run yet. Tick items off as you go and delete the file when it's empty.

## Verify first

- [x] **Scripts import clean.** Done via MCP 2026-09-30: compiled with no console errors, .meta files committed. Focus Unity and check the Console. New files: `Enemies/Sapper/TowerSapper.cs`, `TowerSapperSO.cs`, `Enemies/DomeWarden/ProjectileDome.cs`, `ProjectileDomeSO.cs`, `WardenEscort.cs`, `WardenEscortSO.cs`. Their `.meta` files are created on import, so commit those afterwards.

## Wiring

- [x] **Generate the Sapper and Dome Warden prefabs** Done via MCP 2026-09-30 (Hexer too, same day) (`GenerateById` for both; every reference wired, benchmark 19F/19G/19H pass, 162 passed / 3 unrelated fails: Bulwark attack, Bannerman rig, victory button label). *(MCP-able)*: **Bladehold > Generate Enemy Prefabs**. This creates `Bladehold Prefabs/Sapper Enemy Variant.prefab`, `Enemies/Sapper/TowerSapperSO.asset` and the `sapper` entry in `EnemyPrefabMap.asset` (plus `Dome Warden Enemy Variant.prefab`, its `ProjectileDomeSO` + `WardenEscortSO` assets and `dome_warden`). `TowerSapper.data`, `health`, `targetSelector` and `animator` are wired by the generator (and auto-wire in `OnValidate` except `data`). Verify: benchmark 19F "Sapper Prefab" passes.
- [ ] **Sapper look** (art pass): it's a plain goblin at 0.9 scale. Give it something readable at a glance (a pickaxe, a satchel, a tint) so players learn "that one goes for my towers".
- [ ] **`TowerSapper.drainFeedback`** (optional `MMF_Player` on the prefab): a hacking thunk + wood chips at the tower each tick. Without it the only sign is the tower's supply bar dropping.
- [ ] **Dome Warden visual check** *(MCP-able)*: the generator creates `Enemies/DomeWarden/ProjectileDomeSO.asset` with `domeVisualPrefab` = `Bladehold Prefabs/VFX/BubbleShieldVisual.prefab` (the Bubbler's sphere), scaled to a 10m-wide dome, half-buried (`visualHeightOffset` 0). It may need its own, more transparent material at that size so it doesn't hide the fight. Its colliders are switched off at runtime. The generator also wires `ProjectileDome.hitNumberPrefab` to the goblin's own damage popup; check it actually shows purple (`hitNumberColor` on the SO), in case that popup prefab forces its own colour. Verify: benchmark 19G "Dome Warden Prefab" passes.
- [ ] **Dome Warden look + `ProjectileDome` feedbacks** (optional `MMF_Player`s): `hitFeedback` (glassy ping at the hit point), `breakFeedback` (shatter), `rebuildFeedback` (the Warden raising it again). Without them the dome just pulses, vanishes and reappears.
- [ ] **Hexer look + beam** (art pass): a plain goblin; the beam is `LS_Chain_02` (`TowerHexerSO.beamPrefab`), chosen to differ from the Bubbler's `LS_Chain_01`. Pick a colour that reads as "hex" (purple, like the HEXED marker). Optional `TowerHexer.hexStartFeedback` (at the tower) and `interruptFeedback` (at the Hexer).
- [ ] **HEXED marker icon**: `ObjectiveWaypointTrackerUI` reuses `noSupplyIcon` tinted purple. Give it its own icon if the two get confused.

## UI review

- [ ] **Build wheel range line.** Hovering a slice now adds `Range: Medium  |  Blind spot: Small` (or `Trap: hits what walks over it`) under the description at 85% size, in `descriptionLabel` on `Bladehold HUD.prefab` → `BuildWheelUI`. Check it fits without overflowing, and with gamepad focus as well as mouse. The buckets are tunable on `BuildWheelUI` (`mediumRangeFrom` 12, `longRangeFrom` 20, `mediumBlindSpotFrom` 5, `largeBlindSpotFrom` 7).

## Tuning

- [ ] **Blind spot sizes**: `minRange` on each `Bladehold Prefabs/Defenses/Defense_*.prefab`. Currently Arrow 3, Net Thrower 4, Ballista 6, Catapult 8.
- [ ] **Sapper numbers**: `TowerSapperSO`. Currently drain 4 supply/s, drain range 2.8m (keep it under the Arrow Tower's 3m blind spot), search 60m. Roster row: 30 HP, speed 4.2, from wave 2 at threat tier 2, 15% weight, max 3 at once.
- [ ] **Dome Warden numbers**: `ProjectileDomeSO`: radius 5m, 150 dome HP, back up 8s after breaking. Stands 9m off its target (`NavMeshAgent` stopping distance on the prefab). Roster row: 80 HP, speed 2.8, from wave 2 at threat tier 3, 12% weight, max 1 at once.
- [ ] **Hexer numbers**: `TowerHexerSO`: channels within 11m (stands 10m off), breaks 1.5s on a hero hit, search 45m. Roster row: 35 HP, speed 3.6, from wave 3 at threat tier 2, 12% weight, max 2 at once.
- [ ] **Escort numbers**: `WardenEscortSO`: 12 slots on a 3.5m ring, recruits `goblin`s within 16m, all break formation when you come within 6m of the Warden.
- [ ] **Tower damage** (cut to ~28%, hard-coded in each `ApplyLevelStats`): Arrow 5/9/14, Ballista 31/53/84, Catapult 13/22/36, Spike Trap 24/41/64, Oil Vat 6/10/15 per second, Net 3 per level. Tesla Spire's damage comes from its draft card and wasn't touched.
- [ ] **Wave size**: `Bladehold Config/SurvivorsRoundPacingConfig.asset`: `maxConcurrentEnemies` 40, quotas 40/45/50/55/60. Watch frame rate with 40 agents plus towers, and whether supply income (per kill) now outpaces tower costs.

## Playtest

- [ ] **Blind spot**: build an Arrow Tower and a Catapult, and let goblins walk up to each. The tower stops shooting anything hugging it and resumes once they step out. A Spike Trap next to the Catapult catches what the Catapult can't.
- [ ] **Sapper beats**: DevConsole (backquote) → spawn-type picker → `sapper` with a tower built. It runs past you to the nearest tower, hacks until the tower shows NO SUPPLY, then goes for the next one. With no towers it fights you like a normal goblin.
- [ ] **Negative cases**: the tower it's draining never shoots it (it's in the blind spot), but a neighbouring tower does. It never drains a tower after dying. Hitting it doesn't make it turn on you (single-minded by design, decided 2026-09-30). Refilling a tower with [E] while a sapper is on it works and the sapper keeps draining.
- [ ] **Dome beats**: DevConsole spawn picker → `dome_warden` next to a few goblins, with an Arrow Tower and a Catapult built. Arrows and ballista bolts stop on the dome with a purple number and no white one. Catapult boulders burst on the shell. Your bow hits the dome from outside and the goblins once you step in. Your sword works. Nets don't root anything inside, and fire/ice arrows don't ignite or chill anything inside. Enough hits break the dome (everything inside is hittable), and it comes back 8s later.
- [ ] **Dome negative cases**: the dome drops the moment the Warden dies, and nothing stays shielded after. A goblin that walks out of the dome is hittable within a quarter-second. Enemy projectiles are never affected.
- [ ] **Towers keep firing into a dome** by design now: their shots wear it down, so it costs supply but isn't wasted.
- [ ] **Escort beats**: spawn a `dome_warden` with 20+ goblins around (DevConsole +50 burst). Up to 12 goblins form a ring inside the dome and walk with the Warden, ignoring you at range. Walk within about 6m and they all charge. Escorts that die are replaced from nearby goblins. Killing the Warden sends every escort back to normal chasing.
- [ ] **Escort negative cases**: sappers, bulwarks and other specials never join the ring. A goblin never belongs to two Wardens at once.
- [ ] **Hexer beats**: spawn a `hexer` with two towers built. It walks to about 10m from the nearer one, a beam connects, the tower stops firing and shows HEXED on the HUD and `[HEXED]` in its prompt. Hitting the Hexer drops the beam for 1.5s. Killing it, or knocking it away, frees the tower at once. A second Hexer goes for the other tower.
- [ ] **Hexer negative cases**: a hexed tower still refunds on victory. It still refills/upgrades with [E]. It never stays hexed after the Hexer dies. Tower hits on the Hexer don't break the channel.
