---
name: editor-wiring-todo
description: Use when finishing any Bladehold feature or plan whose C# is done but which needs manual Unity Editor work (SO assets, prefab/scene wiring, animator or clip work, icons, art/audio, UI review) — writes the plan's checklist file in plans/editor/.
---

# Write the Editor checklist in plans/editor/

Anything that can't be done by editing files headlessly (and wasn't done via Unity MCP) goes in **`plans/editor/NN-<topic>.md`**: one file per plan, matching the plan's number (e.g. `plans/editor/04-fishing.md` for `plans/04-fishing-review.md`). Work that isn't from a plan gets a short topic name with no number.

**Never write to `TODO.md`.** It's Lance's personal list. Don't leave the checklist only in chat either.

## Wiring

- The plan file's `## Needs Lance in the Editor` section holds just one line: `Moved to its own checklist: [\`plans/editor/NN-<topic>.md\`](editor/NN-<topic>.md).`
- Add the file to the "Open ones" list at the top of `plans/00-editor-checklist-human.md`.
- If the file already exists (a follow-up session), append to it or tick items off. Don't start a second file.
- Also repeat the items, briefly, under "Needs Lance in the Editor" in your session summary.

## Format (`plans/editor/04-fishing.md` is the template)

1. **Title + provenance:** `# Editor to-do: plan NN (<Topic>)`, then a line linking the plan, giving the date, saying whether Unity MCP was connected, and "Tick items off as you go and delete the file when it's empty." If something is blocking (e.g. "the pond has no exit until section 2"), say so in bold here.
2. **Sections in the order Lance should do them:** Verify first → wiring (assets → prefabs → scene) → UI review → decisions → playtest. Skip any that are empty.
3. **Each item** is a `- [ ]` with **what** (bold lead), **where** (exact asset/scene/object/field names), and **how to verify**. Add a **why** when it isn't obvious. Name the failure mode for anything easy to miss ("a missing event = silent no-hit swings").
   - Call out which refs auto-wire in `OnValidate` and which must be hand-assigned.
   - Mark items an agent could do with Unity MCP or an editor build script, so Lance can hand them back.
   - UI mockups: "Synty art, Texturina headers / Grenze body, gamepad focus" review item.
   - Decisions: give the options (a table for code-vs-spec choices).
4. **Playtest:** observable in-game behaviour, including negative cases (things that must *never* happen) and how to reach the state fast (DevConsole cheats, EnemyZoo).

## Rules

- Keep ids, paths and values **exact**: Lance executes this in the Editor without the code open.
- Keep it short. Findings, rationale and code notes belong in the plan file, not here.
