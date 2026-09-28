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

Since plan 15, objectives are **picked, not rolled**: between waves the player chooses one of 3 wave cards, and each card names an objective by `ObjectiveId` through its `Config/WaveObjectives.csv` row.

1. **Start.** `GameLoopManager.StartWave` calls `objectiveManager.StartObjective(card.ObjectiveId, wave)`.
   - It finds you in the pool, or anywhere in the scene, by id.
   - Wave 1 is the fixed Hold the Gate card. Wave 5 is the fixed `defeat_captain`.
   - The old random pick (`StartWaveObjective`) is only a fallback when there's no card or your id isn't in the scene.
2. `SetActiveObjective`: the **previous** objective is unsubscribed and gets `CleanupObjective()`. Then yours is subscribed and gets `StartObjective()`, and `OnObjectiveStarted` fires. The spawner starts the wave's kill quota right after. `KillEnemiesObjective` gets `SetRequiredKills(quota)` so Hold the Gate scales with skulls.
3. `UpdateObjective(dt)` is called from the manager's `Update` every frame while the phase is Active or Cleanup. Event-driven objectives can leave it empty.
4. **Complete.** Set `isComplete`, clear `isActive`, then fire `OnProgressChanged` followed by `OnCompleted`, **once**.
   - That alone ends the wave. The kill quota only sizes the spawns.
   - `GameLoopManager.ResolveWave(true)` stops the spawner and routs the stragglers (`Enemies/EnemyRout`), then `ClearActiveWave` runs (+30 supply). Then the card's reward bundle is granted, and the next draft opens (or victory after wave 5).
   - Your `CleanupObjective()` runs in `ClearActiveWave`.
5. **Fail.** Fire `OnFailed`: the wave resolves the same way but pays **no card reward**. Kill gold and supply are still kept.
   - Only **Offence** objectives should fail: on a timer, or when the target escapes.
   - **Defence** objectives never fail. Their risk is gate damage, and the gate falling ends the run.
6. **Scene unload**: the manager's `OnDestroy` calls `CleanupObjective()`, and `StopAllObjectives` is never called. So `CleanupObjective` must be idempotent and null-safe: unsubscribe `Health` events, destroy your spawned objects only if they're not dead, hide any HUD you showed.
7. The manager's `cleanupDuration` (30 s) is never counted down. `KillRemainingEnemiesObjective` is no longer started, since the rout replaced it. `GameLoopManager` has a 45 s lightning backstop for a stalled rout.

## The CSV row (required)

Every draftable objective needs a `Config/WaveObjectives.csv` row. Its `id` must equal your `ObjectiveId`.
- The columns are: `stance` (Defence / Offence), `timerSeconds`, `failRule` (None / Timer / Escapes), `failParam`, `weight`, `minWave`, `minThreat`, `draftable`, `demoEnabled`, plus the English `title,rule,failText,threatText` that the card shows through `Loc.Get("wave.obj.<locKey>.*", english)`.
- **Read your timer from the row** (`WaveChoiceConfigSO.Load()?.Catalog.Get(ObjectiveId)`), falling back to your serialized value. That way the card and the objective can't disagree.
- Section 27 of the benchmark checks that every CSV id has a matching objective on the prefab.
- Implement **`IObjectivePreview`** if your locations are known before `StartObjective` (cages, engines, ram start). The prep phase then shows them as waypoints so the player can place towers.

## Optional behaviours

- **`IRequiresContinuousSpawns`** (ProtectWagon, StopBatteringRam): the wave can't end before you resolve. Once the quota is out, `SurvivorsSpawner` trickles enemies (`objectiveTrickleMinAlive`/`objectiveTrickleInterval` on the pacing asset).
- **`IOverrideEnemySpawns`** (GoblinRush): your `AllowedEnemyIds` replace the threat/`minThreat` gating for the wave, and the 60% fodder floor still applies.
- **Timed / no-quota objectives**: GoblinRush is special-cased **by type** in `GameLoopManager` (quota set to 999999 in `StartWave`). A new timed objective needs the same code change there. Prefer adding a small marker interface next to the existing two over another type check, and flag the choice to Lance.
- **Enemy targeting**: `GetObjectiveTargetPosition`/`GetObjectiveDamageable` have **no callers** (return `null` unless you also wire a consumer). Enemy steering is type-checked in `Enemies/AITargetSelector.cs`: `KillEnemiesObjective` sends enemies at the gate, `StopBatteringRamObjective` makes them escort the ram. New steering needs a branch there.

## Rewards (what objectives actually pay)

The XP level-up path was deleted in plan 08. The manager pays **nothing** on completion; the `goldXpRewardPerObjective: 50` in the manager prefab's YAML is a leftover value with no field behind it. The player gets paid anyway, because:
- Per kill: `GameLoopManager.OnEnemyKilled` pays in-run gold (2-5) and a supply roll. This only counts `SurvivorsSpawner` spawns; your own spawns don't advance the quota.
- Wave clear: +30 supply, and on success the picked wave card's bundle (gold + supply + bonus, scaled by stance and skulls on `WaveChoiceConfigSO`). Don't add a separate payout on top: plan 15 removed the wagon's gold bags for that reason.
- Objective-specific rewards are **physical pickups**: spawn the authored `Coin` prefab from a serialized field and call `SetAmount` (pickup → `RunSession.AddInRunGold`).
  - `GoldenGoblinObjective`: `coinPrefab`, `goldPerDrop` every 10% HP lost, `killBonusGold`.
- For other currencies, call `RunSession.AddInRunSupply`/`AddInRunGold`, or `AddGoblinBlood`/`AddOrcishMetal` (both are permanent `SaveData`), from your completion handler. Put the amounts on your SO.

## Recipe

1. **Script** `Objectives/<Name>Objective.cs : MonoBehaviour, ISurvivorsObjective` (+ marker interfaces if needed). Give it a unique `objectiveId` (snake_case), a `title` (shown as the HUD header and quest banner) and a `description`.
   - Put numbers (counts, durations, HP, rewards) on a `<Name>ObjectiveSO` (`[CreateAssetMenu(menuName = "Scriptable Objects/Objectives/...")]`). The existing objectives keep them as inspector fields; don't copy that.
   - Spawned things (engine, cage, wagon, boss) are **authored prefabs** in serialized fields, with scene spawn points as `Transform[]`. If one is missing: `Debug.LogError` in `Start`, set `anyError`, and don't start.
   - Don't `AddComponent` fallbacks the way DefeatSlayer does for `AITargetSelector`/`SpecialEnemyIntro`/`EnemyDamageRetaliation`. Require the components on the prefab.
   - Track your targets with `Health.OnDied`/`IsDead`, never `OnDestroy` or object counts. Unsubscribe in `CleanupObjective`.
   - **Never call `GameLoopManager` from an objective** (except `DefeatCaptainObjective` spawning its captain through `SpawnCaptainForWave`). Just fire your own events.
   - Feedback (spawn horn, hit, break, fanfare) goes through serialized `MMF_Player`s on the prefab (`/feel-integration`); see `PrisonerCage`/`DestructibleSiegeEngine`.
2. **HUD** (all driven by the interface, so there's nothing to register):
   - `UI/ObjectiveTrackerUI`: `Title` + `ProgressText` on `OnObjectiveStarted`/`OnObjectiveProgressChanged`, so fire `OnProgressChanged` whenever the text changes.
   - `UI/WaveClearedBannerUI`: quest banner on start and complete.
   - `UI/ObjectiveWaypointTrackerUI`: polls `GetActiveWaypointTargets` while `IsActive`. Add `ObjectiveWaypointTarget(transform, offset, icon, tint, label)`, with the icon as a serialized sprite field.
   - Boss-style targets: `EnemyIntroController.Instance.PlayIntro(SpecialEnemyIntro)` and `BossHealthBarUI` (hide it in `CleanupObjective`, as DefeatSlayer does).
3. **Register**: add the CSV row (above), then add the component to the `SurvivorsObjectives` GameObject in `Bladehold Prefabs/Objectives/SurvivorsObjectives.prefab` and append it to `SurvivorsObjectiveManager.repeatingObjectiveComponents`.
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
