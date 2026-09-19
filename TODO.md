# Unity Editor Wiring TODOs

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
