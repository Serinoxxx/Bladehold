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
| 5+ | Direct audio/VFX/shake → MMF, one batch per session (ranked list below) | Next: batch B |

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

## Refreshed audit (2026-09-26, after session 3)

Sessions 2 (code-built UI) and 3 (code-built world visuals) are done; the visuals grep below now returns only the documented exceptions.

**Documented exceptions (leave):**
- `DefenseAssemblyAnimation` proxy `MeshFilter`/`MeshRenderer`: re-uses the tower prefab's own meshes to animate them.
- `Waves/Gate` `LoadAssetAtPath` in `Reset`/`OnValidate`: edit-time auto-wire that's saved into the asset, so builds are fine. Its sound/VFX still need MMF (batch E).
- `Debug/DiegeticDraftTester`: debug tool. `Editor/`: editor code.
- No longer hits (deleted or fixed since the first grep): `MetaProgressionGridUI`, `VictoryScreenUI`, `SurvivorsGameManager`, `CampaignMapUI`, all `Economy/` pickups.

## Ranked MMF migration batches (direct `PlayOneShot`/`PlayClipAtPoint`/`MMSoundManagerSoundPlayEvent`/camera shake)

Highest traffic first. The counts come from a grep, so a few `.Play()` hits may be particles rather than audio.

| Batch | Area | Scripts |
|---|---|---|
| ~~A~~ | Player (every second of play), **done session 4** | `PlayerAttack`, `PlayerBow`, `PlayerDodge`, `PlayerAmmo`, `AxeProjectile`, `MaceCombatController`, `MaceUltimate`, `PlayerUltimateController`, `FlameZone`, `PlayerArmourManager` |
| B | Hit feedback | `SwordHitFeedback`, `BowHitFeedback`, `DamageTrigger`, `KnockbackReceiver`, `RagdollBloodImpact`, `RagdollImpactAudio`, `EnemyStatusManager` (7 calls) |
| C | Common enemies + spawner | `SurvivorsSpawner`, `GoldenGoblin`(+`Flee`), `ImpulseGoblin`, `AssassinAttack`, `HookProjectile`, `BoulderProjectile`, `LightningBall`, `LightningOrbDropper`, `LightningStormZone`, `ToxicPoolZone`, `HomingOrb`, `SlayerDashAttack`, `SlayerStompCrusher`, `EnemyIntroController`, `SpecialEnemyIntro`, `DestructibleBanner`, `CaptainKombustaController`, `DynamiteProjectile`, `ArrowBarrageZone`, `BubbleShield` |
| D | Towers | `DefenseStructure`, `BuildWheelUI`, `ArrowTowerDefense`, `FortArrowProjectile`, `BallistaDefense`, `CatapultDefense`, `CatapultProjectile` (**direct shake**), `NetThrowerDefense`, `NetProjectile`, `OilVatDefense`, `BurningOilZone`, `SpikeTrapDefense`, `RollingFireball`, `SlipperyIceZone`, `CatapultStormCloud` |
| E | Waves, objectives, hub UI | `GameLoopManager`, `WaveClearedBannerUI`, `WarBannerController`, `Gate`, `WaveUpgradePowerup`, `BatteringRam`, `DestructibleSiegeEngine`, `PrisonerCage`, `RescuedPrisoner`, `SupplyWagonEscort`, `SurvivorsStatsPanelUI`, `DraftStation`, `WellStation`, `HorseHoofbeatAudio`, `FishingBowController`, `FishingManager` |
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
