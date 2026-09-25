# Fishing Minigame Feature Specification

## Overview
The **Fishing Minigame** is a standalone "game within a game" accessible via 7 tactical nodes across the Castle Campaign map. Upon entering a Fishing Pond node, the player is temporarily equipped with a specialized Fishing Bow (unaffected by their normal weapon loadout or bow meta/in-run upgrades) and challenged to hunt swimming fish in a circular pond within a 60-second time limit.

---

## 1. Campaign Map Integration

### Campaign Nodes
- **Node Count**: 7 nodes designated as `CampaignNodeType.FishingPond`.
- **Node Type**: `CampaignNodeType.FishingPond` added to `CampaignNodeType.cs`.
- **Icon & Visual Styling**: Teal/cyan theme utilizing `ICON_FantasyWarrior_Map_Fishing_01_Clean.png`.
- **Scene**: `Bladehold Fishing Pond.unity`.
- **Graph Placement**: Distributed across Campaign Tiers as high-reward optional or alternative paths (e.g. alongside Combat/Rest areas).
- **Completion**: Completing the 60-second fishing minigame marks the node as completed in `CampaignManager`, awards all harvested resources, and returns the player to `Bladehold Campaign Map Scene.unity` with forward nodes unlocked.

---

## 2. Core Minigame Loop & Rules

- **Duration**: Exactly 60 seconds (1 minute countdown timer displayed prominently on the HUD).
- **Population Cap**: Maximum 30 concurrent fish swimming in the pond at any given time. As fish are killed, new fish spawn at the pond edges to maintain density.
- **Player Loadout**:
  - The player is automatically equipped with the **Fishing Bow**, regardless of their normal equipped melee or ranged weapon.
  - Normal bow upgrades, elemental charges, and skill tree modifiers from the main game are **inactive** during fishing.
  - Player controls aim and shot with the standard Synty/InputReader controls.

---

## 3. Fish Taxonomy & Behaviors

All fish use low-poly fish meshes (`SM_Item_Meat_Fish_01` through `07`) swimming in circular orbits around the center of the pond at varying radiuses, depths, and speeds.

### Resource Fish
| Fish Type | Color / Visuals | Base HP | Resource Yield | Spawn Rules |
| :--- | :--- | :--- | :--- | :--- |
| **Gold Fish** | Metallic Gold | 1 HP | +5 to +15 In-Run Gold | Common (60% weight) |
| **Orc Metal Fish** | Forest Green | 1 HP | +1 Orcish Metal | Uncommon (20% weight) |
| **Goblin Blood Fish**| Crimson Red | 1 HP | +1 to +2 Goblin Blood | Uncommon (15% weight) |
| **Diamond Fish** | Glowing Blue / Emissive | 20 HP (20x HP) | +1 **Diamond Fish Bones** (new permanent meta resource) | 2x-3x larger scale; spawns with a chance once timer > 30s |

### Buff Fish (Rare Spawns)
Buff fish spawn occasionally during the 60-second session. When killed, they unlock an end-of-session meal choice.
- **Rule: Maximum 3 Buff Fish consumed per campaign run.**
- **Rule: Maximum 1 Buff Fish chosen per fishing pond visit.**
- If multiple buff fish are killed during a single visit, the player selects only **one** to consume on the tally screen.

| Buff Fish | Visual Identity | Unique Trait | In-Run Permanent Buff Granted |
| :--- | :--- | :--- | :--- |
| **Speedy Fish** | Swift / Blue-White fin trails | Extra fast swim speed | **+10% Movement Speed** |
| **Armored Fish** | Shiny Steel / Metallic Armor | 5x HP (5 HP) | **+10 Max HP** |
| **Fire Fish** | Flaming particle FX | Moderate speed | **+10% Fire Damage** |
| **Frost Fish** | Frost / Ice mist FX | Moderate speed | **+10% Frost Damage** |
| **Spark Fish** | Lightning / Spark FX | Erratic darting | **+10% Lightning Damage** |
| **Savage Fish** | Purple / Arcane magic FX | Elusive pattern | **+5% ALL Damage** |

---

## 4. In-Minigame Leveling & Draft Cards

Fish yield Fishing XP on death. Leveling up during the 60-second minigame triggers an instant 3-card draft popup (pausing or slow-moing the action) to choose a minigame upgrade.

### Fishing Upgrade Cards
| Card Name | Tiers | Mechanics & Scaling |
| :--- | :--- | :--- |
| **Bounce Shot** | Tier 1–4 | Arrows bounce between nearby fish **1 / 2 / 3 / 4** times. |
| **Fishsploshion** | Tier 1–4 | Killing a fish causes an explosion dealing **1 / 2 / 3 / 4** damage in a 3m radius. |
| **Icey Water** | Tier 1–4 | All fish swimming speed reduced by **20% / 30% / 40% / 50%**. |
| **Fish Skewer** | Tier 1–4 | Arrows pierce through **1 / 2 / 3 / All** fish along flight path. |
| **Bleed** | Tier 1–4 | Damaging a fish causes **1 DPS for 5s**, stacking up to **2 / 3 / 4 / 5** times. |
| **Fat Fish** | Tier 1–4 | Fish swim size increased by **2.5% / 5% / 7.5% / 10%**, increasing resource yield proportionally. |

*Note: Fishing draft upgrades only apply within the active 60-second fishing minigame session and do not carry over to combat waves.*

---

## 5. End of Session & Tally

When the 60-second timer reaches zero:
1. **Bow & Spawners Freeze**: All shooting and fish movement halts.
2. **Results Tally Modal**:
   - Total Fish Caught count.
   - Resource breakdown: Gold, Orcish Metal, Goblin Blood, and Diamond Fish Bones.
3. **Buff Fish Feast Selection (if eligible)**:
   - If player killed at least 1 Buff Fish AND has consumed fewer than 3 Buff Fish this run:
     - Renders an interactive choice of the Buff Fish types killed during this pond visit.
     - Selecting one adds the buff to `RunSession.ActiveBuffFish` and immediately applies the stat bonus.
4. **Return Gate / Continue**:
   - Commits currencies to `RunSession` and `SaveData`.
   - Invokes `CampaignManager.Instance.CompleteCurrentNodeAndContinue()`, the one node exit: saves HP and returns to the map through the loading screen.
