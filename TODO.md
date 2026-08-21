# Bladehold HUD Setup

## Damage Direction Indicator UI — Unity Editor & Scene Wiring

### The C# and Scene Wiring is done
Implemented `DamageDirectionUI.cs` on `FX_FantasyWarrior_Damage_Direction_02` in `Bladehold Test Scene.unity`:
- **`DamageDirectionUI.cs`**: Subscribes to `Health.OnDamaged` on the player. When damage is received, calculates the direction vector from the player to the attacker, projects it relative to the camera's view (or player facing), computes the corresponding Z-axis rotation angle (`-Mathf.Atan2(rightDot, fwdDot) * Mathf.Rad2Deg`), updates the `RectTransform.localRotation`, and fires the `"Hit"` trigger on the component's `Animator`.

### Wiring checklist
- [x] Create `DamageDirectionUI.cs` script under `Assets/Bladehold/Bladehold Scripts/UI/`.
- [x] Attach `DamageDirectionUI` component to `FX_FantasyWarrior_Damage_Direction_02` GameObject under `Bladehold HUD` in `Bladehold Test Scene.unity` via Unity MCP.
- [x] Verify `hitTrigger` ("Hit"), `cameraRelative` (true), `animator`, and `rectTransform` auto-wiring.

### Manual verification (Damage Direction Indicator)
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed and script compilation verified via Unity MCP (`refresh_unity`).
- [ ] Playtest: Enter Play mode in `Bladehold Test Scene.unity`.
- [ ] Playtest: Allow a goblin to attack the player from different angles (front, back, left, right).
- [ ] Playtest: Verify that taking damage triggers the "Hit" UI animation and rotates the red damage indicator UI on the Z axis towards the attacker.



## Arrow Wall Pinning — Unity Editor & Asset Wiring

### The C# is done
Implemented arrow wall pinning when lethal arrow hits deal sufficient knockback force:
- **`Damageable.cs`**: Added `direction`, `hitCollider`, and `canPinToWall` fields to `Damage`.
- **`PlayerBow.cs`**: Set `damage.direction`, `damage.hitCollider`, and `damage.canPinToWall = true` on arrow hit. Added charge-scaled knockback force (`chargeLevel * 2.5f`).
- **`EnemyRagdoll.cs`**: Added `GetBoneRigidbody(hitCollider, hitPoint)` to map hit colliders to exact ragdoll bone rigidbodies and exposed `Config` property.
- **`KnockbackConfigSO.cs`**: Added tunables for `arrowPinKnockbackThreshold` (default 4.0), `wallPinLayers`, `arrowPinPrefab`, `wallPinSfx`, `wallPinVfxPrefab`, `minWallPinSeconds` (default 4.0), and `maxWallPinSeconds` (default 5.0).
- **`KnockbackReceiver.cs`**: Updated `FlingRoutine` and `PinLimbToWall` to fling along the arrow flight trajectory (`damage.direction`), perform continuous spherecasts for wall collisions, lock the struck bone to the wall surface (`isKinematic = true`), clean up initial body-attached arrow props, embed an unparented (`parent: null`) `StuckArrow` facing directly into the wall surface along `pinDir`, trigger wall blood decals, wait for 4–5 seconds, unpin the bone so the corpse drops to the floor under gravity leaving the arrow pinned in the wall, and let the ragdoll settle on the ground.

### Wiring checklist
- [ ] On `Assets/Bladehold/Config/KnockbackConfig.asset` (or in Inspector):
  - [ ] Set `arrowPinKnockbackThreshold` (default 4.0).
  - [ ] Set `wallPinLayers` to include static wall/environment layers (e.g. `Default`, `Ground`, `Environment`).
  - [ ] Set `minWallPinSeconds` (default 4.0) and `maxWallPinSeconds` (default 5.0).
  - [ ] (Optional) Assign `arrowPinPrefab` to `Assets/Bladehold/Bladehold Prefabs/StuckArrow.prefab` (automatically falls back to `Player.Instance`'s `StuckArrowSpawner.ArrowPrefab` if unassigned).
  - [ ] (Optional) Assign `wallPinSfx` for custom wall thunk audio.
  - [ ] (Optional) Assign `wallPinVfxPrefab` for wall impact particle effects.

### Manual verification (Arrow Wall Pinning)
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed and script compilation verified via Unity MCP (`refresh_unity`).
- [ ] Playtest: Charge a bow shot and fire at a melee goblin standing near a wall.
- [ ] Playtest: Verify that if the hit is lethal and has enough knockback, the enemy flies back along the arrow trajectory and pins to the wall.
- [ ] Playtest: Verify the wall pin arrow has no parent (unparented in hierarchy), points straight into the wall surface, and stays fixed in the wall when the goblin drops after 4-5 seconds.
- [ ] Playtest: Verify non-lethal hits apply standard knockback/fling without pinning to the wall.

## Bladehold Survivors Mode — Level-Up HUD Prompt, Player Stats Sidebar & Run Telemetry

### The C# is done
- **`Controls.inputactions` & `InputReader.cs` / `Controls.cs`**: Added `DraftSkills` input action mapped to Keyboard `T` and Gamepad `D-pad Down`.
- **`SurvivorsLevelSystem.cs`**: Added `PendingDrafts` stacking queue, `OnPendingDraftsChanged`, `ConsumeDraft()`, and removed instant auto-pausing on level-up.
- **`SurvivorsLevelUpPromptUI.cs`**: HUD prompt component displayed directly below the crosshair showing `"New skills available"` with an `InputGlyph` button prompt (`T` / `D-pad Down`), triggering the card modal when pressed.
- **`SurvivorsPlayerInfoSidebarUI.cs`**: Right-side reusable panel displaying player class, HP, core stats (Melee Dmg, Ranged Dmg, Crit Chance, Move Speed), and an acquired skills list with level badges and interactive `SkillTooltip` hover popups.
- **`SurvivorsCardSelectUI.cs`**: Updated to show big player level header (`LEVEL {X}`), support stacked multi-level drafts consecutively, and host the live player info/skills sidebar.
- **`DeathScreen.cs`**: Updated for Survivors mode to display full run telemetry (time survived, enemies killed, gold earned, damage dealt/taken, critical hits, level reached), link the `SurvivorsPlayerInfoSidebarUI`, and present a single `"Try Again"` button.

### Wiring checklist
- [x] On `Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity` (under HUD Canvas):
  - [x] Add `Survivors Level Up Prompt` UI element below crosshair and attach `SurvivorsLevelUpPromptUI`.
  - [x] In `SurvivorsCardSelectModal`, attach `SurvivorsPlayerInfoSidebarUI` to the right sidebar container and link it to `SurvivorsCardSelectUI.sidebar`.
  - [x] In `DeathScreen`, link `timeSurvivedText`, `damageDealtText`, `damageTakenText`, `critsText`, `levelReachedText`, and `survivorsSidebar`.

### Manual verification
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [ ] Playtest: Gain gold in Survivors mode and confirm `"New skills available"` prompt appears below crosshair without auto-pausing.
- [ ] Playtest: Press `T` (or `D-pad Down`) to open the draft modal; verify big level header text and right-side player stats & skills list.
- [ ] Playtest: Hover over acquired skills in the sidebar to verify `SkillTooltip` displays correctly.
- [ ] Playtest: Die in Survivors mode and verify the death screen displays `"YOU DIDN'T HOLD THE DOOR"`, full run telemetry, and the skills sidebar.

## Bladehold Survivors Mode — Unity Editor & Scene wiring

### The C# and Scene is done
Implemented a full **Survivors-like game mode** in `Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity`:
- **`SurvivorsGameManager.cs`**: Tracks 30-minute run timer, handles victory at 30:00, manages death state, and controls pausing during level-up skill card draft.
- **`SurvivorsLevelSystem.cs`**: Tracks gold as cumulative XP, dynamically scales target gold per level (`baseGoldTarget`, `goldCostMultiplier`, `flatCostIncrement`), and triggers level-up event.
- **`SurvivorsCardSelector.cs`**: Queries active class skill tree (`SkillTreeSO`), filters candidates enforcing dependency chain rules (root nodes, unlocked prereqs, owned upgradeable skills), and picks 3 distinct cards. Added `ApplyFreePurchase` to `SkillTreeService.cs` so card rewards grant skills without spending coins.
- **`SurvivorsSpawner.cs`**: Continuous off-screen enemy spawner replacing wave-based spawning with time-based difficulty scaling, configurable spawn interval, max concurrent cap, and enemy unlocks.
- **`SurvivorsHUDUI.cs`**: HUD overlay displaying run timer (30:00) and Level / Gold XP bar.
- **`SurvivorsCardSelectUI.cs`**: 3-Card level-up modal overlay window displaying skill title, description, level badge, icon, and select button.

### Wiring checklist
- [x] Scene `Bladehold Survivors Scene.unity` created and saved in `Assets/Bladehold/Bladehold Scenes/`.
- [x] Spawner GameObject configured with `SurvivorsSpawner`, `EnemyRosterSO`, and `EnemyPrefabMapSO`.
- [x] `SurvivorsGameManager`, `SurvivorsLevelSystem`, `SurvivorsCardSelector` created and attached to `SurvivorsGameManager` GameObject.
- [x] HUD UI (`Survivors HUD`) created under `Bladehold HUD` Canvas with TimerText, LevelText, XPSlider, XPText, and `SurvivorsHUDUI` component.
- [x] 3-Card Modal UI (`SurvivorsCardSelectModal`) created with 3 card containers, buttons, titles, descriptions, icons, level badges, and `SurvivorsCardSelectUI` component.

### Manual verification (Survivors Mode)
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed and script compilation verified via Unity MCP (`refresh_unity`).
- [ ] Playtest: Open `Bladehold Survivors Scene.unity` in Unity Editor and press Play.
- [ ] Playtest: Verify continuous enemy spawning around player position.
- [ ] Playtest: Collect gold from defeated goblins, verify Level XP bar fills up, and check that reaching target gold opens 3-Card Selection Modal.
- [ ] Playtest: Pick a skill card and verify skill levels up and applies stats while obeying tree dependency rules.
- [ ] Playtest: Verify HUD timer counts up to 30:00.



## Attack Charge Bar UI — Unity Editor wiring

### The C# is done
Added `AttackChargeBarUI.cs` to render a smooth progress bar (`MMProgressBar`) showing the player's attack charge level and timing, based on the structure of `SummonCastBarUI.cs`. Extended `PlayerAttack.cs` with public properties (`ChargeTimePerLevel`, `MaxChargeTime`, `CurrentChargeTime`, `ChargeProgress`). The bar remains visible and stays at 100% full when max charge is reached, until the attack button is released.

### Wiring checklist
- [x] On `Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab`:
  - Created `Attack Charge Bar` UI element under `Bottom` panel using `SummonCastBarUI` layout as template.
  - Attached `AttackChargeBarUI` component and wired `progressBar`, `canvasGroup`, and `chargeLabel`.
- [x] In `Assets/Bladehold/Bladehold Scenes/Bladehold Demo Scene.unity`:
  - Configured `Attack Charge Bar` on the active HUD canvas.

### Manual verification (Attack Charge Bar)
- [x] C# Compilation verified with `dotnet build` (0 errors).
- [x] Unity Editor AssetDatabase refreshed and compiled via Unity MCP.
- [ ] Playtest: Unlock charge attack skill ("Heavy Strike"), hold attack button, and verify bar smoothly fills and stays at 100% until release.



## Dodge / Dash Mechanic — Unity Editor wiring

### The C# is done
Added a Dodge/Dash mechanic bound to Left Control (`PlayerDodge.cs`). The ability is locked by default and unlocked via `DodgeUnlocked` base stat (set to 1 out of the box per user request, but can be scaled). Includes cooldown tracking (`DodgeCooldown`), dash distance (`DodgeDistance`), and a damage multiplier (`DodgeDamageMultiplier`) that allows dashing through enemies to deal damage (the 'Charge/Blink' precedent). Added `PlayerDodgeUI.cs` to handle the radial cooldown UI and hotkey prompt on the HUD.

### Wiring checklist
- [ ] On `Assets/Bladehold/Bladehold Prefabs/Player.prefab`:
  - Add `PlayerDodge` component to the root.
  - The `characterController`, `player`, and `animator` fields should auto-wire via `OnValidate`.
- [ ] On `Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab`:
  - Create a new UI button object in the bottom section near the weapon icons (or duplicate an existing one like SummonMountButton).
  - Rename it to `PlayerDodgeButton`.
  - Add `PlayerDodgeUI` component.
  - Assign the visual components: `skillIcon` (the main sprite image), `radialFillImage` (set image type to Filled, Radial 360), `timerText` (TextMeshProUGUI), and `keybindIcon` (the small prompt image).
  - Set `keyboardSprite` to a Synty 'Ctrl' key sprite, and `gamepadSprite` to an appropriate gamepad button sprite (e.g., LB/L1).
  - Add two `MMF_Player` child objects for `cooldownFinishedFeedback` (e.g., `MMF_PunchScale`) and `activatedFeedback`, and assign them.

### Manual verification (Dodge Mechanic)
- [ ] Playtest: Ensure the Dodge icon appears in the HUD.
- [ ] Playtest: Pressing Left Control performs a quick dash in the forward direction.
- [ ] Playtest: The UI icon radial fill tracks the cooldown accurately (10 seconds by default).
- [ ] Playtest: Ensure that the ability cannot be used while it is on cooldown.
- [ ] Playtest: (With DodgeDamageMultiplier > 0 via a skill upgrade) Dash through an enemy and ensure it takes damage.## End of Wave Stats UI — Unity Editor wiring

### The C# is done
Expanded the between-wave intermission screen to show detailed telemetry from `RunTelemetry` alongside the existing gold and kill counts, per the user's request. Added `Damage Dealt`, `Damage Taken`, and `Critical Hits` to `WaveStatsPanel.cs` and refactored `RunTelemetry.cs` to expose a `GetCurrentWaveStats` method. Modified `WaveIntermissionUI.cs` so that picking "Recover and Upgrade" (skill tree) now clears all consumables (coins, health drops, element nodes, etc.) from the arena, acting as a true forfeit of the wave's cash reward.

### Wiring checklist
- [ ] On `Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab`:
  - Locate the `WaveStatsPanel` object (often nested inside the Intermission UI).
  - Add three new TextMeshPro fields to display Damage Dealt, Damage Taken, and Critical Hits using the **Synty warrior hud assets** to make them pop (bold font at the top or arranged nicely with the Synty panels).
  - Assign these new text fields to the `damageDealtText`, `damageTakenText`, and `critsText` slots on the `WaveStatsPanel` component.
  - Optional: if using fill bars, assign the new bar Images to the `damageDealtBar`, `damageTakenBar`, and `critsBar` slots.
  - For the MMF juice, create three new `MMF_Player` objects as children (or duplicate existing ones like `goblinsRevealFeedback`).
  - Add `MMF_Scale` or `MMF_PunchScale` and `MMF_AudioSource` (tick sound) feedbacks, ensuring **Unscaled time mode** is checked since intermission time is frozen.
  - Assign these to `damageDealtRevealFeedback`, `damageTakenRevealFeedback`, and `critsRevealFeedback` on the `WaveStatsPanel`.

### Manual verification (End of Wave Stats)
- [ ] Playtest: Clear a wave and wait for the Intermission stats to appear.
- [ ] Playtest: Verify Damage Dealt, Damage Taken, and Critical Hits animate in properly, counting up with juice.
- [ ] Playtest: Click "Recover and Upgrade" (Skill Tree) and ensure any uncollected coins or health drops on the ground instantly disappear.
- [ ] Playtest: Click "Hold the Line" and ensure coins remain on the ground during the countdown.

## Summon Mount — Unity Editor wiring

### The C# is done
Added `PlayerSummonMount` to handle summoning a mount on demand, and `SummonMountUI` for the HUD icon and cooldown display. Modified `PlayerMount` to unlock riding by default (`HorseRidingUnlocked` base is 1f). Replaced the "Saddle Up" node in `SkillTree.csv` (and Berserker/Mage variants) with "Summon Mount", which unlocks the ability via `SummonMountUnlocked`, and added stats for `SummonMountDuration` and `SummonMountCooldown` to `StatType.cs`. This uses the `add-skill-line` conventions, intercepting the `Dismount` action in `InputReader` to spawn the horse and auto-mount.

### Wiring checklist
- [ ] On `Assets/Bladehold/Bladehold Prefabs/Player.prefab`:
  - Add `PlayerSummonMount` component.
  - Set `horsePrefab` to `Assets/Bladehold/Bladehold Prefabs/Horse/Horse.prefab`.
  - Create three child objects for `MMF_Player` feedbacks: `SpawnSmoke`, `DespawnSmoke`, `ErrorSound`. Assign them to the script.
  - In `SpawnSmoke` and `DespawnSmoke`, add a particle effect (e.g. `FX_Smoke_White_Large_01.prefab` via `MMF_ParticlesInstantiation`) and a sound effect (`MMF_AudioSource`).
  - In `ErrorSound`, add an `MMF_AudioSource` with an error/buzz sound.
- [x] On `Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab`:
  - Create a new UI button object in the bottom section near the weapon icons (or duplicate an existing one).
  - Add `SummonMountUI` component.
  - Assign the visual components: `skillIcon` (the main sprite image), `radialFillImage` (set image type to Filled, Radial 360), `timerText` (TextMeshProUGUI), and `keybindIcon` (the small prompt image).
  - Set `keyboardSprite` to the Synty 'X' key sprite, and `gamepadSprite` to the Synty 'B/Circle/East' sprite.
  - Add two `MMF_Player` child objects for `cooldownFinishedFeedback` (e.g., `MMF_PunchScale`) and `activatedFeedback`, and assign them.
  - Create the `SummonCastBar` in the middle of the screen using `MMProgressBar`.
  - Attach `SummonCastBarUI` and wire the progress bar, label ("Summoning Mount"), and CanvasGroup. Add MMF_Players for cast start/finish/cancel juice.
- [ ] **HUMAN:** In **Bladehold > Skill Tree Editor**, drag the horse/summon mount icon sprite to ensure it's registered in `SkillTreeSO`'s icons array under the name `Warriorskill_43_nobg` (or whichever sprite you choose).

### Manual verification (Summon Mount)
- [x] Playtest: Ensure the summon mount icon appears in the HUD only if you purchase the skill or give yourself `SummonMountUnlocked = 1`.
- [x] Playtest: Pressing X while unmounted begins a 2s cast.
- [x] Playtest: The Cast Bar appears, fills up, says "Summoning Mount", and the player plays the Cheer animation.
- [x] Playtest: If you move during the cast, or take damage, the cast is cancelled (bar hides, error sound).
- [x] Playtest: When cast finishes, it spawns a horse, plays smoke/sound, and automatically mounts the player.
- [x] Playtest: The HUD icon shows the active duration (cyan color, decreasing timer).
- [x] Playtest: When the duration expires, the player is automatically dismounted, the horse vanishes in smoke, and the ability goes on cooldown.
- [x] Playtest: Pressing X early dismounts the player. The horse stays alive until its duration expires, then it despawns.
- [x] Negative case: Cannot summon a new horse while one is already active or while on cooldown.

## C# Implementation complete
- [x] `WaveSpawner.cs`: Added `WaveGoblinTotal` and `KilledThisWave` to allow tracking wave progress in real time.
- [x] `WaveClearedBannerUI.cs`: Created to handle the `Quest Complete / Wave Cleared` UI elements (tracking and animating Gold/Kill counts with `MMF_Player`).
- [x] `ObjectiveTrackerUI.cs`: Created to track the objective string ("Slay all enemies: x/y") by reading `WaveSpawner`.

## Unity Editor wiring
- [x] In `Bladehold Test Scene`, delete the old `HUD Canvas` / HUD root.
- [x] Instantiate the `Bladehold HUD` prefab into the scene.
- [x] Add `WaveClearedBannerUI` script to the `Top Left -> Quest Complete / Wave Cleared` object in the new HUD.
    - [x] Assign `Wave Cleared Text`, `Gold Earned Text`, and `Enemies Killed Text` from its children.
    - [x] Assign the `Banner Animation Feedback` (add an `MMF_Player` if one isn't already present for popping the banner in/out).
- [x] Add `ObjectiveTrackerUI` script to the `Objective_List` object (or `HUD_FantasyWarrior_Objectives_02`).
    - [x] Assign the `Objective Header Text` ("HOLD THE GATE: WAVE X").
    - [x] Assign the `Objective Progress Text` ("Slay all enemies...").
- [x] Wire the `Ult Meter`: add `UltimateBarUI.cs` to it (or configure if already present) and point it to the relevant fill Image/Text.
- [x] Wire the `Player Health Bar`: add `PlayerHealthBarUI.cs` / `HealthBarUI.cs` as appropriate and ensure it finds `Player.Instance.Health`.
- [x] Wire the `Currency` text: add `CoinUI.cs`.
- [x] Wire the `Fortress Gate Health` (Top Right): add `HealthBarUI.cs` and ensure its `MMHealthBar` component points to the scene's Gate object.
- [x] Wire the `Horse Health/Stamina`: add `HorseBarGroupUI.cs`, `HorseHealthBarUI.cs`, and `HorseStaminaUI.cs` to the corresponding elements if the player is mounted.

## Manual verification
- [ ] Enter Play Mode.
- [ ] Verify HUD is visible and all bars (Health, Gate, Ult) display correctly.
- [ ] Complete Wave 1.
- [ ] Verify the wave banner pops in correctly, displays accurate gold and kill counts, and animates out.
- [ ] Check that the objectives progress updates with each kill.

## Golden Goblin Enemy Type — Dedicated Fleeing Enemy

### The C# is done
Converted Golden Goblin from a spawn-time modifier into a dedicated, fleeing enemy type:
- **`Enemies.csv`**: Added `golden_goblin` row (`health: 25`, `damage: 0`, `minGold: 50`, `maxGold: 100`, `speed: 6.5`, `maxConcurrent: 1`).
- **`GoldenGoblinFleeSO.cs`**: ScriptableObject under `Scriptable Objects/Enemies/Golden Goblin/GoldenGoblinFleeSO` with tunables (`fleeDistance`, `fleeSampleRadius`, `repathInterval`, `deathVfxPrefab`, `deathSfx`).
- **`GoldenGoblinFlee.cs`**: Fleeing AI component. Disables standard `AIMovement` chasing and drives `NavMeshAgent.SetDestination` to sample NavMesh positions in a cone away from the player (testing 0°, ±35°, ±70°, ±110°, ±150°, 180°). Does not attack (`disableBaseAIAttack = true`). Drops bonus coins on death if player has `GoldenGoblinGoldBonusPercent` stat active.
- **`EnemyManifest.cs`**: Added `golden_goblin` entry with golden material swap (`Assets/Bladehold/Bladehold Materials/Golden Goblin.mat`), `GoldenGoblinFlee` component wiring, and marker component removals.
- **`WaveSpawner.cs` / `SurvivorsSpawner.cs`**: Updated spawn logic so `StatType.GoldenGoblinChance` rolls select the `golden_goblin` enemy type instead of applying a modifier to regular goblins.

### Wiring checklist
- [x] Roster entry `golden_goblin` added to `Config/Enemies.csv`.
- [x] ScriptableObject `GoldenGoblinFleeSO` created under `Enemies/Golden Goblin/GoldenGoblinFleeSO.asset`.
- [x] Prefab `Golden Goblin Enemy Variant.prefab` generated and registered in `EnemyPrefabMap.asset` via `EnemyPrefabGenerator.GenerateAll`.
- [x] Material `Golden Goblin.mat` applied to variant mesh.

### Manual verification (Golden Goblin Enemy Type)
- [x] C# compilation verified with `dotnet build` (0 errors).
- [x] Unity Editor AssetDatabase refreshed and compiled via Unity MCP (`refresh_unity`).
- [x] Enemy prefab `Golden Goblin Enemy Variant.prefab` generated and registered in `EnemyPrefabMap.asset`.
- [ ] Playtest: Spawn `golden_goblin` in Play mode (or via DevConsole `DebugSpawnBurst` / `EnemyZoo`).
- [ ] Playtest: Verify Golden Goblin runs frantically away from player, navigating around obstacles without attacking.
- [ ] Playtest: Defeat Golden Goblin and verify coin drops and death feedback.

## Survivors Card UI — Hover Enter, Hover Exit & Selection Feedbacks (MMF_Player) & Final Selection Transition

### The C# is done
- **`SurvivorsCardUI.cs`**: Implemented `IPointerEnterHandler` and `IPointerExitHandler`. Added `hoverEnterFeedback`, `hoverExitFeedback`, and `selectFeedback` serialized `MMF_Player` fields with unscaled time forcing (`ForceTimescaleMode = TimescaleModes.Unscaled`) so hover/click animations play cleanly while gameplay is paused during level-up card selection. `PlaySelectFeedback()` is automatically triggered on card click.
- **`SurvivorsCardSelectUI.cs`**: Added configurable `finalSelectionDelaySeconds` (default: 0.2s) and `finalFadeDurationSeconds` (default: 0.15s) real-time delay & quick alpha fade-out routine. When multiple level-up drafts are queued (`pendingDrafts > 0`), choosing a card instantly re-rolls to the next 3 cards. When the **FINAL** draft is chosen, buttons disable immediately, the 0.2s delay allows the selection feedback/sound/anim to play out, and the modal smoothly fades out before resuming gameplay.
- **`Card.prefab`**: Auto-wired `hoverEnterFeedback` to the prefab's root `MMF_Player` component.

### Wiring checklist
- [x] Auto-wired `hoverEnterFeedback` on `Assets/Bladehold/Bladehold Prefabs/UI/Card.prefab`.
- [ ] (Optional) On `Assets/Bladehold/Bladehold Prefabs/UI/Card.prefab`, create a `HoverExitFeedback` child object with an `MMF_Player` component and assign to `hoverExitFeedback`.
- [ ] (Optional) On `Assets/Bladehold/Bladehold Prefabs/UI/Card.prefab`, create a `SelectFeedback` child object with an `MMF_Player` component (e.g. click audio, punch scale, flash) and assign to `selectFeedback`.

### Manual verification
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed and verified via Unity MCP (`refresh_unity`).
- [ ] Playtest: Select cards in Survivors mode level-up modal. If multiple drafts are queued, verify choices present next cards immediately. On the final card choice, verify the selection feedback plays out during the brief 0.2s delay before the modal smoothly fades out and gameplay resumes.


## Supply Wagon Escort Destination Arrival — Gold Burst Effect & Gold Bag Drops

### The C# is done
Enhanced `SupplyWagonEscort.cs`:
- **Burst Delay**: Added configurable `burstDelay` (default 0.5s) so the wagon pauses upon reaching destination before bursting.
- **MMF_Player & SFX**: Plays `arrivalFeedback` (`MMF_Player`) and `arrivalSound` on arrival burst so designers can customize arrival camera shake, particles, sound, or visual scaling.
- **Gold Bag Spawning**: Spawns 4–5 (`minGoldBags` = 4, `maxGoldBags` = 5) gold bags (`goldBagPrefab`, with fallback to `SM_Icon_CoinBag_01.prefab`), scattered within `dropScatterRadius` (2.0m) around `dropOffset`, each containing `goldPerBag` (25 gold).
- **Cart Auto-Destruction**: Disables wagon colliders and visual mesh renderers on burst and calls `Destroy(gameObject, destroyDelay)` so the cart cleanly disappears leaving gold on the ground.

### Wiring checklist
- [ ] On `Assets/Bladehold/Bladehold Prefabs/Objectives/SupplyWagon.prefab`:
  - Verify `burstDelay` (default 0.5s).
  - (Optional) Assign custom `arrivalFeedback` `MMF_Player` for extra explosion/gold particle effects.
  - Verify `goldBagPrefab` is set to `Assets/Bladehold/Bladehold Prefabs/SM_Icon_CoinBag_01/SM_Icon_CoinBag_01.prefab` (auto-falls back in code if null).
  - Verify `minGoldBags` (4) and `maxGoldBags` (5).

### Manual verification
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [ ] Playtest: Trigger "Protect the supply wagon" objective or spawn supply wagon.
- [ ] Playtest: Escort wagon to fortress gate destination.
- [ ] Playtest: Verify wagon stops at gate, waits 0.5s, bursts with `MMF_Player` / audio / VFX, drops 4–5 gold bag pickups on the ground, and destroys itself.


## Dev Console Objective Controls — Instant Objective Testing & Cycling

### The C# is done
- **`SurvivorsObjectiveManager.cs`**: Added `ObjectivePool` property and public debug methods:
  - `DebugNextObjective()`: Stops any active intermission and immediately starts the next objective in rotation.
  - `DebugStartObjective(int index)`: Forces starting a specific objective from the pool by index.
  - `DebugCompleteCurrentObjective()`: Instantly completes the active objective (granting rewards and triggering completion handlers).
- **`DevConsole.cs`**: Added `DrawObjectiveControls()` to the in-game debug overlay (`~` key):
  - Displays currently active objective title.
  - Adds **"Next Obj"** and **"Complete Obj"** buttons.
  - Provides a `<` / `>` objective selector to pick any objective from the pool by title and press **"Start '[Objective Title]'"**.

### Manual verification
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [ ] Playtest: Press `~` in Play mode to open the Dev Console.
- [ ] Playtest: Verify objective controls appear showing current objective title.
- [ ] Playtest: Use `<` / `>` buttons to select "Protect the Supply Wagon" (or any other objective) and press "Start '[Objective Title]'". Verify objective starts instantly.
- [ ] Playtest: Press "Complete Obj" to instantly complete the active objective.


## Dev Console Skill Upgrades Column — Instant Skill Level Tuning & Unlocks

### The C# is done
- **`SkillTreeService.cs`**: Added `DebugSetLevel(string id, int targetLevel)` to support both increasing (`>`) and decreasing (`<`) skill node levels directly. Updates `PlayerStats` modifiers live and saves/persists progress to disk.
- **`DevConsole.cs`**: Added a second full-height IMGUI panel column (`DrawSkillsColumn()`) next to the main debug panel:
  - Displays **"Skill Upgrades ([Count])"** header with **"Max All"** and **"Reset All"** convenience buttons.
  - Full screen height scroll view displaying every skill in the active class skill tree.
  - `<` and `>` arrow buttons next to each skill title (`Skill Title [Level/Max]`) for instantly adjusting any skill's level up or down.

### Manual verification
- [x] C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [ ] Playtest: Press `~` in Play mode to open the Dev Console.
- [ ] Playtest: Verify the 2nd column appears on the right side of the main dev panel listing all class skill tree upgrades.
- [ ] Playtest: Click `>` next to any skill to increase its level, or `<` to decrease it. Verify character stats and mechanics update live.
- [ ] Playtest: Test "Max All" to unlock max level on all skills at once, or "Reset All" to reset to level 0.






