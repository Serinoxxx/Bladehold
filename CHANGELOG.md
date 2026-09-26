# Bladehold - Changelog

## [0.1.28] - 2026-09-13

### New Features

- Added supply refund on sector victory: towers are dismantled and their remaining supply plus upgrade costs return to you
- Unified victory and defeat presentation using the customizable End Game Screen prefab, displaying triumphant victory titling upon clearing all sector waves
- Added automated behavioral benchmark tests verifying end game screen victory mode, defeat routing, and live currency updates
- Added automatic post-objective cleanup phase transitioning the objective to "Kill all remaining enemies" whenever stragglers remain on the battlefield
- Added red skull HUD waypoint indicators hovering over all remaining enemies with screen-edge clamping and distance meters directing players to stragglers
- Added 5-wave defense sector loop replacing periodic 3-wave rest area breaks with continuous wave combat
- Added Victory Screen overlay celebrating defense node completion with wave stats, kills, currencies secured, and triumphant audio fanfare
- Added Wave 5 Clan Captain climax encounter to defense sectors
- Added automated behavioral benchmark tests verifying 5-wave defense loop, victory trigger, and health ratio persistence
- Added Fishing Minigame with 7 tactical nodes along the campaign path replacing supply rooms and adding side routes
- Added 60-second Fishing Frenzy minigame featuring a dedicated fishing bow with infinite ammo and fish circling a pond
- Added 6 minigame draft upgrade cards (Bounce Shot, Fishsploshion, Icey Water, Fish Skewer, Bleed, Fat Fish) offered on fishing level-up
- Added 6 rare Buff Fish variants granting permanent in-run stat bonuses with a maximum cap of 3 consumed buff fish per run
- Added glowing Diamond Fish spawning after 30 seconds with 20x health, awarding permanent Diamond Fish Bones
- Added start prompt, 3-2-1 countdown with thump audio and punch animation, and horn signal for frenzy start
- Added end-of-session results tally modal displaying fish caught, currencies earned, and Buff Fish feast selection
- Added dedicated Fishing Pond arena scene with water perimeter barriers preventing players from entering the pond
- Added automated behavioral benchmark tests verifying campaign fishing nodes, draft upgrade formulas, buff fish cap, and currency persistence

- Added transparent ghost preview and modular piece-drop construction animations for multi-part defense structures with ground dust puffs and screenshake
- Added rising ground emergence animations with rumble tremors, dust, and heavy lock-in slam for compact defenses
- Added physical arcing net projectiles with spinning net meshes and ground impact effects for Net Thrower defenses
- Added 3D rope and mesh capture visuals bound to enemies while immobilized by Net Throwers
- Added booming impact explosion and fiery debris VFX to catapult boulder detonations
- Added sensory feedback for tower construction, repairs, and upgrades with wood impact sounds, dust puffs, and floating supply cost popups

- Added active towers HUD panel displaying deployed defense types, upgrade levels, and real-time supply sliders
- Added 'NO SUPPLY' objective marker overlay pointing to depleted battlefield defenses
- Added non-destructive supply depletion keeping towers standing on plots when reaching 0 supply
- Added target tracking rotation and visual pre-fire anticipation animations for Ballista, Catapult, and Net Thrower defenses
- Added target movement prediction and leading trajectory calculations to Arrow Towers
- Added visual Balance Tree Editor window (`Bladehold > Balance Tree Editor` or `F1`) displaying an interactive node graph of Weapons, Draft Upgrades, Armour Sets, Mounts, Meta Perks, and Tower Defenses
- Added in-place balance tweaking and inspector panel for graph nodes with live mid-game apply and disk persistence for ScriptableObjects and DraftUpgrades.csv
- Added Add Node menu and TODO node specification tracking writing pending mechanics and dependencies to `UPGRADE_TODOS.md` for AI implementation
- Added 6 Elemental Draft Cards for battlefield defenses in the draft pool: Frost Arrows, Glacial Catapult, Lightning Arrows, Tempest Catapult, Fire Arrows, and Pyroclast Catapult
- Added Frost Arrows imbuing Arrow Tower volleys with chill status that slows target movement speed by 40%
- Added Glacial Catapult creating slippery frozen ground upon rock impact that trips incoming enemies and makes them slip over
- Added player ice sliding allowing heroes to slide smoothly across frozen ice zones with +35% move speed and reduced movement friction while retaining responsive steering and rotation
- Added Lightning Arrows accelerating Arrow Tower fire rate by +50% and imbuing projectiles with electric shock damage
- Added Tempest Catapult generating a lingering overhead storm cloud on impact that repeatedly calls down lightning strikes on enemies below
- Added Fire Arrows enhancing Arrow Tower arrow damage by +40% and igniting struck foes for burning damage over time
- Added Pyroclast Catapult launching an impact rock that breaks out into a rolling fireball, blazing across the battlefield, leaving fiery trails, and damaging enemies in its path
- Added melee fireball redirection allowing the player to strike rolling fireballs with melee weapons to steer their trajectory and grant a speed boost
- Added automated behavioral benchmark tests verifying elemental draft upgrades, tower fire rates, ice zone mechanics, player sliding, storm clouds, and melee fireball redirection

- Added Princess Katherine boss encounter in the Royal Sanctuary with fleeing AI and holy revival mechanics
- Added Armored Knight AI enemies with sword combat, downed states at 0 HP, and golden soul beacons
- Added dynamic revival spell channeling where attacking Princess Katherine delays her 5-second cast by +1.5 seconds per hit
- Added regal Royal Throne Annex arena scene (`Bladehold Princess Sanctuary.unity`) with throne dais, crimson carpets, colonnades, and baked NavMesh
- Added Dark Campaign Victory awarding 500 Gold, 30 Goblin Blood, and 10 Orcish Metal upon defeating the Princess
- Added automated behavioral benchmark tests validating Armored Knight downed state, 5.0-second spell channeling, player hit delay penalties, and revival restoration
- Added Necromancer Revelation encounter with dialogue choice system ([Obey] Slay Princess Katherine vs [Defy] Slay the Necromancer)
- Added two-phase Necromancer boss battle featuring an invulnerable Bubble Shield, skeleton army summoning, and Phase 2 direct scythe combat with 180-degree telegraphed sweeping slashes
- Added Crypt Skeleton AI minions with NavMesh tracking, melee attacks, and automatic shield shatter death callbacks
- Added colossal indoor crypt arena scene (`Bladehold Necromancer Crypt.unity`) with vaulted colonnades, stone tombs, ritual altar, braziers, and baked NavMesh
- Added automated behavioral benchmark tests validating Necromancer bubble shield invulnerability, skeleton death shield shatter, Phase 2 melee vulnerability, and campaign node routing

- Added 7 Castle Campaign level scenes with distinctive architecture and atmospheric lighting: Castle Courtyard (Tier 1 Center), Castle Ramparts (Tier 2A), Castle Armory (Tier 2B), Great Hall (Tier 4), Castle Dungeons (Tier 5A), Castle Conservatory (Tier 5B), and Throne Antechamber (Tier 7)
- Added automated Castle Campaign level generator editor tool (`BuildCastleLevels.cs`) constructing greybox geometry, perimeter battlements, lighting, connected prefabs, choke point tower defense plots, exit gates, and baked NavMesh
- Added 4 to 6 strategic Tower Plots (`TowerPlot.prefab`) in each castle level wired to `TowerPlotManager` and `FortDefenseManager` with full Build Wheel UI support
- Added Castle Gate extraction exits unlocking `[E] View Campaign Map` upon completing the 3rd wave of each sector, returning players directly to the overview war room map
- Added tier-scaled wave progression advancing wave numbers and difficulty across campaign tiers (Tier 1: Waves 1-3, Tier 2: Waves 4-6, Tier 4: Waves 7-9, Tier 5: Waves 10-12, Tier 7: Waves 13-15)
- Added smashable Supply Room sector in the Castle Campaign featuring destructible wooden crates, barrels, and reinforced lockboxes
- Added SupplyBox destructible component rewarding in-run Gold, Fort Supply, permanent Goblin Blood, and Orcish Metal when broken by weapons
- Added non-hostile Supply Room safe area with player upgrade rehydration and return gate to the Campaign Map
- Added Castle Campaign 8-tier branching node progression graph, Campaign Overview Map UI, and sector hover tooltips
- Added Clan Captain Captain Kombusta with a 10-dynamite barrage special attack at range (>=5m) dealing 20 damage in a 2m telegraphed radius, and a close-quarters self-immolation fire aura after 3 seconds in melee range
- Added automated behavioral benchmark tests for Captain Kombusta dynamite explosions, distance qualification, and melee self-immolation
- Added Deep Quiver Tier 1 meta upgrade increasing maximum ammo capacity by +5
- Added ammunition bundle purchasable at the Rest Area shop
- Added battlefield ammunition drops from defeated enemies
- Added center-screen warning when aiming without ammunition
- Added contextual aiming HUD showing current ammo under crosshairs only while aiming
- Added universal ammunition pool for ranged weapons (bow, thrown axe, wand) starting with 20 base capacity
- Added dedicated mount system triggered at any time using [X] with an interruptible summon cast time
- Added 5 unlockable mount variations in the Meta Area purchasable with Orcish Metal (Frost Strider, Infernal Steed, Abyssal Behemoth, Phantom Charger, Celestial Dreadnought) with distinct speeds, charge damage, knockback, durations, and cooldowns
- Added diegetic 3D mount pedestals in the Meta Area for inspecting, unlocking, and equipping mounts
- Added Blade Tempest as the signature sword melee ultimate ability, replacing the old mount ultimate
- Unlocked horse archery from the get-go across all loadouts
- Added HUD cast bar, active duration timer, and cooldown tracker for mounts
- Added battlefield defences system with static Tower Plots placed across the battlefield, allowing players to build defenses during preparation phases via an interactive Build Wheel
- Added 6 upgradable defense types: Arrow Tower (rapid light piercing), Catapult (lobbed fire splash damage), Ballista (heavy line-piercing bolts), Net Thrower (area root and immobilization), Spike Trap (high damage impale triggers), and Oil Vat (boiling oil slowing and scalding)
- Added dynamic assembly drop sequence where defenses drop from the sky piece-by-piece with holy light rays and wood impact slamming
- Added Supply currency system used to construct, maintain, and upgrade battlefield structures; defenses consume supply as they fire and break if depleted
- Added in-field resupply and tier upgrading mechanics on active defenses using Supply
- Added HUD Supply currency counter in the top-left resource bar
- Updated Bulwark with directional blocking allowing backstab strikes to bypass the shield and stagger, separated block animations onto an upper body layer for continuous locomotion, and added distinct full-body stagger flinch reactions
- Added Bulwark enemy carrying a destructible physical shield that stops player melee attacks, mitigates projectile damage, and retaliates with a telegraphed counter-slam
- Added Stop the Battering Ram objective where enemies push a siege ram toward the castle gate requiring the player to destroy it before it breaches the defenses
- Added Powder Keg enemy type carrying an overhead explosive barrel that can be shot with arrows to detonate into nearby foes or slams down to detonate near the castle gate
- Added Bannerman enemy type granting localized proximity aura buffs to nearby allies based on the active wave banner with distinct highlight glows
- Added destructible overhead banner system allowing players to shoot and disable enemy buff auras independently
- Added 1-Click Combat Scenarios harness to developer console for rapid testing (Sword vs Dummy, Axe vs Brutes, Mace vs Bubbler, Fire Swarm, Bow Longshot, Ultimate Unleash)
- Added automated Mechanics Benchmark tool validating all 57 draft cards, all 4 armour sets, live dash fire trail spawning, weapon hitboxes, and bubble shield absorption
- Added automated behavioral mechanic tests for Life Steal healing, Backstab angle damage, Executioner low-HP bonus, Second Wind death revival, Greed/War Chest gold scaling, and ShieldBreaker damage amplification

- Added overhead health bar and hit reactions (red flash, shield bash impact audio, and wood splinter particles) to the Battering Ram
- Added automated behavioral benchmark tests verifying Battering Ram escort formation spacing and lead waypoint calculations

- Added customizable BuildWheelSliceButton prefab asset styled with authentic Synty circular tracery, gold border rings, defense icons, and supply cost badges
- Added defense icons to the defensive build wheel for Arrow Tower, Catapult, Ballista, Net Thrower, Spike Trap, and Oil Vat
- Added automated behavioral benchmark tests verifying decoupled reticle visual scaling, ammo counter font size, and build wheel button prefab dimensions
- Sectors now get harder the deeper you go into the campaign: new enemy types join the horde tier by tier (Bannermen and Powder Kegs, then Bulwarks and Assassins, then Storm Witches, then Trolls), while goblins still make up most of every wave

### Fixes

- Fixed the field going empty during Supply Wagon and Battering Ram objectives: enemies now keep arriving until the objective is resolved
- Fixed Rest Area healing and ultimate charge being lost when returning to the campaign map
- Fixed ultimate charge not carrying over between sectors
- Fixed Armored buff fish max health stacking again on every scene load
- Fixed upgrades being applied twice in the Supply Room
- Fixed Goblin Blood and Orcish Metal counters displaying placeholder values instead of current save quantities on the end game screen
- Fixed defeat screen forcibly transitioning to the meta area after two seconds, allowing players to view combat stats and choose when to return
- Fixed end game screen navigation routing to the Campaign Map upon victory and returning to the Meta Area upon defeat
- Added missing Defensive Supply currency row and icon to the end game screen
- Removed procedural fallback victory UI to ensure end-of-run presentation uses authorable UI assets
- Fixed enemy spawner continuing to spawn new enemy waves after main wave objectives had already been completed
- Fixed objective HUD text prematurely falling back to incoming objective prompts while enemies remained after an objective completed
- Fixed missing Supply currency counter on the player HUD
- Fixed Captain Kombusta bomb telegraphs floating or misaligned with terrain slopes
- Fixed Captain Kombusta bomb pacing and flight duration to give a 1.0-second delay between telegraph indicators
- Removed blueprint ghost visual during tower defense construction
- Fixed arrow ammo count and out of ammo warning appearing too small on 1080p and different resolutions by scaling them for the 4K reference canvas with high-contrast outlines
- Fixed arrow ammo count shrinking when charging bow draws by decoupling the crosshair reticle tightening scale from child HUD counters
- Fixed build wheel UI appearing cramped by expanding the wheel to a 1400px Synty radial layout with 240px slice buttons and scaled typography
- Fixed victory screen being bypassed when completing defense sectors in campaign mode
- Fixed Arrow Tower and other defense structures continuously firing at friendly Castle Gates
- Fixed Battering Ram remaining stalled during the siege objective by ensuring enemies path to lead and escort the ram forward
- Fixed boss controller component initialization in test mode ensuring damage block event listeners are hooked immediately upon combat activation
- Fixed Campaign Overview Map displaying no nodes by authoring dedicated node button and route line prefabs, anchoring scroll containers to the left margin, and connecting a persistent campaign graph asset
- Fixed coin, health pack and orb pickups playing their sound twice
- Fixed the ammo pickup making no sound
- Fixed destroyed defenses, the build wheel, supply wagon gold bags and some objective markers losing their effects or icons outside the Editor
- Fixed Captain Kombusta's dynamite, fishing arrows and pond fish showing up as plain placeholder shapes
- Fixed the vortex blades of the throwing-axe ultimate showing as red boxes instead of axes
- Fixed the mace ultimate's ground slam flashing your hero instead of shaking the screen; it now shakes the screen as intended
- Fixed firing with an empty quiver making no sound; you now hear a dry click
- Fixed the Earth Splitter smash having no screenshake
- Fixed the light flash on Impulse hits never showing; it now pulses where the hit lands
- Fixed Plasma Overload leaving a fireball burning forever where it went off
- Fixed blood splashes from sword hits sometimes coming out the wrong size after a ragdoll had bled nearby
- Fixed the fleeing Golden Goblin dying silently; it now bursts into coins with a jingle like other golden enemies
- Fixed finished enemy effects (lightning, boulder and dynamite blasts, gold bursts) staying in the level after they faded, slowly piling up over a sector
- Fixed tower effects (catapult impacts, rolling fireball bursts, wood splinters when a tower breaks) staying in the level after they faded
- Fixed the Fishing Frenzy countdown thumps and start horn never playing
- Fixed the battering ram's splinters and the prisoner cage's dust cloud staying in the level after they faded
- Fixed the Necromancer's shield break and scythe hits not shaking the screen
- Fixed the Necromancer's summoning circles staying in the crypt for the rest of the fight

### Balance Changes

- Kill quotas grow by 10% per campaign tier, and at least 60% of every wave is goblins
- Heavier enemies are now limited to a few on the field at once
- Replaced Bulwark enemy telegraphed slam attacks with standard melee strikes when close to the player, removing the slam entirely
- Increased Captain Kombusta bomb flight time to 2.0 seconds and bomb throw interval to 3.0 seconds
- Adjusted defense level pacing to 5 waves per combat node with staged enemy roster progression from goblins to heavy siege units
- Configured hero health ratio to persist across campaign nodes
- Standardized basic warhorse to 30-second duration and 90-second cooldown with a 1.5-second summon cast time

### General Changes

- Updated the Battle Portal to start a new run straight on the campaign map
- Removed Retry Level from the defeat screen; dying always returns you to the Meta Area
- Removed the Return to Stronghold button from the campaign map
- Towers no longer carry over between sectors; each sector starts with empty plots
- Updated Bannerman enemy variant to use the Goblin Brute model with war banner mounted to the upper spine
- Updated campaign combat sector tooltip labels to reflect 5-wave defense structure
- Added click-to-proceed button on Victory Screen routing directly to the Campaign Map
- Registered Necromancer's Crypt and Princess Sanctuary in AreaDatabase and linked Tier 8 campaign node branches
- Removed fortress defense cards from the random draft pool in favor of dedicated static battlefield plots and the Build Wheel system
- Replaced banner bounty fortress draft rewards with Supply Cache payouts
- Removed deprecated class definitions and legacy skill tree data in favor of the unified Hero loadout and draft upgrade systems
- Migrated net captured status visuals from procedural runtime code to an authored Editor prefab configured via NetRootConfigSO
- Removed runtime asset-path fallbacks from Net Thrower defense and Net projectile scripts
- Added project rule prohibiting procedural visual creation in code and runtime asset-path fallbacks
- Added automated editor setup script to regenerate campaign UI prefabs and persistent graph data

## [0.1.20] - 2026-09-08

### New Features
- Added the Heavy War Mace as an unlockable two-handed melee weapon with high-impact blunt staggering, armor-shattering strikes, and charged ground shockwaves
- Added diegetic Mace Pedestal in the Meta Area allowing permanent unlock with Orcish Metal and immediate loadout equipping
- Added 5 targeted mace draft cards to in-run upgrade pools (Armor Shatter, Concussive Impact, Earthshaker, Colossal Force, and Seismic Quake)
- Added signature Seismic Quake ultimate ability triggering a cataclysmic radial ground slam that crushes, launches, and stuns surrounding foes
- Added multi-door Rest Area exit system allowing navigation to distinct scenes and stages with contextual interaction prompts
- Added meta-information loading screen displaying "Entering [Area Name]" alongside subtitles, lore descriptions, and progress bars during scene transitions
- Added AreaDefinition ScriptableObject and global AreaDatabase mapping scene destinations to rich player-facing stage lore

### Fixes
- Fixed equipped weapons not updating visually in the Meta Area
- Fixed the Meta Area camera no longer following the player after a death transition

### General Changes
- Updated weapon pedestal information panels to appear when approached or focused
- Unified scene transition and loading logic between Main Menu, Meta Area Battle Portal, and Rest Area exits with shader prewarming support
- Added developer-console shortcuts for testing scene transitions

## [0.1.19] - 2026-09-03

### New Features
- Added dramatic lightning strike elimination for enemies remaining when the 20-second post-quota catch-all timer expires, dealing lethal damage with thunderous visual and audio effects instead of abruptly vanishing
- Added Goblin Blood and Orcish Metal displays to the HUD and updated the Gold display to track run-specific gold
- Added modular Clan Buff and Banner Reward ScriptableObjects allowing easy buff and reward pool customization
- Replaced text-heavy War Banners with a quick-facts card showing clan icon, 1-line buff fact, and reward quantity
- Added Golden Goblin wave objective: a special goblin with 999 health that drops gold based on damage and a 100 gold bonus if killed within 30 seconds.
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
- Added specific visual effects for equipped weapons and dash trails when drafting elemental cards (Fire, Ice, Lightning, Poison)
- Added new visual and sound effects for War Banners including a staggered slam-down animation on spawn, ground waypoints, and a burning sequence when selected

### Fixes
- Fixed an issue where activating an unlocked Warhorse Mount ultimate triggered the Arrow Stream ability instead by ensuring ultimate handlers are correctly configured across player hierarchy transforms and synchronized to the active loadout.
- Fixed enemies pathing and flocking toward objective points (such as the supply wagon or prisoner cages) and idling without attacking; enemies now prioritize and path directly toward the player
- Fixed a softlock where the game would freeze in an empty arena if a timed wave objective was failed
- Fixed Chain Dash elemental upgrade failing to imbue the next melee swing with chain lightning
- Fixed Axe charge attack failing when actively equipped via the new weapon loadout system
- Fixed the game appearing to freeze after clearing a wave by displaying the intermission choice menu immediately, and fixed the pause menu breaking the camera when opened during the intermission
- Fixed wave spawner occasionally exceeding the 20 concurrent enemy limit.
- Fixed enemy health bars rendering inside the models of larger enemies (like Big Ork and Bosses) by dynamically checking height.
- Fixed language settings changes failing to apply in the Main Menu.
- Fixed an issue where objective waypoint markers remained on screen after destroying targets like catapults
- Fixed player character getting stuck in air and unable to move when dismounting or after horse death mid-air
- Fixed interaction prompt persisting indefinitely on screen after moving away from world interactables
- Fixed inability to interact with the Merchant Shop by dynamically tracking character movement and expanding the shop stall interaction radius
- Fixed missing EventSystem in Rest Area and Meta Area scenes preventing UI button clicks and modal inputs
- Fixed enemies continuing to spawn during wave intermissions by strictly halting spawning when the wave quota is wiped
- Fixed character upgrades and drafted in-run skills resetting when transitioning between the Battle Scene and Rest Area Scene
- Fixed permanent meta perks (`backstab`, `executioner`, `second_wind`, `agility`) not functioning in gameplay
- Fixed Rest Area Draft Station failing to open card drafts due to missing scene managers

### Balance Changes
- Adjusted Swarm-Blight Clan buff to regenerate a flat 2 HP per second
- Bubblers will now only spawn in Round 3 (removed from Round 4).
- Adjusted default tunables and mechanics for weapons and dash, and updated training dummy.
- Enforced continuous enemy spawning during wagon escort objectives until the wagon reaches the destination
- Introduced three-tier currency economy: In-Run Gold (temporary for rest shop), Goblin Blood (permanent for perks), and Orcish Metal (permanent for weapon/tier unlocks)
- Capped maximum concurrent active enemies on the field to 20
- Unlocked Dash and Bow by default from wave 1 without requiring skill purchases
- Enforced weapon ultimate exclusivity permitting at most one active ultimate ability per run

### General Changes
- Added sound effects and floating popups when collecting Goblin Blood, Orcish Metal, and Gold resource rewards
- Applied the dark fantasy parchment theme to the Meta Upgrades shop UI
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


















