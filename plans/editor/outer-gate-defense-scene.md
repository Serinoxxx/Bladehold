# Editor to-do: Outer Gate (generated tier-1 defense scene)

From the 2026-09-30 request to replace Castle Courtyard with a generated Alpine gate-defense scene.

**What's done:**
- **The generator.** Code is in `Editor/SceneGen/` (see `/generate-defense-scene`). Stock assets are in `Config/SceneGen/`.
- **The scene.** `Bladehold Outer Gate.unity` was generated and passes the validator: all 10 spawns reach the gate over bridges as both Humanoid and Large Enemy, the ravine floor walks out via the ramps, the plots are flat, and the objectives are at least 32 m from every plot.
- **Campaign wiring.** The tier-1 node, graph seed, `AreaDatabase`, build settings (enabled) and DevConsole button all point at the new scene. The old Courtyard scene and its builder are deleted.
- **NavMesh agent type** "Troll" is renamed "Large Enemy" (same ID).
- **Play-tested via MCP:**
  - Goblins and a troll path to the gate.
  - A goblin flung off a side bridge dies on the spikes.
  - A brute that falls in takes one 12-damage hit and climbs out by the ramp in about 10 s.

**Anything below that changes the layout goes in the spec (`OuterGate_DefenseSpec.asset`), then regenerate. Hand edits to the scene are lost.**

## Look and feel (human)

- [ ] **Walk it in Play mode** and judge scale: field width (75 m half), distance from the gate to the ravine (58 m), spawn distance (~150 m). Tweak the spec numbers.
- [ ] **Cliff ring at the far end.** It can read as a regular wall of boxy rocks. Tune the `PlaceCliffs` count/scale, or the palette's cliff list.
- [ ] **Mountain outcrops.** A few big slabs can look perched on steep slopes. Tune the palette's `outcropScale` or the outcrop sink.
- [ ] **Gate facing.** Check that the gate model's decorated face points at the field (`gateModelYaw` 180 in the palette) and that the wall modules face out too (`wallYaw`).
- [ ] **Lighting pass:** sun angle and colour, fog density (0.0035), the `AlpineGlobalVolume` grade, and the built-in procedural skybox. Consider a Synty sky or a falling-snow FX (`PNB_Alpine_Mountain/FX/FX_Snow_01`).
- [ ] **Castle town behind the wall.** Building packing is random within the courtyard. Check nothing clips a cliff and the silhouette over the wall reads well.

## Gameplay (human)

- [ ] **Full run from the map.** Tier-1 node → the scene loads → 5 waves → victory → the node completes and tier 2 opens.
- [ ] **Objectives.** Cages, catapults, the wagon and the ram spawn beyond the ravine, and the wagon and ram cross the centre bridge.
- [ ] **Pit feel.**
  - Is 12 impact damage right (`Config/SceneGen/SpikePitConfig.asset`)?
  - Do enough player hits fling hard enough to use the bridges?
  - The pit has no feedback yet (sound, blood). Add an `MMF_Player` to `SpikePit` via `/feel-integration` if you want one.
- [ ] **Ammo chest.** When `AmmoChest.prefab` exists, add its placement to the generator (just inside the gate) rather than hand-placing it (see `ammo-chest.md`).

## Follow-ups (optional)

- [ ] **Frozen Pass has no Large Enemy NavMesh bake** (only Humanoid), so trolls, wagons and rams can't path there. Either bake a second surface or regenerate it as a defense scene.
