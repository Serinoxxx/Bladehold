using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Skull waypoints on enemies, fed to <see cref="ObjectiveWaypointTrackerUI" /> as an
///     <see cref="IWaypointSource" />. Lives on the HUD beside the tracker.
///     <list type="bullet">
///         <item>While a wave is under way: one skull per <b>group</b> of enemies (members within
///             <see cref="clusterRadius" /> of each other), at the group's centre, so the player can see
///             where the horde is coming from without a marker on every goblin. Groups already on top of the
///             player are skipped.</item>
///         <item>During the rout (<see cref="GameLoopManager.IsRouting" />): one skull on every fleeing
///             straggler, so they can be hunted down before they escape.</item>
///     </list>
/// </summary>
public class EnemySkullWaypoints : MonoBehaviour, IWaypointSource
{
    [Header("Groups (wave under way)")]
    [Tooltip("Enemies within this distance (m) of any member of a group join it.")]
    [Min(1f)] [SerializeField] private float clusterRadius = 20f;
    [Tooltip("Groups with a member this close (m) to the player get no skull: they're already in the fight.")]
    [Min(0f)] [SerializeField] private float hideWithinPlayerDistance = 12f;
    [Tooltip("Seconds between regrouping passes.")]
    [Min(0.05f)] [SerializeField] private float regroupInterval = 0.3f;
    [Tooltip("How fast a group's skull glides to its new centre (higher = snappier).")]
    [Min(0.1f)] [SerializeField] private float anchorFollowSpeed = 6f;
    [SerializeField] private Vector3 groupOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private Color groupTint = new Color(1f, 0.55f, 0.25f, 1f);

    [Header("Stragglers (rout)")]
    [SerializeField] private Vector3 stragglerOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField] private Color stragglerTint = new Color(1f, 0.25f, 0.25f, 1f);

    private sealed class Group
    {
        public Transform anchor;
        public Vector3 target;
        public bool visible;
    }

    private readonly List<Health> enemies = new List<Health>();
    private readonly List<Group> groups = new List<Group>();
    private readonly List<Vector3> centres = new List<Vector3>();
    private readonly List<bool> centreVisible = new List<bool>();
    private readonly List<int> clusterOf = new List<int>();
    private readonly Stack<int> frontier = new Stack<int>();
    private float nextRegroup;

    private void OnEnable() => ObjectiveWaypointTrackerUI.RegisterSource(this);

    private void OnDisable() => ObjectiveWaypointTrackerUI.UnregisterSource(this);

    private void OnDestroy()
    {
        foreach (Group g in groups)
        {
            if (g.anchor != null) Destroy(g.anchor.gameObject);
        }
    }

    private void Update()
    {
        GameLoopManager loop = GameLoopManager.Instance;
        bool grouping = loop != null && loop.IsWaveActive && !loop.IsRouting && SurvivorsSpawner.Instance != null;
        if (!grouping)
        {
            foreach (Group g in groups) g.visible = false;
            return;
        }

        if (Time.time >= nextRegroup)
        {
            nextRegroup = Time.time + regroupInterval;
            Regroup();
        }

        float k = 1f - Mathf.Exp(-anchorFollowSpeed * Time.deltaTime);
        foreach (Group g in groups)
        {
            if (g.visible) g.anchor.position = Vector3.Lerp(g.anchor.position, g.target, k);
        }
    }

    public void GetWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        ObjectiveWaypointTrackerUI tracker = ObjectiveWaypointTrackerUI.Instance;
        Sprite skull = tracker != null ? tracker.CleanupEnemySkullIcon : null;
        GameLoopManager loop = GameLoopManager.Instance;
        if (loop == null) return;

        if (loop.IsRouting)
        {
            foreach (Health h in loop.RoutStragglers)
            {
                if (h != null && !h.IsDead) results.Add(new ObjectiveWaypointTarget(h.transform, stragglerOffset, skull, stragglerTint));
            }
            return;
        }

        foreach (Group g in groups)
        {
            if (g.visible) results.Add(new ObjectiveWaypointTarget(g.anchor, groupOffset, skull, groupTint));
        }
    }

    // Single-linkage grouping (an enemy joins a group if it's within clusterRadius of any member), then each
    // new centre is matched to the nearest existing skull so markers glide instead of swapping around.
    private void Regroup()
    {
        SurvivorsSpawner.Instance.GetAliveEnemies(enemies);
        Vector3 playerPos = Player.Instance != null ? Player.Instance.transform.position : new Vector3(float.MaxValue, 0f, 0f);
        float radiusSq = clusterRadius * clusterRadius;
        float hideSq = hideWithinPlayerDistance * hideWithinPlayerDistance;

        centres.Clear();
        centreVisible.Clear();
        clusterOf.Clear();
        for (int i = 0; i < enemies.Count; i++) clusterOf.Add(-1);

        for (int seed = 0; seed < enemies.Count; seed++)
        {
            if (clusterOf[seed] >= 0) continue;
            int id = centres.Count;
            Vector3 sum = Vector3.zero;
            int count = 0;
            bool nearPlayer = false;

            clusterOf[seed] = id;
            frontier.Push(seed);
            while (frontier.Count > 0)
            {
                int i = frontier.Pop();
                Vector3 p = enemies[i].transform.position;
                sum += p;
                count++;
                if ((p - playerPos).sqrMagnitude < hideSq) nearPlayer = true;

                for (int j = 0; j < enemies.Count; j++)
                {
                    if (clusterOf[j] >= 0) continue;
                    if ((enemies[j].transform.position - p).sqrMagnitude > radiusSq) continue;
                    clusterOf[j] = id;
                    frontier.Push(j);
                }
            }
            centres.Add(sum / count);
            centreVisible.Add(!nearPlayer);
        }

        foreach (Group g in groups) g.visible = false;
        for (int c = 0; c < centres.Count; c++)
        {
            if (!centreVisible[c]) continue;
            Group best = null;
            float bestSq = radiusSq * 4f;
            foreach (Group g in groups)
            {
                if (g.visible) continue;
                float d = (g.anchor.position - centres[c]).sqrMagnitude;
                if (d < bestSq)
                {
                    bestSq = d;
                    best = g;
                }
            }
            if (best == null)
            {
                best = FindOrCreateFreeGroup();
                best.anchor.position = centres[c];
            }
            best.target = centres[c];
            best.visible = true;
        }
    }

    private Group FindOrCreateFreeGroup()
    {
        foreach (Group g in groups)
        {
            if (!g.visible) return g;
        }
        // An invisible marker anchor (a bare transform the tracker can follow), not a visual.
        Group created = new Group { anchor = new GameObject("EnemyGroupWaypointAnchor").transform };
        groups.Add(created);
        return created;
    }
}
