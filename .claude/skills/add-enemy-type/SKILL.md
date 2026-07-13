---
name: add-enemy-type
description: Use when adding a new enemy type to Bladehold — attack/behaviour components, an Enemies.csv roster row, WaveSpawner routing, and the enemy prefab component checklist.
---

# Add an enemy type

Enemies are roster-driven: a row in `Assets/Bladehold/Config/Enemies.csv` + a prefab mapped by id in `WaveSpawner.enemyPrefabs`. Most of a new enemy is **reused stock components** — usually only the attack/behaviour script is new.

## Ground truth first

1. Read the header row of `Config/Enemies.csv` and the doc comments in `Assets/Bladehold/Bladehold Scripts/Enemies/EnemyRosterSO.cs` — column semantics live there, not in CLAUDE.md. Current columns:
   `id,displayName,health,damage,minGold,maxGold,speed,scale,unlockWave,spawnChance,minSpawn,maxConcurrent,impulseResistance`
2. Read your chosen attack exemplar (table below) end to end.
3. Read `WaveSpawner.ApplyDefinition` in `Waves/WaveSpawner.cs` — it's the routing point every stat override flows through.

## Step 1 — CSV row semantics

- **First row (goblin) is the unlimited fallback** — never reorder it. New types go on later rows.
- Stat columns (`health,damage,minGold,maxGold,speed,scale`) are **optional overrides**: blank keeps the prefab's own SO values; shared SO assets are never mutated.
- `unlockWave` = first wave it can appear. `spawnChance` is a **percent** (20 = 20%). `minSpawn` = per-wave guaranteed budget at unlock, growing +1 per wave after, capped by `maxConcurrent`; blank/0 = chance-only. `maxConcurrent` = max alive at once (blank/0 = unlimited). `impulseResistance` vs. the sword's Impulse fling: power ≥ r = full ragdoll fling, ≥ r−1 = knockdown animation, below = nothing (goblin 0/blank, brute 3, troll 50).

## Step 2 — Behaviour component (pick the closest exemplar under `Enemies/`)

| Wanted behaviour | Copy this exemplar |
|---|---|
| Plain melee chase + swing | `AIAttack.cs` + `AIAttackSO.cs` (wind-up delay, never touches the NavMeshAgent) |
| Telegraphed AoE slam | `TrollSlamAttack.cs` (+SO) — stamps `Damage.unparryable` + impulse; wide AoEs must be unparryable |
| Straight projectile | `LightningBallAttack.cs` / `LightningBall.cs` |
| Persistent zone / storm | `LightningStormAttack.cs` + `LightningStormZone.cs` |
| Self-destruct / fuse rush | `BomberAttack.cs` (+SO) — plant via `AIMovement.SetMovementPaused`, sprint via `SetSpeedMultiplier` (composes with `SlowStatus`), explodes, then **force-kills itself through `Health.ReceiveDamage`** so wave/coin/corpse accounting runs |
| Charge with telegraph lane | `MountedKnightBrain.cs` (+ `MountedKnightRider.cs` for the two-body mount pattern) |
| Applied status (slow etc.) | `SlowStatus.cs` — added at runtime, zero prefab wiring (the `EnemyRagdoll` lazy-build idiom) |
| Head/weak-point bonus | `VulnerableSpot.cs` child collider |

Rules for the new component:
- Config on a `*SO` (`[CreateAssetMenu(menuName = "Scriptable Objects/...")]`); `OnValidate` auto-wire + `Start` null-check/`anyError`; unsubscribe in `OnDestroy`.
- Attacks on the player must stamp `Damage.sourcePosition` and `Damage.source` (your own `Health`) — `Parry`/`Counterstrike` depend on them. Single readable swings stay parryable; wide AoEs/explosions set `unparryable = true` and usually `DamageType.elemental`.
- Stop on the player's death (subscribe `Player.Instance.Health.OnDied` — goblins Cheer) and on your own `Health.OnDied` (corpses don't tick).
- **Expose `SetDamage(float)`** (and any other CSV-overridable number) as a per-instance setter, then add your component to the `?.SetDamage(...)` chain in `WaveSpawner.ApplyDefinition` — overrides are applied right after `Instantiate`, **before the instance's `Start`** (the `MarkGolden` timing trick), so `Start` must respect an already-set override.

## Step 3 — Prefab component checklist (goes in the TODO entry; base it on a goblin variant)

`Health` (+ per-type `HealthSO`), `Enemy` (kill credit), `AIMovement` (+ `AIMovementSO` — horde-scaling knobs live there), `AIAnimation`, your attack component (+SO), `CoinDropper`, `CorpseDespawner`, `DisableCollidersOnDeath`, `KnockbackReceiver`, `EnemyRagdoll` + `ImpulseReceiver`, `DamageNumberSpawner`, a `VulnerableSpot` head child (arrow headshots), optional `GoldenGoblin`/`ImpulseGoblin`/`PowerupDropper`/`AITargetSelector`, `MMHealthBar` + `HealthBarUI`. Animator triggers by convention: `Attack`, `Death`, `Cheer` (+ custom, e.g. Bomber's `LightFuse`).

Finally: register the prefab under the CSV id in **`WaveSpawner.enemyPrefabs`** (Editor wiring — rows without a mapping are skipped with a warning) and in the **`EnemyZoo`** scene's mirror map.

## Pitfalls

- **Death is signalled, not destruction**: react via `Health.OnDied`/`IsDead`, never `OnDestroy`/object counts. Enemies become corpses; `CorpseDespawner`/`CorpseManager` clean up later. Any scripted kill goes **through `Health.ReceiveDamage`** so coins/kill-credit/wave-count stay consistent.
- Don't hand-tune the prefab's `NavMeshAgent` avoidance — `AIMovement` applies the SO's settings in code.
- A non-humanoid or non-standard rig can't use `EnemyRagdoll` (it walks Humanoid bones) — give it high `impulseResistance` instead and note it.
- Testing (Editor): DevConsole `DebugSetNextWave` to jump to the unlock wave, `DebugSpawnBurst` for perf, `DebugWipeWave` to clear; `Debug/EnemyZoo.cs`'s gallery scene spawns every roster row with CSV overrides applied — cite these in the manual-verification checklist.

## Finish protocol

1. `/compile-check` (new files added to `Assembly-CSharp.csproj` first).
2. `/editor-wiring-todo` — SO asset, prefab checklist, animator additions, `WaveSpawner.enemyPrefabs` registration, balance pass; manual verification modeled on the Bomber entry (spawn wave, behaviour beats, death accounting, negative cases).
3. Commit directly to `main` and push.
