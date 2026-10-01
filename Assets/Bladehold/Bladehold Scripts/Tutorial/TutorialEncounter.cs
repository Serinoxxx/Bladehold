using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     A fixed, one-shot enemy spawn for the tutorial arena: <see cref="countPerPoint" /> enemies of <see cref="enemyId" /> per spawn
///     point (in staggered rounds), built the same way <see cref="SurvivorsSpawner" /> builds them (prefab from the
///     <see cref="EnemyPrefabMapSO" />, stats from the <see cref="EnemyRosterSO" /> row via
///     <see cref="EnemyDefinitionApplier" />). Deliberately not a SurvivorsSpawner: that one auto-starts a
///     real wave when no GameLoopManager is present. With no Gate or objective in the scene the enemies'
///     <see cref="AITargetSelector" /> falls through to the player, so they rush straight in.
/// </summary>
public class TutorialEncounter : MonoBehaviour
{
    [SerializeField] private EnemyRosterSO roster;
    [SerializeField] private EnemyPrefabMapSO prefabMap;
    [SerializeField] private string enemyId = "goblin";
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Tooltip("Enemies per spawn point. They come out in rounds (one per point each round), secondsBetweenRounds apart.")]
    [Min(1)] [SerializeField] private int countPerPoint = 1;
    [Tooltip("Pause between rounds of spawns, so a bigger encounter trickles in instead of arriving as one blob.")]
    [Min(0f)] [SerializeField] private float secondsBetweenRounds = 2.5f;
    [Tooltip("Random horizontal offset around the spawn point, so stacked spawns don't overlap.")]
    [Min(0f)] [SerializeField] private float spawnScatter = 1.5f;

    [Tooltip("Optional: played at each spawn point as its enemy appears (bark, dust puff). Leave empty for none.")]
    [SerializeField] private MMF_Player spawnFeedback;

    /// <summary>(alive, total) after every spawn and death.</summary>
    public event Action<int, int> OnAliveCountChanged;
    public event Action OnAllDead;

    private readonly List<Health> alive = new List<Health>();
    private int total;
    private int pending;
    private bool spawned;
    private bool anyError;

    private void Start()
    {
        if (roster == null) { Debug.LogError($"[TutorialEncounter] {name}: roster is not assigned.", this); anyError = true; }
        if (prefabMap == null) { Debug.LogError($"[TutorialEncounter] {name}: prefabMap is not assigned.", this); anyError = true; }
        if (spawnPoints.Count == 0) { Debug.LogError($"[TutorialEncounter] {name}: no spawn points.", this); anyError = true; }
    }

    private void OnDestroy()
    {
        foreach (Health h in alive)
        {
            if (h != null) h.OnDied -= HandleDied;
        }
    }

    public void Spawn()
    {
        if (anyError || spawned) return;
        spawned = true;

        GameObject prefab = prefabMap.FindPrefab(enemyId);
        EnemyDefinition def = FindDefinition(enemyId);
        if (prefab == null || def == null)
        {
            Debug.LogError($"[TutorialEncounter] {name}: no prefab/roster row for '{enemyId}'.", this);
            return;
        }

        int points = 0;
        foreach (Transform point in spawnPoints)
        {
            if (point != null) points++;
        }
        total = points * countPerPoint;
        pending = total;
        OnAliveCountChanged?.Invoke(total, total);
        if (total == 0)
        {
            OnAllDead?.Invoke();
            return;
        }
        StartCoroutine(SpawnRounds(prefab, def));
    }

    private IEnumerator SpawnRounds(GameObject prefab, EnemyDefinition def)
    {
        for (int round = 0; round < countPerPoint; round++)
        {
            if (round > 0 && secondsBetweenRounds > 0f) yield return new WaitForSeconds(secondsBetweenRounds);
            foreach (Transform point in spawnPoints)
            {
                if (point == null) continue;
                pending--;
                SpawnOne(prefab, def, point);
            }
        }
        // Everything already spawned may have died before the last round came out.
        if (alive.Count == 0) OnAllDead?.Invoke();
    }

    private void SpawnOne(GameObject prefab, EnemyDefinition def, Transform point)
    {
        Vector3 pos = point.position;
        if (spawnScatter > 0f)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnScatter;
            pos += new Vector3(offset.x, 0f, offset.y);
        }
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;

        GameObject enemy = Instantiate(prefab, pos, point.rotation);
        EnemyDefinitionApplier.Apply(enemy, def);
        if (spawnFeedback != null) spawnFeedback.PlayFeedbacks(pos);

        Health health = enemy.GetComponent<Health>();
        if (health == null) return;
        alive.Add(health);
        health.OnDied += HandleDied;
    }

    private void HandleDied()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null || alive[i].IsDead)
            {
                if (alive[i] != null) alive[i].OnDied -= HandleDied;
                alive.RemoveAt(i);
            }
        }
        OnAliveCountChanged?.Invoke(alive.Count + pending, total);
        if (alive.Count == 0 && pending == 0) OnAllDead?.Invoke();
    }

    private EnemyDefinition FindDefinition(string id)
    {
        foreach (EnemyDefinition def in roster.Enemies)
        {
            if (def != null && string.Equals(def.id, id, StringComparison.OrdinalIgnoreCase)) return def;
        }
        return null;
    }
}
