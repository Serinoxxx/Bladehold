# 17: Walls, the upgrade wheel and elemental crystals

**Goal:** in the gate-defense scenes, building is a choice instead of a supply sink.
- **Wall plots on every bridge.** A wall is a gated barrier the player can open and close. It upgrades wood → stone → metal and shows its damage visibly.
- **Pick-your-upgrade wheel.** Pressing E on a tower (or a wall's crafting station) opens it. It replaces the old auto level-up.
- **Elemental crystals** (fire, ice, lightning) unlock one element per structure.
- **Siege enemies** smash walls that are in their way. Normal enemies route around walls while they can.
- **The old Oil Vat and Spike Trap towers are removed.** Their jobs move onto wall upgrades.
- **An ammo box** goes beside the main gate.

**Scope:** Outer Gate, Desert Gate, Graveyard, Tutorial Gate (the "defense scenes"). The older sector scenes that also have tower plots (Ancient Garden, Frozen Pass, Survivors Scene) keep the old E-to-refill/level-up towers. The switch is a scene flag (see Phase C). The only change there is that oil and spikes disappear from their build wheel.

## Decisions (Lance, 2026-10-02)

| Question | Decision |
|---|---|
| Scenes | All four defense scenes. Tutorial Gate is generate-once, so its walls are placed by hand/MCP. The other three are regenerated from spec. |
| Wall plot locations | **Bridges only:** one plot per bridge, at the castle-side bridgehead. |
| Crystal sources | Elite/special enemy drops, a wave-clear reward, objective rewards, and **elemental fish** in the Fishing Pond. They persist across sectors (`RunSession`). |
| Crystal biome bias | Desert → Fire, Alpine (Outer Gate) → Ice, Graveyard / enchanted forest → Lightning. Tutorial and other grassy scenes get an even mix. |
| Refill | **A Refill slice on the upgrade wheel.** The old "E refills, then auto-levels" goes away in defense scenes. |
| Player vs wall | The wall has a **door**. E on the door opens or closes it. While it's open, enemies and the player both walk through. |
| Wall upgrades | Done at a **crafting station** beside the wall (castle side), not on the wall itself. |
| Wall destroyed | It becomes **rubble** and its high NavMesh cost is removed. It can be rebuilt **during prep only**, at wood tier, and its upgrades are lost. |
| Upgrade caps | Fire rate ×3 tiers, spikes ×1, wall material wood → stone → metal, **1 element per structure, locked once chosen**. Costs rise per tier. |
| Deconstruct | A wheel slice on towers and walls. **Refunds 100%** of the supply and crystals spent, plus the tower's remaining ammo. |
| Element effects | **Fire:** walls pour boiling oil (a burn pool); towers fire burning shots. **Ice:** walls spill icy water (slow, building to a freeze); towers slow their targets. **Lightning:** walls zap attackers with chain arcs; tower hits chain. All three use `EnemyStatusManager` Fire/Ice/Lightning. |
| Siege enemies | Battering ram, Troll, Sapper, Bosses/Captains. They ignore wall cost and attack walls in their path. Their escorts come with them. |
| Old towers | **Removed entirely:** the Oil Vat and Spike Trap classes, prefabs and wheel entries. |

### Follow-up answers (Lance, 2026-10-02)

| Question | Decision |
|---|---|
| Tower HP | Towers stay un-attackable for now, so **Repair +10 is wall-only** and towers get Refill. |
| Spikes | **Wall spikes:** only **melee** hits on the wall cost the attacker (1 damage per hit; projectiles and status ticks don't). **Tower spikes:** a spiked ring at the base that covers the blind spot. |
| Tower elemental draft cards | **Removed.** All 9 `SLOT_FORTRESS` Elemental cards are gone: Fire/Frost/Lightning Arrows, Glacial/Tempest/Pyroclast Catapult, Fortress Pyre, Tesla Spire, Permafrost. Their tower effects live on as the per-tower elements. A catapult's Ice/Storm/Fire is the slippery ground / storm cloud / rolling fireball. |
| Numbers | An element costs 3 crystals. Wood/stone/metal walls are 150/300/500 HP. All tunable on `FortUpgradeConfig`. |
| Main gate | Unchanged. |
| Damage visuals | Damaged segment swaps plus **looping smoke/fire** that reads from a distance. **Light** at 75% HP (smoke wisps), **medium** at 50% (thick smoke, embers), **heavy** at 25% (fire). Thresholds are on `WallConfig`. |
| Tutorial Gate | It has no bridges. **Walls go between big rocks** to make chokepoints that teach walls and gates (hand/MCP-placed; the scene is never regenerated). |

### Defaults I've assumed (shout if wrong)

- **Which ravine:** walls go on every bridge of every ravine (Desert Gate has two lines), capped at 8 plots (one NavMesh area each).
- **Placement:** each wall sits across the bridge deck 1.5 m in from the gate-side rim, so the pit closes its sides. The crafting bench sits on the gate-side rim beside the bridge.
- **Routing penalty:** wall area cost 12, roughly 33 m of detour for a 3 m-deep plot. Enemies closer than that to a shut wall attack it rather than walk round. It's `wallAreaCost` on the config.
- **Crystal drops:** fodder (goblin and the 4 skeleton crowd types) never drops crystals. Other roster enemies drop 1 at a 20% chance, siege units always drop 2, and captains drop 3. Each wave survived gives +1, and +1 more if its objective succeeded. Each elemental buff fish (Fire/Frost/Spark) gives +1.
- **Crystal names:** Fire Crystal, Ice Crystal, Storm Crystal.

## The model

```
 Bridge ─┬─ WallPlot (NavMeshModifierVolume, its own area "WallPlot0..7")
         │    └─ WallStructure  (Health · tier · StructureUpgrades · damage-stage visuals)
         │         ├─ WallDoor           E toggles open/closed → area cost low/high
         │         ├─ WallSpikes         thorns on OnDamaged (if bought)
         │         └─ WallElementEffect  oil / icy water / arcs when attacked (if bought)
         └─ WallCraftingStation (castle side) ── E ──► UpgradeWheelUI(wall)

 TowerPlot ── E (in defense scenes) ──► built?  no → BuildWheelUI (as today, minus oil/spikes)
                                         yes → UpgradeWheelUI(tower)
 DefenseStructure + StructureUpgrades (fire-rate tier · spikes · element)
```

`StructureUpgrades` is a plain component shared by towers and walls. It holds what's been bought, what was spent (for the 100% refund) and the element lock. `UpgradeWheelUI` asks the structure for its `UpgradeOption` list (label, icon, cost per currency, enabled/reason), so the wheel itself is generic.

### NavMesh routing

- **Areas.** Add `WallPlot0`..`WallPlot7` to `NavMeshAreas.asset`. The generator puts a `NavMeshModifierVolume` over each bridge-head footprint, set to that plot's area, and bakes as usual. That's one area per plot, so plots can be costed independently without a rebake.
- **Cost.** A `WallNavCost` static sets `NavMesh.SetAreaCost(area, cost)` when a wall is closed: high (~40) for a standing closed wall, and 1 when the plot is empty, rubble or the door is open. It then asks nearby agents to re-path. Siege agents call `agent.SetAreaCost(area, 1)` for every wall area on spawn, so they path straight through.
- **No carving.** The wall collider blocks physically, but the NavMesh stays connected. If every bridge is walled, the cheapest path still runs through a wall, so enemies walk up to it.
- **Blocked → attack.** If an agent's next corner lies inside a closed wall's area and it's within reach, `AITargetSelector` targets the wall (like `SetTowerTarget`) and attacks it with its normal melee. It drops back to gate/player targeting when the wall breaks or the door opens.

### Siege and escorts

- **Ram:** `BatteringRam` treats a wall on its path like the gate. It stops and rams it every 5 s, then continues to the gate.
- **Troll / Captain:** cost 1, so they walk into the wall and slam or attack it as a target.
- **Sapper:** walls count as sapping targets. A wall has no supply, so sapping deals HP damage over the channel instead.
- **Escorts:** while a siege leader is engaging a wall, enemies within ~10 m of it target that wall too. The ram's escorts already follow `GetEscortTargetPosition`, which keeps working because the ram stays in front of the wall. That gives "escorts clear the path with them" without the escorts' own wall cost mattering.

## Phases

### A. Remove the Oil Vat and Spike Trap towers
- **Enum.** Give `FortDefenseType` **explicit int values** *before* removing anything (`ArrowSlits = 0, Catapult = 3, Ballista = 4, NetThrower = 5`). Serialized wheel entries, plot data and `RunSession` state store ints, so implicit renumbering would silently turn catapults into ballistas. Then mark `BurningOil = 1` / `Spikes = 2` `[Obsolete]` and delete them once no data references them.
- **Delete** `OilVatDefense`, `SpikeTrapDefense`, `Defense_OilVat.prefab` and `Defense_SpikeTrap.prefab`.
- **Remove references:**
  - the `TowerPlot` slots and `SetPrefabs` args;
  - the `BuildWheelUI` entries on the HUD prefab;
  - `DefensesRevampSetup`;
  - the unused `FortBurningOil*` stats.
- **Keep** `BurningOilZone`. It becomes the wall fire effect.

### B. Elemental crystals
- **Run state:** `RunSession.Crystals[Fire|Ice|Lightning]` with `TrySpendCrystals` / `AddCrystals` / `OnCrystalsChanged`. Persisted across sectors like supply, and reset on a new run.
- **Biome bias:** a `SceneCrystalBias` component (weights per element, like `SceneAbilityRules`).
  - Its value comes from a new field on `DefenseBiomePaletteSO`, so generated scenes get it automatically.
  - With no component, the drop is an even mix.
  - Any "random crystal" roll uses it.
- **Sources:**
  - `CrystalPickup` (tinted per element) dropped by elites, siege enemies and captains, through the existing drop SO path;
  - a wave-clear reward from `GameLoopManager`;
  - an objective reward hook in `SurvivorsObjectiveManager`;
  - elemental fish in the Fishing Pond's catch table.
- **HUD:** three small crystal counters next to supply.
- **Tunables:** drop chance, wave reward and per-source amounts in a `CrystalConfigSO`.

### C. Upgrade wheel (towers)
- `DefenseSceneRules` scene component: `useUpgradeWheel` and `allowWalls`. The generator adds it, and you add it by hand in Tutorial Gate. Without it, towers behave as today.
- `StructureUpgrades` + `UpgradeOption`.
- `UpgradeWheelUI` is built from `BuildWheelUI`'s radial layout and button prefab, with gamepad focus and paused-time behaviour as in `/modify-ui`.
- **Tower slices:** Refill (supply, as today's resupply), Fire Rate I/II/III, Spikes, Fire / Ice / Lightning (3 crystals each; the other two grey out once one is chosen), and Deconstruct.
- **`DefenseStructure` changes:**
  - In wheel scenes, `Interact` opens the wheel instead of resupply/auto-upgrade.
  - `ApplyLevelStats` reads the fire-rate tier.
  - Each subclass applies its element: arrow, ballista and catapult shots carry `elementId`, and net-thrower nets apply the status on root.
- **Deconstruct (per structure):** refund via `StructureUpgrades.TotalSpent`, then `TowerPlot.ClearDefense`. Sector-end `DismantleAllForRefund` includes crystals.
- **Tunables:** `StructureUpgradeConfigSO` (costs per tier, fire-rate multipliers, spike ring damage, element strengths).

### D. Walls
- **`WallPlot`:**
  - Interact (prep only) builds a wood wall for supply, or rebuilds from rubble.
  - It owns the area index, the door, the crafting station and the rubble swap.
  - It registers with `TowerPlotManager` so it's included in sector-end refunds.
- **`WallStructure`:**
  - It has `Health`, and listens to `OnDamaged` / `OnDied`, per the hub convention.
  - Tier visuals come from `WallConfigSO` (prefab per tier per damage stage).
  - Material upgrades restore HP proportionally.
- **`WallDoor`:**
  - Interact toggles it with a swing animation.
  - The door collider is on its own child, and toggling it drives `WallNavCost`.
  - The door auto-refuses to open while enemies are inside the doorway, so you can't shut someone inside the wall.
- **`WallCraftingStation`:** a workbench prop on the castle side. Interact opens `UpgradeWheelUI(wall)`.
- **Wall slices:** Material (Stone / Metal), Repair +10 (supply), Spikes, Boiling Oil / Icy Water / Charged (3 crystals), and Deconstruct.
- `WallNavCost` + the `AITargetSelector` blocked-by-wall targeting described above.

### E. Wall HP UI
- A world-space segmented bar per wall (MMProgressBar with a delayed bar, `/mm-progress-bars`) that fades in on damage or when the player is near.
- HUD wall icons under `FortressGateHealthBarUI`, with an under-attack flash and a one-time tip the first time a wall takes damage.
- MMF feedbacks for stage drops and collapse (shake, debris, sound), per `/feel-integration`.

### F. Siege behaviour
- **Ram:** stop and ram any wall on its path.
- **Troll, Sapper, Captain:** per-agent area cost 1 and wall targeting. The Sapper sabotages walls (HP damage).
- **Escorts:** they join the leader's wall attack.
- **Roster flag:** an `isSiege` column in `Enemies.csv` (applied by `EnemyDefinitionApplier`), so new siege types are data.

### G. Wall effects
- **`WallSpikes`:** on each hit, deal 1 damage back to `Damage.source`.
- **`WallElementEffect`:**
  - Triggers on being attacked, with an internal cooldown, against attackers within ~4 m on the outside.
  - **Fire:** spawns a `BurningOilZone`.
  - **Ice:** spawns an icy-water pool (built from `SlipperyIceZone`) that applies Ice.
  - **Lightning:** chains arcs through 3 attackers and applies Lightning.

### H. Scenes and generator
- **Spec:** `BridgeSpec.wallPlot` (default on), plus a `mainGateAmmoChest` placement (castle side, beside the gate, `AmmoChest` at its stock gold cost).
- **Generator:** places `WallPlot` + `WallCraftingStation` per bridge, `NavMeshModifierVolume`s, `DefenseSceneRules`, `SceneCrystalBias` and the ammo chest. The validator checks each wall plot sits on a bridge, the wall area is baked for every agent type, and the chest is reachable.
- **Regenerate** Outer Gate, Desert Gate and Graveyard (`/generate-defense-scene`, never hand-edited).
- **Tutorial Gate** is hand/MCP-placed: wall plots on its bridges (if it has any), the rules component and a crystal bias (even). It already has an ammo chest; check it's by the gate. The tutorial steps don't teach walls (learned in play).

### I. Verification
- **`WeaponReachBenchmark` cases:**
  - an enemy routes around a closed wall and then attacks it once all bridges are walled;
  - the ram breaks a wall and continues;
  - door open → path cost restored;
  - deconstruct refunds 100% of supply and crystals;
  - element lock;
  - wall spikes deal 1 damage per hit.
- **Housekeeping:** `/compile-check`, a changelog entry, and `plans/editor/17-walls-and-upgrade-wheel.md` for human items (art picks for wood/metal damage stages, wheel and HP UI sign-off, MMF timing).

## Open risks

- **Global area cost:** `NavMesh.SetAreaCost` is global and agents re-path lazily. Paths need forcing to refresh near walls when a wall changes state, or enemies already committed to a bridge will walk into a just-closed door (which is acceptable: they attack it).
- **Area budget:** 8 wall areas is plenty (max 3 bridges today), and the Unity cap is 32.
- **Tutorial Gate layout:** if it has no bridges, it gets no wall plots unless we pick spots by hand.
