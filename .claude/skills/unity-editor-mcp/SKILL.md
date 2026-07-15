---
name: unity-editor-mcp
description: Use when a Bladehold task needs the live Unity Editor — creating SO assets, wiring prefab/scene references, running Bladehold menu items, reading the console, or Play-mode verification — via the unity-mcp (CoplayDev) MCP server instead of asking the user.
---

# Drive the Unity Editor via MCP

The `unityMCP` server (repo-root `.mcp.json`, HTTP on `localhost:8080/mcp`) bridges to the **running** Unity Editor through the `com.coplaydev.unity-mcp` package. It turns most of what used to be TODO.md "Editor wiring" into agent work: asset creation, component add/wire, scene edits, menu items, console reads, play-mode tests.

## Ground truth first

1. **The Editor must be open.** Every tool fails with a connection error otherwise. If calls fail, tell the user to open the project (and check `Window > MCP for Unity` shows "Connected") — don't retry blind.
2. **Discover the live tool surface before scripting a plan.** Tool names vary across unity-mcp versions (~48 tools: script/scene/GameObject/asset/prefab/material/console/menu/test groups). Load schemas via ToolSearch (`+unity` or `select:` the specific names) and trust what's actually registered, not this doc.
3. After any C# edit, the Editor recompiles on focus — **read the console** through MCP before doing anything that depends on the new code, and treat new compile errors as yours.

## Step 1 — Pick the right layer for the change

| Wanted change | Do it via |
|---|---|
| C# code | Normal file edits + `/compile-check` (don't use MCP script tools for bulk edits — file tools are faster and reviewable) |
| CSV config (`Config/*.csv`) | Normal file edits; Unity reimports on focus |
| SO asset instance, prefab component/ref wiring, scene object edits | MCP asset/GameObject/prefab tools |
| Enemy prefab variants | **Never by hand** — `/generate-enemy-prefabs` (menu `Bladehold > Generate Enemy Prefabs`, runnable via MCP's menu-item tool) |
| Balance projection | `/balance-sim` (menu `Bladehold > Balance Simulator`, or headless CLI) |
| Animator/clip work, baked animation events, art/audio | **Human** — record in TODO.md via `/editor-wiring-todo` |

## Step 2 — Editor-wiring session pattern

1. Read the relevant TODO.md checklist (or the feature's plan) for the exact assets/refs.
2. Create SO assets with the create-menu path the checklist names (`Scriptable Objects/FooSO`), set serialized values, save.
3. Wire refs: prefer objects whose `OnValidate` auto-wires siblings (the house convention) — only hand-assign what the checklist calls out as hand-assigned.
4. Save the scene/prefab explicitly; verify by reading the modified asset back (a re-query that shows the ref stuck).
5. Read the console — zero new errors/warnings from your changes.

## Step 3 — Play-mode verification

Enter Play mode via MCP, then drive the game with the **DevConsole** (backquote) cheats instead of playing manually: `DebugSetNextWave(n)`, `DebugSpawnBurst(count)`, `DebugWipeWave()`, class ◄/► picker + "Switch & Reload". The `EnemyZoo` gallery scene spawns every roster row with CSV overrides for enemy checks; `SkillTreePreview.unity` for tree/tooltip checks. Read the console during play for errors; exit Play mode when done.

## Pitfalls

- **Play-mode edits don't persist.** Anything wired while playing is lost on exit — always wire in edit mode, verify in play mode.
- **Domain reload wipes state**: after a script recompile mid-session, re-query objects rather than trusting stale ids/paths from before the reload.
- **Batchmode vs MCP are exclusive**: the headless CLI (`BatchBuild`, `BalanceSimCli`) needs the project *closed*; MCP needs it *open*. Don't queue both.
- Don't edit vendored assets (`Assets/Third Party/`, `Assets/Synty/`, `Assets/Feel/`, …) through MCP any more than through files.
- MCP writes to scenes/prefabs are real working-tree changes — review `git status` and include them in the commit like any other change.

## Finish protocol

Console clean → play-mode verification done (or the un-doable remainder recorded via `/editor-wiring-todo`) → commit to `main` and push, including the `.unity`/`.prefab`/`.asset`/`.meta` files MCP touched.
