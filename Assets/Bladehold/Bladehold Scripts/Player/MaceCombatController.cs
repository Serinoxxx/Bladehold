using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Handles passive combat mechanics for the 2H Mace:
///     - Concussive Stun on hit (via SlowStatus 100% slow; AIAttack won't swing while stunned)
///     - Armor Shatter feedback (the damage bonus itself is applied in DamageTrigger)
///     - Ground shockwaves when releasing fully charged strikes (once per swing)
///     Colossal Force's knockback is applied in DamageTrigger by scaling Damage.knockbackForce, so
///     KnockbackReceiver handles the slide/knockdown/fling and its own feedbacks.
/// </summary>
public class MaceCombatController : MonoBehaviour
{
    // "Heavy" for Armor Shatter: knockback resistance of a brute or tougher (brute 3, big ork / knight /
    // bulwark / dome warden 4, trolls and bosses 50), or a beefy health pool (spearman / bulwark 40+).
    private const float HeavyKnockbackResistance = 3f;
    private const float HeavyMaxHealth = 40f;
    private const float ShockwaveRadius = 4.5f;

    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerAttack playerAttack;
    [Tooltip("MMF_Player played at the shockwave origin (rock burst + impact sound). Its particle feedback must use the Script position mode.")]
    [SerializeField] private MMF_Player shockwaveFeedback;
    [Tooltip("Optional: played at the hit point when Concussive Impact stuns an enemy (dizzy burst + thud). Leave empty for silence.")]
    [SerializeField] private MMF_Player stunFeedback;
    [Tooltip("Optional: played at the hit point when Armor Shatter's bonus applies (armor crack + metal crunch). Leave empty for silence.")]
    [SerializeField] private MMF_Player armorShatterFeedback;

    private DamageTrigger subscribedTrigger;
    private bool shockwaveFiredThisSwing;
    private int enemyLayerMask;

    /// <summary>True while the equipped melee weapon is the mace (gates the mace-only draft cards).</summary>
    public static bool IsMaceEquipped =>
        PlayerWeaponManager.Instance != null &&
        string.Equals(PlayerWeaponManager.Instance.CurrentMeleeId, "mace", StringComparison.OrdinalIgnoreCase);

    /// <summary>Armor Shatter's target test: shielded (bubble / Bulwark shield) or heavy.</summary>
    public static bool IsArmorShatterTarget(Component target)
    {
        if (target == null)
        {
            return false;
        }
        if (DamageTrigger.IsShieldedTarget(target))
        {
            return true;
        }

        Health health = target.GetComponentInParent<Health>();
        if (health == null)
        {
            return false;
        }
        if (health.MaxHealth >= HeavyMaxHealth)
        {
            return true;
        }
        KnockbackReceiver knockback = health.GetComponent<KnockbackReceiver>();
        return knockback != null && knockback.CurrentResistance >= HeavyKnockbackResistance;
    }

    private void Awake()
    {
        if (playerStats == null) playerStats = GetComponentInParent<PlayerStats>() ?? GetComponentInChildren<PlayerStats>();
        if (playerAttack == null) playerAttack = GetComponentInParent<PlayerAttack>() ?? GetComponentInChildren<PlayerAttack>();
        enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    private void Start()
    {
        if (shockwaveFeedback == null) Debug.LogError("MaceCombatController: shockwaveFeedback is not assigned.", this);

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
        subscribedTrigger.OnActivated += HandleSwingStarted;
    }

    private void UnhookTrigger()
    {
        if (subscribedTrigger != null)
        {
            subscribedTrigger.OnHit -= HandleMeleeHit;
            subscribedTrigger.OnActivated -= HandleSwingStarted;
            subscribedTrigger = null;
        }
    }

    private void HandleSwingStarted()
    {
        shockwaveFiredThisSwing = false;
    }

    private void HandleMeleeHit(IDamageable target, Damage damageDealt, Vector3 hitPoint)
    {
        if (!IsMaceEquipped || playerStats == null)
        {
            return;
        }

        Component comp = target as Component;

        // 1. Armor Shatter feedback (bonus damage already applied by DamageTrigger)
        if (armorShatterFeedback != null && comp != null &&
            playerStats.GetValue(StatType.MaceArmorShatterBonus) > 0f && IsArmorShatterTarget(comp))
        {
            armorShatterFeedback.PlayFeedbacks(hitPoint);
        }

        // 2. Concussive Stun
        float stunDuration = playerStats.GetValue(StatType.MaceStunDuration);
        if (stunDuration > 0f && comp != null)
        {
            SlowStatus slow = SlowStatus.GetOrAdd(comp);
            if (slow != null)
            {
                bool wasStunned = slow.IsStunned;
                slow.ApplySlow(1.0f, stunDuration);
                if (slow.TryGetComponent<NavMeshAgent>(out var agent) && agent.isOnNavMesh)
                {
                    agent.velocity = Vector3.zero;
                }
                // Only on a fresh stun, so a cleave through a stunned pack doesn't stack the sound.
                if (!wasStunned && stunFeedback != null)
                {
                    stunFeedback.PlayFeedbacks(hitPoint);
                }
            }
        }

        // 3. Charged Shockwave: once per fully charged swing, not once per enemy the swing hits.
        float shockwaveDmg = playerStats.GetValue(StatType.MaceShockwaveDamage);
        if (shockwaveDmg > 0f && !shockwaveFiredThisSwing && playerAttack != null &&
            playerAttack.ChargeLevel >= Mathf.Max(1, playerAttack.MaxChargeLevels))
        {
            shockwaveFiredThisSwing = true;
            Vector3 origin = hitPoint != Vector3.zero ? hitPoint : transform.position + transform.forward * 1.5f;
            TriggerShockwave(origin, shockwaveDmg);
        }
    }

    private void TriggerShockwave(Vector3 position, float damage)
    {
        if (shockwaveFeedback != null)
        {
            shockwaveFeedback.PlayFeedbacks(position);
        }

        float allDamage = playerStats.GetValue(StatType.AllDamageMultiplier);
        float finalDamage = damage * (allDamage > 0f ? allDamage : 1f);
        float knockback = finalDamage * playerStats.GetValue(StatType.KnockbackForce) *
                          (1f + Mathf.Max(0f, playerStats.GetValue(StatType.MaceKnockbackMultiplier)));
        IDamageable source = Player.Instance != null ? Player.Instance.Damageable : null;
        string element = RunSession.GetActiveElement("SLOT_MELEE");

        // Enemy layer only: the castle Gate, walls and towers have Health but must not be hit.
        Collider[] hits = Physics.OverlapSphere(position, ShockwaveRadius, enemyLayerMask, QueryTriggerInteraction.Collide);
        HashSet<Health> processed = new HashSet<Health>();

        foreach (var col in hits)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead || h.transform.root == transform.root || !processed.Add(h))
            {
                continue;
            }

            h.ReceiveDamage(new Damage
            {
                value = finalDamage,
                type = DamageType.blunt,
                source = source,
                sourcePosition = position,
                knockbackForce = knockback,
                isPlayerDamage = true,
                elementId = element,
                unparryable = true
            });
        }
    }
}
