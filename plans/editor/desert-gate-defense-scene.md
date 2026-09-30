# Editor to-do: Desert Gate (generated tier-2 defense scene)

From the 2026-09-30 request for a new Arid Desert defense scene for tier 2, hooked into the campaign.

**What's done:**
- **The assets.** `Config/SceneGen/Arid_DefensePalette.asset` and `DesertGate_DefenseSpec.asset` are built in code by **Bladehold > Scene Gen > Create or Refresh Desert Gate Assets (Arid)** (`DefenseSceneDefaults.CreateDesertGate`).
  - The palette's nature comes from `PNB_Arid_Desert`: Sand_01 ground, Sand_04 drifts, RedSand slopes, faces, trails and ravine floors, the pack's triplanar cliffs, dead trees and cacti, bones and tumbleweed. The castle, bridges and battlefield props stay Fantasy Kingdom.
  - The spec is the Outer Gate shape pushed harder: an inner ravine at z 60 with three bridges, plus an outer ravine at z 112 with only two bridges (x −24 and +26), offset so the warband zigzags. The field is 80 m half-width and spawns are about 165 m out.
- **The scene.** `Bladehold Desert Gate.unity` was generated and **passes the validator**:
  - All 10 spawns reach the gate over bridges as both Humanoid and Large Enemy.
  - The floors of both ravines walk out.
  - The 6 plots are flat.
  - All 8 objective markers are at least 32 m from every plot.
- **Campaign wiring.** The tier-2 node `tier2_armory_barracks` is now **Desert Gate / Sunscorched Canyon**. It keeps Captain Kombusta, the Enraged tier and the +5 Orcish Metal reward. The node asset, graph seed, `AreaDatabase`, build settings (enabled) and DevConsole button all point at the new scene.
- **Set dressing (2026-09-30 follow-up).** Both features are opt-in per palette, so Alpine is unchanged.
  - Rock walls: 129 boulders line the walls and ramp cuts (no colliders), over an `Arid_RockWall` terrain layer. The pit floor is now RedSand instead of the glaring salt cracks.
  - Prop clusters: 29 groups (~690 pieces) harvested from the Arid demo scene and stamped between the roads.
  - The validator still passes.
- **Smoke-tested via MCP.** In Play mode, 8 goblins and a troll spawned on the NavMesh with complete paths. The goblins crossed the outer ravine on both bridges, and the console showed no errors.
- **Not deleted:** `Bladehold Castle Armory.unity` and `BuildCastleLevels.BuildArmoryScene`. Nothing in the campaign uses them now, and the scene is disabled in build settings.

**Layout changes go in the spec, then regenerate. Hand edits to the scene are lost.**

## Look and feel (human)

- [ ] **Walk it in Play mode** and judge scale: the gap between the two ravines (52 m) and the spawn distance (~165 m). Is the outer ravine too far out to matter, given it's beyond tower range?
- [ ] **Colour.** Everything is warm orange-tan. Consider a second rock tone: the palette's `cliffMaterial` set to `Rock_Triplanar_Red_01` or `_Yellow_01`, or `LavaRock` as the `cliff` layer (it read as flat dark cones on the notch mountains in the first pass).
- [ ] **Castle town.** Fantasy Kingdom timber houses behind a desert wall. Decide whether that's fine or whether the courtyard needs desert buildings (not in `PNB_Arid_Desert`; see `/find-and-import-assets`).
- [ ] **Lighting pass:** the warm sun (1.5), sandy fog (0.003), the Arid demo `Global Volume Profile`, and the built-in procedural sky. Consider `FX_Dust_Blowing_01`, `FX_TumbleWeed_01` or `FX_Vulture_01` from the pack for ambience.
- [ ] **Terrain trees.** The dead trees and cacti log "must use the Nature/Soft Occlusion shader" warnings at generation (billboards). Check they look right at distance.
- [ ] **Ravine walls.** Walk a bridge and look down.
  - Do the boulders read as a gorge?
  - Does anything stand in a ramp lane (they're collider-free, so goblins would walk through)?
  - Are there obvious gaps where the rim meets a bridge?
- [ ] **Prop clusters.**
  - Density: 29 groups. Tune the grid/probability in `PlacePropClusters` or the harvest filters in `CreateAridPalette`.
  - Check no cluster's tall rock or dead tree blocks a line of fire from a tower plot.

## Gameplay (human)

- [ ] **Full run from the map.** Clear the Outer Gate → pick Desert Gate → 5 waves with Kombusta → victory → the node completes and tier 3 opens.
- [ ] **Objectives.** The markers sit either between the ravines or beyond the outer one. Check that the wagon and ram cross both ravines cleanly and that the objectives read well.
- [ ] **Difficulty.** Tier 2 threat with two choke lines: tune bridge counts and widths in the spec if it's too easy or too hard.

## Follow-ups (optional)

- [ ] Delete `Bladehold Castle Armory.unity` and the Armory section of `BuildCastleLevels` (as was done for the Courtyard), once you're sure nothing else needs them.
