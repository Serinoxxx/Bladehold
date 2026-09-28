---
name: generate-sprite-variants
description: Use when Bladehold needs a new 2D icon (draft card, status badge, UI sprite) in the Synty flat white-silhouette style — generates the image, turns it into transparent PNG variants plus an SVG with the bundled offline tool, and registers the sprite for draft cards.
---

# Generate icon sprites + variants

Existing card icons live in `Assets/Bladehold/Art/Icons/Skills/Base/` (PNG) and `.../Skills/SVG/`. The style reference is Synty's `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Status/` (e.g. `ICON_FantasyWarrior_Status_Attack_01_Clean.png`, `..._Health_01_Clean.png`, `..._Burninating_01_Clean.png`). Check both folders before generating anything new; `/find-and-import-assets` may already have a fit.

## Step 1: generate the source image

Style: **pure white chunky silhouette on pure black**, faceted low-poly edges, no fine lines or texture, readable at 32px.

### Strict Style Rules:
- **10 paths MAX per image**: Silhouettes must remain bold, simple, and immediately recognizable at small sizes. No tiny debris, no particle clouds, and no noisy shard scatter.
- **NO smooth curves or rounded corners**: All contours must be sharp, jagged, and chiseled.
- **NO perfect circles**: Replace circular elements with imperfect, faceted polygons (e.g. 9-sided nonagons, 7-sided heptagons, or chiseled polyhedral gems).
- **Chiseled low-poly geometry**: Match the Synty faceted low-poly aesthetic with hard vertices and deliberate angular planes.

Prompt template:

```text
An ultra-simple, minimalist 2D flat game UI icon representing [CONCEPT]. Pure solid white chunky silhouette on a solid pitch black background. [One or two sentences describing the iconic shape with explicit faceted/jagged geometry, e.g. "A jagged faceted geometric stone tower spire crowned with an imperfect 9-sided nonagon crystal orb. Three sharp jagged lightning bolts crackle outward with razor-sharp angles."] Strictly faceted polygonal geometry, zero smooth curves, zero perfect circles, zero rounded corners. Maximum 5 to 8 bold chunky geometric shapes total, bold readable silhouette, zero noise, zero fine lines, zero background clutter.
```

## Step 2: make the variants (offline, outside Unity)

`scripts/` is a standalone .NET console tool (Windows, System.Drawing, unsafe code). **Never copy `SpriteVariantsProcessor.cs` into `Assets/`**: Assembly-CSharp can't compile it, and it isn't game code. Keep build output out of the repo with `--artifacts-path`:

```bash
dotnet run --project "C:/Users/lance/source/repos/My project/.claude/skills/generate-sprite-variants/scripts" \
  --artifacts-path "<scratchpad>/spritevariants-build" -- "<source.png>" "<output dir>" <baseName>
```

The output is `<baseName>_Clean.png` (transparent white, the default card/HUD sprite), `_Stroke` (dark outline ribbon), `_Underlay` (soft drop shadow), `_Embossed` (raised), `_Sunken` (inset, e.g. locked states) and `<baseName>.svg`. Match existing names: lowercase snake_case like `axe_boomerang`, with the draft card's `icon` value as the base name.

## Step 2.5: Present to Lance via `/present-icon-gallery`

Never present icons to Lance using raw markdown image links (`![...](C:\...)`) because mobile clients cannot resolve local Windows desktop paths. Always build a mobile-ready, self-contained Base64 HTML gallery via the `present-icon-gallery` skill (`python ".claude/skills/present-icon-gallery/scripts/build_icon_gallery.py" ...`) and embed it in chat with `<agent-embed>`. Wait for Lance's review and approval before proceeding to Step 3.

## Step 3: import and register

1. Copy the variants you need into the project: `_Clean.png` → `Assets/Bladehold/Art/Icons/Skills/Base/<baseName>.png` (existing files there carry no suffix), SVG → `.../Skills/SVG/`. Don't write into `Assets/Synty/` (vendored).
2. Import settings: Sprite (2D and UI), Single, Alpha Is Transparency on, Clamp.
3. Draft cards: set the `icon` column in `Assets/Bladehold/Resources/DraftUpgrades.csv` to the sprite name, then add the sprite to the `icons` array on `Assets/Bladehold/Resources/SkillTreeIcons.asset` (`SkillTreeIconsSO`, looked up by sprite name through `DraftUpgradeService.GetIcon`). Via MCP: `execute_code` + `SerializedObject.FindProperty("icons")`. The Balance Tree Editor (`Bladehold/Balance Tree Editor`, F1) flags cards that still have no icon.
4. `refresh_unity`, then `read_console`. If MCP isn't connected, put steps 2 and 3 in the plan's `plans/editor/` checklist (`/editor-wiring-todo`) and flag the icon for Lance's art review.
