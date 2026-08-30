using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Editor utility to set up AnimatorOverrideControllers for Troll (using Giant Golem animations)
///     and Elemental Golem (using Brute Warrior grab & throw animations), plus updating Troll scale.
/// </summary>
public static class EnemyAnimationSetup
{
    private const string BaseControllerPath = "Assets/Third Party/Synty/AnimationGoblinLocomotion/Animations/Sidekick/Enemy AC (Goblin).controller";
    private const string TrollPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Troll Enemy Variant.prefab";
    private const string TrollOverridePath = "Assets/Bladehold/Bladehold Prefabs/Troll Override.overrideController";

    private const string ElementalGolemPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Elemental Golem Enemy Variant.prefab";
    private const string ElementalGolemOverridePath = "Assets/Bladehold/Bladehold Prefabs/Elemental Golem Override.overrideController";

    private const string EnemiesCsvPath = "Assets/Bladehold/Config/Enemies.csv";

    [MenuItem("Bladehold/Apply Golem And Troll Animations")]
    public static void ApplyAll()
    {
        Debug.Log("=== EnemyAnimationSetup: Starting setup for Troll and Elemental Golem ===");

        RuntimeAnimatorController baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);
        if (baseController == null)
        {
            Debug.LogError($"[EnemyAnimationSetup] Base controller not found at: {BaseControllerPath}");
            return;
        }

        SetupTroll(baseController);
        SetupElementalGolem(baseController);
        UpdateTrollScaleInCsv();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== EnemyAnimationSetup: Setup complete! ===");
    }

    private static void SetupTroll(RuntimeAnimatorController baseController)
    {
        // 1. Create or load Troll Override controller
        AnimatorOverrideController trollAoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(TrollOverridePath);
        if (trollAoc == null)
        {
            trollAoc = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(trollAoc, TrollOverridePath);
            Debug.Log($"Created Troll AnimatorOverrideController at {TrollOverridePath}");
        }
        else
        {
            trollAoc.runtimeAnimatorController = baseController;
        }

        // 2. Load Giant Golem clips from FBXs
        AnimationClip idleClip = LoadFirstClip("Assets/Giant_Golem/Art/Animations/GiantGolem_Idle.fbx");
        AnimationClip walkClip = LoadFirstClip("Assets/Giant_Golem/Art/Animations/GiantGolem_Move_Walk_Forward.fbx");
        AnimationClip slamClip = LoadFirstClip("Assets/Giant_Golem/Art/Animations/GiantGolem_Attack_Swing_SmashDown01.fbx") ?? LoadFirstClip("Assets/Giant_Golem/Art/Animations/GiantGolem_Attack_PowerStomp01.fbx");
        AnimationClip attackClip = slamClip ?? LoadFirstClip("Assets/Giant_Golem/Art/Animations/GiantGolem_Interact_PickUp_ThrowToGround.fbx");
        AnimationClip deathClip = LoadFirstClip("Assets/Giant_Golem/Art/Animations/GiantGolem_Idle_Death01.fbx");
        AnimationClip roarClip = LoadFirstClip("Assets/Giant_Golem/Art/Animations/GiantGolem_Idle_Roar01.fbx");

        // 3. Override clips on Troll AOC
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        trollAoc.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            string origName = overrides[i].Key != null ? overrides[i].Key.name.ToLower() : "";
            if (origName.Contains("slam"))
            {
                if (slamClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, slamClip);
            }
            else if (origName.Contains("attack"))
            {
                if (attackClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, attackClip);
            }
            else if (origName.Contains("death"))
            {
                if (deathClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, deathClip);
            }
            else if (origName.Contains("cheer") || origName.Contains("taunt"))
            {
                if (roarClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, roarClip);
            }
            else if (origName.Contains("idle"))
            {
                if (idleClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, idleClip);
            }
            else if (origName.Contains("walk") || origName.Contains("run") || origName.Contains("locomotion") || origName.Contains("move"))
            {
                if (walkClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, walkClip);
            }
        }

        trollAoc.ApplyOverrides(overrides);
        EditorUtility.SetDirty(trollAoc);

        // 4. Assign controller to Troll prefab
        GameObject trollPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrollPrefabPath);
        if (trollPrefab != null)
        {
            Animator animator = trollPrefab.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = trollAoc;
                EditorUtility.SetDirty(trollPrefab);
                Debug.Log($"Wired Troll Override AOC to {TrollPrefabPath}");
            }
            else
            {
                Debug.LogError($"[EnemyAnimationSetup] Animator component not found on {TrollPrefabPath}");
            }
        }
        else
        {
            Debug.LogError($"[EnemyAnimationSetup] Troll prefab not found at {TrollPrefabPath}");
        }
    }

    private static void SetupElementalGolem(RuntimeAnimatorController baseController)
    {
        // 1. Create or load Elemental Golem Override controller
        AnimatorOverrideController golemAoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ElementalGolemOverridePath);
        if (golemAoc == null)
        {
            golemAoc = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(golemAoc, ElementalGolemOverridePath);
            Debug.Log($"Created Elemental Golem AnimatorOverrideController at {ElementalGolemOverridePath}");
        }
        else
        {
            golemAoc.runtimeAnimatorController = baseController;
        }

        // 2. Load Brute Warrior clips from FBXs
        AnimationClip rangeAttackClip = LoadFirstClip("Assets/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Animations/Brute@RangeAttack1.FBX"); // Grab from ground & throw!
        AnimationClip idleClip = LoadFirstClip("Assets/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Animations/Brute@Idle.FBX");
        AnimationClip walkClip = LoadFirstClip("Assets/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Animations/Brute@Walk.FBX");
        AnimationClip deathClip = LoadFirstClip("Assets/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Animations/Brute@Death.FBX");
        AnimationClip specialAttackClip = LoadFirstClip("Assets/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Animations/Brute@SpecialAttack1.FBX");

        // 3. Override clips on Elemental Golem AOC
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        golemAoc.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            string origName = overrides[i].Key != null ? overrides[i].Key.name.ToLower() : "";
            if (origName.Contains("attack"))
            {
                if (rangeAttackClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, rangeAttackClip);
            }
            else if (origName.Contains("death"))
            {
                if (deathClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, deathClip);
            }
            else if (origName.Contains("cheer") || origName.Contains("taunt"))
            {
                if (specialAttackClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, specialAttackClip);
            }
            else if (origName.Contains("idle"))
            {
                if (idleClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, idleClip);
            }
            else if (origName.Contains("walk") || origName.Contains("run") || origName.Contains("locomotion") || origName.Contains("move"))
            {
                if (walkClip != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, walkClip);
            }
        }

        golemAoc.ApplyOverrides(overrides);
        EditorUtility.SetDirty(golemAoc);

        // 4. Assign controller to Elemental Golem prefab
        GameObject golemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElementalGolemPrefabPath);
        if (golemPrefab != null)
        {
            Animator animator = golemPrefab.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = golemAoc;
                EditorUtility.SetDirty(golemPrefab);
                Debug.Log($"Wired Elemental Golem Override AOC to {ElementalGolemPrefabPath}");
            }
            else
            {
                Debug.LogError($"[EnemyAnimationSetup] Animator component not found on {ElementalGolemPrefabPath}");
            }
        }
        else
        {
            Debug.LogError($"[EnemyAnimationSetup] Elemental Golem prefab not found at {ElementalGolemPrefabPath}");
        }
    }

    private static void UpdateTrollScaleInCsv()
    {
        if (!File.Exists(EnemiesCsvPath))
        {
            Debug.LogError($"[EnemyAnimationSetup] Enemies.csv not found at {EnemiesCsvPath}");
            return;
        }

        string[] lines = File.ReadAllLines(EnemiesCsvPath);
        bool updated = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.StartsWith("troll"))
            {
                string[] parts = line.Split(',');
                // CSV header: id,displayName,health,damage,minGold,maxGold,speed,scale,unlockWave,spawnChance,minSpawn,maxConcurrent,knockbackResistance,enabled
                // Index 7 is scale. Previous value was 1.6, 3x is 4.8.
                if (parts.Length > 7)
                {
                    parts[7] = "4.8";
                    lines[i] = string.Join(",", parts);
                    updated = true;
                    Debug.Log("[EnemyAnimationSetup] Updated Troll scale in Enemies.csv to 4.8 (3x original 1.6)");
                }
            }
        }

        if (updated)
        {
            File.WriteAllLines(EnemiesCsvPath, lines);
        }
    }

    private static AnimationClip LoadFirstClip(string path)
    {
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi != null && !mi.importAnimation)
        {
            mi.importAnimation = true;
            mi.SaveAndReimport();
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets != null && assets.Length > 0)
        {
            foreach (UnityEngine.Object obj in assets)
            {
                if (obj is AnimationClip clip)
                {
                    if (!clip.name.StartsWith("__preview__"))
                    {
                        Debug.Log($"[EnemyAnimationSetup] Loaded clip '{clip.name}' from {path}");
                        return clip;
                    }
                }
            }
        }

        // Direct load fallback
        AnimationClip directClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (directClip != null)
        {
            Debug.Log($"[EnemyAnimationSetup] Loaded direct clip '{directClip.name}' from {path}");
            return directClip;
        }

        Debug.LogWarning($"[EnemyAnimationSetup] AnimationClip not found in asset at: {path} (total sub-assets: {(assets != null ? assets.Length : 0)})");
        return null;
    }
}
