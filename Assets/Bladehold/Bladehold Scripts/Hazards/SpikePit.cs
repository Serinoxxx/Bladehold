using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A spiked ravine floor: anything except the player that lands inside the box takes one heavy hit.
///     Enemies only end up here when a fling ragdolls them off a bridge or rim; KnockbackReceiver then
///     recovers them onto the baked floor NavMesh and they walk out via the exit ramp. The box is polled
///     with OverlapBox (like SpikeTrapDefense) rather than a trigger collider so it never ends up in a
///     NavMesh bake. Placed by the defense scene generator, one per ravine segment.
/// </summary>
public class SpikePit : MonoBehaviour
{
    [SerializeField] private SpikePitConfigSO config;
    [Tooltip("Half extents of the hazard box in local space, centred on this transform.")]
    [SerializeField] private Vector3 halfExtents = new Vector3(5f, 1f, 3f);

    private readonly Collider[] overlapBuffer = new Collider[64];
    // Shared by every pit: a ravine is several boxes end to end, and walking along the floor from one
    // into the next must not count as a second fall.
    private static readonly Dictionary<Health, float> lastSeenInside = new Dictionary<Health, float>();
    private static readonly List<Health> staleVictims = new List<Health>();
    private float nextPollTime;
    private bool anyError;

    public Vector3 HalfExtents
    {
        get => halfExtents;
        set => halfExtents = value;
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError($"{name}: SpikePit.config is not assigned.", this);
            anyError = true;
        }
    }

    private void Update()
    {
        if (anyError) return;
        if (Time.time < nextPollTime) return;
        nextPollTime = Time.time + config.pollInterval;

        int count = Physics.OverlapBoxNonAlloc(transform.position, halfExtents, overlapBuffer, transform.rotation,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Health h = overlapBuffer[i].GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            bool freshFall = !lastSeenInside.TryGetValue(h, out float lastSeen)
                             || Time.time - lastSeen > config.reentryCooldown;
            lastSeenInside[h] = Time.time;
            if (freshFall) Impale(h);
        }

        PruneVictims(config.reentryCooldown);
    }

    private void Impale(Health target)
    {
        target.ReceiveDamage(new Damage
        {
            value = config.impactDamage,
            type = config.damageType,
            isPlayerDamage = true,
            sourcePosition = transform.position,
            source = Player.Instance != null ? Player.Instance.Damageable : null
        });
    }

    private static void PruneVictims(float cooldown)
    {
        if (lastSeenInside.Count < 32) return;
        staleVictims.Clear();
        foreach (var kv in lastSeenInside)
        {
            if (kv.Key == null || kv.Key.IsDead || Time.time - kv.Value > cooldown)
                staleVictims.Add(kv.Key);
        }
        foreach (Health h in staleVictims) lastSeenInside.Remove(h);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
    }
#endif
}
