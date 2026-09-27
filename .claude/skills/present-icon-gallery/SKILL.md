---
name: present-icon-gallery
description: Use whenever presenting newly generated or modified game icons to Lance for review — builds a mobile-friendly, self-contained HTML gallery with inlined base64 images (source silhouettes and Synty variants: Clean, Underlay, Stroke) and embeds it inline via <agent-embed>.
---

# Present Icon Gallery (Mobile-Friendly Base64 Preview)

Use this skill whenever presenting newly generated or updated 2D game icons (skills, draft cards, status badges, currencies, UI sprites) to Lance for visual review.

## The Mobile App Constraint

- **Never rely on raw markdown file links or local filesystem image paths** (`file:///C:/Users/lance/...` or `![...](C:\...)`) to present icons.
- When Lance reviews progress on a mobile device, the phone client has no access to the Windows desktop file system, so local path images break and appear as missing.
- **The Solution:** Always compile the icons into a self-contained HTML gallery where every image (the raw AI source silhouette, plus the Synty-standard `_Clean`, `_Underlay`, and `_Stroke` variants) is embedded directly as an inline **Base64 data URI** (`data:image/png;base64,...`).

## Helper Script

The skill includes a dedicated helper script in `scripts/build_icon_gallery.py`.

### Quick Directory Auto-Discovery

If your variants (`*_Clean.png`, `*_Underlay.png`, `*_Stroke.png`) and source images are saved in a scratch folder:

```bash
python ".claude/skills/present-icon-gallery/scripts/build_icon_gallery.py" \
  --output "<appDataDir>/brain/<conversation-id>/<feature>_icons_gallery.html" \
  --title "<Feature Name> Icons" \
  --subtitle "Self-contained Base64 preview of icons and variants." \
  --dir "<scratch_or_output_dir>"
```

### Manifest Mode (Custom Titles & Descriptions)

For curated cards with game descriptions, provide a JSON manifest:

```json
[
  {
    "id": "fish_skewer",
    "title": "Fish Skewer",
    "description": "Arrows pierce through additional fish.",
    "source": "<path>/fishing_fish_skewer.jpg",
    "clean": "<path>/fish_skewer_Clean.png",
    "underlay": "<path>/fish_skewer_Underlay.png",
    "stroke": "<path>/fish_skewer_Stroke.png"
  }
]
```

Run:
```bash
python ".claude/skills/present-icon-gallery/scripts/build_icon_gallery.py" \
  --output "<appDataDir>/brain/<conversation-id>/<feature>_icons_gallery.html" \
  --title "<Feature Name> Icons" \
  --manifest "<path_to_manifest.json>"
```

## Presentation in Chat

Once the HTML file is written to the conversation artifacts directory:

1. Provide a direct link to the HTML artifact for desktop/web browsing:
   `👉 [Open Full Icon Gallery](file:///<path_to_gallery.html>)`
2. **Embed the widget inline** in your chat response so it renders immediately inside the conversation stream on both desktop and mobile:
   ```html
   <agent-embed src="file:///<path_to_gallery.html>"></agent-embed>
   ```
3. Summarize the card concepts and wait for Lance's review or revision requests before importing them into `Assets/Bladehold/Art/Icons/Skills/`.
