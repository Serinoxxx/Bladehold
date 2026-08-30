# Bladehold - Changelog

## [0.1.14] - 2026-08-30

### New Features
- Added Assassin enemy that winds up with a red circular telegraph, executes a multi-hit whirlwind spin attack, and is temporarily dizzy afterwards
- Added Bubbler support enemy that shields allies with a 2m protective bubble and links them with a channeling beam
- Bubbles deflect and block player melee strikes and projectile arrows
- Added heavy stompy locomotion to Slayer boss with stomping crusher mechanic that tramples minor enemies underfoot
- Added telegraphed ground slam, massive impact VFX, screen shake, and heavy smash audio to Slayer
- Enhanced cinematic enemy intro cutscenes by hiding the HUD canvas and expanding cinematic letterbox height to 220px

### Fixes
- Fixed player getting stuck floating on enemies when dismounting near groups
- Improved airborne movement control and gravity while falling
- Fixed Troll sliding backwards during ground slam wind-up
- Fixed Troll animation warping by mapping dedicated giant slam animations

### Balance Changes
- Added Assassin to the wave roster starting from Wave 3, capped at a maximum of 2 alive concurrently
- Added Bubbler as the third enemy type in Survivors Mode (unlocking at wave 2)

---

## [0.1.12] - 2026-08-29

### New Features
- Added character select screen with hero cards (Ranger, Berserker, Mage), dynamic descriptions, and 3D model preview
- Added key skill preview badges with hover tooltips detailing hero abilities
- Added scrollable changelog window in main menu displaying real-time patch notes
- Added meta progression grid in main menu
- Added 4 periodic elemental weapon imbuements (Fire, Ice, Lightning, Poison)
- Added Slayer boss intro and boss health bar
- Added fort defense sockets and objectives
- Added banish mechanic to in-run card draft
- Added treasure wagons that break into gold

### Fixes
- Fixed Fire Imbuement flame zones spawning below ground
- Fixed elemental imbuement weapon hit bindings and VFX
- Fixed Goblin Brute weapon collision box hitting outside swing arc
- Fixed unaffordable upgrade cards not dimming in meta progression grid
- Fixed card text and button overlap on high resolutions

### Balance Changes
- Capped active weapon/ability slots to 4
- Adjusted Slayer boss attack timings and recovery
- Rebalanced meta upgrade gold costs
- Adjusted enemy wave spawn pacing during defense objectives

### General Changes
- Updated loading screen to fill the logo graphic with loading progress instead of using a loading bar
- Added smooth scale pulsing and color lightening for selected character cards
- Added hover and select feedbacks for character cards
- Updated meta upgrades screen with dark parchment theme and unique icons
- Improved death screen animations and gold tally
- Added directional damage numbers and hit reactions
- Increased screen shake and hit-stop on melee strikes and explosions

---

## [0.1.11] - 2026-08-19

### New Features
- Added Golden Goblin enemy that flees with gold when spotted
- Added charged attacks for Bow, Thrown Axes, and Wand
- Added pinned arrows and thrown axes that stick into targets and terrain
- Added blood decals when ragdoll enemies hit surfaces

### Fixes
- Fixed projectiles occasionally passing through obstacles
- Fixed attack charge gauge remaining on screen after cancelling aim
- Fixed stuck arrows remaining visible after corpse despawn

### Balance Changes
- Adjusted enemy health, speed, and wave counts in early to mid waves
- Tuned knockback force and recovery thresholds
- Adjusted charge time and damage multipliers for ranged weapons

### General Changes
- Added attack charge HUD gauge
- Updated post-battle summary screen layout
- Improved arrow release sound and trail visuals

---

## [0.1.9] - 2026-08-18

### New Features
- Added Survivors Mode with continuous enemy hordes
- Added 3-card upgrade drafting on level up
- Added dedicated Survivors Mode HUD (timer, level, XP bar, card inventory)
- Added dedicated horde arena scene

### Fixes
- Fixed Goblin Brute getting stuck in attack animation loop
- Fixed enemy pathfinding hitches near arena edges
- Fixed card tooltips clipping off-screen

### Balance Changes
- Adjusted Troll ground slam radius and warning timing
- Adjusted Berserker skill tree costs and damage bonuses
- Tuned XP gem drop rates and leveling curve

### General Changes
- Added visual telegraph decals for heavy and area attacks
- Polished enemy movement and attack animation blending

---

## [0.1.8] - 2026-08-17

### New Features
- Added stat and cost hover tooltips to skill tree nodes

### Fixes
- Fixed skill nodes rapidly growing and shrinking in a loop on hover
- Fixed cursor hover jitter on skill tree buttons

### Balance Changes
- Adjusted mid-tier skill node gold costs

### General Changes
- Smoothed hover scale transitions on skill tree buttons

---

## [0.1.7] - 2026-08-17

### New Features
- Added class ultimate abilities
- Added ultimate charge meter with full-charge and activation visuals
- Added Arid Desert biome
- Added loot chest drop visual effects

### Fixes
- Fixed horse summon duration bar not draining properly
- Fixed overkill damage counting toward ultimate charge
- Fixed missing controller and keyboard bindings for ultimate ability

### Balance Changes
- Reduced player movement speed while aiming ranged weapons
- Replaced sprint upgrades with active dodge
- Changed ultimate trickle charge to tick once per second instead of every frame

### General Changes
- Added hit sparks, screen shake, and impact sounds to weapon hits
- Added Berserker and Mage skill tree preview panels in editor

