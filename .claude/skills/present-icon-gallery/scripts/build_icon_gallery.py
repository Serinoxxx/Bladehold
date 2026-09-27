#!/usr/bin/env python3
"""
build_icon_gallery.py
Builds a self-contained, mobile-friendly HTML gallery embedding game icons
and their Synty-style variants (Clean, Underlay, Stroke) as inline Base64 data URIs.
"""

import os
import sys
import json
import base64
import argparse
from pathlib import Path


def file_to_b64(path):
    if not path or not os.path.exists(path):
        return None
    mime = "image/png"
    lower = str(path).lower()
    if lower.endswith(".jpg") or lower.endswith(".jpeg"):
        mime = "image/jpeg"
    elif lower.endswith(".svg"):
        mime = "image/svg+xml"
    with open(path, "rb") as f:
        data = base64.b64encode(f.read()).decode("ascii")
        return f"data:{mime};base64,{data}"


def build_gallery(icons, title, subtitle, output_path):
    cards_html = []
    for item in icons:
        icon_id = item.get("id", "")
        card_title = item.get("title", icon_id.replace("_", " ").title())
        desc = item.get("description", "")

        src_uri = file_to_b64(item.get("source"))
        clean_uri = file_to_b64(item.get("clean"))
        underlay_uri = file_to_b64(item.get("underlay"))
        stroke_uri = file_to_b64(item.get("stroke"))

        preview_slots = []
        if src_uri:
            preview_slots.append(f"""
              <div style="text-align: center;">
                <div style="background: #000; border: 1px solid #374151; border-radius: 8px; padding: 6px; aspect-ratio: 1; display: flex; align-items: center; justify-content: center;">
                  <img src="{src_uri}" style="max-width: 100%; max-height: 100%; object-fit: contain;" />
                </div>
                <span style="font-size: 11px; color: #9ca3af; display: block; margin-top: 4px;">Source</span>
              </div>""")

        if clean_uri:
            preview_slots.append(f"""
              <div style="text-align: center;">
                <div style="background: #2b2620; border: 1px solid #443e37; border-radius: 8px; padding: 6px; aspect-ratio: 1; display: flex; align-items: center; justify-content: center;">
                  <img src="{clean_uri}" style="max-width: 100%; max-height: 100%; object-fit: contain; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.5));" />
                </div>
                <span style="font-size: 11px; color: #9ca3af; display: block; margin-top: 4px;">Clean</span>
              </div>""")

        if underlay_uri:
            preview_slots.append(f"""
              <div style="text-align: center;">
                <div style="background: #2b2620; border: 1px solid #443e37; border-radius: 8px; padding: 6px; aspect-ratio: 1; display: flex; align-items: center; justify-content: center;">
                  <img src="{underlay_uri}" style="max-width: 100%; max-height: 100%; object-fit: contain;" />
                </div>
                <span style="font-size: 11px; color: #9ca3af; display: block; margin-top: 4px;">Underlay</span>
              </div>""")

        if stroke_uri:
            preview_slots.append(f"""
              <div style="text-align: center;">
                <div style="background: #030712; border: 1px solid #1f2937; border-radius: 8px; padding: 6px; aspect-ratio: 1; display: flex; align-items: center; justify-content: center;">
                  <img src="{stroke_uri}" style="max-width: 100%; max-height: 100%; object-fit: contain;" />
                </div>
                <span style="font-size: 11px; color: #9ca3af; display: block; margin-top: 4px;">Stroke</span>
              </div>""")

        slots_grid = "\n".join(preview_slots)
        col_count = max(1, len(preview_slots))

        cards_html.append(f"""
        <div style="background: #111827; border: 1px solid #1f2937; border-radius: 12px; padding: 16px; margin-bottom: 16px;">
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
            <h3 style="margin: 0; color: #fbbf24; font-size: 16px;">{card_title}</h3>
            <code style="background: #1f2937; color: #9ca3af; padding: 2px 6px; border-radius: 4px; font-size: 11px;">{icon_id}</code>
          </div>
          {f'<p style="color: #9ca3af; font-size: 13px; margin: 0 0 12px 0;">{desc}</p>' if desc else ''}
          <div style="display: grid; grid-template-columns: repeat({col_count}, 1fr); gap: 10px;">
            {slots_grid}
          </div>
        </div>""")

    body_content = "\n".join(cards_html)
    html = f"""<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{title}</title>
</head>
<body style="background: #030712; color: #f9fafb; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 16px; margin: 0;">
  <div style="max-width: 600px; margin: 0 auto;">
    <h2 style="margin-top: 0; color: #f3f4f6;">{title}</h2>
    {f'<p style="color: #9ca3af; font-size: 14px; margin-bottom: 20px;">{subtitle}</p>' if subtitle else ''}
    {body_content}
  </div>
</body>
</html>"""

    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"Gallery written to: {output_path} ({len(html)} bytes)")


def main():
    parser = argparse.ArgumentParser(description="Generate self-contained Base64 icon preview gallery.")
    parser.add_argument("--output", "-o", required=True, help="Destination HTML file path.")
    parser.add_argument("--title", "-t", default="Icon Preview Gallery", help="Page title.")
    parser.add_argument("--subtitle", "-s", default="Self-contained Base64 preview of icons and variants.", help="Page subtitle.")
    parser.add_argument("--manifest", "-m", help="Path to JSON manifest file describing the icons.")
    parser.add_argument("--dir", "-d", help="Directory containing generated sprite variants to auto-discover.")
    args = parser.parse_args()

    icons = []

    if args.manifest and os.path.exists(args.manifest):
        with open(args.manifest, "r", encoding="utf-8") as f:
            icons = json.load(f)
    elif args.dir and os.path.exists(args.dir):
        search_dir = Path(args.dir)
        clean_files = sorted(search_dir.glob("*_Clean.png"))
        for clean_path in clean_files:
            base_id = clean_path.name.replace("_Clean.png", "")
            underlay_path = search_dir / f"{base_id}_Underlay.png"
            stroke_path = search_dir / f"{base_id}_Stroke.png"

            src_candidate = None
            for ext in [".jpg", ".jpeg", ".png"]:
                matches = list(search_dir.glob(f"*{base_id}*{ext}"))
                non_variant = [m for m in matches if not any(v in m.name for v in ["_Clean", "_Underlay", "_Stroke", "_Embossed", "_Sunken"])]
                if non_variant:
                    src_candidate = str(non_variant[0])
                    break

            icons.append({
                "id": base_id,
                "title": base_id.replace("_", " ").title(),
                "clean": str(clean_path),
                "underlay": str(underlay_path) if underlay_path.exists() else None,
                "stroke": str(stroke_path) if stroke_path.exists() else None,
                "source": src_candidate
            })

    if not icons:
        print("Error: No icons provided via --manifest or found in --dir", file=sys.stderr)
        sys.exit(1)

    build_gallery(icons, args.title, args.subtitle, args.output)


if __name__ == "__main__":
    main()
