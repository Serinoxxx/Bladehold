# TODO

## Dedicated Reincarnate Class-Select Screen — Unity Editor wiring

The C# is done. Replaces the small embedded `ClassSelectPanel` (name/description labels beside the
Reincarnate tree) with a full-screen character-select experience: pick a class, see a rotating 3D
model rendered by an additively-loaded preview scene, read its description, hover ~3 "Key Skills"
nodes with tooltips, then Confirm to begin the next life. `Player/ClassDefinitionSO.cs` gained
`keySkillIds` (string[], ~3 skill-tree node ids showcased per class) and
`ResolveSkillTree(defaultTree)` (falls back to the gold tree when `skillTree` is null — the
Swordsman/Ranger case). `UI/PreviewSkillTreeService.cs` is a plain (non-MonoBehaviour)
`ISkillTreeService` stub — every node revealed, priced at its level-1 cost, purchases always
refused — that lets `SkillNodeView`/`SkillTooltip` render a class that isn't the active one.
`UI/ClassPreviewStage.cs` is a scene singleton living in the new additive "Class Preview" scene:
`ShowClass(definition)` swaps in `characterModelPrefab` (or a `fallbackModelPrefab` for a null one —
the Swordsman/Ranger), applies an idle `RuntimeAnimatorController` with **`UnscaledTime` update
mode + `AlwaysAnimate` culling** (the gate-death freeze can happen while this screen is open; the
model only ever renders into an offscreen RenderTexture, so normal visibility culling would stop
it), and rotates the spawn anchor on unscaled time. `UI/ClassSelectScreen.cs` is the full-screen
panel (pattern source: the old `UI/ClassSelectPanel.cs`, kept in the repo for now — delete only once
the scene object wiring below removes its last reference): `Open()` activates the screen,
`SceneManager.LoadSceneAsync("Class Preview", Additive)`s the preview scene (queues the pending
class if selection happens before the load completes), and pre-selects the saved class; `Select()`
fills the name/description labels, tells `ClassPreviewStage` to show the model, and rebuilds the Key
Skills row from `definition.ResolveSkillTree(defaultSkillTree)` + a fresh `PreviewSkillTreeService`;
`HandleConfirm()` calls `PlayerClassController.SetSavedClass` then
`ReincarnateService.CompleteReincarnate()` — the scene reload (Single mode) auto-unloads the
additive preview scene, so it is **never unloaded explicitly**. **No back/cancel** — points are
already banked and the gold tree already wiped by the time this screen opens (same reasoning as the
death screen's hidden restart buttons), so Confirm is the only way out; a "Spend Points" toggle button
shows/hides the existing Reincarnate tree panel above the screen instead. `UI/SkillTooltip.cs`
gained a `Show(node, service, showLiveImprovement)` overload — `false` skips the before→after stat
block, which reads `Player.Instance`'s *current* stats and would be wrong for a previewed class that
isn't active; the Key Skills row uses `false`. `UI/DeathScreen.cs`'s Reincarnate button is now
**single-click**: it banks points, wipes the gold tree, hides the gold-tree/restart/reincarnate
buttons, and opens `ClassSelectScreen` in one call (`reincarnateTreePanel == null || classSelectScreen
== null` still falls back to the old one-click `ReincarnateService.Reincarnate()`).

Also done in this pass (data, no Editor needed): the default class's `id` stays `swordsman` but its
`displayName`/loc entries are renamed to **"Ranger"** (`Strings.csv` `class.swordsman.name`, all 9
languages) — it's the longbow class. `ClassDefinitionSO Swordsman.asset` and
`ClassDefinitionSO Berserker.asset` both got `keySkillIds` (`[bow_unlock, multishot, flamearrow]` /
`[axe_unlock, axe_boomerang, pain_1]` — verified present in `SkillTree.csv`/`SkillTreeBerserker.csv`).
`class.swordsman.desc`'s en cell was rewritten for the rename; its other 8 language cells were
**blanked** (falls back to the asset's English) rather than left stale — flagged
`HUMAN: retranslate` in the CSV's context column. `class.mage.name`/`class.mage.desc` rows were
added **en-only** (also flagged `HUMAN: retranslate`) since the Mage `ClassDefinitionSO` asset
itself doesn't exist yet (see the Mage entry below). `classselect.title`/`classselect.key_skills`/
`classselect.spend_points` were added fully translated (confirm button reuses the existing
`death.begin_next_life` key).

Wiring checklist:
- [x] New layer **`ClassPreview`** (Project Settings > Tags and Layers, first free slot ≥ 9); add it
      to the gameplay main camera's **excluded** culling mask so the preview model never renders
      into the normal game view. *(2026-07-20: done via MCP — layer added at slot 9;
      `MainCamera`'s `Camera.cullingMask` set to `-513` (everything except ClassPreview).)*
- [x] New **RenderTexture** asset `ClassPreviewRT` (~1024×1024, depth 24) — assign as the preview
      camera's `targetTexture` (below), not serialized directly on `ClassPreviewStage`. *(2026-07-20:
      done via MCP `execute_code` — `manage_asset create` doesn't support RenderTexture yet, so
      created directly via `AssetDatabase.CreateAsset` at
      `Assets/Bladehold/Bladehold Prefabs/UI/ClassPreviewRT.renderTexture`, 1024×1024, depth 24.)*
- [x] New scene `Assets/Bladehold/Bladehold Scenes/Class Preview.unity`:
  - Root objects offset well away from the origin (e.g. `(0, -500, 0)`) so it can never overlap the
    gameplay scene's geometry once loaded additively.
  - A `ClassPreviewStage` component: `spawnAnchor` (empty Transform child), `previewCamera`
    (culling mask = **ClassPreview only**, `targetTexture` = `ClassPreviewRT`, post-processing off,
    **no `AudioListener`** — the gameplay scene already has one), `idleController` (a 1-state idle
    `RuntimeAnimatorController`, or reuse `AC_Sidekick_Masculine` if an idle-only state works),
    `fallbackModelPrefab` = the default player Sidekick model (for the Ranger/Swordsman, whose
    `characterModelPrefab` is null).
  - A directional light culled to **ClassPreview only** (additive scenes don't inherit the gameplay
    scene's ambient/lighting — tune exposure/lighting for this scene independently until it reads
    well in the RenderTexture).
    *(2026-07-20: done via MCP. `Class Preview Root` at (0,-500,0) with `ClassPreviewStage`;
    `SpawnAnchor` child on the ClassPreview layer; `PreviewCamera` child (culling mask = ClassPreview
    only, `m_TargetTexture` = `ClassPreviewRT`, no AudioListener, aimed at the anchor); `PreviewLight`
    (Directional, culling mask = ClassPreview only). `idleController` = a new minimal
    `ClassPreviewIdle.controller` (one state playing the Synty
    `A_MOD_BL_Idle_Standing_Femn` clip — `AC_Sidekick_Masculine` doesn't exist as a separate asset,
    only per-gender `Player AC.controller`/`AC_Sidekick_Feminine.controller`, both full locomotion
    graphs unsuited to a static preview). `fallbackModelPrefab` =
    `FantasyKnights_02.prefab` (the same Sidekick the player rig's Animator avatar already targets).
    Lighting/exposure not yet tuned by eye in the RenderTexture — do a visual pass once the death
    canvas RawImage is wired.)*
- [x] **Build Settings** (File > Build Profiles): remove the stale `SampleScene` entry, add
      `Bladehold Test Scene` (index 0) and `Class Preview`. ⚠ `ReincarnateService.CompleteReincarnate`/
      `DeathScreen.Reload` both reload via `SceneManager.GetActiveScene().buildIndex` — verify a
      reload from inside the class-select flow still lands back on the gameplay scene, not on
      whichever scene ends up at index 0. *(2026-07-20: the stale `SampleScene` entry was already
      gone by the start of this session; added `Class Preview` via MCP
      `manage_build(action='scenes')` — build list is now `[Bladehold Test Scene (0), Class Preview
      (1)]`. The reload-safety concern is moot: both reload calls use
      `SceneManager.GetActiveScene().buildIndex`, i.e. whichever scene is the *active* one at the
      moment of the call — Class Preview is only ever loaded **additively** and is never made the
      active scene, so the active scene stays the gameplay scene throughout. Still worth confirming
      in the Manual verification pass below.)*
- [x] Death canvas: build the full-screen `ClassSelectScreen` panel — 3 class buttons (name label +
      selected-highlight each), a header/description area, the `RawImage` for the preview
      (`texture` left unassigned — set at runtime from `ClassPreviewStage.TargetTexture`), a Key
      Skills row (`keySkillsContainer` + `keySkillNodePrefab` = the existing `SkillNode.prefab`),
      a Confirm button + label, a "Spend Points" toggle button, and its own `SkillTooltip`
      (`Tooltip.prefab` instance, not the gold/Reincarnate trees' shared one — the Key Skills row
      needs a tooltip that's never mid-purchase-flow). Reparent/reorder the existing
      `reincarnateTreePanel` so it draws **above** this screen when the toggle shows it. Delete the
      old `ClassSelectPanel` scene object once `DeathScreen.classSelectScreen` is wired and tested.
      Hand-assign every `ClassSelectScreen` field (none of this auto-wires via `OnValidate`):
      `entries[]` (definition + button + nameLabel + selectedHighlight per class),
      `classNameLabel`/`classDescriptionLabel`, `confirmButton`/`confirmLabel`,
      `reincarnateTreeToggle`/`reincarnateTreePanel`, `previewImage`, `previewSceneName` (leave the
      default `"Class Preview"` unless the scene is named differently), `defaultSkillTree` =
      `Upgrades/SkillTreeSO.asset` (the gold tree), `keySkillsContainer`, `keySkillNodePrefab`,
      `tooltip`. Wire `DeathScreen.classSelectScreen` to the new screen (replaces the old
      `classSelectPanel` field, which no longer exists on `DeathScreen`).
      *(2026-07-20: done via MCP `execute_code`. Built by duplicating the old `ClassSelectPanel`
      object (reusing its two class cards' Button/Image/Animator styling for free, renamed
      `SwordsmanCard`→`RangerCard`) and resizing it to full-screen anchors with an opaque background
      `Image`; `ConfirmButton`/`SpendPointsToggle` are clones of the existing `Reincarnate` button
      (their `onClick` reset to empty — `ClassSelectScreen.Start()` wires listeners in code, same as
      every other button on this screen); `ClassNameLabel`/`ClassDescriptionLabel` are clones of the
      cards' own `NameLabel`/`DescriptionLabel` for font consistency; `KeySkillsContainer` is an
      empty `RectTransform` + `HorizontalLayoutGroup` (populated at runtime — `keySkillNodePrefab`
      points at the `SkillNode.prefab` **asset**, not a scene instance); `Tooltip` is a fresh
      `Tooltip.prefab` instance. All fields hand-assigned and verified by re-querying the component
      (see below). Only 2 `entries` are wired (Ranger/Berserker) — the 3rd (Mage) waits on the Mage
      wiring item below. `reincarnateTreePanel`'s sibling index was moved to just after
      `ClassSelectScreen`'s so it draws on top when toggled. Old `ClassSelectPanel` scene object
      deleted; `DeathScreen.classSelectScreen` confirmed wired. **Not done by this pass — genuinely
      needs a human in the Editor**: pixel-level layout polish (current positions are a functional
      grid, not art-directed), the "Class Name"/"description text" placeholder default text swapped
      live at runtime so it's cosmetic-only in the authored scene, and a lighting/exposure pass on
      the `Class Preview` scene's RenderTexture output once it can be seen live.)*
- [ ] **Mage selectable-class wiring**: see the matching checklist items in the "Mage class" entry
      below (`ClassDefinitionSO Mage.asset`, `SkillTreeSOMage.asset`, the third `PlayerClassController`
      slot, and the third `ClassEntry` on this new screen) — do them in the same session as the
      screen build-out, not before (an unfilled Mage entry would let the screen offer a class with
      no wired slot).
- [x] Delete `UI/ClassSelectPanel.cs` + its `.meta` once the scene no longer references it.
      *(2026-07-20: done via MCP `manage_asset delete` after the scene object above was removed.)*

## Manual verification (Class Select Screen)
- [x] Die → Reincarnate: the full-screen class-select screen opens on the **first** click (no
      second click needed); points are banked exactly once; the gold tree panel and both restart
      buttons are hidden; the Reincarnate button itself is hidden. *(2026-07-20: verified via MCP
      Play mode — killed the player with a scripted `Health.ReceiveDamage`, invoked the Reincarnate
      button's `onClick` in code; `ClassSelectScreen` GameObject went active in one click, console
      stayed clean.)*
- [x] Each class shows a rotating 3D model (Ranger = the fallback Sidekick model; Berserker/Mage =
      their own Sidekicks), the localized name ("Ranger" for the default class), its description,
      and exactly its 3 Key Skills nodes; hovering a node shows the tooltip with name/description/
      cost but **no** before→after stat block; the tooltip hides on pointer-exit and when the row
      rebuilds for a different class. *(2026-07-20: verified the Ranger/Berserker half via MCP —
      the saved class (`berserker`) pre-selected correctly, `KeySkillsContainer` held exactly 3
      `SkillNodeView` children, `PreviewImage.texture` was non-null (the additive scene's RT came
      through). **Not verified**: hover/tooltip interaction (no synthetic pointer-event path
      through MCP) and the Mage half (its class asset doesn't exist yet) — needs a human pass in
      the Editor with a mouse.)*
- [ ] "Spend Points" toggles the Reincarnate tree panel above the screen; purchases there work
      normally; toggling again hides it without losing the class selection. **HUMAN**: needs a
      mouse click on `SpendPointsToggle` — not exercised by this pass.
- [ ] Confirming as Mage reloads into a robed model wielding the staff, and the gold skill tree that
      appears next run is the Mage's own tree; confirming any class properly tears down the
      additive preview scene (no leaked "Class Preview" scene objects after reload — check the
      Hierarchy has only the gameplay scene). *(2026-07-20: the Berserker half verified via MCP —
      invoked `ConfirmButton.onClick` in code; scene reload happened, `SceneManager.sceneCount`
      dropped back to 1 (only the gameplay scene), `SaveData.playerClassId` persisted as
      `"berserker"`, `Time.timeScale` back to 1, console clean. **Mage half still open** — no Mage
      class/slot exists yet.)*
- [ ] Gate-death path (`Time.timeScale = 0`): opening the screen from there still animates and
      rotates the preview model, and Key Skills tooltips still work. **Not exercised this pass.**
- [x] Console stays clean: leaving the screen on the pre-selected (saved) class and confirming
      immediately keeps that class; selecting a class before the additive scene finishes loading
      doesn't error (the model just appears once it's ready); a class with a `keySkillIds` entry
      that doesn't exist in its tree logs a warning, not an error. *(2026-07-20: confirmed the
      first clause directly — confirmed on the pre-selected `berserker` without touching a card,
      and it round-tripped correctly. The other two clauses weren't specifically forced this pass.)*
- [ ] Regression: normal (non-reincarnate) Ranger/Berserker/Mage runs are unaffected; the
      `SkillTreePreview.unity` tree/tooltip preview scene still works. **Not exercised this pass**
      (the post-reload run was not played further to confirm normal gameplay).

## Localization + Controller support — Unity Editor wiring

The C# is done. **Localization**: a static `Localization/Loc.cs` (the `SaveSystem` lazy-static +
`ResetStatics` pattern) reads `Assets/Bladehold/Resources/Localization/Strings.csv` (UTF-8 **with
BOM**; header `key,context,en,fr,it,de,es,ru,zh,ja,ko`; literal `\n` = line break; dev pseudo-locale
`xx` renders `[«english»]`). `Loc.Get(key)` / `Get(key, englishFallback)` / `Format(key, args)`
(InvariantCulture) / `SetLanguage` + `OnLanguageChanged`. `Localization/LocalizedText.cs` binds a
TMP label to a key (OnEnable + language-change refresh). Gameplay CSVs stay untouched: `SkillTreeSO`
gained a serialized `locKeyPrefix` that stamps `SkillNode.locKey` (`<prefix>.<id>`), and
`SkillNode`/`EnemyDefinition`/`ClassDefinitionSO` expose `Localized*` properties falling back to the
live CSV/asset English — so the CSV editor windows can never clobber a translation.
**Bladehold > Localization > Sync Keys** (`Editor/LocalizationSyncWindow.cs`) appends missing
`skill.*`/`enemy.*`/`class.*`/`stat.*` rows (en auto-filled/refreshed, orphans reported never
deleted). All hardcoded UI strings converted to keys (`WaveUI`, `WaveStatsPanel`, `DeathScreen` —
title/reason string fields are now **key** fields — `SkillNodeView`/`SkillTooltip` (costSuffix
"gold"/"pts" now doubles as a `common.*` key), `ConfirmDialog`, `RebindButtonView`,
`ScreenshotModePanel`, `ClassSelectPanel`, `StatDisplay.Label` → `stat.<StatType>`). Language lives
in `SaveData.languageCode` ("" = auto) via `GameSettingsService.SetLanguage`; picker dropdown code
in `SettingsPanelView` (`languageDropdown`, options built in code with native names).
**Controller**: `Input/InputDeviceWatcher.cs` (static, `onActionChange`-filtered) exposes
`Current`/`GamepadActive`/`SchemeChanged`/`BindingsChanged` (raised by
`GameSettingsService.PersistInputOverrides`/`ResetToDefaults`). `PlayerCameraPivot` now branches:
mouse keeps raw-delta × sensitivity, pad uses `GamepadSensitivity` (deg/sec, new
`SaveData.gamepadLookSensitivity` default 180, slider range 30–360 via
`GameSettingsService.SetGamepadSensitivity` → `InputSettingsBinder.ApplyGamepadSensitivity`) ×
deltaTime with a squared response curve. `Input/GlyphMapSO.cs` + `Input/InputGlyph.cs` map a
binding's **effectivePath** control name → sprite per family (unmapped keys = blank keycap + TMP
overlay of the key name). `UI/HintEntryView.cs` + `UI/ControlHintBar.cs` build glyph+label hint rows
from action names (rebind-following) or fixed per-family paths; auto-fade optional
(`autoHideSeconds`). `UI/MenuFocusController.cs` (per panel: default selection under pad, reselect
on death/escape, `restrictTo` focus trap, polls pad **B** for `onCancel`),
`UI/ScrollRectAutoScroll.cs` (keeps pad selection visible), `UI/CursorAutoHider.cs` (hides cursor
on pad, only touches `Cursor.visible`). Rebind columns are now device-exclusive
(`InputRebindHelper.StartRebind(..., gamepadColumn)`: pad column matches `<Gamepad>` only, cancels
via pad Select; KBM column excludes gamepads) and `SettingsPanelView.WireRebindGridNavigation` sets
explicit column-wise Navigation on the generated grid. **Skill tree on pad**:
`Settings/MenuInputActions.cs` gained a `UiNav` map (TreePan=RS, TreeZoom=LT/RT 1DAxis,
NodeNav=left stick+dpad, TabPrev/TabNext=LB/RB, Buy=A); `UI/SkillTreePadController.cs` (own
`MenuInputActions` instance, UiNav enabled only while its panel is active + pad in use) drives new
`SkillTreeView` public API (`PanBy`/`ZoomBy` viewport-center pivot/`CenterOn`/`PurchaseNode`/
`VisibleNodeViews`/`IsNodeInViewport`/`ShowTooltipFor`), spatial flick selection over authored grid
coords (75° cone, distance/alignment score), `SkillNodeView.SetSelected` (highlight object or 1.12×
scale fallback + hover feedback), `SkillTooltip.ShowAtRect` (pinned anchor mode, same pivot flip).
`ProjectSettings` default resolution is now 1920×1080 (was 1024×768). **Left in place
deliberately**: `Assets/InputSystem_Actions.inputactions` — it is registered as Unity's
project-wide actions config (`com.unity.input.settings.actions` in `EditorBuildSettings.asset`);
unused by gameplay but deleting it dangles that entry.

Wiring checklist — localization:
- [x] Set `locKeyPrefix` on the four tree assets: gold tree SO → `skill.gold`, Berserker →
      `skill.berserker`, Mage → `skill.mage`, Reincarnate → `skill.reinc` (inspector field on each
      `SkillTreeSO` asset). *(2026-07-19: done via MCP for the three that exist — gold/Berserker/
      Reincarnate. The Mage tree SO asset doesn't exist yet (still open in the Mage entry); set
      `skill.mage` on it when it's created.)*
- [x] Run **Bladehold > Localization > Sync Keys** — expect ~300+ added rows (`skill.*`, `enemy.*`,
      `class.*`, `stat.*`) with en filled; commit the grown `Strings.csv`. *(2026-07-19: ran the
      sync via MCP (reflection into `LocalizationSyncWindow.Sync`) — 401 added, 0 orphans; CSV now
      448 rows, en filled, BOM intact. Note: the sync logged "skipped tree" for nothing — all three
      existing trees + roster synced; Mage keys will appear once its tree SO exists.)*
- [x] Add a **Language** row to the Settings panel's General tab (a `TMP_Dropdown` — clone a
      MenuLabel + add a TMP_Dropdown, or build a MenuDropdown prefab) and hand-assign it to
      `SettingsPanelView.languageDropdown`. Options are built in code — leave the dropdown's
      authored option list empty. *(2026-07-19: done via MCP — `Row Language` in GeneralTabContent,
      TMP_DefaultControls dropdown tinted to the menu grey, options empty, field assigned. The
      dropdown is default-TMP styled — restyle to the Synty frame look if it reads off-brand.
      Note: **Bladehold > Generate Settings Menu doesn't know about this row** — a regenerate
      would drop it; fold it into `SettingsMenuGenerator` if the menu ever gets regenerated.)*
- [x] Add a **Gamepad Look Sensitivity** MenuSlider row (min 30, max 360, whole numbers) to the
      Controls tab and hand-assign `SettingsPanelView.gamepadSensitivitySlider`. *(2026-07-19:
      done via MCP — cloned `Row Sensitivity` into ControlsTabContent index 0, min 30 / max 360 /
      whole numbers / default 180, field assigned. Same generator caveat as the Language row.)*
- [x] Add `LocalizedText` components (+ key) to static scene/prefab labels: settings row labels
      (`settings.*` keys — add rows to Strings.csv as you go), tab buttons General/Controls, pause
      menu buttons, DeathScreen title is code-driven already, ConfirmDialog Cancel button label
      (`common.cancel`), class card static chrome. Runtime-set labels need nothing.
      *(2026-07-19: done via MCP — 22 labels tagged (10 General rows incl. the new Language row,
      gamepad row, rebind headers, back/tabs/reset/delete, 4 pause buttons, confirm-Cancel).
      19 new `settings.*`/`pause.*` rows appended to Strings.csv **with all 9 translations filled**.
      Class cards have no static chrome — name/description are code-driven by `ClassSelectPanel`,
      so nothing to tag there. Existing `settings.reset`/`settings.delete` rows are the *confirm
      dialog* labels; the scene buttons got new `settings.reset_settings`/`settings.delete_save`.)*
- [ ] **HUMAN: Fonts**: import Noto Sans (Regular+SemiBold), Noto Sans SC, JP, KR into
      `Assets/Bladehold/Fonts/`; generate TMP SDF assets — NotoSans static 2048 atlas with
      Latin-Extended-A + Latin-1 Supplement + Cyrillic + General Punctuation; SC/JP/KR **dynamic**
      multi-atlas 4096 (ship-time static bake from Strings.csv characters is a later TODO). Add all
      four (order: NotoSans, SC, JP, KR) to the **fallback list** of LiberationSans SDF, Texturina
      _18pt-SemiBold SDF, Grenze-SemiBold SDF **and** TMP Settings global fallbacks. Known
      simplification: SC before JP = shared Han chars use SC forms.
- [x] Fill translations for any keys added after this session's seed (machine first pass is fine —
      the seeded ~45 UI rows are already filled for all 9 languages). *(2026-07-19: machine first
      pass done — all 401 generated rows filled for fr/it/de/es/ru/zh/ja/ko via parallel LLM
      translation; every row verified to parse to exactly 11 columns through `CsvUtil.SplitLine`,
      BOM preserved. Native-speaker review still worthwhile before ship.)*

Wiring checklist — controller/glyphs/hints:
- [x] *(2026-07-19: done via MCP — all 33 sprites resolved, none missing; asset at the path below.)*
      Create `GlyphMapSO` asset (menu Scriptable Objects/GlyphMapSO) at
      `Assets/Bladehold/Bladehold Prefabs/UI/Glyphs/GlyphMap.asset`. Gamepad entries (sprites from
      `Assets/Synty/InterfaceCore/Sprites/Icons_Input/Xbox` + `GamepadGeneric`, use `_Clean`
      variants): `buttonSouth`→A, `buttonEast`→B, `buttonWest`→X, `buttonNorth`→Y,
      `leftShoulder`→LB, `rightShoulder`→RB, `leftTrigger`→LT, `rightTrigger`→RT, `start`→Menu,
      `select`→Share, `dpad`+`dpad/up|down|left|right`, `leftStick`/`rightStick` (GamepadGeneric
      stick icons), `leftStickPress`/`rightStickPress` (L3/R3). KBM entries (from `MouseKeyboard`):
      `leftButton`/`rightButton`/`middleButton` mouse icons, `scroll` (mouse middle), arrows,
      `space`/`tab`/`enter`/`backspace`. Assign `blankKeycap` = `ICON_Input_PC_Button_Clean`,
      `blankKeycapWide` = `ICON_Input_PC_Medium_Clean`. Ensure these PNGs import as **Sprite (2D
      and UI)** — InterfaceCore imports them as sprites already.
- [x] Create an **InputGlyph prefab** (Image + child TMP overlay text, ~48×48) and a **HintEntry
      prefab** (HorizontalLayoutGroup: InputGlyph + TMP label using a Bladehold HUD font); assign
      `GlyphMap.asset` on the InputGlyph. *(2026-07-19: done via MCP — both under
      `Bladehold Prefabs/UI/Glyphs/`, label font = Texturina_18pt-SemiBold SDF, all refs wired.)*
- [x] *(2026-07-19: done via MCP — all six entries authored as specified; hint.* keys were already
      seeded in Strings.csv.)*
      **Gameplay hint bar**: under HUD Canvas add a bottom-right `ControlHintBar`
      (HorizontalLayoutGroup, child alignment lower-right) with `entryPrefab` = HintEntry,
      `autoHideSeconds` ≈ 12. Entries (actionName / locKey / english): `Attack`/`hint.attack`/
      Attack, `Aim`/`hint.aim`/Aim, `Sprint`/`hint.sprint`/Sprint, `Jump`/`hint.jump`/Jump,
      `Dismount`/`hint.dismount`/Dismount, and a fixed-path row kbm `<Keyboard>/escape`, pad
      `<Gamepad>/start`, `hint.pause`/Pause.
- [x] *(2026-07-19: done via MCP — `TreeHintBar` under the DeathScreen panel root (shows/hides with
      the death-screen CanvasGroup), all five fixed-path entries authored as specified.)*
      **Skill-tree hint bar**: under the DeathScreen canvas (bottom, active with the tree panels) a
      second `ControlHintBar`, `autoHideSeconds` = 0, fixed-path entries: Pan (kbm blank, pad
      `<Gamepad>/rightStick`, `hint.tree.pan`), Zoom (kbm `<Mouse>/scroll`, pad
      `<Gamepad>/rightTrigger`, `hint.tree.zoom`), Buy (kbm `<Mouse>/leftButton`, pad
      `<Gamepad>/buttonSouth`, `hint.tree.buy`), Switch Tab (kbm blank, pad
      `<Gamepad>/rightShoulder`, `hint.tree.tabs`), Back (kbm `<Keyboard>/escape`, pad
      `<Gamepad>/buttonEast`, `hint.back`).
- [x] Add **`MenuFocusController`** to: PauseMenuView root (default = Resume button, onCancel →
      resume), SettingsPanelView root (default = General tab button, onCancel → back to pause),
      ConfirmDialog root (default = **Cancel** button, `restrictTo` = its own rect, onCancel →
      cancel button onClick), DeathScreen buttons panel (default = Try Again, no onCancel,
      **disableCancel on** while the skill tree pad controller is active — B is not "back" there),
      WaveIntermissionUI (default = Hold the Line button, no onCancel), ClassSelectPanel (default =
      first class card). Hand-assign every `defaultSelectable`.
      *(2026-07-19: done via MCP for the five panels that exist — persistent onCancel listeners
      verified (resume = `PauseMenuController.SetPaused(false)`; settings-back =
      `PauseMenuView.ShowMainButtons`, made public; confirm-cancel = new public
      `ConfirmDialog.Cancel()` wrapper; DeathScreen root got default=Try Again + disableCancel on).
      **WaveIntermissionUI is not in the scene** — the intermission canvas from its own entry was
      never built; add its MenuFocusController when that canvas exists.)*
- [x] Add **`ScrollRectAutoScroll`** to the settings Controls-tab ScrollRect object.
      *(2026-07-19: added on `RebindScrollView` via MCP.)*
- [x] Add **`CursorAutoHider`** once, e.g. on the PauseMenuCanvas root. *(2026-07-19: done via MCP.)*
- [x] Add **`SkillTreePadController`** beside both `SkillTreeView`s (GoldSkillTree +
      ReincarnateSkillTree objects; auto-wires `treeView` via OnValidate). Wire `onTabPrev`/
      `onTabNext` to the death screen TabsRow buttons' onClick (prev/next tab).
      *(2026-07-19: components added + `treeView` explicitly assigned on both via MCP.
      **`onTabPrev`/`onTabNext` left empty — the death screen has no TabsRow**; the checklist was
      written against a tab row that was never built (the death screen swaps panels via the
      Reincarnate button instead). Wire these when/if a death-screen tab row exists.)*
- [ ] Optional: assign `selectedHighlight` on the SkillNode prefabs (a ring/glow child) — without
      it pad selection falls back to a 1.12× scale pulse.

Wiring checklist — resolution/aspect:
- [x] Set `matchWidthOrHeight` = **0.5** on all five canvases (HUD Canvas, DeathScreen, WaveCanvas,
      CoinCanvas — PauseMenuCanvas is already 0.5). *(2026-07-19: done via MCP — the four were at 1.0.)*
- [x] **ClassSelectPanel**: replace center anchor + anchoredPosition.x=600 with right-edge
      anchoring (anchorMin/Max x = 1, pivot x = 1, anchoredPosition.x ≈ −40), same size.
      *(2026-07-19: done via MCP — anchoredPosition.x came out −80 (not −40) because that's what
      exactly preserves the panel's current on-screen rect (560 wide, right edge 80 in from the
      canvas edge); size/y untouched.)*
- [x] **Skill-tree viewports** (GoldSkillTree + ReincarnateSkillTree, currently fixed
      1759.58×691.6 centered): convert to stretch anchors with margins that reproduce the current
      16:9 framing (≈80 left/right, current top/bottom offsets). Pan/zoom clamps adapt at runtime —
      no code change. *(2026-07-19: done via MCP — gold offsets L/R 80.25, top −186.17 / bottom
      202.17; reincarnate L 80 / R 640 (the class-panel strip) with the same vertical offsets;
      rect sizes verified unchanged at 16:9.)*
- [x] Verify MainButtonsPanel/TabsRow anchors keep them on-screen in a 1280×800 Game view.
      *(2026-07-19: checked anchors via MCP — both are center-anchored with small fixed rects
      (320×260 / 100×100), which cannot clip at 1280×800 with match 0.5; the visual pass is
      covered by the resolution-sweep item in Manual verification.)*

## Manual verification (Localization + Controller)

- [ ] **Language switch live**: pause → Settings → Language → Français: settings labels, pause
      buttons, wave countdown ("La vague commence dans 5"), skill node names/tooltips, class cards
      all switch without reload; back to Auto (System) restores English (on an EN system).
- [ ] **Font fallbacks**: switch to Русский then 简体中文 then 日本語 then 한국어 — no tofu (□)
      anywhere: settings, death screen, tooltips, wave messages.
- [ ] **Pseudo-locale sweep**: DevConsole → set language `xx` (add a cheat button or call
      `Loc.SetLanguage("xx")` via console) — every screen shows `[«…»]`-wrapped text; any bare
      English string is a missed conversion.
- [ ] **Persistence**: pick Deutsch, quit play mode, replay — still Deutsch. Reset Settings →
      language back to Auto and pad sensitivity back to 180.
- [ ] **CSV round-trip safety**: open Bladehold > Skill Tree Editor, save the gold tree unchanged —
      `Strings.csv` untouched, localized node names still resolve in play mode.
- [ ] **Pad look**: with a controller, right-stick look feels the same at 30 fps
      (`Application.targetFrameRate = 30` via DevConsole) and 144 fps; mouse look unchanged;
      Gamepad Look Sensitivity slider visibly changes turn rate; invert toggles apply to the stick
      too.
- [ ] **Pad gameplay**: full wave on pad only — move/sprint(RB)/jump(A)/attack(RT incl. charge)/
      aim(LT)/mount+dismount(B)/lock-on(R3) all work; attack cancels sprint.
- [ ] **Glyphs flip live**: HUD hints show LMB/RMB/Shift on mouse input, flip to RT/LT/RB within
      one input on the pad, and back on mouse move. Rebind Attack (pad) to X — HUD glyph updates
      immediately; Reset Settings restores RT.
- [ ] **Pad menus**: Start opens pause with Resume selected; d-pad navigates; A activates; B backs
      out of Settings → pause → resumes. Sliders adjust with d-pad left/right. Rebind grid: no
      diagonal jumps, selection scrolls the list, pad column rebind only accepts pad controls
      (cancel = Select), KBM column ignores the pad. Cursor hides while pad active, returns on
      mouse move — and is never re-shown mid-gameplay.
- [ ] **Pad death screen loop**: die → Try Again selected; LB/RB switch Gold/Reincarnate/Class
      tabs; on the tree RS pans (clamped at edges), LT/RT zoom eases around the viewport center,
      left-stick flicks step the selection along the graph (tooltip pinned beside the node, view
      pans to follow off-screen moves), A buys (feedback + auto-center, gold deducts), reincarnate
      flow + class pick + Begin Next Life all pad-only. Mouse hover still works and clears the pad
      highlight.
- [ ] **Intermission on pad**: wave cleared → Hold the Line selected by default; both choices work;
      frozen-time UI (tooltips, hint bar fade, focus pulses) still animates (unscaled).
- [ ] **Negative cases**: stick drift never flips glyphs to pad while typing (deadzone-filtered);
      pad B during gameplay never opens/closes menus; the tree's Buy (A) never double-fires a
      selected DeathScreen button (UiNav is enabled only while the tree panel is open — verify a
      tree-tab A press doesn't also click Try Again); chest/enemy behaviour unchanged (no gameplay
      systems touched beyond camera look).
- [ ] **Resolution sweep**: Game view at 1280×800, 1920×1080, 2560×1080, 3440×1440, 1024×768 —
      pause/settings (scrolled to bottom), death screen all three tabs, class select fully
      on-screen, intermission, HUD hint bar, tooltip near all four screen edges; nothing clipped or
      off-screen.

## Enemy Manager window — Unity Editor wiring

The C# is done. **Bladehold > Enemy Manager** (`Editor/EnemyManagerWindow.cs`) is a one-stop
enemy-tuning window: a roster list (from `Config/Enemies.csv` via the shared
`Enemies/EnemyRosterSO.asset`) plus four tabs. **Stats** (`Editor/EnemyStatsTab.cs`) edits a row's
CSV cells — optional-override semantics preserved, blank ≠ 0, untouched cells round-trip
byte-for-byte including the hand-aligned padding (`Editor/EnemyCsvIO.cs`, the `SkillTreeCsvIO`
pattern; save = write file → `ImportAsset` → `roster.Reload()`); pending edits live in
`Editor/EnemyManagerSession.cs` (a `ScriptableSingleton`, the `SkillTreeEditSession` pattern) so
they survive domain reloads and play-mode round-trips until the explicit **Save to CSV**. In play
mode, edits re-apply instantly to live zoo enemies through the new
`WaveSpawner.ApplyDefinitionLive` (same setter chain as `ApplyDefinition`, but health goes through
the new `Health.SetMaxHealth(value, preserveFraction: true)` overload — the `ScaleMaxHealth`
semantics — so a half-dead test subject stays half-dead). **Model** (`Editor/EnemyModelTab.cs`)
swaps a Sidekick-rig character model onto the enemy's prefab variant via
`Editor/ModelSwapUtility.cs` (the `PlayerModelSwapWindow` bone-name rebind, now shared; that window
delegates to it), baked with `LoadPrefabContents`/`SaveAsPrefabAsset` and recorded in a
`Enemies/ModelSwapRecord.cs` marker — authored renderers are disabled not deleted (revertible), and
`EnemyPrefabGenerator.Apply` now skips its manifest `materialPath` when a record is present so
re-runs can't paint over a swap. **Animation** (`Editor/EnemyAnimationTab.cs`) browses the Synty +
Kevin Iglesias clip libraries, previews any Humanoid clip on the enemy in an isolated
`Editor/EnemyAnimPreviewStage.cs` (a `PreviewSceneStage`; scrub/play driven by
`Editor/EnemyAnimSampler.cs`, an edit-mode PlayableGraph on the child Animator — the
`BowPropAnimator` pattern, root pinned so root-motion clips can't walk away), and assigns clips
into a per-variant `AnimatorOverrideController` (`Bladehold Animations/Overrides/AOC_<name>`,
created on first use, edited in place after — never nested) wired as the variant's controller.
**Zoo** (`Editor/EnemyZooTab.cs`) drives the play-mode `EnemyZoo` via its new public API
(`TrySelect`/`SpawnBatchOf`/`SetBattleMode`/`ApplyLiveDefinition`; `extraSpawns` are now id-tagged).
The animation-baking scope (GameObjectRecorder + baked ragdoll falls) was **dropped by request
2026-07-18** — the Bake tab does not exist.

- [x] **Reconnect the MCP bridge** (Window > MCP for Unity > Reconnect) — the WebSocket dropped
      mid-session; the remaining items were verified headlessly (compile) but not yet in-Editor.
      *(2026-07-19: bridge responding again — console reads/asset edits working.)*
- [ ] **Model tab round-trip check**: pick a low-stakes type (dwarf), swap
      `Assets/Synty/SidekickCharacters/Characters/GoblinFighters/GoblinFighter_02/GoblinFighter_02.prefab`
      onto it, confirm the variant gains a `ModelSwapRecord` (added renderer names listed, authored
      renderers disabled), then **Revert to Authored Model** and confirm `git diff` on the variant
      prefab is clean.
- [ ] **Generator idempotence**: with a swap in place on any manifest-generated variant, run
      **Bladehold > Generate Enemy Prefabs** — the console should log "skipping the manifest
      material apply" for that id and the swap must survive (`git diff` shows no renderer churn).
- [ ] **Animation preview smoke test**: Animation tab → Open Preview Stage on the goblin → pick a
      Kevin Iglesias attack clip → scrub and Play. Retarget sanity: no T-pose, feet roughly
      planted. Close the stage via the breadcrumb — the preview instance must not leak into the
      open scene.
- [ ] **AOC apply**: assign a different Attack clip on a variant ("◄ use previewed" or the object
      field), confirm `Bladehold Animations/Overrides/AOC_<name>.overrideController` is created and
      set as the variant's controller, then in the zoo the enemy attacks with the new clip and
      damage timing is unchanged (wall-clock `windupToApex`, not animation events).
- [ ] **HUMAN: balance/UX pass** on the window itself — column labels, one-shot hint list
      (`EnemyAnimationTab.OneShotHints`), zoo batch-size cap (500), whatever feels off in use.

## Manual verification (Enemy Manager)

- [x] CSV round-trip: no-edit save is byte-identical; a single Stats edit changes exactly one CSV
      line (verified via MCP 2026-07-18).
- [x] Live edits: in the zoo, halving/doubling a type's health preserves each live enemy's damage
      fraction; batch spawn + battle-mode toggle work from the window (verified via MCP 2026-07-18).
- [ ] Edit a stat, enter play mode, exit — the "unsaved changes" banner still shows the pending
      edit; Reload discards it; Save to CSV persists it and the zoo picks it up on next play.
- [ ] Row-0 guard: with Goblin selected, Delete is greyed out; scheduling fields are disabled and
      the fallback note shows.
- [ ] A model-swapped enemy in the zoo animates normally (locomotion, attack, death) and its
      hitboxes/weapons still work — the swap only touches renderers.
- [ ] An AOC-overridden enemy in a real wave (Bladehold Test Scene, `DebugSetNextWave` to its
      unlock wave) plays the new clip — the override is on the prefab, not zoo-only.

## Max Health and Bow Damage Skills — Unity Editor wiring

The C# is done. Added `PlayerMaxHealthMultiplier` to `StatType.cs` and a new `PlayerMaxHealthBinder` component to scale the player's max health based on the stat. Also added `hp_1` (Vitality), `hp_2` (Vigor), and `bow_dmg` (Stronger String) nodes to `SkillTree.csv`.

- [x] **Player prefab — `PlayerMaxHealthBinder`** (`Bladehold Prefabs/Player.prefab`): add the component on the player root (next to `Health` and `PlayerStats`). Both `stats` and `health` refs auto-wire via `OnValidate`. *(2026-07-19: added via MCP on `SidekickSyntyCharacter`; `stats`/`health` refs verified wired in the prefab file.)*
- [ ] **Skill icons**: `hp_1`, `hp_2`, and `bow_dmg` currently reuse `skill_134_heal` and `bow_shot1_nobg`. Assign better sprites via **Bladehold > Skill Tree Editor** if desired.
- [ ] **Balance pass**: tune cost/growth and amounts for the new nodes in `SkillTree.csv`.

## Manual verification (Max Health and Bow Damage Skills)

- [ ] Purchase Vitality and Vigor in the skill tree; verify the player's max health visibly increases in the UI and in actual hit points.
- [ ] Purchase Stronger String; verify arrow damage numbers increase.
## Arrow projectiles + Swift Arrows skill — Unity Editor wiring

The C# is done. Arrows are no longer hitscan: `Player/ArrowProjectile.cs` is a real projectile
(the `AxeProjectile` convention — per-`FixedUpdate` sphere cast from previous to current position so
nothing tunnels, `IPlayerProjectile` registration so Barbarian whirlwinds can shatter arrows) that
flies at the new `StatType.BowArrowSpeed` (base from `BowSO.baseArrowSpeed`, default 30 m/s) and
drops under `BowSO.arrowGravity` (default 9.81 m/s²) — so distant shots must be aimed high, and
faster arrows drop quadratically less. `Player/PlayerBow.cs` spawns one per arrow from a new
serialized `arrowPrefab` field and the projectile calls back into the extracted
`PlayerBow.ApplyArrowHit(...)`, so every arrow skill line (crit/precision/flaming/exploding
heads/brain freeze/midas/storm/bounce, plus the per-flight-segment Pickup Arrows and Unstable Orbs
sweeps) behaves exactly as before; charge level is captured at release (the `AxeProjectile.LaunchSpec`
convention). **With `arrowPrefab` unassigned the bow degrades to the old hitscan + `BowTracer`**
(a one-time `Start` warning says so), so the game runs before this wiring. New CSV row:
`swiftarrow` ("Swift Arrows", `BowArrowSpeed` Percent +0.2/level, 5 levels, cost 75 growth 1.4,
prereqs `multishot;flamearrow`, at 24.5,12). `Stats/StatDisplay.cs` has the "Arrow Speed" entry.

- [x] Create the arrow prefab — done via MCP: `Bladehold Prefabs/ArrowProjectile.prefab`, reusing
      the StuckArrow's Synty `Wep_Arrow_Primitive_01` model (tip at root, shaft down -Z, model
      rotated +90°X) plus a `Trail` child TrailRenderer matching the BowTracer look (Default-Line
      material, width 0.05, white→transparent, time 0.25 s). No collider/Rigidbody.
- [x] On the Player prefab's `PlayerBow`, hand-assign the new **Arrow Prefab** field — done via MCP
      (assigned on `SidekickSyntyCharacter`, re-queried to confirm it stuck).
- [x] `BowSO` asset defaults verified via MCP (`baseArrowSpeed` 30, `arrowGravity` 9.81,
      `arrowRadius` 0.05). **HUMAN:** tune speed/gravity for feel ("fairly slow with visible drop"
      is the design intent).
- [x] Skill icon: `swiftarrow` — done via MCP: `Archerskill_01_nobg` (the one unused Archer sheet
      sprite) registered in `SkillTreeSO.icons` and set in the CSV; `GetIcon` verified resolving.
- [ ] **HUMAN:** Balance pass: `swiftarrow` cost 75 / growth 1.4 / +20%×5 levels and its 24.5,12
      tree position are placeholders, as are `baseArrowSpeed` 30 / `arrowGravity` 9.81 (feel check).

## Manual verification (arrow projectiles + Swift Arrows)

- [ ] Buy `bow_unlock`, aim and fire: an arrow visibly travels (not instant) and arcs downward;
      at long range you must aim above a goblin to land the hit.
- [ ] Point-blank and mid-range shots still hit what the crosshair covers; headshots on a
      `VulnerableSpot` still trigger Precision/Brain Freeze/Exploding Heads (buy those nodes and
      check the popups/blast).
- [ ] Multi Shot extras fan out and each flies as its own dropping projectile; Bounce Shot still
      arcs a `BowTracer` streak from the impact to a second enemy.
- [ ] With Retriever, an arrow lobbed over a coin field collects along its curved path; with
      Unstable Orbs, the main arrow detonates an Impulse/Lightning Orb it flies through.
- [ ] Stuck-arrow props still appear in corpses at the impact point (`StuckArrowSpawner` listens to
      the same `OnArrowImpact`), and impact sound/blood (`BowHitFeedback`) plays at the landing spot,
      not at the bow.
- [ ] Buy Swift Arrows tiers (skill-tree tooltip shows Arrow Speed before→after): arrows get visibly
      faster and the arc flattens — same aim point lands higher on distant targets.
- [ ] A Barbarian Giant's whirlwind swats arrows out of the air (new behaviour — arrows were
      immune as hitscan).
- [ ] Mounted with Horse Archer: arrows never hit your own horse (the ignored-target pass-through).
- [ ] Charge a full draw, fire, and immediately release aim: the landing damage number reflects the
      charged shot (charge captured at release, not at impact).
- [ ] Unassign the arrow prefab temporarily: the bow falls back to hitscan tracers and logs the
      one-time warning (then re-assign).

## Screen-space Health / Stamina Bars (Synty Warrior UI + Feel) — Unity Editor wiring

Four C# scripts are done (`PlayerHealthBarUI`, `HorseHealthBarUI`, `HorseBarGroupUI`,
`HorseStaminaUI` upgraded). All use `MMProgressBar` (Feel) for animated lerp fills and delayed-bar
drain. `HorseBarGroupUI` uses `MMF_Player` feedbacks for the mount/dismount slide-in animation.

The scene still needs a canvas hierarchy and wired references:

### 1 — Build the BottomHUD hierarchy inside the existing HUD Canvas

Add a child `BottomHUD` anchored to **bottom-center** (`Stretch X`, anchor min Y=0, max Y=0,
pivot Y=0), offset ~80 px from the bottom edge. Suggested layout (horizontal stack, left-to-right):

```
HUD Canvas (Screen Space – Overlay)
  └─ BottomHUD                       ← HorizontalLayoutGroup, spacing 24
      ├─ PlayerHealthGroup            ← always visible; HorizontalLayoutGroup
      │   ├─ HealthIcon               ← Image: ICON_FantasyWarrior_Stat_Health_01_Clean.png
      │   │                             (Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Stats/)
      │   └─ PlayerHealthBar          ← MMProgressBar (see wiring below)
      │       ├─ Background           ← Image: SPR_FantasyWarrior_Bar_Horizontal_04.png
      │       │                         (Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/)
      │       ├─ Delayed              ← Image (orange fill) – assign to MMProgressBar.DelayedBarDecreasing
      │       └─ Fill                 ← Image (red fill)   – assign to MMProgressBar.ForegroundBar
      │
      └─ HorseGroup                   ← HorseBarGroupUI + CanvasGroup; starts hidden (alpha 0)
          ├─ HorseIcon                ← Image: ICON_SM_Item_Horseshoe_01.png
          │                             (Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Resources/)
          ├─ HorseHealthGroup         ← HorizontalLayoutGroup
          │   ├─ HeartIcon (optional) ← Image: ICON_FantasyWarrior_Stat_Health_01_Clean.png
          │   └─ HorseHealthBar       ← MMProgressBar (foreground red, delayed orange)
          │       ├─ Background
          │       ├─ Delayed
          │       └─ Fill
          └─ HorseStaminaGroup        ← HorizontalLayoutGroup
              ├─ SpeedIcon            ← Image: ICON_FantasyWarrior_Stat_Speed_01_Clean.png
              └─ HorseStaminaBar      ← MMProgressBar (foreground green; Fill Image also assigned to
                  ├─ Background         HorseStaminaUI.fillImage for exhaustion tinting)
                  └─ Fill
```

### 2 — MMProgressBar settings (apply to each bar)

| Setting | Value |
|---|---|
| FillMode | FillAmount |
| BarDirection | LeftToRight |
| TimeScale | UnscaledTime |
| LerpForegroundBar | ✓ (speed 10–15) |
| DelayedBarDecreasing | assign Delayed child |
| SetInitialFillValueOnStart | ✓, InitialFillValue 1.0 |

For `HorseStaminaBar`: leave `DelayedBarDecreasing` unassigned (stamina regen is fast; no drain ghost needed).

### 3 — Attach scripts

| GameObject | Component | Key fields |
|---|---|---|
| `PlayerHealthGroup` | `PlayerHealthBarUI` | `progressBar` → PlayerHealthBar |
| `HorseHealthGroup` | `HorseHealthBarUI` | `progressBar` → HorseHealthBar |
| `HorseGroup` | `HorseBarGroupUI` | `mountShowFeedback`, `mountHideFeedback` |
| `HorseStaminaGroup` | `HorseStaminaUI` | `progressBar` → HorseStaminaBar; `fillImage` → HorseStaminaBar/Fill |

All `PlayerMount`/`Health` refs auto-wire from `Player.Instance` when left empty.

### 4 — Wire MMF_Player feedbacks on HorseGroup

**Mount Show (`mountShowFeedback`)** — new MMF_Player on HorseGroup:
- `MMF_CanvasGroupAlpha`: target = HorseGroup CanvasGroup, 0 → 1, duration 0.3 s, curve EaseOut
- `MMF_Scale` (Punch): target = HorseGroup RectTransform, punch Y=0.08, duration 0.25 s
- Optionally `MMF_Position` offset: anchor pos shift Y +20 → 0, duration 0.3 s, EaseOut

**Mount Hide (`mountHideFeedback`)** — separate MMF_Player:
- `MMF_CanvasGroupAlpha`: target = HorseGroup CanvasGroup, 1 → 0, duration 0.2 s

Both feedbacks: set **Timing > Timescale Mode = Unscaled** (so they play during slow-mo intermission).

### 5 — Bar visual polish (Synty sprites)

- Set the `Background` images to `SPR_FantasyWarrior_Bar_Horizontal_04.png` (sliced, border ~8 px)
  or `SPR_FantasyWarrior_Bar_Horizontal_05.png` for a narrower bar style
- Tint the player health Fill red `(1, 0.2, 0.2)`, horse health Fill a slightly different red,
  stamina Fill green `(0.25, 0.85, 0.3)` — `HorseStaminaUI` overrides this at runtime for exhaustion
- Add a frame overlay using `SPR_HUD_FantasyWarrior_Frame_Box_Small_01.png` or a Box Background
  sprite behind the whole BottomHUD for a parchment/fantasy feel

### 6 — Verify

- [ ] Player health bar always visible; losing health causes fill to drain with delayed orange ghost
- [ ] Mounting a horse: HorseGroup slides/fades in (MMF animation plays)
- [ ] Horse stamina bar drains green while galloping; tints orange when exhausted
- [ ] Horse health bar drains when enemies hit the horse
- [ ] Dismounting: HorseGroup fades out
- [ ] Horse dying (auto-dismount): HorseGroup fades out
- [ ] No errors in Console when starting from a non-mounted state

---

## Enemy roster Phases ③–⑤ — Ancient Queen, Forest Witch, Mutant Guy, Medusa, Spirit Demon, Dark Elf, Slayer, Red Demon, Pig Butcher, Barbarian Giant, Fort Golem, Mechanical Golem — Unity Editor wiring

The C# is done and the generator was run headlessly (all 12 variants built + mapped; idempotency
re-run clean) — this completes the 20-enemy roadmap in `ENEMY_TYPES_PLAN.md`. Twelve new
`Config/Enemies.csv` rows (`forest_witch` w9, `mutant_guy` w9, `spirit_demon` w10,
`ancient_queen` w11, `dark_elf` w11, `medusa` w12, `slayer` w13, `pig_butcher` w14, `red_demon`
w15, `barbarian_giant` w17, `fort_golem` w18, `mechanical_golem` w19 — the plan's draft numbers).
**Phase ③**: `Enemies/ArmorPlating.cs` (Queen — `Health.ScaleDamageTaken`, the `RageBuff`
precedent: hits under `lightHitThreshold` ×0.4, charged swings pass), `Enemies/AllyAura.cs`
(Witch — 1 Hz `OverlapSphere` heal of `Enemy` roots via `Health.Heal`, never herself),
`Enemies/ToxicPoolOnDeath.cs` + `ToxicPoolZone.cs` (Mutant — `OnDied` listener, the `CoinDropper`
idiom, spawning a `LightningStormZone` copy-tune: unparryable elemental ticks, hits enemies too),
`Enemies/MedusaGazeAura.cs` (cone test = the `Parry` facing shape; MoveSpeed Percent modifier with
exact-negative removal — the `HoldTheLineBonus` idiom — behind a **static refcount** so two
Medusas can't stack). **Phase ④**: Spirit Demon is config-only (own `AIMovementSO` with both
avoidance tiers off + capsule `excludeLayers` = enemy layer 7, both generator-wired; CSV impulse
resistance 50), `Enemies/DodgeDash.cs` (Dark Elf — timer v1 of "when targeted": near + in the
player's facing cone → NavMesh.Raycast-checked lateral `agent.Move` burst),
`Enemies/SlayerDashAttack.cs` (locked lane, `NavMesh.Raycast` pre-clamp — the
`MountedKnightBrain` beats — stretched `SlamTelegraph`, capsule-overlap sweep, `agent.Warp`;
unparryable), `Enemies/LeapSlamAttack.cs` (Red Demon — TrollSlam telegraph at the player,
parabolic flight with agent disabled, `SamplePosition`+`Warp` re-seat, the Troll's
impulse-stamped impact block). **Phase ⑤**: `Enemies/HookProjectileAttack.cs` +
`HookProjectile.cs` (Pig Butcher — sharp, PARRYABLE, `Damage.source`-stamped so Counterstrike
punishes him; the pull only fires if damage actually landed, probed via a momentary `OnDamaged`
subscription) + **`Player/PlayerPullReceiver.cs`** (CharacterController.Move drag — walls
interrupt via `CollisionFlags.Sides`; no-op mounted/dead; never re-enables controls over a
corpse — the `PlayerMount` dead-guard), `Enemies/WhirlwindAttack.cs` (Barbarian Giant — periodic
unparryable pulse + shatters `IPlayerProjectile`s) + **`Player/IPlayerProjectile.cs`**
(`PlayerProjectileRegistry.Live`, the `EnemyRagdoll.ActiveCount` flavor, implemented by
`AxeProjectile`/`MagicMissileProjectile` in `OnEnable`/`OnDisable`; bow is hitscan — untouched,
and the whirlwind adds no collider so it can't eat arrows), `Enemies/MinionSpawner.cs` (Fort
Golem — dwarves via the public `WaveSpawner.ApplyDefinition`, registered through the **new
`WaveSpawner.RegisterExternalEnemy`**: grows `waveGoblinTotal`/alive set, never
`remainingToSpawn`; graceful no-op outside a live wave; capped at `maxAliveMinions` 6),
`Enemies/PinballCharge.cs` (Mechanical Golem — rev telegraph, agent detached, `NavMesh.Raycast`
wall reflection, contact damage with per-target re-hit window, `Warp` re-seat). All five new
attacks joined the `?.SetDamage(...)` chain in `ApplyDefinition`. Two prefabs were hand-authored
(the HomingOrb precedent, script GUIDs pinned via `.meta`): `ToxicPool.prefab` (zone script + a
green point light as the pre-art visual) and `HookProjectile.prefab` (re-uses the lightning-ball
sphere look). Every SO's defaults are authored in-code; the shared `SlamTelegraph.prefab` serves
Slayer/Red Demon telegraphs.

- [x] **Player prefab — `PlayerPullReceiver`** (`Bladehold Prefabs/Player.prefab`): add the
      component on the player root (next to `Health`/`PlayerMount`). `health`/
      `characterController`/`mount` auto-wire via `OnValidate`; **hand-fill
      `componentsToDisableWhilePulled`** with the `SamplePlayerAnimationController`,
      `CombatFacing`, `AttackCancelsSprint` (the PlayerMount list — NOT `InputReader`, NOT this
      component). Optional `pulledFeedback` (yank + grunt). Until wired, hooks damage but the drag
      fights the controller (a warning logs).
      *(2026-07-18: verified via MCP — component is on `SidekickSyntyCharacter` with `health`/
      `characterController`/`mount` auto-wired, but `componentsToDisableWhilePulled` is still an
      empty array and `pulledFeedback` is unassigned. The hand-fill sub-task below is still open.)*
  - [x] **Hand-fill `componentsToDisableWhilePulled`** on the existing `PlayerPullReceiver` with
        `SamplePlayerAnimationController`, `CombatFacing`, `AttackCancelsSprint` — currently empty,
        so a hook still fights the controller (warning logs). *(2026-07-19: filled via MCP with all
        three components from the same GameObject; prefab saved. `pulledFeedback` still unassigned —
        optional juice, no audio picked.)*
  - [ ] Also add `PlayerPullReceiver` to `PlayerDeath`'s disable list? **No** — it must stay
        enabled to refuse pulls while dead (it checks `health.IsDead` itself).
- [ ] **Animator (cosmetic gaps, not blocking)**: Slayer/Red Demon/Pinball rev/Hook throw all fire
      the existing `Attack` trigger (goblin swing stands in for their wind-ups); the Barbarian's
      whirlwind has **no** animation at all (he just walks). Proper states/clips per enemy when
      animation time exists. All attacks deal damage on timers regardless.
- [ ] **Art passes**: ToxicPool is a bare green light — needs a ground decal/particles
      (`ToxicPoolZone.tickVfxPrefab`/`tickSfx` are wireable slots); HookProjectile reuses the
      lightning-ball sphere — needs a hook model + chain trail; Spirit Demon ghost material,
      whirlwind spin VFX (`WhirlwindAttack.spinFeedback`), leap/land VFX
      (`LeapSlamAttack.impactVfxPrefab`), pinball sparks (`bounceFeedback`), per-enemy materials
      for all 12 (set `materialPath` in the manifest + re-run the generator).
- [ ] **Optional MMF juice**: every new component exposes optional feedback slots
      (`windupFeedback`/`dashFeedback`/`slamFeedback`/`revFeedback`/`spawnFeedback`/
      `healFeedback`/`gazeCaughtFeedback`/`startAttackFeedback`/`pulledFeedback`).
- [ ] **Balance pass**: all 12 CSV rows are plan drafts; SO defaults to tune —
      `AncientQueenArmorSO` (threshold 15 / ×0.4), `ForestWitchAuraSO` (r6, 2 HP/s),
      `MutantToxicPoolSO` (r2.5, 6s, 2/0.75s), `MedusaGazeSO` (r10, 35°, 50% slow),
      `DarkElfDodgeSO` (5m, dot 0.7, 3.5m dash, 2s cd), `SlayerDashSO` (lane 12×1.6, 0.9s tell,
      5s cd), `RedDemonLeapSO` (r12, 0.8s+0.7s, slam r3.5, imp 3/12, 6s cd), `PigButcherHookSO`
      (r12, hook 11 m/s, pull 0.5s→2m, 6s cd), `BarbarianWhirlwindSO` (r3.5, 1s pulse),
      `FortGolemSpawnerSO` (2/8s, cap 6), `MechGolemChargeSO` (rev 1.2s, 14 m/s ×4s, rehit 1s,
      6s cd).

## Manual verification (enemy roster Phases ③–⑤)

- [ ] **Ancient Queen** (w11): uncharged sword hits show visibly shrunken damage numbers (~×0.4);
      a fully charged Heavy Strike hits for full damage. Numbers match what her health actually
      loses (the ScaleDamageTaken contract).
- [ ] **Forest Witch** (w9): wounded goblins near her visibly regain health (damage numbers you
      dealt get 'undone'); kill her first and the healing stops. She never heals herself; the
      player is never healed by her.
- [ ] **Mutant Guy** (w9): on death a green-lit patch appears at the corpse; standing in it ticks
      ~2 damage per ¾s for ~6s, then it vanishes. It also hurts goblins that wander in. The ticks
      are never parried (elemental + unparryable). The corpse itself is never re-damaged by its
      own pool (no phantom damage numbers over the body).
- [ ] **Medusa** (w12): walking into her frontal cone visibly halves your move speed (sprint
      too — MoveSpeed feeds the binder); stepping out of the cone or killing her restores it
      exactly. **Two Medusas gazing at once slow you no further than one**, and killing one of the
      two keeps the slow until the other's gaze breaks. Die while gazed → restart is at full speed.
- [ ] **Spirit Demon** (w10): other goblins walk straight through it (no shoulder-barging), and it
      never body-blocks the horde; the sword still hits it normally (it's on the enemy layer for
      hitLayers). Impulse hits never ragdoll it (resistance 50).
- [ ] **Dark Elf** (w11): face one at close range — it periodically bursts sideways out of your
      line; hitting it mid-dodge is hard but possible. It never dodges through walls, and never
      dodges when approached from behind (you're not facing it).
- [ ] **Slayer** (w13): a red lane appears pointing at where you stood, ~1s later the slayer is
      instantly at the far end and anything in the lane (you, goblins) took 8 unparryable damage.
      Sidestep the lane during the tell → zero damage. Near a wall the lane (and dash) is
      visibly shorter. It has no regular melee swing.
- [ ] **Red Demon** (w15): a slam circle appears under you, the demon crouches, arcs through the
      air, and lands ON the circle — standing outside it when he lands takes nothing; inside,
      heavy damage and nearby goblins ragdoll-fling. Kill him mid-air (bow) → corpse drops where
      it was, no slam, wave accounting fine.
- [ ] **Pig Butcher** (w14, needs PlayerPullReceiver wired): the hook projectile is visible and
      slow enough to sidestep; getting hit yanks you toward him for ~½s into melee range where he
      swings. **Parry (facing him) or a Solid block negates BOTH the damage and the pull**;
      Counterstrike after a parried hook damages HIM. Hooked while a wall is between you → the
      drag stops at the wall. Hooks do nothing while mounted.
- [ ] **Barbarian Giant** (w17): standing next to him ticks heavy damage every second (never
      parried); **Berserker thrown axes and Mage missiles visibly vanish when they enter his
      radius** (no damage numbers), while **bow arrows hit him normally**. Axes still boomerang
      home if they never enter the radius.
- [ ] **Fort Golem** (w18): every ~8s it clanks out 2 dwarves beside itself, up to 6 alive; kill
      the golem and production stops (existing dwarves live on). **The wave does not clear until
      spawned dwarves are also dead** (registration), and minion kills pay gold/count in stats
      exactly like normal dwarves. `DebugWipeWave` kills registered minions too.
- [ ] **Mechanical Golem** (w19): revs in place (~1.2s tell), then careens in a straight line,
      visibly bouncing off arena walls like a pinball for ~4s; contact hurts (12, unparryable,
      goblins clipped get flung) but the same target isn't ground repeatedly (≥1s between hits).
      It resumes normal chasing exactly where the charge ended (no off-mesh stranding), and never
      leaves the arena.
- [ ] Global negatives: none of the new specials ever damages its own enemy (`source`/owner
      guards); every enemy stops attacking and cheers on player death (whirlwind pulses stop, no
      new hooks/leaps/charges); all 12 appear in the Enemy Zoo with CSV overrides applied;
      `DebugSetNextWave` to each unlock wave spawns the right type.

## Enemy roster Phase ①+② — Dwarf, Ancient Warrior, Big Ork, Forest Guardian, Mystic, Evil God — Unity Editor wiring

The C# is done (`ENEMY_TYPES_PLAN.md` Phases ① and ②) **and the generator was already run
headlessly** — all six prefab variants exist, their SO assets are created, and the six ids are
registered in `EnemyPrefabMap.asset` (which the run created; the hand-built mappings are still
missing from it — see the next entry). **Six new `Config/Enemies.csv` rows** (ids `dwarf` w3,
`ancient_warrior` w5, `big_ork` w7, `forest_guardian` w8, `mystic` w10, `evil_god` w16 — the
plan's draft numbers, inserted in unlock-wave order so common types keep chance-roll priority).
Phase ① is CSV + manifest only (stock `AIAttack` melee). Phase ②: **Forest Guardian** reuses
`Enemies/LightningBallAttack.cs` with its own generator-created `ForestGuardianAttackSO`
(ballSpeed 12, cooldown 2 — fast straight bolts); **Mystic** got `Enemies/HomingOrbAttack.cs` +
`HomingOrbAttackSO.cs` + `Enemies/HomingOrb.cs` (the `LightningBallAttack`/`LightningBall`
skeleton plus per-tick `Vector3.RotateTowards` steering capped at `turnRateDegPerSec` 60, giving
up after `homingSeconds` 2.5 so a committed dodge always works; deliberately a copy, not a
subclass — the Conduit skill interaction stays LightningBall/Storm Witch-only); **Evil God** got
`Enemies/RadialBurstAttack.cs` + `RadialBurstAttackSO.cs` (every 5s within 16m — no line of
sight — releases 8 `LightningBall`s at even angles anchored on its facing; range is NOT
re-checked at the apex, a committed boss burst always comes out). Both new attacks join the
`?.SetDamage(...)` chain in `WaveSpawner.ApplyDefinition`.
**`Bladehold Prefabs/HomingOrb.prefab` was hand-authored** (a copy of `LightningBall.prefab`
YAML with the script swapped; script GUID pinned via a hand-written `HomingOrb.cs.meta`) so the
generator could wire it headlessly. Five new manifest entries in `Editor/EnemyManifest.cs`
(all six enemies incl. the pre-seeded `dwarf`); the three casters disable base `AIAttack`,
stand off (`navStoppingDistance` 8/8/10), and remove `GoldenGoblin`/`ImpulseGoblin` (the Storm
Witch precedent).

- [x] **Prereq — the next entry's map-asset items**: `EnemyPrefabMap.asset` now exists (the
      generator run created it with the six generated ids) but still needs the hand-built
      mappings added (`goblin`, `goblin_brute`, `storm_witch`, `troll`) **and** assignment on
      `WaveSpawner` (Bladehold Test Scene) and `EnemyZoo` before ANY type can spawn.
      *(2026-07-16: done — see the enemy-prefab-map section below.)*
- [ ] **Animator (cosmetic gap, not blocking)**: all three casters fire the existing `Attack`
      trigger, so they play the goblin melee swing as their "cast". Attacks still deal damage on
      their wind-up timers; add proper cast states/clips on the shared goblin controller when
      animation time exists.
- [ ] **Projectile art pass**: Forest Guardian and Evil God currently fire the shared (Storm
      Witch blue) `LightningBall.prefab`, and `HomingOrb.prefab` is visually identical to it —
      re-tint per enemy (duplicate prefab + material swap for the Guardian/God, material swap on
      HomingOrb) and repoint the manifest `wire` paths, then re-run the generator.
- [ ] **Per-enemy materials**: all six use the goblin body (user decision 2026-07-13 — manual art
      pass). When materials exist, set `materialPath` on each manifest entry and re-run.
- [ ] **Optional MMF juice**: `startAttackFeedback` on the three casters' attack components
      (cast whoosh/glow); hit feedback is already carried by the projectiles.
- [ ] **Balance pass**: all six CSV rows are the plan's draft numbers; also
      `ForestGuardianAttackSO` (range 14, dmg 3, speed 12, cooldown 2), `MysticAttackSO`
      (range 12, dmg 4, orbSpeed 4, turn 60°/s, homing 2.5s, cooldown 4), `EvilGodBurstSO`
      (range 16, dmg 5, count 8, speed 5, cooldown 5).

## Manual verification (enemy roster Phase ①+②)

- [ ] DevConsole `DebugSetNextWave 3` → dwarves spawn: tiny (0.7×), very fast, die in one hit,
      2 guaranteed ramping +1 per wave, no per-wave cap.
- [ ] Wave 5 → Ancient Warriors mix in (normal-looking, 20 HP, ~25% of slots, 1 ramping); wave 7 →
      Big Orks (1.35×, hit for 6, max 2 alive, impulse resistance 4: sword impulse at power 3
      knocks down, 4+ ragdolls).
- [ ] Wave 8 → Forest Guardian stands off ~8m and fires **fast straight** balls every ~2s that
      hurt for 3 — strafing sideways dodges them; it never melees.
- [ ] Wave 10 → Mystic's orb **visibly curves to chase you**; running laterally then cutting back
      after ~2.5s makes it whiff (it flies straight once homing expires); outrunning it works
      (orb 4 m/s < player run speed).
- [ ] Wave 16 → Evil God (rare, max 1, 1.5×, 220 HP) releases an 8-projectile ring every ~5s even
      with walls/no line of sight between you; the gaps are walkable; consecutive rings are
      rotated (they follow its facing); impulse never ragdolls it (resistance 50).
- [ ] Negative: none of the three casters ever spawns as a golden/impulse variant (components
      removed); no projectile ever damages its own caster; a caster killed mid-windup fires
      nothing; all projectile hits are **never parried** (elemental) — check with Parry maxed.
- [ ] Player death → casters stop firing and cheer; a Mystic orb already in flight stops homing
      at the corpse (coasts straight).
- [ ] Enemy Zoo scene → all six new types appear with CSV overrides applied (scales/speeds read
      correctly).
- [ ] Wave accounting: kill an Evil God while its ring is still in flight → wave clears normally.

## Enemy prefab map + prefab generator — Unity Editor wiring

The C# is done. **Prefab map refactor** (`Enemies/EnemyPrefabMapSO.cs`): the id → prefab mapping
moved out of the two scene inspector lists into one `EnemyPrefabMapSO` asset — `Waves/WaveSpawner.cs`
and `Debug/EnemyZoo.cs` now hold a single `prefabMap` field (validated in `Start` with the `anyError`
idiom) instead of their duplicated `EnemyPrefabEntry[]` arrays; `FindPrefab` lives on the SO.
**⚠ The game spawns NOTHING until the asset below is created and assigned in both scenes.**
**Generator** (`Editor/EnemyPrefabGenerator.cs` + `Editor/EnemyManifest.cs`, see the
`generate-enemy-prefabs` skill): **Bladehold > Generate Enemy Prefabs** builds prefab *variants* of
`Goblin Enemy (Base)` from declarative `EnemySpec` manifest entries (the `SettingsMenuGenerator` /
`SkillTreePreviewBuilder` PrefabUtility + `SerializedObject` precedents) — creates missing per-enemy
SO assets (never overwrites existing ones), wires serialized refs (fails loudly on renamed fields),
disables/removes base components per spec, and registers the id in the map asset. Idempotent;
headless entry `EnemyPrefabGenerator.GenerateAll` for `-batchmode -executeMethod`. The manifest is
seeded with a structure-free `dwarf` entry (the smoke test; no `Enemies.csv` row yet, so it can't
spawn in waves). Hand-built variants (Brute/Storm Witch/Troll) deliberately have no manifest entry.
The 20-enemy roadmap that will consume this lives in `ENEMY_TYPES_PLAN.md`.

- [x] **Fill the hand-built mappings in the map asset** — the asset itself now exists (the
      generator run created `Assets/Bladehold/Bladehold Scripts/Enemies/EnemyPrefabMap.asset` with
      the six generated ids): add `goblin` → `Goblin Enemy (Base)`, `goblin_brute` →
      `Goblin Brute Enemy Variant`, `storm_witch` → `Storm Witch Enemy Variant`, `troll` →
      `Troll Enemy Variant`. (`bomber`/`knight` stay unmapped until their prefabs exist —
      same as before the refactor.) *(2026-07-16: all four added; map has 22 entries.)*
- [x] **Assign the asset** on the `WaveSpawner` in `Bladehold Test Scene.unity` (`prefabMap`,
      hand-assigned — the old `enemyPrefabs` list rows are gone) **and** on the `EnemyZoo`
      component in the Enemy Zoo scene (`prefabMap`). *(2026-07-16: WaveSpawner assigned;
      EnemyZoo already had it.)*
- [x] **Run the generator once** — done headlessly 2026-07-14 (Phase ①+② session): all six
      variants created, map asset created with the six generated ids, idempotency re-run clean.
      (`dwarf` also got its `Enemies.csv` row, so no missing-row warning applies anymore.)

## Manual verification (enemy prefab map + generator)

- [ ] Enter Play mode → waves spawn goblins exactly as before the refactor (brutes from wave 4,
      storm witches from 6, troll wave 8 — spot-check with DevConsole `DebugSetNextWave`).
- [ ] Open the Enemy Zoo scene → the gallery still populates one of every mapped roster type with
      CSV overrides applied.
- [ ] After the generator run: `Dwarf Enemy Variant.prefab` opens as a **variant** of
      `Goblin Enemy (Base)` (header shows the base as its parent), scale 1, all stock goblin
      components present and enabled.
- [ ] Re-run **Bladehold > Generate Enemy Prefabs** → console reports an update, `git status`
      shows no changes (idempotency), and nothing in the map asset is duplicated.
- [ ] Negative: waves never spawn a dwarf (no CSV row), and the Brute/Storm Witch/Troll variants
      are untouched by the generator run (`git status` clean on their .prefab files).

## Between-wave intermission (slow-mo + stats + Recover/Hold the Line) + Loot chests — Unity Editor wiring

The C# is done. **Hold the Line economy** (`Economy/HoldTheLineBonus.cs`): a scene-singleton greed
meter — `Extend()` banks one more consecutive Hold by adding `StatType.HoldTheLineGoldPerWave`
(base 5%, registered in `Start`) as a stacking Percent modifier on `StatType.GoldDropMultiplier`
(so `CoinDropper` needs zero changes — it already reads the multiplier fresh per kill). Both loss
conditions reset the whole stack: player `Health.OnDied` and `Gate.OnAnyGateDestroyed` (the two
fail-states — the gate must be re-enabled in the scene). Exposes `Instance`/`Multiplier`/
`StackCount`/`OnChanged`; optional `extendFeedback`/`resetFeedback` MMF_Players. **Wave flow**
(`Waves/WaveSpawner.cs`): after each `WaveCleared`, `RunWaves` now fires the new
`IntermissionStarted` event and parks on `WaitForPlayerChoice` until the UI calls `ChooseRecover()`
(normal countdown next) or `ChooseHoldTheLine()` (skips the countdown — next wave lands
immediately). **With no subscriber the spawner proceeds exactly as before**, so the game runs
un-wired. New top-level `IntermissionChoice` enum. **Intermission UI**
(`Waves/WaveIntermissionUI.cs` + `UI/WaveStatsPanel.cs`): the orchestrator plays a slow-mo
MMF_Player, reveals the stats, shows the two choices, opens the skill-tree panel on Recover, and
frees/re-locks the cursor (the `DeathScreen` pattern); the stats panel snapshots per-wave deltas
(`WaveStarted` → clear) and count-up-animates goblins/gold/total + Hold multiplier on unscaled
time. **Loot chests** (`Chests/Chest.cs`, `Chests/ChestLootTableSO.cs`, `Chests/ChestSpawner.cs`):
a chest is just a `Health` target (hit/break juice on its `damageFeedback`/`deathFeedback`
MMF_Players — no new hit script), dropping guaranteed gold (a `Coin`) + one weighted bonus item
from existing pickups; the spawner scatters N per wave on `WaveStarted` (NavMesh-snapped, away from
the player, weighted by chest level). **Reincarnate node** `greedy_stand` ("Greedy Stand", +2%/wave
per level ×4) added to `Config/Reincarnate.csv`. New `StatType.HoldTheLineGoldPerWave`.

- [x] **Re-enable the gate in the scene** (your task) — the second fail-state. `HoldTheLineBonus`
      and the intermission already listen for `Gate.OnAnyGateDestroyed`; nothing else to wire for it.
      *(2026-07-16: `Gate_Test` re-activated in `Bladehold Test Scene`.)*
- [x] **HoldTheLineBonus**: add the component to a scene object (the player root or a systems object
      — it finds `Player.Instance.Stats` itself). Optional: assign `extendFeedback` (a chime/flash
      when the bonus grows) and `resetFeedback` (a deflating stinger on loss). `baseGoldPerWave` 0.05.
      *(2026-07-16: added to the `WaveSpawner` object; optional feedbacks left unassigned — no audio picked yet.)*
- [ ] **Intermission canvas** (new, or a panel on the HUD canvas):
  - [ ] A root object with `WaveIntermissionUI`; assign `spawner`/`holdTheLineBonus`/`statsPanel`
        (auto-wire via `OnValidate`), `intermissionRoot`, `choiceButtons` (container), the two
        `Button`s (`recoverButton` "Recover and Upgrade", `holdTheLineButton` "Hold the Line"), a
        `continueButton` (shown with the tree), and `skillTreePanel`.
  - [ ] `waveClearFeedback` MMF_Player = an **MMF Timescale Modifier** (~0.15× for ~0.5s, ease-out)
        + optional **MMF Bloom / Chromatic Aberration (URP)** grade + a riser **MMF Sound**. Keep its
        total duration ≤ `slowMoRealSeconds` (0.7) — the UI waits that long for the slow-mo to restore
        before it hard-freezes time, so they don't fight over `Time.timeScale`.
  - [ ] **Set every intermission MMF_Player to Unscaled time mode** (the MMF_Player's *TimeScale Mode:
        Unscaled*) — the stats/choice screen runs at `Time.timeScale = 0`, so scaled feedbacks freeze.
  - [ ] `WaveStatsPanel` on a child; assign the TMP labels (`waveLabel`, `goblinsSlainText`,
        `goldEarnedText`, `totalGoldText`, optional `bonusMultiplierText`), optional fill `Image`s
        (`goblinsBar`/`goldBar`), and per-line reveal MMF_Players (pop + tick sound + `MMF_TMPText`
        reveal). Tune `countUpDuration`/`lineStagger`.
  - [ ] `skillTreePanel` = a panel hosting a `SkillTreeView` bound to `SkillTreeService.Instance`
        (same as the death-screen gold tree — reuse that prefab/panel or make a sibling).
- [x] **Chest prefabs** (one per level/model): `Collider` **on a layer inside the sword
      `DamageTrigger.hitLayers` mask** (else BladeSweep won't hit it), a **kinematic** `Rigidbody`
      (or none), `Health` (+ a per-level `HealthSO`), `MMHealthBar` + `HealthBarUI`, `Chest`
      (assign `lootTable`, `coinPrefab`, your explosion `breakVfxPrefab`). Assign `Health`'s
      `damageFeedback` (squash/flash/thunk/splinters) and `deathFeedback` (big boom + hit-stop).
      **Do NOT add `ImpulseReceiver`/`KnockbackReceiver`** — that's what makes chests impulse-immune.
      *(2026-07-18: verified via MCP — `Bladehold Scripts/Chests/Loot Chest.prefab` exists with
      `Health` (`LootChestHealth.asset`), `Chest`, `MeshCollider`, and an `MMF_Player`. **Only one
      tier exists** (no per-level variants yet), and it has **no `MMHealthBar`/`HealthBarUI`** —
      the enemy-pattern health bar is still missing. Confirm `ImpulseReceiver`/`KnockbackReceiver`
      are genuinely absent before considering this fully done.)*
  - [x] **Add `MMHealthBar` + `HealthBarUI` to `Loot Chest.prefab`** (missing — confirmed via MCP).
        *(2026-07-19: done via MCP — components copied from the goblin's configured HealthBar and
        pasted on the chest root; `HealthBarUI.health`/`healthBar` re-pointed at the chest's own
        `Health`/`MMHealthBar`. Also re-confirmed the prefab carries **no**
        `ImpulseReceiver`/`KnockbackReceiver` — full component list checked.)*
  - [x] **Chest now disappears after breaking** *(2026-07-18)*: `Chest.HandleDied` previously
        never destroyed or disabled anything — a broken chest stayed a solid, hittable mesh
        forever. Added a `destroyDelay` (default 2s — long enough for `deathFeedback`/`breakVfx`
        to play), disables all child `Collider`s immediately on death, then `Destroy(gameObject,
        destroyDelay)`. Compile-checked clean; no corpse pipeline needed since chests aren't
        `Enemy`s.
  - [x] **Sword hit VFX on chests was always blood** *(2026-07-18)*: `SwordHitFeedback` played its
        `bloodParticlePrefab` on every hit regardless of target, including chests. Added a new
        `inanimateHitParticlePrefab` field, used instead of blood when the hit target resolves to
        a `Chest` (`target.GetComponentInParent<Chest>() != null`); wired on the Player prefab's
        sword to `Assets/Synty/PolygonParticleFX/Prefabs/FX_Impact_Large_01.prefab` via
        `manage_prefabs modify_contents` (headless — no prefab-stage save-prompt risk).
  - [x] **ChestLootTableSO assets** (menu `Scriptable Objects/ChestLootTableSO`), one per tier:
        set `minGold`/`maxGold`, `bonusItemChance`, and the weighted `items` roster from existing
        pickup prefabs (`HealthPack`, `LightningOrb`, `ImpulseOrb`, a gold-bag `Coin`).
        *(2026-07-18: verified via MCP — `Chests/ChestLootTableSO.asset` exists; only one tier.)*
  - [x] Place a **`ChestSpawner`** in the scene; fill `chestPrefabs` (prefab + weight + unlockWave
        per level), `minPerWave`/`maxPerWave`, and optional `spawnPoints`.
        *(2026-07-18: verified via MCP — a `Chest Spawner` GameObject is in `Bladehold Test Scene`,
        `chestPrefabs` has one entry (Loot Chest, weight 100, unlockWave 0), `minPerWave` 1 /
        `maxPerWave` 3, `spawnRadius` 12, `minPlayerDistance` 5. `spawnPoints` is empty (falls back
        to radius-based placement) — fine per the script's design.)*
- [ ] **Greedy Stand icon**: `greedy_stand` ships with a blank icon — assign a sprite via
      **Bladehold > Skill Tree Editor** (and re-save so the row parses), or it shows no icon.
- [ ] **Balance**: `baseGoldPerWave` (5%), the `greedy_stand` cost/growth/maxLevel, chest
      `HealthSO`s / loot tables / per-wave counts, and the slow-mo strength/duration.
- [ ] **Optional polish**: a persistent HUD indicator for the live Hold multiplier
      (`HoldTheLineBonus.Instance.Multiplier` / `OnChanged`). (The choice screen freezes time — the
      loot window is the unfrozen countdown after choosing Hold the Line.)

## Manual verification (intermission + chests)

- [ ] Clear a wave → brief slow-mo, then **time freezes** and the stats cascade in (goblins/gold
      count up, bars wipe) with the cursor freed; the player can't move or loot while deciding.
- [ ] "Hold the Line" → time resumes and the **pre-wave countdown runs as a loot window** (grab the
      coins on the cleared field before the next wave hits); coin drops are visibly larger, and
      holding wave after wave stacks the bonus higher (`bonusMultiplierText` / HUD climbing x1.05 →
      x1.10 → …).
- [ ] "Recover and Upgrade" → the skill tree opens **still frozen** (untimed shopping); buy nodes,
      hit Continue → time resumes and the next wave starts **immediately** (no loot window). The
      banked Hold multiplier is retained (not grown, not reset).
- [ ] Die, or let the gate be destroyed → the Hold multiplier resets to x1 (verify the indicator
      drops immediately and the next run starts fresh); checkpoint restart returns to the died-on wave.
- [ ] With no intermission UI in the scene, waves still chain via the normal countdown (un-wired
      safety).
- [ ] Chests spawn each wave, away from the player; smashing one depletes its health bar, plays hit
      feedback per swing, and on break plays the explosion + drops gold plus sometimes one item
      (health pack / lightning orb / impulse orb).
- [ ] A chest is **never** flung or knocked by impulse-buffed / knockback sword hits (kinematic,
      no `ImpulseReceiver`); different chest levels show different models/health/loot.
- [ ] Reincarnate, buy **Greedy Stand** → the per-wave Hold bonus is visibly larger on the next run.

## Mounted Knight enemy + rideable Horse — Unity Editor wiring

The C# is done. **Shared horse** (`Horse/` — new folder): `HorseSO.cs` (all locomotion/charge/trample
tunables), `HorseMotor.cs` (player-mode driving: W/S accel/brake/reverse, A/D turn, held Shift at
speed = charge; speeds × `StatType.HorseSpeedMultiplier`), `HorseAnimation.cs` (transform-delta →
damped `Speed`/`Turn` animator params + `Rear`/`Charge`/`Death`, one script for AI/riderless/player
modes), `HorseChargeDamage.cs` (the shared trample box — the `TrollSlamAttack` impulse-stamping shape
with a per-target re-hit cooldown, used by both the knight's charge and the player's),
`HorseMountable.cs` (jump-into-trigger mounting, occupancy-gated), `HorsePickupProxy.cs` (a ridden
horse collects pickups on the rider's behalf — `Coin`/`HealthPack`/`ImpulseOrb`/`LightningOrb`
redirect through it; `HealthPack` also heals the horse with the Stable Diet node). **Knight**
(`Enemies/`): `MountedKnightSO.cs`, `MountedKnightBrain.cs` (standoff ring → aim → rear + telegraph
lane pre-clamped with `NavMesh.Raycast` → `agent.Move` dash with the trample open → decelerate →
cooldown), `MountedKnightRider.cs` (knight root = the spawned enemy; detaches the horse child at
Awake, seat-syncs in LateUpdate, forwards all horse damage to the knight via `TryBlockDamage`,
unseats him below 50% HP — enabling his stock goblin components, which ship disabled — and hands the
horse over riderless at full HP; killed mounted = corpse-dismount, wave still clears). **Player**
(`Player/`): `PlayerMount.cs` (mount/dismount state machine, invulnerable-while-mounted by
forwarding hits to the horse, sword `SetReachBonus`/`SetIgnoredTarget` while mounted, Barded Steed
`ScaleMaxHealth` once per horse), `MountedCombat.cs` (re-fires `StartAttack`/`IsHoldingAttack` since
the Synty controller is disabled in the saddle), `MountedCombatLook.cs` (the `BowAimLook` sibling —
spine yaw/pitch toward the camera while attacking/aiming mounted), `StartMountedSpawner.cs` (the
Reincarnate Cavalier node). Core changes: `DamageTrigger` gained `SetReachBonus` (samples past the
blade tip — no visual change) + `SetIgnoredTarget`; `Health.ScaleMaxHealth` (fraction-preserving);
`PlayerBow` gained the `HorseArcheryUnlocked` gate + `SetIgnoredTarget`; `AIAttack` range is now
planar XZ (saddle height); 6 new `StatType`s; `knight` row in `Config/Enemies.csv` (wave 7, 60 HP,
dmg 4 → charge 16, max 1); `WaveSpawner.ApplyDefinition` routes damage to
`MountedKnightBrain.SetDamage`; a `Dismount` action (X / gamepad East) added to the vendored
`Controls.inputactions` + `InputReader.cs` (marked additions); `horse_*` branch in
`Config/SkillTree.csv` off `range_ext`, `start_mounted` root in `Config/Reincarnate.csv`.

- [ ] **Regenerate the input class**: select
      `Assets/Third Party/Synty/AnimationBaseLocomotion/Samples/Scripts/InputSystem/Controls.inputactions`,
      and Apply/"Generate C# Class" so `Controls.cs` picks up the new `Dismount` action (the
      interface gains `OnDismount`, already implemented in `InputReader`). Until then dismount
      falls back to a direct X-key read (keyboard only — `PlayerMount` logs a warning).
- [x] **Create SO asset instances**: a `HorseSO` (menu `Scriptable Objects/HorseSO` — defaults are
      authored in-code: maxSpeed 8, chargeSpeed 12, trample 15 dmg / impulse 10/14), a
      `MountedKnightSO` (`Scriptable Objects/MountedKnightSO` — standoff 12, rear 1.2s, charge 14
      m/s ×4 dmg, dismount at 50%), and a horse `HealthSO` (~100 max health).
      *(2026-07-18: verified via MCP — `HorseSO.asset`, `MountedKnightSO.asset`, and
      `HorseHealthSO.asset` all exist under `Bladehold Prefabs/Horse/`.)*
- [x] **Horse prefab** (`Bladehold Prefabs/Horse.prefab`) from
      `Assets/Malbers Animations/Horse AnimSet Pro/Undead Horse/Models/Undead_Horse_Re.fbx`:
      root with `Health` (+ horse `HealthSO`), `HorseAnimation`, `HorseChargeDamage`,
      `HorsePickupProxy`, `CorpseDespawner`, `DisableCollidersOnDeath`, optional
      `DamageNumberSpawner`, a body collider, and — **all disabled** — `NavMeshAgent`
      (horse-sized), `CharacterController` (horse-sized), `HorseMotor`. Children: a `RiderSeat`
      empty on the saddle (assign on `HorseMotor.riderSeat` and `MountedKnightRider.riderSeat`) and
      a `HorseMountable` trigger collider over the saddle (**enabled** — riderless is the prefab's
      default state). NO `Enemy`/`CoinDropper`/`EnemyRagdoll` — it's a vehicle: no kill credit, no
      gold, animation-only death.
      *(2026-07-18: done via MCP — a fully-built `Horse` GameObject was already sitting in
      `Bladehold Test Scene` (all the components above, `RiderSeat` with `HorseMountable` nested
      under the animated rig), so it was converted in place with
      `manage_prefabs create_from_gameobject` into `Bladehold Prefabs/Horse/Horse.prefab`; the
      scene instance is now linked to it. **Note**: the body has no standalone Collider — only the
      `CharacterController` (disabled until a rider mounts) and the `HorseMountable` trigger. A
      riderless horse currently can't be hit by the sword (no active collider on its hit layer)
      until that's addressed — flag for a follow-up if combat against riderless horses matters.
      `Health.damageFeedback`/`deathFeedback` are also still unassigned.)*
  - [ ] **Horse animator controller**: params `Speed` (float, m/s), `Turn` (float -1..1), `Charge`
        (bool), `Rear` (trigger), `Death` (trigger). Blend tree on Speed: `H_Idle_01` → `H_Walk` →
        `H_Trot` → `H_Canter` → `H_Gallop` (blend the `_Left`/`_Right` variants on Turn), `Charge`
        → a gallop/lean state, `Rear` → `H_Attack_Front_Legs`, `Death` → `H_Death01`. Clips under
        `Assets/Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/`.
        *(2026-07-18: appears already built — the scene's horse Animator has working `Speed`/
        `Turn`/`Charge`/`Rear`/`Death` params per `HorseAnimation`'s own param-presence check
        logging no warnings in a play-mode smoke test — but the blend tree states/clips weren't
        individually verified, so leaving this unchecked pending an explicit look in the Editor.)*
- [ ] **Knight prefab** (`Knight Enemy (Mounted).prefab`): knight ROOT with enabled `Health` (+
      HealthSO), `Enemy`, `CoinDropper`, `DamageNumberSpawner`, `DisableCollidersOnDeath`,
      `CorpseDespawner`, `EnemyRagdoll`, `MountedKnightRider`, `MountedKnightBrain`, optional
      `PowerupDropper`, body capsule + `VulnerableSpot` head child; **disabled** (enabled at
      dismount): `NavMeshAgent` (goblin-sized), `AIMovement`, `AIAttack`, `AIAnimation`,
      `ImpulseReceiver`, `KnockbackReceiver`. Nest the Horse prefab as a child at the root origin
      (`MountedKnightRider.Awake` detaches it at runtime); most refs auto-wire via `OnValidate`,
      assign `riderSeat` + the SOs + `telegraphPrefab` by hand.
  - [ ] **Knight model**: try `Undead_Knight.fbx` (same Malbers folder) imported as **Humanoid**
        and retarget the goblin animator controller; if the rig won't map, fall back to a Synty
        goblin rig with knight-ish materials. Add a `Riding` bool state (seated pose from the
        Malbers `Rider/` clips, e.g. `Rider_Weapon_Sword` idle) and a `Dismount` trigger
        (`Rider_Mount_Dismount_Left` or a simple cut); keep the stock `Attack`/`Death`/`Cheer`.
  - [ ] **Telegraph prefab**: a flat ground quad (the Troll telegraph precedent) — it gets scaled
        to (laneWidth, y, laneLength) and oriented along the charge; assign on
        `MountedKnightBrain.telegraphPrefab`. Optional `MMF_Player`s: rear whinny → `rearFeedback`,
        charge thunder → `chargeFeedback`.
  - [ ] Register the prefab in `WaveSpawner.enemyPrefabs` under id `knight` (row already in
        `Config/Enemies.csv`).
- [x] **Player prefab**: add `PlayerMount` (assign the sword `DamageTrigger` explicitly — the
      VampiricBlade precedent; fill `componentsToDisableWhileMounted` with the
      `SamplePlayerAnimationController`, `CombatFacing`, `AttackCancelsSprint` — NOT `InputReader`/
      `PlayerAttack`/`PlayerBow`, they stay live for mounted combat, and NOT `PlayerMount` in
      `PlayerDeath`'s list), `MountedCombat`, `MountedCombatLook`, `StartMountedSpawner` (assign
      the Horse prefab). Everything else auto-wires.
      *(2026-07-18: verified via MCP — `PlayerMount`/`MountedCombat`/`MountedCombatLook` are all on
      `SidekickSyntyCharacter`; `PlayerMount.swordTrigger` is explicitly assigned to `1H_Sword`'s
      `DamageTrigger`, `bow` is wired, and `componentsToDisableWhileMounted` has 3 entries filled.
      **`StartMountedSpawner` is NOT present** — and can't be usefully added yet since there's no
      Horse prefab to assign it (see below). Re-open this once the Horse prefab exists.)*
  - [x] **Mount rear no longer locks movement** *(2026-07-18)*: mounting used to call
        `HorseMotor.TriggerRear()`, which held `targetSpeed`/`TurnInput` at zero for
        `HorseSO.rearSeconds` (~1.2s) as if it were the knight's charge telegraph — so a
        freshly-mounted player couldn't move until the rear finished. `TriggerRear()` was the only
        caller of that lock (the knight's own telegraph calls `HorseAnimation.TriggerRear()`
        directly, bypassing `HorseMotor` entirely), so the lock was dead weight for its one actual
        use. Simplified `HorseMotor.TriggerRear()` to just play the animation — cosmetic only, no
        movement lock — and removed the now-unused `IsRearing`/`rearRoutine`/`HorseSO.rearSeconds`.
        Compile-checked clean; play-mode smoke test showed no new errors.
  - [ ] **Player animator**: add `IsMounted` (Bool) + `HorseSpeed` (Float) params and a "Riding"
        layer — seated idle/gait blend on `HorseSpeed` from the Malbers `Rider/` clips — plus an
        upper-body-masked attack layer above it **reusing the existing sword attack clips** (their
        `OneHandedSwordAttack`/`PlaySwordWoosh` animation events must ride along, or the mounted
        sword deals no damage); the bow layer sits above Riding so mounted archery poses compose.
        Until wired, `PlayerMount` logs a one-time warning and everything works with the rig stuck
        in its ground pose.
- [ ] **Scene**: optionally place one riderless Horse prefab in `Bladehold Test Scene` for quick
      mount testing (it needs no NavMesh; the player horse is CharacterController-driven).
- [ ] **Skill icons**: `horse_unlock`/`horse_health`/`horse_speed`/`horse_heal`/`horse_archery`
      (gold tree) and `start_mounted` (Reincarnate) have blank icons — assign in
      **Bladehold > Skill Tree Editor** when art exists.
- [ ] **Balance pass**: the `knight` CSV row, `HorseSO`, `MountedKnightSO`, horse `HealthSO`, the
      `horse_*` node costs/positions, and `PlayerMount.mountedReachBonus` (0.6).

## Manual verification (Mounted Knight + Horse)

- [ ] Reach wave 7 (or dev-set next wave) — one knight spawns riding the horse; it circles at a
      ~12m standoff instead of closing to melee.
- [ ] Telegraph: the horse turns to face you, **rears** (front-legs clip) with a ground lane shown,
      then charges dead straight along it — sidestepping the lane avoids everything; near a wall
      the lane (and the run) is visibly shorter, and the horse never leaves the NavMesh.
- [ ] Standing in the charge hurts (~16) and goblins in the lane ragdoll-fling exactly like
      Impulse-buff hits; the charge is never parried; nothing is hit twice per pass.
- [ ] Sword/arrow hits on the horse OR the knight both pop damage numbers on the knight; below half
      HP he lands beside the horse and fights exactly like a goblin (chase, melee, knockback,
      Impulse fling); Impulse hits while mounted never fling him, they just unseat him faster.
- [ ] After the unseat the horse idles alive at **full HP**, is damageable, and shows no rider.
- [ ] Kill the knight while still mounted (burst / `DebugWipeWave`) — corpse drops beside the
      horse, coins + kill credit + corpse sink all run, the horse survives, and the **wave clears
      with the horse alive**.
- [ ] Without Saddle Up: jumping into the saddle does nothing. Buy it — jumping into a riderless
      horse's saddle mounts (walking through the trigger does not); the camera follows.
- [ ] W/S accelerates/brakes, A/D turns with gait blending; holding Shift at speed charges —
      goblins ahead take damage and ragdoll-fling; X (and gamepad East after the input-class
      regen) dismounts beside the horse.
- [ ] Mounted, the player takes ZERO damage while enemy hits visibly drain the horse's HP; at 0 the
      horse plays `H_Death01`, the player lands beside the corpse with controls back, and the dead
      horse can't be re-mounted. Solid/Parry cooldowns are NOT consumed by mounted hits.
- [ ] Mounted sword: swings connect noticeably farther with **no visible blade change**, and never
      damage your own horse; dismounted, the reach returns to normal.
- [ ] Bow from horseback only works with Horse Archer (aiming does nothing without it); arrows
      never stop on the horse's own body.
- [ ] Ride over coins/orbs/health packs — the player's wallet/buffs/health collect them (riderless
      horses collect nothing); with Stable Diet, packs also heal a wounded horse.
- [ ] Barded Steed raises the ridden horse's max HP (fraction-preserving on a wounded horse);
      Thoroughbred visibly raises its speed.
- [ ] Reincarnate (wiping the gold tree), buy Cavalier — the next run starts already mounted and
      riding still works with zero gold-tree nodes; restart-from-wave while mounted reloads clean.
- [ ] Die on foot next to a riderless horse — the corpse doesn't mount; die mid-ride (if ever
      possible) — controls stay dead.

## Bomber enemy + Flaming Arrows skill line — Unity Editor wiring

The C# is done: **Bomber** (`Enemies/BomberAttack.cs` + `Enemies/BomberAttackSO.cs`) chases with a
torch; within `triggerRange` (8m) it plants for a short ignite pause (`AIMovement.SetMovementPaused`,
the Troll wind-up precedent), lights the dynamite (a `LightFuse` animator trigger, torch hidden,
spark visuals shown), then sprints at `fuseSpeedMultiplier`× via the new
`AIMovement.SetSpeedMultiplier` (folded into `BaseSpeed` so `SlowStatus` slows compose with it).
`fuseSeconds` (5s) after lighting it explodes: AoE damage + impulse fling to everything in
`explosionRadius` except itself (the `TrollSlamAttack` shape — elemental, `unparryable`), then
force-kills itself through `Health.ReceiveDamage` so wave/coin/corpse accounting runs (the
`ImpulseReceiver` precedent). Killed before the fuse burns down = no explosion, sparks out.
**Flaming Arrows** (`flamearrow_1..5` in `Config/SkillTree.csv`, fresh root column at x=18):
`PlayerBow` deals `StatType.FlamingArrowsDamagePercent` (25% from the unlock) of each arrow hit as
a separate elemental fire hit, and rolls `StatType.FlamingArrowsBomberDetonateChance` (10-50%) per
arrow hit to call `BomberAttack.Detonate()` — rolled *before* the arrow damage lands so a lethal
arrow still gets its explosion (corpses never explode). New `bomber` row in `Config/Enemies.csv`
(unlocks wave 5, 20% chance, 1 guaranteed ramping to 3 concurrent, resistance 2);
`WaveSpawner.ApplyDefinition` routes the CSV damage column to `BomberAttack.SetDamage`.

- [ ] **Create SO asset instance**: a `BomberAttackSO` (menu `Scriptable Objects/BomberAttackSO`) —
      tune `triggerRange` (8), `igniteSeconds` (0.6), `fuseSeconds` (5 — total from lighting to
      boom, ignite pause included), `fuseSpeedMultiplier` (1.6), `explosionRadius` (4), `damage`
      (25 — the CSV column overrides it per spawn), `impulsePower` (3) / `impulseForce` (12).
- [ ] **Bomber prefab** (a goblin variant works as the base): `Health`, `Enemy`, `AIMovement` (its
      own or the goblin `AIMovementSO` — the CSV speed 5.5 overrides it), `AIAnimation`,
      `BomberAttack` (assign `attackData`; `animator`/`health`/`movement`/`targetSelector`
      auto-wire via `OnValidate`), `CoinDropper`, `CorpseDespawner`, `KnockbackReceiver`,
      `EnemyRagdoll` + `ImpulseReceiver`, optional `GoldenGoblin`/`ImpulseGoblin`/
      `AITargetSelector`, and a `VulnerableSpot` head collider (arrow headshots).
  - [ ] **Props/VFX on the prefab**: a torch prop in one hand → `torchVisual`; dynamite/spark
        objects (e.g. one per hand, each with a sparking `ParticleSystem`), authored **inactive** →
        `fuseSparkVisuals`; an explosion VFX prefab → `explosionVfxPrefab`; optional
        `igniteFeedback`/`explodeFeedback` `MMF_Player`s (fuse hiss, big boom).
  - [ ] **Animator**: add a `LightFuse` trigger + a short crouch/ignite state on the bomber's
        controller (the `BomberAttack.lightFuseTrigger` default), timed to roughly `igniteSeconds`.
  - [ ] Register the prefab in `WaveSpawner`'s `enemyPrefabs` list under id `bomber` (row already
        in `Config/Enemies.csv`).
- [ ] **Skill icons**: the `flamearrow_*` rows have blank icons (no fire sprite registered yet) —
      assign in **Bladehold > Skill Tree Editor** when art exists.
- [ ] **Balance pass**: tune the `bomber` CSV row, the `BomberAttackSO` numbers, and the
      placeholder `flamearrow_*` costs/positions to taste.

## Manual verification (Bomber + Flaming Arrows)

- [ ] Reach wave 5 (or lower `unlockWave`) — a bomber spawns and runs at the player holding the
      torch, faster than a regular goblin.
- [ ] Get within ~8m — it stops, plays the ignite animation (torch swaps for sparking dynamite in
      both hands), then sprints at you noticeably faster; ~5s after lighting it explodes, hurting
      you if you're inside the radius and damaging/flinging any goblins caught in it.
- [ ] Outrun the blast — standing outside the radius when it pops takes no damage; the bomber dies
      in its own explosion either way (coins drop, wave count decrements, corpse pipeline runs).
- [ ] Kill the bomber before the fuse burns down (without the detonate roll) — sparks go out,
      **no explosion**, normal death.
- [ ] The explosion is never parried (elemental + unparryable), even with Parry maxed.
- [ ] Buy `flamearrow_1` — every arrow hit now pops a second, smaller damage number (~25% of the
      arrow's) on the same target; Multi Shot side arrows each get their own fire hit; bounce hits
      don't reroll it.
- [ ] Shoot bombers with `flamearrow_1` — roughly 1 in 10 hits detonates one on the spot (full
      explosion where it stands, nuking its own horde); higher tiers detonate visibly more often.
- [ ] A bomber the arrow would have killed anyway still explodes on a winning roll (the roll
      happens before the arrow damage lands).
- [ ] Shooting a bomber **corpse** never explodes it.
- [ ] Die (or lose a gate) while a fuse is burning — the bomber fizzles (sparks out, no explosion)
      and celebrates with the rest.
- [ ] Slow a lit bomber (Freezing Draw / Brain Freeze) — the slow visibly reduces the sprint, and
      when it expires the bomber returns to *sprint* speed, not walking pace (the multiplier and
      the slow compose).

## Parry + Counterstrike skill lines — Unity Editor wiring

The C# is done: `Player/Parry.cs` hooks `Health.TryBlockDamage` with a chance roll
(`StatType.ParryChance`) gated on facing the attacker (dot product of the player's forward vs. the
direction to `Damage.sourcePosition`) — only "melee" (sharp/blunt) hits qualify, elemental hits
(Storm Witch) can never be parried, and neither can anything stamped `Damage.unparryable` (the
Troll's ground slam — a wide AoE with no single directional swing to read/block).
`Player/Counterstrike.cs` listens to the new `Parry.OnParried` event (the `VampiricBlade`/sword-
`OnHit` precedent) and deals `StatType.CounterstrikePercent` of effective sword damage back to the
attacker via the new `Damage.source` field, which `AIAttack` and `TrollSlamAttack` now stamp with
their own `Health` alongside `sourcePosition` (previously melee attacks against the player didn't
set either). New `parry_*`/`counter_*` rows already in `Config/SkillTree.csv` (fresh column at
x=6, y=0-8, chained off `solid_1`).

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [x] Add a `Parry` component on the player root (next to `Health`/`DamageBlocker`); optionally
        assign a `parryFeedback` `MMF_Player` (a parry clang/flash) and tune `facingDotThreshold`
        (0.3 default — wider than dead-on, but not the whole front hemisphere).
        *(2026-07-16: added on `SidekickSyntyCharacter` next to `Health`/`DamageBlocker`; `parryFeedback` left unassigned, threshold at default.)*
  - [x] Add a `Counterstrike` component on the player root; `parry` auto-wires via `OnValidate`
        (`GetComponent<Parry>()`). *(2026-07-16: added + `parry` ref verified wired.)*
- [x] **Skill icon**: `parry_*`/`counter_*` reuse already-registered icon names
      (`Warriorskill_18_block`, `IncreaseStrength_2/3/4_nobg`), so no new icon drag-and-drop should
      be needed — confirm they render in **Bladehold > Skill Tree Editor**. *(2026-07-19: verified
      via MCP — the rows now use `Paladinskill_43_dodge`/`Skill_Parry_nb` and both resolve through
      `SkillTreeSO.GetIcon`.)*
- [ ] **Balance pass**: tune the placeholder costs/positions of the new rows to taste.

## Manual verification (Parry + Counterstrike)

- [ ] Buy a `Parry` tier — facing a goblin as it lands a melee hit sometimes blocks it entirely (no
      health loss, no damage feedback); getting hit from behind or the side never parries even at
      100% rolled luck.
- [ ] A Storm Witch's lightning ball/storm damage is never parried, even with `Parry` maxed.
- [ ] Without `Counterstrike` bought, a successful parry blocks damage but the attacker takes
      nothing back.
- [ ] Buy a `Counterstrike` tier — a successful parry now also damages the goblin that hit you
      (damage number on the goblin), scaling with the node's %; the sword's own swing damage is
      unaffected.
- [ ] Face a Troll and let its ground slam land on you — it's never parried (no block, no
      counterstrike) even with `Parry`/`Counterstrike` maxed; you just take the damage normally.
- [ ] The existing `Solid` auto-block still works independently (both can trigger; whichever's
      handler runs first on a given hit wins that hit).



## Bow weapon + bow skill lines + Raw Power — Unity Editor wiring

- [ ] **Skill icons**: the new `multishot_*` and `multidmg_*` rows have blank icons (no bow-ish
      sprite is registered yet) — drop sprites on them in **Bladehold > Skill Tree Editor** when
      suitable art exists; the other new rows reuse already-registered icon names.


## Frost/orb/conduit skill lines — Unity Editor wiring

The C# for eight new skill lines is done: **Freezing Draw** (`Player/FreezingDraw.cs` — slows
enemies near the player while the bow is drawn), **Brain Freeze** (headshots chill the target) and
**Elongated Freeze** (slows linger longer) via the new runtime-added `Enemies/SlowStatus.cs` (scales
`NavMeshAgent.speed` + `Animator.speed`, no prefab wiring — the `EnemyRagdoll` lazy-build idiom;
`AIMovement.BaseSpeed` is the restore source), **Ice Breaker** (sword bonus vs slowed enemies, in
`DamageSystem/DamageTrigger.cs`), **Exploding Heads** (headshot impulse blasts) / **Arrows of
Midas** (`GoldenGoblin.TryConvertToGolden`) / **Unstable Orbs** (main arrow detonates orbs — orbs'
new `TryDetonate`, `ChainLightning.ForceChain`) in `Player/PlayerBow.cs`, and **Conduit** (reduced
lightning-ball damage + chain proc, in `Enemies/LightningBall.cs`, bases registered by
`ChainLightningBuff`). New CSV rows: `freezedraw_*`, `brainfreeze_*`, `elongfreeze_*`,
`icebreaker_*`, `explodeheads_*`, `midas_*`, `conduit_*`, `unstableorbs_1`.

- [x] **Player prefab**: add a `FreezingDraw` component next to `PlayerBow`; assign `config` (the
      same `BowSO` asset) and set `enemyLayers` to the enemy layer (exclude player/environment).
      `bow`/`stats` auto-wire via `OnValidate`.
- [x] **BowSO asset**: tune the new fields — `freezingDrawRadius` (8), `brainFreezeSeconds` (3 —
      the `brainfreeze_*` descriptions assume this), and the shared impulse-blast tunables
      `impulseBlastRadius` (4) / `impulseBlastPower` (2 — flings default-resistance goblins; raise
      to topple brutes) / `impulseBlastForce` (10).
- [x] **Vulnerable spots**: Brain Freeze and Exploding Heads trigger on the same `VulnerableSpot`
      head colliders as Precision Shot — the wiring item in the bow section above covers all three.
- [ ] **Skill icons**: the `freezedraw_*`, `brainfreeze_*`, and `elongfreeze_*` rows have blank
      icons (no frost sprite registered yet) — assign in **Bladehold > Skill Tree Editor** when art
      exists. The other new rows reuse registered names.
- [ ] **Balance pass**: costs/positions of the new rows are placeholders (frost branch sits at
      y6-y8 under the bow region, Conduit at x9-10/y3-4 by the lightning branch).
- [ ] **Optional polish (future)**: slows have no VFX/tint yet — a frost material swap or aura on
      `SlowStatus` would telegraph the state; an `MMF_Player`/VFX on the impulse blasts and orb
      detonations (currently the orb just plays its pickup feedback and vanishes).

## Manual verification (frost/orb/conduit skills)

- [ ] Buy Freezing Draw, hold aim near goblins — they visibly crawl (movement and animation) while
      the bow is drawn, and resume full speed ~instantly after releasing aim (or lingering, with
      Elongated Freeze bought).
- [ ] Buy Brain Freeze (after head colliders exist) — a headshot slows that goblin for ~3s; body
      shots don't. The slow expires and speed restores exactly.
- [ ] Buy Ice Breaker — sword hits on a chilled goblin show boosted damage numbers; unslowed
      goblins take normal damage. Death Nova damage is unaffected (it isn't melee).
- [ ] Buy Exploding Heads — a headshot detonates a blast that damages/flings the goblins around the
      victim for the node's % of the arrow's damage.
- [ ] Buy Arrows of Midas — roughly 1 in 20 arrow hits (at tier 1) turns a live goblin gold
      mid-fight (material swap + bonus coin on death); already-golden goblins are unaffected.
- [ ] Buy Conduit, let a Storm Witch ball hit you — damage taken drops by the node's %, and ~1 in
      10 hits arcs chain lightning from the impact into nearby enemies (needs the Chain Lightning
      unlock for bounces/damage to be non-zero).
- [ ] Buy Unstable Orbs — shoot your main arrow through an Impulse Orb (impulse blast at the orb)
      and a Lightning Orb (chain lightning from the orb, no buff needed); the orb is consumed and
      grants no buff. Multi Shot side arrows and walk-over pickups behave as before.
- [ ] Player is never slowed by anything (SlowStatus only attaches to NavMesh enemies).


## Impulse skill line — Unity Editor wiring

- [ ] Add feedback/VFX on player when charged by an Impulse Orb (e.g. a small shockwave + SFX) so the player knows the
      buff is active.

## Storm Witch enemy + Chain Lightning skill line — Unity Editor wiring

- [ ] Add feedback/VFX on player when charged by a Storm Witch's ball (e.g. a small lightning flash + SFX) so the player knows the
      buff is active.

## Chain Lightning bolt visual (SineVFX LightningSystemChain) — Unity Editor wiring

The C# is done: `Player/ChainLightningVfx.cs` drives a single, reused SineVFX `LightningSystemChain`
so a chain draws an animated bolt through the enemies it hops across (previously only the one-off
`bounceVfxPrefab` flashed at each target). It owns a pool of world-stable anchor transforms, and
`ShowChain(points)` snaps them to the captured hop positions, assigns them as the chain's
`chainPoints`, flips `vfxEnabled` on, and auto-off after `flashDuration` (0.15s). `ChainLightning`
collects the origin + each hop into `chainPointsBuffer` and calls `ShowChain` at the end of a chain;
`chainVfx` auto-wires from `Player.Instance.GetComponentInChildren<ChainLightningVfx>()`. Reused
single instance by design — if several sword hits in one swing each chain, the most recent wins the
shared bolt (all damage still lands). Both new files added to `Assembly-CSharp.csproj`; build clean.

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): drag in a
      **`SingleVFXOnly`** Chain prefab as a child of the player —
      `Assets/Third Party/SineVFX/LightningSystem/CompleteEffectsPrefabs/SingleVFXOnly/Chain/LS_Chain_0X.prefab`
      (pick a look; the `04_ColorBlend` variants read as electric-blue). **Not** a `WithExampleMeshes`
      variant — those carry visible demo spheres. Clear/ignore its authored `chainPoints` (we overwrite
      them at runtime).
      *(2026-07-18: verified via MCP — `LS_Chain_04_ColorBlend_01` is a child of
      `SidekickSyntyCharacter`, carrying `LightningSystemChain`.)*
- [x] Add a **`ChainLightningVfx`** component on the player root; assign its `lightningChain` to the
      child `LightningSystemChain` (auto-wires via `OnValidate` if it's the only one in children).
      Defaults for `flashDuration` (0.25) and `maxAnchors` (16) are fine. `ChainLightningVfx` now
      **forces `autoScaleEnabled = false` and sets `masterScale = boltScale` at startup** — the raw
      prefab ships with `autoScaleEnabled = true` and a null `autoScaleAnchor`, which NREs every frame
      in `ProcessAutoScale()` and stops the bolt rendering, so this must stay code-driven. No manual
      autoScale wiring needed.
      *(2026-07-18: verified via MCP — `ChainLightningVfx` present with `lightningChain` wired to
      `LS_Chain_04_ColorBlend_01`, `flashDuration` 1.0, `maxAnchors` 16.)*
- [x] On the existing **`ChainLightning`** component, leave `chainVfx` blank to auto-wire, or drag the
      `ChainLightningVfx` in explicitly. (The old `bounceVfxPrefab` per-target flash still works and
      complements the bolt — keep or clear as desired.)
- [x] Tune `ChainLightningVfx.boltScale` (default 1.5) so the arc reads at gameplay camera distance;
      bump it up if the bolt still looks too thin for enemy-to-enemy spans.
      *(2026-07-18: still at the default 1.5 — nothing bumped, but the wiring itself is complete;
      re-tune only if it reads too thin in play.)*

## Manual verification (chain lightning bolt)

- [ ] Buy Chain Lightning, pick up a Lightning Orb, hit a goblin in a crowd → a visible animated bolt
      arcs from the hit point through each chained goblin (matching the damage numbers), then fades.
- [ ] Kill/scatter enemies so a chain finds no second target → no bolt (needs ≥2 points), no errors.
- [ ] Run around while chaining → the bolt stays anchored in the world for its flash (doesn't drag
      along with the player).


## Manual verification (skill icons + new skill lines)

- [ ] Buy Solid, take a goblin hit → first hit negated (no damage number/feedback), next hits land
      normally until the cooldown elapses; higher tiers shorten the window.
- [ ] Buy Amplified Knockback with Heavy Strike owned → a fully charged swing shoves goblins visibly
      further than an uncharged one, scaling per tier.

## Combat facing (stationary attack/aim turns to camera) — Unity Editor wiring

The C# is done: `Player/CombatFacing.cs` yaw-rotates a stationary player toward the camera heading
while the attack button is held or the bow is aiming, filling the gap where the Synty controller's
stationary strafe branch only drives the turn-in-place animator offset and never rotates the root.
Attack hold comes from `InputReader` press/release events; bow aim reads `PlayerBow.IsAiming`;
movement is detected via the `CharacterController`, so the controller's own strafe rotation takes
over untouched the moment the player moves.

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): add a `CombatFacing`
      component on the player root. `inputReader`/`bow`/`characterController` auto-wire via
      `OnValidate`; `facingCamera` defaults to `Camera.main`. Defaults for `rotationSmoothing` (10,
      matching the controller) and `stationarySpeedThreshold` (0.1) should be fine.
      *(2026-07-16: was already on `SidekickSyntyCharacter` with all refs wired — verified.)*
- [x] **PlayerDeath**: add the new `CombatFacing` component to `PlayerDeath`'s inspector list of
      control components it disables on death, so a corpse holding attack doesn't keep turning.
      *(2026-07-16: appended to `componentsToDisable`.)*

## Manual verification (combat facing)

- [ ] Stand still, hold left click (sword), and swing the camera around → the character smoothly
      turns to face where the camera looks, both mid-hold and during a held charge.
- [ ] Stand still, hold right click (bow aim), and swing the camera → same smooth turn; arrows and
      the body agree on direction (the `BowAimLook` spine bend still lines up).
- [ ] Attack/aim **while moving** → rotation feels exactly as before (the controller's strafe
      rotation, no fighting or jitter at the moving/stationary boundary).
- [ ] Release attack/aim while stationary → the character stops tracking the camera and idle
      turn-in-place behaviour is back to normal.
- [ ] Die while holding attack → the corpse doesn't rotate with the camera (requires the
      `PlayerDeath` list wiring above).

## Berserker class prototype — Unity Editor wiring

The C# lands in stages (A: class infrastructure, B: reincarnate class select, C: throwing axe,
D: rage + pain into power, E: berserker skill tree + rage bar). **Stage A is done in C#**:
`Player/ClassDefinitionSO.cs` (per-class asset: id, display name/blurb, optional
`AnimatorOverrideController`, per-class `chargeTimePerLevel`, optional per-class `SkillTreeSO`),
`Player/PlayerClassController.cs` (on the player root; in **Awake** — strictly before every Start —
reads `SaveData.playerClassId`, activates the chosen slot's weapon GameObjects and class components
while deactivating the rest (an inactive weapon never registers the shared `SwordDamage`/range/crit
bases, so two `readsPlayerStats` triggers can't clobber each other), re-points
`AnimationEvents`/`VampiricBlade`/`ChainLightning`/`ImpulseHitFeedback`/`PlayerMount` at the active
melee `DamageTrigger` via new setters, applies the class's animator override + charge pacing).
`SkillTreeService.Start` now adopts the active class's `skillTree` when set (saved ids missing from
the active tree were already skipped, so the other class's purchases just go dormant).
`SaveData.playerClassId` ("swordsman" default, wiped by `ResetProgress`). DevConsole gained a class
◄/► picker + "Switch & Reload" cheat; telemetry `run_start` now logs `class=` and binds only the
**active** weapon's trigger.

- [ ] **Axe weapon prefab** (`Bladehold Prefabs/1H_Axe.prefab`): duplicate `1H_Sword.prefab`, swap
      the mesh for an axe model, create a new `DamageSO` (~1.4× sword baseDamage) + `DamageTriggerSO`
      for it, keep BladeSweep `Blade Base`/`Blade Tip` on the new mesh and `readsPlayerStats` ON.
      Nest under the same right-hand bone in `Player.prefab`, **inactive** by default. Re-assign the
      copy's out-of-prefab refs (they don't survive duplication): `DamageTrigger.playerAttack`,
      `SwordHitFeedback.animator`, `SwordChargeFeedback.playerAttack`.
- [ ] **Berserker AnimatorOverrideController** based on `AC_Sidekick_Masculine.controller`: override
      the Attack-layer 1H clip with `Assets/Third Party/Kevin Iglesias/Human Animations/Animations/
      Male/Combat/2H/HumanM@Attack2H01.fbx` (or a Synty `A_MOD_SWD_Heavy*` clip). **Bake animation
      events on the new clip's import settings**: `PlaySwordWoosh` early in the swing and
      `OneHandedSwordAttack` on the impact frame — same names as the 1H clip, so `AnimationEvents`
      routes them to whichever weapon is active. A missing event = silent no-hit swings.
- [x] **ClassDefinitionSO assets ×2** (menu `Scriptable Objects/ClassDefinitionSO`):
      `swordsman` (displayName "Swordsman", null override, chargeTimePerLevel 1.0, null skillTree)
      and `berserker` (displayName "Berserker", the override controller, ~1.2, skillTree left null
      until Stage E). *(2026-07-16: both created in `Bladehold Scripts/Player/` with
      id/displayName/description only — berserker's `animatorOverride` and ~1.2 chargeTime still
      pending the Stage A axe/animator work.)*
- [ ] **Player.prefab**: add `PlayerClassController` to the root. Slot 0 = swordsman: weaponObjects
      `[1H_Sword]`, meleeTrigger/hitFeedback = the sword's, classComponents `[PlayerBow,
      FreezingDraw]`. Slot 1 = berserker: weaponObjects `[1H_Axe]`, the axe's trigger/feedback,
      classComponents empty for now (gains `PlayerThrownAxe`/`RageBuff`/`PainIntoPower` in stages
      C/D). The shared-component refs (`AnimationEvents`, `PlayerAttack`, `VampiricBlade`,
      `ChainLightning`, `ImpulseHitFeedback`, `PlayerMount`, child `Animator`) auto-wire via
      OnValidate — verify they resolved.

**Manual verification (Stage A)**
- [ ] Swordsman unchanged end-to-end: swing, charge, bow, vamp/lightning, mount, tree, reincarnate.
- [ ] Dev console (backquote) → Class picker → "Switch to Berserker & Reload": axe in hand, sword
      gone; axe swings deal the axe SO's damage; sword skill nodes (Sharpened Edge etc.) scale it.
- [ ] Console shows no base-clobber weirdness: only one weapon's `DamageTrigger` registers bases.
- [ ] Telemetry CSV `run_start` row carries `class=berserker` after the switch.

**Stage B (reincarnate class select) — done in C#**: `UI/ClassSelectPanel.cs` (authored buttons, one
per `ClassDefinitionSO`; fills name/description labels, toggles a selected highlight, pre-selects
the saved class so an untouched panel keeps it) and `DeathScreen` hooks: the panel appears with the
Reincarnate tree on the first Reincarnate click, and the second click ("Begin Next Life") persists
`SelectedClassId` via `PlayerClassController.SetSavedClass` right before the scene reload.

- [x] **Class-select panel** under the death-screen canvas, next to `reincarnateTreePanel`:
      a `ClassSelectPanel` with two authored buttons (Swordsman / Berserker), each with a name TMP
      label, an optional description TMP label, and an optional selected-highlight object. Assign
      the two `ClassDefinitionSO` assets, then assign the panel on `DeathScreen.classSelectPanel`.
      Active/inactive state doesn't matter — `DeathScreen.Start` hides it until points are banked.
      *(2026-07-16: built — `ReincarnateSkillTree` narrowed to 1200 wide at x=-280, panel in the
      freed right strip; two Synty-framed cards (weapon icon + name + description + gold glow
      highlight, `AC_Button_FantasyWarrior_Basic_01` animator), options + `DeathScreen.classSelectPanel`
      wired. Play-mode verified: pre-selects saved class, click switches highlight/`SelectedClassId`.)*

**Manual verification (Stage B)**
- [ ] Die → Reincarnate: class panel appears beside the Reincarnate tree, current class highlighted.
- [ ] Pick Berserker → "Begin Next Life": next run is the Berserker with an empty gold tree.
- [ ] Reincarnate without touching the panel: class unchanged.

**Stage C (throwing axe) — done in C#**: `Player/PlayerThrownAxe.cs` (the `PlayerBow` skeleton:
hold aim = wind-up in charge levels tuned on `Player/ThrownAxeSO.cs`, press attack = throw — a
hitscan **sphere cast**, a straight line with `AxeThrowWidth` metres of width, damaging + knocking
back every unique enemy along it with a fresh crit roll each, up to a pierce budget
(`AxeThrowPierceCount` + charge × `piercePerChargeLevel`), stopping at environment or when the
budget runs out; melee suppressed while aiming exactly like the bow; **`AxeThrowUnlocked` base is
temporarily 1** until the `axe_unlock` node ships in Stage E). `Player/AxeProjectileVisual.cs`
(cosmetic spinning axe flying to the line's end, the `BowTracer` sibling).
`Player/IChargedAimWeapon.cs` + `Player/AimWeaponResolver.cs`: `BowAimCamera`, `BowCrosshairUI`,
and `BowReloadUI` now poll whichever aim weapon the active class carries (serialized bow while
enabled, else `PlayerClassController.ActiveAimWeapon`) — aim-camera framing values surface through
the interface from each weapon's SO, so `BowAimCamera` no longer needs its `BowSO` reference.
7 new StatTypes (`AxeThrow*`). `PlayerAttack` also skips melee charge while the axe aims.

- [ ] **ThrownAxeSO asset** (menu `Scriptable Objects/ThrownAxeSO`): defaults are authored in-code
      (dmg 25, range 25 m, cooldown 0.6 s, width 0.6 m, pierce 2, charge 3 levels @ +50%/level,
      knockback 6 + 25%/level; aim-camera block mirrors BowSO).
- [ ] **Throwing-axe projectile prefab**: an axe mesh + optional trail with `AxeProjectile`
      (renamed from `AxeProjectileVisual` — see the projectile-axe follow-up section below);
      assign on `PlayerThrownAxe.projectilePrefab`. **Now required, not cosmetic** — the projectile
      carries the throw's damage.
- [ ] **Player.prefab**: add `PlayerThrownAxe` to the root **disabled**; assign the SO; optional
      `meleeWeaponModel` (the 1H_Axe) / `thrownAxeModel` (an axe prop in the throwing hand) for the
      in-hand swap while aiming. Add the component to the **berserker slot's classComponents** on
      `PlayerClassController` so the class system enables it (and the aim UI finds it).
- [ ] Optional: berserker override controller re-skins the bow layer's aim/fire states with 2H
      wind-up/throw poses (the axe drives the same `IsAiming`/`BowFire` params).

**Manual verification (Stage C)**
- [ ] Berserker: hold aim → camera shoulders in, crosshair fades in and tightens with charge;
      release → back to normal. Attack while aiming throws (no melee swing).
- [ ] A charged throw pierces more goblins in a visible line, knocks them back harder; the reload
      radial runs between throws.
- [ ] Swordsman: bow behaves exactly as before (camera/crosshair/reload unchanged).
- [ ] Mounted berserker: aiming does nothing (no mounted throwing yet — intended).

**Stage D (rage + pain into power) — done in C#**: `Health` gained its third alter-hook,
**`ScaleDamageTaken`** (`event Func<Damage, float>` — handlers return multipliers, products
combined, `Damage.value` scaled in place so damage numbers/telemetry/knockback all see the
mitigated value; CLAUDE.md's Health paragraph updated). `Player/RageSO.cs` + `Player/RageBuff.cs`
(the rage meter: builds from dealing damage — melee `OnHit` via the class controller's active
trigger, plus `PlayerThrownAxe.OnHit` — and ×4 from taking damage; drains after an idle grace;
at full meter +50% damage (read by `DamageTrigger`/`PlayerThrownAxe` like `ImpulseBuff`), +20% move
speed (quantized signed-delta MoveSpeed percent modifiers), −30% damage taken (the new hook); gain
and retention are the upgradeable half via `RageGainMultiplier`/`RageRetentionMultiplier`, base
1.0). `Player/PainIntoPower.cs` (damage taken while melee-charging or axe-aiming banks
`PainIntoPowerPercent` — base 0 = locked — of it; consumed once per melee activation /
throw and added **flat** to every target of that one attack). DevConsole shows a live
`Rage 62/100 | Pain +8` readout for the Berserker. 3 new StatTypes.

- [ ] **RageSO asset** (menu `Scriptable Objects/RageSO`): defaults authored in-code (max 100,
      +1 rage/dmg dealt, +4 rage/dmg taken, 3 s grace then −10/s; at full: +50% dmg, +20% move,
      −30% taken).
- [ ] **Player.prefab**: add `RageBuff` (assign the RageSO) and `PainIntoPower` to the root,
      both **disabled**; add both to the berserker slot's `classComponents` on
      `PlayerClassController`.

**Manual verification (Stage D)**
- [ ] Berserker: rage climbs on hits dealt AND (faster) on hits taken; decays after ~3 s idle.
- [ ] At high rage: bigger damage numbers, visibly faster run, smaller incoming damage numbers.
- [ ] Hold a charged swing / axe wind-up, tank a goblin hit, release → that attack's numbers jump
      (needs a `pain_1` node or a temporary PainIntoPowerPercent base > 0 to see it pre-Stage-E).
- [ ] Swordsman: no rage readout in the dev console, no behaviour change.

**Stage E (berserker skill tree + rage bar + telemetry) — done in C#**:
`Config/SkillTreeBerserker.csv` — 32 rows carried over from the gold tree (all melee/crit/charge/
knockback/vamp/impulse/lightning/conduit/parry/solid/economy/mount lines work on the axe unchanged,
descriptions re-worded sword→axe) with the 17 bow rows dropped (incl. horse_archery), prereqs
scrubbed (`sword_dmg`/`sprint` now link to `axe_unlock`), plus 9 new nodes in the old bow footprint:
`axe_unlock` (Throwing Axe, 60g), `axe_dmg` (Heavier Head ×7), `axe_pierce` (Skull Splitter ×5),
`axe_width` (Wide Arc ×4), `axe_knock` (Crushing Throw ×4), `axe_charge` (Wound Up ×3, +1 level
+25%/level), `pain_1` (Pain into Power ×4: 50/75/100/125%), `rage_fury` (Boiling Blood ×5,
+15%/lvl gain), `rage_retain` (Slow Burn ×4, +20%/lvl retention). `AxeThrowUnlocked` base flipped
back to **0** (locked until `axe_unlock`). `UI/RageBarUI.cs` (polls `RageFraction`, drives a Filled
Image + optional TMP label; hides itself when the class has no enabled RageBuff). `RunTelemetry`
now also accumulates thrown-axe hits into damage-dealt (bow damage was never telemetered; the axe
is included deliberately for face-tank balance reading).

- [ ] **Berserker SkillTreeSO asset** (menu `Scriptable Objects/SkillTreeSO`), csv =
      `Config/SkillTreeBerserker.csv`, hasHeaderRow on; **copy the gold tree SO's `icons` list**
      so the carried-over nodes' icon names resolve (the 9 new nodes ship without icons — add via
      the Skill Tree Editor's drag-and-drop later). Sanity-check in **Bladehold > Skill Tree
      Editor** (parse errors surface on save/reload).
- [ ] Assign the new tree SO on the **berserker `ClassDefinitionSO.skillTree`** (swordsman stays
      null = default gold tree).
- [ ] **HUD rage bar**: a Filled Image (+ frame, + optional TMP number) under the HUD canvas with
      `RageBarUI`; it hides itself for the Swordsman automatically.

**Manual verification (Stage E)**
- [ ] Berserker death screen shows the berserker tree (axe/pain/rage branch where the bow was);
      Swordsman still shows the gold tree. Node icons render on carried-over nodes.
- [ ] Fresh berserker: aiming does nothing until `axe_unlock` is bought; after buying, the full
      Stage C loop works and `axe_*` nodes visibly raise damage/pierce/width/knockback/charge.
- [ ] `pain_1`/`rage_fury`/`rage_retain` purchases visibly change the Stage D behaviours.
- [ ] Rage bar fills/drains in sync with the DevConsole readout; absent for the Swordsman.
- [ ] Telemetry damage-dealt rows include thrown-axe hits.

**Berserker follow-ups (projectile axe + Boomerang + class model swap) — done in C#**:
The throwing axe is now a **real projectile**: `Player/AxeProjectile.cs` (renamed from
`AxeProjectileVisual.cs` — no longer a cosmetic tracer, it *carries the damage*; a missing prefab
is now a Start error on `PlayerThrownAxe`). It flies at `ThrownAxeSO.projectileSpeed`
(deliberately slow, default 12 m/s) and every `FixedUpdate` **sphere casts from its last position
to its current one** (radius = `AxeThrowWidth`/2, so the existing "Wide Arc" nodes are the
axe-area skill — they widen the swept damage volume *and* scale the prop visually), damaging each
unique enemy once per leg via `PlayerThrownAxe.CreateHitDamage` (fresh crit roll per target;
charge + Pain into Power captured at release and carried by the axe) until the pierce budget runs
out or terrain lodges it; hits flow back through `PlayerThrownAxe.ReportHit`, so `RageBuff` and
telemetry `OnHit` listeners are unchanged. New **Boomerang** node (`axe_boomerang`, 120g, prereq
`axe_charge`, new `AxeBoomerangUnlocked` StatType, base 0 = locked): the axe turns around on
terrain/pierce-spend/max-range and homes back to the hand at `ThrownAxeSO.returnSpeedMultiplier`
× speed, damaging enemies on the return leg too (fresh target set + pierce budget; return ignores
terrain, despawns on catch). And classes now swap the **character model**:
`ClassDefinitionSO.characterModelPrefab` (null = keep authored, i.e. Swordsman) —
`PlayerClassController.Awake` re-binds each of the prefab's `SkinnedMeshRenderer`s onto the
existing rig **by bone name** (Synty Sidekicks share the skeleton) and disables the authored
renderers, so the Animator, animation events, weapon bones, and camera need no re-wiring.

- [ ] **Throwing-axe projectile prefab** (if not already made in Stage C): axe mesh + optional
      trail with `AxeProjectile`; assign on `PlayerThrownAxe.projectilePrefab` — now **required**.
      Inspector tunables (spin, linger, `catchRadius` 0.75 m, safety `maxLifetimeSeconds` 15)
      have sane defaults. New `ThrownAxeSO` fields (`projectileSpeed` 12, `returnSpeedMultiplier`
      1.25) pick up their in-code defaults on the existing asset — just eyeball them.
- [ ] **Berserker character model**: pick a Synty Sidekick character prefab (same skeleton as the
      player rig; only its `SkinnedMeshRenderer`s are used — bone-attached static props won't
      carry over) and assign it on the **berserker `ClassDefinitionSO.characterModelPrefab`**.
      Swordsman stays null (keeps the authored model).
- [ ] **Berserker SkillTreeSO**: reload the tree asset (or just re-save in **Bladehold > Skill
      Tree Editor**) so the new `axe_boomerang` row parses; optionally drag an icon onto it.

**Manual verification (follow-ups)**
- [ ] Throw: the axe visibly travels (slow enough to outrun briefly), damaging goblins *as it
      passes them* — damage numbers pop along the flight, not all at once on release.
- [ ] A goblin sprinting across the axe's path between physics ticks still gets hit (the
      last-to-current sweep) — hard to force, but no "walked through it unharmed" moments.
- [ ] Without Boomerang: the axe lodges in terrain / its last pierced target and despawns.
- [ ] Buy `axe_boomerang`: the axe turns around at walls/max range and flies back to the hand,
      hitting goblins on the return (an enemy straddling the turnaround point gets hit twice —
      once per leg). It despawns in the hand; no orphaned axes after 15 s even when sprinting away.
- [ ] Wide Arc purchases visibly fatten the projectile and let it clip goblins farther off-line.
- [ ] Rage still builds from axe hits; telemetry damage-dealt still includes them.
- [ ] Reincarnate into Berserker: the character model is the assigned Sidekick, animating
      normally (walk/sprint/swing/aim all play; no T-pose, no floating meshes); weapons still sit
      in the hand. Back to Swordsman: original model, no leftovers.
- [ ] Player death/ragdoll-free flows unaffected by the model swap (death anim plays on the
      swapped mesh).

## Mage class (staff + wand + elemental imbuement)

**Stage A (class shell + PlayerAttack refactor) — done in C#**:
`Player/PlayerAttack.cs` no longer hard-types `PlayerBow`/`PlayerThrownAxe` for aim suppression —
it now asks `PlayerClassController.ActiveAimWeapon` (the `IChargedAimWeapon` the class controller
already resolves in Awake), so the wand (and any future class's aim weapon) suppresses the melee
charge with zero further edits. A missing class controller (e.g. `SkillTreePreview.unity`)
degrades to "no suppression". Everything else about adding a class is data-driven — no other code.

- [ ] **Staff weapon prefab** (`1H_Staff`): duplicate the sword weapon object, swap the mesh for a
      staff, give it its own `DamageSO` (~sword-level damage — the Mage's squish is kit-side, not
      melee-nerf-side) + `DamageTriggerSO`; BladeSweep `Blade Base`/`Blade Tip` along the shaft,
      `readsPlayerStats` ON. Nest under the right-hand bone in `Player.prefab`, **inactive**.
      Re-assign the out-of-prefab refs (`DamageTrigger.playerAttack`, `SwordHitFeedback.animator`,
      `SwordChargeFeedback.playerAttack`) — they don't survive duplication (the 1H_Axe precedent).
- [ ] **Mage AnimatorOverrideController** based on `AC_Sidekick_Masculine.controller`: staff attack
      clip(s); **bake the `PlaySwordWoosh` + `OneHandedSwordAttack` animation events** on the clip
      import (missing events = silent no-hit swings).
- [ ] **ClassDefinitionSO asset** `mage` (menu `Scriptable Objects/ClassDefinitionSO`): id `mage`
      (never rename once shipped), displayName "Mage", description blurb, the override controller,
      a robed Synty Sidekick on `characterModelPrefab`, `chargeTimePerLevel` ~1.1, `skillTree` =
      the Mage tree SO (Stage E below).
- [ ] **Player.prefab**: third `PlayerClassController` slot — definition = mage SO, weaponObjects
      `[1H_Staff]`, meleeTrigger/hitFeedback = the staff's, classComponents `[PlayerWand,
      MageImbuement]` (both added below, both **disabled** on the prefab).
- [ ] **ClassSelectScreen** (superseded `ClassSelectPanel` — see the "Dedicated Reincarnate
      Class-Select Screen" entry above): third authored `ClassEntry` (button + name label +
      highlight) wired to the mage SO, and a third `keySkillIds` set on the mage
      `ClassDefinitionSO` (e.g. `[wand_unlock, light_unlock, fire_zone]` — confirm these ids exist
      in `SkillTreeMage.csv` once its imbuement nodes are named). ⚠ Land this in the same wiring
      session as the class-select screen build-out — Mage must not be confirmable on the screen
      before this slot exists (missing-trigger errors on reload).

**Stage B (wand: hold-aim magic missile) — done in C#**:
`Player/WandSO.cs` (tunables incl. aim-camera block), `Player/PlayerWand.cs` (the `PlayerThrownAxe`
skeleton: aim/charge/cooldown/melee-suppression/animator params `IsAiming`+`BowFire`/model swap;
implements `IChargedAimWeapon` so the shared aim camera/crosshair/reload UI work via
`ActiveAimWeapon`; registers `WandUnlocked` **0 = locked**, `WandDamage` 12, `WandMaxChargeLevels`
3, `WandChargeDamageBonus` 0.5, `WandKnockback` 2; damage = shared crit stats ×
`AllDamageMultiplier` × per-target Ice Breaker, `type = elemental`, stamps `Damage.source`),
`Player/MagicMissileProjectile.cs` (SphereCast-swept real projectile, no pierce; collects
`ElementNode`s it flies past and keeps going; per-element child visuals via `SetElement`).
`RunTelemetry` accumulates wand hits into damage-dealt (the axe precedent; elemental
riders/zones/chains stay untelemetered like chain lightning).

- [ ] **WandSO asset** (menu `Scriptable Objects/WandSO`).
- [ ] **Magic-missile prefab**: glow mesh/trail + `MagicMissileProjectile`; author the per-element
      child visuals (neutral bolt, fireball, spark, frost) all inactive; optional impact VFX.
- [ ] **`PlayerWand` on the player root, disabled**: assign WandSO + missile prefab + castOrigin
      (wand tip / chest) + hitLayers (the bow's mask); optional in-hand wand model + staff model
      for the aim swap; add to the mage slot's classComponents (it must be the slot's first
      `IChargedAimWeapon`).

**Stages C+D (imbuement core + element effects + runestones) — done in C#**:
`Player/ElementType.cs` (Fire/Lightning/Ice — append-only), `Player/MageImbuementSO.cs`,
`Player/MageImbuement.cs` — the one shared buff (ImpulseBuff skeleton + element identity):
timed + charge-stacked, **pickup RESETS the timer** (never adds); same element = +1 charge
(capped), different = replace at 1 charge; runestone = replace with `MageRunestoneCharges`
(base 2) charges, same-element runestone = timer-only; expiry clears all. Listens to the staff
trigger's + wand's `OnHit`: a flat elemental damage rider per charge (+ Searing Focus on fire),
then per element — **Fire** explosion (direct OverlapSphere at the hit point once Combustion is
owned; skips the direct target) + `Player/FlameZone.cs` burning ground (a player-owned
`LightningStormZone` clone with an enemy-layer mask, one zone per 2 s, NavMesh-snapped) once
Scorched Earth is owned; **Lightning** arcs via the existing `ChainLightning.ForceChain` (new
`excludeTarget` param; the Mage tree raises the shared `ChainLightning*` stats — a Mage never
activates the orb buff, so the two paths can't double-fire); **Ice** applies `SlowStatus`
(Ice Breaker pays off through the staff's existing per-target check and the wand's mirror of it).
`Economy/ElementNode.cs` (4th pickup sibling; a failed grant does NOT consume it, so non-Mages
leave nodes lying; 60 s lifetime; `TryCollectRemote` for wand flybys),
`Waves/ElementNodeSpawner.cs` (ChestSpawner idiom, self-disables for non-Mage runs),
`Waves/Runestone.cs` (IDamageable arena furniture; only player-`source` hits activate it —
`DamageTrigger.BuildDamage` (readsPlayerStats branch) and the wand now stamp `Damage.source`, so
enemy melee/Storm-Witch splash can never flip the element; per-stone 1 s cooldown),
`UI/MageElementUI.cs` (RageBarUI pattern: element icon/tint, charge count, time fill; self-hides),
DevConsole `DrawImbuementReadout()` (`Imbue: Fire x3 (8.2s)`).

- [ ] **MageImbuementSO asset** (menu `Scriptable Objects/MageImbuementSO`).
- [ ] **`MageImbuement` on the player root, disabled**: assign the SO, the **staff's**
      DamageTrigger (explicit — the VampiricBlade rule), the wand, enemyLayers (exclude player /
      gates / runestones / environment), per-element styles (aura child objects + activation MMFs
      + HUD icon sprites + tints), deactivation MMF, explosion VFX prefab, FlameZone prefab; add
      to the mage slot's classComponents.
- [ ] **3 ElementNode prefabs** (fire/lightning/ice): tinted orb mesh + trigger collider +
      `ElementNode` (set the element!) + DamageNumbersPro popup + pickup MMF.
- [ ] **FlameZone prefab**: looping ground-fire VFX + `FlameZone`; optional per-tick burn VFX/SFX.
- [ ] **Explosion VFX prefab** (cosmetic; the damage is code-side).
- [ ] **`ElementNodeSpawner` scene object** near the arena centre (spawnRadius ~12, min 2 / max 4
      per wave) — safe to leave in the scene for all classes, it self-disables.
- [ ] **3 Runestone prefabs placed near the gate** in `Bladehold Test Scene`: rock mesh + **solid**
      collider + `Runestone` (set the element) + activate/fizzle MMFs + per-element glow. Layer
      check: runestones must be inside the staff's + wand's `hitLayers` but **outside**
      `MageImbuement.enemyLayers` and `ChainLightning.enemyLayers` (chains/explosions/zones must
      never flip elements — switching is a deliberate, aimed act).
- [ ] **ChestLootTableSO**: add the 3 node prefabs as bonus items at low weight (~0.5) — a
      non-Mage chest roll leaves an inert node that expires (bounded waste, like a full-HP
      HealthPack).
- [ ] **HUD `MageElementUI` widget** under the HUD canvas: element icon Image + Filled time Image
      + optional "x3" TMP label, an `activeGroup` child for the active-only contents; hides itself
      for other classes.

**Stage E (Mage skill tree) — done in C#**:
`Config/SkillTreeMage.csv` — carried melee/crit/charge/knockback/vamp/plunder/sprint/impulse/
alldmg/medpack/horse lines reworded sword→staff, with the Berserker's axe/rage branch and the
gold tree's `solid`/`parry`/`counter` (glass cannon — no defensive lines) and orb-lightning family
dropped; 19 new nodes: `wand_unlock` (60g) → `wand_dmg`/`wand_charge`; fire line `fire_dmg` →
`fire_explode` (Combustion) → `fire_radius`/`fire_zone` (Scorched Earth) → `fire_zone_up`;
lightning line `light_unlock` (grants ChainLightningBounces+DamagePercent) →
`light_bounce`/`light_dmg`/`light_crit`; ice line `ice_deep` → `ice_dur` → `ice_break`; imbuement
QoL `imbue_dur`/`imbue_power`/`imbue_max` → `rune_charges` (runestones 2→3→4 charges) in the old
lightning-family footprint (x 29–30). New StatTypes: 5 wand + 11 Mage (see `Stats/StatType.cs`),
with `StatDisplay` rows.

- [ ] **Mage SkillTreeSO asset** (menu `Scriptable Objects/SkillTreeSO`), csv =
      `Config/SkillTreeMage.csv`, hasHeaderRow on; **copy the gold tree SO's `icons` list** so
      carried-over icon names resolve (the 19 new nodes ship without icons). Sanity-check in
      **Bladehold > Skill Tree Editor**; assign on the mage `ClassDefinitionSO.skillTree`.

**Manual verification (Mage)**
- [ ] Swordsman + Berserker regress-check first: bow aim and axe aim still suppress the melee
      charge (the PlayerAttack refactor's risk); Parry/Counterstrike still work (the
      `Damage.source` stamp is additive); `SkillTreePreview` scene throws no NPEs.
- [ ] DevConsole → Switch to Mage & Reload: robed model, staff swings, melee nodes scale it;
      telemetry `run_start` shows `class=mage`.
- [ ] Fresh Mage: aiming does nothing until `wand_unlock`; after buying — aim shoulders the camera
      in, crosshair + cooldown radial work, missiles fly and damage; charge scales damage; no
      mounted casting.
- [ ] Element nodes scatter on wave start (Mage only); walk-over imbues (aura + HUD + DevConsole
      readout); same element = +1 charge & timer reset; different element = swap at 1 charge;
      expiry clears everything at once; imbued staff and wand hits pop a second elemental damage
      number scaling with charges.
- [ ] Fire: rider only until `fire_explode`; then explosions at hit points (neighbours damaged,
      direct target not double-dipped); `fire_zone` adds burning ground, max one per 2 s, ticking
      enemies but never the player/gate/chests. Wand + fire reads as a fireball.
- [ ] Lightning: inert until `light_unlock`; then hits arc (never back into the struck enemy);
      a Lightning Orb pickup does NOT start the old orb buff for the Mage (no double chains).
- [ ] Ice: hits visibly slow (agent + animator); `ice_break` (Shatter) raises staff AND wand
      damage vs chilled targets; `ice_dur` lingers the slow.
- [ ] Runestones: blast from range = element swap with 2 charges (3/4 with `rune_charges`);
      same-element re-blast = timer refresh only; goblin attacks / Storm Witch storms hitting the
      stone never flip the element; non-Mage blast = fizzle feedback only.
- [ ] Wand missile flying over a ground node collects it mid-flight without stopping.
- [ ] Chest bonus rolls can drop element nodes; a non-Mage walking over one leaves it lying (it
      expires after ~60 s).
- [ ] Reincarnate wipe restores every gate (wand locked, fire/lightning lines inert).

**Mage follow-ups (recorded, not scheduled)**
- [ ] Extract a shared pickup base class — `ElementNode` is the fourth sibling of
      Coin/ImpulseOrb/LightningOrb; a fifth pickup (or a second remote-collect consumer) is the cue.
- [ ] Optional: `LightningOrb` grants a Lightning imbuement charge to a Mage instead of a dead
      no-op consume.
