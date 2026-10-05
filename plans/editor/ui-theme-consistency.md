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

## Third pass: Fishing Pond

2026-10-05. **Bladehold > UI > Restyle > Fishing Pond** (also part of *All Screens*) reskins `FishingCanvas` in `Bladehold Fishing Pond.unity`, `FishingBuffFishButton.prefab` and `FishingDraftCard.prefab`. The scene's three draft cards are instances of that prefab and carry no overrides, so they follow it. The canvas has its own `UIMenuId.Fishing`, which uses the global Ember Gold theme. The countdown colours in `FishingHUDUI` now come from the theme (Accent ticks, Success for "FISHING FRENZY!"). Checked with offscreen captures, **not yet played**.

### UI review (Synty art, Texturina headers / Grenze body, gamepad focus, 16:9 and 16:10/ultrawide)
- [ ] **Frenzy HUD**: the stats panel is a dark framed well with a gold XP fill. The timer uses the black-underlay Texturina, so it reads over bright foliage. Gold is themed; blood, metal and diamond keep their currency colours. Check the countdown punch: gold digits, then a green "FISHING FRENZY!".
- [ ] **Level-up draft**: dark cards with a gold frame, hover glow and scale (the old Shadow is gone), a diamond medallion behind the icon, and a flourished "LEVEL UP!" title. Check that the real upgrade icons read in the accent tint, that pad focus moves between the cards, and that the click MMF still plays. The frenzy timer sits behind the title through the dimmer, as it did before.
- [ ] **Tally**: a window frame with gold reward lines, other currencies brightened to the HUD colours, ghost buff-fish buttons with hover glow, and a parchment Continue button. Check the disabled state of the buff buttons after eating one, and that there is a gap above Continue (the buff section has a fixed height).
- [ ] **Start prompt**: now a framed panel with a `HintEntry` glyph bound to StartWave (`FishingHUDUI.startHint`), labelled "Begin Fishing Frenzy" (`fishing.prompt.start`; English only so far). Check that it shows T on keyboard and the D-pad Down glyph on a pad, that it flips live when you switch device, that it follows a rebind, and that D-pad Down starts the countdown.

## Fourth pass: main menu and loading screen

2026-10-05. **Bladehold > UI > Restyle > Main Menu** reskins the title screen in `MainMenu.unity`. Play is a parchment button; Replay Tutorial, Settings and Quit are framed ghost buttons (their Synty Animators were removed and replaced by juice). The patch notes are a dark framed window with Grenze body text, and their category colours come from the theme. `Screen_Title` gained a `MenuFocusController` (default Play, no B action). **Bladehold > UI > Restyle > Loading Screen** rebuilds `Resources/LoadingScreenManager.prefab`: key art (or the area's `previewSprite`) slowly drifting behind a dark lower band, an "Entering" eyebrow, the area name with a flourished rule, subtitle and lore, and a framed gold progress bar with a status line and percentage. The bar now glides rather than jumping. Both are part of *All Screens*. **The main menu now loads through `LoadingScreenManager`** (its own in-scene loading screen was deleted), and the manager prefab got `BladeholdPrewarmVariants` so it still finishes the shader prewarm. Checked with offscreen captures at 16:9, 16:10 and 21:9, plus one Play-mode transition with no console errors.

### UI review (Synty art, Texturina headers / Grenze body, gamepad focus, 16:9 and 16:10/ultrawide)
- [ ] **Title screen in Play mode**: a `manage_camera` Play-mode screenshot came out washed-out and pale, while the offscreen edit-mode renders looked right. Check it by eye in the Game view. If it really is pale, the Synty title intro animator (`AC_Screen_FantasyWarrior_AssetDemo_Title_01`) is the first suspect.
- [ ] **Title buttons**: hover glow and scale on all four. Pad: focus lands on Play when a pad is active. Click sounds: the Synty buttons had no `UIClickFeedback`, so check whether they are silent and whether they should get one.
- [ ] **Patch notes**: there is one blank line between blocks, and wrapped bullet lines indent under their text. The window must not touch the buttons at 16:10.
- [ ] **Loading screen**: pick a slot from the main menu, then go Meta Area → a battle. You should see the "Entering" eyebrow, the gold name, the bar gliding to 100% with "Ready", and then a fade-out. Very long area names auto-size down to 52. None of the `AreaDefinitionSO`s have a `previewSprite` yet, so every area shows the key art. Add per-area art to make each load feel distinct.

### Playtest
- [ ] **New Game and Continue** from the main menu must still reach the tutorial (on a fresh slot) or the Meta Area (on a finished slot). That load path changed in this pass. The loading screen must **never** stay stuck on screen. It waits at most 8 s on `HoldFadeOut`.
