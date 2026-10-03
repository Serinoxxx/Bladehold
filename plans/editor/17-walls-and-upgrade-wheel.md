# Editor to-do: plan 17 (Walls, upgrade wheel, elemental crystals)

From [`plans/17-walls-and-upgrade-wheel.md`](../17-walls-and-upgrade-wheel.md), 2026-10-02. Unity MCP was connected. Everything below is built, wired and play-checked in Outer Gate; what's left is art, feel and your review. Tick items off as you go and delete the file when it's empty.

**The wall is one hand-placed model** (since 2026-10-03; see "Single-model wall" below). `Bladehold Prefabs/Defenses/Walls/Wall.prefab` → `Model/ShortWallWithGate` is nested in `WallPlot.prefab`. `Build(rebuild: true)` on `FortWallAssetsBuilder` overwrites it.

## Single-model wall (2026-10-03, Unity MCP connected)

**What's done:**
- The three tier models are gone. Upgrades swap every Castle_Wall material slot on the model: `WallConfig.asset → tiers[0..2].material` = Castle_Wall_01/02/03, names Wood/Stone/Metal.
- Damage shows as smoke/fire only.
- At 0 HP the model sinks into the ground and `Rubble` appears.
- The player climbs the model's own stairs onto the walkway; the ladder and parapet are gone.
- Building rises out of the ground like the arrow tower, and wall workbenches get prep "BUILD" waypoints.
- Prefab restructured by the agent: your ShortWallWithGate moved under `Model`, shifted (-0.28, 0, +1.58) so the portcullis sits on the doorway. `WallConfig.doorWidth` = 4.5 to match it.
- Play-checked in Tutorial Gate: rise, Wood → Stone material swap, collapse sink + rubble, workbench waypoints, no console errors.
- The Tutorial Gate's lost `Bladehold HUD` was restored from the last commit and its 11 scene references re-pointed.
- Code: `Fort/Walls/WallStructure.cs`, `WallPlot.cs`, `WallDoor.cs`, `WallConfigSO.cs`, `Fort/DefenseAssemblyAnimation.cs` (`PlayRise`), `UI/ObjectiveWaypointTrackerUI.cs` (`AddWallPlotTargets`).

- [ ] **Fit each scene's wall to its plot.** The gatehouse is about 17 m wide; plots are 5.6–10.6 m.
  - **Where:** `WallPlot_N/Wall/Model` in every scene with wall plots. Edits there are per-plot overrides.
  - **Keep the portcullis inside the cyan gizmo box** (the doorway that actually blocks). The orange box is the plot `width`: the invisible side blockers that stop enemies. The model's own walls only stop the player.
  - If you move the gate off the Wall origin, move the whole `Model`, not the gate alone; the door's blocker is fixed at the origin.
- [ ] **Walkable layers.** `Floor_*` and `Stairs_*` pieces are on **Environment**, everything else on **Fortification**. Any walkable piece you add needs the Environment layer, or the player can't stand on it properly.
  - **Verify:** in Play, walk up the stairs onto the walkway, shoot over the battlements, and confirm you can't step off the outside.
- [ ] **Rubble** (`Wall/Rubble`, three stone piles, hidden): place it to taste, or swap in other art.
- [ ] **Tier names:** all three tiers are castle stone now, so "Wood / Stone / Metal" reads oddly. Rename in `WallConfig.asset → tiers[].displayName` (e.g. Stone / Reinforced / Fortified).
- [ ] **Rise and collapse look:** build during prep (it rises about 1.35 s with dust and a slam), then kill it (DevConsole). It should sink over `collapseSinkSeconds` (1.4) and leave rubble. On bridge plots the buried part may show under the deck while it moves.
- [ ] **Prep waypoints:** each wall workbench shows a gold "BUILD" marker until a wall stands on it, then a faded one.

## Art (your call)

- [ ] **Damage smoke/fire** (`lightDamageVfx` / `mediumDamageVfx` / `heavyDamageVfx`, at 75/50/25% HP).
  - The heavy stage's black smoke (`FX_Smoke_Dark_01`) is very large. Good for spotting from afar, but probably too much. Scale down `Wall_Damage_*_VFX` in `Art/`.
- [ ] **Upgrade props:** `Wall/Spikes` (goblin spikes along the outside face) and `Wall/Fixture_BoilingOil`, `Fixture_IcyWater`, `Fixture_LightningRod` (cauldron, barrels, log + crystal). They're still laid out for the old 10 m wall at y = 5 over the origin, so reposition them onto the gatehouse.
- [ ] **Crafting bench** (`WallPlot.prefab` → `CraftingStation`, a Synty workbench) and the **empty-plot stakes** (`EmptyMarker/Stake_L`, `Stake_R`, moved to the plot's ends at runtime).
- [ ] **Crystal pickup** (`Walls/CrystalPickup.prefab`): an enlarged `SM_Item_Crystal_03`, tinted per element at runtime through `tintRenderers`, with a point light.

## Feel (MMF content)

All of these are clones of the Arrow Tower's Repair/Upgrade/Break players (or the ammo pickup's), so they sound like tower building for now.
- [ ] **`Wall.prefab` → `Feedbacks/`:** `HitMMF` (each hit), `StageDropMMF` (crack + debris when a damage stage drops), `CollapseMMF` (crash, dust, shake), `UpgradeMMF`, `LightningArcMMF` (per struck attacker), `DoorOpenMMF` / `DoorCloseMMF` / `DoorBlockedMMF`.
- [ ] **`WallPlot.prefab` → `BuildMMF`**, **`TowerSpikeRing.prefab` → `StabMMF`**, **`WallIcyWaterZone.prefab` → `SplashMMF`** (the icy pool's visual is copied from `SlipperyIceZone`; give it a water look), **`CrystalPickup.prefab` → `PickupMMF`**.

## UI review (Synty art, Texturina headers / Grenze body, gamepad focus)

- [ ] **Upgrade wheel.** E on a tower in a defense scene opens it.
  - Up to 7 slices: Refill, Fire Rate I–III, Spikes, Fire / Ice / Storm, Deconstruct. Extra slices are cloned from `Slice_0`, and all slices are re-laid round the authored ring (radius 380).
  - Check the 7-slice spacing, the one-line slice cost ("10 Supply + 1 Storm"), the greyed "Max / Locked / Built / Full" states, and the header's "Supply · Fire · Ice · Storm" line.
  - **Gamepad:** check focus moves round 7 slices.
- [ ] **Wheel details panel + readable costs (2026-10-03).** Both wheels now show hover details in a box to the right of the wheel (`Bladehold HUD` → `BuildWheelModal/CenterContainer/DetailsPanel`, `BuildWheelDetailsPanel`). It has the icon, name, each cost against what you hold ("60 Supply (you have 20)"), the description, and range/blind spot or the blocked reason. The old bottom `DescriptionText` is switched off.
  - Unaffordable slices are now darkened (icon, tracery and background ×0.7), not faded to 50% alpha. Slice label plates are darker and the cost text is bigger (auto-size 20–32).
  - Check the panel clears the wheel at 16:10 and 4:3 (it sits 640 units right of centre on the 3840×2160 reference), and that long upgrade descriptions don't push it off-screen.
  - **Gamepad:** the panel follows pad focus (`ISelectHandler`). But unaffordable slices are non-interactable, so the pad can't focus them to read why. Decide whether that matters.
  - Scene-local wheel copies (the Castle scenes are binary, so I couldn't check) without a `detailsPanel` fall back to the old bottom label.
- [ ] **Build wheel** now has 4 options (oil and spikes removed), re-spaced round the ring automatically.
- [ ] **Wall HP bar** (`Wall.prefab` → `HealthBar`, world-space, 3 m wide over the wall). It shows when you're within 14 m or for 4 s after a hit, and the frame tints to the wall's element.
- [ ] **HUD wall row** (`Bladehold HUD` → `Bottom Right/Wall Status`, icons from `UI/WallStatusIcon.prefab`), above the gate bar.
  - One icon per plot, numbered left to right. HP fill goes green/yellow/red, the frame flashes when hit, an X means breached, "OPEN" means the door is open, and unbuilt plots are dimmed.
  - The icons are small at the HUD's 0.27 canvas scale.
- [ ] **HUD crystal counters** (`Top Left/Currencies/CrystalsUI`, cloned from SupplyUI, crystal icons tinted orange/blue/yellow). Hidden until you own a crystal.
- [x] **Wheel icons** on `Config/Fort/FortUpgradeConfig.asset` (Wheel icons section): bespoke generated icons wired 2026-10-03 (`Art/Icons/Skills/Base/`: refill, fire_rate, tower_spikes, repair, wall_material, deconstruct, crystal_*), also used by the build wheel and the HUD crystal counters.

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
