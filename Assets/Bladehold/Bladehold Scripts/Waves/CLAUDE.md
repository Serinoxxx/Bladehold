# Waves: the in-sector battle loop

Every battle scene runs the same loop: `GameLoopManager` + `SurvivorsSpawner` + `Objectives/SurvivorsObjectiveManager` + `Fort/TowerPlotManager`. They're scene objects in `Bladehold Survivors Scene` and come from prefabs elsewhere (`Bladehold Prefabs/Managers/GameLoopManager.prefab`, `Waves/EnemySpawner.prefab`, `Objectives/SurvivorsObjectives.prefab`). The old `WaveSpawner` loop was deleted in plan 08; roster overrides go through `Enemies/EnemyDefinitionApplier`.

## Sector sequence (5 waves, plan 15 wave choice)

1. **Wave 1 is fixed**: a 1-skull Hold the Gate card (`WaveChoiceConfigSO.firstWave`) with a rolled clan. There's no draft. Prep (build towers) → **hold Ready** → 3-2-1 → `StartWave`.
2. **Waves 2-4 are drafted** (`GameLoopManager.BeginIntermission` → `WaveCardGenerator.Roll` → `SurvivorsCardSelectUI.OpenWaveChoice`). Each of the 3 cards bundles:
   - an objective (a `Config/WaveObjectives.csv` row: stance, timer, fail rule);
   - 1-3 skulls with a clan modifier (`WarBannerClanSO`, magnitude scaled by skulls);
   - a reward bundle: gold + supply + one bonus.

   There's always ≥1 Defence and ≥1 Offence card, and last wave's objective is down-weighted. The campaign node's `clanBuff` is weighted ×2. Skull odds come from the SO's table by wave and `SectorThreat.Current` (the node's `tierIndex`).
3. **Prep**: `IsPrepPhase` is true from the moment the field clears until Ready. It's the only window where tower plots and the `Fort/GateRepairStation` (hold [E], supply → gate HP) work.
   - `ObjectiveWaypointTrackerUI` shows the picked objective's `IObjectivePreview` markers.
   - **Ready** = hold the `StartWave` input (T / D-pad Down) for 1 s.
   - `ObjectiveTrackerUI` shows `GameLoopManager.StatusText`: the prompt, then the countdown.
4. **Wave**:
   - `SurvivorsObjectiveManager.StartObjective(card.ObjectiveId)`. The random pool pick is only a fallback when there's no card.
   - `SurvivorsSpawner` spawns the kill quota: the pacing asset's base × `quotaGrowthPerThreat`, then × the card's skull quota multiplier.
     - It spawns in batches of 10, at most 20 alive, each with a 3 s telegraph (`SpawnIndicator`).
     - Every spawn goes through `GameLoopManager.ApplyWaveModifiers`: skull HP multiplier, then `EnemyBuffController` with the card's clan buff.
   - A 3-skull card also spawns a captain (`SpawnCaptainForWave(card.CaptainTier)`: skulls 1/2/3 → Standard/Enraged/Nightmare). Which captain is the card's `captainName`: the node's, or (35%, `WaveChoiceConfigSO.wanderingCaptainChance`) the wandering Captain Mogra Hexfang. The same roll applies to wave 5's Captain Assault card.

   **Sector difficulty (threat).** `SectorThreat.Current` is the campaign node's `tierIndex` (1-8), or 1 outside a campaign run; the DevConsole can override it. Deeper sectors get more enemy types and bigger quotas:
   - A roster row spawns when `threat >= minThreat` **and** `wave >= unlockWave` (both CSV columns). `minThreat` 0/blank keeps a row out of sector waves entirely (golden goblin, captains, unfinished enemies).
   - **Fodder floor:** at least `fodderShare` (60%) of each wave's spawns are `fodderEnemyId` (goblin). The rest is a weighted `spawnChance` roll over every unlocked type (goblins included), respecting each row's `maxConcurrent` and the 2-bubbler cap.
   - Current curve: T1 goblin, brute, bubbler, big ork, bomber. T2 adds Bannerman and Powder Keg, T3 Bulwark and Assassin, T4 Storm Witch, T5 Troll.
   - The selection rules live in `SectorSpawnRules` (pure code), shared with the balance sim so the two can't drift.
   - An `IOverrideEnemySpawns` objective (Goblin Rush) replaces the threat gating with its own id list.
5. **Resolution**: the objective's `OnCompleted` or `OnFailed` ends the wave. The kill quota only sizes the spawns.
   - Spawning stops and every straggler (and a card captain) **routs** (`Enemies/EnemyRout`: a short stun, then it flees to the nearest spawn point). After `routDurationSeconds` the rest despawn. Kills during the rout still pay.
   - A 45 s lightning backstop covers a stalled rout.
   - **Defence** objectives can't fail: their risk is gate damage, and the gate falling ends the run (`Gate.OnAnyGateDestroyed`). Gate HP carries across sectors.
   - **Offence** objectives fail on their timer or when the target escapes. Failure loses the card reward only.
6. **Reward** (success only): the bundle is auto-granted (gold, supply, then Goblin Blood / Orcish Metal / Troll Heart max HP / draft picks, which open the skill-card modal in a chain). `WaveClearedBannerUI` pops the reward line. Then, after `rewardPopupSeconds`, the next draft opens.
7. **Wave 5 is fixed**: `defeat_captain` (Captain Assault, `Objectives/DefeatCaptainObjective`). It spawns the node's captain at a tier set by sector threat (`GameLoopManager.CaptainTierForThreat`: Enraged at threat 1-2, Nightmare at 3-5, Omega at 6+), and killing the captain resolves the wave. If the objective is missing from the scene, the manager spawns the captain itself and falls back to a random objective.
8. **Victory**: resolving wave 5 → `TriggerVictory` → `DeathScreen.ShowVictory` → back to the Campaign Map.

## Rewards inside a sector

- Per kill: in-run gold, plus a chance of supply (bigger enemies give more). This is always kept, win or fail.
- Wave clear: +30 supply (always kept), plus the regeneration / Special Herbs heals.
- A successful objective: the card's bundle. Offence pays ×1.5 and skulls pay ×1 / ×1.5 / ×2.25, all on `WaveChoiceConfigSO`.

## Gotchas

- The objective manager's 30 s cleanup timer is never counted down. `KillRemainingEnemiesObjective` (the old cleanup phase) is no longer started by the flow; the rout replaced it.
- `WarBannerController`, `WaveUpgradePowerup` and the `WarBannerRewardSO`/`WarBannerConfigSO` assets are dead code since plan 15, kept until a playtest confirms (listed for deletion in `plans/editor/15-wave-choice.md`). `WarBannerClanSO` and `BannerDifficultyTier` are still live (card clans and captain tiers).
- Wagon and ram objectives implement `IRequiresContinuousSpawns`: the wave can't end until they resolve, and once the quota is out the spawner trickles enemies (topping up to `objectiveTrickleMinAlive`, one per `objectiveTrickleInterval`) so the field never empties.
- Hold the Gate (`KillEnemiesObjective`) is also `IRequiresContinuousSpawns`, plus `IKillQuotaObjective`: once the quota is out, the spawner re-opens `remainingToSpawn` by `KillsRemaining - aliveCount` at normal batch pacing instead of trickling. Deaths that don't count as kills (enemy-on-enemy, e.g. bomber blasts) would otherwise leave the counter short with an empty field.
- A missing Captain Fraglob prefab (`captainPrefab`) logs an error and spawns Kombusta instead. No Fraglob prefab exists yet.
- `GameLoopManager` still has an empty Second Wind stub in `HandlePlayerDied`.
