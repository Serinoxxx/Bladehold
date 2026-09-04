# Specification & Implementation Prompt: Elemental Ability Slots & Synergy System

## Overview & Objective
Refactor the legacy single-element lock system into an **Ability-Slot Elemental System** inspired by *Hades*.
Instead of locking an entire run to a single element, players may mix and match elements across distinct combat ability slots. Each ability can hold exactly **one** elemental infusion at a time. Inflicting multiple elemental statuses onto the same target triggers emergent gameplay interactions, including a baseline status-stacking damage bonus and rare **Duo Synergies**.

## 1. Core Architecture: Elemental Ability Slots
The player character and fortress feature 5 designated elemental infusion slots:
| Slot Name | Affected Action | Input Action | Description |
| --- | --- | --- | --- |
| **Melee Slot** | Regular Attack | Left Click | Infuses primary melee swings with elemental status and bonus effects. |
| **Ranged Slot** | Ranged Attack | RMB aim / LMB fire | Infuses projectile attacks with elemental status and bonus effects. |
| **Mobility Slot** | Dash | Ctrl / Space | Leaves an elemental trail, aura, or burst when dashing. |
| **Ultimate Slot** | Ultimate | Q | Applies massive elemental burst/surges during ultimate uptime. |
| **Fortress Slot** | Fortress Emplacements | Passive / Automated | Imbues automated defenses (Arrow Slits, Boiling Oil, Barricades) with an element. |

### Slot Overwrite Rules
* Each slot can only hold **one** elemental upgrade at a time.
* If a draft card targets an already occupied slot (e.g., player has *Frost Step* on Dash and is offered *Blazing Trail*), the card displays an **[Overwrite]** tag.
* Accepting an overwrite card replaces the old elemental perk and awards a conversion bonus (e.g., +25 Gold compensation for the discarded card).

## 2. Standardized Status Effects
Each element applies a distinct, stackable debuff:
* **Fire (`STATUS_IGNITED`):** Target burns for Damage over Time (DPS) over 4 seconds.
* **Ice (`STATUS_CHILLED` & `STATUS_FROZEN`):** 
  * **Chilled:** Slows movement and attack speed by 20%–50% for 3 seconds.
  * **Frozen:** Applying a second Chill stack while already Chilled freezes the target solid for 2 seconds.
* **Lightning (`STATUS_CONDUCTIVE`):** Applies a static charge for 5 seconds. Struck targets discharge chain lightning arcs to nearby enemies and take bonus burst damage.

## 3. Synergy Systems

### A. Universal Synergy: "Elemental Discord"
* **Condition:** Any enemy simultaneously afflicted with **2 or more distinct elemental statuses** (e.g., `IGNITED` + `CHILLED`, or `IGNITED` + `CONDUCTIVE`) enters the **Discord** state.
* **Effect:** Target takes an automatic **+40% damage from all sources** and suffers a brief 0.3s micro-stagger.
* **UI Feedback:** Displays an intertwined elemental particle ring above the enemy's health bar.

### B. Duo Synergy Upgrades (Rare Draft Pool)
When a player has active investments in two different elements across their equipped slots, unique **Duo Synergy Cards** unlock and enter the Elemental Draft pool:
| Synergy Name | Element Prerequisites | Mechanic & Gameplay Effect |
| --- | --- | --- |
| **Thermal Shock** | Fire Slot + Ice Slot | Striking a Chilled or Frozen target with Fire deals **+150% instant burst damage** and clears the freeze, releasing a scalding steam cloud that blinds and slows nearby targets for 3s. |
| **Plasma Overload** | Fire Slot + Lightning Slot | Lightning strikes on an Ignited enemy consume all remaining burn damage ticks instantly, causing an immediate **Plasma Detonation** in a 4m AoE. |
| **Superconductor** | Ice Slot + Lightning Slot | Lightning damage against Frozen targets deals an automatic **100% Critical Strike** and causes the ice to shatter outward into 5 piercing shards dealing 40 damage each. |

### C. Fortress Multi-Element Interactions
Fortress defenses react dynamically based on player elemental attacks:
* **Electrified Oil (Boiling Oil + Lightning):** Firing lightning into Boiling Oil electrifies the entire puddle, causing all enemies wading through it to take 15 shock damage per second.
* **Permafrost Spikes (Spiked Barricades + Ice):** Enemies knocked back by Barricades into a Chilled zone take double knockback distance and are automatically Frozen upon colliding with fortress spikes.

## 4. Draft Card Data Model & Schema
Refactor the Elemental Card schema to support explicit slot targeting and Duo requirements:
```json
{
  "card_id": "ELEM_FIRE_MELEE_01",
  "name": "Combustion Blade",
  "element": "FIRE",
  "target_slot": "SLOT_MELEE",
  "tier": 1,
  "description": "Melee attacks ignite enemies, dealing 6 DPS for 4s.",
  "is_duo": false,
  "prerequisite_elements": []
}
```
```json
{
  "card_id": "ELEM_DUO_THERMAL_SHOCK",
  "name": "Thermal Shock",
  "element": "DUO_FIRE_ICE",
  "target_slot": "PASSIVE_SYNERGY",
  "tier": 3,
  "description": "Striking Frozen enemies with Fire consumes the freeze for 150% bonus burst damage and a blinding steam explosion.",
  "is_duo": true,
  "prerequisite_elements": ["FIRE", "ICE"]
}
```

## 5. Event Hooks & Game Logic Flow

### 1. `CanDraftCardAppear(ElementalCard card, PlayerState player)`
* Check `card.target_slot`. If slot is occupied by a different element, flag card as `isOverwrite = true`.
* If `card.is_duo == true`:
  * Verify `player.activeElements` contains all IDs in `card.prerequisite_elements`.
  * Return `true` if met; otherwise, exclude from draft roll.

### 2. `OnDamageDealt(DamageContext context, EnemyBase target)`
* Apply slot-specific elemental status to `target.statusContainer`.
* Check `target.statusContainer.GetUniqueElementCount()`.
* If count $\ge 2$:
  * Apply `STATUS_DISCORD` (+40% incoming damage multiplier).
* Check active Duo perks:
  * If `HasDuo("Thermal Shock")` and `context.damageType == FIRE` and `target.HasStatus("FROZEN")`:
    * Trigger `ExecuteThermalShock(target)`.
  * If `HasDuo("Plasma Overload")` and `context.damageType == LIGHTNING` and `target.HasStatus("IGNITED")`:
    * Trigger `ExecutePlasmaOverload(target)`.
