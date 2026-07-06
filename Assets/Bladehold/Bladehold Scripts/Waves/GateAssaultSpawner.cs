using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Gate-defense pressure: on a fixed interval (~30s), spawns a mini-wave of enemies that beeline
///     for a gate, alternating which gate each mini-wave targets (round-robin over the alive
///     <see cref="Gate" />s, so the pattern stays predictable/learnable). The player must clear one
///     gate and travel to the next before the next mini-wave lands — spacing between gates is the
///     tuning lever, per the TODO design.
///
///     Deliberately independent of <see cref="WaveSpawner" />: mini-wave enemies do NOT count toward
///     the main wave's kill total (they're extra pressure, not wave members), though their deaths
///     still drop coins and score kills through the normal <see cref="Health.OnDied" /> listeners.
///     Difficulty scales with the main spawner's <see cref="WaveSpawner.CurrentWave" /> via
///     <see cref="countAddedPerWave" />, and by gate HP / enemy mix — never by randomizing the
///     timing. Inert (disables itself) in scenes without gates. Stops when the run ends: player
///     death or any gate falling.
/// </summary>
public class GateAssaultSpawner : MonoBehaviour
{
    [Header("What to spawn")]
    [Tooltip("Enemy prefab for mini-waves (e.g. a quick gob). Needs Health; an AITargetSelector makes it actually beeline its gate.")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Pacing")]
    [Tooltip("Seconds between mini-waves. Fixed and predictable on purpose — mastery over time.")]
    [SerializeField] private float assaultInterval = 30f;
    [Tooltip("Enemies in a mini-wave on wave 1.")]
    [SerializeField] private int baseCount = 3;
    [Tooltip("Extra enemies per main WaveSpawner wave beyond the first.")]
    [SerializeField] private int countAddedPerWave = 1;

    [Header("Where to spawn")]
    [Tooltip("Spawn points for mini-waves (e.g. beyond the walls). A random one is used per enemy. If empty, enemies spawn around this object within Spawn Radius.")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Fallback spawn radius around this object, used only when no spawn points are assigned.")]
    [SerializeField] private float spawnRadius = 8f;
    [Tooltip("Spawn positions are snapped to the nearest NavMesh point within this distance.")]
    [SerializeField] private float navMeshSampleDistance = 3f;

    private Health playerHealth;
    private int nextGateIndex;
    private bool runOver = false;
    private bool anyError = false;

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("GateAssaultSpawner has no enemy prefab assigned.");
            anyError = true;
        }
        else if (enemyPrefab.GetComponent<Health>() == null)
        {
            Debug.LogError("GateAssaultSpawner's enemy prefab has no Health component.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Gates registered in their Awake, so this sees the scene's full set. No gates = nothing to
        // defend; stay inert so the component is safe to leave in gate-less scenes.
        if (Gate.All.Count == 0)
        {
            Debug.LogWarning("GateAssaultSpawner found no Gates in the scene; mini-waves are disabled.");
            enabled = false;
            return;
        }

        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandleRunOver;
        }
        Gate.OnAnyGateDestroyed += HandleGateDestroyed;

        StartCoroutine(AssaultLoop());
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandleRunOver;
        }
        Gate.OnAnyGateDestroyed -= HandleGateDestroyed;
    }

    private void HandleRunOver()
    {
        runOver = true;
    }

    private void HandleGateDestroyed(Gate gate)
    {
        // A fallen gate ends the run (the DeathScreen/WaveSpawner routing) — no more mini-waves.
        runOver = true;
    }

    private IEnumerator AssaultLoop()
    {
        while (!runOver)
        {
            yield return new WaitForSeconds(assaultInterval);
            if (runOver)
            {
                yield break;
            }

            Gate gate = NextAliveGate();
            if (gate == null)
            {
                continue;
            }

            int wave = WaveSpawner.Instance != null ? WaveSpawner.Instance.CurrentWave : 1;
            int count = baseCount + countAddedPerWave * Mathf.Max(0, wave - 1);
            for (int i = 0; i < count; i++)
            {
                SpawnAssaulter(gate);
            }
        }
    }

    /// <summary>Round-robin over the alive gates so consecutive mini-waves alternate targets.</summary>
    private Gate NextAliveGate()
    {
        int gateCount = Gate.All.Count;
        for (int i = 0; i < gateCount; i++)
        {
            Gate gate = Gate.All[nextGateIndex % gateCount];
            nextGateIndex++;
            if (gate != null && !gate.IsDestroyed)
            {
                return gate;
            }
        }
        return null;
    }

    private void SpawnAssaulter(Gate gate)
    {
        Vector3 position = ResolveSpawnPosition();
        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);

        // Assigned right after Instantiate (the MarkGolden timing trick) so the enemy heads for its
        // gate from its very first repath.
        enemy.GetComponent<AITargetSelector>()?.AssignGate(gate);
    }

    private Vector3 ResolveSpawnPosition()
    {
        Vector3 candidate;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            candidate = point != null ? point.position : transform.position;
        }
        else
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
        }

        // Snap onto the NavMesh so spawned enemies can immediately pathfind (the WaveSpawner idiom).
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            candidate = hit.position;
        }
        return candidate;
    }
}
