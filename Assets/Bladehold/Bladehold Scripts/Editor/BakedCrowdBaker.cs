using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
///     Bakes the fodder goblin's idle, run and attack into a <see cref="BakedCrowdAnimationSO" /> for
///     <see cref="BakedCrowdRenderer" />: samples the clips through a PlayableGraph on a temporary
///     prefab instance (the attack is the controller's upper-body-masked Attack layer over idle legs,
///     and again over run legs), writes each frame's bone skin matrices to a float texture, and copies
///     the body mesh with its bone indices/weights moved into UV4/UV5. Existing output assets are
///     rewritten in place so their GUIDs, and the tuning fields on the SO, survive a re-bake.
///
///     Re-run after changing the goblin's body mesh, rig or any of the three clips.
/// </summary>
public static class BakedCrowdBaker
{
    private const string PrefabPath = "Assets/Bladehold/Bladehold Prefabs/Goblin Enemy Variant.prefab";
    private const string OutputFolder = "Assets/Bladehold/Bladehold Animations/Crowd";
    public const string DataPath = OutputFolder + "/Goblin Crowd Animation.asset";
    private const string TexturePath = OutputFolder + "/Goblin Crowd Bones.asset";
    private const string MeshPath = OutputFolder + "/Goblin Crowd Mesh.asset";
    private const string MaterialPath = OutputFolder + "/Goblin Crowd.mat";
    private const string ShaderName = "Bladehold/Baked Crowd Lit";

    private const float BakeFps = 60f;
    private const string IdleStateName = "Idle_Standing";
    private const string LocomotionTreeStateName = "LocomotionBlendTree";
    private const string RunTreeName = "Run_F_Incline_BlendTree";
    private const string AttackLayerName = "Attack";
    private const string AttackStateName = "Attack";

    [MenuItem("Bladehold/Crowd/Bake Goblin Crowd Animation")]
    public static void Bake()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException($"Goblin prefab not found: {PrefabPath}");
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
            if (controller == null) throw new InvalidOperationException("The goblin rig has no AnimatorController.");

            AnimationClip idleClip = FindStateClip(controller.layers[0].stateMachine, IdleStateName);
            float runReferenceSpeed;
            AnimationClip runClip = FindRunClip(controller.layers[0].stateMachine, out runReferenceSpeed);
            AnimatorControllerLayer attackLayer = FindLayer(controller, AttackLayerName);
            AnimationClip attackClip = FindStateClip(attackLayer.stateMachine, AttackStateName);
            if (attackLayer.avatarMask == null) throw new InvalidOperationException("The Attack layer has no avatar mask.");

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

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Bladehold/Bladehold Animations", "Crowd");
            }

            Texture2D texture = WriteTexture(rows, bones.Length * 3);
            Mesh mesh = WriteMesh(body.sharedMesh);
            Material material = WriteMaterial(shader, body.sharedMaterial, texture);

            BakedCrowdAnimationSO data = AssetDatabase.LoadAssetAtPath<BakedCrowdAnimationSO>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BakedCrowdAnimationSO>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.mesh = mesh;
            data.material = material;
            data.boneTexture = texture;
            data.boneCount = bones.Length;
            data.bakeFps = BakeFps;
            data.clips = clips.ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BakedCrowdBaker] Baked {bones.Length} bones, {rows.Count} frames at {BakeFps} fps " +
                      $"(idle '{idleClip.name}', run '{runClip.name}' @ {runReferenceSpeed} m/s, attack '{attackClip.name}') into {DataPath}.");
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
        throw new InvalidOperationException("The goblin prefab has no active SkinnedMeshRenderer.");
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

    private static Texture2D WriteTexture(List<Color[]> rows, int width)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        bool isNew = texture == null;
        if (isNew)
        {
            texture = new Texture2D(width, rows.Count, TextureFormat.RGBAFloat, false, true);
        }
        else
        {
            texture.Reinitialize(width, rows.Count, TextureFormat.RGBAFloat, false);
        }
        texture.name = "Goblin Crowd Bones";
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
        if (isNew) AssetDatabase.CreateAsset(texture, TexturePath);
        else EditorUtility.SetDirty(texture);
        return texture;
    }

    private static Mesh WriteMesh(Mesh source)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "Goblin Crowd Mesh";
        mesh.indexFormat = source.indexFormat;
        mesh.vertices = source.vertices;
        mesh.normals = source.normals;
        mesh.tangents = source.tangents;
        mesh.uv = source.uv;

        BoneWeight[] weights = source.boneWeights;
        var indices = new List<Vector4>(weights.Length);
        var values = new List<Vector4>(weights.Length);
        foreach (BoneWeight w in weights)
        {
            indices.Add(new Vector4(w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3));
            values.Add(new Vector4(w.weight0, w.weight1, w.weight2, w.weight3));
        }
        mesh.SetUVs(4, indices);
        mesh.SetUVs(5, values);

        mesh.subMeshCount = source.subMeshCount;
        for (int s = 0; s < source.subMeshCount; s++)
        {
            mesh.SetTriangles(source.GetTriangles(s), s);
        }
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        if (isNew) AssetDatabase.CreateAsset(mesh, MeshPath);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material WriteMaterial(Shader shader, Material source, Texture2D boneTexture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        bool isNew = material == null;
        if (isNew) material = new Material(shader);
        material.shader = shader;
        material.name = "Goblin Crowd";

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

        if (isNew) AssetDatabase.CreateAsset(material, MaterialPath);
        else EditorUtility.SetDirty(material);
        return material;
    }
}
