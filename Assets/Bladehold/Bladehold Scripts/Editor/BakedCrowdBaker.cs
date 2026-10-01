using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
///     Bakes each crowd enemy's idle, run and attack (see <see cref="Targets" />: the fodder goblin and
///     the skeletons) into a <see cref="BakedCrowdAnimationSO" /> for <see cref="BakedCrowdRenderer" />:
///     samples the clips through a PlayableGraph on a temporary prefab instance (the attack is the
///     controller's upper-body-masked Attack layer over idle legs, and again over run legs), writes
///     each frame's bone skin matrices to a float texture, and copies the body mesh with its bone
///     indices/weights moved into UV4/UV5. Existing output assets are rewritten in place so their
///     GUIDs, and the tuning fields on the SO, survive a re-bake.
///
///     Held props (a sword, a shield: active MeshRenderers parented under the rig's bones) are merged
///     into the baked mesh, every vertex weighted fully to the bone the prop hangs off, so the crowd
///     draws them in the same instanced call. A prop must share the body's material (the Synty
///     PolygonDungeon atlas), since the crowd draws one material.
///
///     It also records real ragdoll falls: a goblin posed from an idle frame is launched the way
///     <see cref="KnockbackReceiver" /> throws a corpse, simulated in an isolated preview physics scene
///     on flat ground until it settles, and its bones are captured each step as a non-looping clip
///     (the last frame is the landed pose). The pelvis's horizontal travel is taken out, so the clip
///     is the tumble in place and the runtime <see cref="BakedFallBody" /> supplies the travel. Hard
///     landings are logged as <see cref="BakedCrowdAnimationSO.ImpactEvent" />s so the baked fall
///     bleeds where a real one would.
///
///     The first <see cref="DeathFallCount" /> variations are the deaths. More are recorded until
///     <see cref="MinGetUpFalls" /> land face-up, the way the GetUp clip starts: each face-up fall gets
///     its own copy of the get-up, turned and shifted so its first frame lies where the fall landed,
///     plus where the body stands at the end. Face-up falls serve non-lethal flings as well as deaths.
///
///     Re-run after changing a crowd enemy's body mesh, props, rig, ragdoll or any of the baked clips.
///     A new crowd enemy is a <see cref="Targets" /> entry plus a manifest entry wiring
///     <see cref="BakedCrowdAgent" /> to <see cref="EnsureDataAsset" />; generate the prefab first,
///     then bake.
/// </summary>
public static class BakedCrowdBaker
{
    private const string PrefabFolder = "Assets/Bladehold/Bladehold Prefabs/";
    private const string OutputFolder = "Assets/Bladehold/Bladehold Animations/Crowd";
    private const string ShaderName = "Bladehold/Baked Crowd Lit";

    /// <summary>Every baked crowd enemy: (crowd name, which names its output assets; source prefab).</summary>
    private static readonly (string crowdName, string prefabName)[] Targets =
    {
        ("Goblin", "Goblin Enemy Variant"),
        ("Skeleton Soldier", "Skeleton Soldier Enemy Variant"),
        ("Skeleton Soldier Shield", "Skeleton Soldier Shield Enemy Variant"),
        ("Skeleton Knight", "Skeleton Knight Enemy Variant"),
        ("Skeleton Knight Shield", "Skeleton Knight Shield Enemy Variant"),
    };

    public static string DataPathFor(string crowdName) => $"{OutputFolder}/{crowdName} Crowd Animation.asset";
    private static string TexturePathFor(string crowdName) => $"{OutputFolder}/{crowdName} Crowd Bones.asset";
    private static string MeshPathFor(string crowdName) => $"{OutputFolder}/{crowdName} Crowd Mesh.asset";
    private static string MaterialPathFor(string crowdName) => $"{OutputFolder}/{crowdName} Crowd.mat";

    /// <summary>
    ///     The crowd's data asset, created empty (its playback tuning copied from the goblin's) when
    ///     it doesn't exist yet, so the prefab generator can wire it before the first bake fills it in
    ///     place.
    /// </summary>
    public static BakedCrowdAnimationSO EnsureDataAsset(string crowdName)
    {
        string path = DataPathFor(crowdName);
        BakedCrowdAnimationSO data = AssetDatabase.LoadAssetAtPath<BakedCrowdAnimationSO>(path);
        if (data != null) return data;

        EnsureOutputFolder();
        data = ScriptableObject.CreateInstance<BakedCrowdAnimationSO>();
        BakedCrowdAnimationSO goblin = AssetDatabase.LoadAssetAtPath<BakedCrowdAnimationSO>(DataPathFor("Goblin"));
        if (goblin != null)
        {
            EditorUtility.CopySerialized(goblin, data);
            data.mesh = null;
            data.material = null;
            data.boneTexture = null;
            data.boneCount = 0;
            data.clips = Array.Empty<BakedCrowdAnimationSO.Clip>();
            data.deathClips = Array.Empty<int>();
            data.impacts = Array.Empty<BakedCrowdAnimationSO.ImpactEvent>();
            data.getUpClip = -1;
        }
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/Bladehold/Bladehold Animations", "Crowd");
        }
    }

    private const float BakeFps = 60f;
    private const string IdleStateName = "Idle_Standing";
    private const string LocomotionTreeStateName = "LocomotionBlendTree";
    private const string RunTreeName = "Run_F_Incline_BlendTree";
    private const string AttackLayerName = "Attack";
    private const string AttackStateName = "Attack";
    private const string GetUpStateName = "GetUp";

    // Ragdoll fall recordings: (launch strength multiplier, sideways angle in degrees), one clip each.
    // Past these, FallSettings generates more until enough land face-up.
    private static readonly Vector2[] FallVariations =
    {
        new Vector2(1.0f, 0f), new Vector2(1.5f, 18f), new Vector2(0.8f, -15f),
        new Vector2(2.0f, -8f), new Vector2(1.2f, 28f), new Vector2(1.7f, -25f),
    };
    private const int DeathFallCount = 6;
    private const int MinGetUpFalls = 4;
    private const int MaxFallCandidates = 48;
    // The belly faces at least this far up (dot with world up) for a fall to count as face-up.
    private const float FaceUpDot = 0.5f;
    private const float FallMinSeconds = 1.0f;
    private const float FallMaxSeconds = 4.0f;
    private const float FallLiftMetres = 0.03f;

    [MenuItem("Bladehold/Crowd/Bake Crowd Animations")]
    public static void BakeAll()
    {
        try
        {
            for (int i = 0; i < Targets.Length; i++)
            {
                (string crowdName, string prefabName) = Targets[i];
                EditorUtility.DisplayProgressBar("Baking crowd animations", crowdName, (float)i / Targets.Length);
                Bake(crowdName, PrefabFolder + prefabName + ".prefab");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void Bake(string crowdName, string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new InvalidOperationException($"{crowdName} prefab not found: {prefabPath} (run Bladehold > Generate Enemy Prefabs first).");
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException($"Shader '{ShaderName}' not found.");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        PlayableGraph graph = default;
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = instance.GetComponentInChildren<Animator>();
            SkinnedMeshRenderer body = FindActiveBody(instance);
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null) throw new InvalidOperationException($"The {crowdName} rig has no AnimatorController (override controllers aren't supported).");

            AnimationClip idleClip = FindStateClip(controller.layers[0].stateMachine, IdleStateName);
            float runReferenceSpeed;
            AnimationClip runClip = FindRunClip(controller.layers[0].stateMachine, out runReferenceSpeed);
            AnimatorControllerLayer attackLayer = FindLayer(controller, AttackLayerName);
            AnimationClip attackClip = FindStateClip(attackLayer.stateMachine, AttackStateName);
            if (attackLayer.avatarMask == null) throw new InvalidOperationException("The Attack layer has no avatar mask.");
            AnimationClip getUpClip = FindStateClip(controller.layers[0].stateMachine, GetUpStateName);

            animator.applyRootMotion = false;
            // A PlayableGraph doesn't write transforms in edit mode on its own; AnimationMode's graph
            // sampling (what Timeline previews use) does, and restores the rig when stopped.
            AnimationMode.StartAnimationMode();
            graph = PlayableGraph.Create("BakedCrowdBaker");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Bake", animator);

            Transform[] bones = body.bones;
            Matrix4x4[] bindposes = body.sharedMesh.bindposes;
            var rows = new List<Color[]>();
            var clips = new List<BakedCrowdAnimationSO.Clip>();

            // Order must match the BakedCrowdAnimationSO.Clip* indices.
            clips.Add(SampleClip(graph, output, animator, bones, bindposes, rows, "Idle", idleClip, null, null, true, 0f));
            clips.Add(SampleClip(graph, output, animator, bones, bindposes, rows, "Run", runClip, null, null, true, runReferenceSpeed));
            clips.Add(SampleClip(graph, output, animator, bones, bindposes, rows, "Attack", idleClip, attackClip, attackLayer.avatarMask, false, 0f));
            clips.Add(SampleClip(graph, output, animator, bones, bindposes, rows, "Attack (moving)", runClip, attackClip, attackLayer.avatarMask, false, 0f));
            int getUpIndex = clips.Count;
            clips.Add(SampleClip(graph, output, animator, bones, bindposes, rows, "Get Up", getUpClip, null, null, false, 0f));
            AnimationMode.StopAnimationMode();

            int hipsBone = Array.IndexOf(bones, animator.GetBoneTransform(HumanBodyBones.Hips));
            int headBone = Array.IndexOf(bones, animator.GetBoneTransform(HumanBodyBones.Head));
            Transform chestTransform = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chestTransform == null) chestTransform = animator.GetBoneTransform(HumanBodyBones.Spine);
            int chestBone = Array.IndexOf(bones, chestTransform);
            if (hipsBone < 0 || headBone < 0 || chestBone < 0) throw new InvalidOperationException($"The {crowdName} body isn't skinned to its hips, chest/spine and head bones.");

            // The get-up starts lying face-up: its chest axis that points up there is the belly.
            BakedCrowdAnimationSO.Clip getUp = clips[getUpIndex];
            Color[] getUpStart = rows[getUp.startFrame];
            Vector3 bellyLocal = Quaternion.Inverse(BoneInRig(getUpStart, chestBone, bindposes).rotation) * Vector3.up;

            var deathClips = new List<int>();
            var getUpFalls = new List<int>();
            var impacts = new List<BakedCrowdAnimationSO.ImpactEvent>();
            BakedCrowdAnimationSO.Clip idle = clips[BakedCrowdAnimationSO.ClipIdle];
            for (int v = 0; v < MaxFallCandidates && (deathClips.Count < DeathFallCount || getUpFalls.Count < MinGetUpFalls); v++)
            {
                Color[] startPose = rows[idle.startFrame + (v * 17) % idle.frameCount];
                var fallRows = new List<Color[]>();
                var fallImpacts = new List<BakedCrowdAnimationSO.ImpactEvent>();
                BakedCrowdAnimationSO.Clip fall = RecordFall(prefab, v, FallSettings(v), startPose, clips.Count, rows.Count, fallRows, fallImpacts);

                Matrix4x4 landedChest = BoneInRig(fallRows[fallRows.Count - 1], chestBone, bindposes);
                bool faceUp = (landedChest.rotation * bellyLocal).y >= FaceUpDot;
                bool keep = deathClips.Count < DeathFallCount || (faceUp && getUpFalls.Count < MinGetUpFalls);
                if (!keep) continue;

                rows.AddRange(fallRows);
                impacts.AddRange(fallImpacts);
                deathClips.Add(clips.Count);
                if (faceUp) getUpFalls.Add(clips.Count);
                clips.Add(fall);
            }
            if (getUpFalls.Count < MinGetUpFalls)
            {
                Debug.LogWarning($"[BakedCrowdBaker] Only {getUpFalls.Count} of {MaxFallCandidates} recorded falls landed face-up; non-lethal baked flings have fewer variations.");
            }

            foreach (int fallIndex in getUpFalls)
            {
                BakedCrowdAnimationSO.Clip fall = clips[fallIndex];
                fall.getUpClip = clips.Count;
                clips.Add(AlignGetUp(fall, getUp, rows, bindposes, hipsBone, headBone));
            }

            EnsureOutputFolder();
            List<MeshRenderer> props = FindProps(animator.transform, body);
            Texture2D texture = WriteTexture(crowdName, rows, bones.Length * 3);
            Mesh mesh = WriteMesh(crowdName, body, props);
            Material material = WriteMaterial(crowdName, shader, body.sharedMaterial, texture);

            BakedCrowdAnimationSO data = EnsureDataAsset(crowdName);
            data.mesh = mesh;
            data.material = material;
            data.boneTexture = texture;
            data.boneCount = bones.Length;
            data.bakeFps = BakeFps;
            data.clips = clips.ToArray();
            data.deathClips = deathClips.ToArray();
            data.impacts = impacts.ToArray();
            data.getUpClip = getUpIndex;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BakedCrowdBaker] {crowdName}: baked {bones.Length} bones, {rows.Count} frames at {BakeFps} fps " +
                      $"(idle '{idleClip.name}', run '{runClip.name}' @ {runReferenceSpeed} m/s, attack '{attackClip.name}', " +
                      $"get-up '{getUpClip.name}', {deathClips.Count} ragdoll falls ({getUpFalls.Count} face-up with a get-up) " +
                      $"with {impacts.Count} impacts, {props.Count} merged props) into {DataPathFor(crowdName)}.");
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (graph.IsValid()) graph.Destroy();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static SkinnedMeshRenderer FindActiveBody(GameObject instance)
    {
        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.enabled) return smr;
        }
        throw new InvalidOperationException($"'{instance.name}' has no active SkinnedMeshRenderer.");
    }

    /// <summary>The held props: active MeshRenderers under the rig, each hanging off one of the body's bones.</summary>
    private static List<MeshRenderer> FindProps(Transform rig, SkinnedMeshRenderer body)
    {
        var props = new List<MeshRenderer>();
        foreach (MeshRenderer renderer in rig.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!renderer.enabled) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            if (renderer.sharedMaterial != body.sharedMaterial || renderer.sharedMaterials.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Prop '{renderer.name}' doesn't use the body's material '{body.sharedMaterial.name}'; the baked crowd draws a single material.");
            }
            if (PropBone(renderer.transform, body.bones) < 0)
            {
                throw new InvalidOperationException($"Prop '{renderer.name}' isn't parented under any of the body's bones.");
            }
            props.Add(renderer);
        }
        return props;
    }

    /// <summary>Index into <paramref name="bones" /> of the nearest bone above <paramref name="prop" />, or -1.</summary>
    private static int PropBone(Transform prop, Transform[] bones)
    {
        for (Transform t = prop.parent; t != null; t = t.parent)
        {
            int index = Array.IndexOf(bones, t);
            if (index >= 0) return index;
        }
        return -1;
    }

    private static AnimatorControllerLayer FindLayer(AnimatorController controller, string layerName)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name == layerName) return layer;
        }
        throw new InvalidOperationException($"Controller '{controller.name}' has no '{layerName}' layer.");
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == stateName) return child.state;
        }
        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
        {
            AnimatorState found = FindState(sub.stateMachine, stateName);
            if (found != null) return found;
        }
        return null;
    }

    private static AnimationClip FindStateClip(AnimatorStateMachine machine, string stateName)
    {
        AnimatorState state = FindState(machine, stateName);
        AnimationClip clip = state != null ? state.motion as AnimationClip : null;
        if (clip == null) throw new InvalidOperationException($"State '{stateName}' is missing or has no clip.");
        return clip;
    }

    /// <summary>The flat-ground clip of the non-strafing forward run, and the MoveSpeed it sits at in the locomotion tree.</summary>
    private static AnimationClip FindRunClip(AnimatorStateMachine machine, out float referenceSpeed)
    {
        AnimatorState locomotion = FindState(machine, LocomotionTreeStateName);
        BlendTree tree = locomotion != null ? locomotion.motion as BlendTree : null;
        if (tree == null) throw new InvalidOperationException($"State '{LocomotionTreeStateName}' has no blend tree.");
        foreach (ChildMotion child in tree.children)
        {
            BlendTree runTree = child.motion as BlendTree;
            if (runTree == null || runTree.name != RunTreeName) continue;
            referenceSpeed = child.position.x;
            // The incline tree's flat entry is the child nearest threshold 0.
            AnimationClip best = null;
            float bestDistance = float.MaxValue;
            foreach (ChildMotion inclineChild in runTree.children)
            {
                AnimationClip clip = inclineChild.motion as AnimationClip;
                if (clip == null) continue;
                float distance = Mathf.Abs(inclineChild.threshold);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = clip;
                }
            }
            if (best != null) return best;
        }
        throw new InvalidOperationException($"No '{RunTreeName}' clip found in the locomotion tree.");
    }

    private static BakedCrowdAnimationSO.Clip SampleClip(PlayableGraph graph, AnimationPlayableOutput output, Animator animator,
        Transform[] bones, Matrix4x4[] bindposes, List<Color[]> rows, string clipName,
        AnimationClip baseClip, AnimationClip overlayClip, AvatarMask overlayMask, bool loop, float referenceSpeed)
    {
        AnimationClipPlayable basePlayable = AnimationClipPlayable.Create(graph, baseClip);
        basePlayable.SetApplyFootIK(false);
        AnimationClipPlayable overlayPlayable = default;
        Playable root = basePlayable;
        if (overlayClip != null)
        {
            overlayPlayable = AnimationClipPlayable.Create(graph, overlayClip);
            overlayPlayable.SetApplyFootIK(false);
            AnimationLayerMixerPlayable mixer = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(basePlayable, 0, mixer, 0);
            graph.Connect(overlayPlayable, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 1f);
            mixer.SetLayerMaskFromAvatarMask(1, overlayMask);
            root = mixer;
        }
        output.SetSourcePlayable(root);

        float length = overlayClip != null ? overlayClip.length : baseClip.length;
        // A loop's last frame would duplicate its first, so loops stop one frame short.
        int frameCount = loop ? Mathf.Max(1, Mathf.RoundToInt(length * BakeFps)) : Mathf.CeilToInt(length * BakeFps) + 1;
        var clip = new BakedCrowdAnimationSO.Clip
        {
            name = clipName,
            startFrame = rows.Count,
            frameCount = frameCount,
            length = length,
            loop = loop,
            referenceSpeed = referenceSpeed,
        };

        Transform rig = animator.transform;
        for (int f = 0; f < frameCount; f++)
        {
            double time = Math.Min(f / BakeFps, length);
            // The base loops under a longer overlay (run legs under the attack swing).
            basePlayable.SetTime(time % baseClip.length);
            if (overlayClip != null) overlayPlayable.SetTime(time);
            // Clip times are set explicitly above; sample with no extra time step.
            AnimationMode.BeginSampling();
            AnimationMode.SamplePlayableGraph(graph, 0, 0f);
            AnimationMode.EndSampling();

            Matrix4x4 toRig = rig.worldToLocalMatrix;
            var row = new Color[bones.Length * 3];
            for (int b = 0; b < bones.Length; b++)
            {
                Matrix4x4 skin = toRig * bones[b].localToWorldMatrix * bindposes[b];
                for (int r = 0; r < 3; r++)
                {
                    Vector4 v = skin.GetRow(r);
                    row[b * 3 + r] = new Color(v.x, v.y, v.z, v.w);
                }
            }
            rows.Add(row);
        }

        root.Destroy();
        if (overlayClip != null)
        {
            basePlayable.Destroy();
            overlayPlayable.Destroy();
        }
        return clip;
    }

    /// <summary>Variation <paramref name="v" />'s (launch strength multiplier, sideways angle): the fixed list, then a deterministic spread.</summary>
    private static Vector2 FallSettings(int v)
    {
        if (v < FallVariations.Length) return FallVariations[v];
        float strength = 0.8f + (v * 0.618034f % 1f) * 1.2f;
        float angle = (v * 37 % 61) - 30f;
        return new Vector2(strength, angle);
    }

    /// <summary>
    ///     Simulates one real ragdoll fall in an isolated preview physics scene and records it as a clip
    ///     into <paramref name="rows" /> / <paramref name="impacts" /> (frames numbered from
    ///     <paramref name="startFrame" />). The launch copies KnockbackReceiver's death throw (flat push +
    ///     lift, tumble spin, a kicked limb), thrown along the rig's -Z. The pelvis's horizontal travel
    ///     is subtracted from every frame and impact.
    /// </summary>
    private static BakedCrowdAnimationSO.Clip RecordFall(GameObject prefab, int variation, Vector2 settings, Color[] startPose,
        int clipIndex, int startFrame, List<Color[]> rows, List<BakedCrowdAnimationSO.ImpactEvent> impacts)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            PhysicsScene physics = scene.GetPhysicsScene();
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(Vector3.up * FallLiftMetres, Quaternion.identity);
            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, scene);
            BoxCollider groundBox = ground.AddComponent<BoxCollider>();
            groundBox.size = new Vector3(80f, 1f, 80f);
            groundBox.center = new Vector3(0f, -0.5f, 0f);

            Animator animator = instance.GetComponentInChildren<Animator>();
            animator.enabled = false;
            SkinnedMeshRenderer body = FindActiveBody(instance);
            Transform[] bones = body.bones;
            Matrix4x4[] bindposes = body.sharedMesh.bindposes;
            Transform rig = animator.transform;
            PoseFromRow(bones, bindposes, rig, startPose);

            EnemyRagdoll ragdoll = instance.GetComponent<EnemyRagdoll>();
            RagdollConfigSO ragdollConfig = ragdoll != null ? ragdoll.Config : null;
            KnockbackConfigSO knockbackConfig = LoadKnockbackConfig(instance);
            if (ragdollConfig == null || knockbackConfig == null)
            {
                throw new InvalidOperationException($"{prefab.name} needs EnemyRagdoll and KnockbackReceiver configs to record ragdoll falls.");
            }

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Rigidbody[] bodies = hips.GetComponentsInChildren<Rigidbody>(true);
            Rigidbody pelvis = hips.GetComponent<Rigidbody>();
            if (bodies.Length < 2 || pelvis == null) throw new InvalidOperationException($"{prefab.name} has no baked ragdoll bodies.");
            var boneColliders = new HashSet<Collider>(hips.GetComponentsInChildren<Collider>(true));
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                // Only the ragdoll's own solid bone colliders take part, as in the game.
                collider.enabled = boneColliders.Contains(collider) && !collider.isTrigger;
            }
            Physics.SyncTransforms();

            var random = new System.Random(1234 + variation * 7919);
            Vector3 flatDir = Quaternion.AngleAxis(settings.y, Vector3.up) * -rig.forward;
            Vector3 launch = flatDir * (1.5f * settings.x) + Vector3.up * 0.8f;
            float torque = knockbackConfig.spinTorque;
            Vector3 tumbleAxis = Vector3.Cross(Vector3.up, flatDir);
            Vector3 noise = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 2f;
            foreach (Rigidbody rb in bodies)
            {
                rb.isKinematic = false;
                rb.linearVelocity = launch;
            }
            pelvis.angularVelocity = tumbleAxis * torque + noise * (torque * 0.3f);
            Rigidbody kicked = bodies[1 + random.Next(bodies.Length - 1)];
            kicked.linearVelocity += noise.normalized * 1.2f;

            var clip = new BakedCrowdAnimationSO.Clip
            {
                name = "Ragdoll Fall " + (variation + 1),
                startFrame = startFrame,
                loop = false,
                throwDirection = flatDir,
                launchVelocity = launch,
                getUpClip = -1,
            };
            Vector3 startHips = rig.InverseTransformPoint(hips.position);
            // The hold point for long flights: the body is half down (head at 60% of its standing height).
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            float startHeadHeight = head.position.y;
            float halfDown = -1f;
            var lastVelocity = new Vector3[bodies.Length];
            var nextImpact = new float[bodies.Length];
            for (int b = 0; b < bodies.Length; b++) lastVelocity[b] = bodies[b].linearVelocity;
            float maxMultiplier = Mathf.Max(0.01f, Mathf.Max(ragdollConfig.torsoBaseScale, ragdollConfig.headBaseScale, ragdollConfig.limbBaseScale));

            float dt = 1f / BakeFps;
            float time = 0f;
            float settled = 0f;
            rows.Add(CaptureRow(bones, bindposes, rig, Vector3.zero));
            while (time < FallMaxSeconds)
            {
                physics.Simulate(dt);
                time += dt;
                Vector3 travel = rig.InverseTransformPoint(hips.position) - startHips;
                travel.y = 0f;
                rows.Add(CaptureRow(bones, bindposes, rig, travel));
                if (halfDown < 0f && head.position.y < startHeadHeight * 0.6f) halfDown = time;

                for (int b = 0; b < bodies.Length; b++)
                {
                    Vector3 velocity = bodies[b].linearVelocity;
                    Vector3 change = velocity - lastVelocity[b];
                    lastVelocity[b] = velocity;
                    // A hard landing: a big velocity change that pushed the body back up off the ground.
                    float speed = change.magnitude;
                    if (change.y <= 0f || speed < ragdollConfig.minImpactSpeed || time < nextImpact[b]) continue;
                    nextImpact[b] = time + ragdollConfig.impactCooldown;

                    RagdollBloodImpact part = bodies[b].GetComponent<RagdollBloodImpact>();
                    float partMultiplier = PartMultiplier(ragdollConfig, part != null ? part.BodyPartType : RagdollBodyPartType.Limb);
                    float speedFactor = Mathf.Clamp01((speed - ragdollConfig.minImpactSpeed) /
                                                      Mathf.Max(0.01f, ragdollConfig.maxImpactSpeed - ragdollConfig.minImpactSpeed));
                    Vector3 contact = bodies[b].worldCenterOfMass;
                    contact.y = 0.01f;
                    impacts.Add(new BakedCrowdAnimationSO.ImpactEvent
                    {
                        clip = clipIndex,
                        time = time,
                        localPoint = rig.InverseTransformPoint(contact) - travel,
                        intensity = speedFactor * partMultiplier / maxMultiplier,
                        decalSize = partMultiplier * Mathf.Lerp(ragdollConfig.minDecalSize, ragdollConfig.maxDecalSize, speedFactor),
                    });
                }

                if (time >= FallMinSeconds)
                {
                    settled = pelvis.linearVelocity.magnitude < knockbackConfig.settleSpeed ? settled + dt : 0f;
                    if (settled >= knockbackConfig.settleTime) break;
                }
            }

            clip.frameCount = rows.Count;
            clip.length = (clip.frameCount - 1) / BakeFps;
            clip.airborneHoldTime = halfDown > 0f ? halfDown : clip.length * 0.5f;
            return clip;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static KnockbackConfigSO LoadKnockbackConfig(GameObject instance)
    {
        KnockbackReceiver receiver = instance.GetComponent<KnockbackReceiver>();
        if (receiver == null) return null;
        return new SerializedObject(receiver).FindProperty("config").objectReferenceValue as KnockbackConfigSO;
    }

    private static float PartMultiplier(RagdollConfigSO config, RagdollBodyPartType type)
    {
        switch (type)
        {
            case RagdollBodyPartType.Torso: return config.torsoBaseScale;
            case RagdollBodyPartType.Head: return config.headBaseScale;
            default: return config.limbBaseScale;
        }
    }

    /// <summary>
    ///     A copy of <paramref name="getUp" /> turned and shifted (about world up, in rig space) so its
    ///     first frame's body line (hips to head) lies along <paramref name="fall" />'s landed one, with
    ///     where the root ends up standing.
    /// </summary>
    private static BakedCrowdAnimationSO.Clip AlignGetUp(BakedCrowdAnimationSO.Clip fall, BakedCrowdAnimationSO.Clip getUp,
        List<Color[]> rows, Matrix4x4[] bindposes, int hipsBone, int headBone)
    {
        Color[] landed = rows[fall.startFrame + fall.frameCount - 1];
        Color[] lying = rows[getUp.startFrame];
        Vector3 fallHips = BoneInRig(landed, hipsBone, bindposes).GetPosition();
        Vector3 fallHead = BoneInRig(landed, headBone, bindposes).GetPosition();
        Vector3 getUpHips = BoneInRig(lying, hipsBone, bindposes).GetPosition();
        Vector3 getUpHead = BoneInRig(lying, headBone, bindposes).GetPosition();

        Vector3 fallLine = fallHead - fallHips;
        Vector3 getUpLine = getUpHead - getUpHips;
        fallLine.y = 0f;
        getUpLine.y = 0f;
        float yaw = Vector3.SignedAngle(getUpLine, fallLine, Vector3.up);
        Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up);
        Vector3 offset = (fallHips + fallHead) * 0.5f - turn * ((getUpHips + getUpHead) * 0.5f);
        offset.y = 0f;
        Matrix4x4 align = Matrix4x4.TRS(offset, turn, Vector3.one);

        var clip = new BakedCrowdAnimationSO.Clip
        {
            name = "Get Up (" + fall.name + ")",
            startFrame = rows.Count,
            frameCount = getUp.frameCount,
            length = getUp.length,
            loop = false,
            getUpClip = -1,
            standPosition = offset,
            standYaw = yaw,
        };
        for (int f = 0; f < getUp.frameCount; f++)
        {
            Color[] source = rows[getUp.startFrame + f];
            var row = new Color[source.Length];
            for (int b = 0; b < source.Length / 3; b++)
            {
                WriteSkin(row, b, align * ReadSkin(source, b));
            }
            rows.Add(row);
        }
        return clip;
    }

    private static Matrix4x4 ReadSkin(Color[] row, int bone)
    {
        Matrix4x4 skin = Matrix4x4.identity;
        for (int r = 0; r < 3; r++)
        {
            Color c = row[bone * 3 + r];
            skin.SetRow(r, new Vector4(c.r, c.g, c.b, c.a));
        }
        return skin;
    }

    private static void WriteSkin(Color[] row, int bone, Matrix4x4 skin)
    {
        for (int r = 0; r < 3; r++)
        {
            Vector4 v = skin.GetRow(r);
            row[bone * 3 + r] = new Color(v.x, v.y, v.z, v.w);
        }
    }

    /// <summary>A baked bone's own transform in rig space (its skin matrix without the bind pose).</summary>
    private static Matrix4x4 BoneInRig(Color[] row, int bone, Matrix4x4[] bindposes)
    {
        return ReadSkin(row, bone) * bindposes[bone].inverse;
    }

    /// <summary>One frame's skin matrices in rig space, shifted back by <paramref name="travel" /> (rig space).</summary>
    private static Color[] CaptureRow(Transform[] bones, Matrix4x4[] bindposes, Transform rig, Vector3 travel)
    {
        Matrix4x4 toRig = Matrix4x4.Translate(-travel) * rig.worldToLocalMatrix;
        var row = new Color[bones.Length * 3];
        for (int b = 0; b < bones.Length; b++)
        {
            Matrix4x4 skin = toRig * bones[b].localToWorldMatrix * bindposes[b];
            for (int r = 0; r < 3; r++)
            {
                Vector4 v = skin.GetRow(r);
                row[b * 3 + r] = new Color(v.x, v.y, v.z, v.w);
            }
        }
        return row;
    }

    /// <summary>Poses the skeleton from a baked row (rig-space skin matrices), parents first.</summary>
    private static void PoseFromRow(Transform[] bones, Matrix4x4[] bindposes, Transform rig, Color[] row)
    {
        var order = new List<int>();
        for (int i = 0; i < bones.Length; i++) order.Add(i);
        order.Sort((a, b) => Depth(bones[a], rig).CompareTo(Depth(bones[b], rig)));
        Matrix4x4 toWorld = rig.localToWorldMatrix;
        foreach (int i in order)
        {
            Matrix4x4 skin = Matrix4x4.identity;
            for (int r = 0; r < 3; r++)
            {
                Color c = row[i * 3 + r];
                skin.SetRow(r, new Vector4(c.r, c.g, c.b, c.a));
            }
            Matrix4x4 world = toWorld * skin * bindposes[i].inverse;
            bones[i].SetPositionAndRotation(world.GetPosition(), world.rotation);
        }
    }

    private static int Depth(Transform t, Transform root)
    {
        int depth = 0;
        for (; t != null && t != root; t = t.parent) depth++;
        return depth;
    }


    private static Texture2D WriteTexture(string crowdName, List<Color[]> rows, int width)
    {
        string path = TexturePathFor(crowdName);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool isNew = texture == null;
        if (isNew)
        {
            texture = new Texture2D(width, rows.Count, TextureFormat.RGBAHalf, false, true);
        }
        else
        {
            texture.Reinitialize(width, rows.Count, TextureFormat.RGBAHalf, false);
        }
        texture.name = crowdName + " Crowd Bones";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[width * rows.Count];
        for (int y = 0; y < rows.Count; y++)
        {
            Array.Copy(rows[y], 0, pixels, y * width, width);
        }
        texture.SetPixels(pixels);
        // Stays readable: BakedCrowdAgent reads it on the CPU to pose the real skeleton on promotion.
        texture.Apply(false, false);
        if (isNew) AssetDatabase.CreateAsset(texture, path);
        else EditorUtility.SetDirty(texture);
        return texture;
    }

    /// <summary>
    ///     The body mesh with its skinning moved into UV4/UV5, plus each prop merged into submesh 0.
    ///     A prop vertex is moved into the body's bind space through its bone (bindpose⁻¹ · bone⁻¹ ·
    ///     prop), so skinning it fully to that bone puts it back on the hand in every frame.
    /// </summary>
    private static Mesh WriteMesh(string crowdName, SkinnedMeshRenderer body, List<MeshRenderer> props)
    {
        Mesh source = body.sharedMesh;
        string path = MeshPathFor(crowdName);
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = crowdName + " Crowd Mesh";

        var vertices = new List<Vector3>(source.vertices);
        var normals = new List<Vector3>(source.normals);
        var tangents = new List<Vector4>(source.tangents);
        var uvs = new List<Vector2>(source.uv);
        var indices = new List<Vector4>(vertices.Count);
        var values = new List<Vector4>(vertices.Count);
        foreach (BoneWeight w in source.boneWeights)
        {
            indices.Add(new Vector4(w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3));
            values.Add(new Vector4(w.weight0, w.weight1, w.weight2, w.weight3));
        }
        if (tangents.Count != vertices.Count)
        {
            tangents = new List<Vector4>(new Vector4[vertices.Count]);
        }

        var submeshes = new List<int>[source.subMeshCount];
        for (int s = 0; s < source.subMeshCount; s++)
        {
            submeshes[s] = new List<int>(source.GetTriangles(s));
        }

        Transform[] bones = body.bones;
        Matrix4x4[] bindposes = source.bindposes;
        foreach (MeshRenderer prop in props)
        {
            Mesh propMesh = prop.GetComponent<MeshFilter>().sharedMesh;
            int bone = PropBone(prop.transform, bones);
            Matrix4x4 toBind = bindposes[bone].inverse * bones[bone].worldToLocalMatrix * prop.transform.localToWorldMatrix;
            int offset = vertices.Count;

            Vector3[] propVertices = propMesh.vertices;
            Vector3[] propNormals = propMesh.normals;
            Vector4[] propTangents = propMesh.tangents;
            Vector2[] propUvs = propMesh.uv;
            for (int v = 0; v < propVertices.Length; v++)
            {
                vertices.Add(toBind.MultiplyPoint3x4(propVertices[v]));
                normals.Add(v < propNormals.Length ? toBind.MultiplyVector(propNormals[v]).normalized : Vector3.up);
                if (v < propTangents.Length)
                {
                    Vector3 tangent = toBind.MultiplyVector(propTangents[v]).normalized;
                    tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, propTangents[v].w));
                }
                else
                {
                    tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                }
                uvs.Add(v < propUvs.Length ? propUvs[v] : Vector2.zero);
                indices.Add(new Vector4(bone, 0f, 0f, 0f));
                values.Add(new Vector4(1f, 0f, 0f, 0f));
            }
            for (int s = 0; s < propMesh.subMeshCount; s++)
            {
                foreach (int index in propMesh.GetTriangles(s))
                {
                    submeshes[0].Add(index + offset);
                }
            }
        }

        mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : source.indexFormat;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(4, indices);
        mesh.SetUVs(5, values);
        mesh.subMeshCount = submeshes.Length;
        for (int s = 0; s < submeshes.Length; s++)
        {
            mesh.SetTriangles(submeshes[s], s);
        }
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material WriteMaterial(string crowdName, Shader shader, Material source, Texture2D boneTexture)
    {
        string path = MaterialPathFor(crowdName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;
        if (isNew) material = new Material(shader);
        material.shader = shader;
        material.name = crowdName + " Crowd";

        // The Synty Generic_Basic body material's look, mapped onto URP Lit's inputs.
        if (source != null)
        {
            if (source.HasProperty("_Albedo_Map")) material.SetTexture("_BaseMap", source.GetTexture("_Albedo_Map"));
            if (source.HasProperty("_BaseColor")) material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            if (source.HasProperty("_Alpha_Clip_Threshold")) material.SetFloat("_Cutoff", source.GetFloat("_Alpha_Clip_Threshold"));
            if (source.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
            if (source.HasProperty("_Metallic")) material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
        }
        material.SetTexture("_CrowdBoneTex", boneTexture);
        material.SetFloat("_AlphaClip", 1f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.enableInstancing = true;
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

        if (isNew) AssetDatabase.CreateAsset(material, path);
        else EditorUtility.SetDirty(material);
        return material;
    }
}
