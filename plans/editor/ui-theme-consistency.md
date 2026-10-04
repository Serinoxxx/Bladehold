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
- [x] **Other screens to bring over**: done 2026-10-05, see the second pass below.

## Playtest
- [ ] Buy something in the shop. Fail to afford something. Learn a perk. Unlock/equip a weapon at a pedestal. No colour should be hardcoded white/green/red any more; everything follows the theme.
- [ ] The rebuild must **never** break shop purchases, perk buying or pedestal prompts. All references were rewired by name, but play each once.

## Second pass: death screen, campaign map, draft + wave cards, save slots, boss banner

2026-10-05. `ScreenRestyleBuilder` (Bladehold > UI > Restyle > …) reskins these in place: same objects, references and MMF players, new sprites, colours and fonts on theme roles. Parchment cards became dark framed wells; titles are gold Texturina with flourishes. The boss intro banner (`EnemyIntroUI` in `Bladehold HUD.prefab`) was rebuilt: dark band, gold rules, eyebrow line, name, tier skulls and subtitle. Letterbox bars appear only on cinematic boss intros, not when a captain arrives. Checked with offscreen captures, **not yet played**.

### UI review (Synty art, Texturina headers / Grenze body, gamepad focus, 16:9 and 16:10/ultrawide)
- [ ] **Boss banner**: spawn a captain from the DevConsole and trigger a special-enemy intro. The band should wipe open, the name settle in, the flourishes spread out, and everything fold away after about 3.5 s. Check that long names auto-size and that the banner never blocks clicks.
- [ ] **Draft cards**: level up. Check the hover glow (MMF), the banish button, the reroll button, and that element icons (fire/ice/lightning tints) read on the dark card. Weapon cards now tint their icon with the theme accent instead of dark brown.
- [ ] **Wave cards**: the red/blue stance bands are unchanged. 1-skull cards now use a light skull and empty skulls are faint. Check the hover glow.
- [ ] **Death / victory screen**: lose once and win once. The panels end above the button row, and the buttons are parchment plates with hover glow (their Synty Animator was removed). Check pad focus moves between Proceed and Return. The currency counters top-left were not touched.
- [ ] **Save slots** (main menu): card hover glow, the delete button (now a ghost square with a red skull), and the delete dialog's hold-to-delete fill (red over a dark plate).
- [ ] **Campaign map**: node borders are now a thin frame coloured by status (gold available, green completed, dim locked); the tooltip uses theme colours. The node fills keep their category colours.

### Decisions
- [ ] **Per-screen themes?** All six screens use the global Ember Gold theme. Their new `UIMenuId`s (DeathScreen, CampaignMap, Draft, SaveSlots, Hud) can each get their own override in `UIThemeRegistry.asset`.
- [ ] **"Seige Breaker"**: that enemy's display name is misspelt (should be "Siege"). Not changed here.
