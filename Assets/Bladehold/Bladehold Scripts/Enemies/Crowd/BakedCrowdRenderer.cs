using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
///     Ticks and draws every <see cref="BakedCrowdAgent" /> in the scene: one
///     <see cref="Graphics.RenderMeshInstanced" /> call per 1023 instances per baked rig, with each
///     instance's baked frame passed through the material property block. Created on demand by the
///     first agent that registers (no scene wiring) and lives with the scene, like the other scene
///     singletons; nothing else talks to it.
/// </summary>
public class BakedCrowdRenderer : MonoBehaviour
{
    private const int BatchSize = 1023;
    private static readonly int CrowdFrameId = Shader.PropertyToID("_CrowdFrame");

    public static BakedCrowdRenderer Instance { get; private set; }

    private class Batch
    {
        public readonly Matrix4x4[] matrices = new Matrix4x4[BatchSize];
        public readonly Vector4[] frames = new Vector4[BatchSize];
        public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    }

    private class Group
    {
        public BakedCrowdAnimationSO data;
        public readonly List<BakedCrowdAgent> agents = new List<BakedCrowdAgent>();
        public readonly List<Batch> batches = new List<Batch>();
    }

    private readonly Dictionary<BakedCrowdAnimationSO, Group> groups = new Dictionary<BakedCrowdAnimationSO, Group>();
    private readonly List<Group> groupList = new List<Group>();

    public static void Register(BakedCrowdAgent agent)
    {
        if (Instance == null)
        {
            new GameObject("BakedCrowdRenderer").AddComponent<BakedCrowdRenderer>();
        }
        Instance.Add(agent);
    }

    public static void Unregister(BakedCrowdAgent agent)
    {
        if (Instance != null)
        {
            Instance.Remove(agent);
        }
    }

    /// <summary>How many goblins are currently drawn baked (DevConsole / benchmarks).</summary>
    public static int BakedCount
    {
        get
        {
            if (Instance == null) return 0;
            int count = 0;
            foreach (Group group in Instance.groupList) count += group.agents.Count;
            return count;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Add(BakedCrowdAgent agent)
    {
        if (!groups.TryGetValue(agent.CrowdData, out Group group))
        {
            group = new Group { data = agent.CrowdData };
            groups.Add(agent.CrowdData, group);
            groupList.Add(group);
        }
        if (!group.agents.Contains(agent))
        {
            group.agents.Add(agent);
        }
    }

    private void Remove(BakedCrowdAgent agent)
    {
        if (agent.CrowdData != null && groups.TryGetValue(agent.CrowdData, out Group group))
        {
            group.agents.Remove(agent);
        }
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        foreach (Group group in groupList)
        {
            DrawGroup(group, deltaTime);
        }
    }

    private void DrawGroup(Group group, float deltaTime)
    {
        List<BakedCrowdAgent> agents = group.agents;

        // Tick backwards: an agent that promotes itself removes itself from the list mid-loop.
        int count = 0;
        Bounds bounds = default;
        int layer = 0;
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            BakedCrowdAgent agent = agents[i];
            if (agent == null)
            {
                agents.RemoveAt(i);
                continue;
            }
            if (!agent.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (!agent.Tick(deltaTime, out Vector4 frame))
            {
                continue;
            }

            int batchIndex = count / BatchSize;
            if (batchIndex >= group.batches.Count)
            {
                group.batches.Add(new Batch());
            }
            Batch batch = group.batches[batchIndex];
            Matrix4x4 matrix = agent.RenderMatrix;
            batch.matrices[count % BatchSize] = matrix;
            batch.frames[count % BatchSize] = frame;

            Vector3 position = matrix.GetPosition();
            if (count == 0)
            {
                bounds = new Bounds(position, Vector3.zero);
                layer = agent.Layer;
            }
            else
            {
                bounds.Encapsulate(position);
            }
            count++;
        }
        if (count == 0) return;

        // Instance origins sit at the feet; pad for body height, weapon reach and scale.
        bounds.Expand(new Vector3(4f, 6f, 4f));

        BakedCrowdAnimationSO data = group.data;
        for (int b = 0; b * BatchSize < count; b++)
        {
            Batch batch = group.batches[b];
            int instances = Mathf.Min(BatchSize, count - b * BatchSize);
            batch.properties.SetVectorArray(CrowdFrameId, batch.frames);
            RenderParams renderParams = new RenderParams(data.material)
            {
                layer = layer,
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = bounds,
                matProps = batch.properties,
                lightProbeUsage = LightProbeUsage.BlendProbes,
                motionVectorMode = MotionVectorGenerationMode.Camera,
            };
            Graphics.RenderMeshInstanced(renderParams, data.mesh, 0, batch.matrices, instances);
        }
    }
}
