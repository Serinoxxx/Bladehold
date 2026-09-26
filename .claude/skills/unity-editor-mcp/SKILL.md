---
name: unity-editor-mcp
description: Use when a Bladehold task needs the live Unity Editor (creating SO assets, wiring prefab/scene references, running Bladehold menu items, reading the console, Play-mode checks) through the UnityMCP (CoplayDev) server, or when that bridge is down, hung or stuck behind a modal dialog.
---

# Drive the Unity Editor via MCP

Tools are `mcp__UnityMCP__*` (`execute_code`, `manage_scene`, `manage_prefabs`, `manage_asset`, `manage_components`, `manage_editor`, `execute_menu_item`, `refresh_unity`, `read_console`, …). The server is an HTTP server at `http://127.0.0.1:8080/mcp`, registered in Lance's user-scope Claude config (the repo's `.mcp.json` is empty). The Unity package `com.coplaydev.unity-mcp` (tracks `#main`; version in `Library/PackageCache/com.coplaydev.unity-mcp@*/package.json`) launches that server from the **open** Editor.

## Ground truth first

1. **The Editor must be open.** If the first call fails, see "When the bridge is down". Don't retry blind.
2. **Load schemas before planning.** Tools are deferred: `ToolSearch` with `select:mcp__UnityMCP__<name>`. Trust the loaded schema over this doc (for example, `manage_editor` has no play-mode-state query; read the editor-state resource instead).
3. After any C# edit: `refresh_unity` (`compile: "request"`), then `read_console`. Treat new errors as yours. A domain reload invalidates earlier instance ids and wipes `execute_code` history, so re-query and re-send rather than `replay`.

## Pick the layer

| Change | Via |
|---|---|
| C#, CSV config | File edits + `/compile-check` (not MCP script tools) |
| Batch scene/prefab wiring, SO field values | `execute_code` (see techniques) |
| Single component or prefab edit | `manage_components` / `manage_prefabs` |
| Enemy prefab variants | **Never by hand.** `/generate-enemy-prefabs` (menu `Bladehold/Generate Enemy Prefabs`) |
| Animator/clip authoring, baked animation events, art/audio picks | **Human.** Record it in the plan's `plans/editor/` checklist (`/editor-wiring-todo`) |

## execute_code techniques

- **Workhorse:** `FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)`, then `new SerializedObject(c).FindProperty("field")` (reaches private `[SerializeField]`s), `ApplyModifiedProperties`, `EditorUtility.SetDirty`, then save the scene (`manage_scene` save, or `EditorSceneManager.SaveScene`). One call can wire a dozen components.
- **Prefab edits:** `PrefabUtility.LoadPrefabContents` → edit → `SaveAsPrefabAsset(root, path, out bool ok)` → `UnloadPrefabContents` in a `finally`. The save **fails silently** on prefabs holding missing-script components, so check `ok` and re-read the asset. To copy a configured component between prefabs use `ComponentUtility.CopyComponent`/`PasteComponentAsNew`, then re-point any refs still aimed at the source.
- **Never `??` or `?.` on Unity objects** (fake null). Use explicit `== null`.
- **Big jobs:** write a throwaway static helper class under `Assets/Bladehold/Bladehold Scripts/Editor/`, `refresh_unity`, call it from `execute_code` by name, and **delete it before committing**.
- **Button-driven editor windows:** call the window's private static method by reflection (`Type.GetType("X, Assembly-CSharp-Editor").GetMethod(..., NonPublic|Static)`). `execute_menu_item` on a menu that just opens a window does nothing useful.
- **Persistent UnityEvent listeners:** `UnityEditor.Events.UnityEventTools.AddVoidPersistentListener` (the target must be a public method).
- **MMF players from code:** a fresh `AddComponent<MMF_Player>()` has a null `FeedbacksList`, so new it first. To clone an authored feedback, use `JsonUtility.FromJson(JsonUtility.ToJson(f), f.GetType())` with a fresh `UniqueID`.
- **Layout-preserving anchor changes:** compute the current rect in parent space and derive the offsets. Don't trust guessed pixel numbers from a checklist.
- Blocked by safety checks: `AssetDatabase.DeleteAsset`, so `git rm` the asset + `.meta` instead.

## Traps that hang or pollute the Editor

- **Anything that pops a native dialog blocks the main thread** and looks like a hung reload. Always `save_prefab_stage` before `close_prefab_stage`, even after a read-only look (opening a stage can dirty it). To read or edit a prefab without a stage, use `manage_prefabs` `get_hierarchy`/`modify_contents` or `LoadPrefabContents`. Don't run menu items that call `EditorUtility.DisplayDialog`; call their helpers by reflection instead.
- **Play-mode edits don't persist.** Wire in edit mode, verify in Play mode.
- **The benchmark leaves junk in the open scene** (`Benchmark_*`, `Test*`, `SupplyWagon(Clone)`). Run `Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark` from `MainMenu`, then reopen the scene with `OpenScene(path, OpenSceneMode.Single)` to discard. **Never save a scene the benchmark ran in.** A huge scene diff: grep the added `m_Name:` lines for `Benchmark_`/`Test`.
- **Binary scenes** (castle, crypt, sanctuary) stay binary even after a save. Verify their wiring by opening them via MCP. Headless "is X referenced?" check: GUIDs are stored as 16 raw bytes, nibble-swapped per byte (`5e9b…` → `e5 b9 …`): `bytes(int(g[i+1]+g[i],16) for i in range(0,32,2))`.
- `BatchBuild` (`Editor/BatchBuild.cs`, batchmode) needs the project **closed**, and MCP needs it **open**. Don't queue both.
- Don't edit vendored folders (`Assets/Third Party/` incl. Feel, LeanTween and DamageNumbersPro, `Assets/Synty/`, `Assets/AssetInventory/`).

## Play-mode verification

`manage_editor` `play`, then drive the game with the **DevConsole** (backquote) instead of playing by hand: the wave panel's Wipe Wave, enemy spawn picker and +N bursts, threat override, objective select, draft cards, currencies, scene loads. Use the `Enemy Zoo` scene for enemy checks. Read the console while playing, and `stop` when done.

## When the bridge is down

1. **Stuck behind a dialog** (timeouts like "ping not answered" while Unity is open): check `%LOCALAPPDATA%\Unity\Editor\Editor.log` (tail it, grep `ShowModal|DisplayDialog`), then run `pwsh -File .claude/skills/unity-editor-mcp/scripts/dismiss-unity-modals.ps1` to **list** native dialogs. `-Dismiss` presses Enter (the dialog's default button, e.g. "Save"), so only use it when that default is fine. Otherwise tell Lance what's open.
2. **Server dead:** the log is at `Library/MCPForUnity/Logs/server-launch-8080.log`. Preferred fix: Lance opens `Window > MCP for Unity` (Ctrl+Shift+M) and starts the server, since the package launches it with its pidfile and instance token. Headless: capture the running server's command line (`Get-CimInstance Win32_Process | ? CommandLine -match 'mcp-for-unity'`), stop those processes, then relaunch that same `uvx.exe --from "mcpforunityserver==<package version>" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 …` with `Start-Process -WindowStyle Hidden`.
3. Verify with `read_console`. If Claude Code's MCP client still shows it disconnected, Lance reconnects it via `/mcp`.

## Finish

Console clean → Play-mode check done (or the remainder recorded via `/editor-wiring-todo`) → `git status`: MCP writes to `.unity`/`.prefab`/`.asset`/`.meta` are real changes and go in the commit. Helper scripts and benchmark junk don't.
