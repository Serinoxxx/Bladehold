---
name: add-ultimate-handler
description: Use when adding or changing a weapon ultimate in Bladehold — the IUltimateHandler component, its isUltimate draft card in DraftUpgrades.csv, the hardcoded id routing in DraftUpgradeService/PlayerUltimateController/DevConsole, and the Player.prefab wiring.
---

# Add a weapon ultimate

Ultimates belong to **weapons**, not classes (classes are gone). They are **bought at the Rest Area shop, never drafted**: `DraftUpgradeService.GetShopUltimates` offers the `isUltimate` row of each equipped weapon whose slot is empty, and `ShopUI` builds the offers at runtime (no `ShopItemSO` asset per ultimate) priced by `UltimateShopConfigSO` (`Bladehold Config/ShopItems/UltimateShopConfig.asset`: 100 first, 400 second). A run has **one ultimate slot, two with the `second_ultimate` meta perk** (Twin Fury, tier 3), one per weapon: `RunSession.MeleeUltimateId` / `RangedUltimateId` (slot from `DraftUpgradeService.SlotForUltimate`: ranged if it's the equipped ranged weapon's). Buying runs `ApplyUpgrade`, which sets the slot, `UltimateUnlocked` = 1, and `ConfigureUltimateHandlers` enables both owned handlers (every other `IUltimateHandler` is disabled).

`PlayerUltimateController` (Player root) keeps **one charge bar per slot**: melee hits (`DamageTrigger.OnHit`) fill the melee bar, bow / throwing axe / wand hits fill the ranged bar, and any other damage enemies take (towers, burns, duos) fills both at half rate, settled in `LateUpdate`. The `Ultimate` input fires the **ranged ultimate while aiming** (`PlayerWeaponManager.ActiveAimWeapon.IsAiming`) and the melee one otherwise, or whichever one you own. One ultimate runs at a time (`ActiveSlot`), and no bar charges meanwhile. The HUD has one `UltimateBarUI` per slot (`slot` field; hidden through its `CanvasGroup` until owned).

Existing handlers in `Player/` (names are class-era leftovers): `SwordBladeTempestUltimate` (sword), `BerserkerUltimate` (axe), `RangerUltimate` (bow), `ThrowingAxeUltimate`, `MaceUltimate` (the cleanest model), plus `MageUltimate` and `SwordMountUltimate`, which no draft card reaches.

## 1. Draft card (`Assets/Bladehold/Resources/DraftUpgrades.csv`)

One row per weapon (the catalog is shared with draft cards, but the draft pool skips `isUltimate` rows): `category` = `Weapon`, `weapon` = the weapon id (`sword`, `axe`, `mace`, `bow`, `throwing_axe`, ...), `isUltimate` = `1`, `maxLevel` = `1`, `stat` = `UltimateUnlocked`, `kind` = `Flat`, `amount` = `1`. Extra unlock stats go `;`-separated in `stat`/`kind`/`amount` (see `taxe_vortex_ult`). `icon` is a sprite name resolved through `Resources/SkillTreeIcons` (`SkillTreeIconsSO`); the shop card shows it. `displayName`/`description` are the shop card's text (ranged ones say "Aim and press Q"). `GetUltimateForWeapon` takes the first `isUltimate` row for a weapon, so keep **one per weapon**.

The **id prefix is the routing key** (next step), so pick a unique one, e.g. `spear_<name>_ult`. The shop only offers ultimates for equipped, non-demo-locked weapons (`DemoConfigSO`).

## 2. Routing (hardcoded, update all three)

- `Upgrades/DraftUpgradeService.cs` → `GetUltimateHandler`: add an `ultimateId.StartsWith("<prefix>")` line returning `FindOrAdd<YourUltimate>()`. Its `AddComponent` fallback creates a handler with **no serialized refs**; don't rely on it (put the component on the prefab, step 4).
- `Debug/DevConsole.cs` → `AvailableUltimates`, so Cycle Ult / Unlock Ult / Fill Charge can reach it.

Handlers must stay idle while enabled but not activated (both owned handlers are enabled at once): do nothing in `Update`/`LateUpdate` until `Activate`.

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
