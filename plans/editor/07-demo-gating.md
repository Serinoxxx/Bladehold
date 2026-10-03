# Editor to-do: plan 07 (Demo gating)

From [plan 07](../07-demo-gating.md), 2026-09-25. Unity MCP was not connected. Tick items off as you go and delete the file when it's empty.

**The demo end panel is in the Campaign Map scene (2026-09-26). Since 2026-10-02 the demo ends after tier 3 (was 4), battles have 3 waves, and the first run pays a gift: see §6.**

## 1. Verify first

- [x] **Unity imports the hand-written demo config.** Checked by an agent via MCP 2026-09-26: all values as listed. `Assets/Bladehold/Resources/DemoConfig.asset` should show as a `DemoConfigSO` with `Demo Enabled` ticked, Allowed Weapons = sword, bow, mace; Allowed Armour Sets = HeroArmourSet, IronVanguardArmourSet; Allowed Mounts = Mount_BasicWarhorse; Max Meta Perk Tier 1; Campaign Cutoff Tier 4. If the fields are empty or the script is missing, the asset/meta GUIDs didn't take, so recreate it via *Create → Scriptable Objects → Demo Config* at the same path. A missing asset logs `[DemoConfigSO] No DemoConfig asset…` and runs as the full game.
- [ ] **Set `Steam Store Url`** on the same asset once the store page exists. The Wishlist button stays hidden while it's blank.

## 2. Wiring (Campaign Map scene)

- [x] **Build a `DemoEndScreen` prefab** (done by an agent via MCP 2026-09-26: Panel + parchment box, header, body, Wishlist + Continue (MenuButton instances), MenuFocusController on Continue; no Show Feedback yet) (`Bladehold Prefabs/UI/DemoEndScreen.prefab`): full-screen raycast-blocking panel with a "Thanks for playing" header, a short line ("The full campaign continues in Bladehold — wishlist on Steam"), a **Wishlist** button and a **Continue** button. Add `DemoEndScreenUI` on the root and assign `Panel Root` (the panel child, *not* the root holding the script), `Continue Button`, `Wishlist Button`, and optionally `Show Feedback` (MMF_Player: open sound + fade). None auto-wire. *(MCP-able as a mockup.)*
- [x] **Place it in `Bladehold Campaign Map Scene`** (done 2026-09-26, last child of the map canvas; checked it shows over the map with Wishlist hidden while the URL is blank) under the map canvas (drawn on top of the nodes and the tooltip) and assign it to `CampaignMapUI` → `Demo End Screen`. Verify: DevConsole → complete nodes up to tier 4 → back on the map the panel appears, Continue loads the Meta Area.

## 3. UI review

- [ ] **Demo end panel:** Synty art (`Assets/Synty/InterfaceFantasyWarriorHUD/`), Texturina header / Grenze body, gamepad focus lands on Continue.
- [ ] **"LOCKED FOR DEMO" text** on weapon/armour/mount pedestals (it reuses each pedestal's cost label), perk cards and the tier II/III unlock buttons: readable, and doesn't look like a price. Wording lives on `DemoConfig` (`Locked Label`, `Locked Prompt`).
- [ ] **Map nodes past tier 3** use the normal lock overlay; the tooltip says "Not available in the demo". Decide whether they need a distinct badge.

## 4. Scene work

- [ ] **Place mount pedestals in the Meta Area** (moved from 00 §B): one per variant in `Resources/Mounts/`. Every variant except Basic Warhorse should read LOCKED FOR DEMO and refuse interaction.

## 5. Playtest (fresh save: delete the save or use the DevConsole reset)

- [ ] Weapon pedestals: Sword and Bow owned, Mace buyable for 10 Metal; Axe, Throwing Axe, Staff and Wand say LOCKED FOR DEMO and can't be bought.
- [ ] Armour: Hero equipped, Iron Vanguard buyable; Windrunner Mail and Dread Champion locked.
- [ ] Spirit NPC: the 4 tier-1 perks are buyable; tier II/III rows are dimmed, their unlock buttons say LOCKED FOR DEMO, and hovering a card shows the demo line.
- [ ] Sword ultimate label on the pedestal reads BLADE TEMPEST; Mace reads SEISMIC QUAKE.
- [ ] Campaign: tiers 4–8 visible but locked (Great Banqueting Hall shows as a padlocked teaser). Clear a tier-3 **fishing pond**, the **Rest Area** and **Frozen Pass** once each: all three end on the demo panel (the sector victory still says "Proceed to Campaign Map"; that's expected).
- [ ] **Never:** a tier-5+ node becomes clickable; drafts offer cards for a locked weapon.
- [ ] Untick `Demo Enabled` on `DemoConfig`: everything unlocks, sectors are back to 5 waves, and tier 3 leads on to tier 4 as before.

## 6. Shorter demo + first-run gift (2026-10-02, Unity MCP connected: compiled clean, nothing play-tested)

- [ ] **Asset values took.** `Resources/DemoConfig.asset`: Campaign Cutoff Tier **3**, Max Waves Per Sector **3**. `Resources/TutorialConfig.asset`: First Run Gift Goblin Blood **30**, Orcish Metal **10** (new fields, so they show their code defaults until the asset is saved once). *(MCP-able.)*
- [ ] **Wave count:** start any combat sector in the demo: the HUD reads `WAVE 1 / 3`, wave 3 is the final (captain) wave, and clearing it is a sector victory. A Bomber should never spawn.
- [ ] **Gift on first death, fresh save:** Play from New Game (or skip the tutorial), die in Outer Gate with DevConsole → **Die**, Return to Meta Area. The currency HUD shows +30 Blood / +10 Metal over what the run earned. The `hint.meta_gift` hint appears with a waypoint on the **Mace** pedestal; once it fades, the Spirit hint appears (it must wait, not cut the gift hint off).
- [ ] **Gift without dying:** on another fresh save, clear the demo (DevConsole node jumps are fine) and press Continue on the demo end panel: the gift pays on arrival in the Meta Area.
- [ ] **Never:** the gift pays a second time (die again: no extra +30/+10); the gift hint shows when no weapon is affordable; tutorial-arena deaths (room reload) pay it.
- [ ] **Hint watcher in the Meta Area:** the hints rely on the `FirstTimeHintsWatcher` inside the `Bladehold HUD` instance in `Bladehold Meta Area Scene`. If no gift hint appears at all, check that instance is there and enabled.
- [ ] **Hint wording/length** (`hint.meta_gift` in `Strings.csv`, en only; other languages blank until the next translation pass). Tune `Gift Hint Seconds` (10) on the watcher if it's too long to read.
- [ ] **Decision: gift size.** 30 Blood buys 3 of the 5 tier-1 perks at rank 1; 10 Metal buys exactly the Mace. Raise Blood to 50 if you want every tier-1 perk available straight away.
