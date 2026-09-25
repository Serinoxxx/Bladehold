# 08: Legacy code removal

**Goal:** delete dead systems so agents stop building on them and the codebase shrinks. Work in small commits, one system per commit, compile-checking each. Before deleting anything with a scene/prefab reference, find it by script GUID (from the `.cs.meta`) in text scenes/prefabs. This needs 00 §B's reserialize done first, so binary scenes can be searched too.

## Candidates (verify each is unreferenced in build scenes before deleting)

- [ ] **`Waves/WaveSpawner.cs`** and its satellites (`WaveUI`, `GateAssaultSpawner`, `WaveIntermissionUI`, `ElementNodeSpawner` if dead):
  - First move the static `ApplyDefinition` (used by `SurvivorsSpawner`) somewhere sensible.
  - Replace the ~20 `WaveSpawner.Instance` null-checks (DeathScreen, DevConsole, MinionSpawner, RunTelemetry, …) with `GameLoopManager`/`SurvivorsSpawner` equivalents or delete them.
- [x] **Balance sim** (decided 2026-09-25: retire it, don't port it). Do this **before or with** the gold skill tree below, because the sim's `UpgradePolicy` spends gold on `SkillTreeSO` and won't compile without it.
  - Delete `Editor/BalanceSim/` (all of it, including `CalibrationLoader`), `Config/SimProfiles.csv`, `Config/SimPacingRules.csv`, `Config/SimSectorPacingRules.csv` (and their `.meta`s), and the `BalanceReports/` line in `.gitignore`.
  - Keep `RunTelemetry`: real playtest CSVs replace the sim as the balance source.
  - Delete the `balance-sim` skill from `.claude/skills/` and `.agents/skills/`, and drop its mentions from the root `CLAUDE.md` skill list, `plans/README.md`, `AI_HARNESS_HANDOFF.md`, and the `add-enemy-type` / `generate-enemy-prefabs` / `unity-editor-mcp` / `add-skill-line` skills.
  - Its replacement (a spawn-budget report) is in plan 11.
- [ ] **Gold skill tree + Reincarnate:** `SkillTreeService`, `SkillTreeView`, `SkillNodeView`, `SkillTreeSO` and assets, `ReincarnateService`, `Reincarnate/`, `SkillTreeCsvEditorWindow`, `DeathScreen` reincarnate bits. Mark the `SaveData` fields `[Obsolete]` or drop them; old saves tolerate missing fields.
  - Check `Player/` components that were gold-tree lines (VampiricBlade, DamageBlocker, Parry, Counterstrike, DeathNova, GoldOnDeathCollector, GoldenGoblin stats): some may be reused by drafts or perks, so keep whatever `DraftUpgrades.csv`/perks reference.
- [ ] **`HoldTheLineBonus`** (in every sector, inert).
- [ ] **`GameLoopManager`:** rest-gate path (`HandleGateInteracted`, `OnRestGateOpened`, gate `CanInteract`), `SpawnEndgameBoss`, `siegebreakerBossPrefab`, the Second Wind stub. Note that Second Wind is a real meta perk: implement it via `Health.TryPreventDeath` if it isn't elsewhere, rather than just deleting it.
- [ ] **`SurvivorsGameManager`** 20-minute siege timer / endgame boss / "SIEGE SURVIVED" text.
- [ ] **`RunSession.RestVisitsCount`** formulas (`RestAreaGate`/`RestAreaDoor` `RestVisitsCount*3+1`), `RunState` (if unused after plan 01), `SaveData.highestUnlockedStage/selectedStage/runsAttempted`.
- [ ] **XP level-up draft path** (`SurvivorsLevelSystem`, `SurvivorsLevelUpPromptUI`) if Lance confirms it's dead, along with coin XP.
- [ ] **Old socket fortress** (`FortDefense*`, `ArrowSlitDefense`, `BurningOilDefense`, `SpikeDefense`) and Fortress-category draft cards. Check with Lance whether Fortress cards should be reborn as tower upgrades in Phase 4.
- [ ] **Scenes with no node:** `Bladehold Supply Room` (+ `SupplyRoomController`, `SupplyBox` if unused), `Frozen Pass`, `Ancient Garden`. Ask Lance: keep them as future sector maps, or remove them from build settings?
- [ ] **Orphans:** `VictoryScreenUI` (only benchmark-created), `MainMenuManager` (not in build).
- [ ] Stale comments referencing `ClassDefinitionSO`/`PlayerClassController`.
- [ ] Update the root `CLAUDE.md` "Legacy / dead code" list as items go.
