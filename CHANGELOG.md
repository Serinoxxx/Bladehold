# Bladehold - Changelog

## [0.1.19] - 2026-09-03

### New Features
- Overhauled core game loop into 4 rounds of 3 waves with progressive enemy unlocks (Round 1: Goblins & Brutes, Round 2: Big Orks, Round 3: Bubblers, Round 4: Bombers + Slayer Boss)
- Replaced end-of-wave reward drops with a physical, interactive War Banner selection system granting unique modifiers and rewards
- Refactored single-element lock into a Hades-inspired ability-slot elemental system (Melee, Ranged, Mobility, Ultimate, Fortress)
- Added new elemental statuses: Ignited (DoT), Chilled (Slow), Frozen (Stun), and Conductive (Chain Lightning)
- Added Elemental Discord mechanic: Enemies inflicted with 2 or more distinct elements take +40% damage from all sources
- Added Duo Synergies to the Draft Upgrade pool (Thermal Shock, Plasma Overload, Superconductor) requiring specific elements equipped
- Added compensation mechanic for replacing an elemental ability slot (+25 In-Run Gold)
- Added dedicated Rest Area scene between rounds (Waves 3, 6, 9) featuring the Well (+20 HP), Merchant Shop, Upgrade Draft station, and Return Gate
- Added dedicated Meta Progression Area scene upon defeat featuring the Spirit NPC and diegetic 3D weapon pedestals
- Added universal interaction framework using the 'E' key and gamepad for all world stations, pedestals, and gates
- Added flexible weapon loadout system allowing switching between 1 Melee weapon (Sword or Axe) and 1 Ranged weapon (Bow or Throwing Axe)
- Added 3-second ground warning telegraph indicators before enemies spawn and capped active enemies to 20
- Added destructible Bubble Shields with health pools and a 10-second re-shield cooldown when broken
- Added wave-end drop rewards (Troll Hearts, Orcish Metal, Goblin Blood, In-Run Gold, and Instant Upgrade Drafts) with 30-second intermissions
- Added passive Training Dummy Goblin to Rest Area and Meta Progression Area with 1000 HP, floating health display, 10-second idle reset, and poof VFX at origin and destination
- Added modular UI prefabs for the interaction prompt and rest area shop modal styled with the dark fantasy parchment aesthetic
- Added MoreMountains Feel feedbacks for Rest Area shop cards: horizontal card shake, red flash, and error sound on invalid buy attempts; spring scale bounce, coins audio, and smooth card disappearance on successful purchase
- Added arena upgrade powerups dropping between waves that open a 3-card draft for Weapon, Elemental, or Fortress upgrades
- Added 'Return to the Fortress' objective with a gate waypoint marker upon clearing all 3 waves of a round
- Added dedicated Draft Upgrades CSV (`DraftUpgrades.csv`) and `DraftUpgradeService` providing targeted weapon upgrades, elemental skill paths, and fortress enhancements
- Added dedicated Weapon Ultimates: Warhorse Cavalry Charge for Sword (`SwordMountUltimate`) and Axe Vortex bloodstorm cyclone for Throwing Axe (`ThrowingAxeUltimate`) with rapid 3-way fan throws
- Added category-themed lighting, emission, and interaction prompt feedback to arena powerups and the Rest Area Draft Station (Orange for Weapon, Cyan for Elemental, Golden Amber for Fortress)

### Fixes
- Fixed player character getting stuck in air and unable to move when dismounting or after horse death mid-air
- Fixed interaction prompt persisting indefinitely on screen after moving away from world interactables
- Fixed inability to interact with the Merchant Shop by dynamically tracking character movement and expanding the shop stall interaction radius
- Fixed missing EventSystem in Rest Area and Meta Area scenes preventing UI button clicks and modal inputs
- Fixed enemies continuing to spawn during wave intermissions by strictly halting spawning when the wave quota is wiped
- Fixed character upgrades and drafted in-run skills resetting when transitioning between the Battle Scene and Rest Area Scene
- Fixed permanent meta perks (`backstab`, `executioner`, `second_wind`, `agility`) not functioning in gameplay
- Fixed Rest Area Draft Station failing to open card drafts due to missing scene managers

### Balance Changes
- Enforced continuous enemy spawning during wagon escort objectives until the wagon reaches the destination
- Introduced three-tier currency economy: In-Run Gold (temporary for rest shop), Goblin Blood (permanent for perks), and Orcish Metal (permanent for weapon/tier unlocks)
- Capped maximum concurrent active enemies on the field to 20
- Unlocked Dash and Bow by default from wave 1 without requiring skill purchases
- Enforced weapon ultimate exclusivity permitting at most one active ultimate ability per run

### General Changes
- Added Bladehold Rest Area Scene and Bladehold Meta Area Scene to project build settings
- Preserved player health ratio, Troll Heart bonus health, and in-run upgrade tiers across scene transitions
- Integrated Bladehold HUD, Pause Menu, and Settings Canvas across the Survivors battle scene, Rest Area, and Meta Area scenes
- Disabled legacy level-up keybind prompt in favor of between-wave arena upgrade powerup drops
- Supported both Space and Left Ctrl keys for triggering player Dash/Dodge

---

## [0.1.14] - 2026-08-30

### New Features
- Added waypoints for objectives
- Added enemy blood decals on hit
- Added Assassin enemy with whirlwind attack
- Added Bubbler support enemy that shields allies
- Added stomping crusher attack to Siegebreaker boss
- Added Siegebreaker boss ground slam attack
- Enhanced cinematic enemy intros
- Reworked spike barricades into thrusting traps
- Added visual and sound effects to dodge
- Added Earth Splitter charge attack
- Updated character selection cards to load dynamically

### Fixes
- Fixed objective and quest complete banners not appearing
- Fixed missing audio for objective and quest announcements
- Fixed Bubbler enemy animation issues
- Fixed Bubbler shield beam not connecting
- Fixed player floating on enemies when dismounting
- Improved airborne movement control
- Fixed Troll sliding backwards during ground slam
- Fixed Troll slam animation warping
- Fixed enemy physics issues on spike traps
- Fixed charge attacks getting stuck during pause or card drafts
- Fixed game time resuming incorrectly after pausing
- Fixed locked ability icons showing in HUD

### Balance Changes
- Added Assassin to wave 3
- Added Bubbler to Survivors Mode wave 2

### General Changes
- Locked unreleased Mage class and extra levels in main menu
- Disabled slide and crouch mechanics
- Updated Survivor Mode enemy spawn locations
- Enemies now guard objective points during Survivor Mode

---

## [0.1.12] - 2026-08-29

### New Features
- Added character select screen with 3D model previews
- Added skill tooltips to character select
- Added in-game changelog viewer in main menu
- Added meta progression grid in main menu
- Added elemental weapon imbuements (Fire, Ice, Lightning, Poison)
- Added Siegebreaker boss intro and health bar
- Added fort defense objectives
- Added banish option to card drafts
- Added gold treasure wagons

### Fixes
- Fixed Fire Imbuement zones spawning below ground
- Fixed elemental weapon hit effects
- Fixed Goblin Brute weapon hitboxes
- Fixed unaffordable upgrade cards not dimming
- Fixed card text overlapping on high resolutions

### Balance Changes
- Capped active weapon slots to 4
- Adjusted Siegebreaker boss attack timings
- Rebalanced meta upgrade gold costs
- Adjusted wave spawn pacing during defense objectives

### General Changes
- Updated loading screen progress visual
- Added selection feedback to character cards
- Updated meta upgrades screen visuals and icons
- Improved death screen animations and gold tally
- Added directional damage numbers
- Increased screen shake and hit-stop on heavy hits

---

## [0.1.11] - 2026-08-19

### New Features
- Added fleeing Golden Goblin enemy
- Added charged attacks for Bow, Thrown Axes, and Wand
- Projectiles now stick into targets and terrain
- Added impact blood decals for ragdolls

### Fixes
- Fixed projectiles passing through obstacles
- Fixed charge gauge remaining on screen after cancelling aim
- Fixed stuck arrows persisting after corpse despawn

### Balance Changes
- Adjusted early to mid wave enemy health and counts
- Tuned knockback force and recovery
- Adjusted charge times and damage for ranged weapons

### General Changes
- Added attack charge HUD gauge
- Updated post-battle summary screen layout
- Improved arrow sound and trail effects

---

## [0.1.9] - 2026-08-18

### New Features
- Added Survivors Mode
- Added 3-card upgrade drafting on level up
- Added Survivors Mode HUD
- Added dedicated horde arena

### Fixes
- Fixed Goblin Brute getting stuck in attack animation
- Fixed enemy pathfinding issues near arena edges
- Fixed card tooltips clipping off-screen

### Balance Changes
- Adjusted Troll ground slam radius and timing
- Adjusted Berserker skill tree costs and damage
- Tuned XP drop rates and leveling curve

### General Changes
- Added attack telegraphs for heavy and area attacks
- Polished enemy movement and attack animations

---

## [0.1.8] - 2026-08-17

### New Features
- Added stat and cost tooltips to skill tree nodes

### Fixes
- Fixed skill node hover animation loop
- Fixed cursor hover jitter on skill tree buttons

### Balance Changes
- Adjusted mid-tier skill node gold costs

### General Changes
- Smoothed hover transitions on skill tree buttons

---

## [0.1.7] - 2026-08-17

### New Features
- Added class ultimate abilities
- Added ultimate charge meter and visual effects
- Added Arid Desert biome
- Added loot chest visual effects

### Fixes
- Fixed mount duration bar not draining
- Fixed overkill damage granting ultimate charge
- Fixed missing controls for ultimate abilities

### Balance Changes
- Reduced movement speed while aiming ranged weapons
- Replaced sprint upgrades with active dodge
- Adjusted passive ultimate charge rate

### General Changes
- Added hit sparks, screen shake, and impact sounds
- Added Berserker and Mage skill tree preview panels
