---
name: telemetry-analytics
description: Use when adding telemetry events, tracking player metrics, modifying GameAnalytics integration, extending RunTelemetry payloads, or troubleshooting analytics in Bladehold.
---

# Bladehold Telemetry & GameAnalytics Integration Guide

This skill documents how telemetry and analytics are tracked in Bladehold, how data flows from gameplay to GameAnalytics and Discord webhooks, and how to safely extend or debug the system.

---

## 1. Architecture & Data Flow

```
[ Gameplay Events ]
       │
       ▼
[ RunTelemetry.cs ]  ──────▶  Tracks run metrics (wave, time, damage, kills, dodges, fatal enemy, etc.)
       │
       ▼ (fires OnRunEnded event)
[ PlaytestTelemetryUploader.cs ]
       ├───▶ [ GameAnalytics SDK ]  ──▶ Progression, Design & Custom Events (sent on Standalone Builds)
       └───▶ [ Webhook Routine ]    ──▶ Discord / Google Apps Script JSON Payload (if webhookUrl configured)
```

---

## 2. Key Files & Components

| File | Path | Role |
| :--- | :--- | :--- |
| **`RunTelemetry.cs`** | `Assets/Bladehold/Bladehold Scripts/Analytics/RunTelemetry.cs` | Captures run metrics during gameplay and raises `OnRunEnded(RunTelemetryData)`. |
| **`PlaytestTelemetryUploader.cs`** | `Assets/Bladehold/Bladehold Scripts/Analytics/PlaytestTelemetryUploader.cs` | Bootstraps `GameAnalytics`, handles versioning labels, and dispatches data to GameAnalytics & Webhooks. |
| **`AutoVersionIncrementer.cs`** | `Assets/Bladehold/Bladehold Scripts/Editor/AutoVersionIncrementer.cs` | Pre-build hook (`IPreprocessBuildWithReport`) that auto-increments patch versions (`0.1.0` -> `0.1.1`). |
| **`Settings.asset`** | `Assets/Resources/GameAnalytics/Settings.asset` | Serialized GameAnalytics configuration (Game Key, Secret Key, platform mappings). |
| **`manifest.json`** | `Packages/manifest.json` | UPM configuration containing OpenUPM registry and scopes (`com.gameanalytics`, `com.google`). |

---

## 3. How to Add a New Tracked Metric

Follow this 3-step workflow whenever adding a new telemetry metric to run summaries:

### Step 1: Add Field to `RunTelemetryData`
In `PlaytestTelemetryUploader.cs`:
```csharp
[Serializable]
public class RunTelemetryData
{
    // ... existing fields
    public int myNewMetric; // e.g. ultimateAbilitiesUsed
}
```

### Step 2: Record Metric in `RunTelemetry.cs`
In `RunTelemetry.cs`:
- Add tracking variable and reset on run start.
- Increment or set variable during gameplay hooks.
- Assign the value in `HandlePlayerDied()` when populating `RunTelemetryData`:
```csharp
RunTelemetryData data = new RunTelemetryData
{
    // ... existing assignments
    myNewMetric = myNewMetricCounter
};
```

### Step 3: Dispatch to GameAnalytics in `PlaytestTelemetryUploader.cs`
In `PlaytestTelemetryUploader.SendToGameAnalytics(RunTelemetryData data)`:
```csharp
GameAnalytics.NewDesignEvent("RunStats:MyNewMetric", data.myNewMetric);
```

---

## 4. GameAnalytics Event API Quick Reference

| Event Type | API Method | Example Usage |
| :--- | :--- | :--- |
| **Progression** | `GameAnalytics.NewProgressionEvent(status, prog01, prog02)` | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "Run", $"Wave_{wave}");` |
| **Design Metric** | `GameAnalytics.NewDesignEvent(eventName, value)` | `GameAnalytics.NewDesignEvent("Damage:MeleeDealt", meleeDamage);` |
| **Error Logging** | `GameAnalytics.NewErrorEvent(severity, message)` | `GameAnalytics.NewErrorEvent(GAErrorSeverity.Error, "Boss failed to pathfind");` |
| **Resource Tracking** | `GameAnalytics.NewResourceEvent(flowType, currency, amount, itemType, itemId)` | `GameAnalytics.NewResourceEvent(GAResourceFlowType.Source, "Gold", goldEarned, "Loot", "Chest");` |

---

## 5. Testing & Environment Differences

> [!IMPORTANT]
> **Editor vs. Standalone Behavior:**
> - **In Unity Editor (`UNITY_EDITOR`)**: GameAnalytics uses a dummy logger to prevent local dev playtests from corrupting live production metrics. Events log locally to the Unity Console (if `InfoLogEditor` is enabled in `Settings.asset`) but **are not sent to the online web dashboard**.
> - **In Standalone Builds (`UNITY_STANDALONE`)**: The SDK uses the native C++ wrapper and sends real HTTP telemetry payloads directly to the live GameAnalytics dashboard.

### How to Run a Live Dashboard Verification Test:
1. Build the standalone player:
   ```csharp
   UnityEditor.BuildPipeline.BuildPlayer(
       new[] { "Assets/Bladehold/Bladehold Scenes/Bladehold Test Scene.unity" },
       "Builds/BladeholdTest/Bladehold.exe",
       BuildTarget.StandaloneWindows64,
       BuildOptions.None
   );
   ```
2. Launch `Builds/BladeholdTest/Bladehold.exe`.
3. Let a run end (player death), allow 3–5 seconds for HTTP network dispatch, then close the executable.
4. Verify event reception on the GameAnalytics web dashboard under **Realtime > Live Events**.

---

## 6. Common Pitfalls & Checklist

- [ ] **OpenUPM Scoped Registry**: Ensure `Packages/manifest.json` includes both `com.gameanalytics` and `com.google` under `package.openupm.com` scopes so transitive dependencies (`com.google.external-dependency-manager`) resolve cleanly.
- [ ] **Platform Key Index Matching**: Ensure `Settings.asset` has matching array lengths for `Platforms`, `gameKey`, `secretKey`, and `Build` (e.g. index 0 for `WindowsPlayer` and index 1 for `WindowsEditor`) to avoid `ArgumentOutOfRangeException` in Editor mode.
- [ ] **Initialization Order**: `GameAnalytics.Initialize()` must run after the `GameAnalytics` MonoBehaviour component is added to the scene hierarchy (handled automatically by `PlaytestTelemetryUploader.Bootstrap()`).
