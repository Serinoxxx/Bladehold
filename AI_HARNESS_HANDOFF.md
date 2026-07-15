# AI Harness — Execution Handoff

Living checklist for the approved plan at `C:\Users\lance\.claude\plans\create-an-ai-harness-valiant-spark.md`.
Check items off as they complete; note deviations inline as sub-bullets. Any agent can resume from here.

**Status: IN PROGRESS — started 2026-07-15.**

## Phase A — Unity MCP setup

- [x] A1. Add `com.coplaydev.unity-mcp` (git URL `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`) to `Packages/manifest.json`
- [x] A2. Author repo-root `.mcp.json` with the unityMCP HTTP entry (`http://localhost:8080/mcp`; confirm port from the wizard)
- [ ] A3. **USER (Editor):** open/focus the Unity Editor so the package resolves; run the MCP for Unity setup wizard (installs `uv`/Python server if flagged); `Window > MCP for Unity` shows "Connected"
- [ ] A4. Verify bridge from a fresh Claude Code session: one read-only MCP call (console read / scene info) succeeds
- [ ] A5. Commit Phase A to `main` + push

### Manual verification (Phase A)
- [ ] With the Editor closed, an MCP call fails with a clear connection error (documents the "Editor must be open" constraint)
- [ ] Port in `.mcp.json` matches the wizard's actual configuration

## Phase B — Balance simulator v1

Code lives in `Assets/Bladehold/Bladehold Scripts/Editor/BalanceSim/`; data in `Assets/Bladehold/Config/`.

- [x] B1. `Config/SimProfiles.csv` (bad/average/good rows, columns per plan)
- [x] B2. `Config/SimPacingRules.csv` (starter rules per plan)
- [x] B3. Core code: `SimConfig.cs`, `PlayerProfile.cs`, `SimWorld.cs`, `SimOverrides.cs`
- [x] B4. Engine: `SimEngine.cs`, `SpawnModel.cs` (mirrors WaveSpawner.cs:405–450), `CombatModel.cs` (mirrors DamageTrigger.cs:388–458 + CoinDropper.cs:97–130), `SimStats.cs` (hidden-GO PlayerStats), `UpgradePolicy.cs`
- [x] B5. Results & rules: `SimResults.cs`, `PacingRules.cs`, `ReportWriter.cs` (waves.csv, summary.json, findings.json, optional trials.csv) + shared `SimRunner.cs`
- [x] B6. Entry points: `BalanceSimCli.cs` (headless, exit codes 0/1/2, `started` marker; executeMethod name is `Bladehold.BalanceSim.BalanceSimCli.Run`), `BalanceSimWindow.cs` (`Bladehold > Balance Simulator`, minimal: config strip + table + findings)
- [x] B7. Add `BalanceReports/` to `.gitignore`
- [x] B8. `/compile-check` — 0 errors (new files registered in `Assembly-CSharp-Editor.csproj`; csproj edits not committed)
- [x] B9. Headless baseline run on shipped data completes, exit 2 (pacing fails = findings, see below), emits waves.csv + summary.json + findings.json (`BalanceReports/baseline/`)
- [x] B10. Reproducibility: same seed twice → byte-identical waves.csv (verified via file hash)
- [x] B11. Directional sanity: `player.maxHealth=100` moves bad_noupgrades median death wave 2 → 5 ✓
- [x] B12. Rules kept as *intent* bands (not auto-pass); added `bad_noupgrades` profile so the no-upgrade probe runs by default, scoped `waves_not_sluggish` to waves 3+, and unmatched rules now emit `skipped` findings
  - **Baseline findings (2 fails, real):** bad players die at wave 2 median (intent band 3–6 — early game may be too harsh for weak players); good-player survival to wave 8 is 38.5% vs the 90% intent (bomber one-shots a 10-HP player from wave 5). Calibration (Phase D) will firm up how much of this is model vs game.
- [ ] B13. Commit Phase B to `main` + push

### Manual verification (Phase B)
- [ ] `Bladehold > Balance Simulator` opens, runs on shipped data, table + findings render, "Open report folder" works
- [ ] Baseline verdicts feel believable vs. real play experience (user judgment)
- [ ] Unknown override key (`-simSet playr.maxHealth=5`) exits 1 with a clear error

## Phase C — Skills

- [x] C1. `.claude/skills/unity-editor-mcp/SKILL.md` — driving the Editor via MCP (preconditions, menu items, SO/prefab/scene wiring, console, play-mode + DevConsole cheats, TODO.md fallback)
- [x] C2. `.claude/skills/editor-wire/SKILL.md` — execute a TODO.md wiring checklist via MCP, check items off, mark human-only leftovers
- [x] C3. `.claude/skills/balance-sim/SKILL.md` — run sim (headless + window), overrides, verdict interpretation, iteration loop
- [x] C4. Finish-protocol touch-ups: `add-enemy-type`, `add-skill-line`, `add-player-class`, `generate-enemy-prefabs` now reference `/balance-sim` and `/editor-wire`/MCP verification
- [ ] C5. Dry-run each new skill once on a real toy task
  - `balance-sim`: exercised for real (baseline + repro + hp100 runs) ✓
  - `unity-editor-mcp` / `editor-wire`: **BLOCKED on A3/A4** (needs the Editor open with the MCP bridge connected)
- [ ] C6. Commit Phase C to `main` + push

## Phase D — Simulator v2

- [ ] D1. EditorWindow charts (HP bands, clear-time curves, death histogram via Handles/EditorGUI)
- [ ] D2. `report.html` — self-contained, inline-JS SVG charts
- [ ] D3. `CalibrationLoader.cs` — Telemetry parse, per-wave drift report (`calibration.csv` + MAPE in summary.json), purchase-replay mode
- [ ] D4. Commit Phase D to `main` + push

v3 (calibration parameter fitting; Berserker/Mage class support) is deliberately deferred — see plan.

## Deviations / notes

(none yet)
