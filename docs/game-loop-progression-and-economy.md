# **Game Design & Systems Overview (v1.2)**

## **1\. Core Controls & Actions**

| Action | Input |
| :---- | :---- |
| **Regular Attack** | Left Click |
| **Ranged Attack** | RMB aim / LMB fire |
| **Dash** | Ctrl / Space |
| **Ultimate** | Q |

 

## **2\. Economy & Currencies**

| Name | Obtained From | Persistence | Primary Usage |
| :---- | :---- | :---- | :---- |
| **Gold** | Enemies, wave completion, objectives | In-run only (lost on death) | Rest Area Shop purchases |
| **Goblin Blood** | Wave completion / Random drop | Permanent | Purchasing Permanent Upgrades |
| **Orcish Metal** | Special enemies / Random drop | Permanent | Unlocking weapons & upgrade tiers |

 

## **3\. Equipment & Loadout Rules**

Players equip exactly **1 Melee Weapon** and **1 Ranged Weapon** per run. Sword and Bow are unlocked by default; all other weapons require Orcish Metal to permanently unlock.

> * **Sword** (Default Melee): Fast, single-target attacks. *Hold:* Thrust / lunge.  
> * **Bow** (Default Ranged): Rapid single arrows. *Hold:* Increases projectile range and damage.  
> * **Axe** (Melee): Broad cleave attacks. *Hold:* Mini whirlwind spin.  
> * **Throwing Axe** (Ranged): Medium-range piercing throw with larger AoE; returns to player.  
> * **Staff** (*LOCKED FOR DEMO*): Short-range AoE burst. *Hold:* Increases damage and AoE radius.  
> * **Wand** (*LOCKED FOR DEMO*): Rapid ranged magic imbued with elemental traits.

 

## **4\. Rest Area Loop & Wave Flow**

Rest areas occur **every 3 waves** and feature three key stations:

> 1. **The Well:** Restores up to 20 HP (1 use per visit).  
> 2. **The Shop:** Offers randomized items for purchase with Gold. Effects last for the run unless specified otherwise.  
> 3. **Draft Station:** Awards a 3-card draft. The draft category automatically alternates randomly across visits between *Weapon*, *Fortress*, and *Elemental* upgrades.

### **Shop Inventory**

| Item | Cost | Effect | Duration |
| :---- | :---- | :---- | :---- |
| **Maggoty Bread** | 5 Gold | Heal \+5 HP | Instant (one-time use) |
| **Troll Heart** | 50 Gold | \+25 Max HP | Rest of run |
| **Crystal Water** | 25 Gold | \+20% Movement speed | 5 waves |
| **Special Herbs** | 40 Gold | \+5 HP at the end of each wave | 5 waves |

 

### **End-of-Wave Drops**

Completing any standard wave drops one reward selected from the following pool:

> * Troll Heart (+25 Max HP for the run)  
> * 1–2 Orcish Metal  
> * 2–3 Goblin Blood  
> * 50–100 Gold  
> * Instant Upgrade Draft

 

## **5\. Draft Rules & In-Run Upgrades**

Drafts present a 3-card selection governed by two core rules:

> * **Targeted Weapon Pool:** Upgrades only appear for the 2 equipped weapons (1 melee, 1 ranged).  
> * **Elemental Lock:** Selecting an elemental upgrade locks your run into that element; cards for other elements are excluded from future drafts.

### **Weapon Upgrades (Mid-Run Only)**

> * **Sword**  
  * **Lunge Mastery:** Lunge deals \+200% damage and gains \+40% critical strike chance.  
  * **Nimble Strike:** Dashing performs an automatic attack along the movement path.  
  * **ShieldBreaker:** Sword attacks deal \+200% damage to shielded targets.  
  * **Vampire Blade:** Heals 2 HP per hit, but player takes 50% extra elemental damage.  
> * **Bow**  
  * **Auto-Shot:** Dashing automatically fires a fully charged shot at the nearest enemy.  
  * **Desperate Volley:** Fires 8 / 12 / 16 / 20 arrows radially when dropping below 50% HP.  
  * **Piercer:** Arrows pierce through 1 / 2 / 3 / 4 additional enemies.  
  * **Bouncer:** Arrows ricochet to 1 / 2 / 3 / 4 nearby targets.  
> * **Axe**  
  * **Fear the Axe:** Kills freeze nearby enemies in fear for 0.2 / 0.3 / 0.4 / 0.5s.  
  * **Like Butter:** Cleaves through 2 / 3 / 4 / 5 additional targets per swing.  
  * **Celebratory Spin:** Kills trigger an automatic mini-whirlwind for 0.5 / 1 / 1.5 / 2s.  
  * **Power Dash:** Attacks charge 100% / 200% / 300% / 400% faster immediately following a dash.  
  * **Heavy Stance:** Fully charged attacks grant a 2 / 4 / 6 / 8 HP shield for 2s.  
> * **Throwing Axe**  
  * **Boomerang:** Returning axes deal full damage on their return flight path.  
  * **Bloodsplosion:** Kills trigger an explosion dealing 10 / 20 / 30 / 40 AoE damage.  
  * **First Strike:** Deals \+50% / 60% / 70% / 80% bonus damage to full-health targets.  
  * **Spin Top:** Axe becomes a slow-moving vortex dealing 4 / 8 / 12 / 16 AoE DPS.

 

### **Fortress Upgrades (Mid-Run Only)**

> * **Arrow Slits**  
  * **Reinforced Slits:** Adds 2 / 3 / 4 automated arrow slits to the fortress.  
  * **Sniper Nest:** When no enemies are nearby, fires heavy long-range arrows for 200% damage at half fire rate.  
  * **Focus Fire:** Targets hit by player ranged attacks take \+50% / 70% / 90% / 110% damage from arrow slits.  
> * **Boiling Oil**  
  * **Fiery Pitch:** Boiling oil ignites all enemies caught in its pool.  
  * **Scalding Heat:** Enemies within boiling oil take \+50% / 70% / 90% / 110% increased damage from all sources.  
  * **Expanded Vats:** Increases oil pool radius by 80% / 120% / 160% / 200%.  
> * **Spiked Barricades**  
  * **Concussive Spikes:** Barricade impacts stun enemies for 0.5 / 1 / 1.5 / 2s.  
  * **Shove:** Knocks enemies back 2m toward their spawn point every 7 / 6 / 5 / 4s.  
  * **Vulnerability Field:** Enemies within the barricade zone take \+50% / 100% / 150% / 200% increased damage from all sources.

 

### **Elemental Upgrades (Mid-Run Only)**

> * **Fire**  
  * **Blazing Trail:** Dashing leaves a fire trail dealing 4 / 6 / 8 / 10 DPS.  
  * **Combustion:** Attacks ignite enemies for 4 / 6 / 8 / 10 DPS.  
  * **Kindling:** Ignited targets take \+50% / 70% / 90% / 110% damage.  
  * **Inferno Burst:** Ultimate ability immediately ignites all nearby enemies.  
  * **Fortress Pyre:** Fortress attacks deal \+50% / 80% / 110% / 140% bonus damage to ignited foes.  
> * **Lightning**  
  * **Static Edge:** First hit of a melee chain deals 30 / 50 / 70 / 90 bonus lightning damage.  
  * **Chain Dash:** Dashing imbues the next melee swing with Chain Lightning, arcing to 4 / 6 / 8 / 10 targets.  
  * **Eye of the Storm:** Lightning strikes a random target for 50 / 100 / 150 / 200 damage throughout ultimate duration.  
  * **Tesla Spire:** Fortress discharges a bolt every 5s for 50 / 100 / 150 / 200 damage.  
> * **Ice**  
  * **Frost Step:** Dashing chills nearby enemies for 3s (reducing move and attack speed by 20% / 30% / 40% / 50%).  
  * **Deep Freeze:** Applying chill to an already chilled enemy freezes them solid.  
  * **Shatter:** Frozen enemies take \+200% / 300% / 400% / 500% damage from all hits.  
  * **Permafrost:** Fortress walls passively emit an aura that chills adjacent foes.  
  * **Ice Shards:** Ranged kills shatter enemies into an 8-way projectile burst dealing 50 / 60 / 70 / 80 damage.

 

## **6\. Permanent Meta-Progression**

Tiers are unlocked using **Orcish Metal**; individual perks are purchased with **Goblin Blood**.

### **Tier 1 (Unlocked by Default)**

> * **Backstab:** Deal \+20% bonus damage when striking enemies from behind.  
> * **Agility:** \+1 Max dash charge.  
> * **Regeneration:** \+5 HP restored upon completing each wave.

### **Tier 2 (Requires 5 Orcish Metal to Unlock)**

> * **Second Wind:** Revive once per run with 50% HP upon death.  
> * **Greed:** \+10% Gold dropped from all sources.  
> * **Executioner:** Deal \+50% damage to targets below 50% HP.

### **Tier 3 (Requires 10 Orcish Metal to Unlock)**

> * **Master Tactician:** Gain 1 free card reroll per rest area draft.  
> * **War Chest:** Begin every run with 75 starting Gold.  
> * **Deep Pockets:** The Rest Area Shop offers 4 item slots instead of 3\.