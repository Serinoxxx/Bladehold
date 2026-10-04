using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
///     Builds the Spearman's animation assets (Bladehold > Spearman > Build Pose Animations): two
///     static, arms-only humanoid clips — spears held high and spears levelled forward — an
///     arms+fingers <see cref="AvatarMask" />, the "Spearman AC" controller (a copy of the goblin
///     enemy controller with a <c>SpearPose</c> layer inserted under Attack/Cheer, so swings and the
///     victory cheer still override the arms) and the "Spearman Override" controller that swaps the
///     Big Ork's brute idle/walk/run/death clips in, as Brute Override does for the Big Ork.
///
///     The arm poses are muscle values solved against the Big Ork rig holding the spear prop at
///     <see cref="SpearGripPosition" />/<see cref="SpearGripEuler" /> on Hand_R (the generator's prop
///     pose in <c>EnemyManifest</c>), on top of the brute idle's spine. Finger curl is copied from the
///     brute idle's right hand (a closed weapon grip) onto both hands. Re-running rebuilds the clips,
///     mask and layer in place (asset GUIDs are kept), so tweak the tables below and run it again.
/// </summary>
internal static class SpearmanPoseBuilder
{
    private const string Folder = "Assets/Bladehold/Bladehold Animations/Spearman";
    private const string ControllerPath = Folder + "/Spearman AC.controller";
    private const string OverridePath = Folder + "/Spearman Override.overrideController";
    private const string MaskPath = Folder + "/Spearman Upper Body.mask";
    private const string HighClipPath = Folder + "/Spearman Spears High.anim";
    private const string ForwardClipPath = Folder + "/Spearman Spears Forward.anim";

    private const string GoblinControllerPath = "Assets/Third Party/Synty/AnimationGoblinLocomotion/Animations/Sidekick/Enemy AC (Goblin).controller";
    private const string BruteOverridePath = "Assets/Bladehold/Bladehold Prefabs/Brute Override.overrideController";
    private const string BruteIdlePath = "Assets/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Animations/Brute@Idle.FBX";
    private const string BigOrkPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Big Ork Enemy Variant.prefab";

    internal const string PoseLayerName = "SpearPose";
    internal const string SpearsForwardParam = "SpearsForward";

    /// <summary>
    ///     The spear prop's pose under Hand_R that the muscle tables were solved for. The spear is
    ///     drawn at <see cref="SpearScale" />× size, gripped 0.15 mesh units up from the butt so the
    ///     butt clears the ground in the spears-high pose (the offset runs along the shaft, so the
    ///     poses' spear direction is unaffected).
    /// </summary>
    internal const float SpearScale = 1.5f;
    internal static readonly Vector3 SpearGripPosition = new Vector3(0.033f, 0.021f, 0.194f);
    internal static readonly Vector3 SpearGripEuler = new Vector3(285.6f, 90f, 218f);

    /// <summary>Spear upright at the right side, butt low, hand at chest height out from the body.</summary>
    private static readonly Dictionary<string, float> SpearsHigh = new Dictionary<string, float>
    {
        { "Right Shoulder Down-Up", 0.216f },
        { "Right Shoulder Front-Back", -0.638f },
        { "Right Arm Down-Up", -0.335f },
        { "Right Arm Front-Back", 0.031f },
        { "Right Arm Twist In-Out", 0.322f },
        { "Right Forearm Stretch", -0.247f },
        { "Right Forearm Twist In-Out", 0.485f },
        { "Right Hand Down-Up", -0.331f },
        { "Right Hand In-Out", 0.285f },
        { "Left Shoulder Down-Up", -0.135f },
        { "Left Shoulder Front-Back", -0.001f },
        { "Left Arm Down-Up", -0.913f },
        { "Left Arm Front-Back", 0.578f },
        { "Left Arm Twist In-Out", 0.423f },
        { "Left Forearm Stretch", -0.107f },
        { "Left Forearm Twist In-Out", 0.523f },
        { "Left Hand Down-Up", -0.491f },
        { "Left Hand In-Out", 0f },
    };

    /// <summary>Spear levelled at the waist pointing ahead (tip slightly up), left hand forward on the shaft.</summary>
    private static readonly Dictionary<string, float> SpearsForward = new Dictionary<string, float>
    {
        { "Right Shoulder Down-Up", 0.284f },
        { "Right Shoulder Front-Back", -0.700f },
        { "Right Arm Down-Up", -1.000f },
        { "Right Arm Front-Back", 0.700f },
        { "Right Arm Twist In-Out", -0.166f },
        { "Right Forearm Stretch", -0.478f },
        { "Right Forearm Twist In-Out", -0.109f },
        { "Right Hand Down-Up", -0.116f },
        { "Right Hand In-Out", 1.000f },
        { "Left Shoulder Down-Up", -0.217f },
        { "Left Shoulder Front-Back", -0.089f },
        { "Left Arm Down-Up", -0.769f },
        { "Left Arm Front-Back", -0.484f },
        { "Left Arm Twist In-Out", 0.117f },
        { "Left Forearm Stretch", -0.070f },
        { "Left Forearm Twist In-Out", 0.523f },
        { "Left Hand Down-Up", -0.491f },
        { "Left Hand In-Out", 0f },
    };

    private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

    [MenuItem("Bladehold/Spearman/Build Pose Animations")]
    internal static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/Bladehold/Bladehold Animations", "Spearman");
        }

        Dictionary<string, float> grip = SampleRightHandGrip();
        AnimationClip high = WriteClip(HighClipPath, "Spearman Spears High", SpearsHigh, grip);
        AnimationClip forward = WriteClip(ForwardClipPath, "Spearman Spears Forward", SpearsForward, grip);
        AvatarMask mask = WriteMask();
        AnimatorController controller = EnsureController(high, forward, mask);
        EnsureOverride(controller);

        AssetDatabase.SaveAssets();
        Debug.Log($"[SpearmanPoseBuilder] Built spear poses, mask, '{ControllerPath}' and '{OverridePath}'.");
    }

    /// <summary>Finger muscles of the brute idle's right hand (a closed weapon grip), keyed by side-less name ("Index 1 Stretched").</summary>
    private static Dictionary<string, float> SampleRightHandGrip()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BigOrkPrefabPath);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(BruteIdlePath);
        if (prefab == null || idle == null)
        {
            throw new System.InvalidOperationException($"[SpearmanPoseBuilder] Missing '{BigOrkPrefabPath}' or '{BruteIdlePath}'.");
        }

        var result = new Dictionary<string, float>();
        GameObject temp = Object.Instantiate(prefab);
        temp.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Animator animator = temp.GetComponentInChildren<Animator>();
            idle.SampleAnimation(animator.gameObject, 0f);
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            handler.Dispose();

            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string name = HumanTrait.MuscleName[i];
                if (name.StartsWith("Right ") && IsFingerMuscle(name))
                {
                    result[name.Substring("Right ".Length)] = pose.muscles[i];
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
        return result;
    }

    private static bool IsFingerMuscle(string muscleName)
    {
        foreach (string finger in Fingers)
        {
            if (muscleName.Contains(" " + finger + " ")) return true;
        }
        return false;
    }

    /// <summary>"Right Index 1 Stretched" → the clip property "RightHand.Index.1 Stretched".</summary>
    private static string FingerProperty(string side, string sidelessMuscle)
    {
        int space = sidelessMuscle.IndexOf(' ');
        return side + "Hand." + sidelessMuscle.Substring(0, space) + "." + sidelessMuscle.Substring(space + 1);
    }

    private static AnimationClip WriteClip(string path, string clipName, Dictionary<string, float> arms, Dictionary<string, float> grip)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = clipName };
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();

        foreach (KeyValuePair<string, float> muscle in arms)
        {
            SetConstant(clip, muscle.Key, muscle.Value);
        }
        foreach (KeyValuePair<string, float> finger in grip)
        {
            SetConstant(clip, FingerProperty("Right", finger.Key), finger.Value);
            SetConstant(clip, FingerProperty("Left", finger.Key), finger.Value);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetConstant(AnimationClip clip, string property, float value)
    {
        var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), property);
        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, value));
    }

    private static AvatarMask WriteMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask { name = "Spearman Upper Body" };
            AssetDatabase.CreateAsset(mask, MaskPath);
        }
        for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
        {
            bool arms = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
            mask.SetHumanoidBodyPartActive(part, arms);
        }
        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static AnimatorController EnsureController(AnimationClip high, AnimationClip forward, AvatarMask mask)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            if (!AssetDatabase.CopyAsset(GoblinControllerPath, ControllerPath))
            {
                throw new System.InvalidOperationException($"[SpearmanPoseBuilder] Couldn't copy '{GoblinControllerPath}'.");
            }
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        // Rebuild the pose layer from scratch so re-runs pick up table changes.
        for (int i = controller.layers.Length - 1; i >= 0; i--)
        {
            if (controller.layers[i].name == PoseLayerName)
            {
                controller.RemoveLayer(i);
            }
        }

        bool hasParam = false;
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            hasParam |= parameter.name == SpearsForwardParam;
        }
        if (!hasParam)
        {
            controller.AddParameter(SpearsForwardParam, AnimatorControllerParameterType.Bool);
        }

        var stateMachine = new AnimatorStateMachine { name = PoseLayerName, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);
        AnimatorState highState = stateMachine.AddState("Spears High");
        highState.motion = high;
        AnimatorState forwardState = stateMachine.AddState("Spears Forward");
        forwardState.motion = forward;
        stateMachine.defaultState = highState;
        AddTransition(highState, forwardState, AnimatorConditionMode.If);
        AddTransition(forwardState, highState, AnimatorConditionMode.IfNot);

        var layer = new AnimatorControllerLayer
        {
            name = PoseLayerName,
            stateMachine = stateMachine,
            avatarMask = mask,
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
        };

        // Under Attack (and Cheer/Slam after it) so those clips still own the arms when they play.
        var layers = new List<AnimatorControllerLayer>(controller.layers);
        int insertAt = layers.FindIndex(l => l.name == "Attack");
        if (insertAt < 0) insertAt = layers.Count;
        foreach (AnimatorControllerLayer existing in layers)
        {
            if (existing.syncedLayerIndex >= insertAt) existing.syncedLayerIndex++;
        }
        layers.Insert(insertAt, layer);
        controller.layers = layers.ToArray();
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.2f;
        transition.AddCondition(mode, 0f, SpearsForwardParam);
    }

    /// <summary>Spearman AC with the Big Ork's brute clip overrides (matched by original clip name).</summary>
    private static void EnsureOverride(AnimatorController controller)
    {
        var bruteOverride = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(BruteOverridePath);
        if (bruteOverride == null)
        {
            throw new System.InvalidOperationException($"[SpearmanPoseBuilder] Missing '{BruteOverridePath}'.");
        }
        var bruteClips = new Dictionary<string, AnimationClip>();
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        bruteOverride.GetOverrides(pairs);
        foreach (KeyValuePair<AnimationClip, AnimationClip> pair in pairs)
        {
            if (pair.Key != null && pair.Value != null) bruteClips[pair.Key.name] = pair.Value;
        }

        var spearOverride = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        if (spearOverride == null)
        {
            spearOverride = new AnimatorOverrideController(controller) { name = "Spearman Override" };
            AssetDatabase.CreateAsset(spearOverride, OverridePath);
        }
        spearOverride.runtimeAnimatorController = controller;

        pairs.Clear();
        spearOverride.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
        {
            bruteClips.TryGetValue(pairs[i].Key.name, out AnimationClip replacement);
            pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, replacement);
        }
        spearOverride.ApplyOverrides(pairs);
        EditorUtility.SetDirty(spearOverride);
    }
}
