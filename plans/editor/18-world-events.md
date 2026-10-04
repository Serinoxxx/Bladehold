# Editor to-do: plan 18 (Biome world events)

From [plan 18](../18-world-events.md), 2026-10-04. Unity MCP was connected: every asset, prefab and scene below was built and play-checked by an agent. What's left needs your eyes, ears and taste. Tick items off as you go and delete the file when it's empty.

## Done by an agent via MCP

- [x] **Assets:** `Assets/Bladehold/Config/WorldEvents/` (6 event SOs, `WorldEventSchedule`, `CaravanWagonHealth`, `PostProfiles/*_Post`), `Assets/Bladehold/Bladehold Prefabs/WorldEvents/` (`WorldEvents`, `WorldEventBanner`, `WorldEventEffectRow`, `CaravanWagon`, `StampedeRunner`, `EruptionMeteor`, `MagmaPool`, 3 tinted telegraphs, `VFX/*Burst` timed variants, 2 horse loop controllers).
- [x] **Player.prefab:** `PlayerShoveReceiver` added on `SidekickSyntyCharacter`, refs wired.
- [x] **Scenes:** `WorldEvents` placed in Desert Gate, Outer Gate, Graveyard, Frozen Pass, Ancient Garden and the Survivors Scene. Palettes carry `worldEventIds` so a regenerate keeps them.
- [x] **Play-checked:** Eruption (Desert Gate), Caravan (spills and wreck pay out, outcome line shows), Blizzard (gusts shove and chill, buff removed on end), Thunderstorm and Blood Moon (5 of 10 kills rose) in the Graveyard, Stampede (trample kills pay) in the Survivors Scene. Console clean.

## UI review

- [ ] **Banner and chip.** Open any battle scene → Play → DevConsole (backquote) → **World Event** ◄/► → **Start**. Judge: Synty Level-Up plate and diamond emblem, Texturina title / Grenze body, the BOONS / PERILS wording (edit `effects` on each `*EventSO`; keep lines under ~28 chars), 5 s linger (`lingerSeconds` on `WorldEventBanner`), and position (`Banner` at y −215, scale 0.9; `Chip` under the minimap at (−40, −400)). Check 16:9 and ultrawide, and that the banner doesn't cover the player during a wave.
- [ ] **Icons are Synty placeholders** (Element_Fire / Ice, Map_Lightning / Horse / Treasure, Status_Cursed for Blood Moon). Bespoke emblems (volcano, gale, storm cloud, hooves, blood moon, wagon) were not generated: image generation isn't configured in the MCP Asset Generation tab. *Agent-doable with `/generate-sprite-variants` once a fal or OpenRouter key is set.*

## Art and audio pass (placeholders chosen from what's in the project)

- [ ] **Sound picks.** Each event's `Feedbacks/*MMF` under `WorldEvents.prefab`. Gaps worth a proper asset: a **wind howl** for Blizzard (currently a deep whoosh), a **volcano rumble** loop for Eruption (currently Mogra's acid warm-up loop at 0.25), a **stampede rumble** (currently Heavy Stomp + whinnies), a **church bell toll** for Blood Moon (currently `chime_bell_10`). *`/find-and-import-assets` can search the Asset Inventory DB.*
- [ ] **Stampede herd model.** `StampedeRunner` uses the Malbers *undead* horse mesh with barding hidden: it reads grey and skeletal. Swap in a brown horse or other herd animal if you have one (keep the in-place gallop and +Z facing).
- [ ] **Caravan wagon.** `CaravanWagon.prefab`: Synty trader wagon + coin pile + trotting horse + golden loot beam. Check the horse sits at the shafts (`Horse` at z 4.2) and the box collider fits, at gameplay camera distance.
- [ ] **Grades.** `PostProfiles/*_Post` replace (not add to) the scene grade for the overridden params. Tuned once per biome; look at each in its scene. Desert's own lens-dirt bloom makes bright meteor bursts flare, which is fine but strong.

## Playtest

- [ ] **Natural rolls:** play a real sector from the campaign map. From wave 2 an event should start 20-40 s into roughly 60% of waves, at most one per wave, never the same twice in a row. It must **never** start in prep, the draft, the tutorial or the castle interiors, and must end when the wave resolves or you die.
- [ ] **Fairness:** meteors (6 dmg), bolts (7), trample (10) on a 55 HP hero. Standing still in an Eruption is meant to hurt; moving should dodge nearly everything. Tune on each SO.
- [ ] **Caravan pacing:** 400 HP wagon, 3.5 m/s, ~230 gold if wrecked (× the gold multiplier). The Goblin Brute escort can trigger its first-encounter intro the first time; decide if that's OK mid-event.
- [ ] **Blood Moon:** risen skeletons hold the wave open until killed. Check a wave can't stall with one stuck somewhere.
