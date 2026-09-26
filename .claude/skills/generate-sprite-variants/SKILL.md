---
name: generate-sprite-variants
description: Use when Bladehold needs a new 2D icon (draft card, status badge, UI sprite) in the Synty flat white-silhouette style — generates the image, turns it into transparent PNG variants plus an SVG with the bundled offline tool, and registers the sprite for draft cards.
---

# Generate icon sprites + variants

Existing card icons live in `Assets/Bladehold/Art/Icons/Skills/Base/` (PNG) and `.../Skills/SVG/`. The style reference is Synty's `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Status/` (e.g. `ICON_FantasyWarrior_Status_Attack_01_Clean.png`, `..._Health_01_Clean.png`, `..._Burninating_01_Clean.png`). Check both folders before generating anything new; `/find-and-import-assets` may already have a fit.

## Step 1: generate the source image

Style: **pure white chunky silhouette on pure black**, faceted low-poly edges, no fine lines or texture, readable at 32px.

- Unity MCP `generate_image` (`action: "list_providers"` first; needs a fal.ai/OpenRouter key set in the Editor; `generate` returns a `job_id`, poll with `status`). It imports into the project, so give `output_folder` a scratch folder you delete afterwards, never the final icon folder.
- No provider configured: ask Lance for a source image. Don't substitute a code-drawn placeholder.

Prompt template:

```text
An ultra-simple, minimalist 2D flat game UI icon representing [CONCEPT]. Pure solid white chunky silhouette on a solid pitch black background. [One sentence on the single iconic shape, e.g. "A single low-poly battle axe with a faceted head, angled diagonally."] Bold faceted geometric contours, thick readable silhouette, zero noise, zero fine lines, zero background clutter.
```

## Step 2: make the variants (offline, outside Unity)

`scripts/` is a standalone .NET console tool (Windows, System.Drawing, unsafe code). **Never copy `SpriteVariantsProcessor.cs` into `Assets/`**: Assembly-CSharp can't compile it, and it isn't game code. Keep build output out of the repo with `--artifacts-path`:

```bash
dotnet run --project "C:/Users/lance/source/repos/My project/.claude/skills/generate-sprite-variants/scripts" \
  --artifacts-path "<scratchpad>/spritevariants-build" -- "<source.png>" "<output dir>" <baseName>
```

The output is `<baseName>_Clean.png` (transparent white, the default card/HUD sprite), `_Stroke` (dark outline ribbon), `_Underlay` (soft drop shadow), `_Embossed` (raised), `_Sunken` (inset, e.g. locked states) and `<baseName>.svg`. Match existing names: lowercase snake_case like `axe_boomerang`, with the draft card's `icon` value as the base name.

## Step 3: import and register

1. Copy the variants you need into the project: `_Clean.png` → `Assets/Bladehold/Art/Icons/Skills/Base/<baseName>.png` (existing files there carry no suffix), SVG → `.../Skills/SVG/`. Don't write into `Assets/Synty/` (vendored).
2. Import settings: Sprite (2D and UI), Single, Alpha Is Transparency on, Clamp.
3. Draft cards: set the `icon` column in `Assets/Bladehold/Resources/DraftUpgrades.csv` to the sprite name, then add the sprite to the `icons` array on `Assets/Bladehold/Resources/SkillTreeIcons.asset` (`SkillTreeIconsSO`, looked up by sprite name through `DraftUpgradeService.GetIcon`). Via MCP: `execute_code` + `SerializedObject.FindProperty("icons")`. The Balance Tree Editor (`Bladehold/Balance Tree Editor`, F1) flags cards that still have no icon.
4. `refresh_unity`, then `read_console`. If MCP isn't connected, put steps 2 and 3 in the plan's `plans/editor/` checklist (`/editor-wiring-todo`) and flag the icon for Lance's art review.
