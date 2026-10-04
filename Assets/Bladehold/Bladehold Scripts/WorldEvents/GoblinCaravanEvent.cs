using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Goblin Caravan (any biome, the good-news event): a gold wagon with a goblin escort crosses the field,
///     passing near the player, until the timer runs out and it leaves with the gold. Hitting it spills gold
///     bags; wrecking it bursts a pile of them and ends the event early. Each leg is planned on the NavMesh from
///     one side of the player to the other; when a leg ends with time left, the next leg doubles back past the
///     player, so the wagon is always somewhere it can be chased.
/// </summary>
public class GoblinCaravanEvent : WorldEvent
{
    [SerializeField] private GoblinCaravanEventSO config;

    [Header("Prefabs (gameplay spawns)")]
    [SerializeField] private CaravanWagon wagonPrefab;

    [Header("Feedback")]
    [Tooltip("Optional: the caravan's arrival (horn, rattling wheels) at its entry point.")]
    [SerializeField] private MMF_Player arrivalFeedback;

    private CaravanWagon wagon;
    private string outcome;
    private bool outcomeGood;

    public override WorldEventSO Config => config;
    public override string OutcomeText => outcome;
    public override bool OutcomeIsGood => outcomeGood;

    protected override void ValidateEvent()
    {
        if (wagonPrefab == null) MarkInvalid("wagonPrefab is not assigned.");
    }

    protected override void OnEventBegan()
    {
        outcome = null;
        outcomeGood = false;
    }

    protected override void OnHazardsStarted()
    {
        Vector3 player = WorldEventHazards.PlayerPosition(transform.position);
        Vector3 fromDir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        if (!TryPlanLeg(player, fromDir, out Vector3 entry, out Vector3 exit))
        {
            Debug.LogWarning("[GoblinCaravanEvent] No NavMesh route across the field; the caravan didn't come.", this);
            outcome = null;
            Stop();
            return;
        }

        wagon = Instantiate(wagonPrefab, entry, Quaternion.LookRotation(WorldEventHazards.Flat(exit - entry)));
        wagon.Wrecked += HandleWrecked;
        wagon.Escaped += HandleEscaped;
        wagon.LegFinished += HandleLegFinished;
        wagon.Launch(config, exit);
        if (arrivalFeedback != null) arrivalFeedback.PlayFeedbacks(entry);

        SpawnEscort(entry, exit);
    }

    /// <summary>
    ///     A leg from <paramref name="fromDir" />'s side of <paramref name="player" /> to the opposite side, both ends
    ///     on the NavMesh with a complete, fairly direct path between. Fans out to nearby angles before giving up.
    /// </summary>
    private bool TryPlanLeg(Vector3 player, Vector3 fromDir, out Vector3 entry, out Vector3 exit)
    {
        NavMeshPath path = new NavMeshPath();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-15f, 15f) * (1 + attempt), 0f) * fromDir;
            Vector3 a = player + dir * Random.Range(config.routeRadius.x, config.routeRadius.y);
            Vector3 b = player - dir * Random.Range(config.routeRadius.x, config.routeRadius.y);
            if (!NavMesh.SamplePosition(a, out NavMeshHit ha, 6f, NavMesh.AllAreas)) continue;
            if (!NavMesh.SamplePosition(b, out NavMeshHit hb, 6f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            // Reject routes that wind all over the map: the wagon should cross the field, not tour it.
            if (length > Vector3.Distance(ha.position, hb.position) * 1.8f) continue;

            entry = ha.position;
            exit = hb.position;
            return true;
        }
        entry = exit = player;
        return false;
    }

    /// <summary>End of a leg with time left: double back past the player from where the wagon is now.</summary>
    private void HandleLegFinished()
    {
        if (wagon == null) return;
        Vector3 player = WorldEventHazards.PlayerPosition(transform.position);
        Vector3 fromDir = WorldEventHazards.Flat(wagon.transform.position - player);
        if (TryPlanLeg(player, fromDir, out _, out Vector3 exit)) wagon.DriveTo(exit);
        else wagon.DriveTo(WorldEventHazards.RandomGroundPointNear(player, config.routeRadius.x * 0.5f, config.routeRadius.y));
    }

    private void SpawnEscort(Vector3 entry, Vector3 exit)
    {
        if (SurvivorsSpawner.Instance == null || config.escortIds == null) return;
        Vector3 forward = WorldEventHazards.Flat(exit - entry);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int i = 0; i < config.escortIds.Length; i++)
        {
            float side = (i % 2 == 0 ? 1f : -1f) * (2.5f + i * 0.5f);
            Vector3 at = entry + right * side - forward * (1.5f + i);
            SurvivorsSpawner.Instance.SpawnEnemyAt(config.escortIds[i], at);
        }
    }

    protected override void TickHazards(float deltaTime)
    {
        // The wagon drives itself; nothing to tick.
    }

    private void HandleWrecked()
    {
        outcome = Loc.Get("world_event.caravan.outcome.wrecked", "Caravan looted!");
        outcomeGood = true;
        Detach();
        Stop();
    }

    private void HandleEscaped()
    {
        outcome = Loc.Get("world_event.caravan.outcome.escaped", "The caravan got away");
        outcomeGood = false;
        Detach();
        Stop();
    }

    private void Detach()
    {
        if (wagon == null) return;
        wagon.Wrecked -= HandleWrecked;
        wagon.Escaped -= HandleEscaped;
        wagon.LegFinished -= HandleLegFinished;
        wagon = null;
    }

    protected override void OnEventEnded()
    {
        // Timer ran out (or the wave ended) with the wagon still rolling: it gets away.
        if (wagon != null)
        {
            CaravanWagon leaving = wagon;
            leaving.Escape();
        }
    }
}
