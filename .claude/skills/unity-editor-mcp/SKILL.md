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

First check which side is broken. Read `mcpforunity://instances` (the MCP server answers even when Unity doesn't):
- **Tools fail with `no_unity_session` and `instance_count` is 0:** the server is up but no Editor is attached (step 3).
- **The tools themselves are gone or refuse to connect:** the server is down (step 3), then Lance runs `/mcp`.
- **The server is fine but the Editor doesn't answer:** step 1 or 2.

1. **Stuck behind a dialog** (timeouts like "ping not answered" while Unity is open). Check `%LOCALAPPDATA%\Unity\Editor\Editor.log` (tail it, grep `ShowModal|DisplayDialog`). Then run `pwsh -File .claude/skills/unity-editor-mcp/scripts/dismiss-unity-modals.ps1` to **list** native dialogs. `-Dismiss` presses Enter (the dialog's default button, e.g. "Save"), so only use it when that default is fine. Otherwise click a specific button via UI Automation (below), or tell Lance what's open. `(Get-Process Unity).MainWindowTitle` names the dialog at a glance.
2. **Hung Editor.** Signs: `Editor.log` stops growing for minutes, often right after a domain reload, with the last lines `[MODES] ModeService…` / `ScheduleIndexationOnStartup`. A reload after our script edits did this on 2026-10-02.
   - **Ask Lance before killing it** (unsaved scene/prefab work is lost).
   - **Kill** the Editor and its `AssetImportWorker*` children: `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"`, where the worker command lines contain `-batchMode … AssetImportWorker`.
   - **Relaunch:** `Start-Process "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -ArgumentList '-projectPath "C:\Users\lance\source\repos\My project"'`. Licensing works while Unity Hub is running.
   - **Watch out for a startup modal.** A force-kill usually leaves a **"Recovering Scene Backups"** dialog ("copy and preserve these backups in Assets/_Recovery/?"), which blocks loading at ~35 log lines.
     - Inspect `Temp/__Backupscenes/*.backup` (binary; `Library/LastSceneManagerSetup.txt` names the scene) and compare its time with the scene's last save/commit.
     - Copy it to the scratchpad, then answer **No** so nothing lands in `Assets/`.
     - Click a named button with UI Automation: `AutomationElement.FromHandle(MainWindowHandle)` → `FindFirst(Descendants, NameProperty "No")`. Unity's buttons are Panes with no InvokePattern, so read `BoundingRectangle` and click it with `SetCursorPos` + `mouse_event`.
   - A "No windows found in layout" exception plus a Synty Sidekick `ModularCharacterWindow.OnDestroy` NRE on that first load are harmless (the layout resets to default).
3. **No server, or a stale one.** The package launches the server **from the Editor** with `--unity-instance-token <token>` and a pidfile (`Library/MCPForUnity/RunState/mcp_http_8080.pid`).
   - **A server left over from a killed Editor** (its parent PID is gone) carries the old token, and the new Editor won't attach to it. Stop the whole tree (`Get-CimInstance Win32_Process | ? CommandLine -match 'mcp-for-unity'`: cmd → uvx → uv → mcp-for-unity.exe → python ×2).
   - **The Editor only starts a server on load when the EditorPref `MCPForUnity.AutoStartOnLoad` is on.** It was off until 2026-10-02 and is now on. Otherwise Lance clicks Start in `Window > MCP for Unity` (Ctrl+Shift+M).
   - **Headless EditorPrefs:** registry `HKCU:\Software\Unity Technologies\Unity Editor 5.x`, value name `<key>_h<hash>`. The hash is djb2-xor (`h=5381; h=(h*33)^ord(c)`, 32-bit), so `MCPForUnity.AutoStartOnLoad_h2539145689`, a DWORD (1 = true).
   - **Re-run the auto-start hook with a domain reload:** `pwsh -File .claude/skills/unity-editor-mcp/scripts/kick-unity-reload.ps1 -Reload`. It writes a throwaway editor script and bounces focus, because Unity only refreshes when its window *gains* focus, and a touched timestamp isn't a change.
     - Wait for `Session connected` in `Editor.log`, then run `-Cleanup` (one more reload) and check `git status` is clean of `McpKickTemp`.
     - Wait in an `until grep …; do sleep 3; done` loop, not chained sleeps.
4. **Verify** with `read_console` or `mcpforunity://instances`. Claude Code's client usually drops when the server restarts, so **Lance must run `/mcp` → reconnect UnityMCP**; an agent can't. Ask once, after the server is confirmed up, not before.

## Finish

Console clean → Play-mode check done (or the remainder recorded via `/editor-wiring-todo`) → `git status`: MCP writes to `.unity`/`.prefab`/`.asset`/`.meta` are real changes and go in the commit. Helper scripts and benchmark junk don't.
