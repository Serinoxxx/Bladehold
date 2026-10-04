using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Blood Moon (graveyard biome): a red grade over the field, and the dead don't stay down. An enemy killed
///     near the player may rise again: a grave glow marks its corpse, then a frail skeleton climbs out where it
///     fell. In exchange the player gets <see cref="StatType.LifeStealPercent" /> while it lasts (the asset's
///     buff), so "keep killing" is the answer to both sides. Risen skeletons count as wave enemies: their kills
///     pay, and the wave can't end while one stands. They never rise twice, and big enemies never rise.
/// </summary>
public class BloodMoonEvent : WorldEvent
{
    [SerializeField] private BloodMoonEventSO config;

    [Header("Prefabs (gameplay spawns)")]
    [Tooltip("Purple grave-glow marker on a corpse about to rise, scaled to markerRadius.")]
    [SerializeField] private GameObject riseMarkerPrefab;

    [Header("Feedback")]
    [Tooltip("Optional: a low groan and wisps when a corpse starts to stir.")]
    [SerializeField] private MMF_Player stirFeedback;
    [Tooltip("The skeleton bursting out: bone burst VFX and a rattle, at the corpse.")]
    [SerializeField] private MMF_Player riseFeedback;

    private readonly HashSet<Health> risen = new HashSet<Health>();
    private readonly List<GameObject> liveMarkers = new List<GameObject>();
    private GameLoopManager gameLoop;
    private int pendingRises;

    public override WorldEventSO Config => config;

    protected override void ValidateEvent()
    {
        if (riseMarkerPrefab == null) MarkInvalid("riseMarkerPrefab is not assigned.");
        if (riseFeedback == null) Debug.LogError($"[BloodMoonEvent] {name}: riseFeedback is not assigned.", this);
        if (config != null && (config.risenEnemyIds == null || config.risenEnemyIds.Length == 0))
        {
            MarkInvalid("the config lists no risenEnemyIds.");
        }
    }

    protected override void OnEventBegan()
    {
        risen.Clear();
        pendingRises = 0;
        gameLoop = GameLoopManager.Instance;
        if (gameLoop != null) gameLoop.OnEnemyKilledEvent += HandleEnemyKilled;
    }

    protected override void TickHazards(float deltaTime)
    {
        // Rises are driven by kills (HandleEnemyKilled), not a timer.
    }

    private void HandleEnemyKilled(Health enemy)
    {
        if (!IsRunning || enemy == null) return;
        if (risen.Remove(enemy)) return; // A risen skeleton going back down stays down.
        if (enemy.MaxHealth > config.maxRisableHealth) return;
        if (risen.Count + pendingRises >= config.maxRisenAlive) return;
        if (Random.value > config.riseChance) return;

        Vector3 player = WorldEventHazards.PlayerPosition(transform.position);
        Vector3 at = enemy.transform.position;
        if ((at - player).sqrMagnitude > config.riseRadiusFromPlayer * config.riseRadiusFromPlayer) return;

        StartCoroutine(Rise(enemy, at));
    }

    private IEnumerator Rise(Health corpse, Vector3 at)
    {
        pendingRises++;
        GameObject marker = WorldEventHazards.SpawnCircleTelegraph(riseMarkerPrefab, at, config.markerRadius);
        liveMarkers.Add(marker);
        if (stirFeedback != null) stirFeedback.PlayFeedbacks(at);

        yield return new WaitForSeconds(config.riseDelaySeconds);

        pendingRises--;
        liveMarkers.Remove(marker);
        if (marker != null) Destroy(marker);
        if (!IsRunning || SurvivorsSpawner.Instance == null) yield break;

        // The corpse sinks as the skeleton climbs out, so the body visibly "becomes" it.
        if (corpse != null && corpse.TryGetComponent(out CorpseDespawner despawner)) despawner.DespawnNow();
        if (riseFeedback != null) riseFeedback.PlayFeedbacks(at);

        string id = config.risenEnemyIds[Random.Range(0, config.risenEnemyIds.Length)];
        GameObject skeleton = SurvivorsSpawner.Instance.SpawnEnemyAt(id, at);
        if (skeleton == null) yield break;
        Health health = skeleton.GetComponent<Health>();
        if (health == null) yield break;
        health.ScaleMaxHealth(config.risenHealthMultiplier);
        risen.Add(health);
    }

    protected override void OnEventEnded()
    {
        if (gameLoop != null) gameLoop.OnEnemyKilledEvent -= HandleEnemyKilled;
        StopAllCoroutines();
        pendingRises = 0;
        foreach (GameObject m in liveMarkers)
        {
            if (m != null) Destroy(m);
        }
        liveMarkers.Clear();
        // Skeletons already up stay up: they're wave enemies now.
        risen.Clear();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (gameLoop != null) gameLoop.OnEnemyKilledEvent -= HandleEnemyKilled;
    }
}
