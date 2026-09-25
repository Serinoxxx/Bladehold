# 09: Code-built visuals + MMF audit

**Rules** (`/CLAUDE.md` Conventions):
- No visuals or fallback UI built in code.
- All audio, VFX, screenshake and flashes go through `MMF_Player`.

This plan audits and migrates the existing violations. Split it into several sessions by folder.

## Progress

| Session | Scope | Status |
|---|---|---|
| 1 | Runtime `LoadAssetAtPath` fallbacks (they silently return null in a player build) | **Done 2026-09-26** |
| 2 | Code-built UI → prefab mockups | Next. Needs Unity MCP (prefab mockups) |
| 3 | Code-built world visuals (primitives, telegraph LineRenderers, lights) | Needs MCP or Lance for art |
| 4+ | Direct audio/VFX/shake → MMF, one batch per session (ranked list below) | After 2-3 |

**Session 1 done:**
- Deleted the Editor-only duplicate pickup sounds from `AmmoPickup`, `Coin`, `HealthPack`, `ImpulseOrb`, `LightningOrb` and `PlayerSummonMount`. Their MMF players already carry the sound; AmmoPickup's player existed but wasn't wired, so it's wired now.
- `DefenseStructure`, `BuildWheelUI`, `ObjectiveWaypointTrackerUI`, `KillRemainingEnemiesObjective`, `SupplyWagonEscort`, `PlayerInteraction`, `WaveClearedBannerUI`: fallbacks deleted and replaced by `LogError`. Any empty prefab fields the fallbacks were covering are now wired in YAML (tower `breakVfxPrefab` ×6; HUD build-wheel popup/SFX/VFX; tracker `noSupplyIcon`/`cleanupEnemySkullIcon`; `SupplyWagon.prefab` `goldBagPrefab`).
- `UIClickFeedback` is MMF-only now (the `customClickSound` path and its Editor fallback are gone). Every wired click MMF player already had a sound.
- `DefenseAssemblyAnimation` is an authored prefab (`Defenses/DefenseAssemblyAnimation.prefab`, referenced by `TowerPlot.assemblyAnimationPrefab`) with three `MMF_Player`s, replacing `AddComponent` + loaded clips/particles + `MMCameraShakeEvent`. Dead ghost-blueprint code removed. Benchmark 20D updated.
- Editor work: [`plans/editor/09-visuals-mmf.md`](editor/09-visuals-mmf.md).

## Refreshed audit (2026-09-26, after session 1)

**Session 2, code-built UI** (each needs a prefab mockup: Synty art, Texturina/Grenze, human review):
`ObjectiveWaypointTrackerUI` (marker template), `LoadingScreenManager` (whole fallback canvas), `BowAmmoUI` (counter/icon/warning + its sprite `LoadAssetAtPath`), `ActiveTowersHUDUI` (panel + rows), `SurvivorsPlayerInfoSidebarUI` (rows), `EnemyIntroUI` (subtitle), `BowCrosshairUI` (reticle), `SkillTooltip` (canvas), `PrincessBossController` (cast bar).

**Session 3, code-built world visuals:**
`NecromancerBossController` (bubble sphere, sweep-arc LineRenderer, placeholder skeleton capsule), `ArmoredKnightAI` (beacon LineRenderer), `PrincessBossController` (magic circle LineRenderer), `BubbleShield` (sphere), `ArrowBarrageZone` (telegraph LineRenderer), `Captain/DynamiteProjectile` (stick cylinder + fuse LineRenderer), `FishingBowController` (arrow cylinder), `FishingManager` (fish capsule), `CatapultStormCloud` (puffs), `RollingFireball` (sphere), `SlipperyIceZone` (disc), `SpawnIndicator` (cylinder), `WaveUpgradePowerup` (sphere + lights), `ThrowingAxeUltimate` (blade cube), `KnockbackReceiver` (light), `RestArea/DraftStation` (light).

**Documented exceptions (leave):**
- `DefenseAssemblyAnimation` proxy `MeshFilter`/`MeshRenderer`: re-uses the tower prefab's own meshes to animate them.
- `Waves/Gate` `LoadAssetAtPath` in `Reset`/`OnValidate`: edit-time auto-wire that's saved into the asset, so builds are fine. Its sound/VFX still need MMF (batch E).
- `Debug/DiegeticDraftTester`: debug tool. `Editor/`: editor code.
- No longer hits (deleted or fixed since the first grep): `MetaProgressionGridUI`, `VictoryScreenUI`, `SurvivorsGameManager`, `CampaignMapUI`, all `Economy/` pickups.

## Ranked MMF migration batches (direct `PlayOneShot`/`PlayClipAtPoint`/`MMSoundManagerSoundPlayEvent`/camera shake)

Highest traffic first. The counts come from a grep, so a few `.Play()` hits may be particles rather than audio.

| Batch | Area | Scripts |
|---|---|---|
| A | Player (every second of play) | `PlayerAttack`, `PlayerBow`, `PlayerDodge`, `PlayerAmmo`, `AxeProjectile`, `MaceCombatController`, `MaceUltimate`, `PlayerUltimateController`, `FlameZone`, `PlayerArmourManager` |
| B | Hit feedback | `SwordHitFeedback`, `BowHitFeedback`, `DamageTrigger`, `KnockbackReceiver`, `RagdollBloodImpact`, `RagdollImpactAudio`, `EnemyStatusManager` (7 calls) |
| C | Common enemies + spawner | `SurvivorsSpawner`, `GoldenGoblin`(+`Flee`), `ImpulseGoblin`, `AssassinAttack`, `HookProjectile`, `BoulderProjectile`, `LightningBall`, `LightningOrbDropper`, `LightningStormZone`, `ToxicPoolZone`, `HomingOrb`, `SlayerDashAttack`, `SlayerStompCrusher`, `EnemyIntroController`, `SpecialEnemyIntro`, `DestructibleBanner`, `CaptainKombustaController`, `DynamiteProjectile`, `ArrowBarrageZone`, `BubbleShield` |
| D | Towers | `DefenseStructure`, `BuildWheelUI`, `ArrowTowerDefense`, `FortArrowProjectile`, `BallistaDefense`, `CatapultDefense`, `CatapultProjectile` (**direct shake**), `NetThrowerDefense`, `NetProjectile`, `OilVatDefense`, `BurningOilZone`, `SpikeTrapDefense`, `RollingFireball`, `SlipperyIceZone`, `CatapultStormCloud` |
| E | Waves, objectives, hub UI | `GameLoopManager`, `WaveClearedBannerUI`, `WarBannerController`, `Gate`, `WaveUpgradePowerup`, `BatteringRam`, `DestructibleSiegeEngine`, `PrisonerCage`, `RescuedPrisoner`, `SupplyWagonEscort`, `SurvivorsStatsPanelUI`, `DraftStation`, `WellStation`, `HorseHoofbeatAudio`, `FishingBowController`, `FishingManager` |
| F | Bosses (once per run) | `ArmoredKnightAI`, `CryptSkeletonAI`, `NecromancerBossController` (**hand-rolled shake coroutine**), `NecromancerConfrontationUI`, `PrincessBossController` |

Skip `TrainingDummy` (debug).

## Code-built visuals (non-Editor scripts, from grep; re-run to refresh)

- **Bosses:** `ArmoredKnightAI`, `NecromancerBossController`, `PrincessBossController`
- **Economy pickups:** `AmmoPickup`, `Coin`, `HealthPack`, `ImpulseOrb`, `LightningOrb`, `SupplyBox`
- **Enemies:** `ArrowBarrageZone`, `BubbleShield`, `Captain/DynamiteProjectile`
- **Fishing:** `FishingBowController`, `FishingManager`, `UI/FishingTallyUI` (overlaps plan 04)
- **Fort:** `CatapultStormCloud`, `DefenseAssemblyAnimation`, `DefenseStructure`, `RollingFireball`, `SlipperyIceZone`
- **Objectives:** `KillRemainingEnemiesObjective`, `SupplyWagonEscort`
- **Player:** `PlayerInteraction`, `PlayerSummonMount`, `ThrowingAxeUltimate`
- **UI:** `ActiveTowersHUDUI`, `BowAmmoUI`, `BowCrosshairUI`, `BuildWheelUI`, `EnemyIntroUI`, `MainMenu/MetaProgressionGridUI`, `ObjectiveWaypointTrackerUI`, `SkillTooltip`, `SurvivorsPlayerInfoSidebarUI`, `Transitions/LoadingScreenManager`, `UIClickFeedback`, `VictoryScreenUI`, `WaveClearedBannerUI`
- **Other:** `Upgrades/SurvivorsGameManager`, `Waves/Gate`, `Waves/SpawnIndicator`, `Waves/WaveUpgradePowerup`, `Debug/DiegeticDraftTester`, plus `Campaign/CampaignMapUI` (handled in plan 02)

Grep used: `CreatePrimitive|AddComponent<(MeshRenderer|MeshFilter|LineRenderer|ParticleSystem|Image|TextMeshProUGUI|Canvas)>|LoadAssetAtPath|Ensure(Visuals|UI|Canvas)|new GameObject("…", typeof(RectTransform)`

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
