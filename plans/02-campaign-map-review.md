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
- [x] **Remove code-built UI** still in `CampaignMapUI`/`CampaignNodeButtonUI`: `CreateProceduralNodeButton`, the path line `new GameObject` fallback, and the `InitializeReferences` helpers if only editor setup uses them (move that wiring into the editor script). Missing prefab → `Debug.LogError` + early out.
- [x] Flag `CampaignNodeButton.prefab` / `CampaignPathLine.prefab` for human UI review (Synty art, Texturina/Grenze).
- [x] **Necromancer confrontation in builds.** (Code done; scene cleanup + build check are Lance's, below.) Move `DelayedConfrontationStarter` out of `Editor/BuildNecromancerCryptScene.cs` into a runtime script (e.g. `Bosses/ConfrontationTrigger.cs`), keeping the scene reference valid (same class name, or re-add via MCP). Verify in a player build.
- [x] **Campaign end:**
  - Defeating either final boss completes the correct node.
  - It shows the end screen (reuse `DeathScreen.ShowVictory` with a campaign-complete mode, not the orphaned `VictoryScreenUI`) and returns to Meta with the run cleared.
  - Obey should complete the crypt node before deploying.
- [x] **Campaign-end hook for the demo:** add a per-node flag (e.g. `isDemoEnd`) or a graph-level "demo cutoff tier" that plan 07 will use, so the end-screen path is shared.
- [x] Update `Campaign/CLAUDE.md`: delete the resolved "known problems".

## Acceptance

Map shows exactly 19 prefab-built nodes, correct statuses after clearing, sector completion unlocks children, Necromancer choice appears in a player build, campaign end returns to Meta.

## Session notes (2026-09-25)

**What changed**
- `CampaignManager`: new `CompleteCurrentNodeAndContinue()` (replaces `CompleteCurrentNodeAndOpenMap`) is the single node exit: map, or `EndCampaign()` (run wiped → Meta) when the node `EndsCampaign`. New `EnterBranchNode(id, loadScene)` for Obey/Defy. `DeployToNode`'s silent Survivors-scene fallback and the runtime default-graph fallback are now `Debug.LogError`s. Added `HasInstance` + `OnDestroy` so teardown doesn't auto-spawn a manager.
- `CampaignNodeSO.endsCampaign` flag + `EndsCampaign` property (flag, or no next nodes). **Plan 07: tick `endsCampaign` on the cutoff nodes; nothing else needed.**
- `DeathScreen.ShowVictory` switches to "CAMPAIGN COMPLETE!" / "RETURN TO SANCTUARY" when the current node ends the campaign.
- Both boss controllers show that screen on death instead of completing the node themselves.
- `NecromancerConfrontationUI` opens itself on scene start (replaces the Editor-only `DelayedConfrontationStarter`, now deleted). I folded the trigger into the UI rather than adding `ConfrontationTrigger.cs`, so the scene needs no new wiring (`boss` is auto-found). Obey now completes the Crypt node; Defy completes the Crypt and moves to `tier8_necromancer_boss`, so the right node gets completed either way.
- `CampaignMapUI` / `CampaignNodeButtonUI` / `CampaignTooltipUI`: removed `CreateProceduralNodeButton`, the `new GameObject` path line, all `InitializeReferences` helpers, and the runtime `BuildDefaultGraph()` call on an empty graph. Missing prefab/container/graph → `Debug.LogError` + early out.
- `FishingManager` returns via `CompleteCurrentNodeAndContinue` / loading screen.
- Deleted `Editor/BuildAndCaptureCampaignMap.cs`: stale (old node ids, embedded a runtime graph in the scene, no prefabs, wrote screenshots to an old Antigravity folder). `SetupCampaignPrefabsAndAssets` is the one map setup tool.

**Review findings fixed**
- `tier7_fishing_pond` had no next nodes: picking it stranded the run (and with the new rules would have ended the campaign). Now links to the Crypt, in code and in the asset.
- Node buttons deployed twice per mouse click (`Button.onClick` + `IPointerClickHandler` both fired `SelectNode`).
- `SetupCampaignPrefabsAndAssets`: re-running it left `tiers[].nodes` pointing at unsaved temp nodes, and it silently overwrote hand-edited node assets (including from the `[InitializeOnLoadMethod]` path when any prefab was missing). It now re-links tiers and asks before regenerating.

**Review findings not fixed (for later plans)**
- The boss scenes include `GameLoopManager.prefab`. Worth checking in playtest that it doesn't start waves/banners there (plan 06/08).
- Boss controllers still grant their own blood/metal on death *and* the node pays its rewards on completion, so the payout is doubled. Could be intended; your call.
- `VictoryScreenUI` is still orphaned (plan 08). Boss controllers still use direct `Instantiate`/camera-shake/`AudioSource` calls, and so does `NecromancerConfrontationUI` (plan 09 MMF audit).
- A standalone Crypt play (no campaign run) → Defy/Obey quietly starts a partial campaign run. Editor-only, harmless.

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/02-campaign-map.md`](editor/02-campaign-map.md).
