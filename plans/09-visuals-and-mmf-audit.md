# 09: Code-built visuals + MMF audit

**Rules** (`/CLAUDE.md` Conventions):
- No visuals or fallback UI built in code.
- All audio, VFX, screenshake and flashes go through `MMF_Player`.

This plan audits and migrates the existing violations. Split it into several sessions by folder.

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
