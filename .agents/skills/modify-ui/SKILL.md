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

## Step 2 — Execution using `execute_code`
Use `execute_code` (C# 6 / codedom compliant) to apply your layout changes:

### Common UI Fixes & Patterns
- **Anchors vs Resolution**: If a UI element is meant to stay at the bottom of the screen, set its `anchorMin` and `anchorMax` to `(x, 0)` instead of `(0.5, 0.5)`. Elements anchored to the center with negative Y offsets will fall off-screen on wide aspect ratios.
- **Adding Layout Groups**: 
  ```csharp
  var vlg = panel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
  vlg.childControlWidth = true;
  vlg.childForceExpandHeight = false;
  vlg.spacing = 10f;
  ```
- **Modifying Prefabs vs Scenes**: 
  - If editing a prefab, use `AssetDatabase.LoadAssetAtPath`, make changes, then call `EditorUtility.SetDirty(prefab)` and `AssetDatabase.SaveAssets()`.
  - If editing a scene object, ensure you are NOT in Play Mode (`EditorApplication.isPlaying`), make changes, then call `EditorSceneManager.MarkSceneDirty(scene)` and `EditorSceneManager.SaveScene(scene)`.
- **Finding Components**: Use `GetComponent<RectTransform>()` to adjust `sizeDelta`, `anchoredPosition`, and `pivot`.

## Step 3 — Verification
- Ensure you have exited Play Mode before saving scene changes (`UnityEditor.EditorApplication.isPlaying = false;`).
- Remind the user to enter Play Mode and check the UI at different resolutions to verify anchor correctness and layout responsiveness.
- If fixing a visual bug (e.g., black boxes, missing icons), verify the camera's `clearFlags` and `backgroundColor` (transparent vs solid), and ensure `Image.sprite` is actually assigned and `enabled`.

## Pitfalls
- **CodeDom limitations**: `execute_code` uses C# 6. Do not use local functions or tuples. Use `System.Action` or `System.Func` delegates instead.
- **Play Mode loss**: Changes made to scene GameObjects while in Play Mode will be lost when Play Mode stops. Always modify the scene in Edit Mode, or modify the source Prefab assets directly.
