---
name: telemetry-analytics
description: Use when adding or changing Bladehold run telemetry — the per-run balance CSVs from RunTelemetry, the end-of-run payload, GameAnalytics design/progression events or the playtest webhook — or when analytics aren't arriving.
---

# Run telemetry and GameAnalytics

Both pieces self-bootstrap via `[RuntimeInitializeOnLoadMethod]` into `DontDestroyOnLoad` objects. Nothing goes in a scene.

| File (`Assets/Bladehold/Bladehold Scripts/`) | Role |
|---|---|
| `Analytics/RunTelemetry.cs` | Pure listener (`GameLoopManager` wave events, player `Health.OnDamaged`/`OnDied`, the player-stats `DamageTrigger.OnHit`, `InputReader`, `PlayerDodge`, `PlayerMount`, gate/chest events). Writes one CSV per scene run to `persistentDataPath/Telemetry/run_<timestamp>.csv` (rows: `run_start`, `wave_clear`, `death`, `run_summary`). On player death it raises `OnRunEnded(RunTelemetryData)`. |
| `Analytics/PlaytestTelemetryUploader.cs` | Defines `RunTelemetryData`, initialises the GameAnalytics SDK, subscribes to `OnRunEnded`, sends design/progression events and optionally POSTs JSON to `webhookUrl` (Discord or Apps Script; empty = off). |
| `Editor/AutoVersionIncrementer.cs` | Pre-build hook: bumps the patch of `bundleVersion` (which `Application.version` and the GA build label use) and copies `CHANGELOG.md` into StreamingAssets. |

The SDK is `com.gameanalytics.sdk` via the OpenUPM scoped registry in `Packages/manifest.json`. Its keys are in `Assets/Resources/GameAnalytics/Settings.asset` (Windows Player only; the uploader maps those keys to WindowsEditor at play time).

The plans/README roadmap relies on these CSVs: Phase 4's balance pass is driven by playtesters' `RunTelemetry` files.

## Adding a metric

1. Add the field to `RunTelemetryData` (`PlaytestTelemetryUploader.cs`).
2. In `RunTelemetry.cs`: add a counter, reset it in `BeginRun`, subscribe to an **existing event** in `Start` and unsubscribe in `Unbind`, then fill the field in `HandlePlayerDied`. Stay a listener: never change gameplay, and never poll object counts for deaths (`Health.OnDied` only). A missing dependency logs one warning and gets skipped, so telemetry can never break the game.
3. Per-wave CSV column: add it to `Header` **and** as a named parameter on `AppendRow`, so the columns can't drift.
4. Send it in `PlaytestTelemetryUploader.SendToGameAnalytics`: `GameAnalytics.NewDesignEvent("RunStats:MyMetric", data.myMetric);`. Keep the existing `Category:Name` naming (`RunStats:`, `Damage:`, `DeathBy:`, `GateDestroyedBy:`).
5. `/compile-check`.

## Known gaps (check before relying on them)

- `OnRunEnded` fires **only on death**. Sector victories and campaign completion send nothing to GA and write no `run_summary`, and each battle scene reload starts a new CSV. A campaign-level run would need a hook from `CampaignManager`/`RunSession`.
- `RunTelemetryData.classId` and the CSV `class=` detail are leftovers from the deleted class system. They currently hold `SaveData.equippedArmourSet`.
- `PlaytestTelemetryUploader` builds its own GameObject and `AddComponent<GameAnalytics>()` at runtime, which breaks the house "no runtime assembly" rule. Don't copy that pattern.

## Testing

- **Editor**: GA only logs locally (`InfoLogEditor` in `Settings.asset`) and nothing reaches the dashboard. Check the CSV path printed in the console (`RunTelemetry: logging this run to ...`) and the `[PlaytestTelemetryUploader]` logs on death.
- **Live dashboard**: make a standalone build with the normal Build Settings scene list (`Bladehold Test Scene` isn't in the build any more). Die once, wait a few seconds before quitting, then check GameAnalytics under **Realtime > Live Events**. Building bumps `bundleVersion`, so tell Lance.
- A GA `ArgumentOutOfRangeException` in the Editor means the `Platforms`/`gameKey`/`secretKey`/`Build` array lengths in `Settings.asset` don't match.
