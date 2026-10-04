# Editor to-do: UI theme + menu consistency

Not from a numbered plan. 2026-10-05, Unity MCP connected. The theme system was built and the builders were run by an agent. Shop, Spirit (meta perks) and pedestal screens were rebuilt in the settings-panel style; settings now uses theme roles too. Checked with offscreen captures, **not yet played**. Tick items off as you go and delete the file when it's empty.

## Verify first
- [ ] **Recovered scene backups**: Unity crashed before this session, and its recovery dialog copied the backups to `Assets/_Recovery/0.unity`, `0 (1).unity` and `0 (2).unity`. Open them, keep anything you need, then delete the folder. The Graveyard scene was left open and dirty, and the agent did not save it.
- [ ] **Console is clean after a recompile**: no `No field` errors from `SettingsPanelBuilder`. Also no serialization warnings on `SettingsTabButton` / `SettingsRowHighlight` / `MetaPerkCardUI` (their colour fields changed from `Color` to `UIColorRole`).

## UI review (Synty art, Texturina headers / Grenze body, gamepad focus, 16:9 and 16:10/ultrawide)
- [ ] **Shop** (Rest Area → Merchant): framed window, 3–7 offer cards, gold wallet in the footer. Check that the price turns red when you can't afford it, the buy shake/red flash/pop still play, and the row shrinks to fit with Deep Pockets + 2 ultimates + replacement horse. Pad: focus starts on the first buy button, B closes. *(new: there was no pad support before)*
- [ ] **Spirit window** (Meta Area → Spirit NPC): tier sections, cards (hover glow, gold selected frame, pips), details panel, Learn button, and blood/metal wallet in the footer. Pad: card focus, B closes.
- [ ] **Pedestals** (Meta Area weapon + armour pedestals): the card now has a fixed width, its bottom edge sits 1.1 m below the canvas origin, and it grows upward. Check it doesn't cover the floating weapon/armour model from the usual camera angle, and that EQUIPPED / LOCKED / price colours read well. Tune `CanvasScale` / `BottomBelowOrigin` in `PedestalPanelBuilder.cs` and re-run if not. *(MCP-able)*
- [ ] **Settings** should look exactly as before (pause menu, main menu). It only gained theme tags.
- [ ] **Theme picks**: Ember Gold (global, settings), Spirit (teal, meta perks), Merchant (bronze, shop), Armoury (steel, pedestals). Edit the colours in `Assets/Bladehold/UI Themes/UITheme_*.asset`. In Play mode the edits repaint open menus live. In Edit mode, run **Bladehold > UI > Themes > Apply UI Themes** afterwards so the prefabs match.

## Decisions
- [ ] **How distinct should menus be?** To put every menu on one scheme, untick `useMenuOverrides` on `Assets/Bladehold/Resources/UIThemeRegistry.asset`. To change only one menu, point its override at another theme.
- [ ] **Other screens to bring over** (not done here): death screen, campaign map, draft cards, wave card, save slots. Each needs a builder on `BladeholdUIKit`, or at least a `UIThemeScope` + `UIThemeTools.TagByPalette` pass. *(MCP-able)*

## Playtest
- [ ] Buy something in the shop. Fail to afford something. Learn a perk. Unlock/equip a weapon at a pedestal. No colour should be hardcoded white/green/red any more; everything follows the theme.
- [ ] The rebuild must **never** break shop purchases, perk buying or pedestal prompts. All references were rewired by name, but play each once.
