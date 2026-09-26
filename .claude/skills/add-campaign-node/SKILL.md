---
name: add-campaign-node
description: Use when adding a campaign map node to Bladehold (a new node on the graph, a new CampaignNodeType like Fishing Pond or Rest Area) and/or a new combat sector scene — node asset + graph wiring, map UI, scene routing, RunSession hand-off, battle-scene checklist, build settings, NavMesh and demo gating.
---

# Add a campaign node / sector scene

Read `Assets/Bladehold/Bladehold Scripts/Campaign/CLAUDE.md` first. The graph is data: **`Resources/CampaignGraph.asset` is the source of truth**, one `CampaignNodeSO` asset per node in `Resources/CampaignNodes/Node_<nodeId>.asset` (format settled by plan 02, done 2026-09-25). `CampaignGraphSO.BuildDefaultGraph()` is only the seed that **Bladehold/Campaign/Setup All Campaign Prefabs & Scene** copies over the assets when asked to regenerate. There's no runtime fallback graph.

Decide which case you're in:

| Case | Steps |
|---|---|
| New node of an existing type (another pond, another battle on an existing scene) | 1, 2, 4 |
| New combat sector scene | 1, 2, 4, 5 |
| New node **type** (new kind of stop) | all |

## 1. The node asset (`Campaign/CampaignNodeSO.cs`)

Create via **Create > Scriptable Objects > Campaign > Campaign Node** (or MCP `manage_scriptable_object`) at `Resources/CampaignNodes/Node_<nodeId>.asset`. Fields that matter:
- `nodeId` (unique, `tierN_snake_name`; lookups are case-insensitive), `nodeTitle`/`subtitle`/`description` (also the loading-screen text), `sceneName` (exact scene name, loaded by name).
- `nodeType`, `tierIndex` (1-8). **`tierIndex` is also the sector's threat level** (`Waves/SectorSpawnRules.cs` → `SectorThreat.Current`): it gates roster rows by `minThreat` and grows kill quotas. It also drives the demo cutoff (step 4).
- `mapPosition`: tiers sit roughly 200 px apart on x (tier 1 at x=100, tier 3 at x=500), branches offset on y. Copy a sibling's value and nudge.
- Combat only: `captainName` (`GameLoopManager.SpawnCaptainForWave` picks Kombusta if the name contains "Kombusta", otherwise Fraglob; see `/add-captain`), `difficultyTier`, `bountyType`, `clanBuff`. `GameLoopManager.Start` reads `bountyType` + `difficultyTier` from `CampaignManager.CurrentNode`.
- `goldReward` (into the run) / `bloodReward` / `metalReward` (straight to `SaveData`), all paid by `CampaignManager.CompleteCurrentNode`. `rewardsDescription` is tooltip text only.
- `nextNodes`: children. **Empty = ends the campaign** (`EndsCampaign`), so every non-final node needs at least one child.
- `endsCampaign`: leave off. Its tooltip says "used for the demo cutoff", which is stale: the demo uses the tier helpers (step 4).

## 2. Wire it into the graph (asset **and** seed)

On `CampaignGraph.asset`:
- Add it to `tiers[tier-1].nodes` **and** to `allNodes`. `CampaignMapUI` draws only `allNodes`, and `CampaignGraphSO.GetNodeById` (which backs `CampaignManager.CurrentNode`) searches only `allNodes`. A node that's only in `tiers` never shows on the map, and if you reach it anyway `CurrentNode` is null (no threat, bounty or completion).
- Add it to each parent's `nextNodes`, and fill its own `nextNodes`.
- Never put anything before the tier-1 node in `allNodes`: regeneration sets `rootNode = allNodes[0]`.

Then mirror it in `CampaignGraphSO.BuildDefaultGraph()`: a `CreateNode(...)` call, the `nextNodes.Add` links, `tierN.nodes.Add`, **and `allNodes.Add`**. If you skip this, the next "Regenerate" deletes your node from every link.

Checks: the map shows the node at the right status, and clearing a parent makes it Available. Benchmark section 21A (`Editor/WeaponReachBenchmark.cs`) expects exactly 7 Fishing Pond nodes in the seed graph. Update it if you add or remove a pond.

## 3. A new node type

- **Enum** (`Campaign/CampaignNodeType.cs`): **append only.** Never remove or reorder values: node assets serialize the int. `SupplyRoom` is dead but stays.
- **Deploy** (`CampaignManager.SelectNode`): loads `sceneName` through `Bladehold.UI.LoadingScreenManager.Instance.LoadScene(sceneName, title, subtitle, description, rewardIcon)`. `LoadingScreenManager` spawns itself from `Resources/LoadingScreenManager.prefab`, so don't add it to your scene. If the new type runs the 5-wave battle loop, add it to the `Combat || PreBoss` check that resets `RunSession.CurrentWave = 1`.
- **Exit**: the node's scene must end by calling **`CampaignManager.Instance.CompleteCurrentNodeAndContinue()`** exactly once. That captures the HP ratio, completes the node, pays rewards, unlocks children, then opens the map (or runs `EndCampaign()` for a final node). Copy the pattern from `UI/RestArea/RestAreaGate.HandleReturnToBattle` or `Fishing/FishingManager.CommitRewardsAndReturnToCampaign`, including the `!IsCampaignActive` branch for a scene opened straight from the Editor (`OpenOverviewMap()` or a plain load). Use `CampaignManager.HasInstance` in teardown code so you don't auto-spawn a manager.
- **Map UI**: per-type visuals are `switch`es on `nodeType`:
  - `CampaignNodeButtonUI.GetNodeBgColor`: add a serialized `Color` field.
  - `CampaignNodeButtonUI.Setup` icon block: only `FishingPond` has an icon today (serialized `fishingIcon`). Add a serialized `Sprite` field and a branch.
  - `CampaignTooltipUI.FormatNodeType` / `GetNodeTypeColor`: badge text and colour. `FishingPond` is missing there too and falls through to "Sector" and grey.

  Assign the new sprite/colour on `Bladehold Prefabs/UI/CampaignNodeButton.prefab` (Synty icon from `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/`). **Trap:** `SetupCampaignPrefabsAndAssets.ExecuteAll` rebuilds that prefab from code on every run (`CreateOrUpdateNodeButtonPrefab`), so also wire the new field there or the next run wipes it. The node/path prefabs are still awaiting human UI review, so flag any change for review (Synty art, Texturina headers, Grenze body).
- No visuals built in code and no `AssetDatabase` fallbacks in runtime code: missing refs get `Debug.LogError` + an early return, validated in `Start`.

## 4. RunSession hand-off and demo gating

- **Carried in** (static `Economy/RunSession.cs`, survives scene loads): HP ratio, gold, supply, ammo, draft levels (`InRunUpgradeLevels`), ultimate id + charge, buff fish, campaign ids. `Player.cs` calls `RunSession.RestoreInRunUpgrades(this)` + `ReapplyBuffFishBonuses` on spawn, so any scene with the Player prefab rehydrates automatically.
- **Carried out**: ammo and ultimate charge write through as they change. HP is snapshotted by `CompleteCurrentNodeAndContinue` (`CapturePlayerHealthRatio`). New per-run state goes on `RunSession` and gets reset in `StartNewRun()`. Campaign progress is **not** saved to disk.
- **Demo** (`Demo/DemoConfigSO.cs`, `Resources/DemoConfig.asset`, `campaignCutoffTier` = 4): gating is by `tierIndex` alone, via `DemoConfigSO.IsCampaignNodeLocked(node)` (tier > cutoff: shown as DemoLocked, never Available) and `IsDemoCutoffNode(node)` (tier ≥ cutoff: clearing it shows `DemoEndScreenUI` on the map). A node at tier ≤ 4 is in the demo; deeper is locked. There's nothing to set per node. If new content needs a different gate, add a field + static helper to `DemoConfigSO`. Never add a per-asset "demo" flag.

## 5. A new combat sector scene

1. **Create** it in `Assets/Bladehold/Bladehold Scenes/`. `Bladehold Frozen Pass Scene` / `Bladehold Ancient Garden` are text-YAML battle scenes. The castle scenes (Courtyard, Ramparts, Armory, Great Hall, Dungeons, Conservatory, Throne Antechamber) plus the Crypt/Sanctuary are **binary-serialized**: you can't grep or hand-edit them, so inspect them through MCP. A copied scene can carry leftovers: Frozen Pass had a Rest Area exit gate that would complete the node mid-battle and had to be switched off.
2. **Register** it in `ProjectSettings/EditorBuildSettings.asset` (path + the scene's `.meta` guid, `enabled: 1`). `MainMenu` must stay at index 0. MCP `manage_build` or **File > Build Profiles** also works. An unregistered scene fails to load in a build.
3. **Populate.** **Bladehold/Setup Active Scene for Survivors Mode** (`Editor/SetupSurvivorsSceneTool.cs`) instantiates and cross-wires the required prefabs:
   - Characters and UI: `Player`, `Bladehold HUD` (includes `BuildWheelUI`, `SupplyUI`, the objective tracker), `GameMenu`, `PauseMenuCanvas`, `EventSystem`, `DeathScreen` (victory/defeat, also the node exit), `GameAnalytics`, `CameraRig`.
   - Battle loop: `Waves/EnemySpawner` (`SurvivorsSpawner` + `Spawnpoints` children), `Objectives/SurvivorsObjectives` (children `WagonRoute`, `CatapultSpawns`, `CageSpawns`, `GoldenGoblinWaypoints`).
   - Managers (`Managers/`): `GameLoopManager`, `SurvivorsGameManager`, `GameStats`, `DraftUpgradeService`, `EnemyIntroController`, `MMTimeManager`.
   - Anchors: `UpgradePowerupSpawnPoint`, `BannerSpawnPoint_0..2`, `IntermissionVirtualCamera`. An optional `Gate` gets configured if one is found.
   - The tool snaps points onto the NavMesh, so **bake first**. Its layout is procedural: hand-place the spawn and objective points afterwards.
4. **Tower plots** (the tool doesn't add these): a `Battlefield Tower Plots` root with `TowerPlotManager` + 6 `Bladehold Prefabs/Defenses/TowerPlot.prefab` instances at choke points. `BuildCastleLevels.SetupBattlefieldTowerPlots` is the reference. The prefab already carries the defence prefabs and `assemblyAnimationPrefab`. See `Fort/CLAUDE.md`.
5. **NavMesh**: `NavMeshSurface` on the environment root, `useGeometry = PhysicsColliders`, then Bake (see `BuildCastleLevels.BakeNavMesh`). Every battle scene needs its own bake. Without one, enemies don't move and the setup tool can't place points.
6. Add a `DrawSceneLoadButton("<scene>")` line in `Debug/DevConsole.DrawSceneControls` for quick testing.
7. **Verify** by entering Play mode in the scene directly (no campaign run = threat 1; DevConsole ▲ overrides threat), and from the map. Check: banners, a wave, building a tower in prep, then victory → dismantle refund → "Proceed to Campaign Map" → the node is Completed and its children are Available.

## Editor work: MCP vs. checklist

- **Agent via `/unity-editor-mcp`**: node SO create/edit, graph asset lists, `EditorBuildSettings`, running the setup menu items, adding the plot root/instances, `NavMeshSurface` bake, prefab field wiring, console reads, Play-mode checks. Never save a scene the benchmark ran in.
- **`/editor-wiring-todo`** (`plans/editor/NN-<topic>.md`): level art/layout, final spawn/objective/plot placement, lighting, map icon/UI review, playtest feel. Agents never write to `TODO.md`.

## Finish

1. `/compile-check` (new `.cs` files registered in `Assembly-CSharp.csproj` first).
2. `/editor-wiring-todo` for the leftovers.
3. Commit directly to `main` and push.
