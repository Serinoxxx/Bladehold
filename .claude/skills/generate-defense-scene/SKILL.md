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
| `DefenseBiomePaletteSO` | Terrain layers, prefab lists by role (cliffs, rocks, snow, trees, stakes, banners, litter, gate/wall/bridge/spikes), `cliffMaterial` override, lighting. Optional: `ravineWall` layer + `ravineWallRocks`, and `propClusters`. |
| `DefenseHeightfield` | The analytic ground model. **Every stage queries it**, so edges, rims and floors agree. |
| `DefenseTerrainBuilder` | Heights, rule-based splat (slope, floor, roads, courtyard, optional ravine-wall rock), terrain-tree forests. |
| `DefenseSceneScatter` | Scatter rules: cliffs, outcrops, boulders, rim rocks, ravine-wall and ramp-wall rocks, prop clusters, clusters, snow drifts, stakes, banners, litter, courtyard props. |
| `DefenseClusterHarvester` | Lifts the artist's prop groupings out of a Synty demo scene into `PropCluster` templates for a palette. |
| `DefenseSceneGenerator` | Orchestrates the stages. It also builds the castle, bridges, spikes, SpikePit volumes and play-area boundary, bakes the NavMesh, and lays out the gameplay. |
| `DefenseSceneValidator` | Checks the gameplay rules (below). |
| `PrefabMeasure` | Mesh-measured bounds, footprints and grounding for any prefab. |
| `DefenseSceneDefaults` | Builds the stock assets in `Assets/Bladehold/Config/SceneGen/`: `SpikePitConfig`, Outer Gate spec + Alpine palette (tier 1), Desert Gate spec + Arid palette (tier 2, two ravines), Tutorial Gate spec + Kingdom palette (plan 16, no ravines). One menu item per scene, so new stock scenes get a `Create<Scene>` builder here too. |

**Exception: the Tutorial Gate is generated once, then hand-edited.** `TutorialGate_DefenseSpec_GENERATED_ONCE.asset` produced `Bladehold Valley Stronghold.unity` (the tutorial gate, renamed by plan 21), and the tutorial director, steps, waypoints, ammo chest and tutorial pacing were then placed on top. Regenerating it wipes all of that. Edit the scene directly instead. The Kingdom palette (grass terrain layers built from the Fantasy Kingdom ground textures) is free to reuse for other scenes.

Runtime piece: `Hazards/SpikePit.cs` + `SpikePitConfigSO` (one heavy hit on landing, 12 by default, so
normal 10-HP goblins die. A shared registry means walking along the floor between boxes isn't a second
fall.)

## Walls, fort rules and the gate ammo chest (plan 17)

- **Wall plots.** `PlaceWallPlots` runs before the NavMesh bake. It puts one `WallPlot` prefab (`Bladehold Prefabs/Defenses/Walls/WallPlot.prefab`) across every bridge deck, `wallPlotInset` (1.5 m) in from the gate-side rim. The cap is 8, sorted by ravine then x.
  - Each plot gets a `NavMeshModifierVolume` on its own area `WallPlot{i}` (NavMeshAreas 3–10). Runtime `WallNavCost` prices that area while the wall stands shut.
  - The crafting bench goes on the gate-side rim beside the bridge, with its colliders stripped.
  - Spec switch: `wallPlotsOnBridges`.
- **Fort rules.** `PlaceFortRules` instantiates `Bladehold Prefabs/Defenses/FortRules.prefab` (`DefenseSceneRules` for the upgrade wheel, `CrystalRewards`, `SceneCrystalBias`). It copies the palette's `crystalBias` (x Fire, y Ice, z Storm): Arid 3/1/1, Alpine 1/3/1, Graveyard 1/1/3, Kingdom even.
  - Spec switch: `placeFortRules`.
- **Ammo chest.** `Bladehold Prefabs/Economy/AmmoChest.prefab` goes at `ammoChestPosition` (8, 5), outside the gate, facing it.
  - Spec switch: `placeAmmoChest`.
- **Check after a regeneration:** each plot's `NavMesh.SamplePosition` area mask equals `1 << GetAreaFromName("WallPlot{i}")`.
- **Valley Stronghold** (the tutorial gate) has no bridges. Its 3 wall plots sit in gaps in a hand-placed rock line (`Wall Chokepoints`). Never regenerate it.

## Frame and rules the generator enforces

- **Frame.** The gate is at the origin facing +Z, enemies come from +Z, the castle is behind at −Z, and the battlefield is at y = 0.
- **Placement is measured.** Prefabs are grounded on the lowest terrain under their footprint and sunk by a fraction of their own height. Corner-pivot pieces (walls, bridge tiles) are placed by bounds centre (`RootForCentre`). Wall modules tile at their measured width.
- **Gameplay lanes stay clear** of anything with a collider (`DefenseBuildContext.KeepClear`): plots, spawns, the player spawn, the gate apron, roads, bridge approaches, ramp lanes, ravine rims. Field litter has its colliders stripped. Pit spikes are visual only, so their colliders are stripped too.
- **Play-area boundary.** An invisible double-sided MeshCollider ribbon runs 4 m into the foothills. It keeps the player in, and because the NavMesh bakes from physics colliders, it stops enemies walking round a ravine's end. The NavMesh bake volume covers only the valley.
- **NavMesh.** One `NavMeshSurface` per agent type in `NavMesh.GetSettings*`. Today that's Humanoid and **Large Enemy** (radius 1; used by trolls, the supply wagon and the battering ram; renamed from "Troll", same ID). Adding a Medium type later needs no generator change.
- **Ravines.** The floor is baked NavMesh, so flung goblins recover onto it instead of dying from `KnockbackReceiver`'s no-NavMesh fallback. Exit ramps are cut into the **gate-side** bank only, so pathfinding never routes *through* the pit and the only crossings are bridges. Bridge decks sit 0.18 m proud of the rims.
- **Objectives** (cages, catapults, wagon, ram) come from a grid filtered by rule: at least `MaxTowerRange()` (read from the TowerPlot prefab's defences, currently 24 m) plus `objectiveRangeMargin` from every plot, off roads and rims, and reachable from the player. The wagon and ram also need a Large Enemy path to the gate.

## Ravine walls and prop clusters (set dressing that makes a scene read as art)

Both are **opt-in per palette**. The Alpine palette leaves them empty, so the Outer Gate regenerates exactly as before (the rules return before touching the RNG). The Arid palette uses both. To add them to another biome, fill the fields and regenerate.

- **Ravine walls.** A bare ravine is a heightmap cliff, and top-down terrain UVs stretch it into vertical streaks. That looks like "a sudden drop", not a gorge. Two fixes:
  - **`ravineWall` terrain layer.** Painted by height from just under the rim down to the floor, across the ravine band and the ramp cuts' side walls. The floor and the ramp surface keep `ravineFloor`. Use a real rock texture. The Arid palette builds `Config/SceneGen/Arid_RockWall.terrainlayer` from the pack's `RockWall_Texture_01` (the pack ships it only as a mesh material), darkened a little with `diffuseRemapMax`.
  - **`ravineWallRocks`.** Tall, narrow boulders stood shoulder to shoulder along both walls:
    - Sized to the ravine depth, buried below the floor and topped out 0.2–0.8 m under the rim.
    - Long axis along the wall, leaning slightly into the bank.
    - Face at most ~0.4 m onto the floor edge.
    - The gate-side wall is skipped where a ramp cut replaces it; the cut's side wall gets its own lining, sized to the falling wall height.
    - **Colliders are stripped** (visual only, like the spikes), so the floor NavMesh, the ramps, the SpikePit volumes and the validator are unaffected.
  - **Pitfall — ramp-cut walls are near vertical.** Their face is only ~0.8 m wide in plan, so a rock whose face just meets the computed wall line is buried, with slivers poking through. Stand it ~0.6 m proud, into the lane edge. Check with a camera *inside* the ramp lane looking at the wall.
  - **Pitfall — light floors.** A pale `ravineFloor` (e.g. salt cracks) glares and flattens the pit, and it bleeds up the ramps. A mid or darker layer reads as depth and makes the spikes pop.
- **Prop clusters.** Props scattered one at a time read as noise. The pack demos already contain artist-composed groups, like a skeleton among spiky rocks, or a cactus with pebbles, scrub and succulents.
  - **Harvesting.** `DefenseClusterHarvester.Harvest(demoScene, allowPrefixes, featurePrefixes, …)` opens the demo additively (and closes it unsaved). It single-link groups prefab instances within 3.2 m and keeps groups of 5–30 pieces, radius ≤ 9 m, that contain a feature piece. Each piece's height is stored relative to the demo terrain, clamped to [−1.5, 0.15], so pieces that sat on a mound don't float.
  - **Filtering.** Demo scenes group by type, not by cluster, so harvest spatially. Filter out off-theme props: the Arid demo has sci-fi hoses, solar panels and beacons, plus lava and sulphur pools.
  - **Placement.** `PlacePropClusters` stamps them on an 11 m grid in the open field. Only the core (0.4 × radius) must be clear. The whole group gets a random yaw and each piece is re-grounded, and any piece landing in a `KeepClear` lane is dropped. Pieces under 1.5 m tall lose their colliders. Taller rocks and dead trees keep theirs, so they become NavMesh obstacles the validator re-checks.
  - **Harvest in code.** The palette builder in `DefenseSceneDefaults` calls the harvester, so re-running the menu item re-harvests.

## Night scenes: lanterns, fog, glow, stone bridges, enemy lists (Graveyard)

All opt-in per palette or spec. Empty or zero values leave the other stock scenes unchanged (the rules run last and return before touching the RNG).
- **`lanterns`**: lamps down both road edges every `lanternSpacing`, at the bridge corners and either side of the gate. Colliders are stripped. Every instance of these prefabs under Scatter, cluster pieces included, gets a shadowless point light (`lanternLight*`). PC is Forward+, so ~85 lights is fine.
- **`groundFog`**: particle fog on a grid, plus a line along each ravine floor, tinted by `groundFogTint` and prewarmed. It only animates in Play mode, so edit-mode screenshots won't show it.
- **`ravineGlow*`**: point lights low in the pits, so the ravines read at night.
- **`bridgeSpan`**: one arched piece per crossing (Kingdom `SM_Env_Bridge_Stone_01`, 20 m), used instead of tiles. It's widened by `bridgeSpanWidthScale`, and the ends are seated `DeckRise` above the rims by raycasting the piece's own deck. The deck crests about 2 m up, so the validator probes 3 m.
- **Spec `enemyRosterIds` / `fodderEnemyId`**: places a `SceneEnemyRoster` (see `Waves/CLAUDE.md`).
- Roads: when the inner ravine has no centre bridge, a trunk road runs from where the side roads meet to the gate.
- Grave plots are procedural `PropCluster`s built in code (`GraveyardClusters`), not harvested from a demo scene.

## Workflow (via `/unity-editor-mcp`)

1. **New scene:** duplicate `Config/SceneGen/OuterGate_DefenseSpec.asset` and set `scenePath` (under `Assets/Bladehold/Bladehold Scenes/`) and `seed`. Edit the numbers: plots, ravines/bridges/ramps, spawns, `objectiveZRange`. **New biome:** duplicate `Alpine_DefensePalette.asset` and swap the prefabs and layers. Check materials: Alpine's own `SM_Env_Rock_Cliff_*` are **refractive glacier ice**, which is why the Alpine palette uses the Arid cliff meshes with `Snow_Rock_Tri`. Pick terrain layers from the pack's own demo `TerrainData` (its layer shares show which ones the artist used), and avoid a very pale road layer: it reads as painted lines.
2. **Generate.** It can take about a minute, so queue it on `delayCall`. A synchronous call can time out the MCP response even though it completes:
   ```csharp
   var spec = AssetDatabase.LoadAssetAtPath<DefenseSceneSpecSO>("Assets/Bladehold/Config/SceneGen/<Spec>.asset");
   EditorApplication.delayCall += () => DefenseSceneGenerator.Generate(spec);
   ```
   Then wait for `[DefenseSceneGenerator] Generated` in `%LOCALAPPDATA%\Unity\Editor\Editor.log`. The validator report follows it. Menu equivalent: select the spec, then **Bladehold > Scene Gen > Generate Selected Defense Scene Spec**.
3. **Read the report.** Every `FAIL` names the rule and the location. Fix the **spec** (move a plot, add a bridge, widen the objective band), not the scene. Regeneration overwrites the scene file, and hand edits are lost.
4. **Look at it.** Use `manage_camera` screenshots from fixed spots: `[0,35,-25]→[0,0,70]` (overview from the castle), `[8,7,48]→[0,5,0]` (the gate), `[-14,4,50]→[0,-1,58]` (a bridge), `[0,14,140]→[0,3,40]` (from the spawns). For ravine walls, also shoot **along** a ravine from a rim away from bridges (e.g. `[-33,5,51]→[-12,-3,61]`), and look across at a ramp cut from the far bank. Save them to the scratchpad, **not** `Assets/Screenshots`, or move them out before committing.
5. **Play-test.** Enter Play mode in the scene and use `SurvivorsSpawner.DebugSpawnBurst` / `DebugSpawnEnemyType("troll")`. Wait a frame before hitting a fresh spawn: `KnockbackReceiver` subscribes in `Start`. For a fling test, warp a goblin onto a side bridge, then `ReceiveDamage` with `knockbackForce` ≥ its resistance and a sideways `knockbackVelocity`.
6. **Campaign hookup** (for a new scene): follow `/add-campaign-node` (node `sceneName`, graph seed, `AreaDatabase`, build settings, DevConsole button).

## Pitfalls already solved (don't reintroduce)

- **Save the TerrainData before painting.** `CreateAsset` re-initialises the splat textures, and paint applied first comes back as 100% layer 0.
- **Run `Physics.SyncTransforms()` before baking.** Otherwise the first surface misses the new TerrainCollider and bakes only the wall tops.
- **No random values inside a `Sort` comparator.** Precompute the keys.
- **Don't parent the walls under `Gate`.** An empty `Gate.visualsToHide` hides every renderer under the gate when it falls.

## Finish

`/compile-check` for any SceneGen code change. Commit the spec/palette assets, the scene, and its data folder (`Terrain.asset`, `NavMesh-*.asset`, `PlayAreaBoundary.asset`). Record art/feel leftovers with `/editor-wiring-todo`.
