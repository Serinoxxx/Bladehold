# 06: Sector difficulty + enemy roster in waves

**Goal:** sectors get harder deeper into the campaign, with stronger enemy types in later sectors while basic goblin fodder keeps flowing (power fantasy). The built-but-unused enemies join sector waves.

## Current state

- `Campaign/CampaignManager.SelectNode` resets `RunSession.CurrentWave = 1`, and every sector uses the same `SurvivorsRoundPacingConfig.asset` (5 waves, quota 15/20/25/30/35).
- Enemy gating is `unlockWave <= wave` **and** the wave's `allowedEnemyIds`, so sectors only ever get goblin, brute, big_ork, bubbler, bomber.
- Bulwark, Bannerman, Powder Keg, Assassin, Storm Witch and Troll are only reachable through the DevConsole or EnemyZoo. The Bannerman clan-buff design assumes Bannermen spawn.
- `SurvivorsSpawner` stops once the quota has spawned, so Wagon/Ram objectives can leave the field empty (comments claim it keeps spawning).

## Tasks

- [ ] **Design the scaling model with Lance first**, then implement it as data rather than code. Proposal:
  - Each campaign node (or tier) references a pacing asset or a "threat level" int.
  - Enemy rows gate on threat level instead of the per-sector wave.
  - Each wave has a fodder share (e.g. at least 60% goblins) plus an elite budget.
  - Captains scale with node tier (already partly true via `difficultyTier`).
- [ ] Put Bulwark, Bannerman, Powder Keg, Assassin, Storm Witch and Troll into the progression. Check each prefab works in a sector (telegraphs, NavMesh, health bars, CorpseDespawner).
- [ ] Wagon/Ram objectives keep a trickle of spawns until the objective resolves.
- [ ] Use or delete the unused pacing fields (`weight*`, `intermissionDuration`, `bossSpawnWave`, `spawnStaggerInterval`).
- [ ] Captain prefab slots: code-side, make missing captain prefabs a `LogError` rather than silently scaling a brute. The asset assignment is on Lance's list (00 §B).
- [ ] Note for balance: basic enemies swing without telegraphs for minor damage, specials telegraph (see `Enemies/CLAUDE.md`). Keep that split.
- [ ] Run the `balance-sim` skill on the new curve and record the verdicts here.
- [ ] Update `Waves/CLAUDE.md`, `Enemies/CLAUDE.md` and the root "Design direction".

## Acceptance

Tier-1 sectors feel like today; tier-5 sectors show elites mixed into goblin swarms; the sim shows no difficulty cliff.
