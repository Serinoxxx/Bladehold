# 02: Campaign Map review + fix

**Goal:**
- Code review of `Campaign/` (all files), `Bosses/NecromancerConfrontationUI.cs`, `Bosses/*BossController.cs` (campaign hooks only), and `Editor/BuildAndCaptureCampaignMap.cs`.
- Fix the setup problems and the missing end of the campaign.
- Start by reading `Campaign/CLAUDE.md` ("Known map setup problems").

## Review focus

- Graph ownership and lifetime: the scene-embedded `CastleCampaignGraph_Runtime` SO vs `CampaignManager`'s runtime fallback, and whether scene unload leaves a destroyed reference.
- `CampaignManager` singleton edge cases: auto-create in `Instance`, duplicates across scenes, `RestoreFromRunSession` vs `StartCampaignRun` ordering.
- Node completion rules: bosses completing the wrong node (Defy completes `tier8_crypt_sanctum`, Obey never completes it), `DeployToNode`'s silent Survivors Scene fallback.
- Code-built UI in `CampaignMapUI` (`CreateProceduralNodeButton`, the `new GameObject` path lines) and in `CampaignNodeButtonUI.InitializeReferences` / `CampaignMapUI.InitializeReferences`.
- Loading screens: `FishingManager` loads the map with a plain `SceneManager` call.

## Fix tasks

- [x] ~~Graph authoring + prefabs + stale buttons~~: done upstream in `fa185dc7e` (graph asset in `Resources/`, node/path prefabs wired, containers cleared). **Review that commit** as part of this plan: `SetupCampaignPrefabsAndAssets.cs`, `CampaignMapUI`/`CampaignNodeButtonUI` changes, and the `Application.isPlaying` guards in `CampaignManager`.
- [ ] **Remove code-built UI** still in `CampaignMapUI`/`CampaignNodeButtonUI`: `CreateProceduralNodeButton`, the path line `new GameObject` fallback, and the `InitializeReferences` helpers if only editor setup uses them (move that wiring into the editor script). Missing prefab → `Debug.LogError` + early out.
- [ ] Flag `CampaignNodeButton.prefab` / `CampaignPathLine.prefab` for human UI review (Synty art, Texturina/Grenze).
- [ ] **Necromancer confrontation in builds.** Move `DelayedConfrontationStarter` out of `Editor/BuildNecromancerCryptScene.cs` into a runtime script (e.g. `Bosses/ConfrontationTrigger.cs`), keeping the scene reference valid (same class name, or re-add via MCP). Verify in a player build.
- [ ] **Campaign end:**
  - Defeating either final boss completes the correct node.
  - It shows the end screen (reuse `DeathScreen.ShowVictory` with a campaign-complete mode, not the orphaned `VictoryScreenUI`) and returns to Meta with the run cleared.
  - Obey should complete the crypt node before deploying.
- [ ] **Campaign-end hook for the demo:** add a per-node flag (e.g. `isDemoEnd`) or a graph-level "demo cutoff tier" that plan 07 will use, so the end-screen path is shared.
- [ ] Update `Campaign/CLAUDE.md`: delete the resolved "known problems".

## Acceptance

Map shows exactly 19 prefab-built nodes, correct statuses after clearing, sector completion unlocks children, Necromancer choice appears in a player build, campaign end returns to Meta.
