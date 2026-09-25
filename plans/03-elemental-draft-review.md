# 03: Elemental draft system review + fix

**Symptom (Lance):** the DevConsole element buttons apply elements to weapons nicely (glow etc.), but picking elemental draft cards never changes the weapon glow.

## Likely cause (verify first)

- Every `Elemental` row in `Assets/Bladehold/Resources/DraftUpgrades.csv` has a **blank `targetSlot`**, so `DraftUpgradeService` (~L411) never calls `RunSession`'s `SetElementalSlot`.
- The DevConsole (`Debug/DevConsole.cs` ~L420-432) sets `RunSession.ElementalSlots` directly, and `PlayerWeaponManager` (~L202-293) reads those slots for highlights, so it works.
- Hit riders in `Player/DraftWeaponElementEffects.cs` and the duo prerequisite checks also key off the slots.

## Review scope

`Upgrades/DraftUpgradeService.cs`, `Upgrades/DraftCategory.cs`, the CSV parser, `Economy/RunSession.cs` (ElementalSlots, SLOT_MELEE/RANGED/ULTIMATE), `Player/PlayerWeaponManager.cs` (highlight code), `Player/DraftWeaponElementEffects.cs`, the DevConsole element code, the element lock rule ("picking an element locks the run to it", `docs/game-loop-progression-and-economy.md` §5), and the draft card UI (`SurvivorsCardSelectUI`).

## Fix tasks

- [ ] **Decide the model with Lance before coding.** Which slot does an elemental card imbue: a fixed one per card, the player's choice, or "first empty slot"? The DevConsole shows slots are per weapon plus ultimate.
- [ ] Make drafting an elemental card set the slot through the **same code path the DevConsole uses**, so there's one path. Refactor the DevConsole to call it too.
- [ ] **Duo cards:** category `Element` isn't in the `DraftCategory` enum, so it silently parses as Weapon, and the duos (thermal_shock, plasma_overload, superconductor) appear in weapon drafts without prerequisites. Fix the enum or the CSV, make unknown categories a load error, and enforce `prerequisiteElements`.
- [ ] Enforce the element lock in candidate filtering (`GetCandidateUpgrades`).
- [ ] Glow/imbue visuals must be prefab/MMF-driven, not code-built. Flag any code-built visuals found for plan 09.
- [ ] Add regression coverage for "draft elemental card → slot set → weapon highlight on". Follow plan 11 if it has landed; otherwise add to `Editor/WeaponReachBenchmark.cs`.

## Acceptance

Drafting a fire card in a real sector makes the weapon glow and the hits ignite, duos only offer with their prerequisites, and a locked element excludes other elements.
