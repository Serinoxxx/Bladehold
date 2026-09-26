---
name: modify-ui
description: Use when changing existing Bladehold UI (HUD, menus, pause/settings, death screen, shop, meta perks, campaign map, draft cards) — finding the prefab and script, how the UI gets its data, gamepad focus, paused-time behaviour, and editing it safely via Unity MCP. For building a new screen or element, use /ui-mockup.
---

# Modify existing UI

All runtime UI is **uGUI + TextMeshPro** (UI Toolkit appears only in editor tools such as `Editor/BalanceTree/`). Scripts are in `Assets/Bladehold/Bladehold Scripts/UI/` (plus `UI/Meta/`, `UI/RestArea/`, `UI/MainMenu/`, `UI/Transitions/`, `Campaign/CampaignMapUI.cs`), prefabs in `Assets/Bladehold/Bladehold Prefabs/UI/`.

## 1. Find the real thing

- **Script first:** grep the screen's name in `Bladehold Scripts/UI/`. Then find where the script lives: prefab (`Bladehold HUD`, `DeathScreen`, `PauseMenuCanvas`, `SettingsPanel`, `ShopUI`, `EventSystem`, repeated elements like `ShopSlotPrefab`, `PerkCardPrefab`, `Card`, `CampaignNodeButton`, `Glyphs/HintEntry`, `RebindRow`) or scene-only objects (Meta Area perk UI, campaign map).
- Grep a script's `.meta` guid across `*.prefab`/`*.unity` to find every user. Castle, crypt and sanctuary scenes are binary: open them via MCP (see the `binary-scene-guid-search` memory for headless greps).
- Scenes often hold their **own copy** of a HUD/manager (e.g. the Survivors scene). Check whether the object is a nested prefab instance or unpacked before editing; edit the prefab when you can so every scene gets the change.
- `Bladehold Prefabs/UI/Old HUD/[DEPRECATED] HUD Canvas.prefab`, `Card_Class`, `ClassPreview*` and `RageBarUI`/`MageElementUI` belong to removed systems: don't build on them.

## 2. How the UI gets its data

UI observes; it never owns game state.

- **Run state:** static `RunSession` events (`OnInRunGoldChanged`, `OnInRunSupplyChanged`, `OnAmmoChanged`, `OnGoblinBloodChanged`, …). Subscribe in `OnEnable` (unsubscribe first to avoid doubles), unsubscribe in `OnDisable`, and pull the current value once on enable (`UI/SupplyUI.cs`).
- **Permanent progress:** `SaveSystem.Load()` (cached `SaveData`).
- **Combat values:** `Health.OnHealthChanged` / `OnDied` on the relevant object (`PlayerHealthBarUI`), controller events (`PlayerUltimateController.OnChargeChanged`), or polling in `Update` for continuous values.
- **Scene singletons** (`Player.Instance`, `SurvivorsObjectiveManager.Instance`, …) are resolved in `Start`, not `Awake`. Remember `Player.Instance` is on the child `SidekickSyntyCharacter` (CLAUDE.md "Don't assume hierarchy").
- **Content** comes from SOs and CSVs (`ShopItemSO`, `MetaPerkDefinitionSO`, draft cards as `SkillNode`s), bound to one prefab per element with a `Setup`/`Bind`/`SetData` method.
- **Text:** player-facing strings go through `Loc.Get(key, englishFallback)` (`Assets/Bladehold/Resources/Localization/Strings.csv`), not hardcoded literals.
- Validate refs in `Start` (LogError + `anyError`); no `transform.Find` auto-wiring in new code (older cards/shop still have it as a fallback, don't extend it).

## 3. Gamepad and input

- `Bladehold Prefabs/UI/EventSystem.prefab` uses `InputSystemUIInputModule` (pad Navigate/Submit/Cancel).
- Every panel root gets a **`MenuFocusController`**: `defaultSelectable` (selected when a pad is active), optional `restrictTo` focus trap for modals, `onCancel` for pad B (needs a *public* method to wire as a persistent listener). After rebuilding rows, make sure the default is still valid.
- Scroll lists: add `ScrollRectAutoScroll` next to the `ScrollRect`. Check `Navigation` on new Selectables (Automatic usually works for grids; set Explicit for radial/odd layouts).
- Button prompts use `InputGlyph` / `ControlHintBar` + `HintEntryView` so glyphs follow the active device and rebinds; never hardcode "[E]".
- Modal screens call `CursorLockManager.SetUnlock("<Owner>", true/false)` and, if they pause, set `Time.timeScale` (see `ShopUI`).

## 4. Paused time

Anything animating on a paused screen must be unscaled: coroutines use `Time.unscaledDeltaTime` / `WaitForSecondsRealtime`, `MMF_Player`s force unscaled time (`/feel-integration`), `MMProgressBar` is unscaled by default (`/mm-progress-bars`).

## 5. Style (keep it consistent)

- Fonts (TMP, `Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/`): **Texturina** for headers (`Texturina/Texturina_18pt-SemiBold SDF.asset`, `… SDF Black Underlay.asset` over busy backgrounds), **Grenze** for all other text (`Grenze/Grenze-SemiBold SDF.asset`). Never leave LiberationSans.
- Art: Synty sprites from `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/` and `Assets/Synty/InterfaceCore/Sprites/` (e.g. `SPR_HUD_FantasyWarrior_Box_Medium_ParchmentGradient_01..05`, `…_Box_Large_Parchment_01`, `…_Box_Small_Parchment_01..03`, `…_Line_01`, `…_Line_04_Left/Right`); Kenney dividers in `Assets/Bladehold/Bladehold Images/UI/Kenney UI/Divider/`.
- Palette used so far: text on parchment `#63564B` (menu labels `#54483D`), text on dark `#D9D1BF`, gold/highlight `#FFD170` (costs `#FFA000`), damage red `#A45845`, parchment tint `#C5BEA8` / `#D4C6A3`, modal dimmer `#0D0D14EB`.
- Feedback (clicks, pops, sounds) is `MMF_Player`s on the prefab (`UIClickFeedback`, `ShopSlotUI.purchaseFeedback`), never `AudioSource` calls.

## 6. Editing via Unity MCP

Use `/unity-editor-mcp`. Edit in **Edit mode** only (Play-mode changes are lost).

- **Inspect first:** with `execute_code` (CodeDom, C# 6: no local functions, tuples or `?.`/`??` on Unity objects; use `System.Action` recursion and `== null`), dump names, `anchorMin/Max`, `pivot`, `anchoredPosition`, `sizeDelta` and components of the tree.
- **Prefabs:** `PrefabUtility.LoadPrefabContents` → edit → `SaveAsPrefabAsset(root, path, out bool ok)` → `UnloadPrefabContents` in `finally`; check `ok` (missing-script components make the save fail silently). If you open a prefab stage, **save it before closing** or the Editor blocks on a modal.
- **Scene objects:** set serialized fields through `SerializedObject`/`FindProperty` (works on private fields), `EditorSceneManager.MarkSceneDirty`, save the scene.
- **Anchors:** bottom/edge elements anchor to that edge, not the centre, or they fall off-screen on other aspect ratios. When re-anchoring, derive offsets from the current rect instead of guessing numbers.
- `AssetDatabase.LoadAssetAtPath` is fine here (editor code), never in runtime UI scripts.

## 7. Finish

- `/compile-check` if you touched C#. Read the console.
- UI changes need a human look: add a **UI review** item (Synty art, Texturina headers / Grenze body, gamepad focus, 16:9 and 16:10/ultrawide) to the plan's `plans/editor/` checklist via `/editor-wiring-todo`, plus anything you couldn't wire.
