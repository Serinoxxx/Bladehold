using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The "Chain Lightning" skill line: while <see cref="ChainLightningBuff" /> is active (granted by
///     picking up a <see cref="LightningOrb" />), every sword hit chains to nearby enemies. Follows the
///     <see cref="VampiricBlade" />/<see cref="SwordHitFeedback" /> pattern — a separate listener on the
///     sword's <see cref="DamageTrigger.OnHit" />, so the trigger stays unaware of it.
///
///     From the hit point, each hop finds the nearest not-yet-hit enemy within
///     <see cref="ChainLightningBuff.ChainRadius" /> (the same <c>OverlapSphereNonAlloc</c> +
///     <c>TryGetComponent</c>/<c>GetComponentInParent</c> resolution <see cref="DamageTrigger" /> uses),
///     deals <see cref="ChainLightningBuff.CurrentDamagePercent" /> of the triggering hit's damage
///     (rolling <see cref="ChainLightningBuff.CurrentCritChance" /> against the sword's
///     <see cref="StatType.CritMultiplier" />), then continues the chain from there — up to
///     <see cref="ChainLightningBuff.CurrentBounces" /> hops, stopping early if no target is in range.
/// </summary>
public class ChainLightning : MonoBehaviour
{
    [Tooltip("The sword's DamageTrigger whose hits can chain. Assign explicitly — the player has other DamageTriggers (e.g. the Death Nova hitbox).")]
    [SerializeField] private DamageTrigger swordTrigger;
    [Tooltip("Optional; defaults to Player.Instance's ChainLightningBuff.")]
    [SerializeField] private ChainLightningBuff buff;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Layers a bounce can hit. Exclude the player and environment so a chain can't loop back or fizzle on scenery.")]
    [SerializeField] private LayerMask enemyLayers = ~0;
    [Tooltip("Optional cosmetic VFX instantiated at each bounce's target.")]
    [SerializeField] private GameObject bounceVfxPrefab;

    private const int MaxOverlapResults = 32;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> excluded = new HashSet<IDamageable>();

    private bool anyError = false;

    private void Start()
    {
        if (buff == null)
        {
            buff = Player.Instance != null ? Player.Instance.GetComponentInChildren<ChainLightningBuff>() : null;
        }
        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }

        if (swordTrigger == null)
        {
            Debug.LogError("ChainLightning 'swordTrigger' (the sword's DamageTrigger) is not assigned in the inspector.");
            anyError = true;
        }
        if (buff == null)
        {
            Debug.LogError("ChainLightning could not find a ChainLightningBuff (set it or ensure Player.Instance has one).");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("ChainLightning could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        swordTrigger.OnHit += HandleHit;
    }

    private void OnDestroy()
    {
        if (swordTrigger != null)
        {
            swordTrigger.OnHit -= HandleHit;
        }
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        if (anyError || !buff.IsActive)
        {
            return;
        }

        int bounces = buff.CurrentBounces;
        float damagePercent = buff.CurrentDamagePercent;
        if (bounces <= 0 || damagePercent <= 0f)
        {
            return;
        }

        float critChance = buff.CurrentCritChance;
        float critMultiplier = stats.GetValue(StatType.CritMultiplier);
        float chainRadius = buff.ChainRadius;

        excluded.Clear();
        excluded.Add(target);
        Vector3 origin = hitPoint;

        for (int i = 0; i < bounces; i++)
        {
            if (!TryFindNearestTarget(origin, chainRadius, out IDamageable found, out Vector3 foundPosition))
            {
                break;
            }

            excluded.Add(found);

            float value = damage.value * damagePercent;
            bool crit = Random.value < critChance;
            if (crit)
            {
                value *= critMultiplier;
            }

            found.ReceiveDamage(new Damage
            {
                value = value,
                type = DamageType.elemental,
                isCritical = crit,
                sourcePosition = origin,
            });

            if (bounceVfxPrefab != null)
            {
                Instantiate(bounceVfxPrefab, foundPosition, Quaternion.identity);
            }

            origin = foundPosition;
        }
    }

    private bool TryFindNearestTarget(Vector3 origin, float radius, out IDamageable found, out Vector3 foundPosition)
    {
        found = null;
        foundPosition = origin;
        float bestSqrDistance = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(origin, radius, overlapBuffer, enemyLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }
            if (damageable == null || excluded.Contains(damageable))
            {
                continue;
            }

            float sqrDistance = (collider.transform.position - origin).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                found = damageable;
                foundPosition = collider.transform.position;
            }
        }

        return found != null;
    }
}
