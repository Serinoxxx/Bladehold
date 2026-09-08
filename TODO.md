# Unity Editor Wiring TODOs

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
