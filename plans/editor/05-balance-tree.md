# Editor to-do: plan 05 (Balance Tree Editor)

From [plan 05](../05-balance-tree-editor-review.md), 2026-09-25. Unity MCP wasn't connected, so the window has only been compile-checked, never opened. Tick items off as you go and delete the file when it's empty.

## 1. Verify first (10 min)

- [ ] **It opens.** **Bladehold > Balance Tree Editor** (F1). You should see lanes, left to right: weapons/elements/towers | weapon cards | elemental cards | fortress cards | stats | armour/mounts/perks. The console should stay clean.
- [ ] **Acceptance: sword + fire.**
  - Set **Weapon: sword** and **Element: Fire**. You should get the 5 sword cards, the Fire cards (including Thermal Shock and Plasma Overload), their stats, and the Arrow Tower and Catapult on the fire tower cards.
  - Select Lunge Mastery, change `amount` `2.0;0.40` → `2.5;0.40`, press Enter, then **Save**.
  - `git diff Assets/Bladehold/Resources/DraftUpgrades.csv` should show exactly that one number. Then revert it.
- [ ] **Unsaved-edit guard.** Edit a cell, then close the window: Unity should offer to save. Also check that **Reload** asks before discarding.
- [ ] **Layout persists.** Drag a node, then close and reopen: it stays put (saved in `UserSettings/BalanceTreeLayout.json`). **Reset Layout** puts it back.
- [ ] **Validation tab.**
  - Expect warnings for the 4 stats nothing reads: `UltimateThrowingAxeVortexUnlocked`, `FortArrowSlitsUnlocked`, `FortBurningOilUnlocked`, `FortSpikesUnlocked`.
  - Expect a warning that `fort_electrified_oil` / `fort_permafrost_spikes` set `isDuo` on a Fortress card.
  - Clicking an issue selects its node.
  - Tell an agent about any false positive (for example an icon warning for an icon that does show in-game).

## 2. Decisions

- [ ] **The 4 dead stats.** They do nothing today.
  - The axe vortex ultimate unlocks by card id, so its stat can just be dropped from the CSV row.
  - The three `fort_*` stats belong to legacy Fortress cards, which plan 08 may delete outright.
- [ ] **The two Fortress "duo" rows** log an error on every load. Nothing reads `isDuo`/`prerequisiteElements` on Fortress cards, so either clear those two cells or delete the rows with the legacy Fortress cards in plan 08.
