---
name: add-enemy-type
description: Use when adding a new enemy type to Bladehold — attack/behaviour component, an Enemies.csv roster row (threat/wave gating), EnemyDefinitionApplier override routing, the generated prefab, and an automated benchmark assertion.
---

# Add an enemy type

Enemies are roster-driven: a row in `Assets/Bladehold/Config/Enemies.csv` + a prefab mapped by id in `Enemies/EnemyPrefabMap.asset` (`EnemyPrefabMapSO`). `SurvivorsSpawner` loads every **enabled** row that has a map entry, and `EnemyDefinitionApplier.Apply` stamps the row's overrides on each spawn. Most of a new enemy is stock components; usually only the attack/behaviour script is new, and the prefab is generated (Step 4).

## Ground truth first

1. Header row of `Config/Enemies.csv` and the doc comments in `Enemies/EnemyRosterSO.cs` (`EnemyDefinition` + `ParseRow`). Current columns:
   `id,displayName,health,damage,minGold,maxGold,speed,scale,unlockWave,spawnChance,minSpawn,maxConcurrent,knockbackResistance,enabled,minThreat`
2. `Waves/SectorSpawnRules.cs` (pure selection rules) and `SurvivorsSpawner.SelectSpawnTypeForWave` (how they're applied).
3. `Enemies/EnemyDefinitionApplier.cs`: the one routing point every CSV override flows through (spawner, `MinionSpawner`, DevConsole, `EnemyZoo`).
4. Your chosen exemplar (Step 2) end to end.

## Step 1: CSV row

- **Row 1 (goblin) is the roster fallback.** Never reorder it; add new types lower down. Sector fodder is separately `fodderEnemyId` on `Bladehold Config/SurvivorsRoundPacingConfig.asset`.
- Stat columns (`health,damage,minGold,maxGold,speed,scale,knockbackResistance`) are **optional overrides**: blank keeps the prefab's own SO value; shared SOs are never mutated. Filling only one gold column means a fixed drop. `scale` multiplies the transform and NavMeshAgent radius/height.
- `knockbackResistance` overrides the prefab `KnockbackReceiver` (goblin 2, bomber 2, bulwark 4, captain 6).
- `enabled` must be `TRUE`/`FALSE` (`bool.TryParse`; `0`/`1` log a parse error and read as **true**). `FALSE` keeps the row parsed but out of the spawner, DevConsole picker and sector waves.
- `minThreat` = first campaign tier (node `tierIndex`, via `SectorThreat.Current`) the type joins sector waves at. **0/blank = never in sector waves** (captains, objective-only or unfinished enemies). `unlockWave` = first wave (1-5) within the sector.
- `spawnChance` is a **percent** and a *weight* in the roll over all unlocked types. `minSpawn`/`maxConcurrent`: see the `EnemyDefinition` doc comments. `maxConcurrent` is enforced per type at selection time; bubblers/"shield" ids also get the spawner's `maxConcurrentShielders` (2) cap.
- How a wave picks (`SectorSpawnRules` + spawner): a row is eligible when `enabled && minThreat > 0 && threat >= minThreat && wave >= unlockWave` and it's under its concurrent cap. **Fodder floor:** if goblins would drop below `fodderShare` (60%) of this wave's spawns, the next spawn is forced fodder; otherwise a weighted `spawnChance` roll over the eligible types (goblin included). Pick `minThreat` against the current curve (T1 goblin/brute/bubbler/big ork/bomber, T2 bannerman/powder keg, T3 bulwark/assassin, T4 storm witch, T5 troll).
- **Objective override:** an objective implementing `IOverrideEnemySpawns` (only `GoblinRushObjective`, `allowedEnemyIds = { "goblin" }`) replaces threat gating with its own id list for that wave. There is no per-sector id list; per-sector variety comes from `minThreat`.
- **Demo:** `DemoConfigSO.campaignCutoffTier` is 4, so a `minThreat` of 5+ never appears in the demo.
- Optional localized name: `Resources/Localization/Strings.csv` key `enemy.<id>.name` (falls back to `displayName`).

## Step 2: Behaviour component (closest exemplar under `Enemies/`)

| Wanted behaviour | Copy this exemplar |
|---|---|
| Plain melee chase + swing | `AIAttack.cs` + `AIAttackSO.cs` (wind-up delay, never touches the NavMeshAgent) |
| Telegraphed AoE slam | `TrollSlamAttack.cs` (+SO): stamps `Damage.unparryable` + impulse; wide AoEs must be unparryable |
| Straight projectile | `LightningBallAttack.cs` / `LightningBall.cs` |
| Persistent zone / storm | `LightningStormAttack.cs` + `LightningStormZone.cs` |
| Self-destruct / fuse rush | `BomberAttack.cs` (+SO): plant via `AIMovement.SetMovementPaused`, sprint via `SetSpeedMultiplier`, then **force-kill itself through `Health.ReceiveDamage`** so kill/coin/corpse accounting runs |
| Shield / directional block | `Bulwark/` (`BulwarkAttack` + `BulwarkShield`, hooks `Health.TryBlockDamage`) |
| Ally buff aura | `Bannerman/BannermanAura`, `AllyAura.cs` |
| Charge with telegraph lane | `MountedKnightBrain.cs` (+ `MountedKnightRider.cs` for a two-body mount) |
| Applied status (slow etc.) | `SlowStatus.cs`, added at runtime with zero prefab wiring |
| Head/weak-point bonus | `VulnerableSpot.cs` child collider |

Rules for the new component:
- Tunables on a `*SO` (`[CreateAssetMenu(menuName = "Scriptable Objects/...")]`). Auto-wire in `OnValidate`/`Awake`, null-check in `Start` with an `anyError` flag + `Debug.LogError`, early-return from `Update`. Unsubscribe in `OnDestroy`.
- Attacks on the player stamp `Damage.sourcePosition` and `Damage.source` (your own `Health`); `Parry`/`Counterstrike` rely on them. Single readable swings stay parryable; wide AoEs/explosions set `unparryable = true` and usually `DamageType.elemental`.
- Stop on the player's death (`Player.Instance.Health.OnDied`) and on your own `Health.OnDied`. Death is signalled, never `OnDestroy`/object counts.
- **Feedback through MMF only** (`/feel-integration`): a serialized `MMF_Player` per beat (telegraph, impact, death) and `PlayFeedbacks(pos)`. No `PlayOneShot`, no `Instantiate(vfxPrefab)`, no direct camera shake. `BomberAttack`/`PowderKegAttack` still `Instantiate(explosionVfxPrefab)`: don't copy that part.
- Telegraphs, projectiles and zones are **authored prefabs** on serialized/SO fields; never build meshes or renderers in code.
- **Expose `SetDamage(float)`** (and any other CSV-overridable number) as a per-instance override, and add your component to the `?.SetDamage(...)` chain in `EnemyDefinitionApplier.ApplyDefinitionInternal`. Overrides land right after `Instantiate`, **before the instance's `Start`**, so `Start` must respect an already-set override (see `CaptainKombustaController.damageOverride`).

## Step 3: Automated behavioural test

Follow `/test-mechanic` (it absorbed the old `/maintain-mechanic-tests`) so any unique mechanic has an automated assertion in `Editor/WeaponReachBenchmark.cs`. The Bulwark block is the model: SO math, the `Health` hook behaviour, the prefab has its components, and the `EnemyPrefabMap` registration. The suite is menu **Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark** (`N PASSED | M FAILED`). Lance runs it himself unless he asks, so name it in your summary. If he asks, run it from `MainMenu` and never save the scene it ran in.

## Step 4: Build the prefab with the generator (`/generate-enemy-prefabs`)

Author an `EnemySpec` in `Editor/EnemyManifest.cs` and run **Bladehold > Generate Enemy Prefabs**: it creates the variant (of `Goblin Enemy (Base)` or `basePrefabPath`), per-enemy SO assets, wiring, and the `EnemyPrefabMap.asset` entry. Hand-built variants with no manifest entry: Goblin Brute, Storm Witch, Troll, Knight, Bulwark.

The goblin base carries the stock set every enemy inherits: `Health`, `Enemy` (kill credit + `GameStats`), `AIMovement` + `AIAnimation`, `AIAttack`, `CoinDropper`, `CorpseDespawner`, `DisableCollidersOnDeath`, `KnockbackReceiver`, `EnemyRagdoll` + `ImpulseReceiver`, `DamageNumberSpawner`, `AITargetSelector`, `GoldenGoblin`/`ImpulseGoblin`/`PowerupDropper`. The manifest describes only the deltas. Animator triggers by convention: `Attack`, `Death`, `Cheer` (+ custom); new animator states stay manual.

## Pitfalls

- Scripted kills go **through `Health.ReceiveDamage`** so coins, kill credit and wave counts stay consistent. Only `SurvivorsSpawner` spawns call `GameLoopManager.OnEnemyKilled` (quota + per-kill gold/supply); DevConsole/objective spawns don't count toward the quota.
- Don't hand-tune NavMeshAgent avoidance; `AIMovement` applies it from its SO.
- A non-humanoid rig can't use `EnemyRagdoll` (walks Humanoid bones): give it high `knockbackResistance` instead.
- Testing: DevConsole (backquote) threat ▲/▼ (`SectorThreat.DebugOverride`, applied at the next `StartWave`), the spawn-type picker, +50/+100/+300 bursts, "Wipe Wave"; **Bladehold > Enemy Manager** (Stats tab edits the CSV row, Zoo tab drives `Debug/EnemyZoo.cs`, which spawns every roster row with overrides applied).

## Finish protocol

1. `/compile-check` (`Assembly-CSharp.csproj` for runtime code, `Assembly-CSharp-Editor.csproj` for manifest/benchmark edits; register new files first).
2. Editor work: with the Editor open, run the generator and a Play-mode EnemyZoo check via `/unity-editor-mcp`. Whatever's left (animator states/transitions, MMF feedback authoring, VFX/materials, balance pass, manual verification: spawn tier/wave, behaviour beats, death accounting, negative cases) goes in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo`; `/editor-wire` can execute the MCP-doable items later. Agents never write to `TODO.md`.
3. Commit directly to `main` and push.
