# Editor to-do: plan 12 (Mount from start + fishing restrictions)

From [plan 12](../12-mount-and-fishing-restrictions.md), 2026-09-27. Tick items off as you go and delete the file when it's empty.

**Done in C#:**
- `Player/PlayerSummonMount.cs`: unlocked from the start (stat base 1) and listens to the new `SummonMount` action.
- The Synty `Controls.inputactions`, `Controls.cs` and `InputReader.cs` gained `SummonMount` (X / D-pad Up).
- The new `Player/SceneAbilityRules.cs` is read by the summon, the ultimate (`PlayerUltimateController`, `UltimateBarUI`) and melee (`PlayerAttack`, the Synty `SamplePlayerAnimationController.ActivateAttack`).

**Done by an agent via MCP (2026-09-27):**
- Unity compiled with no errors, imported the new action (X, D-pad Up) and gave the new script its GUID.
- Added a "Scene Ability Rules" object to `Bladehold Fishing Pond.unity` with Allow Mount, Allow Ultimate and Allow Melee all off, and saved the scene.

## 1. Verify first

- [x] **Generated wrapper matches.** Checked by an agent via MCP 2026-09-27: a forced reimport of `Controls.inputactions` (Generate C# Class is on) left the hand-edited `Controls.cs` byte-identical.

## 2. Playtest

- [ ] **New save → first sector:** the mount slot shows on the HUD. X (keyboard) or D-pad Up (controller) starts the cast bar, the horse arrives after the cast and you ride it. It expires after its duration, and the cooldown counts down on the slot.
- [ ] **While riding, X dismounts** and doesn't start a second summon. On a controller, East still dismounts.
- [ ] **Moving or taking a hit during the cast cancels it.**
- [ ] **Settings → Controls:** a "SummonMount" row shows X / D-pad Up and can be rebound. Decide whether it needs a friendlier label.
- [ ] **Fishing Pond:**
  - X / D-pad Up does nothing, and the mount slot and ultimate bar are hidden.
  - Q / pad North does nothing.
  - Clicking without aiming doesn't swing the sword; aimed bow shots still fire.
- [ ] **Leaving the pond:** ultimate charge is the same as before you entered, and the mount works again in the next sector.
- [ ] **UltimateBarUI hides the whole bar** (check its component sits on the bar's root, not a child that leaves an empty frame).
