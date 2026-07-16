---
name: add-player-class
description: Use when adding a new playable class to Bladehold (like Swordsman/Berserker/Mage) — ClassDefinitionSO, PlayerClassController slot, class weapons, signature mechanics, class skill tree, and class-select UI.
---

# Add a playable class

Classes are data-driven and **reload-based** (never hot-swapped): `SaveData.playerClassId` picks a slot in `Player/PlayerClassController.cs`, which activates that class's weapons/components in `Awake` — strictly before every `Start`. Shipped classes to copy from: **Swordsman** (baseline), **Berserker** (axe/rage), **Mage** (staff/wand/imbuement — the newest, most complete exemplar).

## Ground truth first

1. Read `Assets/Bladehold/Bladehold Scripts/Player/PlayerClassController.cs` and `Player/ClassDefinitionSO.cs` end to end — the doc comments explain *why* everything happens in `Awake` (stat-base clobbering, listener re-pointing). Field names below may have drifted; the code wins.
2. Read the Berserker and Mage sections of `TODO.md` — they are the staged playbook this skill codifies, including all the Editor wiring a class needs.
3. Grep for the newest class's components (`PlayerWand`, `MageImbuement`, `RageBuff`, …) before designing — a mechanic you want may already exist.

## Build in stages (the Berserker A–E rollout — each stage compiles, is TODO-documented, and is independently verifiable)

### Stage A — class infrastructure + melee weapon
1. **`ClassDefinitionSO` asset spec** (asset created in-Editor; you author the C# expectations + TODO entry): `id` (stable, saved — never rename once shipped), `displayName`, `description`, optional `animatorOverride` (AnimatorOverrideController), optional `characterModelPrefab` (Synty Sidekick sharing the rig skeleton — bone names must match 1:1), `chargeTimePerLevel`, optional per-class `skillTree`.
2. **Melee weapon**: duplicate the sword prefab pattern — a `DamageTrigger` in **BladeSweep** mode with `readsPlayerStats` ON, its own `DamageSO` (scale baseDamage vs. sword, e.g. axe ≈ 1.4×) and `DamageTriggerSO`, plus `SwordHitFeedback`. Nested under the hand bone, **inactive by default**. Duplicated prefabs lose out-of-prefab refs — the TODO entry must list re-assigning `DamageTrigger.playerAttack`, `SwordHitFeedback.animator`, `SwordChargeFeedback.playerAttack`.
3. **The slot** (`PlayerClassController.ClassSlot`, wired in the Editor — document in TODO): `definition`, `weaponObjects` (activated for this class only), `meleeTrigger` + `hitFeedback` (re-pointed onto `AnimationEvents`/`VampiricBlade`/`ChainLightning`/`ImpulseHitFeedback`/`PlayerMount`), `classComponents` (class-only `Behaviour`s, disabled for other classes).
4. **Animator override**: the attack clip **must carry the `OneHandedSwordAttack` (impact frame) and `PlaySwordWoosh` (early swing) animation events**, baked on the clip's import settings with those exact names — `AnimationEvents.cs` routes them to whichever weapon is active. A missing event = silent no-damage swings. This is Editor work; make it a bold TODO item.

### Stage B — class select UI
`UI/ClassSelectPanel.cs` takes authored buttons, one per `ClassDefinitionSO`; `DeathScreen` shows it with the Reincarnate tree and persists the pick via `PlayerClassController.SetSavedClass(id)` right before the scene reload. Usually only Editor wiring: a new button + the new SO asset assigned.

### Stage C — ranged / hold-aim weapon (if the class has one)
Implement `Player/IChargedAimWeapon.cs`; `Player/AimWeaponResolver.cs` and the shared aim UI/camera discover the active class's weapon as the first `IChargedAimWeapon` in its `classComponents`. Three exemplars, pick the closest:
- **Hitscan** → `Player/PlayerBow.cs` (+ `BowSO`)
- **Physical projectile** (pierce/return) → `Player/PlayerThrownAxe.cs` + `AxeProjectile.cs` (+ `ThrownAxeSO`)
- **Caster / homing projectile** → `Player/PlayerWand.cs` + `MagicMissileProjectile.cs` (+ `WandSO`)

### Stage D — signature mechanics
Copy the closest precedent: `Player/RageBuff.cs` (timed buff + `Health.ScaleDamageTaken`), `Player/PainIntoPower.cs` (damage-taken → power), `Player/MageImbuement.cs` (mode/element switching; see also `ElementType.cs`, `FlameZone.cs`, `Economy/ElementNode.cs`, `Waves/Runestone.cs`/`ElementNodeSpawner.cs` for world-interaction mechanics). Register every tunable number as a `StatType` base (see `/add-skill-line` step 2) so class skill nodes can modify it later.

### Stage E — class skill tree
A new CSV under `Assets/Bladehold/Config/` + a `SkillTreeSO` asset assigned on the class's `ClassDefinitionSO.skillTree` (null = the Swordsman default). `SkillTreeService.Start` adopts the active class's tree; saved node ids missing from it are skipped, so other classes' purchases go dormant safely. Author rows per `/add-skill-line`.

## Pitfalls (each has bitten before)

- **Stat-base clobbering**: only the active class's weapon/components may run `Start` — an inactive GameObject / disabled Behaviour never registers its `SwordDamage`/range/crit bases. Never activate two `readsPlayerStats` triggers; never put a class component outside `classComponents` "just to be safe".
- Class components use **base 0 = locked** for anything a skill node unlocks — a disabled component's unregistered base is what keeps it off for other classes.
- `PlayerClassController` shared refs (`animationEvents`, `playerAttack`, optional `vampiricBlade`/`chainLightning`/`impulseHitFeedback`/`playerMount`, child `animator`) auto-wire via `OnValidate` — new shared listeners that hold a melee-trigger ref need a setter the controller can call.
- Testing without the Editor is limited; in-Editor: DevConsole (backquote) has a class ◄/► picker + "Switch & Reload" cheat, and telemetry `run_start` rows log `class=` — cite both in the manual-verification checklist.
- Never modify vendored Synty/StarterAssets code for a class; animator changes go through `AnimatorOverrideController`, controller internals through cached reflection (`PlayerMoveSpeedBinder` precedent).

## Finish protocol

1. `/compile-check` after each stage (add new files to `Assembly-CSharp.csproj` first).
2. `/editor-wiring-todo` — one entry per stage, matching the Berserker Stage A–E entries in `TODO.md` (SO assets, prefab slot wiring, animator override + baked events, class-select button, icons, balance). With the Editor open, `/editor-wire` executes the MCP-doable items (baked animation events stay human).
3. Commit each stage directly to `main` and push.
