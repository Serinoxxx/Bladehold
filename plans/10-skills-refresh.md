# 10: Skills refresh

**Goal:** one skill set that both Claude Code (`.claude/skills/`) and Antigravity (`.agents/skills/`) use, reflecting the current game. Decide with Lance whether one folder is canonical and the other a copy/junction (a Windows directory junction keeps them in sync).

**Done 2026-09-26.** Lance chose `.claude/skills/` as canonical; `.agents/skills` is now a gitignored directory junction to it (recreate on a fresh clone: `cmd /c mklink /J .agents\skills .claude\skills`; git follows junctions, hence the ignore). `gws-docs`/`gws-drive` dropped.

## Tasks

- [x] **Retire** `add-player-class` (class system deleted) (`balance-sim` was already deleted in plan 08). (`editor-wiring-todo` and `editor-wire` were repointed to `plans/editor/` checklists on 2026-09-25, so keep them. Fold any useful Unity MCP wiring know-how into `unity-editor-mcp`.)
- [x] **Rewrite** `add-skill-line` → `add-draft-card`:
  - `DraftUpgrades.csv` columns, `;`/`|` syntax, categories, `targetSlot`, `isUltimate`/`isDuo`/`prerequisiteElements`.
  - `StatType` registration, where effects are applied.
  - Test coverage, a localization key, an icon.
- [x] **Port from `.agents/skills`**, then review each for staleness against the new CLAUDE.md:
  - `changelog`, `maintain-mechanic-tests`, `test-mechanic`, `add-ultimate-handler`, `dismiss-unity-modals`, `restart-unity-mcp`, `modify-ui`, `feel-integration`, `find-and-import-assets`, `mm-progress-bars`, `telemetry-analytics`, `translate-game-name`, `generate-sprite-variants`.
  - `gws-docs`/`gws-drive` only if Lance uses them.
  - Done. Merged `maintain-mechanic-tests` into `test-mechanic`, and `dismiss-unity-modals` + `restart-unity-mcp` into `unity-editor-mcp` (its modal script now lists by default and needs `-Dismiss`). `feel-integration`'s 23 copied Feel docs became one `references/catalog.md`. `changelog.ps1` rewritten (it wrote LF lines into the CRLF changelog). `generate-sprite-variants` now runs as a standalone `dotnet run` project outside `Assets/`. Everything else kept and corrected.
- [x] **Update** `add-enemy-type` and `generate-enemy-prefabs` for the current CSV columns (`knockbackResistance`, `enabled`) and `SurvivorsSpawner`/`allowedEnemyIds` gating instead of `WaveSpawner`.
- [x] **New skills:**
  - `add-objective` (`ISurvivorsObjective`, registering in `SurvivorsObjectiveManager`, cleanup interplay, XP).
  - `add-campaign-node` (after plan 02's asset format).
  - `add-defense-type` (`DefenseStructure` subclass, build wheel entry, supply costs, assembly animation, MMF).
  - `add-captain`.
  - `add-meta-perk` (`MetaPerkDefinitionSO` + `RunSession` application).
  - `add-shop-item`.
  - `ui-mockup` (the house UI rules: prefab/data-driven, Synty art, Texturina/Grenze, human review flag).
- [x] Update the skills list in `/CLAUDE.md`.

## Findings (not fixed; code was out of scope)

Found while grounding the skills. Each needs a decision or its own session.

- **Frozen Pass and Ancient Garden never appear on the map.** `tier3_frozen_pass` and `tier6_ancient_garden` are in `tiers` but missing from `allNodes`, both in `Resources/CampaignGraph.asset` (19 entries, should be 21) and in `CampaignGraphSO.BuildDefaultGraph()`. The map and `GetNodeById` only read `allNodes`.
- **Crystal Water does nothing.** The shop charges 25 gold and sets `RunSession.CrystalWaterWavesRemaining`, but nothing applies the move-speed bonus.
- **Ultimate pick doesn't configure the handler at pick time.** `DraftUpgradeService.ApplyUpgrade` calls `player.GetComponentInChildren<PlayerUltimateController>()` on the child `Player`, but the controller is on the root. It only works because `SyncActiveUltimateHandler` configures it on first use.
- **Ultimate loose ends:** `SwordBladeTempestUltimate` isn't on Player.prefab and is added at runtime; `MaceUltimate` is enabled by default while the others are disabled; `MageUltimate`/`SwordMountUltimate` are unreachable; ultimate card text says "Press Q", but Q is Dismount per CLAUDE.md.
- **Draft cards aren't localized.** `SkillNode.locKey` exists but `ConvertToSkillNode` never sets it.
- **Meta perk `prerequisites` is ignored** by `MetaUpgradesUI.PurchasePerk`.
- **`UpdateBar` every frame** in `UI/AttackChargeBarUI.cs` and `UI/HorseStaminaUI.cs` restarts the lerp each frame; should be `SetBar`. `UI/UltimateBarUI.cs` logs on every charge change.
- **Probable dead class-era UI:** `UI/RageBarUI.cs`, `UI/MageElementUI.cs`, `Card_Class.prefab`, `ClassPreviewIdle.controller`/`ClassPreviewRT`, `Old HUD/[DEPRECATED] HUD Canvas.prefab`.
- **Code-built visuals / fallbacks still in towers:** `DefenseAssemblyAnimation` drop mode builds meshes with `AddComponent<MeshRenderer>`, instantiates the prefab twice, and its rise mode re-enables components authored as disabled; `ArrowTowerDefense` has a direct-damage fallback. Tower numbers are hard-coded rather than on SOs. The build wheel has 6 hand-placed buttons (`sliceButtonPrefab` unused).
- **Campaign UI:** `CampaignTooltipUI` has no Fishing Pond case (shows grey "Sector"); the campaign setup tool rebuilds `CampaignNodeButton.prefab` from code, wiping hand edits.
- **Telemetry:** end-of-run payload only sent on death (not victory/campaign end); `classId` is a class-era leftover; the uploader adds GameAnalytics at runtime.
- **Docs:** `Campaign/CLAUDE.md` says depth scaling is planned (it's live via `SectorThreat`); the `CampaignNodeSO.endsCampaign` tooltip and plan 02 describe the old demo cutoff; plan 02 says 19 nodes (21 now). CHANGELOG 0.1.28 has internal names/dev-tooling entries. HUD canvas scales from 3840×2160, other screens 1920×1080.
- **Decision for Lance:** the build bumps `bundleVersion` before copying the changelog, so a 0.1.29 build ships notes headed 0.1.28. The `changelog` skill writes under the current `bundleVersion`.
