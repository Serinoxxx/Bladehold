# Editor to-do: Mount charge, permanent horse loss, horse HUD

Not from a numbered plan. Built 2026-10-04 with Unity MCP connected; everything below the "Verify" section was wired and play-checked by the agent in Outer Gate and the Rest Area. Tick items off as you go and delete the file when it's empty.

**Read the horse health decision first:** the warhorse has 12 max HP, it soaks every hit while you ride, and it's now gone for good when it dies.

## Verify (already done by the agent, recheck if anything looks off)

- [ ] **Charge vs cruise trample:** `Bladehold Prefabs/Horse/HorseSO.asset`. The existing trample values (no slowdown, full damage) now apply **only while charging**. The new **Cruise** block (damage 0.15, knockback 0.5, speed loss 0.15 per victim, crowd drag 0.12, floor 0.35) applies when riding without Shift. **Trample Min Speed Fraction** went from 0 to 0.3, so a standing horse no longer hurts anyone.
- [ ] **Stamina from kills:** same asset. **Stamina Regen Per Second** 15 → 2, new **Stamina Per Kill** 8 (on foot) and **Mounted Kill Stamina Fraction** 0.5. Trample kills earn nothing unless the player has the Bloodlust card. That's about 13 on-foot kills for a full bar (4 s of charge).
- [ ] **Assets created:** shop items `Bladehold Config/ShopItems/{replacement_warhorse, horse_poultice, barding_plates, sack_of_carrots, riding_spurs}.asset`; meta perks `Bladehold Config/MetaPerks/{war_bred, stable_hand, cavalry_drills, loyal_steed}.asset` (added to `Resources/MetaPerkCatalog.asset`, which is what the Spirit's screen reads). `Bladehold Rest Area Scene` → `ShopUI`: 4 items added to **Item Pool**, **Replacement Mount Item** = `replacement_warhorse`.
- [ ] **Player prefab:** `MountChargeAbilities` added to `SidekickSyntyCharacter` beside `PlayerMount` (**Mount** auto-wires in `OnValidate`, **Enemy Layers** = Enemy).

## Wiring left for you

- [ ] **Feedback slots, all optional and empty** *(human: pick sounds/VFX)*:
  - `PlayerMount` → **Mount Lost Feedback** (horse dies for good) and **Loyal Steed Feedback**.
  - `Bladehold HUD` → `Mount Vitals` → `HorseBarGroupUI` → **Mount Show Feedback** / **Mount Lost Feedback** (must not animate the group's alpha, which the script drives).
  - `Horse Stamina Bar` → `HorseStaminaUI` → **Ready Feedback** (glint when the charge becomes ready).
  - `MountChargeAbilities` → **Frost Pulse Feedback** (Frost Wake has no visual yet, so enemies just turn chilled) and **Fire Trail Vfx Prefab** (falls back to the small fire-status flame, which reads weakly at gallop speed).
- [ ] **Binary castle scenes** (Armory, Conservatory, Dungeons, Ramparts, Great Hall, Throne Antechamber, Crypt, Sanctuary): couldn't be grepped for HUD overrides. Open one via MCP or by hand and check that the minimap sits top-right and the horse bars sit above the health bar *(MCP-able)*.

## UI review

- [ ] **Horse vitals** (`Bladehold HUD` → `Bottom Left/Mount Vitals`): copies of the player health bar, horse diamond, tan health fill, gold stamina fill, "Charge ready" tag, red "Fallen" line. Check the Synty art, Grenze text, and both 16:9 and ultrawide. The group shows at 70% alpha on foot, 100% when mounted, 85% when the horse is dead.
- [ ] **Minimap in the top-right** (`Minimap/Frame`, anchored top-right at (−40, −96)). The `TutorialHint` moved to (−260, −380) to stay clear of it; trigger a hint and check nothing overlaps.
- [ ] **Shop panel widened** (`ShopUI.prefab` → `ShopPanel` 1150 → 1720, `SlotsContainer` 1080 → 1640). Six cards fit at full size; a seventh (Deep Pockets) shrinks the row automatically.

## Decisions

- [ ] **Horse max health** (`Bladehold Prefabs/Horse/HorseHealthSO.asset`, **Max Health** 12, shared by both horse prefabs). With permanent loss, a few goblin hits now end the horse for the run.

  | Option | Feel |
  |---|---|
  | Keep 12 | Very punishing; the horse becomes a short burst you protect |
  | ~30–40 | Survives a wave if you charge smartly; loss still stings |
  | Keep 12, but stop forwarding all player damage to the horse | Bigger change: the rider takes some hits |
- [ ] **Prices:** Replacement Warhorse 75, Poultice 20 (heals 50%), Barding 35 (+25% HP), Carrots 30 (+30% stamina from kills), Spurs 30 (+10% speed). Perks: War-Bred and Stable Hand tier 1 (10/15/20/25), Cavalry Drills tier 2 (25/35/45), Loyal Steed tier 2 one-off 25 (tier 2 is hidden in the demo).
- [ ] **Stable Hand triggers on every area entry, including the Rest Area.** Fine as "rest heals", or limit it to battle scenes?

## Playtest

- [ ] Ride without Shift into a pack: enemies get shoved and barely hurt, and the horse bogs down. Hold Shift at speed: full trample, no slowdown, the stamina bar turns pale gold and drains.
- [ ] Kill enemies on foot: the bar fills while dismounted, "Charge ready" appears at 35%. Mounted sword kills fill it at half rate.
- [ ] Get the horse hurt, dismount, resummon: same health. Change area: same health (plus Stable Hand if owned).
- [ ] Horse dies: the X slot turns red, the bars read 0% with "Fallen…", and X refuses to summon. The Rest Area shop shows **Replacement Warhorse** in the featured row; buying it removes the offer and restores a full horse. **Must never:** heal the horse by dismount/resummon, or lose the horse when a Sword Mount ultimate horse or a scene horse dies.
- [ ] Draft cards: give `mount_blazing_hooves`, `mount_frost_wake`, `mount_bloodlust`, `mount_battering_ram`, `mount_iron_barding`, `mount_eager_steed` via the DevConsole and charge through a wave. Fast state: DevConsole currencies for shop gold.
- [ ] Loyal Steed: buy it, let the horse take a lethal hit. You get thrown off, the horse comes back at 30% on the next summon, and only once per run.
