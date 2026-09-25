# 05: Balance Tree Editor review + usability

The "view of all the drafts and dependencies" is **`Editor/BalanceTreeEditorWindow.cs`** (menu `Bladehold > Balance Tree Editor`, or F1). It's a GraphView over `DraftUpgrades.csv` cards, weapons, armour sets, mounts, meta perks and tower defences, with inline editing and TODO nodes that write `UPGRADE_TODOS.md/.json` (those files don't currently exist).

## Review focus

- Correctness: does saving round-trip the CSV losslessly (column order, `;` / `|` lists, quoting)? Does live-apply mid-game actually reach `DraftUpgradeService`, or does it go stale?
- Does it show real dependencies: weapon → cards, element → cards/duos (`prerequisiteElements`), ultimate cards, card → `StatType` → the systems reading it, meta perk tiers, tower types → tower element cards?
- Code size/structure: it's 728 lines; split view, model and IO if needed.

## Make it nice to work with (confirm the wishlist with Lance before building)

- [x] **Auto layout** by category/weapon/element lanes. Persist manual node positions (e.g. an EditorPrefs/JSON sidecar), not in the CSV.
- [x] **Filters and search:** by weapon, element, category, `isUltimate`, "not reachable in demo" (plan 07 data).
- [x] **Validation panel:** unknown categories, blank `targetSlot` on elemental cards, stats with no consumer, cards with no icon, duplicate ids, missing localization keys.
- [x] **Card preview** showing the per-level numbers (`|` values) and rendered description text.
- [x] A **pacing view**: how many cards each weapon/element has, for spotting thin pools during the content phase.
- [x] ~~Remove or repurpose the TODO-node feature if Lance doesn't use it.~~ Removed (Lance).

## Acceptance

Open it, filter to "sword + fire", see every relevant card and dependency, edit a number, save, and the CSV diff is only that number.

## Outcome (2026-09-25)

**Decisions (Lance):** build the whole wishlist, remove the TODO nodes, drop live-apply.

**Bugs found and fixed:**
- **Save rewrote the whole CSV.** `File.WriteAllLines` wrote CRLF over an LF file, so every line changed. Save now writes only edited rows (untouched lines byte for byte) and keeps line endings, BOM, trailing newline, per-cell quoting and any extra columns. It writes nothing when nothing changed.
- **Rows with fewer than 16 columns were silently dropped on save.** The editor's parser also lost `""` escaped quotes. It now uses the runtime's `DraftUpgradeService.ParseCsvRow`.
- **Live-apply was dead.** It reflected into a `ReloadCatalog` that doesn't exist, and the Resources TextAsset isn't reimported mid-play. Removed. Save now reimports the CSV and says changes apply from the next Play session.
- **Dependencies:** only weapon → card and perk prerequisites were drawn. Now drawn:
  - weapon → card (ultimates get a gold border)
  - element → card, and duo prerequisites
  - tower → card, when a `Fort/` script on the tower prefab reads the card's stat
  - card → StatType and armour → StatType; a stat's inspector lists the gameplay scripts that reference it
  - meta perk prerequisite → perk
- An unsaved-changes prompt on close (`hasUnsavedChanges`).

**Structure:** `Editor/BalanceTree/`:
- `DraftCsvDocument` (lossless IO)
- `BalanceTreeModel` (nodes, edges, stat-consumer index)
- `BalanceTreeFilter`
- `BalanceGraphView` (lanes, barycenter layout, position store in `UserSettings/BalanceTreeLayout.json`)
- `BalanceTreeValidator` (plus pacing)
- `BalanceTreeEditorWindow`

`DraftUpgradeService.ParseRow` is now `public static` with an optional error list, so the validation panel runs the game's own row rules.

**Filter semantics:** weapon and element combine as a union ("sword + fire" = sword cards plus Fire cards, duos needing Fire included). Category, Ultimates, Demo only and search narrow on top. Hubs, stats, towers and armour show when they're attached to a visible card. **Demo only** is a stand-in (it hides demo-locked weapons and tier 2+ perks) until plan 07 owns the gating data.

**Validator findings on today's CSV:**
- 4 stats nothing reads: `UltimateThrowingAxeVortexUnlocked`, `FortArrowSlitsUnlocked`, `FortBurningOilUnlocked`, `FortSpikesUnlocked`.
- `fort_electrified_oil` and `fort_permafrost_spikes` set `isDuo` on Fortress cards, which already logs an error at runtime.
- Draft cards have no localization keys at all (no `draft.*` rows in `Strings.csv`), unlike skill nodes. That's for the Phase 3 localization plan.

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/05-balance-tree.md`](editor/05-balance-tree.md).
