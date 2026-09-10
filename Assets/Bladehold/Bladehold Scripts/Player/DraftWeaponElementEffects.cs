using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds explosion/chain riders to draft-charged weapon hits. Ice and burning statuses are
/// applied by EnemyStatusManager from the same damage payload, not applied a second time here.
/// </summary>
public class DraftWeaponElementEffects : MonoBehaviour
{
    [SerializeField] private DraftWeaponElementSO config;
    [SerializeField] private PlayerWeaponManager weapons;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private ChainLightning chainLightning;
    [SerializeField] private LayerMask enemyLayers;

    private readonly HashSet<DamageTrigger> meleeTriggers = new HashSet<DamageTrigger>();
    private readonly HashSet<PlayerBow> bows = new HashSet<PlayerBow>();
    private readonly HashSet<PlayerThrownAxe> axes = new HashSet<PlayerThrownAxe>();
    private readonly HashSet<Health> explosionTargets = new HashSet<Health>();
    private Collider[] overlapBuffer = new Collider[32];
    private bool anyError;

    private void OnValidate()
    {
        if (weapons == null) weapons = GetComponent<PlayerWeaponManager>();
        if (stats == null) stats = transform.root.GetComponentInChildren<PlayerStats>(true);
        if (chainLightning == null) chainLightning = transform.root.GetComponentInChildren<ChainLightning>(true);
    }

    private void Start()
    {
        if (config == null || weapons == null || stats == null || chainLightning == null || enemyLayers.value == 0)
        {
            Debug.LogError("[DraftWeaponElementEffects] Assign config, populated weapon manager, stats, chain lightning and enemy layers.", this);
            anyError = true;
            return;
        }

        stats.SetBase(StatType.WeaponFireExplosionDamagePercent, config.explosionDamagePercent);
        stats.SetBase(StatType.WeaponFireExplosionRadius, config.explosionRadius);
        stats.SetBase(StatType.WeaponLightningBounces, config.lightningBounces);
        stats.SetBase(StatType.WeaponLightningDamagePercent, config.lightningDamagePercent);

        foreach (var slot in weapons.meleeWeapons)
        {
            if (slot.damageTrigger != null && meleeTriggers.Add(slot.damageTrigger))
                slot.damageTrigger.OnHit += HandleHit;
        }
        foreach (var slot in weapons.rangedWeapons)
        {
            if (slot.aimWeaponComponent is PlayerBow bow && bows.Add(bow)) bow.OnHit += HandleHit;
            if (slot.aimWeaponComponent is PlayerThrownAxe axe && axes.Add(axe)) axe.OnHit += HandleHit;
        }
    }

    private void OnDestroy()
    {
        foreach (var trigger in meleeTriggers) if (trigger != null) trigger.OnHit -= HandleHit;
        foreach (var bow in bows) if (bow != null) bow.OnHit -= HandleHit;
        foreach (var axe in axes) if (axe != null) axe.OnHit -= HandleHit;
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        if (anyError || !isActiveAndEnabled || target == null || damage == null || !damage.isPlayerDamage || damage.value <= 0f)
            return;

        // Use the element carried by this hit, including projectiles already in flight.
        if (string.Equals(damage.elementId, "FIRE", StringComparison.OrdinalIgnoreCase))
        {
            Explode(target, damage, hitPoint);
        }
        else if (string.Equals(damage.elementId, "LIGHTNING", StringComparison.OrdinalIgnoreCase))
        {
            chainLightning.ForceChain(damage.value, hitPoint, target,
                Mathf.Max(0, Mathf.RoundToInt(stats.GetValue(StatType.WeaponLightningBounces))),
                Mathf.Max(0f, stats.GetValue(StatType.WeaponLightningDamagePercent)));
        }
    }

    private void Explode(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        float radius = stats.GetValue(StatType.WeaponFireExplosionRadius);
        float value = damage.value * stats.GetValue(StatType.WeaponFireExplosionDamagePercent);
        if (radius <= 0f || value <= 0f) return;

        int count;
        while ((count = Physics.OverlapSphereNonAlloc(hitPoint, radius, overlapBuffer, enemyLayers, QueryTriggerInteraction.Collide)) == overlapBuffer.Length)
            Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);

        explosionTargets.Clear();
        for (int i = 0; i < count; i++)
        {
            Health health = overlapBuffer[i].GetComponentInParent<Health>();
            if (health == null || health == target || health.IsDead || health.ImmuneToPlayerDamage
                || health.transform.root == transform.root || !explosionTargets.Add(health)) continue;
            health.ReceiveDamage(new Damage
            {
                value = value,
                type = DamageType.elemental,
                sourcePosition = hitPoint,
                source = damage.source,
                isPlayerDamage = true,
            });
        }

        if (config.explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(config.explosionVfxPrefab, hitPoint, Quaternion.identity);
            Destroy(vfx, config.explosionVfxLifetime);
        }
    }
}
