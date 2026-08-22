---
name: modify-ui
description: Use when restructuring or modifying Unity UI (Canvas, RectTransforms, Layout Groups, settings menus, skill trees, health bars) via the unity-mcp execute_code tool.
---

# Modifying Unity UI via MCP

When the user asks to restructure UI screens, fix overlaps, add new settings, or adjust UI components like health bars or skill trees, do so via the `unityMCP` server using the `execute_code` tool. This avoids manual Editor wiring and ensures precision.

## Step 1 — Inspection and Planning
1. **Find the Target**: Use `execute_code` to search for the specific UI GameObjects in the scene or load the Prefab using `AssetDatabase.LoadAssetAtPath<GameObject>(...)`.
2. **Read the Hierarchy**: Write a short recursive function in `execute_code` (using C# 6 `Action` delegate since local functions aren't supported in CodeDom) to print the names, `anchoredPosition`, `sizeDelta`, `anchorMin/Max`, `pivot`, and component types (`Image`, `Button`, `LayoutGroup`) of the UI tree.
3. **Plan Layout Changes**: Decide if the UI needs a `VerticalLayoutGroup`, `HorizontalLayoutGroup`, or absolute positioning. 

## Step 2 — Visual Theme & Design System Defaults

Bladehold uses a consistent parchment & dark fantasy theme across cards, modals, game over screens, pause menus, and sidebars. **Always apply these defaults when building or modifying UI elements:**

### 1. Typography & Font Assets
- **Primary Text Font**: `Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset`
- **Underlay / Header Font**: `Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Black Underlay.asset`
- **Text Colors**:
  - **Dark Brown (Default on Parchment)**: `#63564B` (`Color(0.388f, 0.337f, 0.294f, 1f)`) or `#54483D` for menu button labels.
  - **Muted Off-White (on dark backgrounds)**: `#D9D1BF` (`Color(0.851f, 0.820f, 0.749f, 1f)`)
  - **Gold / Highlight**: `#FFD170` (`Color(1f, 0.820f, 0.439f, 1f)`) or `#FFA000` for costs.
  - **Health / Damage Red**: `#A45845` (`Color(0.643f, 0.345f, 0.271f, 1f)`)

### 2. Backgrounds & Panels (Parchment)
- **Panel / Card Tint**: Warm light brown / parchment `#C5BEA8` (`Color(0.773f, 0.745f, 0.659f, 1f)`) or `#D4C6A3` (`Color(0.831f, 0.776f, 0.639f, 1f)`).
- **Sprites**:
  - **Cards & Sub-Panels**: `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Medium_ParchmentGradient_01.png` (or `_02`, `_04`, `_05`)
  - **Full Window / Large Backdrops**: `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Large_Parchment_01.png`
  - **Buttons & Small Badges**: `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Small_Parchment_01.png` (or `_02`, `_03`)

### 3. Separators & Dividers
- **Separator Tint**: Dark brown `#63564B` (`Color(0.388f, 0.337f, 0.294f, 1f)`).
- **Sprites**:
  - **Primary Line**: `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Line_01.png`
  - **Ornamental Ends**: `SPR_HUD_FantasyWarrior_Line_04_Left.png` / `SPR_HUD_FantasyWarrior_Line_04_Right.png`
  - **Kenney UI Dividers**: `Assets/Bladehold/Bladehold Images/UI/Kenney UI/Divider/` (`divider-000` to `divider-005`, `divider-fade-001` / `002`)

### 4. Modal Overlays
- **Screen Dimmer / Backdrop**: Dark translucent `#0D0D14EB` (`Color(0.051f, 0.051f, 0.078f, 0.92f)`) or `#000000CC`.

---

## Step 3 — Execution using `execute_code`
Use `execute_code` (C# 6 / codedom compliant) to apply your layout and visual styling changes:

### Common UI Fixes & Patterns
- **Anchors vs Resolution**: If a UI element is meant to stay at the bottom of the screen, set its `anchorMin` and `anchorMax` to `(x, 0)` instead of `(0.5, 0.5)`. Elements anchored to the center with negative Y offsets will fall off-screen on wide aspect ratios.
- **Adding Layout Groups**: 
  ```csharp
  var vlg = panel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
  vlg.childControlWidth = true;
  vlg.childForceExpandHeight = false;
  vlg.spacing = 10f;
  ```
- **Applying Theme Styling via Code**:
  ```csharp
  // Font
  var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
  tmpText.font = font;
  Color brownText;
  ColorUtility.TryParseHtmlString("#63564B", out brownText);
  tmpText.color = brownText;

  // Background Parchment Image
  var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Medium_ParchmentGradient_04.png");
  bgImage.sprite = bgSprite;
  Color parchmentTint;
  ColorUtility.TryParseHtmlString("#C5BEA8", out parchmentTint);
  bgImage.color = parchmentTint;

  // Separator Line
  var sepSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Line_01.png");
  sepImage.sprite = sepSprite;
  sepImage.color = brownText;
  ```
- **Modifying Prefabs vs Scenes**: 
  - If editing a prefab, use `AssetDatabase.LoadAssetAtPath`, make changes, then call `EditorUtility.SetDirty(prefab)` and `AssetDatabase.SaveAssets()`.
  - If editing a scene object, ensure you are NOT in Play Mode (`EditorApplication.isPlaying`), make changes, then call `EditorSceneManager.MarkSceneDirty(scene)` and `EditorSceneManager.SaveScene(scene)`.
- **Finding Components**: Use `GetComponent<RectTransform>()` to adjust `sizeDelta`, `anchoredPosition`, and `pivot`.

## Step 4 — Verification
- Ensure you have exited Play Mode before saving scene changes (`UnityEditor.EditorApplication.isPlaying = false;`).
- Remind the user to enter Play Mode and check the UI at different resolutions to verify anchor correctness and layout responsiveness.
- If fixing a visual bug (e.g., black boxes, missing icons), verify the camera's `clearFlags` and `backgroundColor` (transparent vs solid), and ensure `Image.sprite` is actually assigned and `enabled`.

## Pitfalls
- **CodeDom limitations**: `execute_code` uses C# 6. Do not use local functions or tuples. Use `System.Action` or `System.Func` delegates instead.
- **Play Mode loss**: Changes made to scene GameObjects while in Play Mode will be lost when Play Mode stops. Always modify the scene in Edit Mode, or modify the source Prefab assets directly.
- **Inconsistent Theme**: Do not leave white backgrounds or default LiberationSans text when constructing cards, modals, or stats panels. Always apply the Texturina font, dark brown text (`#63564B`), parchment background (`#C5BEA8`), and dark brown separators (`#63564B`).
