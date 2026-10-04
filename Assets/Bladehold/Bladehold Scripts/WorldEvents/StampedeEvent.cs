using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Stampede (green / kingdom biome): the ground trembles, then a wild herd charges down a marked lane,
///     trampling the player and enemies in its path and flinging the enemies. Lanes go through the player's
///     spot or an enemy pack, are checked against the NavMesh (never through walls or over ravines), and are
///     telegraphed with a long lane marker plus dust and rumble at the end the herd comes from. The player
///     gets a move-speed buff while it lasts (the asset's buff), for dodging lanes and luring packs into them.
/// </summary>
public class StampedeEvent : WorldEvent
{
    [SerializeField] private StampedeEventSO config;

    [Header("Prefabs (gameplay spawns)")]
    [Tooltip("Unit-square lane marker scaled to width × length (ChargeTelegraph).")]
    [SerializeField] private GameObject laneTelegraphPrefab;
    [Tooltip("One galloping animal (model + in-place gallop loop + hoof audio). Faces +Z.")]
    [SerializeField] private GameObject runnerPrefab;
    [Tooltip("Layers the herd stands on (terrain, ground meshes). Characters must be excluded.")]
    [SerializeField] private LayerMask groundLayers = 1;

    [Header("Feedback")]
    [Tooltip("Rumble, dust plume and a light shake at the lane's start, played when the lane appears.")]
    [SerializeField] private MMF_Player laneWarningFeedback;
    [Tooltip("Optional: a dull thud at each enemy or player the herd tramples.")]
    [SerializeField] private MMF_Player trampleFeedback;

    private readonly List<GameObject> liveObjects = new List<GameObject>();
    private readonly HashSet<Health> trampledThisRun = new HashSet<Health>();
    private float runTimer;

    public override WorldEventSO Config => config;

    protected override void ValidateEvent()
    {
        if (laneTelegraphPrefab == null) MarkInvalid("laneTelegraphPrefab is not assigned.");
        if (runnerPrefab == null) MarkInvalid("runnerPrefab is not assigned.");
        if (laneWarningFeedback == null) Debug.LogError($"[StampedeEvent] {name}: laneWarningFeedback is not assigned.", this);
    }

    protected override void OnHazardsStarted()
    {
        runTimer = 0f;
    }

    protected override void TickHazards(float deltaTime)
    {
        runTimer -= deltaTime;
        if (runTimer > 0f) return;
        runTimer = Random.Range(config.runInterval.x, config.runInterval.y);

        if (TryPlanLane(out Vector3 start, out Vector3 dir, out float length))
        {
            StartCoroutine(Run(start, dir, length));
        }
    }

    /// <summary>A straight lane through a target point, clipped to unbroken NavMesh in both directions.</summary>
    private bool TryPlanLane(out Vector3 start, out Vector3 dir, out float length)
    {
        Vector3 player = WorldEventHazards.PlayerPosition(transform.position);
        Vector3 through = player;
        if (Random.value >= config.targetPlayerChance &&
            WorldEventHazards.TryPickEnemyCluster(player, 35f, 5f, out Vector3 pack))
        {
            through = pack;
        }
        if (NavMesh.SamplePosition(through, out NavMeshHit onMesh, 3f, NavMesh.AllAreas)) through = onMesh.position;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            float half = config.laneLength * 0.5f;
            Vector3 back = Clip(through, -dir, half);
            Vector3 front = Clip(through, dir, half);
            length = Vector3.Distance(Flat(back), Flat(front));
            if (length < config.minLaneLength) continue;
            start = back;
            return true;
        }
        start = through;
        dir = Vector3.forward;
        length = 0f;
        return false;
    }

    private static Vector3 Clip(Vector3 from, Vector3 dir, float distance)
    {
        Vector3 to = from + dir * distance;
        return NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas) ? hit.position : to;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private IEnumerator Run(Vector3 start, Vector3 dir, float length)
    {
        GameObject lane = WorldEventHazards.SpawnLaneTelegraph(laneTelegraphPrefab, start, dir, config.laneWidth, length);
        liveObjects.Add(lane);
        if (laneWarningFeedback != null) laneWarningFeedback.PlayFeedbacks(start);

        // The herd starts off the back of the lane so it's already at full gallop when it reaches it.
        float runUp = config.herdSpeed * 0.6f;
        Vector3 origin = start - dir * runUp;
        int size = Random.Range(config.herdSize.x, config.herdSize.y + 1);
        var runners = new List<(Transform t, float lateral, float lag)>();

        yield return new WaitForSeconds(Mathf.Max(0f, config.telegraphSeconds - runUp / config.herdSpeed));

        Quaternion facing = Quaternion.LookRotation(dir, Vector3.up);
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        for (int i = 0; i < size; i++)
        {
            float lateral = Random.Range(-0.4f, 0.4f) * config.laneWidth;
            float lag = Random.Range(0f, config.herdDepth);
            GameObject runner = Instantiate(runnerPrefab, Ground(origin - dir * lag + right * lateral), facing);
            liveObjects.Add(runner);
            runners.Add((runner.transform, lateral, lag));
        }

        trampledThisRun.Clear();
        float travelled = 0f;
        float total = runUp + length + config.herdDepth + 4f;
        while (travelled < total)
        {
            travelled += config.herdSpeed * Time.deltaTime;
            foreach ((Transform t, float lateral, float lag) in runners)
            {
                if (t == null) continue;
                t.position = Ground(origin + dir * (travelled - lag) + right * lateral);
            }

            // Only the stretch of the front rank that's on the lane tramples.
            float front = travelled - runUp;
            if (front > 0f && front < length + config.herdDepth)
            {
                Vector3 frontPoint = start + dir * Mathf.Min(front, length);
                HitFront(frontPoint);
            }
            if (lane != null && front > length + config.herdDepth) Remove(lane);
            yield return null;
        }

        Remove(lane);
        foreach ((Transform t, float _, float __) in runners)
        {
            if (t != null) Remove(t.gameObject);
        }
    }

    private void HitFront(Vector3 frontPoint)
    {
        // Knockback pushes out from the front's centre line, so trampled enemies are flung out of the lane.
        WorldEventHazards.HitArea(frontPoint + Vector3.up * 0.5f, config.laneWidth * 0.5f, config.trampleDamage, null,
            enemy =>
            {
                if (trampleFeedback != null) trampleFeedback.PlayFeedbacks(enemy.transform.position);
            },
            trampledThisRun);
    }

    private Vector3 Ground(Vector3 p)
    {
        Vector3 top = new Vector3(p.x, p.y + 20f, p.z);
        return Physics.Raycast(top, Vector3.down, out RaycastHit hit, 60f, groundLayers, QueryTriggerInteraction.Ignore)
            ? hit.point
            : p;
    }

    private void Remove(GameObject go)
    {
        if (go == null) return;
        liveObjects.Remove(go);
        Destroy(go);
    }

    protected override void OnEventEnded()
    {
        // The event ends with the wave: herds and lanes still out are called off.
        StopAllCoroutines();
        foreach (GameObject go in liveObjects)
        {
            if (go != null) Destroy(go);
        }
        liveObjects.Clear();
    }
}
