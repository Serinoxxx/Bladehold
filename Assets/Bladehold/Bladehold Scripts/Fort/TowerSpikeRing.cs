using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Spikes upgrade on a tower (plan 17): a ring of stakes round the tower base that pulses damage
///     into every enemy inside <see cref="FortUpgradeConfigSO.towerSpikeRadius" />. It covers the turret's
///     blind spot, the job the removed Spike Trap tower used to do. Parented to the tower when bought, so
///     it goes when the tower does. A hexed tower's spikes stay down too.
/// </summary>
public class TowerSpikeRing : MonoBehaviour
{
    [Tooltip("Played at the ring each pulse that hits something (stab sound, dust).")]
    [SerializeField] private MMF_Player stabFeedback;

    private DefenseStructure owner;
    private float radius;
    private float damage;
    private float interval;
    private float nextPulse;
    private int enemyMask;
    private readonly Collider[] hits = new Collider[48];
    private readonly HashSet<Health> struck = new HashSet<Health>();

    public void Init(DefenseStructure tower, FortUpgradeConfigSO config)
    {
        owner = tower;
        radius = config.towerSpikeRadius;
        damage = config.towerSpikeDamage;
        interval = config.towerSpikeInterval;
        nextPulse = Time.time + interval;
    }

    private void Start()
    {
        int mask = LayerMask.GetMask("Enemy");
        enemyMask = mask != 0 ? mask : 1 << 7;
        if (stabFeedback == null) Debug.LogError($"{name}: TowerSpikeRing.stabFeedback is not assigned.", this);
    }

    private void Update()
    {
        if (owner == null || Time.time < nextPulse) return;
        nextPulse = Time.time + interval;
        if (owner.IsHexed) return;

        struck.Clear();
        Vector3 centre = owner.transform.position;
        int count = Physics.OverlapSphereNonAlloc(centre, radius, hits, enemyMask, QueryTriggerInteraction.Collide);
        float dmg = damage;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            dmg *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
        }

        for (int i = 0; i < count; i++)
        {
            Health h = hits[i] != null ? hits[i].GetComponentInParent<Health>() : null;
            hits[i] = null;
            if (h == null || h.IsDead || h.ImmuneToPlayerDamage || !struck.Add(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            Vector3 offset = h.transform.position - centre;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius) continue;

            h.ReceiveDamage(new Damage
            {
                isDefenseDamage = true,
                isPlayerDamage = true,
                value = dmg,
                type = DamageType.sharp,
                sourcePosition = centre,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            });
        }

        if (struck.Count > 0 && stabFeedback != null)
        {
            stabFeedback.PlayFeedbacks(centre);
        }
    }
}
