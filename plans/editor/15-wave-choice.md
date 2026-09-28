# Editor to-do: plan 15 (Wave choice draft)

From [`plans/15-wave-choice-draft.md`](../15-wave-choice-draft.md), session 1 (Phases A+B), 2026-09-28. Unity MCP was connected. Tick items off as you go and delete the file when it's empty.

**Session 2 (2026-09-28): Phases C-E are in code.** The wave cards now drive the real flow. Everything below needs Play-mode checks, and some items need scene wiring.

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

## Session 2 wiring (Phases C-E)

What's done in C#:
- **Flow:** `Waves/GameLoopManager.cs` was rewritten. It has `CurrentWaveCard`, the draft → prep → hold Ready → countdown sequence, objective resolution, the rout (`Enemies/EnemyRout.cs`), and the reward bundle.
- **Objectives:**
  - `SurvivorsObjectiveManager.StartObjective(id)`.
  - `Objectives/DefeatCaptainObjective.cs` (wave 5).
  - `Objectives/ObjectiveCsv.cs`: timers now come from the CSV.
  - Offence fail rules: the Golden Goblin fails if it escapes, the wagon has a 3:00 timer, and the siege engines no longer damage the gate.
  - `IObjectivePreview` markers.
- **Gate repair:** `Fort/GateRepairStation.cs`, hold [E] during prep.
- **Input:** `DraftSkills` → `StartWave` (T / D-pad Down), also used by the Fishing Pond start.
- **Telemetry:** `wave_choice` rows, and `wave_clear` detail (outcome, card, reward, gate repair supply).
- **DevConsole:** Reroll Draft (wavecards), Force (wavecard <id> <skulls>), and Ready.

Wiring:
- [x] **Captain Assault in the scenes** (done via MCP 2026-09-28: added to the Survivors scene; Ancient Garden and Frozen Pass already had it through the prefab):
  - Add a `DefeatCaptainObjective` component to the SurvivorsObjectives object in `Bladehold Survivors Scene`, `Bladehold Ancient Garden` and `Bladehold Frozen Pass Scene`. They hold unpacked inline copies, so the prefab change doesn't reach them. Save each scene.
  - Without it, wave 5 falls back to a random objective plus a manager-spawned captain, and logs an error.
  - Optionally, set its `captainWaypointIcon`.
- [ ] **Gate repair station in each battle scene** (Survivors, Ancient Garden, Frozen Pass):
  - Add a `GateRepairStation` as a child of the Gate on the player's side, so `gate` auto-wires. Keep it clear of TowerPlot radii, because the closest interactable wins [E].
  - Author `RepairLoopMMF` (looping hammer sound) and `RepairTickMMF` (a tick or gate-bar pulse), and wire them to `repairLoopFeedback` / `repairTickFeedback`.
  - Consider making it one prefab nested in each scene.
- [ ] **Countdown text:** the `waveAnnouncementText` / `intermissionTimerText` / `rewardNotificationText` fields on GameLoopManager are empty in the scenes. The objective tracker already shows the Ready prompt, the countdown and the rout line (`GameLoopManager.StatusText`), so wiring them is optional. Check the 3-2-1 is readable where it is, and wire a bigger centre-screen text if it isn't.
- [ ] **Sidebar gate row in the Survivors scene:** saving the scene serialized `gateHealthText: None` on its sidebar, so the scene's own sidebar copy may not show gate HP. Check it, and wire `GateHPLabel` if it's missing.
- [ ] **Siegebreaker spawn points:** `DefeatSlayerObjective.spawnPoints` on the prefab is `[None]`, so its prep preview shows nothing. Assign real spawn transforms.
- [ ] **Inspector clean-up on GameLoopManager** (the prefab and each scene): the old `bannerConfig`, `warBannerPrefab`, `bannerSpawnPoints`, `upgradePowerupSpawnPoint` and `upgradePowerupPrefab` fields are gone from code. Their stale YAML clears on the next save. The `BannerSpawnPoint_*` and `UpgradePowerupSpawnPoint` scene objects can be deleted.
- [ ] **Unity re-import of `Controls.inputactions`:** if "Generate C# Class" is on, check that the regenerated `Controls.cs` still has `StartWave` (it was hand-edited to match).

## Manual verification (Play mode, Survivors scene)

- [ ] **The new run loop.** Start a new run: sector → build → hold [T] (and D-pad Down on a pad) for 1 s → 3-2-1 → wave 1, Hold the Gate.
  - The kill target equals the wave quota.
  - Releasing [T] early resets the hold.
- [ ] **After wave 1:**
  - quest complete, then the stragglers flee to the spawns and vanish within ~4 s;
  - the reward banner appears;
  - 3 cards appear, with at least 1 Defence and 1 Offence.
- [ ] **Offence pick → prep:**
  - the preview markers show (cages, catapults, ram start, wagon route);
  - towers and gate repair work; they don't once Ready is held.
- [ ] **Offence fail** (let a timer run out):
  - the fail banner shows and there's no card reward;
  - kill gold and the +30 supply are still paid;
  - the stragglers rout.
- [ ] **Success pays exactly what the card showed:** gold, supply and the bonus. A draft-pick bonus opens the skill modal, twice on a 3-skull card.
- [ ] **3 skulls:**
  - a captain spawns;
  - enemies have ~30% more HP;
  - the clan buff is visible. Check each of the 5 (Shield, Haste, Berserk, Regen, Armor) at least once with **Force (wavecard)**: `EnemyBuffController` had never run before this.
- [ ] **Golden Goblin:**
  - no regular spawns;
  - an escape = fail, no reward;
  - a kill = one completion (not two).
- [ ] **Wagon:** it fails at 3:00 if not delivered, and no gold bags drop on arrival.
- [ ] **Wave 5:** Captain Assault spawns the node's captain. Killing it wins the sector, and no cards appear after wave 4's clear → prep.
- [ ] **Bannerman aura** colour follows the card's clan.
- [ ] **Campaign tooltip:** shows "Clan: <name>" and no bounty row.
- [ ] **Telemetry CSV** (`persistentDataPath/Telemetry`): has `wave_choice` rows, and `wave_clear` details with outcome, card, reward and gate repair supply.
- [ ] **Fishing Pond:** T and D-pad Down start the countdown.
- [ ] **Pseudo-locale:** every new string shows `[xx]`.

## After a playtest confirms (plan 08-style deletion)

- [ ] Delete `Waves/Banners/WarBannerController.cs`, `Waves/WaveUpgradePowerup.cs`, `WarBannerRewardSO` + `WarBannerConfigSO` (scripts and assets), the war banner prefab, and the `Powerups/WaveReward_*` prefabs.
- [ ] Also delete `KillRemainingEnemiesObjective`, since the rout replaced it.
- [ ] Keep `WarBannerClanSO`, `EnemyBuffController` and `BannerDifficultyTier`/`BannerDifficultyHelper`: they're still used by card clans and captain tiers.
- [ ] Remove `CampaignNodeSO.bountyType` / `difficultyTier`, which are no longer read by the sector.

## Session 3: prep prompt (done via MCP)

- `UI/WavePrepPromptUI.cs` sits at `Bladehold HUD.prefab` → `Survivors HUD/WavePrepPrompt`, top centre, 190 px down. It contains:
  - a "PREPARE YOUR DEFENCES" header and a "Next: <objective>" line;
  - a StartWave glyph with "Hold to start next wave";
  - a gold hold bar;
  - a big 3-2-1 during the countdown.

  The objective tracker line now reads "Build your defences, then start the wave" instead of repeating the key.
- [ ] **UI review:**
  - check the position against the top-centre HUD elements (compass, boss bar, enemy intro) at 16:9 and 16:10;
  - check readability over bright ground (add a soft dark backing band if needed);
  - check the glyph shows T on keyboard and D-pad Down on a pad.
- [ ] **Countdown feel:** `countdownBeatFeedback` is empty. Copy the Fishing Pond's countdown punch + thump MMF onto `WavePrepPrompt` and wire it.

## Session 3: victory screen + campaign tooltip (done via MCP)

- **Victory screen** (`DeathScreen.prefab`, `UI/DeathScreen.cs`, `UI/SectorRewardRowUI.cs`):
  - a top title band;
  - three panels: Sector Stats, Sector Rewards (from `GameLoopManager.Summary`: waves won/lost, card rewards, tower refund, captains defeated), and Hero;
  - one bottom-anchored button.
  - Damage/crit stats now read the run totals.

  The Survivors scene's unpacked copy was replaced with a prefab instance. The Demo and Test scenes still have unpacked copies.
- **Campaign tooltip** (`Campaign/CampaignTooltipUI.cs`, `CampaignMapUI.cs`, the Campaign Map scene):
  - it no longer blocks raycasts, which was the cause of the flicker;
  - it sits beside the node, clamped to the screen;
  - it's restyled as a dark Synty box that sizes to its content, with a type tag, captain + skull icons, reward chips and a footer state line.

  The node buttons' "□□" emoji skulls are removed, along with the same skulls in the captain intro.
- [ ] **UI review, victory:**
  - a real castle-sector win with rewards and a captain kill;
  - campaign-complete;
  - defeat (banner, then screen);
  - pad focus on proceed;
  - 16:10 and ultrawide;
  - a long skills list.

  Known limits: bow and elemental damage aren't in the Damage Dealt total, and with only two panels on defeat they stretch wide.
- [ ] **Translations:** the `endscreen.*` rows for Supply / Goblin Blood / Orcish Metal / Troll Heart / Sanctuary / tower refund are English-only (`/translate-game-name`).
- [ ] **UI review, tooltip:**
  - Play-mode hover over and around the tooltip (no flicker);
  - gamepad focus while the map scrolls;
  - the gold frame's corner notches.
- [ ] **Campaign node data:** some reward summaries still mention bounties that no longer exist ("Weapon Upgrade Draft", "Captain Fraglob Bounty"). Clean them up in `CampaignGraphSO` / the node assets.
