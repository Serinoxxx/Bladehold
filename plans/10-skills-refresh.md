# 10: Skills refresh

**Goal:** one skill set that both Claude Code (`.claude/skills/`) and Antigravity (`.agents/skills/`) use, reflecting the current game. Decide with Lance whether one folder is canonical and the other a copy/junction (a Windows directory junction keeps them in sync).

## Tasks

- [ ] **Retire** `add-player-class` (class system deleted), plus `editor-wiring-todo` and `editor-wire` (both TODO.md-based; TODO.md is now Lance's own list). Fold any useful Unity MCP wiring know-how into `unity-editor-mcp`.
- [ ] **Rewrite** `add-skill-line` → `add-draft-card`:
  - `DraftUpgrades.csv` columns, `;`/`|` syntax, categories, `targetSlot`, `isUltimate`/`isDuo`/`prerequisiteElements`.
  - `StatType` registration, where effects are applied.
  - Test coverage, a localization key, an icon.
- [ ] **Port from `.agents/skills`**, then review each for staleness against the new CLAUDE.md:
  - `changelog`, `maintain-mechanic-tests`, `test-mechanic`, `add-ultimate-handler`, `dismiss-unity-modals`, `restart-unity-mcp`, `modify-ui`, `feel-integration`, `find-and-import-assets`, `mm-progress-bars`, `telemetry-analytics`, `translate-game-name`, `generate-sprite-variants`.
  - `gws-docs`/`gws-drive` only if Lance uses them.
- [ ] **Update** `add-enemy-type` and `generate-enemy-prefabs` for the current CSV columns (`knockbackResistance`, `enabled`) and `SurvivorsSpawner`/`allowedEnemyIds` gating instead of `WaveSpawner`.
- [ ] **New skills:**
  - `add-objective` (`ISurvivorsObjective`, registering in `SurvivorsObjectiveManager`, cleanup interplay, XP).
  - `add-campaign-node` (after plan 02's asset format).
  - `add-defense-type` (`DefenseStructure` subclass, build wheel entry, supply costs, assembly animation, MMF).
  - `add-captain`.
  - `add-meta-perk` (`MetaPerkDefinitionSO` + `RunSession` application).
  - `add-shop-item`.
  - `ui-mockup` (the house UI rules: prefab/data-driven, Synty art, Texturina/Grenze, human review flag).
- [ ] Update the skills list in `/CLAUDE.md`.
