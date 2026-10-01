# Editor to-do: skeleton crowd enemies

No plan file. Built 2026-10-01 with Unity MCP connected: prefabs generated, all five crowd types baked, and the baked weapons checked numerically against the live ones (within 0.2 mm). No Play-mode check was run, since you asked to do those yourself. Tick items off as you go and delete the file when it's empty.

**The skeletons are in `Enemies.csv` with `enabled = TRUE` but `minThreat = 0`, so they never join sector waves.** Spawn them from the DevConsole spawn-type picker or Enemy Manager → Zoo.

## Verify first

- [ ] **Benchmark section 29 passes.** Run Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark from `MainMenu` (don't save the scene it ran in). Look for `[29] Baked Crowd Enemies`, with 5 lines passing: goblin plus the 4 skeletons.
- [ ] **Weapon poses on the live rig.** Open `Bladehold Prefabs/Skeleton Soldier Shield Enemy Variant.prefab` and `Skeleton Knight Shield Enemy Variant.prefab` and look at `Weapon_Sword` (under `Hand_R`) and `Weapon_Shield` (under `Hand_L`).
  - The sword uses the Assassin's grip: pos (0.123, 0.021, 0.063), rot (0, 90, 270).
  - The shield is the Bulwark's pose moved onto the goblin rig: pos (-0.144, -0.084, 0), rot (273.1, 226.4, 309.3). This one is the most likely to need a nudge.
  - **Fix poses in `Editor/EnemyManifest.cs` → `Skeleton(...)`, not on the prefab.** The generator re-applies them on every run. Then run Generate Enemy Prefabs, then Bladehold/Crowd/Bake Crowd Animations, or the baked weapon stays where it was.

## Playtest

- [ ] **Baked vs live look the same.** Spawn about 20 of each skeleton type. Check:
  - idle, run and the sword swing (standing and while running)
  - the sword and shield move with the hands
  - no doubled or floating weapon
  - hitting one (it promotes to the live rig) shows no pose or weapon jump
- [ ] **Sword swing timing.** Damage should land on the swing's hit, about 0.4 s in. The skeletons share the goblin's `AIAttackSO`. If it feels off, they need their own SO (a manifest `assets` entry).
- [ ] **Deaths and flings past the ragdoll cap.** Set Max Ragdolls low and kill a group. Baked falls should play with the weapons attached, corpses should stay down, and non-lethal flings should get back up and rejoin the crowd.
- [ ] **Real ragdolls under the cap.** The live sword and shield should go limp with the hands, and nothing should stay frozen in mid-air.
- [ ] **Never:** an invisible skeleton (missing bake: check the console for a `BakedCrowdAgent ... not baked` error), or a weapon left visible on a baked skeleton (that would be a doubled weapon).

## Decisions

- [ ] **Sector waves:** set `minThreat` / `unlockWave` / `spawnChance` for the four `skeleton_*` rows in `Config/Enemies.csv`. The stats there are placeholders: soldier 12 HP, soldier with shield 18, knight 25, knight with shield 35.
- [ ] **Shield behaviour:** the shield is cosmetic plus extra HP. Should the shield variants block like the Bulwark (`BulwarkShield` / `Health.TryBlockDamage`)?
- [ ] **Weapon choice:** soldier has `SM_Wep_BrokenSword_01` + `SM_Wep_Shield_Bone_01`; knight has `SM_Wep_Ornate_Sword_02` + `SM_Wep_Shield_Heater_01`. Any swap must use the `PolygonDungeon_01` material, or the bake refuses it.
