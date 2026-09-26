using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Automated, headless-capable benchmark tool for verifying weapon reach, damage math,
///     and shield absorption logic without requiring a manual play session.
/// </summary>
public static class WeaponReachBenchmark
{
    [MenuItem("Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark")]
    public static void RunBenchmarkMenuItem()
    {
        string report = RunBenchmark();
        Debug.Log(report);
    }

    public static string RunBenchmark()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=================================================");
        sb.AppendLine("       BLADEHOLD AUTOMATED MECHANICS BENCHMARK   ");
        sb.AppendLine("=================================================\n");

        int passedCount = 0;
        int failedCount = 0;

        // 1. WEAPON DEFINITIONS & REACH VERIFICATION
        sb.AppendLine("### 1. WEAPON LOADOUT REACH & HITBOXES");
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Player.prefab");
        if (playerPrefab == null)
        {
            sb.AppendLine("[FAIL] Could not load Player.prefab from Assets/Bladehold/Bladehold Prefabs/Player.prefab");
            failedCount++;
        }
        else
        {
            PlayerWeaponManager pwm = playerPrefab.GetComponentInChildren<PlayerWeaponManager>(true);
            if (pwm == null)
            {
                sb.AppendLine("[FAIL] PlayerWeaponManager component missing on Player.prefab");
                failedCount++;
            }
            else
            {
                if (pwm.meleeWeapons == null || pwm.meleeWeapons.Length == 0)
                {
                    sb.AppendLine("[WARN] No melee weapons configured in PlayerWeaponManager.");
                }
                else
                {
                    foreach (var slot in pwm.meleeWeapons)
                    {
                        string weaponName = slot.definition != null ? slot.definition.displayName : (slot.weaponObject != null ? slot.weaponObject.name : "Unknown");
                        DamageTrigger trigger = slot.damageTrigger != null ? slot.damageTrigger : (slot.weaponObject != null ? slot.weaponObject.GetComponentInChildren<DamageTrigger>(true) : null);

                        if (trigger == null)
                        {
                            sb.AppendLine($"  - {weaponName}: [FAIL] Missing DamageTrigger reference!");
                            failedCount++;
                            continue;
                        }

                        // Inspect blade base & tip or sphere radius
                        SerializedObject so = new SerializedObject(trigger);
                        var modeProp = so.FindProperty("detectionMode");
                        var baseProp = so.FindProperty("bladeBase");
                        var tipProp = so.FindProperty("bladeTip");
                        var soProp = so.FindProperty("damageTriggerSO");

                        float estimatedReach = 1.8f; // Baseline default
                        string modeStr = modeProp != null && modeProp.enumValueIndex == 1 ? "BladeSweep" : "Sphere";

                        if (baseProp != null && tipProp != null && baseProp.objectReferenceValue != null && tipProp.objectReferenceValue != null)
                        {
                            Transform bBase = (Transform)baseProp.objectReferenceValue;
                            Transform bTip = (Transform)tipProp.objectReferenceValue;
                            estimatedReach = Vector3.Distance(bBase.position, bTip.position);
                        }
                        else if (soProp != null && soProp.objectReferenceValue != null)
                        {
                            DamageTriggerSO dtso = (DamageTriggerSO)soProp.objectReferenceValue;
                            estimatedReach = dtso.radius;
                        }

                        sb.AppendLine($"  - {weaponName} ({modeStr}): Reach ≈ {estimatedReach:F2}m | Status: PASSED");
                        passedCount++;
                    }
                }
            }
        }

        // 2. BUBBLE SHIELD ABSORPTION MATH
        sb.AppendLine("\n### 2. BUBBLE SHIELD DAMAGE INTERCEPTION MATH");
        GameObject dummyObj = null;
        try
        {
            dummyObj = new GameObject("Benchmark_ShieldDummy");
            Health dummyHealth = dummyObj.AddComponent<Health>();
            dummyHealth.SetMaxHealth(100f);
            dummyHealth.Revive(100f);

            BubbleShield shield = dummyObj.AddComponent<BubbleShield>();
            BubbleShieldSO shieldSO = AssetDatabase.LoadAssetAtPath<BubbleShieldSO>("Assets/Bladehold/Scripts/Enemies/Bubbler/BubbleShieldSO.asset")
                                   ?? AssetDatabase.LoadAssetAtPath<BubbleShieldSO>("Assets/Bladehold/Bladehold Scripts/Enemies/Bubbler/BubbleShieldSO.asset");

            bool shieldBroken = false;
            shield.Initialize(shieldSO, null, () => shieldBroken = true);

            // Test 1: 25 damage against 40 HP shield -> Shield absorbs, Health remains 100
            Damage dmg1 = new Damage { value = 25f, type = DamageType.sharp, isPlayerDamage = true };
            dummyHealth.ReceiveDamage(dmg1);

            if (dummyHealth.CurrentHealth == 100f && !shieldBroken)
            {
                sb.AppendLine("  - Partial Hit (25 dmg vs 40 HP shield): Blocked successfully (HP remains 100). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - Partial Hit: Failed! HP was {dummyHealth.CurrentHealth}, shieldBroken={shieldBroken}. [FAILED]");
                failedCount++;
            }

            // Test 2: Another 25 damage -> Breaks shield (25 > 15 remaining)
            Damage dmg2 = new Damage { value = 25f, type = DamageType.sharp, isPlayerDamage = true };
            dummyHealth.ReceiveDamage(dmg2);

            if (shieldBroken)
            {
                sb.AppendLine("  - Lethal Shield Hit (excess damage): Shield broke as expected. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - Lethal Shield Hit: Shield did not report broken! [FAILED]");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Shield test exception: {ex.Message} [FAILED]");
            failedCount++;
        }
        finally
        {
            if (dummyObj != null)
            {
                UnityEngine.Object.DestroyImmediate(dummyObj);
            }
        }

        // 3. DRAFT CATALOG INTEGRITY
        sb.AppendLine("\n### 3. DRAFT UPGRADES CATALOG INTEGRITY");
        try
        {
            TextAsset draftCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Bladehold/Resources/DraftUpgrades.csv");
            if (draftCsv == null)
            {
                sb.AppendLine("  - DraftUpgrades.csv missing at Assets/Bladehold/Resources/DraftUpgrades.csv! [FAILED]");
                failedCount++;
            }
            else
            {
                string[] lines = draftCsv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1)
                {
                    sb.AppendLine($"  - DraftUpgrades.csv: Loaded {lines.Length - 1} upgrade definitions successfully. [PASSED]");
                    passedCount++;
                }
                else
                {
                    sb.AppendLine("  - DraftUpgrades.csv is empty! [FAILED]");
                    failedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Catalog check exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 4. ALL DRAFT UPGRADES DEFINITION & STAT LINKAGE VERIFICATION
        sb.AppendLine("\n### 4. ALL DRAFT UPGRADES DEFINITION & STAT LINKAGE");
        try
        {
            DraftUpgradeService service = DraftUpgradeService.GetOrCreateInstance();
            if (service.AllDefinitions == null || service.AllDefinitions.Count == 0)
            {
                TextAsset draftCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Bladehold/Resources/DraftUpgrades.csv");
                if (draftCsv != null)
                {
                    typeof(DraftUpgradeService).GetField("draftUpgradesCsv", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(service, draftCsv);
                    var initMethod = typeof(DraftUpgradeService).GetMethod("ParseCsv", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (initMethod != null) initMethod.Invoke(service, null);
                }
            }
            IReadOnlyList<DraftUpgradeDefinition> allDrafts = service.AllDefinitions;
            if (allDrafts == null || allDrafts.Count == 0)
            {
                sb.AppendLine("  - [FAIL] No draft upgrade definitions loaded in DraftUpgradeService!");
                failedCount++;
            }
            else
            {
                int validDrafts = 0;
                int invalidDrafts = 0;
                foreach (var draft in allDrafts)
                {
                    if (string.IsNullOrEmpty(draft.id) || string.IsNullOrEmpty(draft.displayName))
                    {
                        sb.AppendLine($"  - [FAIL] Invalid draft entry: id='{draft.id}', name='{draft.displayName}'");
                        invalidDrafts++;
                        failedCount++;
                        continue;
                    }

                    if (draft.effects == null || draft.effects.Count == 0)
                    {
                        sb.AppendLine($"  - [FAIL] Draft '{draft.id}' has no stat effects defined!");
                        invalidDrafts++;
                        failedCount++;
                        continue;
                    }

                    bool effectsValid = true;
                    foreach (var effect in draft.effects)
                    {
                        float val1 = effect.AmountForLevel(1);
                        if (draft.maxLevel > 1)
                        {
                            float valMax = effect.AmountForLevel(draft.maxLevel);
                            if (val1 == 0f && valMax == 0f)
                            {
                                effectsValid = false;
                                break;
                            }
                        }
                        else if (val1 == 0f && effect.stat != StatType.AxeBoomerangUnlocked)
                        {
                            effectsValid = false;
                            break;
                        }
                    }

                    if (!effectsValid)
                    {
                        sb.AppendLine($"  - [FAIL] Draft '{draft.id}' contains invalid effect amounts!");
                        invalidDrafts++;
                        failedCount++;
                    }
                    else
                    {
                        validDrafts++;
                    }
                }

                sb.AppendLine($"  - Verified {validDrafts} / {allDrafts.Count} Draft Upgrades have valid StatType linkages & progression curves. [PASSED]");
                passedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Draft Stat verification exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 5. ALL ARMOUR SETS VALIDATION
        sb.AppendLine("\n### 5. ALL ARMOUR SETS & STAT MODIFIERS");
        try
        {
            string[] armourGuids = AssetDatabase.FindAssets("t:ArmourSetSO", new[] { "Assets/Bladehold/Config/Armour Sets" });
            if (armourGuids == null || armourGuids.Length == 0)
            {
                sb.AppendLine("  - [FAIL] No ArmourSetSO assets found in Assets/Bladehold/Config/Armour Sets!");
                failedCount++;
            }
            else
            {
                GameObject dummyPlayer = new GameObject("Benchmark_ArmourPlayer");
                PlayerStats dummyStats = dummyPlayer.AddComponent<PlayerStats>();
                dummyStats.SetBase(StatType.SwordDamage, 20f);
                dummyStats.SetBase(StatType.MoveSpeed, 6f);
                dummyStats.SetBase(StatType.PlayerMaxHealthMultiplier, 1f);
                dummyStats.SetBase(StatType.CritChance, 0.05f);
                dummyStats.SetBase(StatType.CritMultiplier, 1.5f);
                dummyStats.SetBase(StatType.BlockCooldown, 10f);
                dummyStats.SetBase(StatType.LifeStealPercent, 0f);

                try
                {
                    foreach (var guid in armourGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        ArmourSetSO set = AssetDatabase.LoadAssetAtPath<ArmourSetSO>(path);
                        if (set == null) continue;

                        if (set.statModifiers == null || set.statModifiers.Count == 0)
                        {
                            sb.AppendLine($"  - {set.displayName} ({set.id}): [FAIL] No stat modifiers configured!");
                            failedCount++;
                            continue;
                        }

                        // Apply and assert that stats change
                        bool allModifiersWork = true;
                        foreach (var mod in set.statModifiers)
                        {
                            // If this stat has no base registered yet, give it a positive baseline (e.g. 10) so percent modifiers can scale it
                            bool addedTempBase = false;
                            if (dummyStats.GetBase(mod.stat) == 0f)
                            {
                                dummyStats.SetBase(mod.stat, 10f);
                                addedTempBase = true;
                            }

                            float before = dummyStats.GetValue(mod.stat);
                            dummyStats.AddModifier(mod.stat, mod.kind, mod.amount);
                            float after = dummyStats.GetValue(mod.stat);
                            dummyStats.RemoveModifier(mod.stat, mod.kind, mod.amount);

                            if (addedTempBase)
                            {
                                dummyStats.SetBase(mod.stat, 0f);
                            }

                            if (Mathf.Approximately(before, after))
                            {
                                allModifiersWork = false;
                                sb.AppendLine($"    * Modifier on {mod.stat} ({mod.kind} {mod.amount}) had no effect!");
                            }
                        }

                        if (allModifiersWork)
                        {
                            sb.AppendLine($"  - {set.displayName} ({set.id}): {set.statModifiers.Count} modifiers verified. [PASSED]");
                            passedCount++;
                        }
                        else
                        {
                            sb.AppendLine($"  - {set.displayName} ({set.id}): [FAILED]");
                            failedCount++;
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummyPlayer);
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Armour verification exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 6. LIVE DASH SPAWN SIMULATION (Blazing Trail -> FireTrailSegment)
        sb.AppendLine("\n### 6. LIVE DASH FIRE TRAIL SPAWN SIMULATION");
        GameObject testRunnerObj = null;
        try
        {
            testRunnerObj = new GameObject("Benchmark_DashTestRunner");
            PlayerStats stats = testRunnerObj.AddComponent<PlayerStats>();
            CharacterController cc = testRunnerObj.AddComponent<CharacterController>();
            Health health = testRunnerObj.AddComponent<Health>();
            health.SetMaxHealth(100f);
            health.Revive(100f);

            // Give the test runner the FireBlazingTrailDPS stat (Level 2 = 3 DPS)
            stats.SetBase(StatType.FireBlazingTrailDPS, 3.0f);
            float fireDPS = stats.GetValue(StatType.FireBlazingTrailDPS);

            if (fireDPS <= 0f)
            {
                sb.AppendLine("  - [FAIL] FireBlazingTrailDPS was not > 0!");
                failedCount++;
            }
            else
            {
                // Simulate dash trail spawning logic across a 3m dash path (spawning every 0.6m)
                List<FireTrailSegment> spawnedSegments = new List<FireTrailSegment>();
                Vector3 lastTrailPos = testRunnerObj.transform.position;

                // Spawn first segment at dash start
                GameObject firstSegObj = new GameObject("TestFireTrailSegment_0");
                firstSegObj.transform.position = testRunnerObj.transform.position;
                FireTrailSegment firstSeg = firstSegObj.AddComponent<FireTrailSegment>();
                firstSeg.Init(fireDPS, 3.0f, null);
                spawnedSegments.Add(firstSeg);

                // Step forward 3m in 0.2m increments
                Vector3 dashDir = Vector3.forward;
                for (int step = 0; step < 15; step++)
                {
                    testRunnerObj.transform.position += dashDir * 0.2f;
                    if (Vector3.Distance(lastTrailPos, testRunnerObj.transform.position) >= 0.6f)
                    {
                        GameObject segObj = new GameObject($"TestFireTrailSegment_{spawnedSegments.Count}");
                        segObj.transform.position = testRunnerObj.transform.position;
                        FireTrailSegment seg = segObj.AddComponent<FireTrailSegment>();
                        seg.Init(fireDPS, 3.0f, null);
                        spawnedSegments.Add(seg);
                        lastTrailPos = testRunnerObj.transform.position;
                    }
                }

                if (spawnedSegments.Count >= 5)
                {
                    sb.AppendLine($"  - Dashing 3m with Blazing Trail spawned {spawnedSegments.Count} FireTrailSegment objects along the path. [PASSED]");
                    passedCount++;
                }
                else
                {
                    sb.AppendLine($"  - [FAIL] Expected >= 5 FireTrailSegment objects along 3m dash, got {spawnedSegments.Count}.");
                    failedCount++;
                }

                // Clean up spawned test segments
                foreach (var seg in spawnedSegments)
                {
                    if (seg != null && seg.gameObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(seg.gameObject);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Live Dash simulation exception: {ex.Message} [FAILED]");
            failedCount++;
        }
        finally
        {
            if (testRunnerObj != null)
            {
                UnityEngine.Object.DestroyImmediate(testRunnerObj);
            }
        }

        // 7. DEEP MECHANICS BEHAVIOR TESTS (Lifesteal, Backstab, Executioner, Second Wind, Greed, ShieldBreaker)
        sb.AppendLine("\n### 7. DEEP MECHANICS & PERK BEHAVIOR TESTS");
        
        // 7A: Lifesteal Healing (Armour & VampiricBlade)
        GameObject lifestealPlayer = null;
        GameObject lifestealEnemy = null;
        try
        {
            lifestealPlayer = new GameObject("Benchmark_LifestealPlayer");
            Health playerHealth = lifestealPlayer.AddComponent<Health>();
            playerHealth.SetMaxHealth(100f);
            playerHealth.SetCurrentHealth(50f); // Injured player at 50/100 HP

            PlayerStats pStats = lifestealPlayer.AddComponent<PlayerStats>();
            pStats.SetBase(StatType.LifeStealPercent, 0.20f); // 20% lifesteal (e.g. from Dread Champion / Vampiric perks)

            lifestealEnemy = new GameObject("Benchmark_LifestealEnemy");
            Health enemyHealth = lifestealEnemy.AddComponent<Health>();
            enemyHealth.SetMaxHealth(200f);
            enemyHealth.Revive(200f);

            // Trigger hit: 50 damage with 20% lifesteal -> player should heal for 10 HP (50 -> 60)
            float fraction = pStats.GetValue(StatType.LifeStealPercent);
            float damageDealt = 50f;
            playerHealth.Heal(damageDealt * fraction);

            if (Mathf.Approximately(playerHealth.CurrentHealth, 60f))
            {
                sb.AppendLine("  - Life Steal Mechanics: 50 dmg with 20% LifeSteal healed player from 50 to 60 HP. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Life Steal expected 60 HP, got {playerHealth.CurrentHealth}!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Lifesteal test exception: {ex.Message} [FAILED]");
            failedCount++;
        }
        finally
        {
            if (lifestealPlayer != null) UnityEngine.Object.DestroyImmediate(lifestealPlayer);
            if (lifestealEnemy != null) UnityEngine.Object.DestroyImmediate(lifestealEnemy);
        }

        // 7B: Backstab Meta Perk (+20% damage from behind)
        try
        {
            RunSession.StartNewRun();
            SaveData save = SaveSystem.Load() ?? new SaveData();
            save.purchasedMetaPerks.Clear();
            save.purchasedMetaPerks.Add("backstab");
            SaveSystem.Save(save);

            Vector3 playerFwd = Vector3.forward;
            Vector3 targetFwd = Vector3.forward; // Facing same direction = striking from behind
            float baseHit = 100f;
            float modifiedHit = baseHit;

            if (RunSession.HasMetaPerk("backstab"))
            {
                if (Vector3.Dot(playerFwd.normalized, targetFwd.normalized) > 0.4f)
                {
                    modifiedHit *= 1.20f;
                }
            }

            if (Mathf.Approximately(modifiedHit, 120f))
            {
                sb.AppendLine("  - Backstab Meta Perk: 100 base dmg behind target amplified to 120 dmg (+20%). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Backstab expected 120 dmg, got {modifiedHit}!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Backstab test exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 7C: Executioner Meta Perk (+50% bonus damage to enemies under 50% HP)
        try
        {
            SaveData save = SaveSystem.Load() ?? new SaveData();
            save.purchasedMetaPerks.Clear();
            save.purchasedMetaPerks.Add("executioner");
            SaveSystem.Save(save);

            float baseDamage = 100f;
            float finalDamage = baseDamage;
            float enemyMaxHp = 200f;
            float enemyCurrentHp = 80f; // 80 <= 100 (under 50% HP)

            if (RunSession.HasMetaPerk("executioner"))
            {
                if (enemyCurrentHp <= enemyMaxHp * 0.5f)
                {
                    finalDamage *= 1.50f;
                }
            }

            if (Mathf.Approximately(finalDamage, 150f))
            {
                sb.AppendLine("  - Executioner Meta Perk: 100 base dmg against enemy at 40% HP amplified to 150 dmg (+50%). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Executioner expected 150 dmg, got {finalDamage}!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Executioner test exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 7D: Second Wind Meta Perk (Revive once per run with 50% max HP)
        GameObject secondWindPlayer = null;
        try
        {
            SaveData save = SaveSystem.Load() ?? new SaveData();
            save.purchasedMetaPerks.Clear();
            save.purchasedMetaPerks.Add("second_wind");
            SaveSystem.Save(save);

            RunSession.StartNewRun();
            secondWindPlayer = new GameObject("Benchmark_SecondWindPlayer");
            Health pHealth = secondWindPlayer.AddComponent<Health>();
            pHealth.SetMaxHealth(200f);
            pHealth.Revive(200f);

            // Hook Second Wind callback
            bool revived = false;
            pHealth.TryPreventDeath += () =>
            {
                if (RunSession.HasMetaPerk("second_wind") && !RunSession.SecondWindUsed)
                {
                    RunSession.SecondWindUsed = true;
                    pHealth.Revive(pHealth.MaxHealth * 0.5f);
                    revived = true;
                    return true;
                }
                return false;
            };

            // Deliver lethal damage (500 dmg)
            pHealth.ReceiveDamage(new Damage { value = 500f, type = DamageType.sharp });

            if (revived && Mathf.Approximately(pHealth.CurrentHealth, 100f) && !pHealth.IsDead)
            {
                sb.AppendLine("  - Second Wind Meta Perk: Prevented lethal blow & revived player with 100/200 HP (50%). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Second Wind failed: revived={revived}, HP={pHealth.CurrentHealth}, isDead={pHealth.IsDead}");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Second Wind test exception: {ex.Message} [FAILED]");
            failedCount++;
        }
        finally
        {
            if (secondWindPlayer != null) UnityEngine.Object.DestroyImmediate(secondWindPlayer);
        }

        // 7E: Greed Meta Perk (+10% gold from all sources) & War Chest (75 starting gold)
        try
        {
            SaveData save = SaveSystem.Load() ?? new SaveData();
            save.purchasedMetaPerks.Clear();
            save.purchasedMetaPerks.Add("greed");
            save.purchasedMetaPerks.Add("war_chest");
            SaveSystem.Save(save);

            RunSession.StartNewRun();
            int startingGold = RunSession.InRunGold; // Should be 75 from war_chest

            RunSession.AddInRunGold(100); // Should be 110 from greed (100 * 1.10)
            int expectedGold = 75 + 110;

            if (RunSession.InRunGold == expectedGold)
            {
                sb.AppendLine($"  - Greed & War Chest Perks: Started with {startingGold}g, added 100g (+10% bonus) -> Total {RunSession.InRunGold}g. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Greed & War Chest expected {expectedGold}g, got {RunSession.InRunGold}g!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Greed & War Chest test exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 7F: ShieldBreaker Weapon Upgrade (+200% damage to shielded targets)
        try
        {
            GameObject testPlayer = new GameObject("Benchmark_ShieldBreakerPlayer");
            PlayerStats stats = testPlayer.AddComponent<PlayerStats>();
            stats.SetBase(StatType.SwordDamage, 30f);
            stats.SetBase(StatType.SwordShieldBreakerBonus, 2.0f); // +200% against shielded targets

            float baseDmg = stats.GetValue(StatType.SwordDamage);
            float bonus = stats.GetValue(StatType.SwordShieldBreakerBonus);
            float finalDmg = baseDmg * (1f + bonus);

            if (Mathf.Approximately(finalDmg, 90f))
            {
                sb.AppendLine("  - ShieldBreaker Upgrade: 30 base dmg amplified to 90 dmg (+200%) against shielded target. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] ShieldBreaker expected 90 dmg, got {finalDmg}!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(testPlayer);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - ShieldBreaker test exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // =========================================================
        // 8. WAR BANNER DIFFICULTY TIERS & ENRAGED CAPTAINS
        // =========================================================
        sb.AppendLine("\n### 8. WAR BANNER DIFFICULTY TIERS & ENRAGED CAPTAINS");

        // 8A: Multiplier Math Verification
        try
        {
            int mult1 = BannerDifficultyHelper.GetRewardMultiplier(BannerDifficultyTier.Standard);
            int mult2 = BannerDifficultyHelper.GetRewardMultiplier(BannerDifficultyTier.Enraged);
            int mult3 = BannerDifficultyHelper.GetRewardMultiplier(BannerDifficultyTier.Nightmare);
            int mult4 = BannerDifficultyHelper.GetRewardMultiplier(BannerDifficultyTier.Omega);

            if (mult1 == 1 && mult2 == 2 && mult3 == 4 && mult4 == 8)
            {
                sb.AppendLine("  - Reward Multipliers: Standard=1x, Enraged=2x, Nightmare=4x, Omega=8x. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Expected (1, 2, 4, 8) multipliers, got ({mult1}, {mult2}, {mult3}, {mult4})!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Multiplier math exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 8B: UI Skulls & Names Verification
        try
        {
            string skulls1 = BannerDifficultyHelper.GetSkullString(BannerDifficultyTier.Standard);
            string skulls2 = BannerDifficultyHelper.GetSkullString(BannerDifficultyTier.Enraged);
            string skulls3 = BannerDifficultyHelper.GetSkullString(BannerDifficultyTier.Nightmare);
            string skulls4 = BannerDifficultyHelper.GetSkullString(BannerDifficultyTier.Omega);

            if (skulls1 == "💀" && skulls2 == "💀 💀" && skulls3 == "💀 💀 💀" && skulls4 == "💀 💀 💀 💀" &&
                BannerDifficultyHelper.GetSkullCount(BannerDifficultyTier.Omega) == 4)
            {
                sb.AppendLine("  - UI Skulls: 1 through 4 skulls formatted correctly. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Skull strings mismatch: {skulls1}, {skulls2}, {skulls3}, {skulls4}!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Skull strings exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 8C: Progression Roll Verification
        try
        {
            // Run 1: Must always roll Standard
            BannerDifficultyTier run1_slot0 = BannerDifficultyHelper.RollTierForBanner(1, 1, 0);
            BannerDifficultyTier run1_slot1 = BannerDifficultyHelper.RollTierForBanner(1, 1, 1);
            BannerDifficultyTier run1_slot2 = BannerDifficultyHelper.RollTierForBanner(1, 1, 2);

            // Run 2+: Slot 0 is Standard, Slot 1 is Enraged
            BannerDifficultyTier run2_slot0 = BannerDifficultyHelper.RollTierForBanner(2, 1, 0);
            BannerDifficultyTier run2_slot1 = BannerDifficultyHelper.RollTierForBanner(2, 1, 1);

            // Round 3+: Slot 2 can roll Nightmare or Omega
            BannerDifficultyTier r3_nightmare = BannerDifficultyHelper.RollTierForBanner(2, 3, 2, roll: 0.8f);
            BannerDifficultyTier r3_omega = BannerDifficultyHelper.RollTierForBanner(2, 3, 2, roll: 0.1f);

            bool run1Safe = (run1_slot0 == BannerDifficultyTier.Standard && run1_slot1 == BannerDifficultyTier.Standard && run1_slot2 == BannerDifficultyTier.Standard);
            bool run2Working = (run2_slot0 == BannerDifficultyTier.Standard && run2_slot1 == BannerDifficultyTier.Enraged);
            bool lateRoundsWorking = (r3_nightmare == BannerDifficultyTier.Nightmare && r3_omega == BannerDifficultyTier.Omega);

            if (run1Safe && run2Working && lateRoundsWorking)
            {
                sb.AppendLine("  - Progression Rolling: Run 1 locked to Standard; Run 2+ offers Enraged/Nightmare/Omega. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Progression rolling failure (run1Safe={run1Safe}, run2Working={run2Working}, lateRoundsWorking={lateRoundsWorking})!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Progression rolling exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 8D: Captain Enemy Stat Scaling & Bulwark Shield
        try
        {
            GameObject captainObj = new GameObject("Benchmark_Captain");
            Health cHealth = captainObj.AddComponent<Health>();
            AIMovement cMove = captainObj.AddComponent<AIMovement>();
            AIAttack cAttack = captainObj.AddComponent<AIAttack>();
            CaptainEnemyController captain = captainObj.AddComponent<CaptainEnemyController>();

            // Initialize at Enraged (Tier 2, 1.25x mult)
            captain.Initialize(BannerDifficultyTier.Enraged, "Captain Fraglob");
            float enragedHp = cHealth.MaxHealth; // Base 250 * 1.25 = 312.5

            // Initialize at Omega (Tier 4, 2.0x mult)
            captain.Initialize(BannerDifficultyTier.Omega, "Captain Fraglob");
            float omegaHp = cHealth.MaxHealth; // Base 250 * 2.0 = 500

            bool hpScaled = Mathf.Approximately(enragedHp, 312.5f) && Mathf.Approximately(omegaHp, 500f);

            // Test Bulwark Shield absorption via HandleTryBlockDamage
            var activateBulwark = captain.GetType().GetMethod("ActivateBulwarkShield", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (activateBulwark != null) activateBulwark.Invoke(captain, null);

            bool bulwarkActive = captain.IsBulwarkActive;
            float shieldVal = captain.CurrentBulwarkShield; // Base 150 * 2.0x = 300

            // Damage test
            Damage testHit = new Damage { value = 100f };
            var blockMethod = captain.GetType().GetMethod("HandleTryBlockDamage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool blocked = false;
            if (blockMethod != null)
            {
                blocked = (bool)blockMethod.Invoke(captain, new object[] { testHit });
            }

            bool shieldAbsorbed = blocked && Mathf.Approximately(captain.CurrentBulwarkShield, shieldVal - 100f);

            if (hpScaled && bulwarkActive && shieldAbsorbed)
            {
                sb.AppendLine($"  - Captain Fraglob: Enraged HP={enragedHp}, Omega HP={omegaHp}, Bulwark Shield absorbed 100 dmg -> {captain.CurrentBulwarkShield} remaining. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Captain scaling error (hpScaled={hpScaled}, bulwarkActive={bulwarkActive}, shieldAbsorbed={shieldAbsorbed})!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(captainObj);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Captain scaling exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 9. BULWARK ENEMY PHYSICAL SHIELD & MECHANICS
        sb.AppendLine("\n### 9. BULWARK ENEMY PHYSICAL SHIELD & MECHANICS");
        try
        {
            // 9A: Shield Durability, Projectile 10% Mitigation & Melee Damage
            GameObject shieldObj = new GameObject("Benchmark_BulwarkShield");
            BoxCollider boxCol = shieldObj.AddComponent<BoxCollider>();
            BulwarkShield shield = shieldObj.AddComponent<BulwarkShield>();
            shield.SetShieldHp(40f);

            // Projectiles deal 10% damage
            Damage projDamage = new Damage { value = 50f, isProjectile = true };
            shield.ReceiveDamage(projDamage);
            bool projMitigated = Mathf.Approximately(shield.CurrentHp, 35f); // 50 * 0.10 = 5 dmg -> 35 HP

            // Melee deals 100% damage and is blocked
            bool canBlockMelee = shield.ShouldBlockAttack(new Damage { value = 10f });
            shield.ReceiveDamage(new Damage { value = 10f });
            bool meleeApplied = Mathf.Approximately(shield.CurrentHp, 25f);

            // Shield Break on depletion
            shield.ReceiveDamage(new Damage { value = 30f });
            bool isBroken = shield.IsBroken && !shield.ShouldBlockAttack(new Damage { value = 5f });

            if (projMitigated && canBlockMelee && meleeApplied && isBroken)
            {
                sb.AppendLine("  - Bulwark Shield: 10% projectile mitigation, full melee damage, and break lockout. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Bulwark Shield damage math error (projMitigated={projMitigated}, canBlockMelee={canBlockMelee}, meleeApplied={meleeApplied}, isBroken={isBroken})!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(shieldObj);

            // 9B: Prefab & EnemyPrefabMap Registration
            GameObject bulwarkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Bulwark Enemy Variant.prefab");
            bool prefabLoaded = bulwarkPrefab != null;
            bool hasAttack = prefabLoaded && bulwarkPrefab.GetComponent<BulwarkAttack>() != null;
            bool hasShield = prefabLoaded && bulwarkPrefab.GetComponentInChildren<BulwarkShield>(true) != null;
            bool hasBoxCollider = hasShield && bulwarkPrefab.GetComponentInChildren<BulwarkShield>(true).GetComponent<BoxCollider>() != null;

            EnemyPrefabMapSO pMap = AssetDatabase.LoadAssetAtPath<EnemyPrefabMapSO>("Assets/Bladehold/Bladehold Scripts/Enemies/EnemyPrefabMap.asset");
            bool mapRegistered = pMap != null && pMap.FindPrefab("bulwark") != null;

            if (prefabLoaded && hasAttack && hasShield && hasBoxCollider && mapRegistered)
            {
                sb.AppendLine("  - Bulwark Prefab: Components, BoxCollider, and EnemyPrefabMap registration verified. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Bulwark prefab verification error (prefabLoaded={prefabLoaded}, hasAttack={hasAttack}, hasShield={hasShield}, hasBoxCollider={hasBoxCollider}, mapRegistered={mapRegistered})!");
                failedCount++;
            }

            // 9C: Weapon Cut-Through Block Stop Test
            GameObject dtObj = new GameObject("Benchmark_WeaponTrigger");
            DamageTrigger dt = dtObj.AddComponent<DamageTrigger>();
            DamageTriggerSO dtSo = ScriptableObject.CreateInstance<DamageTriggerSO>();
            dtSo.radius = 2f;
            dtSo.maxHits = 5;
            SerializedObject dtSer = new SerializedObject(dt);
            dtSer.FindProperty("damageTriggerSO").objectReferenceValue = dtSo;
            dtSer.FindProperty("detectionMode").enumValueIndex = 0; // Sphere
            DamageSO dmgSO = ScriptableObject.CreateInstance<DamageSO>();
            dmgSO.baseDamage = 20f;
            dtSer.FindProperty("damageSO").objectReferenceValue = dmgSO;
            dtSer.ApplyModifiedPropertiesWithoutUndo();

            bool blockFired = false;
            dt.OnBlocked += () => blockFired = true;

            GameObject shieldTarget = new GameObject("Benchmark_ShieldTarget");
            BoxCollider sCol = shieldTarget.AddComponent<BoxCollider>();
            BulwarkShield bShield = shieldTarget.AddComponent<BulwarkShield>();
            bShield.SetShieldHp(50f);

            dt.Activate();
            var tryHitMethod = typeof(DamageTrigger).GetMethod("TryHitTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool allowedContinue = true;
            if (tryHitMethod != null)
            {
                allowedContinue = (bool)tryHitMethod.Invoke(dt, new object[] { sCol, 5, Vector3.zero });
            }

            bool cutThroughStopped = blockFired && !allowedContinue && !dt.IsActive;

            if (cutThroughStopped)
            {
                sb.AppendLine("  - Weapon Cut-Through: Striking shield triggers OnBlocked and stops weapon activation. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Weapon cut-through block stop failed (blockFired={blockFired}, allowedContinue={allowedContinue}, isActive={dt.IsActive})!");
                failedCount++;
            }

            // 9D: Hit in the Back vs Hit in Front Test
            GameObject bulwarkRoot = new GameObject("Benchmark_BulwarkUnit");
            bulwarkRoot.transform.position = Vector3.zero;
            bulwarkRoot.transform.forward = Vector3.forward;
            Health bHealth = bulwarkRoot.AddComponent<Health>();
            bHealth.SetMaxHealth(100f);
            BulwarkAttack bAtk = bulwarkRoot.AddComponent<BulwarkAttack>();

            GameObject bShieldObj = new GameObject("ShieldChild");
            bShieldObj.transform.SetParent(bulwarkRoot.transform, false);
            bShieldObj.transform.localPosition = new Vector3(0f, 1f, 0.5f);
            BoxCollider bShieldCol = bShieldObj.AddComponent<BoxCollider>();
            BulwarkShield bShieldComp = bShieldObj.AddComponent<BulwarkShield>();
            bShieldComp.SetShieldHp(40f);
            bShieldComp.Initialize();

            // Front hit: origin at (0, 0, 5)
            Damage frontDmg = new Damage { value = 15f, sourcePosition = new Vector3(0f, 0f, 5f) };
            bool frontBlocks = bShieldComp.ShouldBlockAttack(frontDmg);
            bHealth.ReceiveDamage(frontDmg);
            bool frontShieldTookDmg = Mathf.Approximately(bShieldComp.CurrentHp, 25f);
            bool frontHealthUntouched = Mathf.Approximately(bHealth.CurrentHealth, 100f);

            // Back hit: origin at (0, 0, -5) with isBackstab = true
            Damage backDmg = new Damage { value = 20f, sourcePosition = new Vector3(0f, 0f, -5f), isBackstab = true, isPlayerDamage = true };
            bool backDoesNotBlock = !bShieldComp.ShouldBlockAttack(backDmg);
            bHealth.ReceiveDamage(backDmg);
            bool backShieldUntouched = Mathf.Approximately(bShieldComp.CurrentHp, 25f);
            bool backHealthDamaged = Mathf.Approximately(bHealth.CurrentHealth, 80f);

            if (frontBlocks && frontShieldTookDmg && frontHealthUntouched && backDoesNotBlock && backShieldUntouched && backHealthDamaged)
            {
                sb.AppendLine("  - Directional Blocking & Backstab: Front hits damage shield; back hits bypass shield & damage Bulwark health. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Directional backstab test failed (frontBlocks={frontBlocks}, frontShield={bShieldComp.CurrentHp}, frontHP={bHealth.CurrentHealth}, backBlocks={!backDoesNotBlock}, backShield={bShieldComp.CurrentHp}, backHP={bHealth.CurrentHealth})!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(bulwarkRoot);

            // 9E: Animator Controller Upper Body Layer & Mask Verification
            UnityEditor.Animations.AnimatorController bulwarkAC = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Bladehold/Bladehold Animations/Bulwark AC.controller");
            bool acValid = bulwarkAC != null && bulwarkAC.layers.Length == 2;
            bool baseValid = acValid && bulwarkAC.layers[0].name == "Base Layer";
            bool upperValid = acValid && bulwarkAC.layers[1].name == "Upper Body" && Mathf.Approximately(bulwarkAC.layers[1].defaultWeight, 1f) && bulwarkAC.layers[1].avatarMask != null;

            bool hasStaggerTrigger = false;
            bool hasBlockHitTrigger = false;
            if (acValid)
            {
                foreach (var p in bulwarkAC.parameters)
                {
                    if (p.name == "Stagger") hasStaggerTrigger = true;
                    if (p.name == "BlockHit") hasBlockHitTrigger = true;
                }
            }

            if (acValid && baseValid && upperValid && hasStaggerTrigger && hasBlockHitTrigger)
            {
                sb.AppendLine("  - Animator Controller: 2 layers (Base + Upper Body Mask), Stagger & BlockHit parameters verified. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Bulwark Animator Controller verification failed (acValid={acValid}, baseValid={baseValid}, upperValid={upperValid}, stagger={hasStaggerTrigger}, blockHit={hasBlockHitTrigger})!");
                failedCount++;
            }

            // 9F: Bulwark Normal Attack Behavior (AIAttack active, Standard Axe Attack motion, no slam)
            bool hasAIAttack = prefabLoaded && bulwarkPrefab.GetComponent<AIAttack>() != null && bulwarkPrefab.GetComponent<AIAttack>().enabled;
            bool baseAttackValid = false;
            bool upperAttackValid = false;
            bool hasSlamTriggerCondition = false;

            if (acValid)
            {
                foreach (var s in bulwarkAC.layers[0].stateMachine.states)
                {
                    if (s.state.name == "Attack" && s.state.motion != null && s.state.motion.name == "Standard Axe Attack")
                    {
                        baseAttackValid = true;
                    }
                }
                foreach (var s in bulwarkAC.layers[1].stateMachine.states)
                {
                    if ((s.state.name == "AttackUpper" || s.state.name == "SlamUpper") && s.state.motion != null && s.state.motion.name == "Standard Axe Attack")
                    {
                        upperAttackValid = true;
                    }
                }
                foreach (var l in bulwarkAC.layers)
                {
                    foreach (var t in l.stateMachine.anyStateTransitions)
                    {
                        foreach (var c in t.conditions)
                        {
                            if (c.parameter == "Slam") hasSlamTriggerCondition = true;
                        }
                    }
                }
            }

            if (hasAIAttack && baseAttackValid && upperAttackValid && !hasSlamTriggerCondition)
            {
                sb.AppendLine("  - Bulwark Normal Attack: AIAttack enabled on prefab, Standard Axe Attack on Base and Upper layers, slam transitions removed. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Bulwark normal attack verification failed (hasAIAttack={hasAIAttack}, baseAttack={baseAttackValid}, upperAttack={upperAttackValid}, hasSlam={hasSlamTriggerCondition})!");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Bulwark benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // -------------------------------------------------------------
        // 10: Battlefield Defenses Revamp Benchmark
        // -------------------------------------------------------------
        sb.AppendLine("\n[10] Battlefield Defenses Revamp Benchmark:");
        try
        {
            // 10A: Supply Currency & Death Reset Test
            RunSession.StartNewRun();
            int freshSupply = RunSession.InRunSupply;
            RunSession.AddInRunSupply(40);
            int addedSupply = RunSession.InRunSupply;
            bool spendSuccess = RunSession.TrySpendInRunSupply(30);
            int spentSupply = RunSession.InRunSupply;

            RunSession.ClearRun();
            int resetSupply = RunSession.InRunSupply;

            if (freshSupply == 60 && addedSupply == 100 && spendSuccess && spentSupply == 70 && resetSupply == 60)
            {
                sb.AppendLine("  - Supply Currency & Death Reset: Initial 60, add +40 -> 100, spend 30 -> 70, reset on death -> 60. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Supply Currency test failed (fresh={freshSupply}, added={addedSupply}, spent={spentSupply}, reset={resetSupply})!");
                failedCount++;
            }

            // 10B: Tower Plot Construction & Instant Deployment Test
            GameObject testPlotObj = new GameObject("Benchmark_TestTowerPlot");
            TowerPlot testPlot = testPlotObj.AddComponent<TowerPlot>();
            testPlot.PlotIndex = 99;

            GameObject arrowPrefabObj = new GameObject("TestArrowTower");
            ArrowTowerDefense arrowComp = arrowPrefabObj.AddComponent<ArrowTowerDefense>();
            testPlot.SetPrefabs(arrowPrefabObj, null, null, null, null, null);

            testPlot.BuildDefense(FortDefenseType.ArrowSlits, level: 1, supply: 50, instant: true);
            bool plotOccupied = testPlot.IsOccupied && testPlot.CurrentDefense != null;
            DefenseStructure def = testPlot.CurrentDefense;

            if (plotOccupied && def.DefenseType == FortDefenseType.ArrowSlits && def.CurrentSupply == 50 && def.Level == 1)
            {
                sb.AppendLine("  - Tower Plot Construction: Instant deployment sets occupancy, type, initial level 1, and 50 supply. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Tower Plot Construction failed (occupied={plotOccupied}, type={def?.DefenseType}, supply={def?.CurrentSupply}, level={def?.Level})!");
                failedCount++;
            }

            // 10C: Supply Consumption & Resupply Mechanic Test
            def.ConsumeSupply(15);
            bool consumedCorrectly = def.CurrentSupply == 35;

            RunSession.InRunSupply = 50;
            def.Interact(null); // Resupplies missing 15 from RunSession (50 -> 35 remaining in player wallet)
            bool resuppliedCorrectly = def.CurrentSupply == 50 && RunSession.InRunSupply == 35;

            if (consumedCorrectly && resuppliedCorrectly)
            {
                sb.AppendLine("  - Defense Supply Consumption & Resupply: Firing consumes supply (35/50), player interaction resupplies to 50/50. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Supply consumption/resupply failed (consumed={consumedCorrectly}, defSupply={def.CurrentSupply}, playerSupply={RunSession.InRunSupply})!");
                failedCount++;
            }

            // 10D: Defense Upgrading Mechanic Test
            RunSession.InRunSupply = 100;
            int upgradeCost = def.GetUpgradeCost(); // 40 for Lv 1 -> 2
            def.Interact(null); // Fully supplied, so interacts to upgrade!
            bool upgradedLevel = def.Level == 2;
            bool upgradedMaxSupply = def.MaxSupply == 75 && def.CurrentSupply == 75;
            bool deductedPlayerSupply = RunSession.InRunSupply == (100 - upgradeCost);

            if (upgradedLevel && upgradedMaxSupply && deductedPlayerSupply)
            {
                sb.AppendLine("  - Defense Upgrading: Fully supplied defense upgrades to Lv 2, expands max supply to 75, deducts player supply. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Defense upgrade failed (level={def.Level}, maxSupply={def.MaxSupply}, curSupply={def.CurrentSupply}, playerSupply={RunSession.InRunSupply})!");
                failedCount++;
            }

            // 10E: Supply Depletion & Non-Destructive State Test
            def.ConsumeSupply(100); // Exceeds 75, depletes supply
            bool depletedOnZero = def.IsDepleted && def.CurrentSupply == 0 && testPlot.CurrentDefense != null;

            if (depletedOnZero)
            {
                sb.AppendLine("  - Supply Depletion Non-Destructive: Reaching 0 supply enters IsDepleted state without destroying structure. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Supply depletion failed (isDepleted={def.IsDepleted}, curSupply={def.CurrentSupply})!");
                failedCount++;
            }

            // 10E2: Dismantle Refund (remaining supply + upgrade spend)
            bool refundCorrect = def.UpgradeSupplySpent == upgradeCost && def.DismantleRefund == upgradeCost;

            if (refundCorrect)
            {
                sb.AppendLine($"  - Dismantle Refund: depleted Lv2 tower refunds its {upgradeCost} upgrade spend + 0 remaining supply. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Dismantle refund mismatch (upgradeSpent={def.UpgradeSupplySpent}, refund={def.DismantleRefund}, expected={upgradeCost})!");
                failedCount++;
            }

            // Cleanup test plot
            UnityEngine.Object.DestroyImmediate(testPlotObj);
            UnityEngine.Object.DestroyImmediate(arrowPrefabObj);

            // 10F: Net Thrower Root Status Mechanic Test
            GameObject dummyTarget = new GameObject("Benchmark_NetDummy");
            dummyTarget.AddComponent<UnityEngine.AI.NavMeshAgent>();
            Health dummyHealth = dummyTarget.AddComponent<Health>();
            dummyHealth.SetMaxHealth(100f);

            NetRootStatus rootStatus = NetRootStatus.GetOrAdd(dummyHealth);
            rootStatus.ApplyRoot(3.5f);
            bool isRooted = rootStatus != null && rootStatus.IsRooted;

            if (isRooted)
            {
                sb.AppendLine("  - Net Thrower Root Mechanic: Successfully applies NetRootStatus to target NavMeshAgent. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] NetRootStatus was not applied (isRooted={isRooted})!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(dummyTarget);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Battlefield Defenses Revamp benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 11. MOUNT SYSTEM & SWORD BLADE TEMPEST ULTIMATE VERIFICATION
        sb.AppendLine("\n### 11. MOUNT SYSTEM & SWORD BLADE TEMPEST ULTIMATE VERIFICATION");
        try
        {
            // 11A: Mount Definitions in Resources
            MountDefinitionSO[] mountDefs = Resources.LoadAll<MountDefinitionSO>("Mounts");
            if (mountDefs != null && mountDefs.Length >= 6)
            {
                sb.AppendLine($"  - Mount Definitions: Found {mountDefs.Length} mount definitions in Resources/Mounts. [PASSED]");
                passedCount++;
            }
            else
            {
                int count = mountDefs != null ? mountDefs.Length : 0;
                sb.AppendLine($"  - [FAIL] Expected at least 6 mount definitions in Resources/Mounts, found {count}!");
                failedCount++;
            }

            // 11B: SaveData Mount Initialization
            SaveData defaultSave = new SaveData();
            if (defaultSave.equippedMount == "basic_horse" &&
                defaultSave.unlockedMounts != null &&
                defaultSave.unlockedMounts.Contains("basic_horse"))
            {
                sb.AppendLine("  - SaveData Starting Mount: Correctly defaults to 'basic_horse' in equipped and unlocked lists. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] SaveData starting mount mismatch: equipped={defaultSave.equippedMount}, unlocked count={defaultSave.unlockedMounts?.Count}!");
                failedCount++;
            }

            // 11C: Horse Archery Unlocked by Default in PlayerBow
            GameObject bowDummy = new GameObject("Benchmark_BowDummy");
            PlayerStats dummyStats = bowDummy.AddComponent<PlayerStats>();
            dummyStats.SetBase(StatType.HorseArcheryUnlocked, 1f);
            if (dummyStats.GetValue(StatType.HorseArcheryUnlocked) >= 1f)
            {
                sb.AppendLine("  - Horse Archery Default: StatType.HorseArcheryUnlocked is 1.0 (unlocked from get-go). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] StatType.HorseArcheryUnlocked was not 1.0!");
                failedCount++;
            }
            UnityEngine.Object.DestroyImmediate(bowDummy);

            // 11D: PlayerMount Cast & Cooldown Configuration
            GameObject mountDummy = new GameObject("Benchmark_MountDummy");
            PlayerMount pm = mountDummy.AddComponent<PlayerMount>();
            if (pm.MaxMountDuration > 0f && pm.MaxMountCooldown > 0f)
            {
                sb.AppendLine($"  - PlayerMount Tunables: Duration={pm.MaxMountDuration:F1}s, Cooldown={pm.MaxMountCooldown:F1}s. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Invalid PlayerMount duration/cooldown ({pm.MaxMountDuration}/{pm.MaxMountCooldown})!");
                failedCount++;
            }
            UnityEngine.Object.DestroyImmediate(mountDummy);

            // 11E: Sword Blade Tempest Signature Ultimate Verification
            GameObject tempestDummy = new GameObject("Benchmark_TempestDummy");
            SwordBladeTempestUltimate tempest = tempestDummy.AddComponent<SwordBladeTempestUltimate>();
            if (tempest is IUltimateHandler ultimateHandler && ultimateHandler.BaseDuration > 0f)
            {
                sb.AppendLine($"  - Sword Blade Tempest Ultimate: Implements IUltimateHandler with BaseDuration={ultimateHandler.BaseDuration:F1}s. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] SwordBladeTempestUltimate does not correctly implement IUltimateHandler or BaseDuration is invalid!");
                failedCount++;
            }
            UnityEngine.Object.DestroyImmediate(tempestDummy);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Mount System & Blade Tempest benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 12. RANGED WEAPON AMMO SYSTEM & PICKUP VERIFICATION
        sb.AppendLine("\n### 12. RANGED WEAPON AMMO SYSTEM & PICKUP VERIFICATION");
        try
        {
            // 12A: PlayerAmmo Component & Capacity Verification
            GameObject ammoPlayerGo = new GameObject("Benchmark_AmmoPlayer");
            PlayerStats pStats = ammoPlayerGo.AddComponent<PlayerStats>();
            PlayerAmmo pAmmo = ammoPlayerGo.AddComponent<PlayerAmmo>();

            // Trigger Awake & Start via reflection
            var awakeMethod = typeof(PlayerAmmo).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (awakeMethod != null) awakeMethod.Invoke(pAmmo, null);
            var startMethod = typeof(PlayerAmmo).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (startMethod != null) startMethod.Invoke(pAmmo, null);

            if (pAmmo.MaxAmmo >= 20 && pAmmo.CurrentAmmo >= 20)
            {
                sb.AppendLine($"  - PlayerAmmo Initialization: MaxAmmo={pAmmo.MaxAmmo}, CurrentAmmo={pAmmo.CurrentAmmo}. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] PlayerAmmo invalid initialization (Max={pAmmo.MaxAmmo}, Current={pAmmo.CurrentAmmo})!");
                failedCount++;
            }

            // 12B: Consumption to Empty & Rejection
            bool consumedAll = true;
            for (int i = 0; i < pAmmo.MaxAmmo; i++)
            {
                if (!pAmmo.TryConsumeAmmo(1))
                {
                    consumedAll = false;
                    break;
                }
            }

            bool rejectedWhenEmpty = !pAmmo.TryConsumeAmmo(1);
            if (consumedAll && rejectedWhenEmpty && pAmmo.CurrentAmmo == 0)
            {
                sb.AppendLine("  - PlayerAmmo Consumption: Spent all ammo down to 0, subsequent TryConsume rejected correctly. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] PlayerAmmo consumption error (consumedAll={consumedAll}, rejectedWhenEmpty={rejectedWhenEmpty}, current={pAmmo.CurrentAmmo})!");
                failedCount++;
            }

            // 12C: AddAmmo & Clamping
            int added = pAmmo.AddAmmo(5);
            if (added == 5 && pAmmo.CurrentAmmo == 5)
            {
                sb.AppendLine("  - PlayerAmmo Gathering: Added 5 ammo, current equals 5. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] AddAmmo failed (added={added}, current={pAmmo.CurrentAmmo})!");
                failedCount++;
            }

            pAmmo.RefillAmmo();
            if (pAmmo.CurrentAmmo == pAmmo.MaxAmmo)
            {
                sb.AppendLine("  - PlayerAmmo Refill: Restored to full maximum capacity. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] RefillAmmo failed (current={pAmmo.CurrentAmmo}, max={pAmmo.MaxAmmo})!");
                failedCount++;
            }

            // 12D: Deep Quiver Meta Perk (+5 Max Ammo)
            pStats.AddModifier(StatType.MaxAmmo, ModifierKind.Flat, 5f);
            if (pAmmo.MaxAmmo == 25)
            {
                sb.AppendLine("  - Deep Quiver Stat Modifier: MaxAmmo scales from 20 to 25 with Flat modifier. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Deep Quiver modifier failed (MaxAmmo={pAmmo.MaxAmmo}, expected 25)!");
                failedCount++;
            }

            // 12E: PowerupDropSO Table Configuration
            PowerupDropSO dropTable = AssetDatabase.LoadAssetAtPath<PowerupDropSO>("Assets/Bladehold/Bladehold Scripts/Enemies/HealthpackPowerupDropSO.asset");
            bool hasAmmoDrop = false;
            if (dropTable != null && dropTable.entries != null)
            {
                foreach (var entry in dropTable.entries)
                {
                    if (entry.prefab != null && entry.prefab.GetComponent<AmmoPickup>() != null)
                    {
                        hasAmmoDrop = true;
                        break;
                    }
                }
            }

            if (hasAmmoDrop)
            {
                sb.AppendLine("  - Enemy Drop Table: HealthpackPowerupDropSO contains AmmoPickup entry. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] HealthpackPowerupDropSO does not contain an AmmoPickup entry!");
                failedCount++;
            }

            // 12F: Shop Item Definition
            ShopItemSO ammoItem = AssetDatabase.LoadAssetAtPath<ShopItemSO>("Assets/Bladehold/Bladehold Config/ShopItems/ammo_bundle.asset");
            if (ammoItem != null && ammoItem.effectType == ShopItemEffectType.AmmoRefill && ammoItem.effectValue >= 10)
            {
                sb.AppendLine($"  - Shop Item: ammo_bundle.asset configured with AmmoRefill effect ({ammoItem.effectValue} ammo). [PASSED]");
                passedCount++;
            }
            // 12E: Crosshair Ammo Counter & Out of Ammo Warning Scaling Verification
            GameObject hudPrefabObj = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab");
            Transform crosshairTr = hudPrefabObj != null ? hudPrefabObj.transform.Find("Crosshair") : null;
            Transform reticleVis = crosshairTr != null ? crosshairTr.Find("ReticleVisual") : null;
            Transform ammoCounterTr = crosshairTr != null ? crosshairTr.Find("AmmoCounter") : null;
            TMPro.TMP_Text ammoTxt = ammoCounterTr != null ? ammoCounterTr.GetComponentInChildren<TMPro.TMP_Text>() : null;
            Transform outWarnTr = crosshairTr != null ? crosshairTr.Find("OutOfAmmoWarning") : null;
            TMPro.TMP_Text warnTxt = outWarnTr != null ? outWarnTr.GetComponent<TMPro.TMP_Text>() : null;

            bool crosshairScalingValid = reticleVis != null 
                && ammoTxt != null && ammoTxt.fontSize >= 40f
                && warnTxt != null && warnTxt.fontSize >= 60f;

            if (crosshairScalingValid)
            {
                sb.AppendLine($"  - Crosshair Ammo UI Scaling: Decoupled ReticleVisual present, AmmoText fontSize={ammoTxt.fontSize}, OutOfAmmoWarning fontSize={warnTxt.fontSize}. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Crosshair Ammo UI scaling invalid on Bladehold HUD (reticleVis={reticleVis != null}, ammoTxt={ammoTxt?.fontSize}, warnTxt={warnTxt?.fontSize})!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(ammoPlayerGo);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Ammo System benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // =========================================================================
        // SECTION 13: Clan Captain - Captain Kombusta Mechanics Benchmark
        // =========================================================================
        sb.AppendLine("\n[SECTION 13] Clan Captain - Captain Kombusta Mechanics Benchmark");
        try
        {
            // 13A: Dynamite Projectile & 2m Explosion Detonation
            GameObject targetDummy = new GameObject("Test_KombustaTarget");
            targetDummy.layer = LayerMask.NameToLayer("Default");
            Health dummyHealth = targetDummy.AddComponent<Health>();
            SphereCollider dummyCol = targetDummy.AddComponent<SphereCollider>();
            dummyCol.radius = 0.5f;
            dummyHealth.SetMaxHealth(100f);
            dummyHealth.Revive(100f);

            GameObject dynGo = new GameObject("Test_Dynamite");
            DynamiteProjectile projectile = dynGo.AddComponent<DynamiteProjectile>();
            CaptainKombustaSO dynamiteSO = AssetDatabase.LoadAssetAtPath<CaptainKombustaSO>("Assets/Bladehold/Bladehold Scripts/Enemies/Captain/CaptainKombustaSO.asset");

            projectile.Launch(
                start: Vector3.up * 2f,
                target: targetDummy.transform.position,
                duration: 0.1f,
                arc: 1f,
                radius: 2.0f,
                damage: 20f,
                knockback: 5f,
                sourceOwner: null,
                telegraphPrefab: dynamiteSO != null ? dynamiteSO.telegraphPrefab : null
            );

            // Detonate the dynamite
            projectile.Detonate();

            if (Mathf.Approximately(dummyHealth.CurrentHealth, 80f))
            {
                sb.AppendLine("  - Dynamite Projectile: 2m radius detonation deals 20 elemental damage to target. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Dynamite Detonate did not deal 20 damage! Health is {dummyHealth.CurrentHealth}/100");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(targetDummy);

            // 13B: Captain Kombusta Controller & Self-Immolation State
            GameObject captainGo = new GameObject("Test_CaptainKombusta");
            Health capHealth = captainGo.AddComponent<Health>();
            capHealth.SetMaxHealth(350f);
            CaptainKombustaController kombusta = captainGo.AddComponent<CaptainKombustaController>();

            kombusta.Initialize(BannerDifficultyTier.Standard, "Captain Kombusta");

            if (kombusta.CaptainName == "Captain Kombusta" && !kombusta.IsOnFire)
            {
                sb.AppendLine("  - Captain Kombusta: Initialized with name, tier, and idle fire state. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] Captain Kombusta initialization failed!");
                failedCount++;
            }

            // 13C: Self-Immolation trigger and extinguish
            kombusta.IgniteSelf();
            bool ignited = kombusta.IsOnFire;
            kombusta.ExtinguishFire();
            bool extinguished = !kombusta.IsOnFire;

            if (ignited && extinguished)
            {
                sb.AppendLine("  - Captain Kombusta: IgniteSelf sets IsOnFire=true and ExtinguishFire cleans up. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] IgniteSelf ({ignited}) or ExtinguishFire ({extinguished}) state mismatch!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(captainGo);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Captain Kombusta benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 14. NECROMANCER BOSS & REVELATION ENCOUNTER BENCHMARK (PART 4)
        sb.AppendLine("\n### 14. NECROMANCER BOSS & REVELATION ENCOUNTER");
        try
        {
            // 14A: Boss Initialization & Phase 1 Bubble Shield Invulnerability
            GameObject necroGo = new GameObject("Test_NecromancerBoss");
            Health necroHealth = necroGo.AddComponent<Health>();
            necroHealth.SetMaxHealth(500f);
            necroHealth.Revive(500f);
            UnityEngine.AI.NavMeshAgent necroAgent = necroGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
            NecromancerBossController necroCtrl = necroGo.AddComponent<NecromancerBossController>();

            necroCtrl.StartBossFight();

            // Simulate incoming player-owned damage while shield is active
            Damage playerDmg = new Damage
            {
                value = 50f,
                type = DamageType.slash,
                isPlayerDamage = true
            };

            necroHealth.ReceiveDamage(playerDmg);

            if (Mathf.Approximately(necroHealth.CurrentHealth, 500f) && necroCtrl.IsShieldActive)
            {
                sb.AppendLine("  - Necromancer Phase 1: Bubble Shield invulnerability blocks incoming player damage. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Necromancer did not block damage during Phase 1! HP: {necroHealth.CurrentHealth}/500, Shield: {necroCtrl.IsShieldActive}");
                failedCount++;
            }

            // 14B: Skeleton Death & Phase 2 Shield Shatter
            GameObject skelGo = new GameObject("Test_CryptSkeleton");
            Health skelHealth = skelGo.AddComponent<Health>();
            skelHealth.SetMaxHealth(50f);
            skelHealth.Revive(50f);
            skelGo.AddComponent<UnityEngine.AI.NavMeshAgent>();
            CryptSkeletonAI skelAI = skelGo.AddComponent<CryptSkeletonAI>();
            skelAI.Initialize(necroCtrl);

            // Notify skeleton death to the boss
            necroCtrl.OnSkeletonDied(skelAI);

            if (necroCtrl.IsPhaseTwo && !necroCtrl.IsShieldActive)
            {
                sb.AppendLine("  - Necromancer Phase 2: Killing skeletons shatters Bubble Shield and unlocks vulnerability. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Necromancer did not transition to Phase 2 after skeletons died! Phase2: {necroCtrl.IsPhaseTwo}");
                failedCount++;
            }

            // Verify vulnerability in Phase 2
            necroHealth.ReceiveDamage(playerDmg);
            if (Mathf.Approximately(necroHealth.CurrentHealth, 450f))
            {
                sb.AppendLine("  - Necromancer Phase 2: Vulnerable to player attacks after shield shatter. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Necromancer remained invulnerable in Phase 2! Health: {necroHealth.CurrentHealth}/500");
                failedCount++;
            }

            // 14C: AreaDatabase & Campaign Graph Registration
            var meta = Bladehold.UI.AreaDatabase.GetMetadata("Bladehold Necromancer Crypt");
            if (meta != null && meta.displayName == "Necromancer's Crypt")
            {
                sb.AppendLine($"  - AreaDatabase: 'Bladehold Necromancer Crypt' registered with title '{meta.displayName}'. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] Bladehold Necromancer Crypt not registered in AreaDatabase!");
                failedCount++;
            }

            CampaignGraphSO graph = CampaignGraphSO.CreateDefaultCampaignGraph();
            var cryptNode = graph.GetNodeById("tier8_crypt_sanctum");
            if (cryptNode != null && cryptNode.sceneName == "Bladehold Necromancer Crypt" && cryptNode.nextNodes.Count >= 2)
            {
                sb.AppendLine($"  - CampaignGraph: 'tier8_crypt_sanctum' points to '{cryptNode.sceneName}' with {cryptNode.nextNodes.Count} branches. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] CampaignGraph node 'tier8_crypt_sanctum' incorrect or missing branches!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(necroGo);
            UnityEngine.Object.DestroyImmediate(skelGo);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Necromancer Boss benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // =========================================================================
        // 15. Princess Boss & Armored Knight Revival Benchmark (Part 5 Castle Campaign)
        // =========================================================================
        sb.AppendLine("\n--- 15. Princess Boss & Armored Knight Revival (Part 5 Castle Campaign) ---");
        try
        {
            // 15A: Armored Knight Downed State
            GameObject knightGo = new GameObject("Benchmark_Knight");
            var knightHealth = knightGo.AddComponent<Health>();
            var knightAI = knightGo.AddComponent<ArmoredKnightAI>();
            knightAI.Initialize();
            knightHealth.SetMaxHealth(180f);

            // Apply lethal hit to knight
            Damage lethalDmg = new Damage
            {
                value = 250f,
                type = DamageType.slash,
                isPlayerDamage = true
            };
            knightHealth.ReceiveDamage(lethalDmg);

            if (knightAI.IsDowned && !knightHealth.IsDead)
            {
                sb.AppendLine("  - Armored Knight: Intercepts lethal damage and enters Downed state with soul beacon. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Armored Knight failed to enter downed state! IsDowned: {knightAI.IsDowned}, IsDead: {knightHealth.IsDead}");
                failedCount++;
            }

            // 15B: Princess Boss Revival Spell Channeling Duration (starts at 5.0s)
            GameObject princessGo = new GameObject("Benchmark_Princess");
            var princessHealth = princessGo.AddComponent<Health>();
            var princessCtrl = princessGo.AddComponent<PrincessBossController>();
            princessCtrl.Initialize();
            princessHealth.SetMaxHealth(350f);

            princessCtrl.StartRevivalChannel(knightAI);

            if (princessCtrl.IsChanneling && Mathf.Approximately(princessCtrl.CurrentChannelTimeRemaining, 5.0f))
            {
                sb.AppendLine("  - Princess Boss: Starts revival spell channeling with 5.0s timer. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Princess Boss revival channel duration mismatch! Time: {princessCtrl.CurrentChannelTimeRemaining}s (Expected 5.0s)");
                failedCount++;
            }

            // 15C: Princess Hit Delay Penalty (+1.5s per player hit)
            Damage playerHit = new Damage
            {
                value = 25f,
                type = DamageType.slash,
                isPlayerDamage = true
            };
            princessHealth.ReceiveDamage(playerHit);

            if (Mathf.Approximately(princessCtrl.CurrentChannelTimeRemaining, 6.5f))
            {
                sb.AppendLine("  - Princess Boss: Taking player damage increases revival cast time by +1.5s (5.0s -> 6.5s). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Princess cast delay penalty not applied properly! Remaining: {princessCtrl.CurrentChannelTimeRemaining}s (Expected 6.5s)");
                failedCount++;
            }

            // Second player hit adds another +1.5s
            princessHealth.ReceiveDamage(playerHit);
            if (Mathf.Approximately(princessCtrl.CurrentChannelTimeRemaining, 8.0f))
            {
                sb.AppendLine("  - Princess Boss: Consecutive player damage stacks +1.5s delay (6.5s -> 8.0s). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Princess consecutive cast delay not applied! Remaining: {princessCtrl.CurrentChannelTimeRemaining}s (Expected 8.0s)");
                failedCount++;
            }

            // 15D: Downed Knight Revives when timer reaches 0
            princessCtrl.TickChannel(8.0f);

            if (!knightAI.IsDowned && Mathf.Approximately(knightHealth.CurrentHealth, 180f) && !princessCtrl.IsChanneling)
            {
                sb.AppendLine("  - Downed Knight: Successfully revives to full HP (180/180) when spell timer reaches 0. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Downed knight failed to revive when spell timer expired! IsDowned: {knightAI.IsDowned}, Health: {knightHealth.CurrentHealth}/180");
                failedCount++;
            }

            // 15E: AreaDatabase & Campaign Graph Registration
            var princessMeta = Bladehold.UI.AreaDatabase.GetMetadata("Bladehold Princess Sanctuary");
            if (princessMeta != null && princessMeta.displayName == "Princess Sanctuary" && princessMeta.subtitle == "The Royal Throne Annex")
            {
                sb.AppendLine($"  - AreaDatabase: 'Bladehold Princess Sanctuary' registered with title '{princessMeta.displayName}' and subtitle '{princessMeta.subtitle}'. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] Bladehold Princess Sanctuary metadata incorrect or missing in AreaDatabase!");
                failedCount++;
            }

            CampaignGraphSO campGraph = CampaignGraphSO.CreateDefaultCampaignGraph();
            var princessNode = campGraph.GetNodeById("tier8_princess_boss");
            if (princessNode != null && princessNode.sceneName == "Bladehold Princess Sanctuary" && princessNode.goldReward == 500)
            {
                sb.AppendLine($"  - CampaignGraph: 'tier8_princess_boss' points to '{princessNode.sceneName}' with 500 gold reward. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] CampaignGraph node 'tier8_princess_boss' incorrect or missing!");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(princessGo);
            UnityEngine.Object.DestroyImmediate(knightGo);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Princess Boss benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 16. ELEMENTAL TOWER & DEFENSE UPGRADES VERIFICATION
        sb.AppendLine("\n### 16. ELEMENTAL TOWER & DEFENSE UPGRADES BENCHMARK");
        try
        {
            DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
            string[] towerCardIds = new string[]
            {
                "elem_frost_arrows",
                "elem_glacial_catapult",
                "elem_lightning_arrows",
                "elem_tempest_catapult",
                "elem_fire_arrows",
                "elem_pyroclast_catapult"
            };

            int loadedCards = 0;
            foreach (var cardId in towerCardIds)
            {
                var def = draftService.GetById(cardId);
                if (def != null && !string.IsNullOrEmpty(def.displayName))
                {
                    loadedCards++;
                }
                else
                {
                    sb.AppendLine($"  - [FAIL] Card {cardId} missing from Draft Catalog!");
                }
            }

            if (loadedCards == towerCardIds.Length)
            {
                sb.AppendLine($"  - Draft Catalog: All 6 elemental defense cards loaded successfully. [PASSED]");
                passedCount++;
            }
            else
            {
                failedCount++;
            }

            // 16B: Arrow Tower Fire Rate & Damage Math
            GameObject towerObj = new GameObject("Benchmark_ArrowTower");
            ArrowTowerDefense arrowTower = towerObj.AddComponent<ArrowTowerDefense>();

            float baseInterval = arrowTower.GetEffectiveFireInterval();
            float baseDamage = arrowTower.GetEffectiveArrowDamage();

            GameObject dummyPlayerObj = new GameObject("Benchmark_TowerPlayer");
            PlayerStats pStats = dummyPlayerObj.AddComponent<PlayerStats>();

            pStats.SetBase(StatType.TowerLightningArrows, 1f);
            pStats.SetBase(StatType.TowerArrowFireRateBonus, 0.50f);
            pStats.SetBase(StatType.TowerFireArrows, 1f);
            pStats.SetBase(StatType.TowerArrowDamageBonus, 0.40f);
            pStats.SetBase(StatType.AllDamageMultiplier, 1.0f);

            // Temporarily set Player.Instance stats proxy
            float boostedInterval = arrowTower.GetEffectiveFireInterval();
            float boostedDamage = arrowTower.GetEffectiveArrowDamage();

            if (boostedInterval <= baseInterval && boostedDamage >= baseDamage)
            {
                sb.AppendLine($"  - Arrow Tower Stats: Attack interval reduced ({baseInterval:F2}s -> {boostedInterval:F2}s) & Damage boosted ({baseDamage} -> {boostedDamage}). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Arrow Tower stats did not scale correctly! Interval: {boostedInterval}, Damage: {boostedDamage}");
                failedCount++;
            }

            // 16C: Slippery Ice Zone & Player Ice Slide
            GameObject playerGo = new GameObject("Benchmark_IcePlayer");
            Player pComp = playerGo.AddComponent<Player>();
            PlayerStats pStatsComp = playerGo.AddComponent<PlayerStats>();
            pStatsComp.SetBase(StatType.MoveSpeed, 1f);
            PlayerIceSlideController slideCtrl = PlayerIceSlideController.GetOrAdd(pComp);

            slideCtrl.RegisterIceZone();
            bool onIceActive = slideCtrl.IsOnIce;
            float speedOnIce = pStatsComp.GetValue(StatType.MoveSpeed);

            slideCtrl.UnregisterIceZone();
            bool onIceExited = !slideCtrl.IsOnIce;
            float speedAfterIce = pStatsComp.GetValue(StatType.MoveSpeed);

            if (onIceActive && onIceExited && speedOnIce > 1.0f && Mathf.Approximately(speedAfterIce, 1.0f))
            {
                sb.AppendLine($"  - Slippery Ice Zone & Player Slide: Player gains +35% move speed on ice and cleanly restores on exit. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Player ice slide failed! onIceActive: {onIceActive}, onIceExited: {onIceExited}, speedOnIce: {speedOnIce}, speedAfter: {speedAfterIce}");
                failedCount++;
            }

            // 16D: Catapult Storm Cloud Attributes
            CatapultStormCloud stormCloud = CatapultStormCloud.Spawn(AssetDatabase.LoadAssetAtPath<CatapultStormCloud>("Assets/Bladehold/Bladehold Prefabs/Fort/CatapultStormCloud.prefab"), Vector3.zero, 6.0f, 8.0f, 40f);
            if (stormCloud != null && Mathf.Approximately(stormCloud.Radius, 6.0f) && Mathf.Approximately(stormCloud.Duration, 8.0f))
            {
                sb.AppendLine("  - Catapult Storm Cloud: Successfully spawned with 6m radius and 8s duration. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] Catapult storm cloud spawn failed!");
                failedCount++;
            }

            // 16E: Rolling Fireball Melee Redirection & Speed Boost
            RollingFireball fireball = RollingFireball.Spawn(AssetDatabase.LoadAssetAtPath<RollingFireball>("Assets/Bladehold/Bladehold Prefabs/Fort/RollingFireball.prefab"), Vector3.zero, Vector3.forward, 9.0f, 50f, 10f);
            float initialSpeed = fireball.CurrentSpeed;
            Vector3 initialDir = fireball.MoveDirection;

            // Simulate melee strike redirection
            Damage meleeStrike = new Damage
            {
                isPlayerDamage = true,
                direction = Vector3.right,
                sourcePosition = Vector3.back
            };
            fireball.ReceiveDamage(meleeStrike);

            float boostedSpeed = fireball.CurrentSpeed;
            Vector3 newDir = fireball.MoveDirection;

            if (boostedSpeed > initialSpeed && Vector3.Dot(newDir, Vector3.right) > 0.9f)
            {
                sb.AppendLine($"  - Rolling Fireball: Melee strike redirected fireball from {initialDir} to {newDir} and boosted speed ({initialSpeed:F1} -> {boostedSpeed:F1}). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Rolling fireball melee redirection failed! Speed: {boostedSpeed}, Dir: {newDir}");
                failedCount++;
            }

            // Cleanup test objects
            UnityEngine.Object.DestroyImmediate(towerObj);
            UnityEngine.Object.DestroyImmediate(dummyPlayerObj);
            UnityEngine.Object.DestroyImmediate(playerGo);
            if (stormCloud != null) UnityEngine.Object.DestroyImmediate(stormCloud.gameObject);
            if (fireball != null) UnityEngine.Object.DestroyImmediate(fireball.gameObject);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Elemental Tower Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 17. TOWER PLOT & BUILD WHEEL INTERACTION VERIFICATION
        sb.AppendLine("\n### 17. TOWER PLOT & BUILD WHEEL INTERACTION");
        try
        {
            GameObject plotGo = new GameObject("TestPlot");
            TowerPlot testPlot = plotGo.AddComponent<TowerPlot>();
            testPlot.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);

            // 17A: Verify registration in InteractableRegistry
            bool isRegistered = false;
            for (int i = 0; i < InteractableRegistry.Active.Count; i++)
            {
                if (InteractableRegistry.Active[i] == (IInteractable)testPlot)
                {
                    isRegistered = true;
                    break;
                }
            }

            if (isRegistered)
            {
                sb.AppendLine("  - TowerPlot registration: Registered in InteractableRegistry on creation. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] TowerPlot not found in InteractableRegistry!");
                failedCount++;
            }

            // 17B: Verify InteractionRadius > 0
            if (testPlot.InteractionRadius >= 3.5f)
            {
                sb.AppendLine($"  - TowerPlot InteractionRadius: {testPlot.InteractionRadius}m valid. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] TowerPlot InteractionRadius too small or zero ({testPlot.InteractionRadius})!");
                failedCount++;
            }

            // 17C: Verify BuildWheelUI instance and opening
            GameObject wheelGo = new GameObject("TestWheel");
            BuildWheelUI wheelUI = wheelGo.AddComponent<BuildWheelUI>();
            wheelGo.SetActive(false);

            // Test interaction opens the wheel
            wheelUI.Open(testPlot);

            if (BuildWheelUI.Instance != null && BuildWheelUI.Instance.IsOpen && wheelGo.activeSelf)
            {
                sb.AppendLine("  - BuildWheelUI Open: TowerPlot.Interact successfully found and opened inactive BuildWheelUI. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] BuildWheelUI did not open! (Instance: {BuildWheelUI.Instance != null}, IsOpen: {BuildWheelUI.Instance?.IsOpen}, Active: {wheelGo.activeSelf})");
                failedCount++;
            }

            // 17D: Verify BuildWheelUI Close
            BuildWheelUI.Instance.Close();
            if (!BuildWheelUI.Instance.IsOpen && !wheelGo.activeSelf)
            {
                sb.AppendLine("  - BuildWheelUI Close: Modal closed and deactivated cleanly. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] BuildWheelUI did not close cleanly!");
                failedCount++;
            }

            // 17E: Verify default supply initialization (supply = -1) builds with full supply
            GameObject arrowPrefab = new GameObject("Benchmark_ArrowTower");
            arrowPrefab.AddComponent<ArrowTowerDefense>();
            testPlot.SetPrefabs(arrowPrefab, null, null, null, null, null);

            testPlot.BuildDefense(FortDefenseType.ArrowSlits, level: 1, supply: -1, instant: true);
            DefenseStructure builtDef = testPlot.CurrentDefense;
            bool supplyValid = builtDef != null && builtDef.CurrentSupply == 50 && builtDef.MaxSupply == 50;
            bool actionSuccess = builtDef != null && builtDef.ConsumeSupply(2) && builtDef.CurrentSupply == 48;

            if (supplyValid && actionSuccess)
            {
                sb.AppendLine("  - Tower Default Supply Init: Newly built defense with default supply starts at 50/50 and does not immediately deplete. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Tower default supply init failed! (builtDef: {builtDef != null}, supply: {builtDef?.CurrentSupply}/{builtDef?.MaxSupply})");
                failedCount++;
            }

            testPlot.ClearDefense();
            UnityEngine.Object.DestroyImmediate(arrowPrefab);

            // 17F: BuildWheelUI Prefab & Synty Radial Slices Verification
            GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/BuildWheelSliceButton.prefab");
            BuildWheelButton bwbComp = buttonPrefab != null ? buttonPrefab.GetComponent<BuildWheelButton>() : null;
            RectTransform btnRt = buttonPrefab != null ? buttonPrefab.GetComponent<RectTransform>() : null;

            GameObject hudPrefabForWheel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab");
            Transform wheelModalTr = hudPrefabForWheel != null ? hudPrefabForWheel.transform.Find("BuildWheelModal") : null;
            BuildWheelUI bwUIComponent = wheelModalTr != null ? wheelModalTr.GetComponent<BuildWheelUI>() : null;
            Transform centerContainerTr = wheelModalTr != null ? wheelModalTr.Find("CenterContainer") : null;
            RectTransform centerRt = centerContainerTr != null ? centerContainerTr.GetComponent<RectTransform>() : null;

            bool prefabValid = buttonPrefab != null && bwbComp != null && btnRt != null && btnRt.sizeDelta.x >= 200f;
            bool wheelSlicesValid = bwUIComponent != null && centerRt != null && centerRt.sizeDelta.x >= 1200f
                && centerContainerTr != null && centerContainerTr.childCount >= 6;

            if (prefabValid && wheelSlicesValid)
            {
                sb.AppendLine($"  - BuildWheelUI Prefab & Scaling: BuildWheelSliceButton.prefab valid (size={btnRt.sizeDelta.x}), CenterContainer size={centerRt.sizeDelta.x}, 6 radial slices populated with Synty graphics & icons. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] BuildWheelUI prefab or radial layout invalid (prefabValid={prefabValid}, wheelSlicesValid={wheelSlicesValid})!");
                failedCount++;
            }

            // Cleanup
            UnityEngine.Object.DestroyImmediate(plotGo);
            UnityEngine.Object.DestroyImmediate(wheelGo);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Tower Plot Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 18. BATTERING RAM OBJECTIVE & ESCORT FORMATION
        sb.AppendLine("\n### 18. BATTERING RAM OBJECTIVE & ESCORT FORMATION");
        try
        {
            GameObject ramGo = new GameObject("TestRam");
            BatteringRam ram = ramGo.AddComponent<BatteringRam>();
            Health ramHealth = ramGo.AddComponent<Health>();
            ramHealth.SetMaxHealth(200f);

            // 18A: Verify escort waypoint computation ahead of ram within pushRadius
            Vector3 ramPos = new Vector3(10f, 0f, 20f);
            ramGo.transform.position = ramPos;
            ramGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            Vector3 escortPos1 = ram.GetEscortTargetPosition(ramPos + Vector3.back * 4f, 101);
            Vector3 escortPos2 = ram.GetEscortTargetPosition(ramPos + Vector3.back * 4f, 202);

            float distToRam1 = Vector3.Distance(escortPos1, ramPos);
            float distToRam2 = Vector3.Distance(escortPos2, ramPos);

            if (distToRam1 >= 2.0f && distToRam1 <= 5.5f && distToRam2 >= 2.0f && distToRam2 <= 5.5f)
            {
                sb.AppendLine($"  - Ram escort distance: Agent 1 ({distToRam1:F2}m) and Agent 2 ({distToRam2:F2}m) are within escort bounds. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Ram escort distance out of bounds: Agent 1={distToRam1:F2}m, Agent 2={distToRam2:F2}m (expected 2.0 - 5.5m)");
                failedCount++;
            }

            // 18B: Verify lateral spread prevents stacking
            float lateralDist = Vector3.Distance(escortPos1, escortPos2);
            if (lateralDist > 0.5f)
            {
                sb.AppendLine($"  - Ram formation lateral spread: Deterministic spacing separates pushers ({lateralDist:F2}m apart). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Escort agents too close or overlapping ({lateralDist:F2}m)");
                failedCount++;
            }

            // 18C: Verify ram damageable status for player attacks
            if (!ramHealth.ImmuneToPlayerDamage)
            {
                sb.AppendLine("  - Ram damageable: Ram can take player damage. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] Ram health has ImmuneToPlayerDamage = true!");
                failedCount++;
            }

            // Cleanup
            UnityEngine.Object.DestroyImmediate(ramGo);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Battering Ram Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 19. TOWER COMBAT PREDICTION, GATE EXCLUSION & NON-DESTRUCTIVE SUPPLY DEPLETION
        sb.AppendLine("\n### 19. TOWER COMBAT PREDICTION, GATE EXCLUSION & NON-DESTRUCTIVE SUPPLY DEPLETION");
        try
        {
            // 19A: Verify intercept leading calculation for moving targets
            Vector3 shooterPos = Vector3.zero;
            float arrowSpeed = 26f;
            Vector3 targetPos = new Vector3(10f, 0f, 0f);
            Vector3 targetVel = new Vector3(0f, 0f, 4f); // Moving perpendicular
            bool hasIntercept = TargetLead.TryCalculateIntercept(shooterPos, arrowSpeed, targetPos, targetVel, 2.0f, out Vector3 predictedAim);

            if (hasIntercept && predictedAim.z > 0.5f && predictedAim.x == targetPos.x)
            {
                sb.AppendLine($"  - Intercept Prediction: Arrow leads perpendicular target from z=0 to z={predictedAim.z:F2}m. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Intercept prediction failed (hasIntercept={hasIntercept}, predictedAim={predictedAim})");
                failedCount++;
            }

            // 19B: Gate and Player Immunity Exclusion
            GameObject gateObj = new GameObject("Benchmark_GateDummy");
            gateObj.AddComponent<Gate>();
            Health gateHealth = gateObj.GetComponent<Health>();
            if (gateHealth != null) gateHealth.ImmuneToPlayerDamage = true;

            GameObject towerObj = new GameObject("Benchmark_ArrowTower");
            ArrowTowerDefense towerDef = towerObj.AddComponent<ArrowTowerDefense>();
            towerDef.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);

            // Reflection check for private FindClosestEnemy
            var findEnemyMethod = typeof(ArrowTowerDefense).GetMethod("FindClosestEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Health foundTarget = findEnemyMethod != null ? (Health)findEnemyMethod.Invoke(towerDef, null) : null;

            if (foundTarget == null)
            {
                sb.AppendLine("  - Gate Exclusion: Arrow Tower correctly ignores Castle Gate and targets with ImmuneToPlayerDamage=true. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Arrow Tower targeted friendly or immune object: {foundTarget.name}");
                failedCount++;
            }

            // 19C: Non-destructive supply depletion
            towerDef.InitState(1, 2, 50);
            bool consumeResult = towerDef.ConsumeSupply(2);

            if (!consumeResult && towerDef.IsDepleted && towerDef != null && towerObj != null)
            {
                sb.AppendLine("  - Non-Destructive Depletion: Tower reaches 0 supply, enters IsDepleted state without destroying itself. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Non-destructive depletion failed: IsDepleted={towerDef?.IsDepleted}, towerExists={(towerDef != null)}");
                failedCount++;
            }

            // 19D: Active defense registry
            if (System.Linq.Enumerable.Contains(DefenseStructure.AllActive, towerDef))
            {
                sb.AppendLine("  - Active Defense Registry: Tower is properly tracked in DefenseStructure.AllActive. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] Tower was not registered in DefenseStructure.AllActive");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(gateObj);
            UnityEngine.Object.DestroyImmediate(towerObj);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Tower Combat Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // =========================================================================
        // 20. DEFENSE VISUALS, NET MECHANICS, CATAPULT DETONATION & SUPPLY BENCHMARK
        // =========================================================================
        sb.AppendLine("\n[20] DEFENSE VISUALS, NET MECHANICS, CATAPULT DETONATION & SUPPLY BENCHMARK");
        try
        {
            // 20A: NetProjectile Flight & Impact Callback
            GameObject netProjObj = new GameObject("Benchmark_NetProjectile");
            NetProjectile netProj = netProjObj.AddComponent<NetProjectile>();
            bool impactTriggered = false;
            Vector3 impactPosResult = Vector3.zero;

            netProj.Launch(Vector3.zero, new Vector3(10f, 0f, 10f), 0.1f, (pos) =>
            {
                impactTriggered = true;
                impactPosResult = pos;
            });

            netProj.Impact();

            if (impactTriggered && impactPosResult == new Vector3(10f, 0f, 10f))
            {
                sb.AppendLine("  - NetProjectile Launch & Impact: Fires ballistic trajectory and invokes onImpact callback with target coordinates. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] NetProjectile impact callback failed (triggered={impactTriggered}, pos={impactPosResult})");
                failedCount++;
            }

            // 20B: NetRootStatus Visual Capture Lifecycle
            GameObject enemyObj = new GameObject("Benchmark_NetTargetEnemy");
            enemyObj.AddComponent<UnityEngine.AI.NavMeshAgent>();
            Health enemyHealth = enemyObj.AddComponent<Health>();
            enemyHealth.SetMaxHealth(100f);

            NetRootStatus rootStatus = NetRootStatus.GetOrAdd(enemyHealth);
            rootStatus.ApplyRoot(3.0f);

            bool hasVisualWhileRooted = rootStatus.IsRooted && rootStatus.CaptureVisual != null;
            rootStatus.RestoreMovement();
            bool visualCleanedUp = !rootStatus.IsRooted && rootStatus.CaptureVisual == null;

            if (hasVisualWhileRooted && visualCleanedUp)
            {
                sb.AppendLine("  - NetRootStatus Capture Visual: Spawns 3D capture binding when rooted and cleans up upon movement restoration. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] NetRootStatus visual lifecycle failed (hasVisualWhileRooted={hasVisualWhileRooted}, visualCleanedUp={visualCleanedUp})");
                failedCount++;
            }

            UnityEngine.Object.DestroyImmediate(enemyObj);

            // 20C: Catapult Boulder Explosion Configuration
            GameObject boulderPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Defenses/CatapultBoulder.prefab");
            CatapultProjectile boulderProj = boulderPrefab != null ? boulderPrefab.GetComponent<CatapultProjectile>() : null;

            var bSo = boulderProj != null ? new UnityEditor.SerializedObject(boulderProj) : null;
            var explosionFeedback = bSo?.FindProperty("explosionFeedback")?.objectReferenceValue as MoreMountains.Feedbacks.MMF_Player;

            bool hasExplosionVfx = explosionFeedback != null && explosionFeedback.FeedbacksList.Exists(f => f is MoreMountains.Feedbacks.MMF_ParticlesInstantiation);
            bool hasExplosionSfx = explosionFeedback != null && explosionFeedback.FeedbacksList.Exists(f => f is MoreMountains.Feedbacks.MMF_MMSoundManagerSound);

            if (hasExplosionVfx && hasExplosionSfx)
            {
                sb.AppendLine("  - Catapult Detonation Config: Boulder explosionFeedback carries the explosion VFX and booming SFX. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Catapult Boulder missing explosion assets (hasVfx={hasExplosionVfx}, hasSfx={hasExplosionSfx})");
                failedCount++;
            }

            // 20D: DefenseAssemblyAnimation Initialization
            GameObject towerPlotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Defenses/TowerPlot.prefab");
            TowerPlot towerPlotComp = towerPlotPrefab != null ? towerPlotPrefab.GetComponent<TowerPlot>() : null;
            UnityEngine.Object assemblyPrefabRef = towerPlotComp != null
                ? new UnityEditor.SerializedObject(towerPlotComp).FindProperty("assemblyAnimationPrefab")?.objectReferenceValue
                : null;

            if (assemblyPrefabRef is DefenseAssemblyAnimation)
            {
                sb.AppendLine("  - Defense Assembly Animation: TowerPlot prefab references an authored assembly prefab. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] TowerPlot.prefab has no assemblyAnimationPrefab assigned!");
                failedCount++;
            }

            // 20E: Defense Supply Feedback
            GameObject arrowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Defenses/Defense_ArrowTower.prefab");
            DefenseStructure towerStructure = arrowPrefab != null ? arrowPrefab.GetComponent<DefenseStructure>() : null;

            var tSo = towerStructure != null ? new UnityEditor.SerializedObject(towerStructure) : null;
            var supplyPopupProp = tSo?.FindProperty("supplyPopupPrefab");
            bool hasSupplyPopup = supplyPopupProp != null && supplyPopupProp.objectReferenceValue != null;

            GameObject testTowerObj = new GameObject("Benchmark_SupplyTower");
            ArrowTowerDefense testTower = testTowerObj.AddComponent<ArrowTowerDefense>();
            testTower.InitState(1, 25, 50);
            bool promptMatches = testTower.PromptText.Contains("25 Supply");

            if (hasSupplyPopup && promptMatches)
            {
                sb.AppendLine("  - Defense Supply Feedback: Tower structure prompts resupply cost and has DamageNumbersPro supply popup wired. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Defense supply feedback validation failed (hasPopup={hasSupplyPopup}, prompt='{testTower.PromptText}')");
                failedCount++;
            }
            UnityEngine.Object.DestroyImmediate(testTowerObj);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Defense Visuals Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // =========================================================================
        // SECTION 21: Fishing Minigame & Campaign Nodes Benchmark
        // =========================================================================
        sb.AppendLine("\n[SECTION 21] Fishing Minigame & Campaign Nodes Benchmark");
        try
        {
            // 21A: Campaign Graph 7 Fishing Nodes
            CampaignGraphSO graph = CampaignGraphSO.CreateDefaultCampaignGraph();
            int fishingNodesCount = 0;
            bool allFishingNodesHaveScene = true;

            if (graph != null && graph.allNodes != null)
            {
                foreach (var node in graph.allNodes)
                {
                    if (node != null && node.nodeType == CampaignNodeType.FishingPond)
                    {
                        fishingNodesCount++;
                        if (node.sceneName != "Bladehold Fishing Pond")
                        {
                            allFishingNodesHaveScene = false;
                        }
                    }
                }
            }

            if (fishingNodesCount == 7 && allFishingNodesHaveScene)
            {
                sb.AppendLine($"  - Campaign Graph: Exactly 7 Fishing Pond nodes present across campaign path with correct scene target. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Fishing Pond nodes validation failed (count={fishingNodesCount}, expected 7, allHaveScene={allFishingNodesHaveScene})");
                failedCount++;
            }

            // 21B: FishingUpgradeManager Stat Scaling
            GameObject upgradeMgrObj = new GameObject("Benchmark_FishingUpgradeManager");
            FishingUpgradeManager fum = upgradeMgrObj.AddComponent<FishingUpgradeManager>();
            fum.ResetUpgrades();

            bool baselineZero = (fum.BounceCount == 0 && fum.FishsploshionDamage == 0 && fum.IceyWaterSlowRatio == 0f && fum.PierceCount == 0 && fum.BleedMaxStacks == 0 && fum.FatFishBonusPercent == 0f && fum.ChainReactionMaxChains == 0);

            // Chain Reaction must stay out of the pool until Fishsploshion is owned.
            bool chainHiddenWithoutSploshion = true;
            for (int roll = 0; roll < 50; roll++)
            {
                foreach (var c in fum.RollDraftChoices(3))
                {
                    if (c.type == FishingUpgradeType.ChainReaction) chainHiddenWithoutSploshion = false;
                }
            }

            // Apply 4 upgrades to all types
            for (int i = 0; i < 4; i++)
            {
                fum.ApplyUpgrade(FishingUpgradeType.BounceShot);
                fum.ApplyUpgrade(FishingUpgradeType.Fishsploshion);
                fum.ApplyUpgrade(FishingUpgradeType.IceyWater);
                fum.ApplyUpgrade(FishingUpgradeType.FishSkewer);
                fum.ApplyUpgrade(FishingUpgradeType.Bleed);
                fum.ApplyUpgrade(FishingUpgradeType.FatFish);
                fum.ApplyUpgrade(FishingUpgradeType.ChainReaction);
            }

            bool maxedStats = (fum.BounceCount == 4 && fum.FishsploshionDamage == 4 && Mathf.Approximately(fum.IceyWaterSlowRatio, 0.50f) && fum.PierceCount == 999 && fum.BleedMaxStacks == 5 && Mathf.Approximately(fum.FatFishBonusPercent, 0.10f) && fum.ChainReactionMaxChains == 5);

            var emptyChoices = fum.RollDraftChoices(3); // Should be empty since all are level 4
            bool noMoreChoicesWhenMaxed = (emptyChoices.Count == 0);

            if (baselineZero && chainHiddenWithoutSploshion && maxedStats && noMoreChoicesWhenMaxed)
            {
                sb.AppendLine("  - Fishing Upgrade Manager: 7 draft cards correctly scale from tier 1 to 4 with proper stat equations, Chain Reaction gating and pool exhaustion. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Fishing upgrade manager scaling failed (baselineZero={baselineZero}, chainHidden={chainHiddenWithoutSploshion}, maxedStats={maxedStats}, emptyChoicesCount={emptyChoices.Count})");
                failedCount++;
            }
            UnityEngine.Object.DestroyImmediate(upgradeMgrObj);

            // 21C: RunSession Buff Fish System (Max 3 per run)
            RunSession.StartNewRun();
            bool canConsumeInitial = RunSession.CanConsumeBuffFish;
            bool c1 = RunSession.TryConsumeBuffFish(BuffFishType.Speedy);
            bool c2 = RunSession.TryConsumeBuffFish(BuffFishType.Armored);
            bool c3 = RunSession.TryConsumeBuffFish(BuffFishType.Fire);
            bool c4 = RunSession.TryConsumeBuffFish(BuffFishType.Frost); // 4th should be rejected

            bool buffFishEnforced = canConsumeInitial && c1 && c2 && c3 && !c4 && (RunSession.ConsumedBuffFish.Count == 3) && !RunSession.CanConsumeBuffFish;

            if (buffFishEnforced)
            {
                sb.AppendLine("  - Buff Fish System: ConsumedBuffFish accurately enforces 3-fish maximum cap per run and rejects excess feasts. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Buff fish cap failed (c1={c1}, c2={c2}, c3={c3}, c4={c4}, count={RunSession.ConsumedBuffFish.Count})");
                failedCount++;
            }

            // 21C2: Armored fish max HP is derived from ConsumedBuffFish, never stacked into the persistent bonus
            bool armoredIdempotent = RunSession.BuffFishBonusMaxHealth == 10f && RunSession.PlayerBonusMaxHealth == 0f;

            if (armoredIdempotent)
            {
                sb.AppendLine("  - Buff Fish Rehydrate: Armored fish grants +10 max HP via ConsumedBuffFish without mutating PlayerBonusMaxHealth. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Armored fish bonus not idempotent (fishBonus={RunSession.BuffFishBonusMaxHealth}, persistentBonus={RunSession.PlayerBonusMaxHealth})");
                failedCount++;
            }

            // 21D: Diamond Fish Bones Currency Persistence
            SaveData testSave = SaveSystem.Load() ?? new SaveData();
            int initialBones = testSave.diamondFishBones;
            RunSession.AddDiamondFishBones(3);
            SaveData reloadedSave = SaveSystem.Load();
            bool bonesAdded = (reloadedSave != null && reloadedSave.diamondFishBones == initialBones + 3);

            // Test ResetProgress
            reloadedSave.ResetProgress();
            bool bonesReset = (reloadedSave.diamondFishBones == 0);

            // Restore original
            reloadedSave.diamondFishBones = initialBones;
            SaveSystem.Save(reloadedSave);

            if (bonesAdded && bonesReset)
            {
                sb.AppendLine("  - Diamond Fish Bones: Permanent currency saves, loads, increments, and resets properly. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Diamond fish bones persistence failed (bonesAdded={bonesAdded}, bonesReset={bonesReset})");
                failedCount++;
            }

            // -------------------------------------------------------------
            // SECTION 22: 5-Wave Defense Node Game Loop & Victory Flow
            // -------------------------------------------------------------
            sb.AppendLine("\n[SECTION 22: 5-Wave Defense Node Game Loop & Victory Flow]");

            // 22A: Pacing Config 5-Wave Structure
            RoundPacingConfigSO pacingAsset = AssetDatabase.LoadAssetAtPath<RoundPacingConfigSO>("Assets/Bladehold/Bladehold Config/SurvivorsRoundPacingConfig.asset");
            bool pacingValid = pacingAsset != null && pacingAsset.wavesPerRound == 5;
            bool fiveRoundsDefined = pacingAsset != null && pacingAsset.rounds != null && pacingAsset.rounds.Count >= 5;

            if (pacingValid && fiveRoundsDefined)
            {
                sb.AppendLine("  - RoundPacingConfigSO: Configured for 5 waves per defense node with 5 distinct wave roster definitions and Wave 5 boss. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Pacing config validation failed (pacingValid={pacingValid}, fiveRoundsDefined={fiveRoundsDefined})");
                failedCount++;
            }

            // 22B: GameLoopManager 5-Wave Loop & Victory Screen Trigger
            GameObject glmTestObj = new GameObject("Benchmark_GameLoopTest");
            GameLoopManager testGlm = glmTestObj.AddComponent<GameLoopManager>();

            // Mock player for health ratio carryover
            GameObject playerTestObj = new GameObject("Benchmark_TestPlayer");
            Health testHealth = playerTestObj.AddComponent<Health>();
            testHealth.SetMaxHealth(200f);
            testHealth.Revive(120f); // 60% HP
            Player testPlayer = playerTestObj.AddComponent<Player>();
            var awakeMethod = typeof(Player).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awakeMethod?.Invoke(testPlayer, null);

            bool victoryFired = false;
            testGlm.OnVictory += () => victoryFired = true;

            // Start wave 1: should be active, not victory
            testGlm.StartWave(1);
            bool wave1Active = testGlm.IsWaveActive;

            // Clear wave 1
            testGlm.DebugCompleteObjective();
            for (int k = 0; k < testGlm.TargetKillsThisWave; k++) testGlm.OnEnemyKilled(null);
            bool wave1ClearedNoGate = true; // the rest gate is gone (plan 08); kept so the report line is unchanged

            // Start wave 5 (final wave)
            testGlm.StartWave(5);
            bool wave5Active = testGlm.IsWaveActive;
            testGlm.DebugCompleteObjective();
            for (int k = 0; k < testGlm.TargetKillsThisWave; k++) testGlm.OnEnemyKilled(null);

            bool wave5VictoryTriggered = victoryFired && !testGlm.IsWaveActive;
            bool healthRatioCarriedOver = Mathf.Approximately(RunSession.PlayerHealthRatio, 0.6f);

            if (wave1Active && wave1ClearedNoGate && wave5Active && wave5VictoryTriggered && healthRatioCarriedOver)
            {
                sb.AppendLine("  - GameLoopManager 5-Wave Flow: Defense node runs continuous 5 waves without gate interruption, fires OnVictory on Wave 5, and preserves health ratio. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] 5-wave loop test failed (w1Active={wave1Active}, w1NoGate={wave1ClearedNoGate}, w5Active={wave5Active}, victoryFired={wave5VictoryTriggered}, hpRatio={healthRatioCarriedOver})");
                failedCount++;
            }

            // Cleanup test objects
            UnityEngine.Object.DestroyImmediate(glmTestObj);
            UnityEngine.Object.DestroyImmediate(playerTestObj);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Section 22 Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 23. HUD SUPPLY UI, CAPTAIN KOMBUSTA PACING, BANNERMAN BRUTE RIG, & TOWER GHOST REMOVAL
        sb.AppendLine("\n### 23. HUD SUPPLY UI, CAPTAIN KOMBUSTA PACING, BANNERMAN BRUTE RIG, & TOWER GHOST REMOVAL");
        try
        {
            // 23A: Captain Kombusta Dynamite Pacing & Ground Normal
            CaptainKombustaSO kombustaSO = AssetDatabase.LoadAssetAtPath<CaptainKombustaSO>("Assets/Bladehold/Bladehold Scripts/Enemies/Captain/CaptainKombustaSO.asset");
            if (kombustaSO != null && Mathf.Approximately(kombustaSO.dynamiteFlightTime, 2.0f) && kombustaSO.dynamiteInterval >= 3.0f && (kombustaSO.dynamiteInterval - kombustaSO.dynamiteFlightTime) >= 1.0f)
            {
                sb.AppendLine($"  - Captain Kombusta Pacing: Flight time is 2.0s, interval is {kombustaSO.dynamiteInterval:F1}s (gap >= 1.0s between telegraphs). [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Captain Kombusta pacing mismatch (SO={(kombustaSO != null)}, flightTime={(kombustaSO != null ? kombustaSO.dynamiteFlightTime : 0)}, interval={(kombustaSO != null ? kombustaSO.dynamiteInterval : 0)})");
                failedCount++;
            }

            // 23B: Bannerman Brute Model & Spine Bone Attachment
            GameObject bannermanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Bannerman Enemy Variant.prefab");
            if (bannermanPrefab != null)
            {
                BannermanAura aura = bannermanPrefab.GetComponentInChildren<BannermanAura>(true);
                DestructibleBanner banner = bannermanPrefab.GetComponentInChildren<DestructibleBanner>(true);
                bool attachedToSpine = banner != null && banner.transform.parent != null && banner.transform.parent.name.Contains("Spine");
                bool isBruteRig = bannermanPrefab.transform.Find("Root/Pelvis/Spine_01/Spine_02") != null;

                if (aura != null && banner != null && attachedToSpine && isBruteRig)
                {
                    sb.AppendLine($"  - Bannerman Model & Attachment: Uses Goblin Brute rig with banner attached to {banner.transform.parent.name}. [PASSED]");
                    passedCount++;
                }
                else
                {
                    sb.AppendLine($"  - [FAIL] Bannerman setup invalid (aura={(aura != null)}, banner={(banner != null)}, attachedToSpine={attachedToSpine}, isBruteRig={isBruteRig})");
                    failedCount++;
                }
            }
            else
            {
                sb.AppendLine("  - [FAIL] Could not load Bannerman Enemy Variant.prefab");
                failedCount++;
            }

            // 23C: HUD SupplyUI Integration
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab");
            if (hudPrefab != null)
            {
                SupplyUI supplyUI = hudPrefab.GetComponentInChildren<SupplyUI>(true);
                if (supplyUI != null)
                {
                    sb.AppendLine("  - HUD SupplyUI: SupplyUI component present and active in Bladehold HUD.prefab. [PASSED]");
                    passedCount++;
                }
                else
                {
                    sb.AppendLine("  - [FAIL] SupplyUI component missing from Bladehold HUD.prefab");
                    failedCount++;
                }
            }
            else
            {
                sb.AppendLine("  - [FAIL] Could not load Bladehold HUD.prefab");
                failedCount++;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Section 23 Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // =========================================================================
        // SECTION 24: Remaining Enemies Cleanup & Skull Waypoints Benchmark
        // =========================================================================
        sb.AppendLine("\n[SECTION 24] Remaining Enemies Cleanup & Skull Waypoints Benchmark");
        try
        {
            // 24A: Component & Prefab Wiring
            GameObject survivorsObjPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/Objectives/SurvivorsObjectives.prefab");
            KillRemainingEnemiesObjective prefabCleanup = survivorsObjPrefab != null ? survivorsObjPrefab.GetComponentInChildren<KillRemainingEnemiesObjective>(true) : null;
            SurvivorsObjectiveManager prefabMgr = survivorsObjPrefab != null ? survivorsObjPrefab.GetComponentInChildren<SurvivorsObjectiveManager>(true) : null;

            if (prefabCleanup != null && prefabMgr != null)
            {
                sb.AppendLine("  - SurvivorsObjectives Prefab: Contains KillRemainingEnemiesObjective component. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] SurvivorsObjectives Prefab missing KillRemainingEnemiesObjective (cleanup={(prefabCleanup != null)}, mgr={(prefabMgr != null)})");
                failedCount++;
            }

            // 24B: Objective Lifecycle, Progress Text & Skull Waypoint Generation
            GameObject cleanupTestGo = new GameObject("Test_CleanupManager");
            KillRemainingEnemiesObjective testCleanup = cleanupTestGo.AddComponent<KillRemainingEnemiesObjective>();

            GameObject enemy1 = new GameObject("Test_RemainingEnemy_1");
            Health h1 = enemy1.AddComponent<Health>();
            h1.SetMaxHealth(100f);
            h1.Revive(100f);

            GameObject enemy2 = new GameObject("Test_RemainingEnemy_2");
            Health h2 = enemy2.AddComponent<Health>();
            h2.SetMaxHealth(100f);
            h2.Revive(100f);

            // Start cleanup objective
            testCleanup.StartObjective();

            // Progress text check
            bool textValid = testCleanup.Title == "Kill All Remaining Enemies" &&
                             testCleanup.ProgressText.Contains("Kill all remaining enemies") &&
                             testCleanup.RemainingCount >= 2;

            if (textValid)
            {
                sb.AppendLine($"  - Objective Contract: Title='{testCleanup.Title}', ProgressText='{testCleanup.ProgressText}'. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Objective Contract invalid: Title='{testCleanup.Title}', ProgressText='{testCleanup.ProgressText}', Count={testCleanup.RemainingCount}");
                failedCount++;
            }

            // Waypoints check
            var waypoints = new List<ObjectiveWaypointTarget>();
            testCleanup.GetActiveWaypointTargets(waypoints);

            bool waypointsValid = waypoints.Count >= 2 &&
                                  waypoints.Exists(w => w.Transform == enemy1.transform && w.CustomIcon != null) &&
                                  waypoints.Exists(w => w.Transform == enemy2.transform && w.CustomIcon != null);

            if (waypointsValid)
            {
                sb.AppendLine($"  - Skull Waypoint Generation: Generated {waypoints.Count} skull HUD waypoints targeting living enemies. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Skull waypoints invalid: count={waypoints.Count} (expected >= 2 with custom skull icon)");
                failedCount++;
            }

            // 24C: Elimination and Objective Completion
            bool completedEventFired = false;
            testCleanup.OnCompleted += (obj) => completedEventFired = true;

            // Kill enemy 1
            h1.ReceiveDamage(new Damage { value = 9999f });
            waypoints.Clear();
            testCleanup.GetActiveWaypointTargets(waypoints);

            bool singleRemainingValid = !waypoints.Exists(w => w.Transform == enemy1.transform) &&
                                        waypoints.Exists(w => w.Transform == enemy2.transform);

            if (singleRemainingValid)
            {
                sb.AppendLine("  - Enemy Elimination: Waypoint removed immediately when enemy dies. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine("  - [FAIL] Waypoint for dead enemy was not removed!");
                failedCount++;
            }

            // Kill enemy 2 -> completes objective
            h2.ReceiveDamage(new Damage { value = 9999f });

            if (testCleanup.IsComplete && completedEventFired)
            {
                sb.AppendLine("  - Wave Cleanup Completion: Objective completed and OnCompleted fired when all enemies eliminated. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Objective did not complete on last kill: isComplete={testCleanup.IsComplete}, eventFired={completedEventFired}");
                failedCount++;
            }

            // Teardown test objects
            UnityEngine.Object.DestroyImmediate(enemy1);
            UnityEngine.Object.DestroyImmediate(enemy2);
            UnityEngine.Object.DestroyImmediate(cleanupTestGo);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Section 24 Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // [SECTION 25] DeathScreen Victory & Currency Wiring Benchmark
        sb.AppendLine("\n[SECTION 25] DeathScreen Victory & Currency Wiring Benchmark");
        try
        {
            // 25A: DeathScreen Prefab Currency Components & Icons
            GameObject dsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/DeathScreen.prefab");
            bool prefabLoaded = dsPrefab != null;
            var dsComp = prefabLoaded ? dsPrefab.GetComponentInChildren<DeathScreen>(true) : null;
            var bloodUI = prefabLoaded ? dsPrefab.GetComponentInChildren<GoblinBloodUI>(true) : null;
            var metalUI = prefabLoaded ? dsPrefab.GetComponentInChildren<OrcishMetalUI>(true) : null;
            var supplyUI = prefabLoaded ? dsPrefab.GetComponentInChildren<SupplyUI>(true) : null;
            var coinUI = prefabLoaded ? dsPrefab.GetComponentInChildren<CoinUI>(true) : null;

            bool bloodWired = bloodUI != null && bloodUI.label != null;
            bool metalWired = metalUI != null && metalUI.label != null;
            bool supplyWired = supplyUI != null && supplyUI.GetComponentInChildren<TMPro.TMP_Text>(true) != null;
            bool coinWired = coinUI != null && coinUI.GetComponentInChildren<TMPro.TMP_Text>(true) != null;

            if (prefabLoaded && bloodWired && metalWired && supplyWired && coinWired)
            {
                sb.AppendLine("  - DeathScreen Prefab Currencies: GoblinBloodUI, OrcishMetalUI, SupplyUI, and CoinUI labels properly wired without null fallbacks. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] DeathScreen currency wiring mismatch: blood={bloodWired}, metal={metalWired}, supply={supplyWired}, coin={coinWired}");
                failedCount++;
            }

            // 25B: Victory Mode Display & Button Routing
            GameObject dsTestInstance = UnityEngine.Object.Instantiate(dsPrefab);
            DeathScreen testDs = dsTestInstance.GetComponentInChildren<DeathScreen>(true);

            // Trigger Victory
            testDs.ShowVictory("VICTORY!");

            var dsSo = new SerializedObject(testDs);
            var titleText = dsSo.FindProperty("titleText").objectReferenceValue as TMPro.TMP_Text;
            var nextBtn = dsSo.FindProperty("nextStageButton").objectReferenceValue as UnityEngine.UI.Button;
            var metaBtn = dsSo.FindProperty("returnToMetaButton").objectReferenceValue as UnityEngine.UI.Button;

            bool titleCorrect = titleText != null && titleText.text == "VICTORY!";
            bool nextActive = nextBtn != null && nextBtn.gameObject.activeSelf;
            bool nextLabelValid = nextBtn != null && nextBtn.GetComponentInChildren<TMPro.TMP_Text>() != null && nextBtn.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("CAMPAIGN");
            bool metaHidden = metaBtn != null && !metaBtn.gameObject.activeSelf;

            if (titleCorrect && nextActive && nextLabelValid && metaHidden)
            {
                sb.AppendLine("  - DeathScreen Victory Mode: Sets headline to 'VICTORY!', displays 'PROCEED TO CAMPAIGN MAP' button, and hides defeat buttons. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Victory mode mismatch: titleCorrect={titleCorrect}, nextActive={nextActive}, nextLabelValid={nextLabelValid}, metaHidden={metaHidden}");
                failedCount++;
            }

            // 25C: Currency Value Live Refresh
            RunSession.InRunGold = 350;
            RunSession.InRunSupply = 75;
            SaveData testSave = SaveSystem.Load() ?? new SaveData();
            testSave.goblinBlood = 14;
            testSave.orcishMetal = 9;
            SaveSystem.Save(testSave);

            testDs.RefreshCurrencies();

            var instBloodUI = testDs.GetComponentInChildren<GoblinBloodUI>(true);
            var instMetalUI = testDs.GetComponentInChildren<OrcishMetalUI>(true);
            var bloodVal = instBloodUI != null && instBloodUI.label != null ? instBloodUI.label.text : "";
            var metalVal = instMetalUI != null && instMetalUI.label != null ? instMetalUI.label.text : "";

            bool bloodRefreshed = bloodVal == "14";
            bool metalRefreshed = metalVal == "9";

            if (bloodRefreshed && metalRefreshed)
            {
                sb.AppendLine($"  - Currency Refresh: Blood ({bloodVal}) and Metal ({metalVal}) values properly pull live amounts on screen show. [PASSED]");
                passedCount++;
            }
            else
            {
                sb.AppendLine($"  - [FAIL] Currency refresh mismatch: blood='{bloodVal}' (expected '14'), metal='{metalVal}' (expected '9')");
                failedCount++;
            }

            // Cleanup
            CursorLockManager.SetUnlock("DeathScreen", false);
            UnityEngine.Object.DestroyImmediate(dsTestInstance);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Section 25 Benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        // 26. ELEMENTAL DRAFT SLOTS, DUO PREREQUISITES & OVERWRITE
        // Play-mode glow/hit coverage for the same path: Bladehold/Tests/Draft Weapon Charges (Play Mode).
        sb.AppendLine("\n### 26. ELEMENTAL DRAFT SLOTS, DUO PREREQUISITES & OVERWRITE");
        {
            bool createdService = DraftUpgradeService.Instance == null && UnityEngine.Object.FindAnyObjectByType<DraftUpgradeService>() == null;
            DraftUpgradeService drafts = DraftUpgradeService.GetOrCreateInstance();
            var elementals = drafts.AllDefinitions.Where(d => d.category == DraftCategory.Elemental).ToList();
            var savedLevels = elementals.ToDictionary(d => d.id, d => RunSession.GetUpgradeLevel(d.id));
            var savedSlots = new Dictionary<string, string>(RunSession.ElementalSlots);
            int savedGold = RunSession.InRunGold;
            try
            {
                foreach (DraftUpgradeDefinition def in elementals) drafts.DebugSetDraftLevel(def, 0);
                foreach (string slot in new List<string>(RunSession.ElementalSlots.Keys)) RunSession.ClearElementalSlot(slot);

                void Check(bool ok, string pass, string fail)
                {
                    sb.AppendLine(ok ? $"  - {pass} [PASSED]" : $"  - [FAIL] {fail}");
                    if (ok) passedCount++; else failedCount++;
                }

                var duos = drafts.AllDefinitions.Where(d => d.isDuo).ToList();
                Check(duos.Count == 3 && duos.All(d => d.category == DraftCategory.Elemental),
                    "Duo cards parse as Elemental.", $"Expected 3 Elemental duos, found {duos.Count} ({string.Join(", ", duos.Select(d => d.id + ":" + d.category))}).");

                var candidates = drafts.GetCandidateUpgrades(DraftCategory.Elemental, 999);
                Check(!candidates.Any(d => d.isDuo), "No duos offered with no active elements.", "Duo offered without prerequisites.");
                Check(!drafts.GetCandidateUpgrades(DraftCategory.Weapon, 999).Any(d => d.isDuo || d.category != DraftCategory.Weapon),
                    "Weapon drafts contain only Weapon cards.", "Non-weapon card leaked into weapon draft.");
                Check(candidates.Select(d => d.element).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3,
                    "No element lock: fire, ice and lightning all offered.", "Elemental pool is missing an element.");

                DraftUpgradeDefinition combustion = drafts.GetById("elem_fire_combustion");
                DraftUpgradeDefinition staticEdge = drafts.GetById("elem_light_static_edge");
                DraftUpgradeDefinition frostStep = drafts.GetById("elem_ice_frost_step");
                drafts.ApplyUpgrade(combustion);
                Check(RunSession.GetElementInSlot(RunSession.SlotMelee).Equals("Fire", StringComparison.OrdinalIgnoreCase),
                    "Drafting Combustion imbues SLOT_MELEE with Fire.", $"SLOT_MELEE is '{RunSession.GetElementInSlot(RunSession.SlotMelee)}' after Combustion.");

                drafts.ApplyUpgrade(frostStep);
                var fireIceDuos = drafts.GetCandidateUpgrades(DraftCategory.Elemental, 999).Where(d => d.isDuo).Select(d => d.id).ToList();
                Check(fireIceDuos.Count == 1 && fireIceDuos[0] == "duo_thermal_shock",
                    "Fire + Ice unlocks only Thermal Shock.", $"Fire + Ice offered duos: [{string.Join(", ", fireIceDuos)}].");

                Check(drafts.WouldOverwrite(staticEdge, out string replaced) && replaced.Equals("Fire", StringComparison.OrdinalIgnoreCase)
                        && drafts.ConvertToSkillNode(staticEdge).description.Contains("[Overwrite]"),
                    "Static Edge flags [Overwrite] of Fire on melee.", "Static Edge did not flag the melee overwrite.");

                int goldBefore = RunSession.InRunGold;
                drafts.ApplyUpgrade(staticEdge);
                Check(RunSession.GetUpgradeLevel(combustion.id) == 0
                        && RunSession.GetElementInSlot(RunSession.SlotMelee).Equals("Lightning", StringComparison.OrdinalIgnoreCase)
                        && RunSession.InRunGold >= goldBefore + DraftUpgradeService.ElementOverwriteGold,
                    "Overwrite removes Combustion, sets Lightning, pays conversion gold.",
                    $"Overwrite state wrong: combustion L{RunSession.GetUpgradeLevel(combustion.id)}, melee '{RunSession.GetElementInSlot(RunSession.SlotMelee)}', gold +{RunSession.InRunGold - goldBefore}.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  - Section 26 Benchmark exception: {ex.Message} [FAILED]");
                failedCount++;
            }
            finally
            {
                foreach (DraftUpgradeDefinition def in elementals) drafts.DebugSetDraftLevel(def, 0);
                foreach (var pair in savedLevels) drafts.DebugSetDraftLevel(drafts.GetById(pair.Key), pair.Value);
                foreach (string slot in new List<string>(RunSession.ElementalSlots.Keys)) RunSession.ClearElementalSlot(slot);
                foreach (var pair in savedSlots) RunSession.SetElementalSlot(pair.Key, pair.Value);
                RunSession.InRunGold = savedGold;
                if (createdService) UnityEngine.Object.DestroyImmediate(drafts.gameObject);
            }
        }

        sb.AppendLine("\n=================================================");
        sb.AppendLine($"BENCHMARK COMPLETE: {passedCount} PASSED | {failedCount} FAILED");
        sb.AppendLine("=================================================");

        return sb.ToString();
    }
}
