---
name: add-ultimate-handler
description: Use when adding or changing a weapon ultimate in Bladehold — the IUltimateHandler component, its isUltimate draft card in DraftUpgrades.csv, the hardcoded id routing in DraftUpgradeService/PlayerUltimateController/DevConsole, and the Player.prefab wiring.
---

# Add a weapon ultimate

Ultimates belong to **weapons**, not classes (classes are gone). A run gets at most one: picking a weapon's `isUltimate` draft card sets `RunSession.ActiveUltimateId`, sets `StatType.UltimateUnlocked` to 1, and `DraftUpgradeService.ConfigureUltimateHandler` disables every `IUltimateHandler` on the player and enables the matching one. `PlayerUltimateController` (Player root) fills charge from damage dealt, and at 100% the `Ultimate` input calls `Activate` on the enabled handler.

Existing handlers in `Player/` (names are class-era leftovers): `SwordBladeTempestUltimate` (sword), `BerserkerUltimate` (axe), `RangerUltimate` (bow), `ThrowingAxeUltimate`, `MaceUltimate` (the cleanest model), plus `MageUltimate` and `SwordMountUltimate`, which no draft card reaches.

## 1. Draft card (`Assets/Bladehold/Resources/DraftUpgrades.csv`)

One row: `category` = `Weapon`, `weapon` = the weapon id (`sword`, `axe`, `mace`, `bow`, `throwing_axe`, ...), `isUltimate` = `1`, `maxLevel` = `1`, `stat` = `UltimateUnlocked`, `kind` = `Flat`, `amount` = `1`. Extra unlock stats go `;`-separated in `stat`/`kind`/`amount` (see `taxe_vortex_ult`). `icon` is a sprite name resolved through `Resources/SkillTreeIcons` (`SkillTreeIconsSO`).

The **id prefix is the routing key** (next step), so pick a unique one, e.g. `spear_<name>_ult`. The pool only offers cards for equipped weapons and hides every ultimate once one is owned; demo-locked weapons (`DemoConfigSO`) are filtered too.

## 2. Routing (hardcoded, update all three)

- `Upgrades/DraftUpgradeService.cs` → `ConfigureUltimateHandler`: add an `ultimateId.StartsWith("<prefix>")` branch that finds your component with `rootTr.GetComponentInChildren<T>(true)` and enables it. Its `AddComponent` fallback creates a handler with **no serialized refs**; don't rely on it (put the component on the prefab, step 4).
- `Player/PlayerUltimateController.cs` → `GetDefaultUltimateIdForEquippedWeapons`, if this is the default ultimate for a weapon.
- `Debug/DevConsole.cs` → `AvailableUltimates`, so Cycle Ult / Unlock Ult / 100% Ult Unleash can reach it.

## 3. The handler

`public class XUltimate : MonoBehaviour, IUltimateHandler` in `Player/`, implementing `Activate(PlayerUltimateController controller)` and `BaseDuration`.

- **Find the player** with `transform.root.GetComponentInChildren<Player>(true)`. `Player` is on the child `SidekickSyntyCharacter`, so `GetComponent<Player>()` finds nothing.
- **Tunables on an SO.** `BaseDuration` comes from an `UltimateConfigSO` (`Assets/Bladehold/Config/Ultimates/`, menu `Scriptable Objects/UltimateConfigSO`). Other numbers go on an SO too, not loose serialized floats (older handlers still use loose fields; don't copy that).
- **Upgradeable numbers are `StatType` bases.** Duration is `StatType.UltimateDurationSeconds`: the controller sets its base from `BaseDuration` on activation, so read the final value with `player.Stats.GetValue(...)`. A new stat is **appended** to `StatType` (never reorder) and registered with `player.Stats.SetBase` by the handler, in `Awake`, which runs even while the component is disabled. Scale damage by `StatType.AllDamageMultiplier` like the others.
- **Validate in `Start`:** null-check the player, stats, SO and every `MMF_Player`, `Debug.LogError` and set `anyError`. `Activate` with `anyError` calls `controller.EndUltimate()` and returns. No silent failures.
- **All feedback through MMF.** Serialized `MMF_Player` fields, `PlayFeedbacks(position)` (see `/feel-integration`). No `Instantiate(vfxPrefab)`, `PlayOneShot` or direct camera shake, and no code-built visuals. Gameplay objects that do damage (e.g. orbiting blades) are authored prefabs on serialized fields.
- **Lifecycle:** keep the `controller` from `Activate`; call `controller.EndUltimate()` **once** when the effect ends. Revert every stat modifier (`RemoveModifier`) and `Health` hook (`ScaleDamageTaken`, `TryBlockDamage`, ...) you added, both at the end and in `OnDisable` (the handler gets disabled when the loadout changes).
- Damage you deal: `new Damage { ..., source = player.Damageable, isPlayerDamage = true }`. The controller ignores charge gain while an ultimate is active.

Then `/compile-check`.

## 4. Editor wiring

Add the component to the **root** of `Assets/Bladehold/Bladehold Prefabs/Player.prefab`, **disabled** (next to `PlayerUltimateController` and the other handlers), create its `UltimateConfigSO`, and wire the MMF players and prefabs. Use Unity MCP if connected (`/unity-editor-mcp`); anything left goes in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo`, with a Play-mode check: DevConsole → Cycle Ult to it → **100% Ult Unleash** → press Ultimate.

## 5. Checks

Add a benchmark section (`/test-mechanic`; section 11 is the existing ultimate check): the card parses with `isUltimate`, `ApplyUpgrade` sets `ActiveUltimateId` and `UltimateUnlocked`, `ConfigureUltimateHandler` leaves exactly your handler enabled, and `BaseDuration > 0`. Lance runs it unless he asks. Add a `/changelog` entry.
