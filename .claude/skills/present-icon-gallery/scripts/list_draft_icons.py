import csv
import os

csv_path = 'Assets/Bladehold/Resources/DraftUpgrades.csv'
icons_dir = 'Assets/Bladehold/Art/Icons/Skills/Base'

cards = []
with open(csv_path, mode='r', encoding='utf-8') as f:
    reader = csv.DictReader(f)
    for row in reader:
        cards.append(row)

unique_icons = {}
for c in cards:
    icon = c['icon'].strip()
    if icon not in unique_icons:
        unique_icons[icon] = []
    unique_icons[icon].append({
        'id': c['id'],
        'name': c['displayName'],
        'category': c['category'],
        'weapon': c['weapon'],
        'element': c['element'],
        'desc': c['description']
    })

print(f"Total cards: {len(cards)}")
print(f"Total unique icon names: {len(unique_icons)}")

for icon, card_list in sorted(unique_icons.items()):
    png_path = os.path.join(icons_dir, f"{icon}.png")
    exists = os.path.exists(png_path)
    card_names = ", ".join([c['name'] for c in card_list])
    print(f"Icon: {icon:<18} | FileExists: {str(exists):<5} | Cards ({len(card_list)}): {card_names}")
