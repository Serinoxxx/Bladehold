# Editor to-do: plan 09 (Code-built visuals + MMF audit)

From [plan 09](../09-visuals-and-mmf-audit.md). Sections 1-3 are from session 1 (2026-09-26, the runtime `LoadAssetAtPath` batch; MCP wasn't connected). Section 4 is session 2 (code-built UI, done via MCP). **Until §2's first item is done, building a tower plays no sound, dust or shake** and logs 3 errors per build. Tick items off as you go and delete the file when it's empty.

## 1. Verify first (5 min)

- [ ] **Let Unity import.** Check the Console for compile errors, and that `Bladehold Prefabs/Defenses/DefenseAssemblyAnimation.prefab` opens: a root with `DefenseAssemblyAnimation` and three children (`PieceLandFeedback`, `RiseDustFeedback`, `SlamFeedback`), each with an empty `MMF_Player`. It was written by hand as YAML. If it fails to import, delete it and recreate it the same way, then drag it into `TowerPlot.prefab` → `TowerPlot.assemblyAnimationPrefab`.
- [ ] **Run the benchmark** (**Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark**). Check 20D now reads "TowerPlot prefab references an authored assembly prefab. [PASSED]".
- [ ] **Binary castle scenes don't override the new wiring.** In any castle scene, select the HUD's `BuildWheelUI`: `supplyPopupPrefab` = Gold, `buildSfx` = HAMMER_Hit_Wood_Shield_stereo, `buildVfxPrefab` = FX_Impact_Wood_01, none of them bold (overridden). Likewise `ObjectiveWaypointTrackerUI.noSupplyIcon` / `cleanupEnemySkullIcon`. *(MCP-able)*

## 2. Prefab wiring

- [ ] **Tower build feedbacks** in `DefenseAssemblyAnimation.prefab`. Fill the three MMF players. The values below are what the old code did; tune to taste:
  - `PieceLandFeedback`: MMSoundManager Sound `HAMMER_Hit_Wood_Shield_stereo`, Particles Instantiation `FX_Dust_Small_01`, Camera Shake 0.14 s / amplitude 0.2.
  - `RiseDustFeedback`: Particles Instantiation `FX_Dust_Small_01` (plays every 0.22 s while a 1-2 piece tower rises).
  - `SlamFeedback`: Sound `HAMMER_Hit_Wood_Shield_stereo`, Particles Instantiation `FX_Dust_Big_01`, Camera Shake 0.28 s / 0.38.
  - Code calls `PlayFeedbacks(position)`, so feedbacks should use the passed position. The object is destroyed 0.15 s after the last landing, so **don't parent spawned particles to the feedback**, or they vanish mid-puff.
  - Verify: build an arrow tower (drop mode) and a spike trap (rise mode). Thuds, dust and shake play, and no `DefenseAssemblyAnimation ... not assigned` errors appear.
- [ ] **Horse summon sound:** `Player.prefab` → `PlayerSummonMount.spawnFeedback` (and `despawnFeedback`) are empty. The neigh was an Editor-only hack and is gone. Add an MMF_Player with Sound `Bladehold Audio/SFX/Horse/Horse_Neigh_01.wav` and assign it. Verify: summon the mount (DevConsole) and hear the neigh.
- [ ] **Main menu click sounds:** the 10 `UIClickFeedback` buttons in `MainMenu` and `UI/MenuButton.prefab` have no `clickFeedback`, so they're silent and log an error on Start. Copy the click `MMF_Player` from any button in `UI/PauseMenuCanvas.prefab` and assign it. *(MCP-able)*

## 3. Playtest

- [ ] **Pickups play one sound, not two:** coin, health pack, Impulse orb, Lightning orb. Each used to play its MMF sound plus a second, Editor-only one.
- [ ] **Ammo pickup now has a sound** ("New Item A"). Its MMF player existed but wasn't wired.
- [ ] **Towers:** building shows the wood-impact VFX from the build wheel. Let enemies wreck a tower: it now shows the wood break VFX in a build too.
- [ ] **Objectives:** supply wagon arrival spawns gold bags; the cleanup phase shows skull markers; the "no supply" waypoint shows the hammer icon; the `[E]` interaction prompt appears.
- [ ] **Must never happen:** any `... is not assigned` error in the Console during a normal run. Each one names the exact field to wire.

## 4. Session 2: UI mockups (agent mockup, human review)

Built by an agent through MCP, wired and play-checked for errors, but nobody has looked at them yet. Synty art, Grenze/Texturina. Tune freely: code only needs the wired refs.

- [ ] **Loading screen** `Resources/LoadingScreenManager.prefab` → child `LoadingScreen_Canvas`. Title, subtitle, lore, Synty bar, status text on a dark vignette. It used to be a plain code-built canvas. Verify: Battle Portal or any map node → the loading screen shows "Entering …" and a filling bar. (Checked by agent: loads Meta Area cleanly, no errors.)
- [ ] **Waypoint marker** `UI/ObjectiveWaypointMarker.prefab`: badge background, icon, off-screen arrow, distance text. Verify: an objective sector shows markers on screen and clamped arrows off screen.
- [ ] **Acquired-skill row** `UI/SidebarSkillRow.prefab`, used by the pause-menu and death-screen sidebars. Name + level badge + icon, with hover tooltip. Verify: pick a couple of draft cards, open pause and check the list. (Ultimate cards no longer get a gold-tinted row; the fallback did that. Say if you want it back.)
- [ ] **Enemy intro subtitle** `Bladehold HUD.prefab` → `Enemy Intro UI/Top Bar/Name Container/SubtitleText`: skulls + `[Captain]` tag in gold Grenze below the name. Verify: a captain sector's intro.
- [ ] **Princess cast bar** `UI/BossCastBar.prefab`, instanced above the Princess in `Bladehold Princess Sanctuary` (world space). Verify: down a knight and watch her revive channel.
- [ ] **Crosshair ammo text** switched from LiberationSans to Grenze (`Bladehold HUD.prefab` → `Crosshair`). Check it still reads at a glance. `Enemy Zoo`'s crosshair is now a copy of the HUD one.

**Seen in the console during the play check, not from this change:** `SwordChargeFeedback`: "PlayerAttack is not assigned" and a `KnockbackReceiver` dependency error on `TrainingDummy_MetaArea`, both in the Meta Area.

## 5. Session 3: world-visual mockups (agent mockup, art review)

Built and wired through MCP, and play-checked for errors in the Fishing Pond, Crypt, Sanctuary and Survivors scenes. The shapes and colours copy what the old code built, so they look exactly as rough as before; now they're prefabs you can reskin. Mockup materials are in `Materials/Mockup/`. Code only needs the wired refs (and, where noted, the child that gets scaled).

- [ ] **Catapult payloads** (`Bladehold Prefabs/Fort/`): `SlipperyIceZone` (flat ice disc), `CatapultStormCloud` (dark puff), `RollingFireball` (fire-material sphere). The ice/cloud `visualRoot` child is authored for a **1 m radius** and its x/z get scaled to the zone radius. Swap in real VFX as children of those roots, or re-point `visualRoot`. Candidates: `vfx_LingerRoundMarker04_Blue`, `FX_Smoke_Black_Large_01`, `FX_Fireball_01`. Verify: DevConsole → build a catapult with the Glacial/Tempest/Pyroclast upgrade and let it fire.
- [ ] **Wave reward chests** (`Powerups/WaveReward_*.prefab`, variants of `WaveUpgradePowerup.prefab`). Each holds its old Synty chest under `Visual`, with the chest colliders removed and a point light. The light is tinted by bounty; the chest isn't. Verify: clear a wave, and the chest appears, bobs, and `[E]` claims it.
- [ ] **Kombusta dynamite** `Enemies/DynamiteProjectile.prefab` (Synty `SM_Prop_Dynamite_01`). It used to be a red cylinder because `dynamitePrefab` was empty. Verify: a Kombusta captain throws visible sticks.
- [ ] **Fishing** `Fishing/FishingArrow.prefab` (Synty arrow) and `Fishing/Fish.prefab` (Synty `SM_Item_Meat_Fish_01` at 0.3 scale; the code tints its renderer per fish type). Both used to be primitives. Probably wants a real live-fish mesh. The pond's `FishingBowController` now lives, disabled, on `Player.prefab → SidekickSyntyCharacter`. Verify: pond → T → fish swim, shots fire visible arrows.
- [ ] **Bubbles**: `VFX/BubbleShieldVisual.prefab` (Bubbler shield, same Piloto material as before; `BubbleShieldSO.bubbleMaterial` became `bubbleVisualPrefab`) and `VFX/NecromancerBubbleShield.prefab` (purple, in the Crypt). Both keep a trigger `SphereCollider`, because projectile/sweep hit tests use it.
- [ ] **Boss telegraphs** (LineRenderers on `Mockup_TelegraphLine`): `SweepTelegraphArc` (Necromancer, scaled to `sweepAttackRange`), `HolySoulBeacon` (×4 knights, Sanctuary), `PrincessMagicCircle` (Sanctuary; Synty `FX_Ritual_Circle_01` might suit it better). Verify: Crypt → Defy → watch a scythe sweep; Sanctuary → down a knight.
- [ ] **Throwing-axe ultimate blades** `VFX/VortexBlade.prefab` (Synty axe at 0.8, no collider; code sets rotation). They used to be red cubes. Check the axe orientation while it orbits.
- [ ] **Knockback flash** `VFX/KnockbackFlashLight.prefab` (Light + `FlashLightDimmer`). `KnockbackConfig` still drives its colour/intensity/range. Plan 09 batch B may turn this into an MMF light feedback.
- [ ] **Rest Area Draft Station** got an authored `StationLight` child (range 8, intensity 2.5), tinted by category at runtime.

**Benchmark after this session: 107 passed, 5 failed.** All 5 are in systems this session didn't touch: Bulwark normal attack, Bannerman rig (`isBruteRig=False`), BuildWheelUI open/close, skull waypoints. Probably older than this session; worth a look (or a plan-11 ticket).
**Seen during play checks, also not from this change:** the Crypt and Sanctuary `GameLoopManager.bannerSpawnPoints` is unassigned; the Fishing Pond logs `FishingDraftUI`/`FishingTallyUI` unwired-field errors (plan 04) and a legacy `UnityEngine.Input` exception (probably a `StandaloneInputModule` in that scene).
