---
name: balance-sim
description: Use when tuning Bladehold balance or checking pacing — run the Monte-Carlo projection sim over the real CSVs/SO values with what-if overrides and player profiles (bad/average/good), read the verdicts, iterate. Also after any balance-relevant change (Enemies.csv, skill trees, wave config, player/weapon SO values).
---

# Project pacing with the Balance Simulator

`Editor/BalanceSim/` simulates whole runs (spawn pacing, engagement, damage, gold, between-wave shopping on the real skill tree) per **player profile** — no play-testing needed to ask "if I double player HP, does a bad player coast to wave 10?". It reads the live assets (`Enemies.csv`, `SkillTree.csv`, `WaveConfigSO`, `PlayerHealthSO`, `DamageSO`, …) and runs the *real* `PlayerStats`/`SkillTreeSO` code, so upgrade math can't drift; what-ifs go through in-memory overrides, never asset edits.

## Ground truth first

1. Profiles live in `Assets/Bladehold/Config/SimProfiles.csv` (bad / bad_noupgrades / average / good — avoidance, kiting, accuracy, target priority, upgrade policy…); the difficulty contract lives in `Assets/Bladehold/Config/SimPacingRules.csv`. Read both before interpreting verdicts.
2. The sim is a **projection, not the game**: positions are timers, and a few spots are re-modeled from source with `// mirrors <file:line>` anchors (`SpawnModel.cs`, `CombatModel.cs`). If you changed `WaveSpawner.SelectSpawnType`, `DamageTrigger.BuildDamage`, or `CoinDropper.HandleDied`, update the mirror or the projection lies.
3. v1 models the **Swordsman** only (the one fully authored class).

## Step 1 — Run it

**Headless** (project must be closed in the Editor; the BatchBuild rule):
```
& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode `
  -projectPath "C:\Users\lance\source\repos\My project" `
  -executeMethod Bladehold.BalanceSim.BalanceSimCli.Run -logFile "BalanceReports\sim.log" `
  -simTrials 200 -simWaves 20 -simSeed 12345 -simOut "BalanceReports/myexperiment" `
  -simSet player.maxHealth=20
```
Exit codes: **0** = all rules pass/warn, **2** = at least one `fail` verdict, **1** = sim error. A `[BalanceSim] started` line prints immediately — if it's missing from the log, a compile error meant the method never ran. Flags: `-simProfiles bad,good`, `-simOverrides file.txt`, `-simEmitTrials`.

**In-Editor**: `Bladehold > Balance Simulator` (same engine; overrides textarea, per-wave table, tinted verdicts).

## Step 2 — Author what-ifs (`key=value`, one per line, `#` comments)

```
player.maxHealth=20            statBase.SwordDamage=7        statMod.CritChance.flat=0.1
wave.goblinsAddedPerWave=3     enemy.bomber.damage=15        node.sword_dmg=3
profile.good.avoidance=0.7     sim.spawnDistanceMeters=25
```
`enemy.<id>.<column>` takes any Enemies.csv column (spawnChance in authored percent); `node.<id>=<level>` grants tree levels free at run start; unknown keys exit 1 on purpose.

## Step 3 — Read the results (in `BalanceReports/<out>/`)

- `findings.json` — the verdicts. `fail`/`warn` per rule per profile; **`skipped` means the rule matched no run — fix the run or the rule, never ignore it.**
- `summary.json` — config echo (seed + overrides), death-wave histogram/percentiles per profile, the full profile params. The file to read programmatically.
- `waves.csv` — per profile × wave: survival rate, clear-time bands, damage taken, HP trajectory, gold, modal purchases. Where "why did they die on wave 6" lives (look at `min_hp_fraction_med` collapsing and which enemies unlock that wave).

## Step 4 — Iterate

Change the real CSV/SO value (or first prototype via override), re-run **with the same seed**, diff the findings. Identical seed + config ⇒ byte-identical output, so any change in the numbers is your change. When a pacing rule's intent itself is wrong, edit `SimPacingRules.csv` — but a rule that fails is a *finding* first, not a band to widen.

## Pitfalls

- **Batchmode and the open Editor are exclusive** — headless runs fail if the project is open (and vice versa, MCP needs it open). Check before launching.
- Overrides prototype, CSVs ship: a balance conclusion reached via `-simSet` isn't done until the real `Enemies.csv`/`SkillTree.csv`/SO value is edited and the baseline re-run clean.
- Don't compare runs across different seeds/trial counts and call a small delta a result — bands overlap; use the same seed.
- The bomber one-shot dominates early deaths (25 dmg vs 10 HP); if deaths cluster at its `unlockWave`, that's the lever to reach for.

## Finish protocol

Real config edited (not just overrides) → baseline re-run, findings reviewed (no unexplained `fail`/`skipped`) → `/compile-check` if C# changed → commit to `main` and push (reports are git-ignored; commit the CSV/SO changes).
