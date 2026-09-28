# Editor to-do: plan 15 (Wave choice draft)

From [`plans/15-wave-choice-draft.md`](../15-wave-choice-draft.md), session 1 (Phases A+B), 2026-09-28. Unity MCP was connected. Tick items off as you go and delete the file when it's empty.

**Phases A+B only.** The flow still uses war banners: nothing opens the wave cards in a real run until Phase C. Preview them with the DevConsole (below).

**Done by an agent via MCP:**
- Created `Assets/Bladehold/Resources/WaveChoiceConfig.asset` (CSV + the 5 clans assigned).
- Built `Bladehold Prefabs/UI/WaveCard.prefab`. The hover/select MMF players are copied from `Card.prefab` and retargeted at the card's own `Visual`/`Glow`.
- In `Bladehold HUD.prefab` → `SurvivorsCardSelectModal`:
  - added an empty `WaveCardsRow` and a `MenuFocusController` (B-cancel off, focus trapped in the modal);
  - wired `skillCardsRow` / `waveCardsRow` / `waveCardPrefab` / `focusController` on `SurvivorsCardSelectUI`;
  - added `Sidebar_PlayerInfo/HeaderSection/GateHPLabel` and wired it to `gateHealthText`.
- After your cut-down (2026-09-28):
  - the card is a fixed 720×999 (fits every row);
  - the optional rows (`TimerRow`, `ClanRow`, `CaptainRow`, `BonusRow`, `FreshRow`) each have a `CanvasGroup` + `LayoutElement.minHeight`, and fade out instead of collapsing (`WaveCardUI.reserveHiddenRows`), so the three cards line up row for row;
  - a flexible `Spacer` sits above `Divider`.
  - **If you add a row or grow text, raise the card height to match**, or the layout squeezes rows with no minHeight to nothing.

## Verify

- [ ] **Benchmark section 27** (wave choice mix, reward maths, fixed waves): open `MainMenu`, run **Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark**, and check that section 27 has no `[FAIL]`. Don't save the scene afterwards.

## UI review: wave cards (agent mockup)

- [ ] **Open it:** Play `Bladehold Survivors Scene` → backquote → **Wave Choice preview** → `<` / `>` sets the wave (2-4) → **Show Wave Cards**. The console logs the pick.
- [ ] **Judge:**
  - Synty art choice (parchment card, tinted stance band, skull symbol, Currency/Wood/Bottle/Ingot/Heart/Star icons);
  - Texturina headers / Grenze body, and text size at 1080p;
  - glance order (stance → title → skulls → clan → reward);
  - layout at 16:9 and ultrawide.
  - **Placeholder:** the Goblin Blood (bottle) and draft pick (star) icons need real icons.
- [ ] **Gamepad:** with a pad active, focus starts on the first card, D-pad left/right moves between cards, and A picks. B does nothing (the choice is mandatory).
- [ ] **Feel:** hover, select and pad focus play the copied `Card.prefab` feedbacks (scale spring + glow + sound). Tune them on `WaveCard.prefab` → `Visual/HoverEnterFeedback` etc.
- [ ] **Sidebar gate row:** `Gate: 340 / 600 HP` shows under player HP when a gate exists, and hides in scenes without one. Check it doesn't crowd the stat rows below.
