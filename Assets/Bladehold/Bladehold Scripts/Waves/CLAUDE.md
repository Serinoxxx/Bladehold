# Waves: the in-sector battle loop

Every battle scene runs the same loop: `GameLoopManager` + `SurvivorsSpawner` + `Objectives/SurvivorsObjectiveManager` + `Fort/TowerPlotManager`. They're scene objects in `Bladehold Survivors Scene` and come from prefabs elsewhere (`Bladehold Prefabs/Managers/GameLoopManager.prefab`, `Waves/EnemySpawner.prefab`, `Objectives/SurvivorsObjectives.prefab`). The old `WaveSpawner` is legacy, so don't extend it.

## Sector sequence (5 waves)

1. **Prep / banner intermission** (`GameLoopManager.CheckAndSpawnBanners`): 3 war banners slam down, each combining a **clan buff** (`WarBannerClanSO`), a **bounty**, and a **difficulty tier** (`Banners/WarBannerDifficulty.cs`: Standard ×1, Enraged ×2, Nightmare ×4, Omega ×8). `IsPrepPhase` is true here, which is the only window where tower plots accept builds. There's no timer.
2. Player picks a banner with `[E]` → it burns for 3s → a 3-2-1 countdown → `StartWave`.
3. **Wave**:
   - `SurvivorsSpawner` spawns the wave's kill quota (from `Bladehold Config/SurvivorsRoundPacingConfig.asset`: 15/20/25/30/35) in batches of 10, with at most 20 alive at once.
   - Each spawn gets a 3s ground telegraph (`SpawnIndicator`).
   - Enemy types are gated by `unlockWave` from the roster **and** by the wave's `allowedEnemyIds` in the pacing asset.
   - "Rounds" in `RoundPacingConfigSO` are indexed by wave number; comments saying 4 rounds / 12 waves are stale.
4. **Objective**: each wave runs one `ISurvivorsObjective`. Wave 1 is the intro (KillEnemies "Hold the Gate"); after that the pick is random with no immediate repeat.
   - The pool: DestroySiegeEngines, ProtectWagon, FreePrisoners, DefeatSlayer, GoldenGoblin, GoblinRush, StopBatteringRam.
5. **Clearing the wave**:
   - When the objective completes, spawning stops. If enemies are still alive, `KillRemainingEnemiesObjective` runs, with skull waypoints on the stragglers.
   - If there are no kills for 45s, lightning finishes the stragglers off.
   - GoblinRush sets an effectively infinite quota and ends on its timer.
6. **Captain**: wave 5 always spawns one; any wave with an Enraged-or-higher banner does too (`SpawnCaptainForWave`).
   - Which captain: Kombusta or Fraglob, from the campaign node's `captainName`, else a coin flip.
   - The captain doesn't count toward the quota but must die before the cleanup can finish.
7. **Bounty**: after waves 1-4, the chosen banner's bounty drops as a `WaveUpgradePowerup`. It gives a weapon/element draft, supply, gold, Orcish Metal, Goblin Blood or a Troll Heart, scaled by the tier multiplier. Then the next banners appear.
8. **Victory**: clearing wave 5 → `TriggerVictory` → `DeathScreen.ShowVictory` → back to the Campaign Map.

## Rewards inside a sector

- Per kill: in-run gold, plus a chance of supply (bigger enemies give more).
- Wave clear: +30 supply.
- Objective completion: level XP. The XP/level-up draft path is legacy with the prompt disabled.

## Gotchas

- Unused pacing fields: the `weight*` drop weights, `intermissionDuration`, `bossSpawnWave`, `spawnStaggerInterval`. The objective manager's 30s cleanup timer is never counted down either.
- Wagon and ram objectives can leave the field empty, because the spawner stops once the quota is spawned even though comments say it keeps spawning.
- `GameLoopManager` still carries dead rest-gate code (`HandleGateInteracted`, `OnRestGateOpened`), an unused `SpawnEndgameBoss`, and an empty Second Wind stub in `HandlePlayerDied`.
