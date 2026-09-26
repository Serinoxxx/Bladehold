using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Oil Vat: Spills boiling/slick oil across the area, slowing enemies by 50% and scalding them with heat damage.
/// </summary>
public class OilVatDefense : DefenseStructure
{
    [Header("Oil Vat Config")]
    [SerializeField] private float poolRadius = 5.0f;
    [SerializeField] private float spillCooldown = 6.0f;
    [SerializeField] private float poolDuration = 4.5f;
    [SerializeField] private float damagePerSecond = 20f;
    [Tooltip("The burning oil pool spawned on each spill (a gameplay zone, not feedback).")]
    [SerializeField] private GameObject oilPoolVfxPrefab;
    [Tooltip("Played at the vat each time it spills (splash sound).")]
    [SerializeField] private MMF_Player spillFeedback;

    private float nextSpillTime = 0f;
    private float activePoolRemaining = 0f;
    private float tickTimer = 0f;

    protected override void ValidateFeedbackReferences()
    {
        base.ValidateFeedbackReferences();
        if (spillFeedback == null) Debug.LogError($"{name}: OilVatDefense.spillFeedback is not assigned.", this);
    }

    protected override void Awake()
    {
        defenseType = FortDefenseType.BurningOil;
        supplyPerAction = 3;
        base.Awake();
    }

    protected override void ApplyLevelStats(int level)
    {
        switch (level)
        {
            case 1:
                damagePerSecond = 20f;
                spillCooldown = 6.0f;
                poolRadius = 5.0f;
                break;
            case 2:
                damagePerSecond = 35f;
                spillCooldown = 5.0f;
                poolRadius = 6.0f;
                break;
            case 3:
                damagePerSecond = 55f;
                spillCooldown = 4.0f;
                poolRadius = 7.0f;
                break;
        }
    }

    private void Update()
    {
        if (activePoolRemaining > 0f)
        {
            activePoolRemaining -= Time.deltaTime;
            tickTimer += Time.deltaTime;

            if (tickTimer >= 0.5f)
            {
                tickTimer = 0f;
                TickOilEffect();
            }
        }
        else if (Time.time >= nextSpillTime)
        {
            if (HasEnemiesNearby())
            {
                SpillOil();
                nextSpillTime = Time.time + spillCooldown;
            }
        }
    }

    private bool HasEnemiesNearby()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, poolRadius);
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h != null && !h.IsDead && (Player.Instance == null || h.transform.root != Player.Instance.transform.root))
            {
                return true;
            }
        }
        return false;
    }

    private void SpillOil()
    {
        if (!ConsumeSupply()) return;

        activePoolRemaining = poolDuration;
        tickTimer = 0f;

        if (spillFeedback != null)
        {
            spillFeedback.PlayFeedbacks(transform.position);
        }

        if (oilPoolVfxPrefab != null)
        {
            Instantiate(oilPoolVfxPrefab, transform.position, Quaternion.identity);
        }

        TickOilEffect();
    }

    private void TickOilEffect()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, poolRadius);
        HashSet<Health> processed = new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || h.IsDead || processed.Contains(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            processed.Add(h);

            // Apply 50% slow
            SlowStatus.GetOrAdd(h)?.ApplySlow(0.50f, 1.5f);

            // Apply heat scald damage
            Damage dmg = new Damage
            {
                value = damagePerSecond * 0.5f, // half second tick
                type = DamageType.elemental,
                elementId = "Fire",
                isPlayerDamage = true,
                sourcePosition = transform.position,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            };
            h.ReceiveDamage(dmg);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, poolRadius);
    }
#endif
}
