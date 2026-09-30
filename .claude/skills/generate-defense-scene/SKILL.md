---
name: generate-defense-scene
description: Use when building, re-skinning or tuning a Bladehold gate-defense battle scene (like the tier-1 Outer Gate) — author a DefenseSceneSpecSO layout + DefenseBiomePaletteSO, run DefenseSceneGenerator (terrain, castle wall/gate/buildings, spike ravines with bridges and exit ramps, measured Synty scatter, NavMesh for every agent type, Survivors wiring), read the validator report, and review screenshots. Never hand-edit the generated scene.
---

# Generate a defense scene

Gate-defense scenes are **generated**, not hand-built. A spec asset (layout, in metres) plus a palette
asset (what it's dressed with) go through one call, and the result is a complete, wired, baked scene
followed by a rule check. Code: `Assets/Bladehold/Bladehold Scripts/Editor/SceneGen/`.

| File | Role |
|---|---|
| `DefenseSceneSpecSO` | Layout: valley/notch/field size, mountains, courtyard + building placements, ravines (bridges, exit ramps), plots, spawns, objective band, scatter density. |
| `DefenseBiomePaletteSO` | Terrain layers, prefab lists by role (cliffs, rocks, snow, trees, stakes, banners, litter, gate/wall/bridge/spikes), `cliffMaterial` override, lighting. |
| `DefenseHeightfield` | The analytic ground model. **Every stage queries it**, so edges, rims and floors agree. |
| `DefenseTerrainBuilder` | Heights, rule-based splat (slope, floor, roads, courtyard), terrain-tree forests. |
| `DefenseSceneScatter` | Scatter rules: cliffs, outcrops, boulders, rim rocks, clusters, snow drifts, stakes, banners, litter, courtyard props. |
| `DefenseSceneGenerator` | Orchestrates the stages. It also builds the castle, bridges, spikes, SpikePit volumes and play-area boundary, bakes the NavMesh, and lays out the gameplay. |
| `DefenseSceneValidator` | Checks the gameplay rules (below). |
| `PrefabMeasure` | Mesh-measured bounds, footprints and grounding for any prefab. |
| `DefenseSceneDefaults` | Builds the stock assets in `Assets/Bladehold/Config/SceneGen/`: the Outer Gate spec, the Alpine palette and `SpikePitConfig`. |

Runtime piece: `Hazards/SpikePit.cs` + `SpikePitConfigSO` (one heavy hit on landing, 12 by default, so
normal 10-HP goblins die. A shared registry means walking along the floor between boxes isn't a second
fall.)

## Frame and rules the generator enforces

- **Frame.** The gate is at the origin facing +Z, enemies come from +Z, the castle is behind at −Z, and the battlefield is at y = 0.
- **Placement is measured.** Prefabs are grounded on the lowest terrain under their footprint and sunk by a fraction of their own height. Corner-pivot pieces (walls, bridge tiles) are placed by bounds centre (`RootForCentre`). Wall modules tile at their measured width.
- **Gameplay lanes stay clear** of anything with a collider (`DefenseBuildContext.KeepClear`): plots, spawns, the player spawn, the gate apron, roads, bridge approaches, ramp lanes, ravine rims. Field litter has its colliders stripped. Pit spikes are visual only, so their colliders are stripped too.
- **Play-area boundary.** An invisible double-sided MeshCollider ribbon runs 4 m into the foothills. It keeps the player in, and because the NavMesh bakes from physics colliders, it stops enemies walking round a ravine's end. The NavMesh bake volume covers only the valley.
- **NavMesh.** One `NavMeshSurface` per agent type in `NavMesh.GetSettings*`. Today that's Humanoid and **Large Enemy** (radius 1; used by trolls, the supply wagon and the battering ram; renamed from "Troll", same ID). Adding a Medium type later needs no generator change.
- **Ravines.** The floor is baked NavMesh, so flung goblins recover onto it instead of dying from `KnockbackReceiver`'s no-NavMesh fallback. Exit ramps are cut into the **gate-side** bank only, so pathfinding never routes *through* the pit and the only crossings are bridges. Bridge decks sit 0.18 m proud of the rims.
- **Objectives** (cages, catapults, wagon, ram) come from a grid filtered by rule: at least `MaxTowerRange()` (read from the TowerPlot prefab's defences, currently 24 m) plus `objectiveRangeMargin` from every plot, off roads and rims, and reachable from the player. The wagon and ram also need a Large Enemy path to the gate.

## Workflow (via `/unity-editor-mcp`)

1. **New scene:** duplicate `Config/SceneGen/OuterGate_DefenseSpec.asset` and set `scenePath` (under `Assets/Bladehold/Bladehold Scenes/`) and `seed`. Edit the numbers: plots, ravines/bridges/ramps, spawns, `objectiveZRange`. **New biome:** duplicate `Alpine_DefensePalette.asset` and swap the prefabs and layers. Check materials: Alpine's own `SM_Env_Rock_Cliff_*` are **refractive glacier ice**, which is why the Alpine palette uses the Arid cliff meshes with `Snow_Rock_Tri`.
2. **Generate.** It can take about a minute, so queue it on `delayCall`. A synchronous call can time out the MCP response even though it completes:
   ```csharp
   var spec = AssetDatabase.LoadAssetAtPath<DefenseSceneSpecSO>("Assets/Bladehold/Config/SceneGen/<Spec>.asset");
   EditorApplication.delayCall += () => DefenseSceneGenerator.Generate(spec);
   ```
   Then wait for `[DefenseSceneGenerator] Generated` in `%LOCALAPPDATA%\Unity\Editor\Editor.log`. The validator report follows it. Menu equivalent: select the spec, then **Bladehold > Scene Gen > Generate Selected Defense Scene Spec**.
3. **Read the report.** Every `FAIL` names the rule and the location. Fix the **spec** (move a plot, add a bridge, widen the objective band), not the scene. Regeneration overwrites the scene file, and hand edits are lost.
4. **Look at it.** Use `manage_camera` screenshots from fixed spots: `[0,35,-25]→[0,0,70]` (overview from the castle), `[8,7,48]→[0,5,0]` (the gate), `[-14,4,50]→[0,-1,58]` (a bridge), `[0,14,140]→[0,3,40]` (from the spawns). Save them to the scratchpad, **not** `Assets/Screenshots`, or move them out before committing.
5. **Play-test.** Enter Play mode in the scene and use `SurvivorsSpawner.DebugSpawnBurst` / `DebugSpawnEnemyType("troll")`. Wait a frame before hitting a fresh spawn: `KnockbackReceiver` subscribes in `Start`. For a fling test, warp a goblin onto a side bridge, then `ReceiveDamage` with `knockbackForce` ≥ its resistance and a sideways `knockbackVelocity`.
6. **Campaign hookup** (for a new scene): follow `/add-campaign-node` (node `sceneName`, graph seed, `AreaDatabase`, build settings, DevConsole button).

## Pitfalls already solved (don't reintroduce)

- **Save the TerrainData before painting.** `CreateAsset` re-initialises the splat textures, and paint applied first comes back as 100% layer 0.
- **Run `Physics.SyncTransforms()` before baking.** Otherwise the first surface misses the new TerrainCollider and bakes only the wall tops.
- **No random values inside a `Sort` comparator.** Precompute the keys.
- **Don't parent the walls under `Gate`.** An empty `Gate.visualsToHide` hides every renderer under the gate when it falls.

## Finish

`/compile-check` for any SceneGen code change. Commit the spec/palette assets, the scene, and its data folder (`Terrain.asset`, `NavMesh-*.asset`, `PlayAreaBoundary.asset`). Record art/feel leftovers with `/editor-wiring-todo`.
