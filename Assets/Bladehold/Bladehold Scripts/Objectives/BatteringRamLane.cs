using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Scene marker for the battering ram's route (plan 22): where a rolled <c>battering_ram</c> spawns and
///     which gate it rolls for. <see cref="SurvivorsSpawner" /> spawns rams at <see cref="SpawnPosition" />,
///     and the ram reads its destination from here in its own Start.
///
///     Formerly the "Stop the Battering Ram" objective; the ram is now a roster enemy a wave card rolls,
///     so this kept only the route. (Same script GUID, so existing scene/prefab instances and their
///     spawn/gate references carried over.)
/// </summary>
public class BatteringRamLane : MonoBehaviour
{
    [Tooltip("Where a rolled battering ram spawns.")]
    [SerializeField] private Transform ramSpawnPoint;

    [Tooltip("The gate the ram rolls for. Blank = the gate nearest the spawn point.")]
    [SerializeField] private Transform gateDestinationPoint;

    [Tooltip("Spawn position used when ramSpawnPoint is unassigned.")]
    [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(30f, 4.1f, -25f);

    [Tooltip("Use fallbackSpawnPosition when ramSpawnPoint is unassigned (otherwise the spawner's own points are used).")]
    [SerializeField] private bool useFallbackSpawn;

    private static readonly List<BatteringRamLane> lanes = new List<BatteringRamLane>();

    public Transform RamSpawnPoint => ramSpawnPoint;
    public Transform GateDestinationPoint => gateDestinationPoint;

    private void OnEnable()
    {
        if (!lanes.Contains(this)) lanes.Add(this);
    }

    private void OnDisable()
    {
        lanes.Remove(this);
    }

    private void Start()
    {
        if (ramSpawnPoint == null && !useFallbackSpawn)
        {
            Debug.LogWarning("[BatteringRamLane] ramSpawnPoint is not assigned; rams will spawn at the spawner's regular points.", this);
        }
    }

    /// <summary>A lane's spawn position, or false when it has neither a spawn point nor an opted-in fallback.</summary>
    public bool TryGetSpawn(out Vector3 position, out Quaternion rotation)
    {
        if (ramSpawnPoint != null)
        {
            position = ramSpawnPoint.position;
            rotation = ramSpawnPoint.rotation;
            return true;
        }
        position = fallbackSpawnPosition;
        rotation = Quaternion.identity;
        return useFallbackSpawn;
    }

    /// <summary>The lane's gate destination: the authored point, else the gate nearest the spawn. False if neither.</summary>
    public bool TryGetDestination(out Vector3 destination, out Gate gate)
    {
        if (gateDestinationPoint != null)
        {
            destination = gateDestinationPoint.position;
            gate = gateDestinationPoint.GetComponentInParent<Gate>();
            return true;
        }

        Vector3 from = ramSpawnPoint != null ? ramSpawnPoint.position : transform.position;
        gate = Gate.NearestAlive(from);
        destination = gate != null ? gate.TargetPosition : default;
        return gate != null;
    }

    /// <summary>The enabled lane nearest a position, or null when the scene has none.</summary>
    public static BatteringRamLane Nearest(Vector3 from)
    {
        BatteringRamLane best = null;
        float bestSqr = float.MaxValue;
        foreach (BatteringRamLane lane in lanes)
        {
            if (lane == null) continue;
            Vector3 p = lane.ramSpawnPoint != null ? lane.ramSpawnPoint.position : lane.transform.position;
            float sqr = (p - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = lane;
            }
        }
        return best;
    }

    /// <summary>A spawn position on any lane in the scene (random if several), or false when there's none.</summary>
    public static bool TryGetAnySpawn(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        if (lanes.Count == 0) return false;
        int start = Random.Range(0, lanes.Count);
        for (int i = 0; i < lanes.Count; i++)
        {
            BatteringRamLane lane = lanes[(start + i) % lanes.Count];
            if (lane != null && lane.TryGetSpawn(out position, out rotation)) return true;
        }
        return false;
    }
}
