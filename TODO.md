# Unity Editor Wiring TODOs

## War Banner System
- [ ] **Banner Art & Animations:** The \WarBanner\ prefab is currently composed of basic primitive shapes (Cylinder). Needs to be updated with proper 3D models and textures.
- [ ] **UI Styling:** The Banner World Space UI needs styling passes to match the game's UI theme.
- [ ] **VFX & SFX:** Hook up tearing down / destruction particle effects and sound effects in \WarBannerController.cs\.
- [ ] **Voice Bark:** Assign the actual audio clip for the enemy bark: *"Them's tearin down ours bannah! Get 'em!"*

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
- [ ] In the SurvivorsObjectiveManager component on the same GameObject, add the GoldenGoblinObjective to the epeatingObjectiveComponents list so it gets selected randomly as a wave objective.

Manual verification:
- [ ] Run the game and trigger the Golden Goblin objective.
- [ ] Verify that no other enemies spawn.
- [ ] Verify the goblin runs in circles along the waypoints.
- [ ] Verify hitting him drops gold periodically, and killing him gives a bonus and ends the wave.
