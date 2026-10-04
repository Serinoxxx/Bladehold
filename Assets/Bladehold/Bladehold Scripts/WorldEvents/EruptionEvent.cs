using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Volcano Eruption (fire biome): telegraphed meteors rain on the battlefield, hitting the player and
///     enemies alike and igniting the enemies they catch; some leave a magma pool. The player gets
///     <see cref="StatType.FireDamageBonus" /> while it lasts (set on the asset's buff). Meteors split
///     between the player's spot, enemy packs and random ground, so kiting a pack under a red circle pays.
/// </summary>
public class EruptionEvent : WorldEvent
{
    [SerializeField] private EruptionEventSO config;

    [Header("Prefabs (gameplay spawns)")]
    [Tooltip("Red ground circle, scaled to the impact diameter (SlamTelegraph).")]
    [SerializeField] private GameObject telegraphPrefab;
    [Tooltip("The falling fireball visual, moved from the sky to the impact point.")]
    [SerializeField] private GameObject meteorPrefab;
    [Tooltip("Optional: the magma pool (WorldHazardZone). Leave empty for no pools.")]
    [SerializeField] private WorldHazardZone magmaPoolPrefab;
    [Tooltip("Where the meteor starts, relative to the impact point (high and behind, so it reads as falling at an angle).")]
    [SerializeField] private Vector3 meteorSpawnOffset = new Vector3(-8f, 30f, 6f);

    [Header("Feedback")]
    [Tooltip("Impact boom + explosion + screenshake, played at the impact point.")]
    [SerializeField] private MMF_Player impactFeedback;
    [Tooltip("Optional: incoming whistle at the circle when it appears.")]
    [SerializeField] private MMF_Player incomingFeedback;

    private readonly List<GameObject> liveObjects = new List<GameObject>();
    private float volleyTimer;

    public override WorldEventSO Config => config;

    protected override void ValidateEvent()
    {
        if (telegraphPrefab == null) MarkInvalid("telegraphPrefab is not assigned.");
        if (meteorPrefab == null) MarkInvalid("meteorPrefab is not assigned.");
        if (impactFeedback == null) Debug.LogError($"[EruptionEvent] {name}: impactFeedback is not assigned.", this);
    }

    protected override void OnHazardsStarted()
    {
        volleyTimer = 0f;
    }

    protected override void TickHazards(float deltaTime)
    {
        volleyTimer -= deltaTime;
        if (volleyTimer > 0f) return;
        volleyTimer = Random.Range(config.volleyInterval.x, config.volleyInterval.y);

        int count = Random.Range(config.meteorsPerVolley.x, config.meteorsPerVolley.y + 1);
        Vector3 player = WorldEventHazards.PlayerPosition(transform.position);
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(Meteor(PickTarget(player, i), i * 0.25f));
        }
    }

    private Vector3 PickTarget(Vector3 player, int indexInVolley)
    {
        float roll = Random.value;
        // One meteor per volley at most goes straight for the player; doubling up feels unfair.
        if (indexInVolley == 0 && roll < config.targetPlayerChance)
        {
            return WorldEventHazards.RandomGroundPointNear(player, 0f, 1.5f);
        }
        if (roll < config.targetPlayerChance + config.targetEnemyChance &&
            WorldEventHazards.TryPickEnemyCluster(player, config.scatterRadius * 1.5f, 4f, out Vector3 pack))
        {
            return pack;
        }
        return WorldEventHazards.RandomGroundPointNear(player, 4f, config.scatterRadius);
    }

    private IEnumerator Meteor(Vector3 point, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        GameObject marker = WorldEventHazards.SpawnCircleTelegraph(telegraphPrefab, point, config.impactRadius);
        liveObjects.Add(marker);
        if (incomingFeedback != null) incomingFeedback.PlayFeedbacks(point);

        float fall = Mathf.Min(config.fallSeconds, config.telegraphSeconds);
        yield return new WaitForSeconds(config.telegraphSeconds - fall);

        Vector3 from = point + meteorSpawnOffset;
        GameObject meteor = Instantiate(meteorPrefab, from, Quaternion.LookRotation(point - from));
        liveObjects.Add(meteor);
        for (float t = 0f; t < fall; t += Time.deltaTime)
        {
            if (meteor == null) break;
            meteor.transform.position = Vector3.Lerp(from, point, t / fall);
            yield return null;
        }

        Remove(marker);
        Remove(meteor);

        if (impactFeedback != null) impactFeedback.PlayFeedbacks(point);
        WorldEventHazards.HitArea(point, config.impactRadius, config.impactDamage, "Fire");

        if (magmaPoolPrefab != null && Random.value < config.poolChance)
        {
            WorldHazardZone pool = Instantiate(magmaPoolPrefab, point, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            pool.Initialize(config.poolRadius, config.poolSeconds, config.poolTickDamage, "Fire");
        }
    }

    private void Remove(GameObject go)
    {
        if (go == null) return;
        liveObjects.Remove(go);
        Destroy(go);
    }

    protected override void OnEventEnded()
    {
        // Meteors still in the air when the wave ends never land.
        StopAllCoroutines();
        foreach (GameObject go in liveObjects)
        {
            if (go != null) Destroy(go);
        }
        liveObjects.Clear();
    }
}
