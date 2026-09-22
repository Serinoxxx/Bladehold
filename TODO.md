# Unity Editor Wiring TODOs

## Castle Campaign Part 1: Campaign Progression, Node Graph & Overview Map UI

The core data structures, state management, UI controllers, and scene transition hooks for Part 1 of the Castle Campaign overhaul are implemented:
- **Campaign Data Models** (`Assets/Bladehold/Bladehold Scripts/Campaign/CampaignNodeType.cs`, `CampaignNodeSO.cs`, `CampaignGraphSO.cs`):
  - `CampaignNodeType`: `Combat`, `RestArea`, `SupplyRoom`, `PreBoss`, `NecromancerEncounter`, `PrincessBoss`, `NecromancerBoss`.
  - `CampaignNodeSO`: Stores Node ID, display title, subtitle, lore description, destination scene name, encounter type, Clan Captain details (captainName, difficultyTier, clanBuff, captainIcon), and rewards preview (bountyType, rewardsDescription, goldReward, bloodReward, metalReward).
  - `CampaignGraphSO`: Houses the 8-tier progression graph with programmatic generator `BuildDefaultGraph()` creating:
    - Tier 1: Courtyard Entrance (Combat: 3 waves, tower slots)
    - Tier 2: North Ramparts (Choice A: Captain Fraglob, Gold Cache) / Armory Barracks (Choice B: Captain Kombusta, Orcish Metal)
    - Tier 3: Castle Rest Area (Option A: Shop, Well, Drafts) / Supply Room (Option B: Free smashable crates)
    - Tier 4: Great Banqueting Hall (Combat: 3 waves, high density horde)
    - Tier 5: Dungeon Oubliette (Choice A: Captain Fraglob, Goblin Blood) / Royal Conservatory (Choice B: Captain Kombusta, Element Draft)
    - Tier 6: Inner Rest Sanctuary / Royal Supply Vault
    - Tier 7: Throne Antechamber (Pre-Boss: Captain Kombusta Omega tier)
    - Tier 8: Crypt Sanctum (Necromancer Encounter: Revelation story dialogue)
- **Campaign State Management** (`Assets/Bladehold/Bladehold Scripts/Campaign/CampaignManager.cs`, `RunSession.cs`):
  - Singleton `CampaignManager.Instance` tracking `CurrentNodeId`, `CompletedNodeIds`, and `AvailableNodeIds`.
  - Syncs across scenes via static collections in `RunSession` and resets on `RunSession.StartNewRun()`.
  - `GameLoopManager.cs` and `RestAreaGate.cs` hooks: completing wave 3, triggering victory, or leaving a rest area advances campaign progress and opens the overview map.
  - `SelectNode(CampaignNodeSO node)` deploys player into the selected sector scene via `LoadingScreenManager`.
- **Overview Map UI Screen** (`CampaignMapUI.cs`, `CampaignNodeButtonUI.cs`, `CampaignTooltipUI.cs`):
  - Node button rendering with distinct visual states: `Completed` (checkmark, slate), `Available` (golden border, glow), `Locked` (padlock, dark steel).
  - Rich hover tooltip displaying Sector Title & Subtitle, Encounter Badge, Clan Captain name & difficulty skulls, Rewards preview, and Sector lore.
  - Procedural dynamic path connector lines indicating routes between nodes.
- **Scene & Build Registration**:
  - `Assets/Bladehold/Bladehold Scenes/Bladehold Campaign Map Scene.unity` created and registered in `EditorBuildSettings.asset` and `AreaDatabase.cs`.

### Wiring & Asset Checklist
- [x] Create Campaign C# data structures and managers under `Assets/Bladehold/Bladehold Scripts/Campaign/`.
- [x] Register `Bladehold Campaign Map Scene.unity` in `EditorBuildSettings.asset` and `AreaDatabase.cs`.
- [ ] In Unity Editor, create asset instance `CampaignGraph_Castle.asset` (Right Click > `Scriptable Objects/Campaign/Campaign Graph`) and run Context Menu `Generate Castle Campaign Graph` to persist the 8-tier asset to `Assets/Bladehold/Resources/CampaignGraph.asset`.
- [ ] In `Bladehold Campaign Map Scene.unity`, wire `CampaignMapUI` serialized fields (Canvas RectTransforms, Button Prefab, Tooltip UI) if custom authored prefabs are desired over procedural fallbacks.

### Manual Verification
- [ ] Load `Bladehold Campaign Map Scene.unity` in Play mode; verify all 8 tiers render with Tier 1 (`Courtyard Entrance`) unlocked as `Available` and forward nodes as `Locked`.
- [ ] Hover over `Courtyard Entrance` and verify `CampaignTooltipUI` appears with title, subtitle, rewards preview, and "[Click to Deploy]" prompt.
- [ ] Click `Courtyard Entrance`; verify `LoadingScreenManager` displays the loading screen and deploys into `Bladehold Survivors Scene.unity`.
- [ ] Complete 3 waves in battle; interact with the gate or clear the wave; verify the game advances the node and transitions back to `Bladehold Campaign Map Scene.unity` with Tier 1 marked `Completed` and Tier 2 options (`North Ramparts` & `Armory Barracks`) unlocked as `Available`.

## Castle Campaign Part 2: Smashable Supply Room Feature & Scene

The core components, loot economics, interactive room controller, and procedural indoor scene for Part 2 of the Castle Campaign overhaul are implemented:
- **SupplyBox Destructible Component** (`Assets/Bladehold/Bladehold Scripts/Economy/SupplyBox.cs`):
  - Attached to crates, barrels, and metal chests (`[RequireComponent(typeof(Health))]`).
  - Implements the codebase convention where `Health` is the central hub: listens to `Health.OnDied` without `Health` being aware of loot logic.
  - Smashed by player sword slashes, arrows, thrown axes, or magic missiles.
  - Drop rates & economics:
    - In-Run Gold: 15–40 gold awarded directly to `RunSession.InRunGold`.
    - Fort Supply: 10–25 fort supply added to `RunSession.FortSupply`.
    - Goblin Blood: 40% chance for 1–3 permanent Goblin Blood (`SaveData.goblinBlood`).
    - Orcish Metal: 20% chance for 1–2 permanent Orcish Metal (`SaveData.orcishMetal`).
  - Visual & audio feedback: DamageNumbersPro popups for each dropped currency, smash particle effects, and sound feedback.
  - Sinks smoothly below floor and cleans up gameObject.
- **Supply Room Controller** (`Assets/Bladehold/Bladehold Scripts/Campaign/SupplyRoomController.cs`):
  - Attached to `[SupplyRoomController]` GameObject in `Bladehold Supply Room.unity`.
  - Non-hostile safe room verification: ensures 0 enemy spawners exist in the scene.
  - Player state rehydration: calls `RunSession.RestoreInRunUpgrades(player)` and restores player health on entry.
  - Exit Gate interaction: wires exit gate `Interactable` to trigger `CampaignManager.Instance.CompleteCurrentNodeAndOpenMap()`, seamlessly routing back to `Bladehold Campaign Map Scene.unity`.
  - Tracks total and smashed crate count, firing `OnAllCratesSmashed` when the room is emptied.
- **Supply Room Scene** (`Assets/Bladehold/Bladehold Scenes/Bladehold Supply Room.unity`):
  - Stone castle cellar/storehouse environment: cobblestone floor, stone walls, ceiling rafters, ambient torchlight.
  - 20 varied destructible supply crates, barrels, and reinforced lockboxes arranged in neat storage piles.
  - Complete gameplay rig: `Player.prefab`, `CameraRig.prefab`, `Bladehold HUD.prefab`, `EventSystem.prefab`, and `MMTimeManager.prefab`.
  - Exit Gate with `Interactable` component (`"Return to Campaign Map"`).
  - NavMeshSurface baked and registered with `enabled: 1` in `EditorBuildSettings.asset` and metadata in `AreaDatabase.cs`.

### Wiring & Asset Checklist
- [x] Create `SupplyBox.cs` with currency drop weights, DamageNumbersPro popups, and smooth sink coroutine.
- [x] Create `SupplyRoomController.cs` for safe-room management, upgrade rehydration, and map transition.
- [x] Register `Bladehold Supply Room` in `AreaDatabase.cs` and `EditorBuildSettings.asset`.
- [x] Generate and bake `Assets/Bladehold/Bladehold Scenes/Bladehold Supply Room.unity` with 20 crates, player spawn, and exit gate.
- [ ] (Optional) In Unity Editor, replace procedural wooden crate meshes with custom Synty dungeon prop models if desired.

### Manual Verification
- [ ] Load `Bladehold Supply Room.unity` in Play mode; verify player spawns correctly with camera following and HUD active.
- [ ] Attack crates and barrels with sword swings, arrows, and abilities; verify crates smash, play hit/break particles, and pop up floating text for Gold, Supply, Blood, and Metal.
- [ ] Verify `RunSession.InRunGold`, `RunSession.FortSupply`, `SaveData.goblinBlood`, and `SaveData.orcishMetal` increase appropriately on crate smash.
- [ ] Approach the Exit Gate and press **[E]**; verify `CampaignManager` completes the node and transitions back to `Bladehold Campaign Map Scene.unity`.

## Castle Campaign Part 3: Castle Greybox Levels Generator & Scenes with Tower Slots & Advancing Waves

The automated level generator, 7 distinctive castle encounter scenes, choke point tower defense slots, advancing wave logic, and scene registrations are fully implemented:
- **7 Castle Level Scenes** (`Assets/Bladehold/Bladehold Scenes/`):
  1. `Bladehold Castle Courtyard.unity` (Tier 1 Center: Courtyard Gate, warm afternoon sunlight, 6 TowerPlots, outer perimeter walls).
  2. `Bladehold Castle Ramparts.unity` (Tier 2 Option A: High battlements, windy silver daylight, 5 TowerPlots, watchtower bastions).
  3. `Bladehold Castle Armory.unity` (Tier 2 Option B: Weapons depot, forge embers, 5 TowerPlots, heavy interior columns, weapon caches).
  4. `Bladehold Great Hall.unity` (Tier 4 Center Merge: Grand banquet hall, golden chandeliers, 6 TowerPlots, royal dais, red carpet runner).
  5. `Bladehold Castle Dungeons.unity` (Tier 5 Option A: Prison oubliette, dark teal fog, 5 TowerPlots, subterranean iron cell gates).
  6. `Bladehold Castle Conservatory.unity` (Tier 5 Option B: Castle garden/greenhouse, emerald daylight, 5 TowerPlots, stone planter beds, fountain basin).
  7. `Bladehold Throne Antechamber.unity` (Tier 7: Pre-final battle, obsidian portico, crimson ceremonial braziers, 6 TowerPlots, royal throne double doors).
- **Automated Level Generator Script** (`Assets/Bladehold/Bladehold Scripts/Editor/BuildCastleLevels.cs`):
  - Menu item `Bladehold/Build Castle Levels/Build All 7 Levels` constructs and bakes all 7 scenes.
  - Builds distinct ground mesh geometry with physics colliders, perimeter boundary walls, and themed props.
  - Directional sunlight, fog, and atmospheric point lights tailored to each sector theme.
  - Connects real prefabs: `Player`, `CameraRig`, `Bladehold HUD` (with `BuildWheelUI` and `SupplyUI`), `EventSystem`, `GameMenu`, `PauseMenuCanvas`, `DeathScreen`, `EnemySpawner`, `SurvivorsObjectives`, `GameLoopManager`, `SurvivorsGameManager`, `GameStats`, `DraftUpgradeService`, `EnemyIntroController`, `FortDefenseManager`, `MMTimeManager`, and `FortDefenseSockets`.
  - Places 4 to 6 `TowerPlot.prefab` instances at strategic choke points, wired to `TowerPlotManager` and `FortDefenseManager`.
  - Configures Castle Gate with `Gate`, `Health` (DoorSO, 200 HP), and `Interactable` (`[E] View Campaign Map`).
  - Bakes `NavMeshSurface` over the entire arena floor.
- **Advancing Waves & Wave Progression**:
  - `CampaignManager.DeployToNode` sets `RunSession.CurrentWave` based on node tier: Tier 1 = Wave 1, Tier 2 = Wave 4, Tier 4 = Wave 7, Tier 5 = Wave 10, Tier 7 = Wave 13.
  - `GameLoopManager` handles 3 waves per sector (`wavesPerRound = 3`); clearing wave 3 unlocks the Castle Gate with `[E] View Campaign Map`.
  - Interacting with the gate calls `CampaignManager.Instance.CompleteCurrentNodeAndOpenMap()`, saving progress and returning to `Bladehold Campaign Map Scene.unity`.
- **Registrations**:
  - All 7 scenes registered in `Assets/Bladehold/Bladehold Scripts/UI/RestArea/AreaDatabase.cs` with titles, subtitles, and lore.
  - All 7 scenes registered in `ProjectSettings/EditorBuildSettings.asset` with `enabled: 1`.
  - `CampaignGraphSO.cs` node definitions updated to point to the dedicated castle scenes.

### Wiring & Asset Checklist
- [x] Create `BuildCastleLevels.cs` editor script under `Assets/Bladehold/Bladehold Scripts/Editor/`.
- [x] Register 7 scenes in `AreaDatabase.cs` and `ProjectSettings/EditorBuildSettings.asset`.
- [x] Connect `CampaignGraphSO.cs` node definitions to the new castle scene names.
- [x] Generate all 7 `.unity` scene assets with geometry, lighting, prefabs, plots, gate, and baked NavMesh.
- [ ] In Unity Editor, open each of the 7 scenes to inspect visual atmosphere and lighting bake settings if lightmaps are desired.
- [ ] Verify `TowerPlot` build wheel interaction in Play mode using available Fort Supply.

### Manual Verification
- [ ] Open `Bladehold Campaign Map Scene.unity`, select `Courtyard Entrance`, and click Deploy. Verify it loads into `Bladehold Castle Courtyard.unity` with player, HUD, camera, and tower plots present.
- [ ] Walk up to a `TowerPlot` during the preparation phase; press **[E]** to verify the Build Wheel opens, select a defense (e.g. Arrow Tower or Catapult), and verify construction occurs.
- [ ] Survive waves 1, 2, and 3; verify that clearing wave 3 announces sector liberation, stops enemy spawns, and unlocks the Castle Gate with `[E] View Campaign Map`.
- [ ] Interact with the Castle Gate; verify the player returns to `Bladehold Campaign Map Scene.unity` with Tier 1 completed and Tier 2 (`North Ramparts` & `Castle Armory`) available.
- [ ] Deploy to `North Ramparts` (or `Castle Armory`); verify wave starts at Wave 4 with Tier 2 enemies, and clearing wave 6 unlocks the gate to return to the map.

## Castle Campaign Part 4: Necromancer Revelation Encounter & Boss Fight (Crypt Arena)

The dialogue monologue, branching choice system, two-phase boss controller, skeleton minions AI, and crypt arena scene are fully implemented:
- **Revelation Monologue & Choice System** (`Assets/Bladehold/Bladehold Scripts/Bosses/NecromancerConfrontationUI.cs`):
  - Cinematic dialogue UI presenting the Necromancer's monologue (revealing he orchestrated the goblin siege, forged the player into the ultimate weapon, and demands Katherine's demise).
  - Typewriter dialogue text with gothic formatting and dialogue blip audio.
  - Interactive choice buttons:
    - `[OBEY]` Slay Princess Katherine: Transitions player directly to the Princess Sanctuary (`tier8_princess_boss`).
    - `[DEFY]` Slay the Necromancer: Initiates the Phase 1 boss battle right in the Crypt (`tier8_necromancer_boss`).
- **Two-Phase Necromancer Boss Battle** (`Assets/Bladehold/Bladehold Scripts/Bosses/NecromancerBossController.cs`):
  - **Phase 1: Invulnerable Bubble Shield & Skeleton Army**:
    - Immune to all player attacks via `Health.TryBlockDamage`.
    - Spawns 8–10 Synty Crypt Skeletons (`CryptSkeletonAI.cs`) from dark ritual summoning circles.
    - Tracks active skeleton count and broadcasts remaining count to the UI.
  - **Phase 2: Shield Shatter & Scythe Melee Combat**:
    - Shatters bubble shield with dramatic VFX, SFX, and screen shake upon all skeletons dying.
    - Draws two-handed scythe and charges player aggressively with high movement speed.
    - Performs 180° telegraphed sweeping slashes dealing 38 damage and 8.5m knockback with procedural LineRenderer telegraph arc.
  - **Defeat & Payout**:
    - Awards 150 Gold, 20 Goblin Blood, and 6 Orcish Metal directly to `RunSession` and `SaveData`.
    - Spawns 15 physical gold coins and triggers victory celebration.
- **Crypt Skeleton Minion AI** (`Assets/Bladehold/Bladehold Scripts/Bosses/CryptSkeletonAI.cs`):
  - NavMeshAgent tracking, attack windup, melee swings dealing damage to player.
  - Notifies `NecromancerBossController.OnSkeletonDied` upon death.
- **Crypt Arena Scene & Registrations**:
  - `Assets/Bladehold/Bladehold Scenes/Bladehold Necromancer Crypt.unity`:
    - Colossal vaulted stone crypt with double colonnades of stone pillars, 8 ancient sarcophagi, sacrificial altar, green flame braziers, and baked NavMesh.
    - Pre-wired with `Player`, `CameraRig`, `HUD Canvas` with `BossHealthBarUI` and `NecromancerConfrontationUI`, `EventSystem`, and `MMTimeManager`.
  - Registered in `AreaDatabase.cs` and `ProjectSettings/EditorBuildSettings.asset` (`enabled: 1`).
  - Linked in `CampaignGraphSO.cs` node `tier8_crypt_sanctum` with branching next nodes to `tier8_princess_boss` and `tier8_necromancer_boss`.
- **Automated Behavioral Mechanics Tests**:
  - Section 14 in `WeaponReachBenchmark.cs` verifying Bubble Shield invulnerability, skeleton death shield shatter, Phase 2 vulnerability, AreaDatabase registration, and Campaign Graph branching (all 54/54 tests passing).

### Wiring & Asset Checklist
- [x] Create `NecromancerConfrontationUI.cs` with typewriter text and branching buttons.
- [x] Create `CryptSkeletonAI.cs` and `NecromancerBossController.cs` two-phase state machine.
- [x] Generate and bake `Bladehold Necromancer Crypt.unity` with environment props, NavMesh, boss, player, camera, and dialogue UI.
- [x] Register scene in `EditorBuildSettings.asset`, `AreaDatabase.cs`, and `CampaignGraphSO.cs`.
- [x] Add automated benchmark tests in `WeaponReachBenchmark.cs` Section 14 and verify 100% pass rate.
- [ ] (Optional) In Unity Editor, inspect `Bladehold Necromancer Crypt.unity` to tune custom particle effects on braziers or altar torches.

### Manual Verification
- [ ] Open `Bladehold Necromancer Crypt.unity` in Play mode; verify the Necromancer confrontation dialogue begins immediately with camera focused on the boss.
- [ ] Test `[OBEY]` button: Verify clicking Obey transitions the player toward Princess Katherine's sanctuary (`tier8_princess_boss`).
- [ ] Reload scene and test `[DEFY]` button:
  - Verify the dialogue closes and Phase 1 begins: Bubble shield surrounds the Necromancer and dark summoning circles appear.
  - Attack the Necromancer with weapons: Verify attacks deflect off the bubble shield without lowering his HP.
  - Slay all summoned Crypt Skeletons: Verify the counter updates and upon the last skeleton's death, the shield shatters with audio and VFX.
  - In Phase 2, verify the Necromancer draws his scythe, charges toward the player, telegraphs 180° sweeps, and damages the player upon impact.
  - Defeat the Necromancer: Verify death animation plays, 150 Gold, 20 Blood, and 6 Metal are awarded, and victory is declared.

## Castle Campaign Part 5: Princess Battle Encounter (Royal Sanctuary)

The boss fight mechanics, AI controllers, procedural environment builder, scene generation, and automated benchmark tests for Part 5 of the Castle Campaign overhaul are implemented:
- **Armored Knight AI** (`Assets/Bladehold/Bladehold Scripts/Bosses/ArmoredKnightAI.cs`):
  - Synty low-poly armored knights (`SM_Chr_Hero_Knight_Male_01`) pursuing player with melee sword attacks (22 dmg, 5.5 knockback).
  - **Downed State on 0 HP**:
    - Intercepts lethal damage via `Health.TryPreventDeath`, sets `IsDowned = true`, kneels/falls down, disables collider, and emits a vertical golden soul beacon (`LineRenderer`).
    - Blocks incoming hits via `Health.TryBlockDamage` while downed (untargetable/invulnerable).
    - Revived to 100% max health (180 HP) when Princess completes her channel.
- **Princess Boss Controller** (`Assets/Bladehold/Bladehold Scripts/Bosses/PrincessBossController.cs`):
  - State Machine: `Idle`, `Fleeing`, `MovingToDownedKnight`, `ChannelingRevival`, `Defeated`.
  - Flees away from player while knights fight.
  - When a knight is downed, moves to within 3.5m and begins 5.0-second revival channel.
  - **Hit Delay Penalty Mechanic**:
    - Taking player damage during channeling adds +1.5s delay to the spell timer (`currentChannelTimeRemaining += 1.5f`) and flashes cast bar red with interrupt text.
  - Procedural Visuals: Rotating golden holy magic circle at feet + overhead world-space billboard cast bar with fill meter and timer text.
  - Defeat & Rewards: Slaying the Princess awards Dark Campaign Victory with 500 Gold, 30 Blood, and 10 Metal.
- **Sanctuary Scene & Level Builder** (`Assets/Bladehold/Bladehold Scripts/Editor/BuildPrincessSanctuaryScene.cs`):
  - Automated builder generating `Assets/Bladehold/Bladehold Scenes/Bladehold Princess Sanctuary.unity`.
  - Regal royal throne annex with royal dais, ornate columns, crimson velvet carpets, chandeliers, baked NavMeshSurface, Player, HUD, CameraRig, Boss, and 4 Armored Knights.
  - Registered in `AreaDatabase.cs` ("Princess Sanctuary", "The Royal Throne Annex") and `EditorBuildSettings.asset` (`enabled: 1`).
  - Linked to `CampaignGraphSO.cs` node `tier8_princess_boss`.
- **Automated Behavioral Mechanics Tests**:
  - Section 15 in `WeaponReachBenchmark.cs` testing knight downed state, 5.0s channel initiation, +1.5s player hit penalties (stacking to 6.5s and 8.0s), full 180 HP revival upon timer completion, AreaDatabase metadata, and Campaign Graph rewards (all 61/61 tests passing).

### Wiring & Asset Checklist
- [x] Create `ArmoredKnightAI.cs` with downed state, soul beacon, and revive logic.
- [x] Create `PrincessBossController.cs` with fleeing AI, +1.5s hit interrupt penalty, rotating magic circle, and cast bar.
- [x] Create `BuildPrincessSanctuaryScene.cs` and generate `Bladehold Princess Sanctuary.unity` with baked NavMesh.
- [x] Register scene in `EditorBuildSettings.asset`, `AreaDatabase.cs`, and `CampaignGraphSO.cs`.
- [x] Add automated benchmark tests in `WeaponReachBenchmark.cs` Section 15 and verify 100% pass rate (61/61 passed).
- [ ] (Optional) In Unity Editor, inspect `Bladehold Princess Sanctuary.unity` to assign custom audio clips (holy choir loop, sword clashes) or Synty low-poly knight prefabs (`SM_Chr_Hero_Knight_Male_01`).

### Manual Verification
- [ ] Load `Bladehold Princess Sanctuary.unity` in Play mode; verify player spawns facing Princess Katherine and 4 Armored Knights.
- [ ] Observe Princess fleeing away from player while knights advance and engage in melee combat.
- [ ] Attack and defeat an Armored Knight:
  - Verify knight drops into kneeling downed state at 0 HP with golden soul beacon beaming into the sky.
  - Verify downed knight cannot be damaged or targeted.
- [ ] Observe Princess pathfinding to the downed knight:
  - Verify rotating golden magic circle appears at her feet and overhead cast bar displays "REVIVING: 5.0s".
- [ ] Strike Princess with player attacks while channeling:
  - Verify each strike adds +1.5s to the timer (e.g. 5.0s -> 6.5s -> 8.0s) with flash feedback.
- [ ] Allow timer to expire:
  - Verify downed knight revives to 100% HP (180 HP) and resumes fighting.
- [ ] Slay Princess Katherine:
  - Verify boss defeat sequence triggers, knights collapse, 500 Gold, 30 Blood, and 10 Metal are awarded, and victory UI appears.

## Mount System & Meta Area Mount Pedestals — Wiring & Verification

The C# implementation, mount ScriptableObjects, SaveData support, sword ultimate replacement (`Blade Tempest`), and auto-setup methods are implemented.
- **Mount System** (`Assets/Bladehold/Bladehold Scripts/Horse/MountDefinitionSO.cs`, `PlayerMount.cs`, `MountStatusUI.cs`):
  - Mount is no longer an ultimate; players can mount up at any time with **[X]** with an interruptible cast time (1.0s–1.8s depending on mount).
  - Mounts have a fixed duration (20s–40s) and cooldown (60s–100s).
  - Player starts with `basic_horse` unlocked and equipped in `SaveData`.
  - Horse archery is unlocked from the get-go across all loadouts (`StatType.HorseArcheryUnlocked` base 1.0).
- **Sword Signature Ultimate** (`Assets/Bladehold/Bladehold Scripts/Player/SwordBladeTempestUltimate.cs`):
  - Replaced the old Warhorse ultimate for sword wielders with **Blade Tempest** (`sword_blade_tempest`), a 360-degree slicing flurry with 30% damage reduction.
- **Mount Variations & Meta Area Pedestals** (`Assets/Bladehold/Bladehold Scripts/UI/Meta/MountPedestal.cs`, `SetupGameLoopAssets.SetupMetaAreaScene`):
  - 6 `MountDefinitionSO` assets created in `Assets/Bladehold/Resources/Mounts/`:
    1. `Mount_BasicWarhorse` (Default / 0 Metal, Rotten mat, 1.0x scale)
    2. `Mount_FrostStrider` (5 Metal, ICE mat, 0.95x scale, high speed)
    3. `Mount_InfernalSteed` (10 Metal, Fire mat, 1.05x scale, high damage)
    4. `Mount_AbyssalBehemoth` (10 Metal, Demon mat, 1.10x scale, high HP/knockback/duration)
    5. `Mount_PhantomCharger` (15 Metal, Ghost mat, 1.0x scale, highest speed, short cd)
    6. `Mount_CelestialDreadnought` (15 Metal, White Gold mat, 1.08x scale, high power/armor)
  - Pedestals automatically spawned/wired via `SetupGameLoopAssets.SetupMetaAreaScene()` (or in Editor).

### Wiring & Asset Checklist
- [x] Create `MountDefinitionSO` assets in `Assets/Bladehold/Resources/Mounts/`
- [x] Wire `SetupGameLoopAssets.cs` to generate mount pedestals in `Bladehold Meta Area Scene.unity`
- [ ] In Unity Editor, run `SetupGameLoopAssets.SetupMetaAreaScene()` (or open `Bladehold Meta Area Scene.unity`) to verify the 5 mount pedestals are placed neatly alongside the weapon pedestals.
- [ ] Verify `HUD Canvas` has `MountStatusUI` attached to display the summon cast bar, remaining mount duration, and remaining cooldown.

### Manual Verification
- [ ] **Horse Archery From Start**: Enter `Bladehold Survivors Scene.unity` in Play mode with a bow, mount up, and verify shooting arrows from horseback works immediately without purchasing any skill nodes.
- [ ] **Mount On Demand [X]**: Press **[X]** while unmounted. Verify the cast bar appears and takes the configured cast time (e.g. 1.5s for basic mount). Verify moving during cast interrupts it.
- [ ] **Mount Duration & Cooldown**: Verify after mounting that the mount timer counts down (30s) and automatically dismounts when expired. Verify the cooldown timer counts down (90s) before [X] can be used again.
- [ ] **Meta Area Mount Pedestals**: Open `Bladehold Meta Area Scene.unity`, approach a mount pedestal (e.g. Frost Strider or Infernal Steed). Verify prompt shows cost in Orcish Metal, and purchasing unlocks and equips the mount.
- [ ] **Sword Ultimate Blade Tempest**: Play with Sword, charge ultimate to 100%, activate it, and verify the spinning blade tempest flurry damages surrounding enemies.

## Powder Keg & Bannerman Enemies — Unity Editor Wiring & Verification

The C# implementation, animator controllers, highlight profiles, Enemies.csv rows, and prefab variants are generated and registered in `EnemyPrefabMap.asset` via `EnemyPrefabGenerator.GenerateAll`.
- **Powder Keg** (`Assets/Bladehold/Bladehold Scripts/Enemies/PowderKeg/PowderKegAttack.cs`, `PowderKegBarrel.cs`, `PowderKegAttackSO.cs`): Slow-moving enemy holding an explosive barrel overhead (`HoldBarrel` animation state on masked upper body layer). When hit by an arrow/projectile on the barrel (`PowderKegBarrel` implements `IDamageable`), or upon reaching within 2m of Castle Gate (`AITargetSelector`), the barrel triggers a 25 damage AoE explosion (`LayerMask.GetMask("Enemies", "Player")`) and self-destructs.
- **Bannerman** (`Assets/Bladehold/Bladehold Scripts/Enemies/Bannerman/BannermanAura.cs`, `DestructibleBanner.cs`, `BannermanAuraSO.cs`): Carries a banner overhead granting proximity buffs to nearby enemies based on active wave banner buff (Damage Buff -> Red Glow, Healing Buff -> Green Glow, Shield Buff -> Yellow Glow via `HighlightEffect` profiles). The banner can be shot and destroyed independently (`DestructibleBanner` with 25 HP / collider); destroying the banner or killing the Bannerman disables the buff aura. `SurvivorsSpawner.cs` localizes banner buffs to Bannerman auras so distant enemies don't get the buff.

### Wiring & Asset Checklist
- [x] Create Highlight Profile assets:
  - `Assets/Bladehold/Bladehold Highlight Profiles/Banner Damage Buff HPP.asset`
  - `Assets/Bladehold/Bladehold Highlight Profiles/Banner Healing Buff HPP.asset`
  - `Assets/Bladehold/Bladehold Highlight Profiles/Banner Shield Buff HPP.asset`
- [x] Create Animator Controllers:
  - `Assets/Bladehold/Bladehold Animations/PowderKeg.controller` (Upper Body layer with `Upper Body Mask.mask`, `HoldBarrel`, `SlamBarrel`, `Slam` trigger)
  - `Assets/Bladehold/Bladehold Animations/Bannerman.controller` (Based on `SimplifiedEnemyAC.controller`)
- [x] Register rows in `Assets/Bladehold/Config/Enemies.csv`: `powder_keg` and `bannerman`.
- [x] Create ScriptableObjects: `PowderKegAttackSO.asset` and `BannermanAuraSO.asset`.
- [x] Generate Prefab variants: `Assets/Bladehold/Bladehold Prefabs/Powder Keg Enemy Variant.prefab` and `Assets/Bladehold/Bladehold Prefabs/Bannerman Enemy Variant.prefab` via `EnemyPrefabGenerator.GenerateAll`.
- [ ] Animator Pose Refinement (Optional Polish):
  - In `PowderKeg.controller`, adjust the `HoldBarrel` state motion/pose on the `Upper Body` layer if a custom keyframed pose holding arms aloft is desired.
  - Set `SlamBarrel` state animation clip to keyframe the barrel slamming onto the ground.
- [ ] Visual FX Polish:
  - If desired, adjust color parameters or rim glow intensities on the 3 `Banner * Buff HPP.asset` Highlight Profile assets.

### Manual Verification (Powder Keg & Bannerman)
- [ ] Load `Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity` in Play mode.
- [ ] **Powder Keg - Gate Detonation**:
  - Allow a Powder Keg enemy to approach within 2m of the Castle Gate (`GateTarget` / `AITargetSelector`).
  - Verify Powder Keg plays the slam trigger and detonates, damaging gate/nearby units for 25 AoE damage.
- [ ] **Powder Keg - Arrow Detonation**:
  - Aim bow/arrows at a Powder Keg carrying the barrel.
  - Shoot the barrel directly.
  - Verify the barrel detonates immediately in mid-transit, damaging all nearby enemies and eliminating the Powder Keg.
- [ ] **Powder Keg - Negative Cases**:
  - Verify hitting the Powder Keg's legs/body with a sword melee strike damages the goblin directly without immediately detonating the barrel unless the barrel itself takes lethal splash damage.
- [ ] **Bannerman - Proximity Buffing**:
  - When a wave buff is active (e.g. Damage, Healing, Shield), observe enemies near the Bannerman.
  - Verify nearby enemies gain the corresponding glow (Red for Damage, Green for Healing, Yellow for Shield).
  - Verify enemies far from the Bannerman do NOT receive the buff glow or stat multipliers.
- [ ] **Bannerman - Banner Destruction**:
  - Shoot the banner carried above the Bannerman's head with arrows.
  - Verify the banner takes damage (25 HP) and is destroyed/unparented/hidden.
  - Verify destroying the banner immediately removes the aura buff from all nearby enemies, even if the Bannerman is still alive.
- [ ] **Bannerman - Unit Death**:
  - Kill the Bannerman directly; verify buff aura clears from all nearby allies upon death.


## Heavy War Mace (2H Melee Weapon) Wiring & Verification

C# implementation, asset definitions, and scene wiring are complete! The Mace is wired as Slot 2 in `Player.prefab` (`PlayerWeaponManager.meleeWeapons`), features blunt staggering, armor-shattering, and ground shockwave mechanics, includes 5 draft cards, and has a dedicated unlock pedestal in `Bladehold Meta Area Scene.unity`.

- [x] Create `WeaponDefinitionSO` (`Assets/Bladehold/Bladehold Config/Weapons/mace.asset`) with 10 Metal cost and Melee category.
- [x] Configure `2H_Mace` under `prop_r` socket on `Player.prefab` with `MeshFilter`, `MeshRenderer`, `MeshCollider`, `DamageTrigger`, `SwordHitFeedback`, `HitstopFeedback`, and `AudioSource`.
- [x] Wire `2H_Mace` into `PlayerWeaponManager.meleeWeapons[2]` on `Player.prefab`.
- [x] Add `MaceCombatController` and `MaceUltimate` components to `Player.prefab`.
- [x] Create `Pedestal_Mace` in `Bladehold Meta Area Scene.unity` at `(-10, 0, 0)` with floating/rotating mace visual and unlock interaction.
- [x] Automated in-editor integration tests in `SetupGameLoopAssets.RunFullGameLoopIntegrationTest()` passing.

Manual verification:
- [ ] Load into `Bladehold Meta Area Scene.unity` in Play Mode.
- [ ] Walk up to the Mace pedestal, press `[E]` to unlock with 10 Orcish Metal, and press `[E]` again to equip.
- [ ] Enter the Battle Portal into `Bladehold Survivors Scene.unity`.
- [ ] Verify light attacks trigger 2H swings with concussive stagger, and charged heavy attacks unleash rock shockwaves.
- [ ] Trigger Ultimate (`F` / North Gamepad button) to unleash the Seismic Quake radial ground slam.

## Rest Area Multi-Door Exit & Meta Loading Screen Wiring

C# implementation is complete! Multiple doors in the Rest Area can now lead to different scenes/stages with contextual HUD prompts, state persistence (`RunSession`), and an atmospheric loading screen displaying `"Entering [Area Name]"` with subtitles, lore, and progress bars.

### 1. (Optional) Create AreaDefinitionSO Assets
- [ ] In the Project window, right-click in `Assets/Bladehold/Config/` (or any subfolder) > **Create > Scriptable Objects > Bladehold > Area Definition**.
- [ ] Configure the asset:
  - **Scene Name**: Target Unity scene (e.g. `Bladehold Survivors Scene`).
  - **Stage Number**: Stage 1-5.
  - **Display Name**: Player-facing name (e.g. `Bladehold Fortress`, `Outer Ramparts`).
  - **Subtitle**: Subtitle (e.g. `The Inner Gate`, `Perimeter Defense`).
  - **Description**: Lore blurb or tips to show while loading.
  - **Preview Sprite**: (Optional) Art or screenshot.
  - **Door Prompt Format**: Default is `"Enter {0}"` (renders as `"Enter Outer Ramparts"`).

### 2. Configure Exit Doors in Bladehold Rest Area Scene
- [ ] Open `Assets/Bladehold/Bladehold Scenes/Bladehold Rest Area Scene.unity`.
- [ ] Locate the existing `Station_4_ExitGate` or create duplicate door GameObjects for each exit path:
  - Ensure each door has a Collider (e.g. Box Collider) set up for interaction.
  - Ensure `Interactable` is attached.
  - Attach `RestAreaDoor` (or keep `RestAreaGate` on the default exit):
    - Assign an `AreaDefinitionSO` OR fill out the inspector fields directly (`Target Scene Name`, `Target Display Name`, `Target Subtitle`, etc.).
    - (Optional) Set `Is Locked` or `Required Stage Unlocked` if this door requires progression.
- [ ] Save the scene.

### 3. (Optional) Custom Loading Screen Prefab
- [ ] If you'd like to use a custom-styled Canvas instead of the built-in automatic fallback:
  - Take the existing `LoadingScreen` GameObject from `Assets/Bladehold/Bladehold Scenes/MainMenu.unity` and save it as a Prefab under `Assets/Bladehold/Bladehold Prefabs/UI/LoadingScreen.prefab`.
  - Attach `LoadingScreenUI` to its root.
  - Wire its references (`logoLoadingFill`, `loadingBar`, `loadingText`, `enteringTitleText`, `subtitleText`, `descriptionText`, `previewImage`, `canvasGroup`).
  - Drop this prefab into the `loadingScreenPrefab` slot on a `LoadingScreenManager` GameObject in the scene (or let `LoadingScreenManager` load it dynamically).

Manual verification:
- [ ] Enter Play Mode in `Bladehold Rest Area Scene`.
- [ ] Walk up to each door; verify the HUD prompt displays `[E] Enter [Area Name]` (or custom text).
- [ ] Press `[E]` to enter.
- [ ] Verify the loading screen displays `"Entering [Area Name]"` with subtitle, description, and smooth progress fill.
- [ ] Verify the target scene loads and player stats/upgrades are intact.

## Loadout System & Armour Sets Wiring

C# refactoring is complete! The old `PlayerClassController` is gone, and the player now uses a mix-and-match Loadout system (`PlayerWeaponManager`) and an Armour Set system (`PlayerArmourManager`).

- [x] Open `Assets/Bladehold/Bladehold Prefabs/Player.prefab`
- [x] On the `PlayerArmourManager` component:
  - Create a new `ArmourSetSO` asset in `Assets/Bladehold/Config/Armour Sets` (e.g., `HeroArmourSet.asset`).
  - Assign the player's 3D model prefab (e.g. `SidekickSyntyCharacter`) to its `characterModelPrefab`.
  - Add this `ArmourSetSO` to the `Available Armour Sets` array on the `PlayerArmourManager` component.
- [x] On the `PlayerWeaponManager` component:
  - **Melee Weapons Array**: Add an element for the Sword. Assign the `WeaponDefinitionSO` (`sword`), assign its child `1H_Sword` object to `weaponObject`, and drag its `DamageTrigger` onto `damageTrigger`. Add another element for the Axe and do the same.
  - **Ranged Weapons Array**: Add an element for the Bow. Assign the `WeaponDefinitionSO` (`bow`), assign `Wep_RecurveBow_01` (or whichever parent contains the bow visuals) to `weaponObject`, and drag the `PlayerBow` component onto `aimWeaponComponent`. Do the same for Throwing Axe, dragging the `PlayerThrownAxe` component.
- [x] Save the prefab.

Manual verification:
- [ ] Enter Play Mode.
- [ ] Verify you start with the Sword and Bow equipped.
- [ ] Verify left-click performs melee attacks, and right-click aims the bow.
- [ ] Verify the animations blend correctly and don't get stuck.

## Cinematic War Banners Overhaul Wiring

- [x] WarBanner Prefab Wiring (Automated via AutoWiringScript)
- [x] Intermission Virtual Camera (Automated via AutoWiringScript)
- [x] Intermission Stats Panel (Automated via AutoWiringScript)

Manual verification:
- [ ] Enter Play Mode.
- [ ] Finish Wave 1.
- [ ] Verify the camera pans to the Gate area and time slows down briefly.
- [ ] Verify the 3 banners fall from the sky staggered by 0.4s, shaking the screen and playing audio (MMF).
- [ ] Verify the banners glow in their respective clan colors (Highlight Plus).
- [ ] Tear down a banner: Verify it plays the burn/dissolve sequence for 3 seconds while the other two shrink and disappear.
- [ ] Verify the camera transitions back to the player and the next wave starts.

## Elemental System
- [ ] **Discord Ring VFX:** \EnemyStatusManager.cs\ currently spawns a primitive Sphere as a placeholder for the Discord synergy visual. Needs to be replaced with a proper particle system or ring mesh.
- [ ] **Chain Lightning VFX:** The Conductive status in \EnemyStatusManager.cs\ deals invisible damage in an overlap sphere. Needs a Line Renderer or VFX Graph to visually arc to targets.
- [ ] **Status Particles:** Need particle systems for Ignited (burning flames), Chilled (frost aura), and Frozen (ice block) on enemies.

# Golden Goblin Objective Wiring

- [ ] Attach GoldenGoblinObjective to the Objectives GameObject (or whichever manager holds the objective components in the Bladehold Survivors Scene.unity).
- [ ] In the GoldenGoblinObjective component:
  - Assign the Golden Goblin prefab to Golden Goblin Prefab.
  - Assign the Coin prefab to Coin Prefab.
  - Create a few empty GameObjects in a circle around the arena (as waypoints) and assign them to the Waypoints array.
- [ ] In the SurvivorsObjectiveManager component on the same GameObject, add the GoldenGoblinObjective to the repeatingObjectiveComponents list so it gets selected randomly as a wave objective.

Manual verification:
- [ ] Run the game and trigger the Golden Goblin objective.
- [ ] Verify that no other enemies spawn.
- [ ] Verify the goblin runs in circles along the waypoints.
- [ ] Verify hitting him drops gold periodically, and killing him gives a bonus and ends the wave.

## Enraged Captains & War Banner Difficulty Tiers Wiring & Verification

The C# implementation for the War Banner difficulty tier system and Clan Captains is complete! War Banners roll difficulty tiers (Standard 1💀, Enraged 2💀, Nightmare 3💀, Omega 4💀) providing 1x, 2x, 4x, and 8x reward multipliers. Tearing down an Enraged or higher banner summons a dedicated Clan Captain (e.g. Captain Fraglob) with custom abilities, accompanied by a cinematic announcement displaying difficulty skulls.

### Wiring & Asset Checklist
- [ ] **Assign Captain Prefab (Goblin Sidekick)**:
  - In `Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity`, select `GameLoopManager`.
  - In the Inspector under **Captain Settings**, assign your customized Goblin Sidekick prefab to the `Captain Prefab` field (or leave null to use the built-in scaled Brute placeholder).
  - Ensure the prefab has `CaptainEnemyController`, `Health`, `AIMovement`, and `AIAttack` attached.
- [ ] **(Optional) War Banner Prefab UI Wiring**:
  - In `Assets/Bladehold/Bladehold Prefabs/WarBanner.prefab`, check if you want dedicated TextMeshPro components wired to:
    - `Difficulty Skulls Text`: Shows `💀 💀`
    - `Difficulty Tag Text`: Shows `ENRAGED [2x REWARDS]`
    - Note: If unassigned, the difficulty tier and multiplier are automatically included in the prompt text (`[E] Tear Down Banner...`).

### Manual Verification Checklist
- [ ] Load `Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity` in Play mode.
- [ ] Clear Wave 1 to trigger the War Banner selection intermission.
- [ ] On Run 2+ (or higher rounds), observe that at least one banner rolls **ENRAGED (2💀)** with an Amber glow and 2x reward indicator.
- [ ] Tear down the Enraged banner with `[E]`:
  - Verify the cinematic announcement displays: `"CAPTAIN FRAGLOB HAS ARRIVED!"` with skulls `💀 💀` underneath.
  - Verify Captain Fraglob spawns and leads the wave.
  - Test **Rallying War Cry**: Captain roars, buffing nearby minions with speed and attack power.
  - Test **Seismic Stomp**: Captain telegraphs a ground circle and stomps, knocking back the player.
  - Slay Captain Fraglob: Verify nearby enemies are staggered for 2 seconds (Morale Break) and bonus Gold/Blood drops.
- [ ] Complete the wave:
  - Collect the bounty powerup and verify the reward amount is doubled (e.g. `+150 Gold (2x)` or bonus draft rerolls).
