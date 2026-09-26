using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Storm Cloud spawned overhead by Tempest Catapults upon impact.
///     Hovers above the battlefield and periodically calls down lightning strikes on enemies in radius.
/// </summary>
public class CatapultStormCloud : MonoBehaviour
{
    [SerializeField] private float radius = 6.0f;
    [SerializeField] private float duration = 8.0f;
    [SerializeField] private float strikeInterval = 0.75f;
    [SerializeField] private float strikeDamage = 35f;
    [Tooltip("The cloud child, authored for a 1m radius; x/z are scaled to the strike radius.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Played at each lightning strike point (bolt burst, plus a sound if one gets added).")]
    [SerializeField] private MMF_Player strikeFeedback;

    private readonly Collider[] hitBuffer = new Collider[32];
    private float aliveTime = 0f;
    private float nextStrikeTime = 0f;
    private Vector3 visualBaseScale = Vector3.one;

    public float Radius => radius;
    public float Duration => duration;
    public float StrikeDamage => strikeDamage;

    /// <summary>Instantiates the authored storm cloud prefab (CatapultProjectile.stormCloudPrefab) above the impact point.</summary>
    public static CatapultStormCloud Spawn(CatapultStormCloud prefab, Vector3 impactPosition, float rad = 6.0f, float dur = 8.0f, float damage = 35f)
    {
        if (prefab == null)
        {
            Debug.LogError("[CatapultStormCloud] No storm cloud prefab: assign CatapultProjectile.stormCloudPrefab.");
            return null;
        }

        CatapultStormCloud cloud = Instantiate(prefab, impactPosition + Vector3.up * 3.5f, Quaternion.identity);
        cloud.Init(rad, dur, damage);
        return cloud;
    }

    public void Init(float rad, float dur, float damage)
    {
        radius = rad;
        duration = dur;
        strikeDamage = damage;
        ScaleVisual();
    }

    private void Awake()
    {
        if (visualRoot != null) visualBaseScale = visualRoot.localScale;
    }

    private void Start()
    {
        if (visualRoot == null)
        {
            // Still strikes, just invisible.
            Debug.LogError("[CatapultStormCloud] visualRoot is not assigned (the cloud child, authored for a 1m radius).", this);
        }
        if (strikeFeedback == null)
        {
            Debug.LogError("[CatapultStormCloud] strikeFeedback is not assigned.", this);
        }
        ScaleVisual();
        nextStrikeTime = Time.time + 0.3f; // quick initial strike
    }

    private void ScaleVisual()
    {
        if (visualRoot == null) return;
        visualRoot.localScale = new Vector3(visualBaseScale.x * radius, visualBaseScale.y, visualBaseScale.z * radius);
    }

    private void Update()
    {
        aliveTime += Time.deltaTime;
        if (aliveTime >= duration)
        {
            Destroy(gameObject);
            return;
        }

        // Slight drift / wobble
        transform.position += new Vector3(Mathf.Sin(Time.time * 1.5f) * 0.15f, Mathf.Cos(Time.time * 2f) * 0.08f, 0f) * Time.deltaTime;

        if (Time.time >= nextStrikeTime)
        {
            nextStrikeTime = Time.time + strikeInterval;
            PerformLightningStrike();
        }
    }

    private void PerformLightningStrike()
    {
        Vector3 groundPos = transform.position - Vector3.up * 3.5f;
        int count = Physics.OverlapSphereNonAlloc(groundPos, radius, hitBuffer);
        List<Health> eligibleTargets = new List<Health>();

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            if (Player.Instance != null && (col.transform.root == Player.Instance.transform.root || col.gameObject == Player.Instance.gameObject))
            {
                continue;
            }

            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead && !eligibleTargets.Contains(h))
            {
                eligibleTargets.Add(h);
            }
        }

        if (eligibleTargets.Count == 0) return;

        // Pick 1 or 2 targets
        int strikeCount = Mathf.Min(eligibleTargets.Count, Random.Range(1, 3));
        for (int s = 0; s < strikeCount; s++)
        {
            int idx = Random.Range(0, eligibleTargets.Count);
            Health target = eligibleTargets[idx];
            eligibleTargets.RemoveAt(idx);

            StrikeTarget(target);
        }
    }

    private void StrikeTarget(Health target)
    {
        if (target == null || target.IsDead) return;

        Vector3 strikePoint = target.transform.position + Vector3.up * 0.8f;
        float actualDamage = strikeDamage;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            actualDamage *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
        }

        Damage damage = new Damage
        {
            value = actualDamage,
            type = DamageType.elemental,
            elementId = "Lightning",
            sourcePosition = transform.position,
            source = Player.Instance != null ? Player.Instance.Damageable : null,
            isPlayerDamage = true
        };

        target.ReceiveDamage(damage);
        EnemyStatusManager.GetOrAdd(target)?.ApplyStatus("Lightning");

        if (strikeFeedback != null)
        {
            strikeFeedback.PlayFeedbacks(strikePoint);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position - Vector3.up * 3.5f, radius);
    }
#endif
}
