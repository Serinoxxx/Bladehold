# Editor to-do: plan 17 (Walls, upgrade wheel, elemental crystals)

From [`plans/17-walls-and-upgrade-wheel.md`](../17-walls-and-upgrade-wheel.md), 2026-10-02. Unity MCP was connected. Everything below is built, wired and play-checked in Outer Gate; what's left is art, feel and your review. Tick items off as you go and delete the file when it's empty.

**All the wall art is placeholder.** Re-skin it freely. `Bladehold > Fort > Build Wall & Upgrade Assets` (`FortWallAssetsBuilder`) only creates what's missing, so your swaps survive a re-run. `Build(rebuild: true)` overwrites them.

## Art (your call)

- [ ] **Wall segments per tier and damage stage.** All in `Assets/Bladehold/Config/Fort/WallConfig.asset` → `tiers[0..2]` (wood, stone, metal).
  - **Slots per tier:** `segment`, `segmentLight/Medium/HeavyDamage`, `door`, `rubble`.
  - **Pivot rules:** segments are base-centred with their length along local X, and `segmentLength` is the tiling length (5 m now). A door's pivot is at the hinge, with the leaf along +X to `doorWidth` (3 m).
  - **The placeholders** are in `Bladehold Prefabs/Defenses/Walls/Art/`:
    - Wood: a log palisade.
    - Stone: Synty castle walls, using the destroyed-wall halves for medium/heavy damage.
    - Metal: stone plus an iron spike crown, with a metal-fence gate.
- [ ] **Damage smoke/fire** (`lightDamageVfx` / `mediumDamageVfx` / `heavyDamageVfx`, at 75/50/25% HP).
  - The heavy stage's black smoke (`FX_Smoke_Dark_01`) is very large. Good for spotting from afar, but probably too much. Scale down `Wall_Damage_*_VFX` in `Art/`.
- [ ] **Upgrade props:** `spikesProp` (goblin spikes along the outside face) and `fire/ice/lightningFixture` (cauldron, barrels, log + crystal on top of the wall over the door).
- [ ] **Crafting bench** (`WallPlot.prefab` → `CraftingStation`, a Synty workbench) and the **empty-plot stakes** (`EmptyMarker/Stake_L`, `Stake_R`, moved to the plot's ends at runtime).
- [ ] **Crystal pickup** (`Walls/CrystalPickup.prefab`): an enlarged `SM_Item_Crystal_03`, tinted per element at runtime through `tintRenderers`, with a point light.

## Feel (MMF content)

All of these are clones of the Arrow Tower's Repair/Upgrade/Break players (or the ammo pickup's), so they sound like tower building for now.
- [ ] **`Wall.prefab` → `Feedbacks/`:** `HitMMF` (each hit), `StageDropMMF` (crack + debris when a damage stage drops), `CollapseMMF` (crash, dust, shake), `UpgradeMMF`, `LightningArcMMF` (per struck attacker), `DoorOpenMMF` / `DoorCloseMMF` / `DoorBlockedMMF`.
- [ ] **`WallPlot.prefab` → `BuildMMF`**, **`TowerSpikeRing.prefab` → `StabMMF`**, **`WallIcyWaterZone.prefab` → `SplashMMF`** (the icy pool's visual is copied from `SlipperyIceZone`; give it a water look), **`CrystalPickup.prefab` → `PickupMMF`**.

## UI review (Synty art, Texturina headers / Grenze body, gamepad focus)

- [ ] **Upgrade wheel.** E on a tower in a defense scene opens it.
  - Up to 7 slices: Refill, Fire Rate I–III, Spikes, Fire / Ice / Storm, Deconstruct. Extra slices are cloned from `Slice_0`, and all slices are re-laid round the authored ring (radius 380).
  - Check the 7-slice spacing, the two-line cost text (supply + crystals), the greyed "Max / Locked / Built / Full" states, and the header's "Supply · Fire · Ice · Storm" line.
  - **Gamepad:** check focus moves round 7 slices.
- [ ] **Build wheel** now has 4 options (oil and spikes removed), re-spaced round the ring automatically.
- [ ] **Wall HP bar** (`Wall.prefab` → `HealthBar`, world-space, 3 m wide over the wall). It shows when you're within 14 m or for 4 s after a hit, and the frame tints to the wall's element.
- [ ] **HUD wall row** (`Bladehold HUD` → `Bottom Right/Wall Status`, icons from `UI/WallStatusIcon.prefab`), above the gate bar.
  - One icon per plot, numbered left to right. HP fill goes green/yellow/red, the frame flashes when hit, an X means breached, "OPEN" means the door is open, and unbuilt plots are dimmed.
  - The icons are small at the HUD's 0.27 canvas scale.
- [ ] **HUD crystal counters** (`Top Left/Currencies/CrystalsUI`, cloned from SupplyUI, crystal icons tinted orange/blue/yellow). Hidden until you own a crystal.
- [ ] **Wheel icons** on `Config/Fort/FortUpgradeConfig.asset` (Wheel icons section) are borrowed Synty/skill icons. Swap them if you want bespoke ones (`/generate-sprite-variants`).

## Tutorial Gate

- [ ] **Look at the chokepoint line** (`Wall Chokepoints` root, `Rocks` + `Wall Plots`). It's 36 Synty cliff rocks across the field at z ≈ 50, with 3 gaps at x = −22, 2, 26, each holding a wall plot.
  - The NavMesh was rebaked, and every spawn still reaches the gate through the gaps (Humanoid and Large Enemy).
  - If you move rocks, rebake (`NavMesh` object, both surfaces). No rock gap may be wider than a wall (7.6 m), or enemies walk round it.
- [ ] **Tutorial wall step (added 2026-10-02).** The new step `Step11b_Wall` (`BuildWallStep`) comes after Mount and before the second Ready.
  - **Waypoint:** the centre gap's workbench (`WallPlot_2`).
  - **Line 1:** "Build a wall at the workbench by the marked gap".
  - **Line 2:** "Goblins go round a walled gap. Wall every gap and they have to break through" (`tutorial.wall` / `tutorial.wall_reroute` in `Strings.csv`, English only).
  - **Path check:** with that wall up, wave 2's three centre spawns path through the left gap instead.
  - **Playtest:** the step completes on building there (or on starting the wave). Wave 2 visibly swerves round the wall, and supply after wave 1 covers the 40-supply wall.

## Playtest

- [ ] **Routing.** Build only the centre wall in Outer Gate.
  - Side spawns should take the side bridges. Centre spawns still come straight and attack it (detour longer than the penalty).
  - Tune with `FortUpgradeConfig.wallAreaCost` (12).
- [ ] **All walled:** enemies stop at a wall face and hit it. They must **never** walk through a shut door (including when you stand right behind it).
- [ ] **Door:** E on the door opens it (enemies stream through, wall icon shows OPEN) and shuts it again. It refuses to shut with someone in the doorway (blocked rattle).
- [ ] **Siege:**
  - A troll (DevConsole spawn picker) walks straight to the nearest wall and slams it, and nearby goblins join in.
  - A battering ram stops at a wall on its path, rams it down, then rolls on to the gate.
  - A sapper that reaches a wall hacks at it.
  - A captain should also go straight for walls; not play-checked yet.
- [ ] **Spikes:** melee attackers lose 1 HP per hit on a spiked wall. Arrows and bombs don't trigger it.
- [ ] **Elements:** fire wall pours a burning pool when hit, ice spills a slowing pool that freezes stayers, lightning arcs into 3 attackers. Each has a 4 s cooldown.
- [ ] **Collapse and rebuild:**
  - At 0 HP: rubble, smoke dies down, the wall icon shows X, and enemies path through.
  - The bench offers "Rebuild Wall" in prep only, at wood tier with upgrades lost.
- [ ] **Refunds:**
  - Deconstruct (tower or wall) returns everything paid, including crystals.
  - Winning the sector refunds standing walls too (victory screen supply).
  - Build-then-deconstruct must **never** gain supply.
- [ ] **Crystals:**
  - Elites drop a crystal 20% of the time (fodder never drops), siege units drop 2, captains 3.
  - +1 crystal per wave survived, +1 more if its objective succeeded.
  - Desert drops mostly Fire, Outer Gate mostly Ice, Graveyard mostly Storm, the tutorial an even mix.
  - Fire/Frost/Spark fish in the Fishing Pond each give 1.
- [ ] **Old scenes** (Ancient Garden, Frozen Pass, Survivors Scene): towers still refill/level up on E. Oil Vat and Spike Trap are gone from the build wheel.
