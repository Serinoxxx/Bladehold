# TODO


## Controller playtest fixes (2026-10-08)

Done in C#/assets: menu focus stack + B-back (`UI/MenuFocusController`, `PauseMenuView`, `SettingsPanelView`), main-menu nav + focus outline (`MainMenuManager`, `UI/UIFocusOutline`), draft-card pad focus (`SurvivorsCardUI`), stick dead-zone setting (`GameSettingsService`, default 0.05), analog RT/LT press-point fix for melee charge and bow aim (`InputReader`), tap = 50% of full-charge damage (`WeaponDefinitionSO.quickAttackDamageFraction`), dismount moved to D-pad Up (`Controls.inputactions`), ultimate wheel opens with nothing lit, directional build/upgrade wheel with stick arrow + gameplay input suppressed (`BuildWheelUI`, `RadialWheel`), live HUD/prompt glyphs (`InputGlyph.AttachTo`, d-pad path fix), campaign map node-stepping + 3D inspected highlight (`Campaign/`), looping airborne baked falls (`BakedCrowdAgent.TickFall`), Bulwark shield dust one-shot + white shield bar (`BulwarkShieldBar`), horse-stall and low-ammo waypoints (`HorseStall`, `AmmoChest`), wall-plot stakes hidden, Tutorial Arena + Valley Stronghold NavMesh rebaked (arena pillars got Not Walkable `NavBlock` volumes: their hollow colliders left islands inside).

Screenshotted on a virtual pad: main menu focus outline, settings footer hints + dead-zone row, HUD glyphs, pause focus tint, build wheel arrow + Cancel glyph.

Manual verification (real pad):
- [ ] RT hold shows the charge bar and swings on release; a tap hits for ~half of a full charge.
- [ ] LT aims the bow / thrown axe / wand.
- [ ] D-pad Up mounts and dismounts; B only dodges (and never dodges when B closes the build wheel).
- [ ] Ultimate wheel opens with no slice lit; mouse hover still works on KBM.
- [ ] Build wheel and tower/wall upgrade wheel: stick picks the slice, arrow lines up, A buys, player can't move while open.
- [ ] Main menu up/down; B backs out of settings (main menu and in-game); B on the pause menu resumes.
- [ ] Pause over "Choose your next battle" keeps focus on the pause buttons; cards regain focus after resume.
- [ ] Draft cards highlight and pick with A.
- [ ] Campaign map steps node to node; castle highlight reads well (tune `CampaignDioramaLook.asset`).
- [ ] Dead-zone slider visibly changes stick response.
- [ ] Baked goblin flung high keeps tumbling until it lands (no mid-air freeze).
- [ ] Bulwark shield hit = single dust puff; white shield bar sits above the HP bar.
- [ ] Horse-stall marker after the horse dies (prep phase only); ammo-box marker under 25% ammo.
- [ ] Tutorial Arena horde no longer spawns in/sticks to pillars; Valley Stronghold paths OK.
- [ ] Still looping vendored `FX_Impact_Wood_01` dust on `DestructibleCatapult`, `Loot Chest` and the Tutorial Dungeon scene — swap to `WoodImpactBurst` if it shows.
