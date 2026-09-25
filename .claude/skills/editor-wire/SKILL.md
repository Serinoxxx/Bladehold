---
name: editor-wire
description: Use when a plans/editor/ checklist has unchecked items to actually execute — works through the items via the unity-mcp bridge, ticks them off in place, and leaves only the genuinely human-only leftovers.
---

# Execute a plans/editor/ checklist via MCP

`/editor-wiring-todo` *writes* the checklist (`plans/editor/NN-<topic>.md`, one per plan); this skill *does* it. That file is the durable ledger — the deliverable of this skill is checked-off `- [x]` items there, not chat claims. Never touch `TODO.md` (Lance's personal list).

## Ground truth first

1. Read the whole target checklist **including its Verify and Playtest sections** — the verification items tell you what the wiring is supposed to make true.
2. Read `/unity-editor-mcp` (bridge preconditions, layer-picker table, play-mode pattern) — this skill is that one applied to a specific checklist.
3. Skim the C# files the entry's blurb points at; the checklist was written against that code and the code may have moved since. **Code wins over the checklist** — if they disagree, update the checklist item, then do what the code needs.

## Step 1 — Triage the checklist

Mark each `- [ ]` item as one of:
- **MCP-doable**: SO asset instances, component adds, ref wiring, prefab/scene edits, menu items, icon-less registration steps.
- **Human-only**: animation events baked on clip import settings, animator state/clip authoring, art/audio picks, "does it feel right" judgment calls.
- **Already done**: verify in the Editor before ticking — entries rot.

## Step 2 — Execute MCP-doable items, one at a time

For each item: do it → verify it stuck (re-query the asset/ref) → edit the checklist to `- [x]` **immediately**, appending a short parenthetical if reality deviated (`(- [x] ... — asset already existed, values updated)`). Never batch-tick at the end; a crash mid-session must leave the checklist accurate.

## Step 3 — Verify and hand off the rest

1. Run the checklist's **Verify/Playtest** items that are automatable: Play mode + DevConsole cheats / EnemyZoo via MCP; tick what passes.
2. For human-only leftovers, edit the item in place to make the humanness explicit: `- [ ] **HUMAN:** bake the OneHandedSwordAttack event on the clip import settings — a missing event = silent no-hit swings`.
3. If any wiring revealed a code bug, fix it in C# (`/compile-check`), then re-verify.

## Pitfalls

- Ticking an item you *believe* is done without re-querying the Editor — the checklist may predate a scene revert.
- Wiring in Play mode (lost on exit) — edit mode only, verify in Play mode.
- Forgetting that MCP edits are working-tree changes: the scene/prefab/asset/meta diffs belong in the commit.
- Doing enemy-prefab work by hand that `/generate-enemy-prefabs` owns — check `Editor/EnemyManifest.cs` first.

## Finish protocol

Checklist accurate (`- [x]` done / `**HUMAN:**` leftovers; delete the file if nothing's left and drop it from `plans/00-editor-checklist-human.md`) → console clean → `/compile-check` if C# changed → commit to `main` and push.
