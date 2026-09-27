# 12: Mount from start + fishing restrictions

**Decided (Lance, Sep 2026):**
- The player can summon their mount from the start of every run by pressing **X**. Cast time, ride duration and cooldown all stay.
- In the **Fishing Pond** it's just you and your bow: no mount summon, no ultimate.

## Current state

- `Player/PlayerSummonMount.cs` is gated by `StatType.SummonMountUnlocked`: base 0 is set at ~L66, and nothing ever raises it, so summoning is unreachable.
- It listens to the Synty `Dismount` action (Q / pad East), not X. The changelog and old docs claim X.
- Mount variants come from `Horse/MountDefinitionSO` (`Resources/Mounts/`); the equipped one is in `SaveData`.
- `Player/PlayerUltimateController.cs` fires on the "Ultimate" input action with no per-scene block.

## Tasks

- [x] **Unlock from start:** done 2026-09-27: `PlayerSummonMount` sets the `SummonMountUnlocked` base to 1 (gate kept as a stat). set the `SummonMountUnlocked` base to 1, or remove the gate if nothing should ever lock it; ask Lance. Keep `basic_horse` as the default.
- [x] **Bind summon to X:** done 2026-09-27: new `SummonMount` action, keyboard X + gamepad **D-pad Up** (Lance: a free D-pad button; Down is DraftSkills). Shares X with `Dismount` on keyboard (on foot summons, riding dismounts); gamepad dismount stays East. `Controls.cs` updated by hand to match the asset (embedded JSON regenerated from it), `InputReader.onSummonMountPerformed` added. The settings rebind list builds rows from the action map, so a "SummonMount" row appears automatically.
  - Add a dedicated `SummonMount` action to the Synty `Controls.inputactions` (keyboard X; pick a free gamepad button and confirm with Lance), regenerate the wrapper, and add the `InputReader` callbacks.
  - Don't overload `Dismount` unless Lance wants the same key for both.
  - Check the rebinding UI (`InputSettingsBinder`) picks up the new action.
- [x] **Scene restrictions, done as data rather than scene-name checks.** Suggestion: a small per-scene rules component or SO (e.g. `SceneAbilityRules` with `allowMount`, `allowUltimate`, `allowMelee`) on the Fishing Pond scene, read by `PlayerSummonMount` and `PlayerUltimateController`.
  - Hide or grey the related HUD elements (mount cast bar/cooldown, ultimate meter) while blocked.
  - Keep the ultimate charge untouched, so it carries on in the next node.
  - Done 2026-09-27: `Player/SceneAbilityRules.cs` (scene singleton; no component = all allowed), placed in `Bladehold Fishing Pond.unity` via MCP as "Scene Ability Rules" with all three off. Mount: `PlayerSummonMount.IsAbilityUnlocked` folds it in, so `SummonMountUI` hides the slot. Ultimate: `PlayerUltimateController.HandleUltimateInput` refuses, `UltimateBarUI` deactivates itself in `Start`. Charge is never written while blocked (`UltimateUnlocked` is 0 in the pond anyway).
- [x] **Normal weapons in the pond (from plan 04, finding 5):** `PlayerAttack`/`PlayerWeaponManager` stay live in the Fishing Pond, so un-aimed clicks swing the sword. (The pond now uses the real bow with free ammo, `PlayerAmmo.InfiniteAmmo`, and `RunSession.RunUpgradesSuspended` already blocks the ultimate there by not restoring it, 2026-09-27.) Add `allowMelee` to the rules component and have the attack code respect it. **Decided (Lance, 2026-09-27): yes, block melee in the pond.** Done: the vendored `SamplePlayerAnimationController.ActivateAttack` and `PlayerAttack.HandlePressed` return when `MeleeAllowed` is false; aimed bow shots are unaffected.
- [x] Update `/CLAUDE.md` once it's done. Those sections no longer exist; added a `SceneAbilityRules` bullet under Conventions instead. Editor checklist: [editor/12-mount-and-fishing.md](editor/12-mount-and-fishing.md).

## Acceptance

- New save → first sector: X summons the horse after the cast time, it expires after its duration, and the cooldown shows on the HUD.
- Fishing Pond: X and the ultimate do nothing and their HUD is hidden.
- Leaving the pond: the ultimate charge is unchanged.
