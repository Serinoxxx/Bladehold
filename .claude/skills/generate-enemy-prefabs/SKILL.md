---
name: generate-enemy-prefabs
description: Use when building or updating an enemy prefab variant in Bladehold — author an EnemyManifest entry and run the EnemyPrefabGenerator instead of hand-wiring prefabs in the Editor.
---

# Generate enemy prefabs

Enemy prefabs are **generated**, not hand-built: a declarative entry in
`Assets/Bladehold/Bladehold Scripts/Editor/EnemyManifest.cs` + one run of
**Bladehold > Generate Enemy Prefabs** (`Editor/EnemyPrefabGenerator.cs`) produces a prefab
*variant* of `Goblin Enemy (Base).prefab`, creates missing per-enemy SO assets, wires component
references, and registers the id in the shared `EnemyPrefabMap.asset` (`EnemyPrefabMapSO`) that
`WaveSpawner` and `EnemyZoo` both read — **no scene edits, ever**.

Division of labour: `Config/Enemies.csv` is the *balance sheet* (stats, spawn gating — see
`add-enemy-type`), the manifest is the *structure* (components, SO assets, children, wiring),
and art (materials/models/VFX) is a manual Editor pass tracked via `/editor-wiring-todo`.

## Step 1 — author the manifest entry

Add an `EnemySpec` to `EnemyManifest.Entries`. Fields (doc comments in `EnemyManifest.cs` are the
ground truth):

- `id` — the roster CSV id; `prefabName` — `"<Name> Enemy Variant"` (house naming).
- `rootScale` — authored on the variant root; the CSV `scale` column multiplies on top at spawn.
- `materialPath` — optional path to an **existing** material for the body renderer (the Storm
  Witch pattern). The generator never creates materials.
- `disableBaseAIAttack` — set whenever the enemy has its own attack component (disabled, never
  removed — matches the hand-built variants).
- `removeComponents` — e.g. `new[] { typeof(GoldenGoblin), typeof(ImpulseGoblin) }` for enemies
  that shouldn't roll the golden/impulse variants (the Storm Witch precedent).
- `children` — fire-point child objects (`"Lightning Ball Spawn"` style), created with the root's layer.
- `assets` — per-enemy SOs, created at `Enemies/<soFolder>/<assetName>.asset`. `initDefaults` runs
  **only on first creation**; an existing asset is never overwritten (designer tuning survives).
- `components` — components to ensure on the root, each with a `wire` lambda that runs **every**
  generator pass. Wire serialized fields with `EnemyPrefabGenerator.SetReference(so, "attackData",
  ctx.LoadedAsset("MyAttackSO"))` — it throws on a missing/renamed field instead of silently
  leaving a null.
- `navStoppingDistance` — ranged stand-off (Storm Witch = 6). Never touch agent *avoidance*
  (`AIMovement` applies it from its SO in code).

Worked example (ranged enemy with its own attack SO and fire point):

```csharp
new EnemySpec
{
    id = "forest_guardian",
    soFolder = "Forest Guardian",
    prefabName = "Forest Guardian Enemy Variant",
    disableBaseAIAttack = true,
    navStoppingDistance = 8f,
    children = new[] { new ChildSpec { name = "Projectile Spawn", localPosition = new Vector3(0f, 1.4f, 0.4f) } },
    assets = new[]
    {
        new SoSpec
        {
            soType = typeof(LightningBallAttackSO), assetName = "ForestGuardianAttackSO",
            initDefaults = so => { /* set defaults via reflection-free casts: ((LightningBallAttackSO)so)... */ },
        },
    },
    components = new[]
    {
        new ComponentSpec
        {
            type = typeof(LightningBallAttack),
            wire = (so, ctx) =>
            {
                EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("ForestGuardianAttackSO"));
                EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                // Prefab refs load with AssetDatabase.LoadAssetAtPath inside the lambda.
            },
        },
    },
},
```

## Step 2 — run the generator

- **In the Editor**: menu **Bladehold > Generate Enemy Prefabs**. Console prints a
  created/updated summary plus roster cross-check warnings.
- **Headless** (Editor must be **closed** — Unity single-instance):
  `& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\lance\source\repos\My project" -executeMethod EnemyPrefabGenerator.GenerateAll -logFile -`
  (version pinned in `ProjectSettings/ProjectVersion.txt`; hard failures throw → non-zero exit).

Idempotent: re-running updates variants in place (variant link to the goblin base preserved),
re-applies structure/wiring, never overwrites existing SO assets, and refreshes the map entry.

## Rules

- **Never add manifest entries for the hand-built variants** (Goblin Brute, Storm Witch, Troll) —
  the generator only owns manifest-authored enemies and must not clobber hand wiring.
- The generated prefab spawns in waves only once its `Enemies.csv` row exists **and** the map has
  its entry — the generator warns about both directions of mismatch.
- New attack components must still follow `add-enemy-type` (SetDamage override chain in
  `WaveSpawner.ApplyDefinition`, `Damage.source`/`sourcePosition` stamping, death/player-death
  handling) — the generator wires prefabs; it doesn't validate component behaviour.

## Verify (headlessly, after a run)

1. Read the new `.prefab` YAML: it must contain a `PrefabInstance` with `m_SourcePrefab` guid
   `64b407995d56642478ea2b02984a62f8` (the goblin base) — proof it's a true variant.
2. Read `Enemies/EnemyPrefabMap.asset`: the id → prefab entry exists.
3. Re-run the generator: `git status` shows no changes (idempotency).
4. Play-mode behaviour checks go in the TODO.md manual-verification list (`/editor-wiring-todo`):
   DevConsole `DebugSetNextWave` to the unlock wave, or the EnemyZoo gallery.

## Finish protocol

`/compile-check` (manifest edits compile in `Assembly-CSharp-Editor.csproj`), `/editor-wiring-todo`
for whatever stays manual (animator states for new triggers, MMF juice, VFX/materials, balance),
commit to `main` and push.
