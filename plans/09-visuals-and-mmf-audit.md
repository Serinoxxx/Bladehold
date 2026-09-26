# 09: Code-built visuals + MMF audit

**Rules** (`/CLAUDE.md` Conventions):
- No visuals or fallback UI built in code.
- All audio, VFX, screenshake and flashes go through `MMF_Player`.

This plan audits and migrates the existing violations. Split it into several sessions by folder.

## Progress

| Session | Scope | Status |
|---|---|---|
| 1 | Runtime `LoadAssetAtPath` fallbacks (they silently return null in a player build) | **Done 2026-09-26** |
| 2 | Code-built UI → prefab mockups | **Done 2026-09-26** (mockups await UI review) |
| 3 | Code-built world visuals (primitives, telegraph LineRenderers, lights) | **Done 2026-09-26** (mockups await art review) |
| 4 | Batch A: player weapons → MMF | **Done 2026-09-26** (feel awaits tuning) |
| 5 | Batch B: hit feedback → MMF | **Done 2026-09-26** (feel awaits tuning) |
| 6 | Batch C: common enemies + spawner → MMF | **Done 2026-09-26** (feel awaits tuning) |
| 7 | Batch D: towers → MMF | **Done 2026-09-26** (feel awaits tuning) |
| 8 | Batch E: waves, objectives, hub UI → MMF | **Done 2026-09-26** (feel awaits tuning) |
| 9+ | Direct audio/VFX/shake → MMF, one batch per session (ranked list below) | Next: batch F |

**Session 1 done:**
- Deleted the Editor-only duplicate pickup sounds from `AmmoPickup`, `Coin`, `HealthPack`, `ImpulseOrb`, `LightningOrb` and `PlayerSummonMount`. Their MMF players already carry the sound; AmmoPickup's player existed but wasn't wired, so it's wired now.
- `DefenseStructure`, `BuildWheelUI`, `ObjectiveWaypointTrackerUI`, `KillRemainingEnemiesObjective`, `SupplyWagonEscort`, `PlayerInteraction`, `WaveClearedBannerUI`: fallbacks deleted and replaced by `LogError`. Any empty prefab fields the fallbacks were covering are now wired in YAML (tower `breakVfxPrefab` ×6; HUD build-wheel popup/SFX/VFX; tracker `noSupplyIcon`/`cleanupEnemySkullIcon`; `SupplyWagon.prefab` `goldBagPrefab`).
- `UIClickFeedback` is MMF-only now (the `customClickSound` path and its Editor fallback are gone). Every wired click MMF player already had a sound.
- `DefenseAssemblyAnimation` is an authored prefab (`Defenses/DefenseAssemblyAnimation.prefab`, referenced by `TowerPlot.assemblyAnimationPrefab`) with three `MMF_Player`s, replacing `AddComponent` + loaded clips/particles + `MMCameraShakeEvent`. Dead ghost-blueprint code removed. Benchmark 20D updated.
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md).

**Session 2 done** (Unity MCP connected):
- New agent-mockup prefabs (Synty art, Grenze/Texturina): `UI/ObjectiveWaypointMarker.prefab` (tracker `markerTemplate`), `UI/SidebarSkillRow.prefab` (sidebar `skillItemPrefab` in the HUD, DeathScreen and Survivors scene), `Resources/LoadingScreenManager.prefab` (manager + loading canvas; the singleton now spawns it via `Resources.Load` instead of building a canvas), `UI/BossCastBar.prefab` (instanced under the Princess in the Sanctuary scene and by `BuildPrincessSanctuaryScene`).
- The HUD's `EnemyIntroUI` got an authored `SubtitleText`. Its crosshair and ammo counter were already authored, so `BowCrosshairUI`/`BowAmmoUI` just lost their builders (ammo text now in Grenze). `Enemy Zoo`'s stand-alone crosshair was replaced with a copy of the HUD one.
- `SkillTooltip` no longer adds its Canvas/CanvasGroup: `Tooltip.prefab` carries both. `LoadingScreenUI` no longer adds a CanvasGroup.
- `ActiveTowersHUDUI` deleted: nothing referenced it (no scene, prefab or code), so it never ran.
- Every fallback now `LogError`s naming the field to wire.
- Blocker cleared on the way: Unity refuses to save a prefab with missing scripts, so plan 08's cleanup was run (its logic via MCP, minus the modal dialog), the legacy prefabs and `Plan08LegacyCleanup.cs` deleted. The binary scenes did **not** become text (neither `SaveScene` nor `ForceReserializeAssets` converted them); see 00 §B.

**Session 3 done** (Unity MCP connected). Every world visual that was built in code is now an authored prefab or a wired reference. Missing refs `LogError` with the field name.
- **Already authored, fallback deleted:** `ArrowBarrageZone` (its prefab already has a red round-marker VFX, so the LineRenderer was a duplicate), `SpawnIndicator` (the pacing config's `indicatorPrefab` is wired), the Kombusta dynamite telegraph (`SlamTelegraph`).
- **New prefabs, all agent mockups built from the old code's shapes and colours:**
  - `VFX/`: `BubbleShieldVisual` (on `BubbleShieldSO.bubbleVisualPrefab`, replacing `bubbleMaterial`), `NecromancerBubbleShield`, `SweepTelegraphArc` (a unit-radius fan scaled to the sweep range), `HolySoulBeacon`, `PrincessMagicCircle`, `KnockbackFlashLight` (on `KnockbackConfigSO.flyingLightFlashPrefab`), `VortexBlade` (Synty axe, on `ThrowingAxeUltimate.orbitBladePrefab`).
  - `Fort/`: `SlipperyIceZone`, `CatapultStormCloud`, `RollingFireball`. Each `Spawn()` takes its prefab now, held by `CatapultBoulder.prefab`'s `CatapultProjectile`. `CatapultDefense`'s no-projectile splash fallback is gone.
  - `Enemies/DynamiteProjectile` (Synty dynamite, on `CaptainKombustaSO.dynamitePrefab`, which was empty before, so the stick was always a code-built red cylinder).
  - `Fishing/FishingArrow` and `Fishing/Fish`. Both prefab fields were empty before, so arrows and fish were always primitives. `FishingBowController` now sits disabled on `Player.prefab → SidekickSyntyCharacter` with the arrow wired; the pond enables it instead of `AddComponent`.
  - `Powerups/WaveUpgradePowerup` (base: Interactable, trigger, light, `Visual`) + 7 `WaveReward_*` variants, one per chest. The `WarBannerRewardSO`s point at the variants. `Spawn()` used to bolt a Light/Interactable/collider/component onto the raw chest mesh.
- **Scene wiring:** Necromancer bubble + arc (Crypt), 4 knight beacons + the magic circle (Sanctuary), `DraftStation.stationLight` (Rest Area), `FishingManager.fishBasePrefab` (Fishing Pond). The Crypt and Sanctuary builders instance the same prefabs.
- **Deleted:** the Necromancer's placeholder skeleton capsule (it now logs an error and skips that skeleton).
- Mockup materials are in `Materials/Mockup/`. Benchmark: 107 passed, 5 failed. All 5 are in files this session didn't touch (Bulwark attack, Bannerman rig, BuildWheel open/close, skull waypoints); listed in the checklist §5.
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md) §5.

**Session 4 done** (batch A, Unity MCP connected). Every `PlayClipAtPoint` in the 10 batch-A scripts is gone, along with the one-shot VFX that went with them. Each event is now one `MMF_Player` on `Player.prefab → SidekickSyntyCharacter` (`DodgeMMF`, `FrostStepMMF`, `EarthSplitterMMF`, `EarthSplitterRockMMF`, `OutOfAmmoMMF`, `MaceShockwaveMMF`, `MaceSlamMMF`, `ArmourEquipMMF`, `IceShatterMMF`, `ThermalShockMMF`, `SuperconductorMMF`), called with `PlayFeedbacks(worldPos)`. Missing refs `LogError` in `Start` but don't disable gameplay.
- **House pattern for positional one-shots** (reuse it in batches B-F): MMSoundManager Sound (2D, same clip and volume as before) + Particles Instantiation in `OnDemand` mode, `CachedRecycle` off, `PositionMode = Script`, `NestParticles` off, `ForceStopAction = Destroy`. **Looping VFX prefabs never reach a stop action**, so wrap them in a variant carrying `MMTimedDestruction` (`VFX/FrostStepBurst` 2 s, `VFX/ArmourEquipPoof` 3 s: the old `Destroy(vfx, t)` lifetimes).
- **`ElementalEffectsManager`** (on `Player.prefab`) gained `iceShatterFeedback`, `thermalShockFeedback` and `superconductorFeedback`, plus `PlayAt(feedback, pos)`. The bow/axe Ice Shards, Inferno burst and Storm Eye zap use them. Its raw VFX/`AudioClip` fields are marked legacy: `EnemyStatusManager`, `DamageTrigger`, `BurningOilZone` and `SurvivorsSpawner` still read them. Batches B-D should add feedbacks here (plasma overload, status applied, discord) and delete the raw fields once the last reader is gone.
- **Bugs found on the way:** `MaceUltimate` used `GetComponentInChildren<MMF_Player>()` for its "screenshake", which on the Player is the hero's damage **flicker**; it now has a serialized `slamFeedback` (sound + rock burst + the mace's Cinemachine impulse). `PlayerAmmo` wasn't on the prefab (`Player.cs` `AddComponent`s it), so its clips were always null. It's on `SidekickSyntyCharacter` now. Its pickup sound was deleted (the pickup's own MMF plays one), and the dry-fire click is a 0.25 s cooldown MMF. `EarthSplitterMMF` gained the mace's impulse; before, the feedback was empty and only the clip played.
- `FlameZone.burnTickFeedback` is optional; its old VFX/clip were never wired and the prefab owns the looping fire.
- **Left in batch-A scripts (not audio):** `PlayerBow` `config.headExplosionPrefab` (one-shot VFX on a config SO) and the `PlayerDodge` per-element dash trail (parented to the player for the dash). Fold them into batch B or a small A2. Arrows, tracer, Earth Splitter telegraph and armour meshes are gameplay objects, not feedback.
- `Player.cs` still `AddComponent<PlayerAmmo>()` as a fallback. It's now dead on the real prefab and would log the unassigned-feedback error if it ever fired.
- Benchmark: 109 passed, 3 failed (Bulwark attack, Bannerman rig, skull waypoints, all pre-existing; BuildWheel open/close passes again).
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md) §6.

**Session 5 done** (batch B, Unity MCP connected). No `PlayOneShot`, `PlayClipAtPoint`, `MMSoundManagerSoundPlayEvent` or one-shot `Instantiate(vfx)` is left in the batch-B scripts. Also covered: `ImpulseHitFeedback` (same family, live on the Player) and batch A's leftover `PlayerBow` head explosion.
- **New house feedback `MMF_PooledParticleBurst`** (`DamageSystem/`). It takes a system from `ParticlePool` and plays it or emits into it. Intensity (0-1) picks emit count, speed multiplier (relative to the prefab's own) and scale from ranges. With `UseOwnerRotation`, the caller aims the burst by rotating the MMF player's GameObject just before `PlayFeedbacks`. Use it for any damage-scaled or directional burst in batches C-F.
- **Player** (`Player.prefab`): each melee weapon has `WooshMMF`/`HitMMF`/`CritHitMMF`/`InanimateHitMMF` children, and `BowHitFeedback` has `HitMMF`/`CritHitMMF`/`VulnerableHitMMF`. Their AudioSources are gone. `SidekickSyntyCharacter` gained `ImpulseBlastHitMMF` (`PlayerBow.impulseBlastHitFeedback`, replacing `BowSO.headExplosionPrefab`), `ImpulseHitMMF` (an MMF Light on its own child light, moved to the hit point), plus `PlasmaOverloadMMF`, `StatusAppliedMMF`, `FrozenMMF` and `DiscordAppliedMMF` on `ElementalEffectsManager`.
- **Enemies**: `Goblin Enemy (Base)` got a `Feedbacks` child with `KnockdownMMF`, `FlingMMF` (thud + `KnockbackFlashLight` via MMF Instantiate Object), `WallPinMMF` and `RagdollBloodMMF`, and every variant inherits them. The standalone `Bulwark Enemy Variant`, `Goblin Brute Enemy`, `Goblin Enemy Variant` and `Training Dummy Goblin` got their own. `Goblin Warrior` has its own `FlingScreamMMF` (its 8 screams) and overrides `flingFeedback` to point at it.
- **Config moved out of SOs**: `KnockbackConfigSO` lost the knockdown/flying VFX and SFX, the light-flash settings and the wall-pin SFX/VFX. The flash colour, intensity, range and fade are now baked into `VFX/KnockbackFlashLight.prefab`, and `FlashLightDimmer` reads its own Light. `RagdollConfigSO.bloodParticlePrefab` is gone too.
- **Deleted dead code**: `RagdollImpactAudio` (nothing added it) and `EnemyRagdoll.impactSounds` (empty on every prefab, so ragdoll impacts never made a sound). Also `KnockbackReceiver.landingVfxPrefab`/`landingSfx` (never wired anywhere) and `RagdollBloodImpact.TriggerDirectImpact` (no callers). `SwordHitFeedback` was removed from the Meta Area pedestal's display sword.
- **`ElementalEffectsManager`**: `thermalShockVfx`/`Sfx`, `plasmaOverloadSfx`, `frozenSfx` and `discordAppliedSfx` deleted. Remaining legacy readers: `fireStatusVfx` (CatapultProjectile, FortArrowProjectile, RollingFireball, PlayerDodge), `iceStatusVfx` (FortArrowProjectile, SlipperyIceZone), `plasmaOverloadVfx` (RollingFireball), `superconductorVfx` (BurningOilZone, CatapultStormCloud, FortArrowProjectile, SurvivorsSpawner), and `statusAppliedSfx`/`superconductorSfx` (BurningOilZone, SurvivorsSpawner).
- **Left in batch-B scripts on purpose:** `EnemyStatusManager`'s fire/ice/frozen/discord status visuals. They're parented to the enemy for as long as the status lasts, so they're state visuals rather than one-shot feedback (like the `PlayerDodge` dash trail). Revisit if a looping-VFX MMF pattern turns up.
- **Bugs found on the way:** the Impulse light pulse never showed, because it cloned an *inactive* child light. Plasma Overload's `FX_Fireball_01` loops and was never destroyed. Pooled blood kept the scale and speed the ragdoll code left on it (shared `FX_BloodSplat_01` pool, and ragdoll speed compounded with `*=`). The burst now always sets both from the prefab.
- **Sound spatialisation:** player-side hits are 2D (their AudioSources were 0.1 / 1 spatial, but on the hero). Enemy-side sounds (knockback, wall pin, status applied, frozen, discord, plasma) are fully 3D, matching `PlayClipAtPoint`. Old `PlayOneShot` volume scales above 1 (1.2-1.8) can't be reproduced, because MMSoundManager caps at 1.
- Benchmark: 107 passed, 5 failed. It's the same 5 as session 3: Bulwark attack, Bannerman rig and skull waypoints, plus BuildWheel open/close, which flips between runs (the objective-completion check flipped once too). The console's `Destroy may not be called from edit mode` comes from `BubbleShield`'s `PlayClipAtPoint` during the edit-mode benchmark (batch C).
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md) §7.

**Session 6 done** (batch C, Unity MCP connected). No `PlayOneShot`, `PlayClipAtPoint` or one-shot `Instantiate(vfx)` is left in the 20 batch-C scripts (plus `BubblerCaster`, which now owns the bubble's feedbacks). Every clip/VFX pair became one `MMF_Player` field, played with `PlayFeedbacks(worldPos)`, using the session-4 house pattern. All sounds are 3D, like `PlayClipAtPoint`.
- **Required (LogError if empty, gameplay unaffected), authored and wired:** `LightningBall`/`HomingOrb`/`BoulderProjectile.impactFeedback`, `LightningStormZone.strikeFeedback` (an `*MMF` child on each prefab); `DynamiteProjectile.fuseFeedback`/`explosionFeedback`; `GoldenGoblin.deathFeedback` (`Feedbacks/GoldenDeathMMF` on the goblin base + the two standalone goblins, and `GoldenGoblinFlee` points at the inherited one); `AssassinAttack.slashFeedback`; `SlayerDashAttack.smashFeedback`, `SlayerStompCrusher.crushFeedback`; `BubblerCaster.shieldBlockFeedback`; `SurvivorsSpawner.cleanupLightningFeedback`; `EnemyIntroController.defaultRoarFeedback` (the prefab + the Survivors scene's standalone spawner/controller).
- **Optional (empty = silent, as before, because nothing was ever wired):** hook, toxic pool, arrow barrage, Storm Witch orb drop, impulse goblin death, Bannerman banner break, Kombusta ignite, bubble break, Assassin windup, spawner horn.
- **Config moved off SOs**: `CaptainKombustaSO` lost `explosionVfxPrefab`/`explosionSfx`/`fuseSfx`/`igniteSfx` (`DynamiteProjectile.Launch` lost those params), `BubbleShieldSO` lost its block/break audio + VFX, `GoldenGoblinFleeSO` its death/flee audio + VFX, `WaveConfigSO` the horn. `EnemyManifest` no longer wires any of them.
- **`BubbleShield`** is added at runtime, so it can't own authored players. `BubblerCaster` holds them and passes them to `Initialize` (optional params; the benchmark passes none, which also removed the edit-mode `Destroy` spam session 5 traced to it).
- **`SpecialEnemyIntro`**: `roarSound`/`roarVolume`/`roarDelay`/`audioSource` became `roarFeedback` (`HasRoar`, `PlayRoar()`). Roar players force unscaled time because the intro freezes the timescale. The Slayer's `RoarAudio` child is deleted.
- **`ElementalEffectsManager`**: `SurvivorsSpawner` no longer falls back to `superconductorVfx`/`superconductorSfx`/`statusAppliedSfx`. The remaining legacy readers are all batch D (`BurningOilZone`, `CatapultProjectile`, `CatapultStormCloud`, `FortArrowProjectile`, `RollingFireball`, `SlipperyIceZone`) plus `PlayerDodge`'s `fireStatusVfx` trail.
- **Left in batch-C scripts on purpose (state visuals / gameplay objects):** Kombusta's fire aura, Assassin whirlwind/stun VFX, Slayer dash trail, and the telegraphs. They're parented for their duration, like the `EnemyStatusManager` visuals.
- **Bug fixed on the way:** none of these one-shot bursts set a stop action, so every lightning, boulder, gold and blast effect stayed in the scene forever after it faded. The `ForceStopAction = Destroy` particles clean up now. The fleeing Golden Goblin (its SO was empty) now gets the golden coin burst.
- Benchmark: 108 passed, 4 failed (same known set).
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md) §8.

**Session 7 done** (batch D, Unity MCP connected). No `PlayClipAtPoint`, direct camera shake or one-shot `Instantiate(vfx)` is left in the 15 batch-D scripts. Same house pattern, all sounds 3D.
- **Towers** (`Defenses/Defense_*.prefab`): each has `Feedbacks/RepairMMF`, `UpgradeMMF` and `BreakMMF` (`DefenseStructure.repairFeedback`/`upgradeFeedback`/`breakFeedback`), plus `FireMMF` (arrow, ballista, net thrower), `NetImpactMMF`, `SpillMMF` and `ImpaleMMF`. `ValidateFeedbackReferences` is now `protected virtual`, so each subclass logs its own missing players. The catapult's `fireFeedback` is optional (it never had a sound).
- **Projectiles/payloads**: `CatapultBoulder → ExplosionMMF` (boom + fire burst + the old `MMCameraShakeEvent` values as an MMF Camera Shake), `NetProjectile → ImpactMMF`, `RollingFireball → StrikeMMF`/`ExplodeMMF`, `CatapultStormCloud → StrikeMMF`. HUD `BuildWheelModal/BuildMMF` (`BuildWheelUI.buildFeedback`).
- **Timed VFX variants** (the source prefabs loop): `VFX/WoodImpactBurst` 2.5 s, `CatapultExplosionBurst` 4 s, `FireBurst` 2 s, `FireballBurst` 1.5 s.
- **Optional, never authored (empty = silent, as before):** `CatapultDefense.fireFeedback`, `BurningOilZone.sizzleFeedback`, `SlipperyIceZone.slipFeedback`.
- **`ElementalEffectsManager`**: `plasmaOverloadVfx`, `statusAppliedSfx` and `superconductorSfx` are deleted. `superconductorVfx` became `lightningTrailVfx` (`FormerlySerializedAs`), a state visual on lightning tower arrows. `BurningOilZone`'s shock plays `superconductorFeedback`. The remaining VFX fields are all state visuals (enemy status, arrow trails, the fireball's fire, the ice zone's frost, the dash trail).
- **Dead code deleted:** `FortArrowProjectile`'s hit sound (no caller ever passed one), `RollingFireball.rollLoopSfx`/`rollAudioSource`, `NetProjectile.groundNetVfxPrefab` and `NetThrowerDefense.netVfxPrefab` (both empty everywhere), `TowerPlot.holyLightVfxPrefab`/`woodImpactSfx` (never read). `DefensesRevampSetup` no longer sets removed fields. Benchmark 20C checks `explosionFeedback`.
- **Left on purpose:** `OilVatDefense.oilPoolVfxPrefab` is the damaging `BurningOilZone`, a gameplay spawn despite its name. `RollingFireball`'s `FireTrailSegment` (a code-built damage object with no visuals).
- **Bugs fixed on the way:** tower break, rolling-fireball strike (plasma fireball) and burn-out (fire) bursts loop and were never destroyed.
- Benchmark: 106 passed, 6 failed (the known set, with both flaky ones failing). BuildWheel open/close fails whenever the Survivors scene is open: the test creates its own wheel but reads the static `Instance`, which finds the scene HUD's wheel first. Plan-11 ticket.
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md) §9.

**Session 8 done** (batch E, Unity MCP connected). No `PlayClipAtPoint`, `PlayOneShot`, `MMSoundManagerSoundPlayEvent` or one-shot `Instantiate(vfx)` is left in the 16 batch-E scripts, apart from the looping state audio below. Sounds keep their old spatialisation: the old MMSoundManager calls were 2D (`MMSoundManagerPlayOptions.Default`), the old `PlayClipAtPoint` calls 3D.
- **Folded into existing players:** the objectives already had MMF players next to their raw clips. `BatteringRam.impactFeedback` gained the boom + splinters, `DestructibleCatapult → DeadFeedback` the explosion sound + dark blast, `PrisonerCage → BreakFeedback` the dust burst.
- **New players:** `BatteringRam → Feedbacks/DeathMMF`, `RescuedPrisoner → CheerMMF`/`PoofMMF`, HUD `Wave Cleared Text → WaveClearedMMF` (random chime) / `NewQuestMMF` (horn), Rest Area `Station_1_Well → DrinkMMF`, Fishing Pond `FishingMinigameManagers → CountdownMMF`/`FrenzyStartMMF`.
- **Feedback prefabs** (nested wherever a scene holds its own copy, so tuning is in one place): `Waves/GateDestructionMMF` (every gate, 12 scenes including the binary castles), `Banners/WarBannerBurnMMF` (the banner prefab + Ancient Garden's unpacked banners), `Objectives/SupplyWagonArrivalMMF`, `Managers/BountyRewardMMF` (the manager prefab + the Survivors scene's own manager), `UI/StatCountTickMMF` and `StatRowCompleteMMF` (the death screen + the Survivors scene's own panel). `BuildCastleLevels` and `SetupSurvivorsSceneTool` nest the gate one.
- **VFX variants:** `VFX/FireExplosionUnscaled`, `ExplosionLargeDarkUnscaled`, `ImpactLargeUnscaled`, `WoodImpactBurstUnscaled` (2.5 s) replace the code that switched each spawned system to unscaled time; the gate/ram death players also run unscaled. `DustBigBurst` (3 s) because `FX_Dust_Big_01` loops.
- **`SurvivorsStatsPanelUI`**: the count-up's rising pitch is kept by setting the tick player's sound pitch before each play. Its `AudioSource` `AddComponent` fallback is gone.
- **Optional, never authored (empty = silent, as before):** `WaveUpgradePowerup.spawnFeedback`/`claimFeedback`, `DraftStation.openDraftFeedback`, `FishingBowController.shootFeedback`, `FishingManager.timeUpFeedback`/`catchFeedback`.
- **Dead code/fallbacks deleted:** `Gate`'s `Reset`/`OnValidate` `LoadAssetAtPath` (so the audit exception is gone), `FishingManager`'s `Resources.Load` clip fallbacks (the paths don't exist, so the countdown and horn were always silent) and its `AddComponent<AudioSource>`, the never-set siege-engine hit sound and cage hit/break sounds, `GameLoopManager`'s `#if UNITY_EDITOR` duplicate. `AutoWireGameLoopManager`, `BatteringRamSetup` and `BatteringRamTest` follow the new fields.
- **Left on purpose (looping state audio shaped per frame):** `HorseHoofbeatAudio`'s gallop bed, `BatteringRam`/`SupplyWagonEscort` rolling loops, and the wagon's wheel dust. `WarBannerController.burnVfxPrefab` is parented fire for the 3 s teardown (state visual); its sound moved.
- **Bugs fixed on the way:** the ram's impact splinters and the cage's break dust looped and never left the level; the Fishing Frenzy countdown and horn never played.
- **Trap found:** the mechanic benchmark runs in edit mode in the active scene and can't `Destroy` what it spawns, so it leaves `Benchmark_*`/`Test*` objects (and MMF bursts) behind. Saving that scene afterwards commits them; it happened once this session and was reverted. Plan-11 ticket.
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md) §10.

## Refreshed audit (2026-09-26, after session 3)

Sessions 2 (code-built UI) and 3 (code-built world visuals) are done; the visuals grep below now returns only the documented exceptions.

**Documented exceptions (leave):**
- `DefenseAssemblyAnimation` proxy `MeshFilter`/`MeshRenderer`: re-uses the tower prefab's own meshes to animate them.
- ~~`Waves/Gate` `LoadAssetAtPath` in `Reset`/`OnValidate`~~: deleted in session 8 (the gate plays `GateDestructionMMF`).
- `Debug/DiegeticDraftTester`: debug tool. `Editor/`: editor code.
- No longer hits (deleted or fixed since the first grep): `MetaProgressionGridUI`, `VictoryScreenUI`, `SurvivorsGameManager`, `CampaignMapUI`, all `Economy/` pickups.

## Ranked MMF migration batches (direct `PlayOneShot`/`PlayClipAtPoint`/`MMSoundManagerSoundPlayEvent`/camera shake)

Highest traffic first. The counts come from a grep, so a few `.Play()` hits may be particles rather than audio.

| Batch | Area | Scripts |
|---|---|---|
| ~~A~~ | Player (every second of play), **done session 4** | `PlayerAttack`, `PlayerBow`, `PlayerDodge`, `PlayerAmmo`, `AxeProjectile`, `MaceCombatController`, `MaceUltimate`, `PlayerUltimateController`, `FlameZone`, `PlayerArmourManager` |
| ~~B~~ | Hit feedback, **done session 5** | `SwordHitFeedback`, `BowHitFeedback`, `DamageTrigger`, `KnockbackReceiver`, `RagdollBloodImpact`, ~~`RagdollImpactAudio`~~ (deleted), `EnemyStatusManager`, + `ImpulseHitFeedback` |
| ~~C~~ | Common enemies + spawner, **done session 6** | `SurvivorsSpawner`, `GoldenGoblin`(+`Flee`), `ImpulseGoblin`, `AssassinAttack`, `HookProjectile`, `BoulderProjectile`, `LightningBall`, `LightningOrbDropper`, `LightningStormZone`, `ToxicPoolZone`, `HomingOrb`, `SlayerDashAttack`, `SlayerStompCrusher`, `EnemyIntroController`, `SpecialEnemyIntro`, `DestructibleBanner`, `CaptainKombustaController`, `DynamiteProjectile`, `ArrowBarrageZone`, `BubbleShield` |
| ~~D~~ | Towers, **done session 7** | `DefenseStructure`, `BuildWheelUI`, `ArrowTowerDefense`, `FortArrowProjectile`, `BallistaDefense`, `CatapultDefense`, `CatapultProjectile` (**direct shake**), `NetThrowerDefense`, `NetProjectile`, `OilVatDefense`, `BurningOilZone`, `SpikeTrapDefense`, `RollingFireball`, `SlipperyIceZone`, `CatapultStormCloud` |
| ~~E~~ | Waves, objectives, hub UI, **done session 8** | `GameLoopManager`, `WaveClearedBannerUI`, `WarBannerController`, `Gate`, `WaveUpgradePowerup`, `BatteringRam`, `DestructibleSiegeEngine`, `PrisonerCage`, `RescuedPrisoner`, `SupplyWagonEscort`, `SurvivorsStatsPanelUI`, `DraftStation`, `WellStation`, `HorseHoofbeatAudio`, `FishingBowController`, `FishingManager` |
| F | Bosses (once per run) | `ArmoredKnightAI`, `CryptSkeletonAI`, `NecromancerBossController` (**hand-rolled shake coroutine**), `NecromancerConfrontationUI`, `PrincessBossController` |

Skip `TrainingDummy` (debug).

## Code-built visuals (non-Editor scripts, from grep; re-run to refresh)

**Clean as of session 3.** The grep returns only the documented exceptions: `DefenseAssemblyAnimation` (proxy meshes), `Waves/Gate` (edit-time `Reset`/`OnValidate`) and `Debug/DiegeticDraftTester`. Any new hit is a regression.

Grep used: `CreatePrimitive|AddComponent<(MeshRenderer|MeshFilter|LineRenderer|ParticleSystem|Light|Image|TextMeshProUGUI|Canvas)>|LoadAssetAtPath|Ensure(Visuals|UI|Canvas)|new GameObject("…", typeof(RectTransform)`

**Per file:**
- If an authored prefab already exists: delete the fallback, add `LogError`.
- If no prefab exists: build a prefab mockup (MCP or a temporary editor script) and list it under "Needs Lance in the Editor" as "agent mockup, human review", or as "human intervention" if it's art that needs Lance.
- Some hits may be legitimate, e.g. a pooled runtime `LineRenderer` configured from a prefab, or DevConsole debug UI. Note and skip those.

## Direct audio / VFX / shake (non-MMF)

- 74 scripts touch `AudioSource` playback directly; camera shake is called directly in `NecromancerBossController`, `CatapultProjectile`, `DefenseAssemblyAnimation`.
- Don't mass-migrate: produce a ranked list, highest-traffic first (player weapons, hit feedback, enemies, towers), and migrate in batches, each adding `MMF_Player` refs on the prefab.
- After each batch, Lance tunes the feel (00 §D).
- Use the `feel-integration` skill (port it from `.agents/skills/` in plan 10).

## Acceptance

The grep returns only documented exceptions, and new code reviews reject direct audio/VFX/shake.

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md).
