using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     How hard a world-event hazard hits. Enemies take the larger of a flat value and a fraction of their
///     max health (so a meteor still matters to a wave-10 brute), capped so bosses and captains shrug it off.
/// </summary>
[Serializable]
public struct WorldHazardDamage
{
    [Tooltip("Damage to the player (base max health is 50).")]
    public float playerDamage;
    [Tooltip("Flat damage to each enemy.")]
    public float enemyDamage;
    [Tooltip("Extra: this fraction of the enemy's max health, if larger than the flat value.")]
    [Range(0f, 1f)] public float enemyMaxHealthFraction;
    [Tooltip("Upper bound on one hit's damage to an enemy (keeps captains and bosses from melting).")]
    public float enemyDamageCap;
    [Tooltip("Knockback force (KnockbackReceiver resistance scale: ~3 slides a goblin, ~6 flings it). 0 = none.")]
    public float knockbackForce;
}

/// <summary>
///     Shared plumbing for world-event hazards: area hits that land on the player <b>and</b> enemies (never
///     the gate, walls or towers), NavMesh point picking, and enemy-cluster targeting. Hazard damage carries
///     no source, so a kill pays gold and counts for objectives like a player kill (CoinDropper only skips
///     kills credited to another enemy).
/// </summary>
public static class WorldEventHazards
{
    private static readonly Collider[] overlapBuffer = new Collider[128];
    private static readonly HashSet<Health> hitThisCall = new HashSet<Health>();
    private static readonly List<Health> enemyScratch = new List<Health>();

    /// <summary>
    ///     Damages everything with a <see cref="Health" /> inside the sphere, once each. Enemies hit also get
    ///     <paramref name="statusId" /> ("Fire"/"Ice"/"Lightning") when given. <paramref name="alreadyHit" />, when
    ///     given, skips (and records) targets a moving hazard already hit. Returns how many were hit.
    /// </summary>
    public static int HitArea(Vector3 center, float radius, WorldHazardDamage damage, string statusId = null, Action<Health> onEnemyHit = null,
        HashSet<Health> alreadyHit = null)
    {
        hitThisCall.Clear();
        int hits = 0;
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Health health = overlapBuffer[i].GetComponentInParent<Health>();
            if (health == null || health.IsDead || !hitThisCall.Add(health)) continue;
            if (IsFortification(health)) continue;
            // A moving hazard (the stampede) passes over the same target for many frames: once per run.
            if (alreadyHit != null && !alreadyHit.Add(health)) continue;

            if (IsPlayer(health))
            {
                if (damage.playerDamage <= 0f) continue;
                health.ReceiveDamage(new Damage
                {
                    value = damage.playerDamage,
                    type = DamageType.elemental,
                    sourcePosition = center,
                    direction = Flat(health.transform.position - center),
                    unparryable = true,
                });
                hits++;
                continue;
            }

            HitEnemy(health, damage, center, statusId);
            onEnemyHit?.Invoke(health);
            hits++;
        }
        return hits;
    }

    /// <summary>One enemy hit with the event's damage rules (also used by lane hazards like the stampede).</summary>
    public static void HitEnemy(Health enemy, WorldHazardDamage damage, Vector3 sourcePosition, string statusId = null)
    {
        if (enemy == null || enemy.IsDead) return;
        float value = Mathf.Max(damage.enemyDamage, enemy.MaxHealth * damage.enemyMaxHealthFraction);
        if (damage.enemyDamageCap > 0f) value = Mathf.Min(value, damage.enemyDamageCap);

        enemy.ReceiveDamage(new Damage
        {
            value = value,
            type = DamageType.elemental,
            sourcePosition = sourcePosition,
            direction = Flat(enemy.transform.position - sourcePosition),
            knockbackForce = damage.knockbackForce,
            unparryable = true,
            piercesShields = true,
        });

        if (!string.IsNullOrEmpty(statusId) && !enemy.IsDead)
        {
            EnemyStatusManager status = EnemyStatusManager.GetOrAdd(enemy);
            if (status != null) status.ApplyStatus(statusId);
        }
    }

    public static bool IsPlayer(Health health) =>
        Player.Instance != null && health != null && health.transform.root == Player.Instance.transform.root;

    /// <summary>Gate, walls and towers are the player's; nature doesn't get to knock them down.</summary>
    public static bool IsFortification(Health health) =>
        health.GetComponentInParent<Gate>() != null ||
        health.GetComponentInParent<WallStructure>() != null ||
        health.GetComponentInParent<DefenseStructure>() != null;

    /// <summary>Alive enemies the spawner tracks (wave, debug and event spawns).</summary>
    public static List<Health> AliveEnemies()
    {
        enemyScratch.Clear();
        if (SurvivorsSpawner.Instance != null) SurvivorsSpawner.Instance.GetAliveEnemies(enemyScratch);
        return enemyScratch;
    }

    /// <summary>
    ///     A ground point for a strike: within <paramref name="maxRadius" /> of <paramref name="origin" />, snapped
    ///     to the NavMesh so circles land where things actually walk. Falls back to the origin.
    /// </summary>
    public static Vector3 RandomGroundPointNear(Vector3 origin, float minRadius, float maxRadius)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            float dist = UnityEngine.Random.Range(minRadius, maxRadius);
            Vector3 candidate = origin + new Vector3(dir.x, 0f, dir.y) * dist;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas)) return hit.position;
        }
        return NavMesh.SamplePosition(origin, out NavMeshHit fallback, 4f, NavMesh.AllAreas) ? fallback.position : origin;
    }

    /// <summary>
    ///     A point on a random alive enemy within <paramref name="range" /> of <paramref name="around" />, preferring
    ///     enemies with company (a strike there catches several). False when no enemy is in range.
    /// </summary>
    public static bool TryPickEnemyCluster(Vector3 around, float range, float clusterRadius, out Vector3 point)
    {
        point = around;
        List<Health> enemies = AliveEnemies();
        if (enemies.Count == 0) return false;

        float rangeSqr = range * range;
        float clusterSqr = clusterRadius * clusterRadius;
        Health best = null;
        int bestCount = -1;
        // Sample a handful rather than scoring every pair: hordes run to hundreds.
        for (int s = 0; s < 6; s++)
        {
            Health candidate = enemies[UnityEngine.Random.Range(0, enemies.Count)];
            if (candidate == null || (candidate.transform.position - around).sqrMagnitude > rangeSqr) continue;
            int neighbours = 0;
            Vector3 c = candidate.transform.position;
            for (int i = 0; i < enemies.Count && neighbours < 12; i++)
            {
                if (enemies[i] != null && (enemies[i].transform.position - c).sqrMagnitude <= clusterSqr) neighbours++;
            }
            if (neighbours > bestCount)
            {
                bestCount = neighbours;
                best = candidate;
            }
        }
        if (best == null) return false;
        point = best.transform.position;
        return true;
    }

    /// <summary>Player position, or the director's own position when there's no player.</summary>
    public static Vector3 PlayerPosition(Vector3 fallback) =>
        Player.Instance != null ? Player.Instance.transform.position : fallback;

    /// <summary>
    ///     Spawns a ground telegraph (a gameplay marker, not feedback) scaled to a circle of <paramref name="radius" />.
    ///     The caller destroys it when the strike lands.
    /// </summary>
    public static GameObject SpawnCircleTelegraph(GameObject prefab, Vector3 point, float radius)
    {
        if (prefab == null) return null;
        GameObject marker = UnityEngine.Object.Instantiate(prefab, point + Vector3.up * 0.05f, Quaternion.identity);
        Vector3 s = marker.transform.localScale;
        marker.transform.localScale = new Vector3(radius * 2f, s.y, radius * 2f);
        return marker;
    }

    /// <summary>A lane telegraph: <paramref name="prefab" /> is a unit-square marker scaled to width × length along dir.</summary>
    public static GameObject SpawnLaneTelegraph(GameObject prefab, Vector3 start, Vector3 dir, float width, float length)
    {
        if (prefab == null) return null;
        Vector3 mid = start + dir * (length * 0.5f);
        GameObject marker = UnityEngine.Object.Instantiate(prefab, mid + Vector3.up * 0.05f, Quaternion.LookRotation(dir, Vector3.up));
        Vector3 s = marker.transform.localScale;
        marker.transform.localScale = new Vector3(width, s.y, length);
        return marker;
    }

    public static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
