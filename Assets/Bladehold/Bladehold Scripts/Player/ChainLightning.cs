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
    [Tooltip("Optional; draws the SineVFX bolt through the chain's hops. Defaults to Player.Instance's ChainLightningVfx.")]
    [SerializeField] private ChainLightningVfx chainVfx;

    private const int MaxOverlapResults = 32;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> excluded = new HashSet<IDamageable>();
    // Reused per chain so ShowChain feeds the SineVFX bolt without per-hit allocation: the origin
    // hit point followed by each hop target, in order.
    private readonly List<Vector3> chainPointsBuffer = new List<Vector3>();

    private bool anyError = false;

    /// <summary>
    ///     Re-points at the active class's melee DamageTrigger. Called by
    ///     <see cref="PlayerClassController" /> in Awake, before Start subscribes.
    /// </summary>
    public void SetSwordTrigger(DamageTrigger trigger)
    {
        swordTrigger = trigger;
    }

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
        if (chainVfx == null)
        {
            chainVfx = Player.Instance != null ? Player.Instance.GetComponentInChildren<ChainLightningVfx>() : null;
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
        TryChain(target, damage.value, hitPoint);
    }

    /// <summary>
    ///     Runs one chain from a hit: while the buff is active, hops to nearby not-yet-hit enemies
    ///     dealing a fraction of <paramref name="triggeringDamage" /> per hop. Public so other player
    ///     damage sources (the bow's Storm Arrow skill) can chain their hits exactly like sword hits;
    ///     a no-op while the buff is inactive or the skill line is locked.
    /// </summary>
    public void TryChain(IDamageable target, float triggeringDamage, Vector3 hitPoint)
    {
        if (anyError || !buff.IsActive)
        {
            return;
        }
        Chain(target, triggeringDamage, hitPoint);
    }

    /// <summary>
    ///     Runs a chain regardless of the buff's timer — the lightning source is external (an Unstable
    ///     Orbs detonation, a Conduit-deflected Storm Witch ball, a Mage lightning-imbued hit), not the
    ///     player's charged blade. Still stat-gated: a no-op until something grants bounces/damage.
    ///     <paramref name="excludeTarget" /> keeps the first hop from arcing straight back into the
    ///     enemy that triggered the chain (pass the enemy that was just hit, when there is one).
    /// </summary>
    public void ForceChain(float triggeringDamage, Vector3 hitPoint, IDamageable excludeTarget = null)
    {
        if (anyError)
        {
            return;
        }
        Chain(excludeTarget, triggeringDamage, hitPoint);
    }

    private void Chain(IDamageable target, float triggeringDamage, Vector3 hitPoint)
    {
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
        if (target != null)
        {
            excluded.Add(target);
        }
        Vector3 origin = hitPoint;

        chainPointsBuffer.Clear();
        chainPointsBuffer.Add(origin);

        for (int i = 0; i < bounces; i++)
        {
            if (!TryFindNearestTarget(origin, chainRadius, out IDamageable found, out Vector3 foundPosition))
            {
                break;
            }

            excluded.Add(found);

            float value = triggeringDamage * damagePercent;
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
            chainPointsBuffer.Add(foundPosition);
        }

        // Draw the connecting bolt through every hop that landed (origin + at least one target).
        if (chainVfx != null && chainPointsBuffer.Count >= 2)
        {
            chainVfx.ShowChain(chainPointsBuffer);
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
