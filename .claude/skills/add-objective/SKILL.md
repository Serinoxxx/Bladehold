---
name: add-objective
description: Use when adding a new per-wave sector objective to Bladehold (like Defeat the Slayer, Free Prisoners, Goblin Rush) — the ISurvivorsObjective component, registering it in SurvivorsObjectiveManager's pool, how completion/failure/cleanup hand off to GameLoopManager, what objectives actually pay, the objective HUD, and DevConsole testing.
---

# Add a sector objective

Every wave of a combat sector runs exactly one `ISurvivorsObjective`. **Templates:**
- Event-driven, spawns something to kill: **`Objectives/DefeatSlayerObjective.cs`**.
- Polled counter: **`Objectives/KillEnemiesObjective.cs`** ("Hold the Gate").

## Ground truth first

Read these before starting:
- `Objectives/ISurvivorsObjective.cs`: the contract, plus the marker interfaces `IOverrideEnemySpawns` and `IRequiresContinuousSpawns`.
- `Objectives/SurvivorsObjectiveManager.cs`: pool, `SetActiveObjective`, `StartCleanupObjective`, the `Debug*` methods.
- `Waves/GameLoopManager.cs`: `StartWave`, `HandleObjectiveCompleted`, `HandleObjectiveFailed`, `CheckWaveCompletionConditions`, `ClearActiveWave`.
- Your template, end to end.

## Lifecycle (what the manager and GameLoopManager do to you)

1. `GameLoopManager.StartWave` → `objectiveManager.StartWaveObjective(wave)`. Wave 1 is always `introductoryObjective` (KillEnemies). Later waves roll the pool at random with no immediate repeat. The intro objective is inserted at pool index 0, so it can be rolled again on waves 2-5.
2. `SetActiveObjective`: the **previous** objective is unsubscribed and gets `CleanupObjective()`. Then yours is subscribed and gets `StartObjective()`, and `OnObjectiveStarted` fires. The spawner starts the wave's kill quota right after.
3. `UpdateObjective(dt)` is called from the manager's `Update` every frame while the phase is Active or Cleanup. Event-driven objectives can leave it empty.
4. **Complete**: set `isComplete`, clear `isActive`, fire `OnProgressChanged` then `OnCompleted` **once**. `GameLoopManager.HandleObjectiveCompleted` then stops the spawner.
   - Enemies still alive: `StartCleanupObjective()` swaps in `KillRemainingEnemiesObjective` (skull waypoints on every living enemy), so **your `CleanupObjective()` runs immediately**.
   - Field empty: `ClearActiveWave` runs (+30 supply, then the bounty powerup, or victory on wave 5). Your objective stays "current" until the next wave swaps it out.
5. **Fail**: fire `OnFailed`. `HandleObjectiveFailed` marks the objective done and re-checks wave completion. There's **no penalty**, and the wave still needs its kill quota. Only DestroySiegeEngines and FreePrisoners can fail today.
6. **Scene unload**: the manager's `OnDestroy` calls `CleanupObjective()`, and `StopAllObjectives` is never called. So `CleanupObjective` must be idempotent and null-safe: unsubscribe `Health` events, destroy your spawned objects only if they're not dead, hide any HUD you showed.
7. The manager's `cleanupDuration` (30 s) is never counted down. The real backstop is `GameLoopManager`'s 45 s no-progress timer, which lightning-strikes stragglers.

## Optional behaviours

- **`IRequiresContinuousSpawns`** (ProtectWagon, StopBatteringRam): the wave can't end before you resolve. Once the quota is out, `SurvivorsSpawner` trickles enemies (`objectiveTrickleMinAlive`/`objectiveTrickleInterval` on the pacing asset).
- **`IOverrideEnemySpawns`** (GoblinRush): your `AllowedEnemyIds` replace the threat/`minThreat` gating for the wave, and the 60% fodder floor still applies.
- **Timed / no-quota objectives**: GoblinRush is special-cased **by type** in `GameLoopManager` (quota set to 999999 in `StartWave`, quota treated as met in `CheckWaveCompletionConditions`). A new timed objective needs the same code change there. Prefer adding a small marker interface next to the existing two over another type check, and flag the choice to Lance.
- **Enemy targeting**: `GetObjectiveTargetPosition`/`GetObjectiveDamageable` have **no callers** (return `null` unless you also wire a consumer). Enemy steering is type-checked in `Enemies/AITargetSelector.cs`: `KillEnemiesObjective` sends enemies at the gate, `StopBatteringRamObjective` makes them escort the ram. New steering needs a branch there.

## Rewards (what objectives actually pay)

The XP level-up path was deleted in plan 08. The manager pays **nothing** on completion; the `goldXpRewardPerObjective: 50` in the manager prefab's YAML is a leftover value with no field behind it. The player gets paid anyway, because:
- Per kill: `GameLoopManager.OnEnemyKilled` pays in-run gold (2-5) and a supply roll. This only counts `SurvivorsSpawner` spawns; your own spawns don't advance the quota.
- Wave clear: +30 supply, and after waves 1-4 the chosen war banner's bounty.
- Objective-specific rewards are **physical pickups**: spawn the authored `Coin` prefab from a serialized field and call `SetAmount` (pickup → `RunSession.AddInRunGold`).
  - `GoldenGoblinObjective`: `coinPrefab`, `goldPerDrop` every 10% HP lost, `killBonusGold`.
  - `SupplyWagonEscort`: `goldBagPrefab`, `goldPerBag`.
- For other currencies, call `RunSession.AddInRunSupply`/`AddInRunGold`, or `AddGoblinBlood`/`AddOrcishMetal` (both are permanent `SaveData`), from your completion handler. Put the amounts on your SO.

## Recipe

1. **Script** `Objectives/<Name>Objective.cs : MonoBehaviour, ISurvivorsObjective` (+ marker interfaces if needed). Give it a unique `objectiveId` (snake_case), a `title` (shown as the HUD header and quest banner) and a `description`.
   - Put numbers (counts, durations, HP, rewards) on a `<Name>ObjectiveSO` (`[CreateAssetMenu(menuName = "Scriptable Objects/Objectives/...")]`). The existing objectives keep them as inspector fields; don't copy that.
   - Spawned things (engine, cage, wagon, boss) are **authored prefabs** in serialized fields, with scene spawn points as `Transform[]`. If one is missing: `Debug.LogError` in `Start`, set `anyError`, and don't start.
   - Don't `AddComponent` fallbacks the way DefeatSlayer does for `AITargetSelector`/`SpecialEnemyIntro`/`EnemyDamageRetaliation`. Require the components on the prefab.
   - Track your targets with `Health.OnDied`/`IsDead`, never `OnDestroy` or object counts. Unsubscribe in `CleanupObjective`.
   - **Never call `GameLoopManager` from an objective.** `GoldenGoblinObjective` calls `GameLoopManager.DebugCompleteObjective()` and then fires `OnCompleted` itself, which double-routes completion. Just fire your own events.
   - Feedback (spawn horn, hit, break, fanfare) goes through serialized `MMF_Player`s on the prefab (`/feel-integration`); see `PrisonerCage`/`DestructibleSiegeEngine`.
2. **HUD** (all driven by the interface, so there's nothing to register):
   - `UI/ObjectiveTrackerUI`: `Title` + `ProgressText` on `OnObjectiveStarted`/`OnObjectiveProgressChanged`, so fire `OnProgressChanged` whenever the text changes.
   - `UI/WaveClearedBannerUI`: quest banner on start and complete.
   - `UI/ObjectiveWaypointTrackerUI`: polls `GetActiveWaypointTargets` while `IsActive`. Add `ObjectiveWaypointTarget(transform, offset, icon, tint, label)`, with the icon as a serialized sprite field.
   - Boss-style targets: `EnemyIntroController.Instance.PlayIntro(SpecialEnemyIntro)` and `BossHealthBarUI` (hide it in `CleanupObjective`, as DefeatSlayer does).
3. **Register**: add the component to the `SurvivorsObjectives` GameObject in `Bladehold Prefabs/Objectives/SurvivorsObjectives.prefab` and append it to `SurvivorsObjectiveManager.repeatingObjectiveComponents`.
   - An empty list auto-discovers every `ISurvivorsObjective` in the scene, but the prefab's list is populated, so **unlisted objectives never roll**.
   - Put spawn points as children of that prefab (like `CageSpawns`, `CatapultSpawns`, `WagonRoute`).
   - The castle scenes are binary-serialized and may override the prefab. Check a scene's instance via MCP before assuming placement is right in every battle scene.
4. **Demo**: nothing gates objectives today. If one must be cut from the demo, add a static helper on `Demo/DemoConfigSO` and filter the pool with it, never a per-asset flag.
5. **Test**: a `WeaponReachBenchmark` section per `/test-mechanic` for any rule or formula. The `KillRemainingEnemiesObjective` block (~line 2643) is the model: build the objective on a temp GameObject, drive `StartObjective`/`UpdateObjective`/`Health` deaths, and assert progress, completion and waypoints. Lance runs the suite himself unless he asks.

## DevConsole testing (backquote, in a battle scene)

- **Next Obj**: `DebugNextObjective` (random roll). **Complete Obj**: `DebugCompleteCurrentObjective` (routes through `GameLoopManager` like a real completion).
- **◄ / ►** picker + **Start '<title>'**: `DebugStartObjective(index)` force-starts a pool entry.
- Caveats with force-start: the spawner isn't restarted, and `GameLoopManager`'s cached `currentObjective` field keeps the wave's original objective. So the GoblinRush and `IRequiresContinuousSpawns` checks in `CheckWaveCompletionConditions` apply to the *original* objective. For an honest run, reach your objective through normal wave rolls (Next Obj between waves) or temporarily make it the only pool entry.
- Wave controls ("Wipe Wave") and the threat ▲/▼ help reach later waves.

## Finish protocol

1. `/compile-check` (register new files in `Assembly-CSharp.csproj`; build `Assembly-CSharp-Editor.csproj` too if you touched the benchmark).
2. Editor work via `/unity-editor-mcp` when connected: create the SO asset, add and wire the component on `SurvivorsObjectives.prefab`, append it to `repeatingObjectiveComponents`, add spawn-point children, and do a Play-mode check that it appears and completes.
   - Everything else goes in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo`: authored prefabs, MMF players, waypoint icon sprites, per-scene placement in the binary castle scenes, and a playtest (start banner, tracker text, waypoints, completion → cleanup → bounty, cleanup destroys leftovers, negative cases like failing or dying mid-objective).
   - Agents never write to `TODO.md`.
3. Commit directly to `main` and push.
