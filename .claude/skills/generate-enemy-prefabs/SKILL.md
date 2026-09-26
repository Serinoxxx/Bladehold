---
name: generate-enemy-prefabs
description: Use when building or updating an enemy prefab variant in Bladehold — author an EnemyManifest entry and run the EnemyPrefabGenerator (which also registers the id in EnemyPrefabMap for SurvivorsSpawner) instead of hand-wiring prefabs in the Editor.
---

# Generate enemy prefabs

Enemy prefabs are **generated**, not hand-built: an `EnemySpec` in
`Assets/Bladehold/Bladehold Scripts/Editor/EnemyManifest.cs` + one run of
**Bladehold > Generate Enemy Prefabs** (`Editor/EnemyPrefabGenerator.cs`) produces a prefab
*variant*, creates missing per-enemy SO assets, wires component references, and registers the id in
`Enemies/EnemyPrefabMap.asset` (`EnemyPrefabMapSO`). **No scene edits, ever.**

Who reads the map: `SurvivorsSpawner` (sector waves, DevConsole spawns, captains via
`DebugSpawnEnemyType`), `Debug/EnemyZoo.cs` and the Enemy Manager window. The spawner only loads a
type when its `Config/Enemies.csv` row exists, is `enabled`, **and** the map has the id; whether it
appears in waves is then the row's `minThreat`/`unlockWave` (see `/add-enemy-type`).

Division of labour: `Enemies.csv` is the *balance sheet* (edit it directly or via
**Bladehold > Enemy Manager** → Stats tab), the manifest is the *structure* (components, SOs,
children, wiring), and art (materials/models/VFX/animator states) is a manual Editor pass.

## Step 1: author the manifest entry

Add an `EnemySpec` to `EnemyManifest.Entries`. The doc comments on `ChildSpec`/`SoSpec`/`ComponentSpec`/`EnemySpec` are the ground truth:

- `id`: the roster CSV id. `prefabName`: `"<Name> Enemy Variant"`, saved to `Assets/Bladehold/Bladehold Prefabs/`.
- `basePrefabPath`: optional parent prefab (Bannerman uses `Goblin Brute Enemy Variant.prefab`). Default `Goblin Enemy (Base).prefab`. Changing it on an existing variant **re-bases** it: the variant is rebuilt from the new parent.
- `rootScale`: authored on the variant root. The CSV `scale` column then *replaces* the root scale at spawn when it isn't 1 (`EnemyDefinitionApplier` sets `localScale = Vector3.one * scale`), so keep the two consistent (Kombusta: 1.35 in both).
- `materialPath`: optional **existing** material for the body `SkinnedMeshRenderer` slot 0. Skipped when the prefab carries a `ModelSwapRecord` (model swapped via Enemy Manager → Model tab). The generator never creates materials.
- `animatorOverridePath`: optional **existing** `RuntimeAnimatorController`/override for the rig Animator (e.g. `Bladehold Prefabs/Brute Override.overrideController`).
- `disableBaseAIAttack`: set whenever the enemy has its own attack component (disabled, never removed). Leave false when the new component *drives* `AIAttack` (Kombusta).
- `removeComponents`: e.g. `new[] { typeof(GoldenGoblin), typeof(ImpulseGoblin) }` so special enemies don't roll golden/impulse variants.
- `children`: fire points etc., created with the root's layer. In `wire`, use `ctx.FindOrCreateChild(name, pos)` or `ctx.FindOrCreateBoneChild(name, "Spine_02", pos, rot)` to parent to a rig bone.
- `assets`: per-enemy SOs at `Enemies/<soFolder>/<assetName>.asset`. `initDefaults` runs **only on first creation**; an existing asset is never overwritten (tuning survives), and a type mismatch throws.
- `components`: ensured on the root. `wire(so, ctx)` runs on **every** pass. Wire with `EnemyPrefabGenerator.SetReference(so, "field", value)`, which throws on a missing/renamed field. `ctx` gives `Root`, `ChildAnimator` (Synty rigs keep it on a child), `Health`, `Movement`, `LoadedAsset(name)`. Manifest helpers `LoadPrefab`/`LoadProjectile<T>`/`LoadAsset<T>` throw on a missing path.
- `navStoppingDistance`: ranged stand-off (Forest Guardian 8). Never touch agent *avoidance* (`AIMovement` owns it).

Templates in the manifest (copy the closest real entry, don't invent one):
- Pure stat variant: `ancient_warrior` (id + prefabName only; all numbers in the CSV).
- Ranged, reused attack with its own SO + fire point: `forest_guardian` (`LightningBallAttack` + `ForestGuardianAttackSO`).
- New attack component + SO: `bomber`. Bone-attached prop + different base: `bannerman`. Captain: `captain_kombusta`.

Editor-time structure in `wire` (adding a collider, instantiating an existing Synty prop under a child) is fine: it authors the prefab. Guard it find-or-add so reruns converge. Runtime code must never build visuals. For feedback, give the component serialized `MMF_Player` fields and author the players on the prefab (`/feel-integration`); don't wire new `*VfxPrefab` fields for `Instantiate` (the `explosionVfxPrefab` wiring on bomber/powder_keg is legacy).

## Step 2: run the generator

- **Editor open**: menu **Bladehold > Generate Enemy Prefabs**, or via `/unity-editor-mcp` (`execute_menu_item`), then read the console. It logs `N variant(s) created, M updated` plus roster cross-check warnings: manifest ids with no CSV row (currently `dwarf`) and CSV rows with no map entry.
- **Headless** (Editor must be **closed**):
  `& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\lance\source\repos\My project" -executeMethod EnemyPrefabGenerator.GenerateAll -logFile -`
  (version from `ProjectSettings/ProjectVersion.txt`; hard failures throw → non-zero exit).

It regenerates **every** manifest entry each run. Idempotent: variants update in place (variant link preserved), structure/wiring re-applied, SO assets never overwritten, map entry refreshed.

## Rules

- **Hand-built variants have no manifest entry and must not get one**: Goblin Brute, Storm Witch, Troll, Knight, Bulwark (all mapped in `EnemyPrefabMap.asset` by hand). The generator must not clobber hand wiring.
- A generated prefab only spawns once its CSV row exists and is `TRUE`; sector waves also need `minThreat > 0`.
- New attack components still follow `/add-enemy-type`: `SetDamage` in `EnemyDefinitionApplier`, `Damage.source`/`sourcePosition` stamping, death/player-death handling, and a benchmark assertion. The generator wires prefabs; it doesn't validate behaviour.

## Verify (after a run)

1. The new `.prefab` YAML has a `PrefabInstance` whose `m_SourcePrefab` guid is the goblin base `64b407995d56642478ea2b02984a62f8` (or your `basePrefabPath`'s guid): proof it's a true variant.
2. `Enemies/EnemyPrefabMap.asset` has the `- id: <id>` entry.
3. Re-run the generator: `git status` shows no new changes (idempotency).
4. Play-mode check: Enemy Manager → Zoo tab / `EnemyZoo`, or the DevConsole spawn-type picker in a battle scene. Do it via MCP when connected; otherwise it goes in the checklist.

## Finish protocol

`/compile-check` (manifest edits compile in `Assembly-CSharp-Editor.csproj`). Run the generator and Play-mode check through `/unity-editor-mcp` if the Editor is open. Anything that stays manual (animator states for new triggers, MMF feedback authoring, VFX/materials/model swap, balance) goes in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo`. Agents never write to `TODO.md`. Commit to `main` and push.
