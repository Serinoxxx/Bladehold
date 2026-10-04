using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Thunderstorm (enchanted forest / graveyard): rain, a darkened grade, and lightning bolts on blue circles.
///     Bolts hit the player and enemies, shock the enemies they catch and arc on to a few more nearby. The
///     player gets <see cref="StatType.LightningDamageBonus" /> while it lasts (the asset's buff).
/// </summary>
public class ThunderstormEvent : WorldEvent
{
    [SerializeField] private ThunderstormEventSO config;

    [Header("Prefabs (gameplay spawns)")]
    [Tooltip("Blue ground circle, scaled to the strike diameter.")]
    [SerializeField] private GameObject telegraphPrefab;

    [Header("Feedback")]
    [Tooltip("The bolt: lightning VFX, thunder crack, sky flash, small shake. Played at the strike point.")]
    [SerializeField] private MMF_Player strikeFeedback;
    [Tooltip("Optional: the arc zap, played on each enemy an arc reaches.")]
    [SerializeField] private MMF_Player arcFeedback;

    private readonly List<GameObject> liveMarkers = new List<GameObject>();
    private readonly List<Health> arcCandidates = new List<Health>();
    private float strikeTimer;

    public override WorldEventSO Config => config;

    protected override void ValidateEvent()
    {
        if (telegraphPrefab == null) MarkInvalid("telegraphPrefab is not assigned.");
        if (strikeFeedback == null) Debug.LogError($"[ThunderstormEvent] {name}: strikeFeedback is not assigned.", this);
    }

    protected override void OnHazardsStarted()
    {
        strikeTimer = 0f;
    }

    protected override void TickHazards(float deltaTime)
    {
        strikeTimer -= deltaTime;
        if (strikeTimer > 0f) return;
        strikeTimer = Random.Range(config.strikeInterval.x, config.strikeInterval.y);
        StartCoroutine(Bolt(PickTarget()));
    }

    private Vector3 PickTarget()
    {
        Vector3 player = WorldEventHazards.PlayerPosition(transform.position);
        float roll = Random.value;
        if (roll < config.targetPlayerChance) return WorldEventHazards.RandomGroundPointNear(player, 0f, 1.5f);
        if (roll < config.targetPlayerChance + config.targetEnemyChance &&
            WorldEventHazards.TryPickEnemyCluster(player, config.scatterRadius * 1.5f, 4f, out Vector3 pack))
        {
            return pack;
        }
        return WorldEventHazards.RandomGroundPointNear(player, 4f, config.scatterRadius);
    }

    private IEnumerator Bolt(Vector3 point)
    {
        GameObject marker = WorldEventHazards.SpawnCircleTelegraph(telegraphPrefab, point, config.strikeRadius);
        liveMarkers.Add(marker);
        yield return new WaitForSeconds(config.telegraphSeconds);
        liveMarkers.Remove(marker);
        if (marker != null) Destroy(marker);

        if (strikeFeedback != null) strikeFeedback.PlayFeedbacks(point);
        arcCandidates.Clear();
        WorldEventHazards.HitArea(point, config.strikeRadius, config.strikeDamage, "Lightning", h => arcCandidates.Add(h));
        Arc(point);
    }

    /// <summary>Arcs to the nearest few enemies outside the strike that the bolt didn't already hit.</summary>
    private void Arc(Vector3 point)
    {
        if (config.arcTargets <= 0) return;
        WorldHazardDamage arcDamage = config.strikeDamage;
        arcDamage.enemyDamage *= config.arcDamageFraction;
        arcDamage.enemyMaxHealthFraction *= config.arcDamageFraction;
        arcDamage.knockbackForce = 0f;

        float rangeSqr = config.arcRange * config.arcRange;
        int arced = 0;
        foreach (Health enemy in WorldEventHazards.AliveEnemies())
        {
            if (arced >= config.arcTargets) break;
            if (enemy == null || enemy.IsDead || arcCandidates.Contains(enemy)) continue;
            if ((enemy.transform.position - point).sqrMagnitude > rangeSqr) continue;
            WorldEventHazards.HitEnemy(enemy, arcDamage, point, "Lightning");
            if (arcFeedback != null) arcFeedback.PlayFeedbacks(enemy.transform.position);
            arced++;
        }
    }

    protected override void OnEventEnded()
    {
        StopAllCoroutines();
        foreach (GameObject m in liveMarkers)
        {
            if (m != null) Destroy(m);
        }
        liveMarkers.Clear();
    }
}
