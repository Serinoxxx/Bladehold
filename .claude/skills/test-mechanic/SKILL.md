---
name: test-mechanic
description: Use when adding, updating or running a Bladehold mechanic check (weapon reach, damage math, draft cards, armour, ultimates, towers, bosses) — the WeaponReachBenchmark suite, the smaller Bladehold/Tests menu items, and ad-hoc checks through unity-mcp execute_code.
---

# Mechanic checks

**Lance runs the suites himself unless he asks.** Add or update the check, compile-check it (`/compile-check`, the `Assembly-CSharp-Editor.csproj` build), and say in your summary which menu item to run. Test strategy is being decided in `plans/11-test-strategy.md`; don't invent a new framework here.

## What exists

There are **no NUnit / Test Runner tests** (`com.unity.test-framework` is installed but unused, no test asmdefs). The checks are Editor menu scripts in `Assets/Bladehold/Bladehold Scripts/Editor/`:

| Menu item | Script | Mode |
|---|---|---|
| **Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark** | `WeaponReachBenchmark.cs` | Edit mode. The main regression suite, ~26 numbered sections |
| **Bladehold/Tests/Draft Catalog Loading (Edit Mode)** | `TestDraftCatalogLoading.cs` | Edit mode |
| **Bladehold/Tests/Draft Weapon Charges (Play Mode)** | `TestDraftWeaponCharges.cs` | Enter Play mode with the Player first |
| **Bladehold/Run Battering Ram Integration Test** | `BatteringRamTest.cs` | Edit mode, prefab checks |
| **Bladehold/Test Dodge Mechanics** | `TestDodgeMechanics.cs` | Enters Play mode itself |

The benchmark logs one report ending `BENCHMARK COMPLETE: N PASSED | M FAILED`; lines are `  - ... [PASSED]` or `  - [FAIL] ...`. Any failure needs fixing or explaining.

For feel in Play mode: DevConsole (backquote) **1-Click Combat Scenarios**, and the `Enemy Zoo` scene.

## When to add a check

Plan 11's proposed policy, until it's settled: add a check for logic with rules or formulas (stat math, draft pools, `RunSession` state, campaign/sector rules, supply) and a regression check for each bug fixed. Nothing mandatory for pure feel or visual changes. If you change behaviour an existing section asserts, update that section in the same change.

## Adding a benchmark section

Append a new numbered block before the `BENCHMARK COMPLETE` footer in `RunBenchmark()`. Copy section 26's shape:

```csharp
// 27. SHORT TITLE
sb.AppendLine("\n### 27. SHORT TITLE");
{
    int savedGold = RunSession.InRunGold;              // snapshot any static state you touch
    var spawned = new List<GameObject>();
    try
    {
        void Check(bool ok, string pass, string fail)
        {
            sb.AppendLine(ok ? $"  - {pass} [PASSED]" : $"  - [FAIL] {fail}");
            if (ok) passedCount++; else failedCount++;
        }
        // arrange, act, Check(...)
    }
    catch (Exception ex) { sb.AppendLine($"  - Section 27 exception: {ex.Message} [FAILED]"); failedCount++; }
    finally
    {
        RunSession.InRunGold = savedGold;              // restore; the benchmark shares the Editor's statics
        foreach (var go in spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }
}
```

## Edit-mode traps

- **`Destroy` does nothing in Edit mode.** Use `DestroyImmediate` in `finally`, including any service you created (`DraftUpgradeService.GetOrCreateInstance()` can create one).
- **Awake/Start/Update don't run** on normal MonoBehaviours in Edit mode, and `Time.deltaTime` is 0. Call the public method under test directly, and step time-based logic in discrete steps.
- **Restore statics.** `RunSession`, draft levels (`drafts.DebugSetDraftLevel(def, level)`), elemental slots and `SaveSystem` data outlive the run. Snapshot and restore them; never leave a real save modified.
- **`PlayerStats`:** `final = (base + Σflat) × (1 + Σpercent)`. A stat with base 0 stays 0 under percent modifiers, so `SetBase` first.
- **Player hierarchy:** `Player`/`PlayerStats` sit on the child `SidekickSyntyCharacter`; `PlayerWeaponManager`/`PlayerUltimateController` on the root. From `Player.prefab` use `GetComponentInChildren<T>(true)`.
- **Unity null:** `?.` and `??` skip Unity's destroyed/missing-object check. Use explicit `== null` on `UnityEngine.Object`s.
- **Raycasts from inside a collider** hit it at distance 0. Offset the origin or filter the caster's colliders.
- `AssetDatabase.LoadAssetAtPath` is fine in Editor test code (it's banned only in gameplay code). Prefabs live in `Assets/Bladehold/Bladehold Prefabs/` (`Player.prefab`, `Goblin Enemy Variant.prefab`, `Training Dummy Goblin.prefab`, ...).

## Running (only when Lance asks, or via MCP when connected)

- **The benchmark runs in whatever scene is open and leaves junk objects in it.** Open `MainMenu` first, run it (`execute_menu_item` with the menu path above), read the console, then reopen the scene with `OpenScene(..., Single)` to discard. **Never save a scene afterwards.** The BuildWheel check also fails if the Survivors scene is open.
- Headless, only with the Editor closed (the project lock blocks it otherwise):
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath "C:\Users\lance\source\repos\My project" -executeMethod WeaponReachBenchmark.RunBenchmarkMenuItem -logFile Temp/benchmark.log
  Select-String -Path Temp/benchmark.log -Pattern "BENCHMARK COMPLETE|\[FAIL\]"
  ```

## Ad-hoc checks via `execute_code`

For a one-off question ("does this card's math come out right?") run a snippet through unity-mcp `execute_code` instead of adding permanent code. Same rules: open `MainMenu` or a scene you won't save, snapshot and restore statics, `DestroyImmediate` everything in `finally`, `return` a report string, and **never call `SaveOpenScenes`**. If the check is worth keeping, turn it into a benchmark section.
