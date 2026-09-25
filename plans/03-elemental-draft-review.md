# 03: Elemental draft system review + fix

**Symptom (Lance):** the DevConsole element buttons apply elements to weapons nicely (glow etc.), but picking elemental draft cards never changes the weapon glow.

## Likely cause (verify first)

- Every `Elemental` row in `Assets/Bladehold/Resources/DraftUpgrades.csv` has a **blank `targetSlot`**, so `DraftUpgradeService` (~L411) never calls `RunSession`'s `SetElementalSlot`.
- The DevConsole (`Debug/DevConsole.cs` ~L420-432) sets `RunSession.ElementalSlots` directly, and `PlayerWeaponManager` (~L202-293) reads those slots for highlights, so it works.
- Hit riders in `Player/DraftWeaponElementEffects.cs` and the duo prerequisite checks also key off the slots.

## Review scope

`Upgrades/DraftUpgradeService.cs`, `Upgrades/DraftCategory.cs`, the CSV parser, `Economy/RunSession.cs` (ElementalSlots, SLOT_MELEE/RANGED/ULTIMATE), `Player/PlayerWeaponManager.cs` (highlight code), `Player/DraftWeaponElementEffects.cs`, the DevConsole element code, the element lock rule ("picking an element locks the run to it", `docs/game-loop-progression-and-economy.md` §5), and the draft card UI (`SurvivorsCardSelectUI`).

## Fix tasks

- [x] **Decide the model with Lance before coding.** Which slot does an elemental card imbue: a fixed one per card, the player's choice, or "first empty slot"? The DevConsole shows slots are per weapon plus ultimate.
- [x] Make drafting an elemental card set the slot through the **same code path the DevConsole uses**, so there's one path. Refactor the DevConsole to call it too.
- [x] **Duo cards:** category `Element` isn't in the `DraftCategory` enum, so it silently parses as Weapon, and the duos (thermal_shock, plasma_overload, superconductor) appear in weapon drafts without prerequisites. Fix the enum or the CSV, make unknown categories a load error, and enforce `prerequisiteElements`.
- [x] ~~Enforce the element lock in candidate filtering (`GetCandidateUpgrades`).~~ Dropped: Lance chose mix-and-match (no lock).
- [x] Glow/imbue visuals must be prefab/MMF-driven, not code-built. Flag any code-built visuals found for plan 09.
- [x] Add regression coverage for "draft elemental card → slot set → weapon highlight on". Follow plan 11 if it has landed; otherwise add to `Editor/WeaponReachBenchmark.cs`.

## Acceptance

Drafting a fire card in a real sector makes the weapon glow and the hits ignite, duos only offer with their prerequisites, and a locked element excludes other elements. *(Lock clause superseded: no lock, elements mix across slots.)*

## Outcome (2026-09-25)

**Decisions (Lance):** no element lock (mix-and-match per `docs/ElementSystemSpec.md`; game-loop doc §5 updated). Each card imbues a **fixed slot** set in the CSV `targetSlot` column:

| Slot | Cards |
|---|---|
| `SLOT_MELEE` | Combustion (Fire), Static Edge (Lightning) |
| `SLOT_RANGED` | Ice Shards (Ice) |
| `SLOT_MOBILITY` | Blazing Trail, Chain Dash, Frost Step |
| `SLOT_ULTIMATE` | Inferno Burst, Eye of the Storm |
| `SLOT_FORTRESS` | Fortress Pyre, Tesla Spire, Permafrost, and the 6 tower cards |
| none (passive) | Kindling, Deep Freeze, Shatter: count as an active element for duos |

**Done:**
- `DraftUpgradeService.ImbueSlot`/`ClearSlot` are the one path for slots: draft picks, `DebugSetDraftLevel` and the DevConsole all use it. The DevConsole now shows all five slots.
- Overwrite strips *all* the old element's cards on that slot (it used to stop after one), pays 25 gold per card, and the card text shows an `[Overwrite]` line.
- Duo rows fixed to `Elemental`. Unknown category, unknown slot, missing element or a malformed duo is now a load error and the row is skipped. Unknown `StatType` is logged too.
- Duo prerequisites check `GetActiveElements()` = imbued slots + owned passive elemental cards.
- **Fixed a stacking bug:** `ApplyUpgrade` added each level's amount on top of the last (Blazing Trail L3 = 2+3+4), but the scene-load reapply and the debug path treat amounts as absolute (L3 = 4), so drafts changed strength after every scene load. `ApplyUpgrade` now swaps old level for new (absolute).
- Tests: benchmark section 26 (edit mode: duo parse/gating, no-lock pool, slot set, overwrite + gold) and a drafted-card glow case in **Bladehold/Tests/Draft Weapon Charges (Play Mode)**.

**Flagged for plan 09 (code-driven visuals/feedback):**
- `PlayerWeaponManager.HandleElementalSlotChanged` `Instantiate`s a poison weapon VFX (the Fire/Ice/Lightning glow is HighlightPlus profiles on the prefab, fine).
- `DraftWeaponElementEffects.Explode` `Instantiate`s `explosionVfxPrefab` directly, not via MMF.
- `PlayerDodge` elemental dash: `Instantiate` of the dash VFX prefabs + `AudioSource.PlayClipAtPoint`.
- `DraftUpgradeService.ConfigureUltimateHandler` `AddComponent`s ultimate handlers as a runtime fallback.

**Design notes for Lance:**
- Only Ice has a ranged card, so Fire/Lightning can never make the bow glow. Consider a Fire and a Lightning ranged card (content, Phase 4).
- A Fire tower slot also turns Arrow Tower arrows into Fire damage (`FortArrowProjectile` falls back to `SLOT_FORTRESS`).

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/03-elemental-draft.md`](editor/03-elemental-draft.md).
