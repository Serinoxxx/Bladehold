using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Ice wall upgrade's pool (plan 17): icy water spilled over the wall onto its attackers. Every
///     tick it slows enemies inside and adds Ice, so attackers who stay at the wall build up to Frozen
///     (<see cref="EnemyStatusManager" />). No damage: the Fire wall's boiling oil is the damage option.
/// </summary>
public class WallIcyWaterZone : MonoBehaviour
{
    [SerializeField] private float radius = 4f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float tickInterval = 0.5f;
    [Range(0f, 0.95f)]
    [SerializeField] private float slowFraction = 0.4f;
    [Tooltip("Played once when the water lands (splash sound, steam).")]
    [SerializeField] private MMF_Player splashFeedback;

    private float age;
    private float nextTick;
    private int enemyMask;
    private readonly Collider[] hits = new Collider[64];
    private readonly HashSet<Health> chilled = new HashSet<Health>();

    public void Init(float rad)
    {
        radius = rad;
    }

    private void Start()
    {
        int mask = LayerMask.GetMask("Enemy");
        enemyMask = mask != 0 ? mask : 1 << 7;
        if (splashFeedback == null) Debug.LogError($"{name}: WallIcyWaterZone.splashFeedback is not assigned.", this);
        else splashFeedback.PlayFeedbacks(transform.position);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }
        if (Time.time < nextTick) return;
        nextTick = Time.time + tickInterval;

        chilled.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, hits, enemyMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i];
            hits[i] = null;
            Health h = col != null ? col.GetComponentInParent<Health>() : null;
            if (h == null || h.IsDead || !chilled.Add(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            SlowStatus.GetOrAdd(col)?.ApplySlow(slowFraction, tickInterval * 2.5f);
            EnemyStatusManager.GetOrAdd(h)?.ApplyStatus("Ice");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0.85f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
