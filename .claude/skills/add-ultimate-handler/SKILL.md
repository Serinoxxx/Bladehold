---
name: add-ultimate-handler
description: Use when adding or changing a weapon ultimate in Bladehold — the IUltimateHandler component, its isUltimate draft card in DraftUpgrades.csv, the hardcoded id routing in DraftUpgradeService/PlayerUltimateController/DevConsole, and the Player.prefab wiring.
---

# Add a weapon ultimate

Ultimates belong to **weapons**, not classes (classes are gone). Since plan 21 phase 5 every **held weapon's ultimate is always unlocked**: nothing is bought or drafted. `DraftUpgradeService.GetUltimate(UltimateSlot)` / `GetUltimateId(slot)` resolve the `isUltimate` row of the held melee / ranged weapon (`GetEquippedWeaponIds`), and `ConfigureUltimateHandlers` enables those two handlers (every other `IUltimateHandler` is disabled). It runs on scene load (`RunSession.RestoreInRunUpgrades`), on equip (`PlayerWeaponManager`) and before each activation. `ApplyUpgrade`/`DebugSetDraftLevel` refuse `isUltimate` rows.

Each activation costs **one Arcane Core** (`RunSession.ArcaneCores`, `AddArcaneCores` / `TrySpendArcaneCore`, reset in `StartNewRun`; sources: captains via `CaptainRewards`, the Arcane Fish, the shop's `arcane_core` item, the `second_ultimate` "Arcane Reserve" meta perk). There is **no charge**. The **Ultimate Wheel** (`UI/UltimateWheelUI.cs` in `Bladehold HUD.prefab`, tunables in `Bladehold Config/UltimateWheelConfig.asset`) opens while the `Ultimate` action is held (Q / Left Bumper), slows time to 10%, and fires a slot through `PlayerUltimateController.TryActivate(slot)`, which checks `GetBlockReason` (scene rules, weapon has an ultimate, none running, a core banked), spends the core and calls `Activate`. One ultimate runs at a time (`ActiveSlot`). The HUD's two `UltimateBarUI`s show Ready / No core / the running timer per slot.

Existing handlers in `Player/` (names are class-era leftovers): `SwordBladeTempestUltimate` (sword), `BerserkerUltimate` (axe), `RangerUltimate` (bow), `ThrowingAxeUltimate`, `MaceUltimate` (the cleanest model), plus `MageUltimate` and `SwordMountUltimate`, which no draft card reaches.

## 1. Draft card (`Assets/Bladehold/Resources/DraftUpgrades.csv`)

One row per weapon (the catalog is shared with draft cards, but the draft pool skips `isUltimate` rows): `category` = `Weapon`, `weapon` = the weapon id (`sword`, `axe`, `mace`, `bow`, `throwing_axe`, ...), `isUltimate` = `1`, `maxLevel` = `1`, `stat` = `UltimateUnlocked`, `kind` = `Flat`, `amount` = `1`. Extra unlock stats go `;`-separated in `stat`/`kind`/`amount` (see `taxe_vortex_ult`). `icon` is a sprite name resolved through `Resources/SkillTreeIcons` (`SkillTreeIconsSO`); the shop card shows it. `displayName`/`description` are the wheel slice's text (no input instructions; the wheel explains the cost). `GetUltimateForWeapon` takes the first `isUltimate` row for a weapon, so keep **one per weapon**.

The **id prefix is the routing key** (next step), so pick a unique one, e.g. `spear_<name>_ult`.

## 2. Routing (hardcoded)

- `Upgrades/DraftUpgradeService.cs` → `GetUltimateHandler`: add an `ultimateId.StartsWith("<prefix>")` line returning `FindOrAdd<YourUltimate>()`. Its `AddComponent` fallback creates a handler with **no serialized refs**; don't rely on it (put the component on the prefab, step 4).
- Nothing in DevConsole lists ultimates any more: equip the weapon and use **+5 Arcane Cores**.

Handlers must stay idle while enabled but not activated (both held weapons' handlers are enabled at once): do nothing in `Update`/`LateUpdate` until `Activate`.

## 3. The handler

`public class XUltimate : MonoBehaviour, IUltimateHandler` in `Player/`, implementing `Activate(PlayerUltimateController controller)` and `BaseDuration`.

- **Find the player** with `transform.root.GetComponentInChildren<Player>(true)`. `Player` is on the child `SidekickSyntyCharacter`, so `GetComponent<Player>()` finds nothing.
- **Tunables on an SO.** `BaseDuration` comes from an `UltimateConfigSO` (`Assets/Bladehold/Config/Ultimates/`, menu `Scriptable Objects/UltimateConfigSO`). Other numbers go on an SO too, not loose serialized floats (older handlers still use loose fields; don't copy that).
- **Upgradeable numbers are `StatType` bases.** Duration is `StatType.UltimateDurationSeconds`: the controller sets its base from `BaseDuration` on activation, so read the final value with `player.Stats.GetValue(...)`. A new stat is **appended** to `StatType` (never reorder) and registered with `player.Stats.SetBase` by the handler, in `Awake`, which runs even while the component is disabled. Scale damage by `StatType.AllDamageMultiplier` like the others.
- **Validate in `Start`:** null-check the player, stats, SO and every `MMF_Player`, `Debug.LogError` and set `anyError`. `Activate` with `anyError` calls `controller.EndUltimate()` and returns. No silent failures.
- **All feedback through MMF.** Serialized `MMF_Player` fields, `PlayFeedbacks(position)` (see `/feel-integration`). No `Instantiate(vfxPrefab)`, `PlayOneShot` or direct camera shake, and no code-built visuals. Gameplay objects that do damage (e.g. orbiting blades) are authored prefabs on serialized fields.
- **Lifecycle:** keep the `controller` from `Activate`; call `controller.EndUltimate()` **once** when the effect ends. Revert every stat modifier (`RemoveModifier`) and `Health` hook (`ScaleDamageTaken`, `TryBlockDamage`, ...) you added, both at the end and in `OnDisable` (the handler gets disabled when the loadout changes).
- Damage you deal: `new Damage { ..., source = player.Damageable, isPlayerDamage = true }`.

Then `/compile-check`.

## 4. Editor wiring

Add the component to the **root** of `Assets/Bladehold/Bladehold Prefabs/Player.prefab`, **disabled** (next to `PlayerUltimateController` and the other handlers), create its `UltimateConfigSO`, and wire the MMF players and prefabs. Use Unity MCP if connected (`/unity-editor-mcp`); anything left goes in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo`, with a Play-mode check: equip the weapon, DevConsole → **+5 Arcane Cores** → hold Q, pick it on the wheel.

## 5. Checks

Add a benchmark section (`/test-mechanic`; section 11 is the existing ultimate check): the card parses with `isUltimate` and is the weapon's only ultimate row (11G), the handler is on `Player.prefab` with its refs wired (11E), and `BaseDuration > 0`. 11F covers the Arcane Core spend. Lance runs it unless he asks. Add a `/changelog` entry.
