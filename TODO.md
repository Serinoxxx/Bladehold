# Unity Editor Wiring TODOs

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
