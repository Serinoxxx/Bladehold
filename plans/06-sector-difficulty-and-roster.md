# 06: Sector difficulty + enemy roster in waves

**Goal:** sectors get harder deeper into the campaign, with stronger enemy types in later sectors while basic goblin fodder keeps flowing (power fantasy). The built-but-unused enemies join sector waves.

## State before this plan

- `Campaign/CampaignManager.SelectNode` resets `RunSession.CurrentWave = 1`, and every sector uses the same `SurvivorsRoundPacingConfig.asset` (5 waves, quota 15/20/25/30/35).
- Enemy gating is `unlockWave <= wave` **and** the wave's `allowedEnemyIds`, so sectors only ever get goblin, brute, big_ork, bubbler, bomber.
- Bulwark, Bannerman, Powder Keg, Assassin, Storm Witch and Troll are only reachable through the DevConsole or EnemyZoo. The Bannerman clan-buff design assumes Bannermen spawn.
- `SurvivorsSpawner` stops once the quota has spawned, so Wagon/Ram objectives can leave the field empty (comments claim it keeps spawning).

## Scaling model (agreed with Lance, 2026-09-25)

| Decision | Choice |
|---|---|
| Threat source | Campaign node `tierIndex` (1-8); 1 outside a campaign run. `SectorThreat.Current`, DevConsole override. |
| Gating | New `Enemies.csv` column `minThreat`: a row spawns when `threat >= minThreat` **and** `wave >= unlockWave`. `minThreat` 0/blank = never in sector waves. The per-wave `allowedEnemyIds` whitelist is gone. |
| Fodder | At least 60% of each wave's spawns are goblins (`fodderShare`, running floor). The rest is a weighted `spawnChance` roll over all unlocked types. |
| Scaling | Kill quota +10% per threat level above 1 (`quotaGrowthPerThreat`). Enemy stats unchanged. |

Curve shipped: T1 goblin, brute, bubbler, big ork, bomber. T2 +Bannerman, Powder Keg. T3 +Bulwark, Assassin. T4 +Storm Witch. T5 +Troll. Newcomers unlock on wave 3 of a sector (Troll wave 4).

## Tasks

- [x] **Design the scaling model with Lance first**, then implement it as data rather than code. See the table above. `SectorSpawnRules` holds the pure selection rules, shared by `SurvivorsSpawner` and the balance sim.
- [x] Put Bulwark, Bannerman, Powder Keg, Assassin, Storm Witch and Troll into the progression. Checked statically: every prefab has Health, NavMeshAgent, AIMovement, Enemy, CorpseDespawner, health bar and CoinDropper, and its special attack's telegraph refs are assigned. Cosmetic gaps (Bannerman banner visual, Assassin audio) and the Play-mode check are in the Editor checklist.
- [x] Wagon/Ram objectives keep a trickle of spawns until the objective resolves (`IRequiresContinuousSpawns`; the pacing asset's `objectiveTrickleMinAlive` / `objectiveTrickleInterval`).
- [x] Use or delete the unused pacing fields: removed `weight*`, `intermissionDuration`, `bossSpawnWave`, `spawnStaggerInterval`, `totalRounds` and `allowedEnemyIds`. `bossEnemyId` stays until plan 08 removes `SpawnEndgameBoss`.
- [x] Captain prefab slots: a missing Fraglob prefab now logs an error and spawns Kombusta (a real captain) instead of silently scaling a brute. No Fraglob prefab exists; building one is in the Editor checklist.
- [x] Note for balance: basic enemies swing without telegraphs for minor damage, specials telegraph. The split is kept; every newcomer is a telegraphed special.
- [x] Run the `balance-sim` skill on the new curve and record the verdicts here (below).
- [x] Update `Waves/CLAUDE.md`, `Enemies/CLAUDE.md` and the root "Design direction".

Also changed: row `maxConcurrent` caps now always apply. The spawner's `ignoreRowMaxConcurrent` flag is gone, and goblins stay uncapped. Without the caps, a tier-5 wave could field a dozen Trolls.

## Balance sim

Run 2026-09-25: `balance-sim` in the new **sector mode** (`-simSectors`), seed 12345, 300 trials, each tier simmed on its own from full HP (`-simStartTier N -simWaves 5`). "Today" is a what-if approximating the old spawner: no floor, no quota growth, no row caps, only the original five types. Reports are in `BalanceReports/p06/` (git-ignored).

**Read these as relative numbers only.** The sim's player is a naked 50-HP swordsman: it spends gold on the dead gold skill tree (no CSV, so nothing is bought) and has no drafts, towers, bow, dash, armour or perks. So every `SimSectorPacingRules.csv` verdict fails or warns: bad/average profiles die on waves 2-3 at every tier, and good fails `good_clears_tier1` (0.57 vs 0.95). Those are sim-calibration failures, not curve findings.

Good profile, one sector per tier:

| Run | Sector clear rate | Median damage taken, waves 1-5 |
|---|---|---|
| Today (any tier) | 18% | 3 / 13 / 28 / 36 / 30 |
| T1 | 57% | 3 / 8 / 20 / 29 / 28 |
| T2 | 13% | 4 / 8 / 38 / 39 / 36 |
| T3 | 3% | 4 / 10 / 52 / 41 / 48 |
| T4 | 4% | 4 / 10 / 52 / 38 / 38 |
| T5 | 0% (wave-5 survival 6%) | 5 / 11 / 56 / 44 / 52 |
| T6-T8 | 0% (wave-5 survival 3% → 1%) | 58-61 on wave 3 |

Verdicts:
- **Difficulty only rises with depth, and the biggest step is T1 → T2** (57% → 13% clear for the naked sim player). After that the decline is gentle: T2 → T8 goes 13% → 0% with wave-5 survival sliding 35% → 1%. T5-T8 differ only by quota growth, because the roster is complete at T5. With a real player (drafts, towers, perks) the T1 → T2 step should shrink, but it's the first thing to watch in playtests.
- **T1 is easier than today** (57% vs 18%). The fodder floor alone accounts for ~+30 points (48% with the floor and no caps), and the row caps alone also ~+30 points (50% with caps and no floor). T2 lands roughly where today's sectors were. If tier 1 should match today exactly, the levers are a lower `fodderShare` or higher brute/big-ork/bomber `spawnChance`.
- **Wave 3 is the spike inside a sector at T2+,** because every newcomer unlocks on wave 3. Staggering them across waves 2-5 (tested) spreads the damage but makes the sector harder overall (T3 clear 3% → 1%). The single wave-3 step was kept.

## Acceptance

Tier-1 sectors feel like today; tier-5 sectors show elites mixed into goblin swarms; the sim shows no difficulty cliff.

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/06-sector-difficulty.md`](editor/06-sector-difficulty.md).
