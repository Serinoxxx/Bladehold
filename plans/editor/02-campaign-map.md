# Editor to-do: plan 02 (Campaign Map)

From [plan 02](../02-campaign-map-review.md), 2026-09-25. Tick items off as you go and delete the file when it's empty.

## Done by an agent via MCP (2026-09-26)

- [x] **DeathScreen in the boss scenes.** `DeathScreen.prefab` added to `Bladehold Necromancer Crypt` and `Bladehold Princess Sanctuary` (root, no overrides, same as the castles). They had no `PauseMenuCanvas` either, so that went in too. Checked: dying in the Sanctuary shows the defeat screen.
- [x] **Crypt cleanup.** `ConfrontationTrigger` (missing script only) deleted.
- [x] **UI review: `CampaignNodeButton.prefab` / `CampaignPathLine.prefab`.** (yours)
- [x] **Map check.** (yours)

## Your test notes, fixed

- **"I don't see any map / cursor gets swallowed".** Two bugs:
  - The map scene never asked `CursorLockManager` to free the cursor, so it stayed locked (every other UI screen does this). `CampaignMapUI` now unlocks it on open.
  - The node and path prefabs were anchored to the container's **centre**, but node positions are measured from its **left edge**, so the whole graph sat ~1250 px to the right and mostly off-screen. Both prefab roots are now anchored at (0, 0.5). The header text had a broken width too (3700 px); fixed, and the header/currency labels use Texturina/Grenze now.
- **WASD / gamepad navigation.** WASD, the arrow keys, the d-pad and the left stick now move focus to the nearest node in that direction (it prefers staying in the same row). Locked nodes can be browsed, so their tooltip shows, but not deployed to. Enter / Space / E / pad A deploys to an available node. The view scrolls to keep the focused node centred, and the mouse still works as before; hovering takes over the focus. Tunables are on `CampaignMapUI` (Keyboard / Gamepad Navigation).

## Still yours

- [ ] **Play-test the map controls** from the Meta Area: portal → map, move with WASD and a pad, check the tooltip follows, deploy with Enter / A. Mouse click-deploy still works once.
- [ ] **UI review: map tooltip.** `CampaignTooltip` is a bright yellow box that covers half the map when a node is focused. It needs an art pass (Synty parchment, smaller).
- [ ] **Player build check.** Crypt → the monologue opens → Obey → Princess → kill → "CAMPAIGN COMPLETE!" → Meta with the run cleared. Repeat with Defy (Necromancer boss).
- [ ] *(Pre-existing, noticed)* Both boss scenes still have `GameLoopManager.bannerSpawnPoints` unassigned.
