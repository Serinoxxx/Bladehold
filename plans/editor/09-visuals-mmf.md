# Editor to-do: plan 09 (Code-built visuals + MMF audit)

From [plan 09](../09-visuals-and-mmf-audit.md). Sections 1-3 are from session 1 (2026-09-26, the runtime `LoadAssetAtPath` batch; MCP wasn't connected). Section 4 is session 2 (code-built UI, done via MCP). **Until §2's first item is done, building a tower plays no sound, dust or shake** and logs 3 errors per build. Tick items off as you go and delete the file when it's empty.

## 1. Verify first (5 min)

- [ ] **Let Unity import.** Check the Console for compile errors, and that `Bladehold Prefabs/Defenses/DefenseAssemblyAnimation.prefab` opens: a root with `DefenseAssemblyAnimation` and three children (`PieceLandFeedback`, `RiseDustFeedback`, `SlamFeedback`), each with an empty `MMF_Player`. It was written by hand as YAML. If it fails to import, delete it and recreate it the same way, then drag it into `TowerPlot.prefab` → `TowerPlot.assemblyAnimationPrefab`.
- [ ] **Run the benchmark** (**Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark**). Check 20D now reads "TowerPlot prefab references an authored assembly prefab. [PASSED]".
- [ ] **Binary castle scenes don't override the new wiring.** In any castle scene, select the HUD's `BuildWheelUI`: `supplyPopupPrefab` = Gold, `buildFeedback` = its `BuildMMF` child (session 7 replaced `buildSfx`/`buildVfxPrefab`), neither bold (overridden). Likewise `ObjectiveWaypointTrackerUI.noSupplyIcon` / `cleanupEnemySkullIcon`. *(MCP-able)*

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
- [ ] **Rest Area Draft Station** got an authored `StationLight` child (range 8, intensity 2.5), tinted by category at runtime.

**Benchmark after this session: 107 passed, 5 failed.** All 5 are in systems this session didn't touch: Bulwark normal attack, Bannerman rig (`isBruteRig=False`), BuildWheelUI open/close, skull waypoints. Probably older than this session; worth a look (or a plan-11 ticket).
**Seen during play checks, also not from this change:** the Crypt and Sanctuary `GameLoopManager.bannerSpawnPoints` is unassigned; the Fishing Pond logs `FishingDraftUI`/`FishingTallyUI` unwired-field errors (plan 04) and a legacy `UnityEngine.Input` exception (probably a `StandaloneInputModule` in that scene).

## 6. Session 4: player MMF feedbacks (feel tuning, 00 §D)

All wired and play-checked through MCP: each fires at the right spot with its old clip/VFX, clones clean up, no errors. What's left is taste. All of them live on `Player.prefab → SidekickSyntyCharacter`, named `*MMF`.

- [ ] **Mace ultimate slam** `MaceSlamMMF`. It used to flash the hero (wrong MMF picked up by `GetComponentInChildren`); now it's sound + rock burst + a copy of the mace hit's Cinemachine impulse. Probably wants a bigger shake for a 10 m slam. Verify: DevConsole → mace + ultimate card → fire it.
- [ ] **Earth Splitter** `EarthSplitterMMF` (sound + impulse, new) and `EarthSplitterRockMMF` (one rock burst per point, ×4). Verify: axe with Earth Splitter, full-charge release.
- [ ] **Out-of-ammo click** `OutOfAmmoMMF`: placeholder `UI_Click_02` at 0.7, 0.25 s cooldown. It never played before (the clip was never set). Swap in a proper dry-fire/bowstring sound. Verify: empty the quiver and keep firing.
- [ ] **Sounds are 2D now** (`SpatialBlend` 0, like the existing bow/zap MMFs), where `PlayClipAtPoint` was fully 3D. Ice shatter, thermal shock and superconductor on far-away enemies now play at full volume. Raise spatial blend on `IceShatterMMF`/`ThermalShockMMF`/`SuperconductorMMF` if that's too loud.
- [ ] **New VFX variants** `VFX/FrostStepBurst` (`FX_ShardIce_Shooting_01` + 2 s `MMTimedDestruction`) and `VFX/ArmourEquipPoof` (`Loot_Poof` + 3 s). Both source prefabs loop; that's the only reason the variants exist. Tune the lifetime there.
- [ ] **Flame Zone** `FlameZone.prefab → burnTickFeedback` is empty (it always was). Add a sizzle MMF if burning ticks should be heard.

## 7. Session 5: hit feedback MMFs (feel tuning, 00 §D)

All wired and play-checked through MCP in the Survivors scene: sword hit/crit/woosh, bow headshot, the Impulse light, plasma/status/frozen/discord sounds, and fling (base thud, Goblin Warrior scream) with its flash light. Knockdown, wall pin and ragdoll bone splashes fire, pooled bursts return to the pool, and no errors are logged. What's left is taste.

- [ ] **Hit loudness.** Sword/axe/mace/staff hits (`Player.prefab → … prop_r/<weapon>/HitMMF`, `CritHitMMF`, `InanimateHitMMF`, `WooshMMF`) and bow hits (`SidekickSyntyCharacter/BowHitFeedback/*MMF`) used `PlayOneShot` at 1.2-1.8 volume. MMSoundManager caps at 1, so they may sound a touch quieter. Raise the clips' import gain or the Sfx track if so. Verify: hit a goblin with each weapon, and land a bow headshot.
- [ ] **Blood burst sizes** are `MMF_PooledParticleBurst` ranges on those players. The speed ranges look odd (e.g. 0.025-0.1) because they reproduce the old code, which overwrote the prefab's start speed with 0.5-2 absolute. Tune freely. The ragdoll splash (`Goblin Enemy (Base)/Feedbacks/RagdollBloodMMF`, emit 4-42, scale 0.4-2.1) approximates the old torso/head/limb scaling: intensity 1 = a full-speed torso hit. Verify: fling goblins (DevConsole Impulse) and watch landings.
- [ ] **Impulse light pulse** `SidekickSyntyCharacter/ImpulseHitMMF` (purple, peak 8, 0.35 s). It **never showed before**: the old code cloned an inactive light, so this is new on screen. Check it isn't too much. Verify: sword with the Impulse card, heavy hit.
- [ ] **Knockback flash** `VFX/KnockbackFlashLight.prefab` (session-3 mockup) now spawns from `FlingMMF` via Instantiate Object. Its colour (pale blue), intensity 2, range 10 and 0.2 s fade live on the prefab (Light + `FlashLightDimmer.fadeDuration`), no longer on `KnockbackConfig`. Art review as before.
- [ ] **Enemy sounds are 3D** (knockdown/fling thuds, Goblin Warrior screams, wall-pin splat, status/frozen/discord/plasma), like the old `PlayClipAtPoint`. Player hit sounds are 2D. The session-4 `ThermalShockMMF`/`SuperconductorMMF` (now also used by the duo synergies and Static Edge on enemies) are still 2D; raise their spatial blend if far-off zaps are too loud.
- [ ] **Status-applied sound** (`StatusAppliedMMF`) fires every time fire/ice lands on an enemy without the visual. Max 3 concurrent instances. If a fire-arrow volley gets noisy, add a cooldown on the MMF player.
- [ ] **Impulse blast burst** `SidekickSyntyCharacter/ImpulseBlastHitMMF` (was `BowSO.headExplosionPrefab`) still plays once per enemy hit, all at the blast centre, as before. Consider playing it once per blast.


## 8. Session 6: enemy + spawner MMFs (feel tuning, 00 §D)

All wired and play-checked through MCP in the Survivors scene. Lightning ball, homing orb, boulder, storm zone, dynamite fuse and blast, golden death, bubble block, Assassin slash, Slayer roar during the (time-frozen) intro, and cleanup lightning each played their old clip at the old volume, 3D like `PlayClipAtPoint`. Bursts spawned unparented and cleaned themselves up, and nothing new was logged. What's left is taste plus a few feedbacks that were never authored.

- [ ] **Golden death is shared.** `Goblin Enemy (Base) → Feedbacks/GoldenDeathMMF` (coin burst + `Coins`), inherited by every variant. `Goblin Brute Enemy` and `Goblin Enemy Variant` (not variants of the base) have their own copy. The fleeing **Golden Goblin** points `GoldenGoblinFlee.deathFeedback` at the same player. Its SO never had a sound or VFX, so **it was silent before; this is new on screen.** Verify: DevConsole → spawn a Golden Goblin, kill it.
- [ ] **Slayer**: `Feedbacks/RoarMMF` (Behemoth roar, unscaled time) replaced the `RoarAudio` child, which is deleted. `DashSmashMMF` is just the old rock burst: it never had a sound, and the old tooltip's "camera shake" was never there either. Add a thud/impulse if the landing feels weak. `CrushMMF` (blood + splat) has its 0.15 s anti-spam on the sound only. Note that `SlayerDashAttack` and `SlayerStompCrusher` are **disabled on the prefab**, and were before this session. Verify: spawn a Slayer (intro roar), let it dash.
- [ ] **Never authored (optional fields, empty = silent, as before):** `HookProjectile.impactFeedback` (Pig Butcher hook), `ToxicPool.tickFeedback`, `ArrowBarrageZone.hitFeedback`, `LightningOrbDropper.deathFeedback` (Storm Witch), `ImpulseGoblin.deathFeedback`, `DestructibleBanner.breakFeedback` (Bannerman banner), `CaptainKombustaController.igniteFeedback`, `BubblerCaster.shieldBreakFeedback` (falls back to the block sound, as before), `AssassinAttack.windupFeedback`, `SurvivorsSpawner.groupSpawnHornFeedback`. The horn clip that used to sit on `WaveConfigSO` (only the dead Demo/Test scenes used it) is `887c0b12…`, if you want it back. Add MMF players where these deserve a sound.
- [ ] **Cleanup lightning** `EnemySpawner.prefab → CleanupLightningMMF` (the castles, Frozen Pass and Ancient Garden use the prefab), plus a copy on the Survivors scene's own `EnemySpawner`. Sounds cap at 3 at once now, so a 20-enemy wipe no longer stacks 20 thunderclaps. The old fallback to `ElementalEffectsManager`'s superconductor VFX/SFX is gone.
- [ ] **Intro default roar** `EnemyIntroController.prefab → DefaultRoarMMF` (CreatureRoar1, unscaled), plus a copy on the Survivors scene's standalone controller. The old `defaultRoarDelay` was 0 everywhere; set an Initial Delay on the feedback if you want one. Keep its timing unscaled, because the intro runs at timescale 0.
- [ ] **Enemy Zoo** still has old unpacked enemy copies (disabled `GoldenGoblin` components). They weren't rewired, same as session 5. Re-drop fresh prefabs there if the zoo matters.

**Benchmark after this session: 108 passed, 4 failed** (Bulwark attack, Bannerman rig, skull waypoints, objective-completion flip: all pre-existing). The edit-mode `Destroy may not be called` errors now come only from `DynamiteProjectile.Detonate` destroying its telegraph and itself during test 13A, not from sounds.

## 9. Session 7: tower MMFs (feel tuning, 00 §D)

Wired through MCP and play-checked in the Survivors scene: all six towers' repair/upgrade/break, the fire/net/spill/impale players, the boulder explosion (with its elemental payloads), the net impact, and the fireball strike and burn-out. No errors, and the bursts clean themselves up. What's left is taste.

- [ ] **Tower sounds are 3D now** (they were `PlayClipAtPoint`, so they always were). That includes the build hammer, repair and upgrade chimes, which play at the tower rather than on the hero. Lower their spatial blend if refilling a far tower sounds too quiet. Verify: build a tower, refill it, upgrade it.
- [ ] **Upgrade burst on every upgrade.** `UpgradeMMF` (chime + wood burst at 1 m) now plays whenever a tower levels up, including DevConsole level-ups. Before, the burst only came with a paid `[E]` upgrade.
- [ ] **Catapult impact shake** `CatapultBoulder → Feedbacks/ExplosionMMF` Camera Shake (0.32 s, amplitude 0.45, frequency 35, 0.35 per axis) copies the old direct call. It needs an `MMCameraShaker` on the camera, the same as before. Verify: a catapult hit near you shakes the screen.
- [ ] **Timed VFX lifetimes** in `VFX/`: `WoodImpactBurst` 2.5 s, `CatapultExplosionBurst` 4 s (the old lifetimes), and `FireBurst` 2 s / `FireballBurst` 1.5 s (new: these looped forever before). Tune them there.
- [ ] **Never authored (optional, empty = silent, as before):** `Defense_Catapult → CatapultDefense.fireFeedback` (launch thunk), `Fort/BurningOilZone → sizzleFeedback`, `Fort/SlipperyIceZone → slipFeedback`, and a sound for the storm cloud's lightning (`CatapultStormCloud → StrikeMMF` is just the bolt). Add MMF sounds where they deserve one.
- [ ] **Net impact plays two sounds** (the projectile's wood break and the thrower's cloth/metal thud). It always did; drop one if it's muddy.

## 10. Session 8: waves, objectives and hub MMFs (feel tuning, 00 §D)

Wired through MCP. Play-checked in the Survivors scene: bounty reward, wave-cleared chime and quest horn, stats count-up, war banner burn, ram impact and death, siege catapult death, cage break and prisoner cheer/poof, wagon arrival and gate destruction. All fired, the bursts cleaned up, and nothing new was logged. The Rest Area well and the Fishing Pond players are wired and saved but weren't play-tested.

- [ ] **Fishing Frenzy countdown and horn are new on screen.** `Bladehold Fishing Pond → FishingMinigameManagers/CountdownMMF` (`cinematic_deep_boom_impact_01`, 2D) and `FrenzyStartMMF` (`battle_viking_horn_call_far_03`). The code always meant to play them, but it loaded them from `Resources` paths that don't exist, so they never played. Verify: pond → start → thump ×3, then horn.
- [ ] **Gate falls** `Waves/GateDestructionMMF.prefab` (explosion sound + `FireExplosionUnscaled`, unscaled), nested under every gate in 12 scenes, including the binary castles. The Rest Area has three gates too (odd, but they're wired). Verify: let a gate fall in a castle sector. During the check, one synthetic kill didn't show a burst while a direct call did, so eyeball this one.
- [ ] **Objective sounds are 2D**, as before (the old MMSoundManager calls used its 2D default): ram boom and break, siege explosion, prisoner laugh and poof, gate explosion, bounty coin. The war banner burn and well drink are 3D (they were `PlayClipAtPoint`). Raise spatial blend on the objective ones if a far-off ram shouldn't be as loud as one next to you.
- [ ] **Stats count-up** `UI/StatCountTickMMF` (coin, 0.7, max 8 at once) and `StatRowCompleteMMF` (chime, 0.77): the code sets their pitch per tick/row. The old `PlayOneShot` had no instance cap. Raise `MaximumConcurrentInstances` on the tick sound if ticks drop out. Verify: die and watch the death-screen stats.
- [ ] **Supply wagon arrival** `Objectives/SupplyWagonArrivalMMF` is just the coin burst. `arrivalSound` was never set, so add a fanfare here if the moment wants one.
- [ ] **Never authored (optional, empty = silent, as before):** chest appear/claim (`WaveUpgradePowerup.spawnFeedback`/`claimFeedback`), Rest Area draft station open, fishing bow shot, fishing time-up and fish-caught sounds.
- [ ] **Benchmark junk trap (plan 11):** running the mechanic benchmark leaves `Benchmark_*`/`Test*` objects in whatever scene is open. Don't save the scene after running it; reopen it instead.
