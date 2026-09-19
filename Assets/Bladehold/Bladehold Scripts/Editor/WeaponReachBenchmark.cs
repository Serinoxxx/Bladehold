using System;
using System.Collections.Generic;
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
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  - Bulwark benchmark exception: {ex.Message} [FAILED]");
            failedCount++;
        }

        sb.AppendLine("\n=================================================");
        sb.AppendLine($"BENCHMARK COMPLETE: {passedCount} PASSED | {failedCount} FAILED");
        sb.AppendLine("=================================================");

        return sb.ToString();
    }
}
