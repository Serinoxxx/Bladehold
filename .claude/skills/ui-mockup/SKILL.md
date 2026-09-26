---
name: ui-mockup
description: Use when building a new Bladehold UI screen, panel, HUD element or repeated widget (a mockup for Lance to sign off) — prefab-based and data-driven, built live via Unity MCP or a throwaway editor build script, Synty placeholder art, Texturina/Grenze, flagged for human UI review. For changing UI that already exists, use /modify-ui.
---

# Build a UI mockup

CLAUDE.md "UI work": agents may build a simple mockup; **Lance signs it off**. A mockup is authored prefabs plus the view script that fills them from data. It is never visuals built by runtime code.

## Rules

1. **Prefab-based and data-driven.** One prefab per repeated element (row, card, slot, node), populated at runtime from data by a view script. Never duplicate an element in the hierarchy and tweak the copies, and never hand-place N copies for N items.
2. **Built in the Editor, not at runtime.** Either live through Unity MCP, or with a **temporary** editor build script that is deleted once the prefabs exist. No `new GameObject`/`AddComponent<Image>` in runtime code, no `EnsureVisuals()`, no `LoadAssetAtPath`/`Resources.Load` fallbacks in the view script.
3. **Synty placeholder art:** `Assets/Synty/InterfaceFantasyWarriorHUD/` (sprites in `Sprites/HUD/`, ready-made widgets in `Prefabs/`: `Popups_Notifications`, `Player_Health_Equipment`, `ActionBar`, `Objectives_Story_Location`, …) and `Assets/Synty/InterfaceCore/Sprites/`. Don't edit Synty assets; make your own prefab (or a variant) under `Assets/Bladehold/Bladehold Prefabs/UI/`.
4. **House fonts:** Texturina for headers (`Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset`), Grenze for everything else (`Fonts/Grenze/Grenze-SemiBold SDF.asset`), both under `Assets/Synty/InterfaceFantasyWarriorHUD/`. Palette and sprite names: `/modify-ui` §5.
5. **Always flagged for human UI review** in the plan's `plans/editor/NN-<topic>.md` checklist (`/editor-wiring-todo`).

## Precedents to copy

- **`UI/ControlHintBar.cs` + `UI/HintEntryView.cs` + `Bladehold Prefabs/UI/Glyphs/HintEntry.prefab`**: the cleanest pattern. The container has a typed prefab field (`HintEntryView entryPrefab`), logs an error if it's missing, instantiates one row per data entry under a layout group, and calls `row.Bind(...)`.
- **`UI/RestArea/ShopUI.cs` + `ShopSlotUI.cs` + `ShopSlotPrefab.prefab`**: content from SO assets (`ShopItemSO` in `Bladehold Config/ShopItems/`), `slotUI.Setup(item, index, purchased, callback)`, and per-slot `MMF_Player`s for purchase/invalid feedback. Don't copy its legacy `transform.Find` fallback branch.
- `SettingsPanelView` + `RebindRow.prefab` (`RebindButtonView`) and `Campaign/CampaignMapUI.cs` + `CampaignNodeButton.prefab` follow the same idea. The draft cards (`SurvivorsCardSelectUI`, a fixed array of hand-placed `Card`s with `Find` auto-wiring) are the older style; don't copy that part.

## Recipe

1. **Data first.** Decide where the content comes from: existing SOs/CSVs, `RunSession`/`SaveSystem` state, or a new `*SO` (`[CreateAssetMenu]` under `Scriptable Objects/…`). Placeholder content goes in real data assets, not string literals in code. Player-facing text uses `Loc.Get(key, english)`.
2. **Element prefab** (e.g. `Bladehold Prefabs/UI/<Thing>Row.prefab`) with its view component: serialized refs to its `TMP_Text`/`Image`/`Button`/`MMF_Player`s and one `Setup(data, …)` / `Bind(…)` method. No logic beyond displaying data and forwarding clicks.
3. **Screen prefab**: `Canvas` (Screen Space Overlay) + `CanvasScaler` Scale With Screen Size at 1920×1080 (as `ShopUI`, `DeathScreen`, `PauseMenuCanvas`), a panel on a Synty parchment/box sprite, a layout group as the container for rows, a `MenuFocusController` on the panel root, and the controller script with `[SerializeField]` refs to the container and the element prefab. Leave the container empty; rows come from data at runtime.
4. **Controller script**: validate refs in `Start` (LogError + `anyError`), subscribe to data events in `OnEnable`, rebuild rows from data, set `MenuFocusController`'s default to the first row, unscaled time for anything animating while paused. Feedback via `MMF_Player`s (`/feel-integration`). `/compile-check`.
5. **Build it:**
   - **Via MCP** (preferred when connected, see `/unity-editor-mcp`): create objects and components, assign sprites/fonts by asset path, `SaveAsPrefabAsset` with the `out bool` overload and re-read it, and save any prefab stage before closing it.
   - **Via a throwaway builder**: `Assets/Bladehold/Bladehold Scripts/Editor/Mockup<Screen>Builder.cs` with a `[MenuItem("Bladehold/Mockups/<Screen>")]` that builds and saves the prefabs (register it in `Assembly-CSharp-Editor.csproj` for `/compile-check`), run it once (MCP `execute_menu_item`, or ask Lance), then **delete the script and its `.meta`**. Permanent scene builders (`BuildNecromancerCryptScene`) are a different thing; don't add mockups to them.
6. **Check in Play mode**: open the screen with real data (DevConsole for currencies/draft cards), with mouse and gamepad, at 16:9 and one other aspect. Read the console.

## Needs Lance in the Editor

In `plans/editor/NN-<topic>.md`, always add:
- **UI review: <screen>**: prefab path(s), how to open it in game, and what to judge: Synty art choice, Texturina headers / Grenze body, layout at 16:9 and ultrawide, gamepad focus order and B-to-close.
- Anything you couldn't author (final art, icons, animation, sound choices) marked **human intervention** or **agent mockup**.
