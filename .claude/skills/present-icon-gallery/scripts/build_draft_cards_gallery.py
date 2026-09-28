import csv
import os
import base64

csv_path = 'Assets/Bladehold/Resources/DraftUpgrades.csv'
icons_dir = 'Assets/Bladehold/Art/Icons/Skills/Base'
base_dir = r'C:/Users/lance/.gemini/antigravity/brain/eccbe0c7-bfb9-453f-8932-882425753d46'
out_html = os.path.join(base_dir, 'draft_skills_gallery.html')

def file_to_b64(path):
    if not path or not os.path.exists(path):
        return None
    with open(path, 'rb') as f:
        data = base64.b64encode(f.read()).decode('ascii')
        return f"data:image/png;base64,{data}"

cards = []
with open(csv_path, mode='r', encoding='utf-8') as f:
    reader = csv.DictReader(f)
    for row in reader:
        cards.append(row)

# Group cards by category/weapon/element
sections = {
    'Sword Skills': [c for c in cards if c['weapon'] == 'sword'],
    'Bow Skills': [c for c in cards if c['weapon'] == 'bow'],
    'Axe Skills': [c for c in cards if c['weapon'] == 'axe'],
    'Throwing Axe Skills': [c for c in cards if c['weapon'] == 'throwing_axe'],
    'Mace Skills': [c for c in cards if c['weapon'] == 'mace'],
    'Fire Elemental': [c for c in cards if c['element'] == 'Fire'],
    'Ice Elemental': [c for c in cards if c['element'] == 'Ice'],
    'Lightning Elemental': [c for c in cards if c['element'] == 'Lightning'],
    'Duo Skills': [c for c in cards if c['isDuo'] == 'TRUE']
}

html_sections = []

for sec_title, sec_cards in sections.items():
    if not sec_cards:
        continue
    cards_markup = []
    for c in sec_cards:
        icon_name = c['icon'].strip()
        icon_path = os.path.join(icons_dir, f"{icon_name}.png")
        b64_uri = file_to_b64(icon_path)
        is_missing = b64_uri is None

        img_box = ""
        if is_missing:
            img_box = f"""
            <div style="width: 64px; height: 64px; background: #371b1b; border: 1px dashed #ef4444; border-radius: 8px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; color: #ef4444; font-size: 10px; font-weight: bold; text-align: center; padding: 2px;">
              MISSING ICON
            </div>"""
        else:
            img_box = f"""
            <div style="width: 64px; height: 64px; background: #1c1917; border: 1px solid #44403c; border-radius: 8px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; padding: 4px;">
              <img src="{b64_uri}" alt="{icon_name}" style="max-width: 100%; max-height: 100%; object-fit: contain; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6));" />
            </div>"""

        badge_type = ""
        if c['isUltimate'] == '1':
            badge_type += '<span style="background: #7c2d12; color: #fed7aa; padding: 2px 6px; border-radius: 4px; font-size: 10px; font-weight: bold; margin-right: 4px;">ULTIMATE</span>'
        if c['isDuo'] == 'TRUE':
            badge_type += '<span style="background: #4c1d95; color: #e9d5ff; padding: 2px 6px; border-radius: 4px; font-size: 10px; font-weight: bold; margin-right: 4px;">DUO</span>'
        if c.get('targetSlot'):
            badge_type += f'<span style="background: #1e293b; color: #94a3b8; padding: 2px 6px; border-radius: 4px; font-size: 10px; font-weight: 500; margin-right: 4px;">{c["targetSlot"]}</span>'

        status_text = f'<span style="color: #ef4444; font-weight: bold;">(Missing file!)</span>' if is_missing else ''

        cards_markup.append(f"""
        <div style="background: #111827; border: 1px solid #1f2937; border-radius: 10px; padding: 12px; display: flex; gap: 14px; align-items: center; margin-bottom: 10px;">
          {img_box}
          <div style="flex-grow: 1; min-width: 0;">
            <div style="display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: 4px; margin-bottom: 4px;">
              <h4 style="margin: 0; color: #f3f4f6; font-size: 15px; font-weight: 600;">{c['displayName']}</h4>
              <div>
                {badge_type}
                <code style="background: #1f2937; color: #fbbf24; padding: 2px 6px; border-radius: 4px; font-size: 11px;">{icon_name}</code> {status_text}
              </div>
            </div>
            <p style="margin: 0; color: #9ca3af; font-size: 12px; line-height: 1.4;">{c['description']}</p>
          </div>
        </div>""")

    cards_block = "\n".join(cards_markup)
    html_sections.append(f"""
    <div style="margin-bottom: 28px;">
      <h3 style="color: #e5e7eb; border-bottom: 1px solid #374151; padding-bottom: 6px; margin-bottom: 12px; font-size: 16px;">{sec_title} ({len(sec_cards)})</h3>
      {cards_block}
    </div>""")

full_body = "\n".join(html_sections)
full_html = f"""<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Bladehold Draft Skill Icons</title>
</head>
<body style="background: #030712; color: #f9fafb; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 16px; margin: 0;">
  <div style="max-width: 680px; margin: 0 auto;">
    <div style="margin-bottom: 20px;">
      <h2 style="margin: 0 0 6px 0; color: #fbbf24; font-size: 20px;">Bladehold — Draft Skill Cards & Icons</h2>
      <p style="margin: 0; color: #9ca3af; font-size: 13px;">Review of all 49 draft upgrade cards and their current assigned icons (embedded Base64 for mobile preview).</p>
    </div>
    {full_body}
  </div>
</body>
</html>"""

with open(out_html, 'w', encoding='utf-8') as f:
    f.write(full_html)

print(f"Wrote draft skills gallery to: {out_html} ({len(full_html)} bytes)")
