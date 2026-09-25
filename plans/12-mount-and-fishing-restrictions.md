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

- [ ] **Unlock from start:** set the `SummonMountUnlocked` base to 1, or remove the gate if nothing should ever lock it; ask Lance. Keep `basic_horse` as the default.
- [ ] **Bind summon to X:**
  - Add a dedicated `SummonMount` action to the Synty `Controls.inputactions` (keyboard X; pick a free gamepad button and confirm with Lance), regenerate the wrapper, and add the `InputReader` callbacks.
  - Don't overload `Dismount` unless Lance wants the same key for both.
  - Check the rebinding UI (`InputSettingsBinder`) picks up the new action.
- [ ] **Scene restrictions, done as data rather than scene-name checks.** Suggestion: a small per-scene rules component or SO (e.g. `SceneAbilityRules` with `allowMount`, `allowUltimate`, `allowMelee`) on the Fishing Pond scene, read by `PlayerSummonMount` and `PlayerUltimateController`.
  - Hide or grey the related HUD elements (mount cast bar/cooldown, ultimate meter) while blocked.
  - Keep the ultimate charge untouched, so it carries on in the next node.
- [ ] **Normal weapons in the pond (from plan 04, finding 5):** `PlayerAttack`/`PlayerWeaponManager` stay live in the Fishing Pond, so clicks swing the sword and the real bow spends `RunSession.CurrentAmmo`. Add `allowNormalWeapons` (or `allowMelee` + `allowRanged`) to the rules component and have the attack/aim code respect it, so only `FishingBowController` fires there.
- [ ] Check the fishing bow flow doesn't already disable these some other way (`Fishing/FishingBowController.cs`); unify on the rules component.
- [ ] Update `/CLAUDE.md` (Player kit + Design direction) once it's done.

## Acceptance

- New save → first sector: X summons the horse after the cast time, it expires after its duration, and the cooldown shows on the HUD.
- Fishing Pond: X and the ultimate do nothing and their HUD is hidden.
- Leaving the pond: the ultimate charge is unchanged.
