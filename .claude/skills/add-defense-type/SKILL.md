---
name: add-defense-type
description: Use when adding a new tower/defence to Bladehold's battlefield build wheel — the DefenseStructure subclass, FortDefenseType value, TowerPlot prefab slot, build-wheel entry and supply costs (build/refill/upgrade), level stats, assembly animation, MMF feedback, dismantle refund and demo gating.
---

# Add a defence type

Read `Assets/Bladehold/Bladehold Scripts/Fort/CLAUDE.md` first. A defence is: an enum value + a `DefenseStructure` subclass + an authored prefab + a slot on `TowerPlot` + an option on the HUD's `BuildWheelUI`. Supply always goes through `RunSession.TrySpendInRunSupply` / `AddInRunSupply`.

**Template: `Fort/ArrowTowerDefense.cs`** + `Bladehold Prefabs/Defenses/Defense_ArrowTower.prefab`. It's the cleanest full example: targeting, rotation, lead-aim projectile, per-shot supply, a fire `MMF_Player`, a Start-validated feedback override and draft-stat hooks. Other shapes to copy from:

| Wanted | Exemplar |
|---|---|
| Lobbed AoE projectile | `CatapultDefense` + `CatapultProjectile` |
| Piercing line shot | `BallistaDefense` + `BallistaBoltProjectile` |
| Crowd control (root) | `NetThrowerDefense` + `NetProjectile` |
| Ground trap, no turret | `SpikeTrapDefense` |
| Persistent zone | `OilVatDefense` + `BurningOilZone` |

## 1. Enum (`Fort/FortDefenseType.cs`)

**Append** a value to `FortDefenseType`. Never reorder or remove: `BuildWheelUI.defenseOptions` on the HUD prefab and every tower serialize the int. (`FortSocketType` is dead, left over from the deleted socket fortress.)

## 2. The subclass (`Fort/<Name>Defense.cs`)

What the base (`DefenseStructure`) already does, so don't redo it:
- `[E]` interaction: refill from the player's pool while below max supply, otherwise upgrade (up to `maxLevel` 3). Refill and upgrade work any time, not just during prep.
- The prompt text, `AllActive` registration (which drives the HUD "NO SUPPLY" marker in `UI/ObjectiveWaypointTrackerUI`), `InteractableRegistry`, and `DismantleRefund` (remaining supply + paid upgrade spend).
- The repair/upgrade/break feedbacks and the supply popup.

What you write:
- `Awake` override: set `defenseType = FortDefenseType.<New>` (and `supplyPerAction` if it isn't 2), then `base.Awake()`. **Keep `Awake`/`OnEnable` side-effect-free.** `DefenseAssemblyAnimation` instantiates the prefab twice (a hidden sample for piece data, then the real tower, which starts inactive), so both fire on objects that never fight.
- `ApplyLevelStats(int level)` (abstract): set this level's numbers. It's called from `InitState` when the tower is built and from `Upgrade`/`SetLevel`.
- `Update`: `if (IsDepleted) return;`, find a target, `RotateTowardsTarget(...)` when `rotateToTarget`, and on fire call `if (!ConsumeSupply()) return;`. A depleted tower stays standing and just stops. Refilling restarts it.
- Target filter (copy `ArrowTowerDefense.FindClosestEnemy`): the `Enemy` layer (fallback `1 << 7`), skip `IsDead`, `ImmuneToPlayerDamage`, the player's root and anything under a `Gate`.
- Damage: `isPlayerDamage = true`, `source = Player.Instance.Damageable`, `sourcePosition`, multiplied by `StatType.AllDamageMultiplier`. Elemental hits set `DamageType.elemental` + `elementId` and apply `EnemyStatusManager.GetOrAdd(target)?.ApplyStatus(...)`.
- Feedback: add one serialized `MMF_Player` per beat (fire, impact…), call `PlayFeedbacks(pos)`, and override `ValidateFeedbackReferences()` (call `base` first) to `Debug.LogError` a missing one. The base calls it from `Start`. **No** `AudioSource.PlayOneShot`, `Instantiate(vfx)` or camera-shake calls (see `/feel-integration`). Gameplay projectiles are authored prefabs on a serialized field. If the field is missing, log an error and don't fire. Don't copy the arrow tower's "fallback direct damage" branch for a null `arrowPrefab`.
- Optional overrides: `GetUpgradeCost()` (base `40 × level`), `CalculateMaxSupplyForLevel()` (base 50/75/100), `OnSupplyDepleted()` (call `base`).

## 3. Tunables

House rule: tunables live on a `ScriptableObject`. The six existing towers predate that: their per-level numbers are hard-coded in each `ApplyLevelStats` switch, and costs are code constants. For a new tower, add a `<Name>DefenseSO` (`[CreateAssetMenu(menuName = "Scriptable Objects/Defenses/...")]`) with per-level rows (damage, interval, range, radius…) and optional supply/upgrade overrides. Put the asset in `Bladehold Config/`, reference it from the prefab, null-check it in `Start` (set `anyError`, early-out of `Update`) and read it in `ApplyLevelStats`.

Draft-card hooks (e.g. `TowerArrowDamageBonus`): read via `Player.Instance.Stats.GetValue(StatType.X)`. Append new `StatType` values at the **end** of the enum, never mid-list. The cards themselves are rows in `Resources/DraftUpgrades.csv` (Elemental category, `SLOT_FORTRESS`). Tesla Spire and Permafrost in `TowerPlotManager` already apply to any built tower, with no work needed.

## 4. The prefab (`Bladehold Prefabs/Defenses/Defense_<Name>.prefab`)

Build it by hand or via MCP, modelled on `Defense_ArrowTower.prefab`:
- Root: the subclass, a collider, the model children, a `FirePoint`.
- A `Feedbacks` child holding `RepairMMF`, `UpgradeMMF`, `BreakMMF` and your beat players (e.g. `FireMMF`), wired to the serialized fields.
- `supplyPopupPrefab` (the same DamageNumbersPro asset the arrow tower uses) and your SO.
- Synty models: `Assets/Synty/PolygonFantasyKingdom/Prefabs/SiegeEngines/`.

Don't extend `Editor/DefensesRevampSetup.cs`: it code-assembles prefabs and rewires the Survivors scene. `TowerPlot.SetPrefabs(...)` exists only for that script.

**The assembly animation is automatic** (`Fort/DefenseAssemblyAnimation.cs`, spawned from `Defenses/DefenseAssemblyAnimation.prefab` via `TowerPlot.assemblyAnimationPrefab`). It picks a mode by counting the prefab's `MeshRenderer`s:
- **≤ 2 renderers:** the tower rises from the ground. All of its `MonoBehaviour`s and colliders are disabled while it rises, then **all** are re-enabled, including any you authored as disabled. Don't rely on disabled components on the prefab.
- **More than 2:** each mesh drops from the sky, lowest first, then the real tower appears.

Its MMF players (piece land, rise dust, slam) live on the shared prefab, so nothing per-tower is needed. Add a build-specific beat to your own prefab only if the tower really needs one.

## 5. Plot slot (`Fort/TowerPlot.cs`)

Add a `[SerializeField] private GameObject <name>Prefab;` and a case in `GetPrefabForType`. `BuildDefense` then handles instant or animated building, `OwnerPlot`/`PlotIndex` and `InitState`. Assign the field on `Bladehold Prefabs/Defenses/TowerPlot.prefab`. Scene plots are instances of it, but check that none override the field. The castle scenes are binary, so check them via MCP. A missing slot logs "No prefab assigned for defense type" and the build fails after the supply is already spent.

## 6. Build-wheel entry (`UI/BuildWheelUI.cs` on `Bladehold Prefabs/UI/Bladehold HUD.prefab`)

- Add a `DefenseOption` (`defenseType`, `displayName`, `supplyCost` = **build cost**, `description`, `icon`) to `defenseOptions` **on the HUD prefab**. The initializer list in code only seeds new components. Also add it to the code initializer so the two stay in step.
- Option *i* is shown by `wheelButtons[i]`, and the wheel shows `min(options, buttons)`. There are 6 authored `BuildWheelButton`s today, so a 7th option needs a 7th button placed on the radial layout. `sliceButtonPrefab` (`UI/BuildWheelSliceButton.prefab`) is serialized but never instantiated. The house rule is "one prefab per repeated element, populated from data", so either add an instance of that prefab to the wheel and to `wheelButtons`, or (better, and worth asking Lance) make `BuildWheelUI` spawn one slice per option from `sliceButtonPrefab`. Either way it's a UI change: flag it for human review (Synty art, Texturina header, Grenze text, gamepad focus).
- Spending is already handled: `OnSelectSlice` → `RunSession.TrySpendInRunSupply(supplyCost)` → `buildFeedback` → `TowerPlot.BuildDefense`. Builds only happen in prep (`TowerPlot.CanInteract` checks `GameLoopManager.IsPrepPhase`).
- Icons: `/generate-sprite-variants` or a Synty icon.

## 7. Sector end

Nothing to add. `GameLoopManager.TriggerVictory` calls `TowerPlotManager.DismantleAllForRefund()`, which pays each tower's `DismantleRefund` into `RunSession.InRunSupply` (shown on the victory screen). On player death the manager calls `ClearAllDefenses()`. Towers never carry between sectors, so keep all tower state on the instance and never in `RunSession`.

## 8. Demo gating

`DemoConfigSO` has no defence gate yet. If the new tower is out of the demo, add `allowedDefenses` (a `List<FortDefenseType>`) plus a static `IsDefenseLocked(FortDefenseType)` helper to `Demo/DemoConfigSO.cs`, following `IsWeaponLocked`, and have `BuildWheelUI` show locked options using `DemoConfigSO.LockedLabel`. Never add a per-asset flag.

## Verify

- Open `Bladehold Survivors Scene` (text YAML, has plots) and enter Play mode. A run starts with 60 supply and each wave clear adds 30. The DevConsole has a gold cheat but no supply cheat, so use MCP `execute_code` `RunSession.AddInRunSupply(500)` if you need more. Build in prep, then check: the assembly plays; it fires and drains supply; it stops at 0 and the NO SUPPLY marker appears; `[E]` refills it; `[E]` at full upgrades it (Lv2/Lv3 stats change); winning adds `remaining + upgrade spend` supply.
- Negative cases: can't build mid-wave; can't afford = the button is dimmed; a depleted tower never fires.
- Add a benchmark assertion (`/test-mechanic`; the existing tower cases are 10B (plot build) and 20A-20D (defence visuals/assembly) in `Editor/WeaponReachBenchmark.cs`): `BuildDefense(<New>, instant: true)` → correct `DefenseType`, supply drains, depletion doesn't destroy the tower.

## Editor work: MCP vs. checklist

- **Agent via `/unity-editor-mcp`**: create the SO asset, build and wire the prefab (MMF children, fields), assign the `TowerPlot.prefab` slot, add the HUD `defenseOptions` entry and button, check scene overrides, Play-mode check.
- **`/editor-wiring-todo`** (`plans/editor/NN-<topic>.md`): MMF content (sounds, particles, shake strength), model/art choice, wheel layout and icon review, balance feel. Agents never write to `TODO.md`.

## Finish

1. `/compile-check` (new `.cs` files registered in `Assembly-CSharp.csproj` first).
2. `/editor-wiring-todo` for the leftovers.
3. Commit directly to `main` and push.
