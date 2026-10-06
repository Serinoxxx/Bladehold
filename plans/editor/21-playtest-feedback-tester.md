# Editor to-do: plan 21, external tester feedback (2026-10-07)

Editor leftovers from `plans/21-playtest-feedback-tester.md`. Tick items off as you go, and delete the file when it's empty.

## Phase 1: Quick fixes

Unity MCP was down for this phase. `dotnet build` is clean for runtime and Editor, but nothing has been checked in Unity yet.

**Done in code and assets:**
- **Mouse sensitivity:**
  - Default 0.1, range 0.01–1, and the typed field takes 3 decimals.
  - Old saves on 0.5 move to 0.1. Other values are clamped into the new range.
  - The slider range is forced in code, so the scene and prefab copies of the settings panel don't need editing.
- **Build wheel:** no more second cost line on each option.
- **Wall health bar:**
  - The minimum height is 7.5 (was 5.8).
  - The bar also auto-raises 1.5 m above the top of the wall's tallest mesh, refitted each time it fades in.
- **Tutorial gate music:** prep and scene music now use `Music_Prep` (your Tavern LOOP SLOW edit).
- **Black main menu:** not reproduced. Added a safety net: on the title screen, `Time.timeScale` and the Feel time-scale stack are reset, and a stuck loading screen is force-hidden after 3 s. Both log when they fire.

**Wiring checklist:**
- [ ] Commit your `Music_Prep.asset` change (Tavern LOOP SLOW). The tutorial gate now relies on it being the calm track.
- [ ] Optional: re-run the settings panel builder, so the authored slider in `SettingsPanel.prefab` / `PauseMenuCanvas.prefab` also says 0.01–1 (the code already overrides it at runtime).

**Manual verification:**
- [ ] Settings → Controls → Sensitivity:
  - The slider starts at 0.100 on a fresh save, and 0.075 typed in sticks.
  - Typing 0 clamps to 0.010.
  - Dragging to max gives 1.000.
  - Check this in both the main menu settings and the pause menu settings.
- [ ] An existing save that was on 0.5 opens at 0.100. One set to a custom value (e.g. 2.0) opens clamped at 1.000.
- [ ] Build wheel at a tower plot: each option shows its name once and its cost once, in red when unaffordable. Clicking an affordable option still builds it; an unaffordable one does nothing. Check with a gamepad too.
- [ ] Build a wall, upgrade it through every tier, and look at it from the prep camera and up close:
  - The bar sits clear above the model, including on the gatehouse.
  - It's not absurdly high.
  - If it is too high, lower `clearanceAboveModel` on `Wall.prefab`'s health bar.
- [ ] Tutorial gate prep phase: the calm tavern track plays, not the dark dungeon one.
- [ ] **Black main menu, in a player build:**
  - Quit to the main menu from the pause menu in a battle, in the tutorial gate, during the gate-fall slow-mo, and from the death and victory screens.
  - The title screen should show its background and buttons.
  - Check `Player.log` for `[MainMenuManager]` warnings or errors. Either one names the cause; report it so the real fix can replace the safety net.
