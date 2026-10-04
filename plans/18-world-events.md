# 18: Biome world events

**Goal:** battles get a mid-wave twist that matches the biome. Each event has hazards that hit the player **and** enemies, a player buff to lean into, its own grade, weather and sound, and a big announcement banner listing its boons and perils.

**Status (2026-10-04):** built and play-checked through Unity MCP in Desert Gate, Outer Gate, Graveyard and the Survivors Scene. Requested by Lance in chat ("do them all"); note this is new scope during the feature freeze.

## The events

| Event | Biome / scenes | Hazard (hits player and enemies) | Buff while active |
|---|---|---|---|
| **Eruption** | Desert Gate, Survivors Scene | Volleys of meteors on red circles (some aimed at the player, some at enemy packs); impacts ignite enemies; half leave a magma pool | +50% Fire damage (`FireDamageBonus`) |
| **Blizzard** | Outer Gate, Frozen Pass | Whiteout; every 6-9 s a howl and wind streaks warn, then a gale shoves the player and every enemy within 45 m and chills the enemies | +50% damage to chilled foes (`ChilledDamageBonus`) |
| **Thunderstorm** | Graveyard, Ancient Garden | Rain, darker grade, a bolt every ~1 s on a blue circle; shocks enemies and arcs to 2 more | +50% Lightning damage (`LightningDamageBonus`) |
| **Stampede** | Survivors Scene, Ancient Garden | A marked lane (rumble + dust at its start), then a galloping herd tramples and flings everything in it. Lanes are clipped to unbroken NavMesh (never through walls or over ravines) | +15% move speed |
| **Blood Moon** | Graveyard | Enemies killed near the player may rise again as frail skeletons after a grave-glow telegraph (35%, max 10 up, elites never rise) | +5% life steal |
| **Goblin Caravan** | Every scene above | A gold wagon (with a loot beam and HUD waypoint) and a goblin escort cross the field leg after leg; hits spill gold bags, wrecking it bursts 10 more; it leaves with the gold when the timer ends | none (it is the reward) |

Tutorial, castle interiors, the Fishing Pond and hub scenes have no `WorldEvents` prefab, so they never roll one.

## How it works

- **`WorldEventDirector`** (`WorldEvents/`) on the `WorldEvents` prefab: when a wave starts it rolls `WorldEventScheduleSO.chancePerWave` (0.6, from wave 2), waits 20-40 s, then starts a weighted pick (no repeats). An event ends on its timer, when the wave resolves, on victory, or when the player dies.
- **`WorldEvent`** base: runs the window, fades its own global `Volume` (priority 50) in and out, keeps its weather particles on the player, adds the stat buff on start and removes exactly that on end, and plays the start/ambience/end `MMF_Player`s. Subclasses only spawn hazards. Hazards start 3 s after the banner so it can be read.
- **Tunables** are on one `*EventSO` per event in `Assets/Bladehold/Config/WorldEvents/` (damage, cadence, radii, banner title, tagline and effect lines with `{0}` = buff % and `{1}` = duration). Hazard damage to enemies is max(flat, % max HP) with a cap, so it stays relevant at high tiers but never melts captains.
- **Kills pay.** Hazard damage carries no source, so `CoinDropper` and kill objectives credit the player. Risen skeletons and caravan escorts go through the new `SurvivorsSpawner.SpawnEnemyAt`, so they count as wave enemies (gold, supply, and the wave can't wipe while they stand).
- **Banner** (`WorldEventBanner.prefab`, `WorldEventBannerUI`): emblem diamond in the event colour, Texturina title, Grenze tagline, BOONS / PERILS columns built from `WorldEventEffectRow.prefab`, a duration badge, and a bar draining over 5 s. It then folds into a timer chip under the minimap, which shows "Caravan looted!" / "The caravan got away" when relevant. Runs on scaled time, so pause freezes it.
- **Player shove:** new `PlayerShoveReceiver` on the Player (`SidekickSyntyCharacter`): gusts nudge the CharacterController without taking controls away; ignored while mounted.
- **Scene setup:** `WorldEventsSceneSetup.Place(ids)` (editor) drops the prefab and enables only the listed ids. `DefenseSceneGenerator` calls it with the new palette field `DefenseBiomePaletteSO.worldEventIds` (Alpine: blizzard + caravan; Arid: eruption + caravan; Graveyard: blood_moon + thunderstorm + caravan; Kingdom: none), so regenerated scenes keep their events.
- **DevConsole:** a World Event picker with Start / Stop.
- **Localization:** all banner text is in `Strings.csv` (`world_event.*`, all 9 languages).

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/18-world-events.md`](editor/18-world-events.md).
