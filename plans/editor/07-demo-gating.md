# Editor to-do: plan 07 (Demo gating)

From [plan 07](../07-demo-gating.md), 2026-09-25. Unity MCP was not connected. Tick items off as you go and delete the file when it's empty.

**Blocking: until the demo end panel is in the Campaign Map scene, clearing a tier-4 node logs an error and drops you straight back to the Meta Area with no Thanks for playing screen (section 2).**

## 1. Verify first

- [ ] **Unity imports the hand-written demo config.** `Assets/Bladehold/Resources/DemoConfig.asset` should show as a `DemoConfigSO` with `Demo Enabled` ticked, Allowed Weapons = sword, bow, mace; Allowed Armour Sets = HeroArmourSet, IronVanguardArmourSet; Allowed Mounts = Mount_BasicWarhorse; Max Meta Perk Tier 1; Campaign Cutoff Tier 4. If the fields are empty or the script is missing, the asset/meta GUIDs didn't take, so recreate it via *Create → Scriptable Objects → Demo Config* at the same path. A missing asset logs `[DemoConfigSO] No DemoConfig asset…` and runs as the full game.
- [ ] **Set `Steam Store Url`** on the same asset once the store page exists. The Wishlist button stays hidden while it's blank.

## 2. Wiring (Campaign Map scene)

- [ ] **Build a `DemoEndScreen` prefab** (`Bladehold Prefabs/UI/DemoEndScreen.prefab`): full-screen raycast-blocking panel with a "Thanks for playing" header, a short line ("The full campaign continues in Bladehold — wishlist on Steam"), a **Wishlist** button and a **Continue** button. Add `DemoEndScreenUI` on the root and assign `Panel Root` (the panel child, *not* the root holding the script), `Continue Button`, `Wishlist Button`, and optionally `Show Feedback` (MMF_Player: open sound + fade). None auto-wire. *(MCP-able as a mockup.)*
- [ ] **Place it in `Bladehold Campaign Map Scene`** under the map canvas (drawn on top of the nodes and the tooltip) and assign it to `CampaignMapUI` → `Demo End Screen`. Verify: DevConsole → complete nodes up to tier 4 → back on the map the panel appears, Continue loads the Meta Area.

## 3. UI review

- [ ] **Demo end panel:** Synty art (`Assets/Synty/InterfaceFantasyWarriorHUD/`), Texturina header / Grenze body, gamepad focus lands on Continue.
- [ ] **"LOCKED FOR DEMO" text** on weapon/armour/mount pedestals (it reuses each pedestal's cost label), perk cards and the tier II/III unlock buttons: readable, and doesn't look like a price. Wording lives on `DemoConfig` (`Locked Label`, `Locked Prompt`).
- [ ] **Map nodes past tier 4** use the normal lock overlay; the tooltip says "Not available in the demo". Decide whether they need a distinct badge.

## 4. Scene work

- [ ] **Place mount pedestals in the Meta Area** (moved from 00 §B): one per variant in `Resources/Mounts/`. Every variant except Basic Warhorse should read LOCKED FOR DEMO and refuse interaction.

## 5. Playtest (fresh save: delete the save or use the DevConsole reset)

- [ ] Weapon pedestals: Sword and Bow owned, Mace buyable for 10 Metal; Axe, Throwing Axe, Staff and Wand say LOCKED FOR DEMO and can't be bought.
- [ ] Armour: Hero equipped, Iron Vanguard buyable; Windrunner Mail and Dread Champion locked.
- [ ] Spirit NPC: the 4 tier-1 perks are buyable; tier II/III rows are dimmed, their unlock buttons say LOCKED FOR DEMO, and hovering a card shows the demo line.
- [ ] Sword ultimate label on the pedestal reads BLADE TEMPEST; Mace reads SEISMIC QUAKE.
- [ ] Campaign: tiers 5–8 visible but locked. Clear the tier-4 **fishing pond** once and the **Great Banqueting Hall** once: both end on the demo panel (the sector victory still says "Proceed to Campaign Map"; that's expected).
- [ ] **Never:** a tier-5+ node becomes clickable; drafts offer cards for a locked weapon.
- [ ] Untick `Demo Enabled` on `DemoConfig`: everything unlocks and tier 4 leads on to tier 5 as before.
