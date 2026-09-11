using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
///     Sets up the Troll's dedicated controller and first-party clips, and the Elemental Golem's
///     Brute Warrior animation overrides.
/// </summary>
public static class EnemyAnimationSetup
{
    private const string BaseControllerPath = "Assets/Third Party/Synty/AnimationGoblinLocomotion/Animations/Sidekick/Enemy AC (Goblin).controller";
    private const string TrollPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Troll Enemy Variant.prefab";
    private const string TrollControllerPath = "Assets/Bladehold/Bladehold Prefabs/Troll AC.controller";
    private const string TrollClipFolder = "Assets/Bladehold/Bladehold Animations/Troll";

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

        SetupTroll();
        SetupElementalGolem(baseController);
        UpdateTrollScaleInCsv();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== EnemyAnimationSetup: Setup complete! ===");
    }

    [MenuItem("Bladehold/Repair Troll Animations")]
    public static void RepairTrollAnimations()
    {
        SetupTroll();
        AssetDatabase.SaveAssets();
    }

    private static void SetupTroll()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(TrollControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException($"Troll controller not found: {TrollControllerPath}");
        }

        var stateMachine = controller.layers[0].stateMachine;
        AnimatorState locomotion = null;
        AnimatorState attack = null;
        foreach (var child in stateMachine.states)
        {
            if (child.state.name == "Locomotion") locomotion = child.state;
            if (child.state.name == "Attack") attack = child.state;
        }
        var blend = locomotion != null ? locomotion.motion as BlendTree : null;
        if (blend == null || blend.children.Length != 2 || attack == null)
        {
            throw new InvalidOperationException("Troll controller requires its idle/walk blend and Attack state.");
        }

        var idle = CreateTrollClip("GiantGolem_Idle", "Troll Idle", true, false);
        var walk = CreateTrollClip("GiantGolem_Move_Walk_Forward", "Troll Walk", true, true);
        var slam = CreateTrollClip("GiantGolem_Attack_Swing_SmashDown01", "Troll Slam", false, false);
        var children = blend.children;
        children[0].motion = idle;
        children[1].motion = walk;
        blend.children = children;
        attack.motion = slam;
        attack.tag = "Slam";

        bool hasSlam = false;
        foreach (var parameter in controller.parameters)
        {
            if (parameter.name != "Slam") continue;
            if (parameter.type != AnimatorControllerParameterType.Trigger)
            {
                throw new InvalidOperationException("Troll Slam parameter must be a trigger.");
            }
            hasSlam = true;
        }
        if (!hasSlam) controller.AddParameter("Slam", AnimatorControllerParameterType.Trigger);
        AnimatorStateTransition slamTransition = null;
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            foreach (var condition in transition.conditions)
            {
                if (condition.parameter == "Slam") slamTransition = transition;
            }
        }
        if (slamTransition == null)
        {
            slamTransition = stateMachine.AddAnyStateTransition(attack);
            slamTransition.AddCondition(AnimatorConditionMode.If, 0f, "Slam");
        }
        slamTransition.destinationState = attack;
        slamTransition.hasExitTime = false;
        slamTransition.hasFixedDuration = true;
        slamTransition.duration = 0.1f;
        slamTransition.canTransitionToSelf = false;

        GameObject root = PrefabUtility.LoadPrefabContents(TrollPrefabPath);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            if (animator == null) throw new InvalidOperationException("Troll Animator is missing.");
            if (animator.runtimeAnimatorController != controller || animator.applyRootMotion)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(root, TrollPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        EditorUtility.SetDirty(blend);
        EditorUtility.SetDirty(attack);
        EditorUtility.SetDirty(slamTransition);
        EditorUtility.SetDirty(controller);
    }

    private static AnimationClip CreateTrollClip(string sourceName, string name, bool loop, bool extractTravel)
    {
        string sourcePath = $"Assets/Giant_Golem/Art/Animations/{sourceName}.fbx";
        AnimationClip source = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sourcePath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                source = clip;
                break;
            }
        }
        if (source == null) throw new InvalidOperationException($"Missing troll animation: {sourcePath}");

        if (!AssetDatabase.IsValidFolder(TrollClipFolder))
        {
            AssetDatabase.CreateFolder("Assets/Bladehold/Bladehold Animations", "Troll");
        }
        string path = $"{TrollClipFolder}/{name}.anim";
        var result = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (result == null)
        {
            result = new AnimationClip();
            AssetDatabase.CreateAsset(result, path);
        }
        EditorUtility.CopySerialized(source, result);
        result.name = name;
        var settings = AnimationUtility.GetAnimationClipSettings(result);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        // Keep every clip on the authored root origin, not a moving body-mass centre.
        settings.keepOriginalPositionXZ = true;
        settings.loopBlendPositionXZ = !extractTravel;
        AnimationUtility.SetAnimationClipSettings(result, settings);
        var events = AnimationUtility.GetAnimationEvents(result);
        foreach (var animationEvent in events)
        {
            if (animationEvent.functionName == "LeftFootStomp") animationEvent.functionName = "FootL";
            if (animationEvent.functionName == "RightFootStomp") animationEvent.functionName = "FootR";
        }
        AnimationUtility.SetAnimationEvents(result, events);
        EditorUtility.SetDirty(result);
        return result;
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
