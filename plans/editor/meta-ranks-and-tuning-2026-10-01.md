# Editor to-do: fish icons, tuning, ranked meta perks (2026-10-01)

Unity MCP was connected, so the asset and scene wiring is done. Unity and `dotnet build` both compile clean. Tick items off as you go and delete the file when it's empty.

**Done in code and assets:**
- **Fish draft icons:** the seven fishing icons (`Art/Icons/Skills/Base/{bleed,bounce_shot,chain_reaction,fat_fish,fishsploshion,fish_skewer,icey_water}.png`) were imported with Alpha Source = From Gray Scale. They're white under the transparency, so they drew as solid squares. They now use Input Texture Alpha. `FishingDraftUI` also finds its icon images when a draft opens, not only in `Start`.
- **Tower range doubled:** Arrow 36/40/44 (`ArrowTowerDefense.ApplyLevelStats`), Ballista 44, Net Thrower 36, Catapult 48, in both the scripts and the `Defense_*.prefab`s. Blind spots are unchanged.
- **Bulwark shield:** `SM_Wep_Shield_Plank_01` on `Bulwark Enemy Variant.prefab` went from scale 2 to 1.25. Its block collider shrinks with it.
- **Fishing XP:** new `FishingManager.firstLevelXp` (80, was a hard-coded 40) and `levelXpGrowth` (1.5), so levelling takes twice as long.
- **Ranked meta perks:**
  - `MetaPerkDefinitionSO.rankCosts` / `rankValues`. Descriptions use `{0}` for the value.
  - A perk's rank is how many times its id appears in `SaveData.purchasedMetaPerks`, so old saves read as rank 1.
  - Run code reads values through `RunSession.GetMetaPerkValue(id, fallback)` from `Resources/MetaPerkCatalog.asset`, a new `MetaPerkCatalogSO`.
  - New tier-1 perk `supply_cache` (Supply Cache): +20/40/60/80 starting supply for 10/15/20/25 Blood.
  - Other ranked perks:

    | Perk | Values | Cost (Blood) |
    |---|---|---|
    | Agility | +1/2 dashes | 10/20 |
    | Backstab | +20/30/40/50% | 10/15/20/25 |
    | Deep Quiver | +5/10/15/20 ammo | 10/15/20/25 |
    | Regeneration | +5/10/15/20 HP per wave | 10/15/20/25 |
    | Executioner | +50/75/100% | 25/35/45 |
    | Greed | +10/20/30% | 25/35/45 |
    | War Chest | 75/150/225 gold | 50/65/80 |
    | Master Tactician | 1/2/3 rerolls | 50/65/80 |

  - Second Wind, Deep Pockets and Twin Fury stay one-off.
- **Meta upgrades window redesign:**
  - Left side: tier sections, each a 5-wide grid of `MetaPerkCard.prefab` tiles (`MetaPerkCardUI`: icon, name, rank pips, next price).
  - Right side: a details panel showing the current effect, the next rank and a Learn/Upgrade button.
  - Pad focus uses a `MenuFocusController` on `WindowRoot` (B closes).
  - Built by `Editor/MetaUpgradesUIBuilder.cs` (menu **Bladehold/UI/Rebuild Meta Upgrades Window**), which `SetupGameLoopAssets` now calls too.
  - `AutoStyleMetaUI` (styled the old layout) is deleted. The old `PerkCardPrefab.prefab` is no longer used.

## UI review

- [ ] **Meta window look**: open the Spirit in `Bladehold Meta Area Scene`. Check the Synty art, Texturina headers and Grenze body text, and readability against the light window background, at 16:9 and 16:10/ultrawide. Then buy a few ranks: the pips fill, the price turns red when you can't afford it, and the card reads MASTERED at max rank.
- [ ] **Meta window gamepad**: a pad opens on the first card. D-pad moves through the cards and over to the Buy button, A on a card buys its next rank, and B closes the window.
- [ ] **Supply Cache icon** uses the HUD's supply hammer (`ICON_SM_Item_Hammer_01`), which is coloured where the other perk icons are white stat icons. Swap it if you'd rather it matched them.

## Playtest

- [ ] **Bulwark shield size**: spawn a `bulwark` (DevConsole). The shield should look in proportion and still block sword swings from the front. Tune it with the `SM_Wep_Shield_Plank_01` scale on the prefab.
- [ ] **Tower range**: with double range, towers reach much further. The open "towers have no line-of-sight check" issue in `towers-vs-hero.md` (shooting at goblins on another level) will show up more often.
- [ ] **Fishing pace**: a frenzy should give about half as many level-ups as before. Tune with `firstLevelXp` / `levelXpGrowth` on the Fishing Pond's `FishingManager`.
- [ ] **Supply Cache in a run**: buy rank 1+, start a run, and check the supply HUD shows 80+. It should be back to 60 after deleting the save.
