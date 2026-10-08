# Editor to-do: playtest feedback, UI and map batch (2026-10-08)

Groups 2 and 3 of the 15-item playtest list. The mount button and enemy roles have their own files: [mount-button-buy-horse](mount-button-buy-horse.md) and [enemy-roles](enemy-roles.md). Everything below is built from code with nothing to wire. All of it compiles. The hero panel, tooltip, countdown, prisoner map markers, swing indicator, buy-horse popup and ammo crate were screenshotted in Outer Gate Play mode on 2026-10-08. Tick items off as you go, and delete the file when it's empty.

**Done in code and assets:**
- **Hero panel beside the draft cards:**
  - `SurvivorsPlayerInfoSidebarUI` scales the panel up to 1.6x into the free space beside the cards. It also gives the panel taller rows, larger icons, and a skill list stretched to the panel's bottom edge.
  - Hover or gamepad focus on a skill row opens `SidebarSkillTooltipUI`: icon, name, level, description and upgrade text, from the same data as `DraftUpgrades.csv`.
  - Tune it in the "Draft Modal Layout" fields on the sidebar in `Bladehold HUD.prefab`.
- **Objective countdown:** `ObjectiveCountdownUI` shows at the top centre for any `ITimedObjective`: Free Prisoners, Siege Engines, Protect Wagon, Goblin Rush (where running out is the win, so it shows green) and Golden Goblin. It also shows during the rout. It turns red and pulses in the last 10 s, and drops below the boss bar while one is up.
- **Objective markers on the minimap:** `MinimapObjectiveMarker` places a marker for each of the active objective's waypoint targets: cages, cart, ram, catapults, Slayer, captain and Golden Goblin. They're shown dimmed for the picked card during prep. Tune `objectiveMarkerSize` and `objectiveGateDedupeMeters` on `MinimapConfig.asset`.
- **Swing direction:** `SwingDirectionIndicatorUI` (created by `AttackChargeBarUI`) puts left, top and right arcs round the screen centre. The latched swing's arc lights up: gold as the charge fills when you chose the direction by looking, dim when the swing just alternated.
- **Warhorse material:** `CaravanWagon.prefab` and `StampedeRunner.prefab` now override the Malbers horse's 9 renderers with `PolyArt/Undead Rotten.mat`, the same material as the player's horse.
- **Fixed after the screenshot pass:** the skill list floated mid-panel (its content was stretch-anchored with a centred pivot), so `FitSkillListToPanel` now pins it to the top. The swing indicator was a 75 px ring at 1080p; it now defaults to 420 ref px, with thicker arcs and unlit arcs at 0.3 alpha.

**Manual verification:**
- [ ] Open a draft at 16:9, 16:10 and ultrawide. Check:
  - the panel doesn't overlap the third card, even while it's hovered;
  - the list reaches the panel's bottom edge;
  - with 10 or more skills, the mouse wheel and gamepad both scroll it;
  - right from card 3 reaches the rows;
  - the tooltip stays on screen for the top and bottom rows;
  - the ultimate, maxed and elemental tooltip cases read right;
  - the wave-choice modal and the death screen sidebar still work.
- [ ] Countdown:
  - It shows for each timed objective, drains, goes red (green for Goblin Rush) in the last 10 s, and shows for the rout.
  - It doesn't clash with the boss bar, the WavePrepPrompt or the "THE GOBLINS FLEE!" banner.
- [ ] Minimap:
  - Markers appear and track on the corner and expanded maps, vanish when an objective target is freed or destroyed, and show as previews in prep.
  - Tooltips work with mouse and d-pad.
  - There's an "Objective" legend row.
- [ ] Swing indicator:
  - Look left, right or up then swing: the matching arc lights. A plain tap shows a dim arc.
  - It doesn't overlap the charge bar.
  - "Left" reads the right way round on screen.
- [ ] Trigger the Goblin Caravan and Stampede world events: the horses are textured, not grey.
