---
name: maintain-mechanic-tests
description: Use when creating, updating, or maintaining automated in-engine mechanic tests in Bladehold — ensures all weapons, armour sets, draft cards, meta perks, and combat mechanics have automated test coverage in WeaponReachBenchmark.cs or Unity Test Runner.
---

# Maintain Mechanic Tests

Bladehold enforces a mandatory testing rule: **Whenever adding or updating a gameplay mechanic, weapon, armour set, draft card, or meta perk, you MUST add or update an automated test in `WeaponReachBenchmark.cs` (and/or Unity Test Runner) and verify it passes.**

This skill provides the standard patterns, test harness architecture, execution instructions, and checklists.

---

## 1. Test Harness Architecture: `WeaponReachBenchmark.cs`

All automated mechanics assertions are centralized in `Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs`.
This suite runs both:
1. **In-Editor via Menu Item**: `Bladehold > Benchmarks > Run Weapon Reach & Damage Benchmark` (or via MCP `execute_menu_item`).
2. **Headless CLI via Batchmode**:
   ```pwsh
   & "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath "c:\Users\lance\source\repos\My project" -executeMethod WeaponReachBenchmark.RunBenchmarkMenuItem -logFile "Temp/benchmark.log"
   Get-Content "Temp/benchmark.log" | Select-String -Pattern "BENCHMARK COMPLETE", "PASSED", "FAILED"
   ```

### Output Protocol
The suite logs a structured summary containing:
```
=================================================
       BLADEHOLD AUTOMATED MECHANICS BENCHMARK   
=================================================
...
BENCHMARK COMPLETE: X PASSED | Y FAILED
=================================================
```
If `failedCount > 0`, the test suite fails and must be fixed before releasing or completing the task.

---

## 2. Core Rules for Mechanic Tests

1. **Immediate Teardown with `DestroyImmediate`**:
   - Any spawned `GameObject` (player dummies, enemies, projectile instances) **must** be destroyed in a `finally` block or right after the assertion using `UnityEngine.Object.DestroyImmediate(obj)`.
   - Never leave orphan objects in memory or dirty scenes with test artifacts.

2. **Player Hierarchy Traps**:
   - Remember: In `Player.prefab`, `Player.cs` / `PlayerStats` lives on a child GameObject (`SidekickSyntyCharacter`), while root systems (`PlayerWeaponManager`, `PlayerUltimateController`) live on the root.
   - When unit-testing isolated components, instantiate minimal test game objects with only the required dependencies:
     ```csharp
     GameObject dummy = new GameObject("Test_Player");
     PlayerStats stats = dummy.AddComponent<PlayerStats>();
     Health health = dummy.AddComponent<Health>();
     ```

3. **`PlayerStats` Stat Types & Base Values**:
   - Remember that for `ModifierKind.Percent` modifiers, `PlayerStats` calculates:
     $$\text{Value} = (\text{Base} + \text{Flat}) \times (1 + \text{Percent})$$
   - If a stat defaults to `0` (such as `LifeStealPercent`, `CriticalStrikeChance`, `SprintSpeed`), adding a percent modifier will still yield `0` unless a base value has been explicitly set:
     ```csharp
     stats.SetBase(StatType.LifeStealPercent, 0.20f); // Set base before testing percent scaling
     ```

4. **Event Cleanup**:
   - If hooking into `Health.TryPreventDeath`, `Health.TryBlockDamage`, or `Health.ScaleDamageTaken`, ensure the dummy object is destroyed so delegate hooks do not leak.

---

## 3. Standard Test Templates

### Pattern A: Damage & Life Steal / Healing
Tests that damage triggers heal the attacker or trigger life steal mechanics:
```csharp
// Example: Life Steal
GameObject pObj = new GameObject("Test_LifestealPlayer");
Health pHealth = pObj.AddComponent<Health>();
pHealth.SetMaxHealth(100f);
pHealth.Revive(50f); // Injured player at 50/100 HP

PlayerStats stats = pObj.AddComponent<PlayerStats>();
stats.SetBase(StatType.LifeStealPercent, 0.20f); // 20% Life Steal

float damageDealt = 50f;
float lifestealFrac = stats.GetValue(StatType.LifeStealPercent);
if (lifestealFrac > 0f)
{
    float healAmount = damageDealt * lifestealFrac;
    pHealth.Heal(healAmount);
}

if (Mathf.Approximately(pHealth.CurrentHealth, 60f)) {
    // PASS
}
UnityEngine.Object.DestroyImmediate(pObj);
```

### Pattern B: Cheat-Death / Revive Hook (`TryPreventDeath`)
Tests that lethal hits are intercepted and health is restored (e.g. `Second Wind`, `DeathNova`):
```csharp
GameObject pObj = new GameObject("Test_RevivePlayer");
Health pHealth = pObj.AddComponent<Health>();
pHealth.SetMaxHealth(200f);
pHealth.Revive(200f);

bool revived = false;
pHealth.TryPreventDeath += () =>
{
    if (canRevive)
    {
        pHealth.Revive(pHealth.MaxHealth * 0.5f);
        revived = true;
        return true; // Cancels lethal hit
    }
    return false;
};

// Deal lethal blow
pHealth.ReceiveDamage(new Damage { value = 500f });

if (revived && Mathf.Approximately(pHealth.CurrentHealth, 100f) && !pHealth.IsDead) {
    // PASS
}
UnityEngine.Object.DestroyImmediate(pObj);
```

### Pattern C: Physical Prefab & Component Spawning (e.g. Dash Trails)
Tests that actions physically spawn game entities with expected components:
```csharp
GameObject trailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Effects/FireTrailSegment.prefab");
// Simulate discrete steps over movement vector
List<GameObject> spawnedSegments = new List<GameObject>();
for (int i = 0; i < 5; i++)
{
    Vector3 pos = Vector3.forward * (i * 0.5f);
    GameObject seg = UnityEngine.Object.Instantiate(trailPrefab, pos, Quaternion.identity);
    spawnedSegments.Add(seg);
}

// Verify physical existence and components
bool valid = spawnedSegments.Count == 5 && spawnedSegments.TrueForAll(s => s.GetComponent<FireTrailSegment>() != null);

// Teardown
foreach (var s in spawnedSegments) UnityEngine.Object.DestroyImmediate(s);
```

### Pattern D: Armour Set & Meta Perk Stat Modifiers
Tests that equipping an armour set or purchasing a meta perk correctly layers stats onto `PlayerStats` or `RunSession`:
```csharp
SaveData save = SaveSystem.Load() ?? new SaveData();
save.purchasedMetaPerks.Clear();
save.purchasedMetaPerks.Add("greed");
SaveSystem.Save(save);

RunSession.StartNewRun();
RunSession.AddInRunGold(100); // 100 * 1.10 = 110
if (RunSession.InRunGold == 110) {
    // PASS
}
```

---

## 4. Maintenance Checklist for New Features

When you add or modify any of the following, check off these steps:

- [ ] **New Weapon**:
  - Add reach and `DamageTrigger` inspection to Section 1 of `WeaponReachBenchmark.cs`.
  - Assert blade tip/base distance or collider radius > 0.
- [ ] **New Armour Set / Stat Perk**:
  - Add an assertion checking that equipping/unlocking applies the exact stat modifier to `PlayerStats`.
- [ ] **New Draft Card**:
  - Verify that `DraftUpgrades.csv` parses cleanly and the card's `StatType` / effects progression values match the design.
- [ ] **New Active Mechanic / Proc (Burn, Bleed, Stun, Trail, Summon)**:
  - Add an end-to-end behavioral test in Section 7 asserting that the status effect, damage over time, or physical object actually spawns and alters health/movement.
- [ ] **Run and Verify**:
  - Run the benchmark via headless batchmode or Unity MCP `execute_menu_item`.
  - Ensure all assertions output `[PASSED]` with 0 failures.
