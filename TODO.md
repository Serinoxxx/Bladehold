# Bladehold Unity Editor Wiring

## Assassin Enemy Type — Unity Editor Wiring

### The C# is done
Implemented the new **Assassin** enemy type with a 3-phase attack cycle:
- **`AssassinAttackSO.cs` & `AssassinAttackSO.asset`**: ScriptableObject configuration defining `triggerRange=3.5`, `spinRadius=3.0`, `windupSeconds=1.0`, `spinDuration=2.0`, `spinHits=5`, `damagePerHit=5`, `stunDuration=4.0`, and `attackCooldown=3.0`.
- **`AssassinAttack.cs`**: Attack component handling the wind-up telegraph display (`SlamTelegraph.prefab`), 5-tick whirlwind spin damage AoE with slash audio (`SwordOnFlesh_100.wav`) and Synty particle VFX (`FX_Swirl_Fast_01.prefab`), followed by a 4.0-second dizzy/stunned state with overhead star VFX (`FX_StarStunned_01.prefab`), before resuming movement pursuit.
- **`WaveSpawner.cs`**: Added `AssassinAttack?.SetDamage(def.damage.Value)` routing in `ApplyDefinitionInternal`.
- **`EnemyManifest.cs`**: Added declarative `EnemySpec` for `assassin` with automated prefab variant generation and asset wiring.
- **`Enemies.csv`**: Registered row `assassin,Assassin,25,5,10,20,4.5,1,3,20,1,2,2,TRUE` (unlocks at Wave 3, max concurrency 2).

### Wiring checklist
- [x] **ScriptableObject Assets**:
  - [x] `AssassinAttackSO.asset` generated at `Assets/Bladehold/Bladehold Scripts/Enemies/Assassin/AssassinAttackSO.asset`.
- [x] **Prefab Generation & Wiring**:
  - [x] `Assassin Enemy Variant.prefab` generated under `Assets/Bladehold/Bladehold Prefabs/`.
  - [x] Wired `attackData` (`AssassinAttackSO.asset`), `telegraphPrefab` (`SlamTelegraph.prefab`), `whirlwindVfxPrefab` (`FX_Swirl_Fast_01.prefab`), `stunVfxPrefab` (`FX_StarStunned_01.prefab`), and `slashAudioClip` (`SwordOnFlesh_100.wav`).
  - [x] Registered `assassin` -> `Assassin Enemy Variant.prefab` in `EnemyPrefabMap.asset`.
- [x] **Wave Spawner & Roster**:
  - [x] Added `assassin` row to `Assets/Bladehold/Config/Enemies.csv`.

### Manual verification
- [x] Headless C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed via `refresh_unity` (0 errors).
- [x] In-Editor Mechanic Integration Test passed:
  - [x] Prefab instantiation and dependency validation passed.
  - [x] `WaveSpawner.ApplyDefinition` CSV override verification passed (25 HP, 5 DMG, 4.5 Speed).
  - [x] Single pulse damage (5 dmg) and 5-pulse total damage (25 dmg) verified.
- [ ] In-game Play mode testing:
  - [ ] Start run and advance to Wave 3 -> verify Assassins begin spawning with a maximum of 2 alive at any time.
  - [ ] Approach Assassin -> verify red circular telegraph appears during 1.0s wind-up while enemy pauses movement.
  - [ ] Whirlwind spin phase -> verify 5 rapid damage ticks over 2.0s with slash audio and whirlwind VFX while stationary.
  - [ ] Dizzy/stun phase -> verify Assassin remains stunned for 4.0s with spinning star VFX over head.
  - [ ] Recovery -> verify Assassin resumes chasing the player after stun ends.
  - [ ] Killing Assassin mid-windup or mid-spin -> verify all telegraphs and particle VFX clean up immediately and corpse despawns normally.

## Meta-Progression System, Periodic Elemental Imbuements, and In-Run Card Drafting Overhaul

### The C# is done
Implemented persistent meta-progression in the Main Menu, in-run card draft overhaul, 4 periodic elemental imbuements, active weapon limit, and card banish mechanics:
- **`StatType.cs` & `StatDisplay.cs`**: Added periodic stats (`PeriodicFire*`, `PeriodicIce*`, `PeriodicLightning*`, `PeriodicImpulse*`) and UI presentation table formatters.
- **`SkillNode.cs` & `SkillTreeSO.cs`**: Added `isMeta`, `isCard`, and `isActiveWeapon` flags with dynamic header-mapped CSV parsing.
- **`SkillTree*.csv`**: Updated config with the 3 classification flags, removed deprecated drop nodes, made Parry/Counter passive, made Multi-shot and Extended Blade in-game cards, and added the 4 Periodic Imbuement skills.
- **`PeriodicImbuementController.cs`**: Scene/Player component managing periodic cycling and on-hit procs for Fire (AoE explosions), Ice (slowing chill & freeze), Lightning (chain lightning), and Impulse (kinetic knockback fling).
- **`SkillTreeService.cs`**: Separated `metaLevels` (persisted to disk SaveData) from `runLevels` (in-run temporary draft choices), ensuring in-run upgrades never write to disk save files.
- **`SurvivorsLevelSystem.cs` & `Coin.cs` & `SurvivorsHUDUI.cs`**: Decoupled in-run XP from persistent Gold, showing in-run progress as XP while Gold is collected for Main Menu meta upgrades.
- **`SurvivorsCardSelector.cs`**: Filtered by `isCard`, enforced 4 active weapons slot cap, and added banish filter.
- **`SurvivorsCardUI.cs` & `SurvivorsCardSelectUI.cs`**: Added 1-per-draft Banish button that removes the card for the run and rolls a replacement immediately.
- **`MetaProgressionGridUI.cs` & `MainMenuManager.cs`**: Built Main Menu Upgrades screen showing a grid of all permanent meta skills, gold counter, and purchase buttons.

### Wiring checklist
- [x] **Main Menu Upgrades Screen (`MainMenu.unity`)**:
  - [x] Added `Button_Upgrades` to `Screen_Title/Buttons` with persistent onClick listener to `MainMenuManager.OnUpgradesClicked()`.
  - [x] Created `Screen_Upgrades` with header title, gold counter, scrollable grid content, and Back button.
  - [x] Attached `MetaProgressionGridUI` and wired all references (`skillTree`, `goldText`, `gridContent`, `backButton`, `mainMenuManager`).
  - [x] Wired `MainMenuManager.upgradesScreen` to `Screen_Upgrades`.
- [x] **Survivors Card Draft Modal (`Bladehold Survivors Scene.unity`)**:
  - [x] Added `Banish_Btn` to all 3 cards in `CardsRow` of `SurvivorsCardSelectModal`.
  - [x] Auto-wired `banishButton` references on each `SurvivorsCardUI`.
- [x] **Skill Tree Config & Agent Skills**:
  - [x] Updated `SkillTree.csv`, `SkillTreeBerserker.csv`, and `SkillTreeMage.csv`.
  - [x] Updated `add-skill-line/SKILL.md` and `AGENTS.md` to enforce human definition of `isMeta`, `isCard`, and `isActiveWeapon`.

### Manual verification
- [x] Headless C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed via `refresh_unity` (0 console errors).
- [ ] Playtest Main Menu: Click "UPGRADES" -> verify grid of permanent upgrades appears with gold cost and level badges.
- [ ] Playtest Main Menu: Purchase an upgrade -> verify gold deducts, level increments, and save persists on reload.
- [ ] Playtest Survivors Mode: Verify starting run does not carry temporary card choices from previous runs.
- [ ] Playtest Survivors Mode: Level up -> verify 3 draft cards appear with "BANISH" button.
- [ ] Playtest Survivors Mode: Click "BANISH" on a card -> verify card is removed from the run pool, a replacement card rolls immediately, and banish buttons disable for the rest of that draft.
- [ ] Playtest Survivors Mode: Acquire Periodic Imbuement (Fire/Ice/Lightning/Impulse) -> verify elemental visual cycling and on-hit procs trigger during combat.

## Slayer Gate-Assault Objective, Generic Enemy Intro Cinematic, Boss Health Bar & Damage Retaliation — Unity Editor Wiring

### The C# is done
Implemented Slayer Gate-Assault objective, cinematic triggers, boss health bar, and retaliatory mechanics.

## Berserker Whirlwind Ultimate — Unity Editor Wiring

### The C# is done
Overhauled Berserker's Ultimate ability into a continuous whirlwind attack:
- **`DamageTrigger.cs`**: Added continuous whirlwind activation mode (`StartWhirlwind`, `StopWhirlwind`), per-target hit cooldown tracking (`whirlwindHitInterval`, defaulting to 0.3s), and fully charged damage scaling (`FullyChargedDamageMultiplier`, charge knockback bonus) without triggering Earth Splitter. Bypasses single-swing hit cap (`MaxHitsPerSwing`).
- **`PlayerAttack.cs`**: Added `FullyChargedDamageMultiplier` property and suppressed regular melee attack inputs while whirlwind is active.
- **`BerserkerUltimate.cs`**: Implemented `IUltimateHandler` with `StartWhirlwind` activation on the equipped melee weapon (2H Axe), `StartWhirlwind` / `StopWhirlwind` Animator trigger triggers for the user's override layer, programmatic 360° spin rotation (`rotateCharacter = true`, `spinDegreesPerSecond = 1080f` on the rig's `root` bone for static animation poses), `FX_Swirl_Fast_01` whirlwind VFX spawning (matching Assassin enemy variant), and damage reduction scaling via `UltimateBerserkerDamageReduction`. Giant growth/scale-up mechanics have been completely disabled.
- **`SkillTreeBerserker.csv` & `SkillTreeSOBerserker.asset`**: Renamed ultimate node to `Whirlwind` and duration to `Enduring Whirlwind`. Removed legacy `ult_size` (`Colossal Growth`) node completely.
- **`Player.prefab`**: Auto-wired `whirlwindVfxPrefab` to `Assets/Synty/PolygonParticleFX/Prefabs/FX_Swirl_Fast_01.prefab`, with `startTrigger = "StartWhirlwind"`, `stopTrigger = "StopWhirlwind"`, `hitInterval = 0.3f`, `rotateCharacter = true`, `spinDegreesPerSecond = 1080`, and `spinTransform = root`.

### Wiring checklist
- [x] **Prefab Wiring (`Player.prefab`)**:
  - [x] Wired `whirlwindVfxPrefab` to `Assets/Synty/PolygonParticleFX/Prefabs/FX_Swirl_Fast_01.prefab`.
  - [x] Configured `startTrigger` = `"StartWhirlwind"` and `stopTrigger` = `"StopWhirlwind"`.
  - [x] Configured `rotateCharacter = true`, `spinDegreesPerSecond = 1080`, and `spinTransform = root`.
- [ ] **Animator Setup (User)**:
  - [ ] Add an Override layer in the Berserker/Player Animator Controller with Weight = 1.
  - [ ] Add the spinning animation clip to this layer.
  - [ ] Create transitions driven by triggers `StartWhirlwind` to enter and `StopWhirlwind` to exit.

### Manual verification
- [x] Headless C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed via `refresh_unity` (0 errors).
- [ ] In-game Play mode testing:
  - [ ] Fill Berserker Ultimate charge (or activate via cheats/hotkey).
  - [ ] Trigger Ultimate: verify `StartWhirlwind` fires on Animator, `FX_Swirl_Fast_01` spawns and loops at player feet/torso.
  - [ ] Verify 2H Axe stays active with weapon trail and cuts through all enemies continuously.
  - [ ] Verify hits deal fully charged weapon damage and knockback, and Earth Splitter line of explosions does not trigger.
  - [ ] Verify Ultimate ends cleanly after duration: `StopWhirlwind` fires on Animator, weapon deactivates, and particle swirl despawns.
