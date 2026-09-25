# 05: Balance Tree Editor review + usability

The "view of all the drafts and dependencies" is **`Editor/BalanceTreeEditorWindow.cs`** (menu `Bladehold > Balance Tree Editor`, or F1). It's a GraphView over `DraftUpgrades.csv` cards, weapons, armour sets, mounts, meta perks and tower defences, with inline editing and TODO nodes that write `UPGRADE_TODOS.md/.json` (those files don't currently exist).

## Review focus

- Correctness: does saving round-trip the CSV losslessly (column order, `;` / `|` lists, quoting)? Does live-apply mid-game actually reach `DraftUpgradeService`, or does it go stale?
- Does it show real dependencies: weapon → cards, element → cards/duos (`prerequisiteElements`), ultimate cards, card → `StatType` → the systems reading it, meta perk tiers, tower types → tower element cards?
- Code size/structure: it's 728 lines; split view, model and IO if needed.

## Make it nice to work with (confirm the wishlist with Lance before building)

- [ ] **Auto layout** by category/weapon/element lanes. Persist manual node positions (e.g. an EditorPrefs/JSON sidecar), not in the CSV.
- [ ] **Filters and search:** by weapon, element, category, `isUltimate`, "not reachable in demo" (plan 07 data).
- [ ] **Validation panel:** unknown categories, blank `targetSlot` on elemental cards, stats with no consumer, cards with no icon, duplicate ids, missing localization keys.
- [ ] **Card preview** showing the per-level numbers (`|` values) and rendered description text.
- [ ] A **pacing view**: how many cards each weapon/element has, for spotting thin pools during the content phase.
- [ ] Remove or repurpose the TODO-node feature if Lance doesn't use it.

## Acceptance

Open it, filter to "sword + fire", see every relevant card and dependency, edit a number, save, and the CSV diff is only that number.
