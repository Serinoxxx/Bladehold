---
name: test-mechanic
description: Use when running in-editor gameplay integration tests, spawning test dummies, or verifying mechanic math (damage, physics sweeps, status effects, socket deployments, draft availability) via unityMCP execute_code.
---

# In-Editor Mechanic & Integration Testing

Use this skill to run rapid, deterministic, in-editor integration tests for Bladehold mechanics (weapons, ultimate abilities, fort defenses, status effects, enemy AI behaviours, or draft pools) directly through `unityMCP`'s `execute_code` without requiring a human Play Mode run.

---

## Core Rules for In-Editor Testing

1. **Deterministic Setup & Teardown**:
   - Always clean up test objects immediately with `UnityEngine.Object.DestroyImmediate(obj)` in a `finally` block or at the end of the test.
   - Never leave test dummies or temporary projectiles in saved scenes.

2. **The `Time.deltaTime` Trap**:
   - In Editor mode (outside of active Play Mode), `Time.deltaTime` evaluates to `0`.
   - Physics sweeps, projectile travel (`speed * Time.deltaTime`), or timer countdowns will not advance in `Update()` without discrete stepping.
   - When testing projectiles or physics steps, either:
     - Call a discrete `StepSimulation(float dt)` method on the component.
     - Advance simulated positions manually over discrete steps (e.g. `10 steps of 0.05s`).

3. **Unity Object Truthiness vs. C# `null`**:
   - Unity uses "fake null" wrapper objects for unassigned `[SerializeField]` fields in Editor mode.
   - Pure C# `field != null ? field : fallback` will evaluate to `true` and throw an `UnassignedReferenceException` upon field access.
   - **Always use Unity truthiness**: `(field ? field : fallback)` or `(field != null && field)`.

4. **Multi-Class Isolation Verification**:
   - Bladehold maintains separate skill tree CSVs and ScriptableObjects per class:
     - Swordsman: `Assets/Bladehold/Config/SkillTree.csv` & `SkillTreeSO.asset`
     - Berserker: `Assets/Bladehold/Config/SkillTreeBerserker.csv` & `SkillTreeSOBerserker.asset`
     - Mage: `Assets/Bladehold/Config/SkillTreeMage.csv` & `SkillTreeSOMage.asset`
   - Any test verifying upgrade availability, draft candidate pools, or stat unlocks **must loop across all 3 class trees**.

5. **Self-Collision & Spawn Geometry**:
   - Projectiles or raycasts spawned at the center of structures/sockets can immediately intersect parent meshes at `distance: 0`.
   - Always test raycasts with forward offsets or filter out `FortDefense` / caster colliders.

---

## In-Editor Test Recipe Template

Use this C# snippet pattern when authoring an integration test via `unityMCP` `execute_code`:

```csharp
// 1. Open Target Scene
string scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Survivors Scene.unity";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

// 2. Load Prefabs
GameObject dummyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Goblin Enemy Variant.prefab");

// 3. Spawn Test Dummy
Vector3 spawnPos = new Vector3(0f, -6.11f, 10f);
GameObject dummy = UnityEngine.Object.Instantiate(dummyPrefab, spawnPos, Quaternion.identity);
Health health = dummy.GetComponent<Health>();
health.SetMaxHealth(500f);
health.Revive(500f);
float initialHp = health.CurrentHealth;

string testReport = "=== MECHANIC TEST REPORT ===\n";

try
{
    // 4. Execute Mechanic / Ability / Hit
    // E.g. Trigger damage, status, or projectile step
    Damage testDmg = new Damage
    {
        value = 25f,
        type = DamageType.sharp,
        isPlayerDamage = true
    };
    health.ReceiveDamage(testDmg);

    // 5. Assert Values & Math
    float dealt = initialHp - health.CurrentHealth;
    testReport += $"- Expected Damage: 25 | Actual Damage: {dealt}\n";
    testReport += $"- Status: {(dealt == 25f ? "PASSED" : "FAILED")}\n";
}
finally
{
    // 6. Clean Up All Test Instances
    if (dummy != null) UnityEngine.Object.DestroyImmediate(dummy);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
}

return testReport;
```

---

## What to Assert for Common Mechanics

| Mechanic Category | Key Assertions |
|---|---|
| **Melee / Ranged Attacks** | `DamageDealt > 0`, `Health.CurrentHealth == Initial - Damage`, `DamageType` preserved. |
| **Ragdoll & Knockback** | `EnemyRagdoll.IsRagdolled == true`, `KnockbackReceiver.State`, `RagdollDamageMultiplier == 5.0x`. |
| **Corpse Impale / Embed** | `Health.IsDead == true`, `Ragdoll.Pelvis.isKinematic == true`. |
| **Area DoT / Zones** | `DamagePerTick > 0`, `SlowStatus.GetOrAdd() != null`, status duration refreshed. |
| **Card Draft Pools** | Candidate cards count > 0, node is present in `SkillTreeSO` across Swordsman, Berserker, and Mage. |
| **Socket Deployments** | `FortDefenseSocket.IsOccupied == true`, `CurrentDefense.Level == ExpectedLevel`. |
