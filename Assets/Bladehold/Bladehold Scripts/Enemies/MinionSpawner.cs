using System;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Fort Golem's production line: every <see cref="MinionSpawnerSO.spawnInterval" /> seconds
///     it spawns <see cref="MinionSpawnerSO.spawnCount" /> minions (dwarves) beside itself,
///     NavMesh-snapped, applies the minion's roster CSV row via the public static
///     <see cref="WaveSpawner.ApplyDefinition" /> (the EnemyZoo precedent — right after Instantiate,
///     before the minion's Start), and reports each one to
///     <see cref="WaveSpawner.RegisterExternalEnemy" /> so wave accounting stays consistent.
///     Registration failing (no spawner, intermission) degrades gracefully — minions still work
///     standalone, since kill credit/coins/corpses are all <see cref="Health" />-event-driven.
///     Production stops at <see cref="MinionSpawnerSO.maxAliveMinions" /> and on the golem's or the
///     player's death.
/// </summary>
public class MinionSpawner : MonoBehaviour
{
    [SerializeField] private MinionSpawnerSO data;
    [SerializeField] private Health health;
    [Tooltip("The minion prefab (normally the Dwarf enemy variant).")]
    [SerializeField] private GameObject minionPrefab;
    [Tooltip("The enemy roster, for the minion row's stat overrides.")]
    [SerializeField] private EnemyRosterSO roster;
    [Tooltip("Optional feedback per spawn batch (a forge clank + glow).")]
    [SerializeField] private MMF_Player spawnFeedback;

    private EnemyDefinition minionDef;
    private Health playerHealth;
    private float lastSpawnTime;
    private int aliveMinions;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("MinionSpawnerSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (minionPrefab == null)
        {
            Debug.LogError("Minion prefab is not assigned in the inspector.");
            anyError = true;
        }
        if (roster == null)
        {
            Debug.LogError("EnemyRosterSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        foreach (EnemyDefinition def in roster.Enemies)
        {
            if (def.id == data.minionId)
            {
                minionDef = def;
                break;
            }
        }
        if (minionDef == null)
        {
            Debug.LogError($"MinionSpawner: roster has no row with id '{data.minionId}'; minions would spawn with raw prefab stats.");
            anyError = true;
            return;
        }

        health.OnDied += HandleDied;

        Player playerInstance = Player.Instance;
        if (playerInstance != null && playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        // First batch lands after one full interval — a golem shouldn't spawn adds the frame it appears.
        lastSpawnTime = Time.time;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastSpawnTime < data.spawnInterval) return;
        lastSpawnTime = Time.time;

        SpawnBatch();
    }

    private void SpawnBatch()
    {
        int budget = Mathf.Min(data.spawnCount, data.maxAliveMinions - aliveMinions);
        if (budget <= 0)
        {
            return;
        }

        bool spawnedAny = false;
        for (int i = 0; i < budget; i++)
        {
            if (SpawnMinion())
            {
                spawnedAny = true;
            }
        }

        if (spawnedAny && spawnFeedback != null)
        {
            spawnFeedback.PlayFeedbacks();
        }
    }

    private bool SpawnMinion()
    {
        // A ring point beside the golem, snapped onto the NavMesh (the WaveSpawner placement idiom).
        Vector2 ring = UnityEngine.Random.insideUnitCircle.normalized * data.spawnRadius;
        Vector3 candidate = transform.position + new Vector3(ring.x, 0f, ring.y);
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
        {
            return false;
        }

        GameObject minion = Instantiate(minionPrefab, navHit.position, Quaternion.identity);

        // Roster overrides before the minion's Start (the MarkGolden timing trick).
        WaveSpawner.ApplyDefinition(minion, minionDef);

        // Wave accounting: registration grows the wave total and alive set so the wave only clears
        // once minions are dead too. Failing (no spawner, intermission) is fine — see class doc.
        WaveSpawner spawner = WaveSpawner.Instance;
        spawner?.RegisterExternalEnemy(minion);

        // Track this golem's own population cap through the same death signal everything else uses.
        Health minionHealth = minion.GetComponent<Health>();
        if (minionHealth != null)
        {
            aliveMinions++;
            Action handler = null;
            handler = () =>
            {
                minionHealth.OnDied -= handler;
                aliveMinions--;
            };
            minionHealth.OnDied += handler;
        }

        return true;
    }
}
