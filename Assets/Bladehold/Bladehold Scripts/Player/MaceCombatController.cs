using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Handles passive combat mechanics for the 2H Mace:
///     - Concussive Stun on hit (via SlowStatus 100% slow)
///     - Extra knockback impulse
///     - Ground shockwaves when releasing charged strikes
/// </summary>
public class MaceCombatController : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private GameObject shockwaveVfxPrefab;
    [SerializeField] private AudioClip shockwaveSfx;

    private DamageTrigger subscribedTrigger;

    private void Awake()
    {
        if (playerStats == null) playerStats = GetComponentInParent<PlayerStats>() ?? GetComponentInChildren<PlayerStats>();
        if (playerAttack == null) playerAttack = GetComponentInParent<PlayerAttack>() ?? GetComponentInChildren<PlayerAttack>();
    }

    private void Start()
    {
        if (playerStats != null)
        {
            playerStats.SetBase(StatType.MaceArmorShatterBonus, 0f);
            playerStats.SetBase(StatType.MaceStunDuration, 0f);
            playerStats.SetBase(StatType.MaceShockwaveDamage, 0f);
            playerStats.SetBase(StatType.MaceKnockbackMultiplier, 0f);
            playerStats.SetBase(StatType.UltimateMaceEarthquakeUnlocked, 0f);
        }

        if (PlayerWeaponManager.Instance != null)
        {
            PlayerWeaponManager.Instance.OnMeleeChanged += HandleMeleeChanged;
            if (PlayerWeaponManager.Instance.ActiveMeleeTrigger != null)
            {
                HookTrigger(PlayerWeaponManager.Instance.ActiveMeleeTrigger);
            }
        }
    }

    private void OnDestroy()
    {
        if (PlayerWeaponManager.Instance != null)
        {
            PlayerWeaponManager.Instance.OnMeleeChanged -= HandleMeleeChanged;
        }
        UnhookTrigger();
    }

    private void HandleMeleeChanged(WeaponDefinitionSO def)
    {
        UnhookTrigger();
        if (PlayerWeaponManager.Instance != null && PlayerWeaponManager.Instance.ActiveMeleeTrigger != null)
        {
            HookTrigger(PlayerWeaponManager.Instance.ActiveMeleeTrigger);
        }
    }

    private void HookTrigger(DamageTrigger trigger)
    {
        if (trigger == null) return;
        subscribedTrigger = trigger;
        subscribedTrigger.OnHit += HandleMeleeHit;
    }

    private void UnhookTrigger()
    {
        if (subscribedTrigger != null)
        {
            subscribedTrigger.OnHit -= HandleMeleeHit;
            subscribedTrigger = null;
        }
    }

    private void HandleMeleeHit(IDamageable target, Damage damageDealt, Vector3 hitPoint)
    {
        if (PlayerWeaponManager.Instance == null || 
            !string.Equals(PlayerWeaponManager.Instance.CurrentMeleeId, "mace", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (playerStats == null) return;

        // 1. Concussive Stun
        float stunDuration = playerStats.GetValue(StatType.MaceStunDuration);
        if (stunDuration > 0f && target is Component comp)
        {
            SlowStatus slow = SlowStatus.GetOrAdd(comp);
            if (slow != null)
            {
                slow.ApplySlow(1.0f, stunDuration);
            }
            if (comp.TryGetComponent<NavMeshAgent>(out var agent) && agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
            }
        }

        // 2. Concussive Knockback Bonus
        float knockbackMult = playerStats.GetValue(StatType.MaceKnockbackMultiplier);
        if (knockbackMult > 0f && target is Component compTarget)
        {
            Health enemyHealth = compTarget.GetComponentInParent<Health>();
            if (enemyHealth != null && enemyHealth.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
            {
                Vector3 knockDir = (enemyHealth.transform.position - transform.position).normalized;
                knockDir.y = 0.25f;
                rb.AddForce(knockDir * 12f * knockbackMult, ForceMode.Impulse);
            }
        }

        // 3. Charged Shockwave
        float shockwaveDmg = playerStats.GetValue(StatType.MaceShockwaveDamage);
        if (shockwaveDmg > 0f && playerAttack != null && playerAttack.ChargeLevel >= 1)
        {
            Vector3 origin = hitPoint != Vector3.zero ? hitPoint : transform.position + transform.forward * 1.5f;
            TriggerShockwave(origin, shockwaveDmg);
        }
    }

    private void TriggerShockwave(Vector3 position, float damage)
    {
        if (shockwaveVfxPrefab != null)
        {
            Instantiate(shockwaveVfxPrefab, position, Quaternion.identity);
        }
        if (shockwaveSfx != null)
        {
            AudioSource.PlayClipAtPoint(shockwaveSfx, position);
        }

        Collider[] hits = Physics.OverlapSphere(position, 4.5f);
        HashSet<Health> processed = new HashSet<Health>();

        foreach (var col in hits)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead && h.transform.root != transform.root)
            {
                if (processed.Add(h))
                {
                    Damage waveDmg = new Damage
                    {
                        value = damage,
                        source = playerStats != null ? playerStats.GetComponent<Health>() : null,
                        sourcePosition = position,
                        unparryable = true
                    };
                    h.ReceiveDamage(waveDmg);

                    if (h.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                    {
                        Vector3 pushDir = (h.transform.position - position).normalized;
                        pushDir.y = 0.3f;
                        rb.AddForce(pushDir * 8f, ForceMode.Impulse);
                    }
                }
            }
        }
    }
}
