using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Physical shield component attached to the Bulwark enemy's shield GameObject.
///     Has a dedicated box collider to detect and intercept attacks in front of the Bulwark.
///     Implements IDamageable so incoming sweeps and projectiles strike the shield directly,
///     and IShieldBlocker so melee sweeps are stopped immediately (triggering cut-through stop and recoil).
///     Projectiles deal only 10% damage to the shield. When shield health reaches 0, the shield breaks,
///     disabling blocking permanently for this enemy.
/// </summary>
public class BulwarkShield : MonoBehaviour, IDamageable, IShieldBlocker
{
    [SerializeField] private BulwarkAttackSO config;
    [SerializeField] private BulwarkAttack bulwarkAttack;
    [SerializeField] private Collider shieldCollider;
    [SerializeField] private Renderer shieldRenderer;
    [SerializeField] private MMF_Player blockFeedback;
    [SerializeField] private MMF_Player breakFeedback;

    public event Action<Damage, Vector3> OnBlocked;
    public event Action OnBroken;

    private float currentHp;
    private bool isBroken = false;
    private bool anyError = false;

    public float CurrentHp => currentHp;
    public float MaxHp => config != null ? config.shieldMaxHp : 40f;
    public bool IsBroken => isBroken;

    private void OnValidate()
    {
        if (shieldCollider == null)
        {
            shieldCollider = GetComponent<Collider>();
        }
        if (shieldRenderer == null)
        {
            shieldRenderer = GetComponent<Renderer>();
        }
        if (bulwarkAttack == null)
        {
            bulwarkAttack = GetComponentInParent<BulwarkAttack>();
        }
    }

    private void Awake()
    {
        if (config != null)
        {
            currentHp = config.shieldMaxHp;
        }
        else
        {
            currentHp = 40f;
        }
    }

    public void Initialize()
    {
        Health parentHealth = GetComponentInParent<Health>();
        if (parentHealth != null)
        {
            parentHealth.OnDied -= HandleParentDied;
            parentHealth.OnDied += HandleParentDied;
            parentHealth.TryBlockDamage -= HandleParentTryBlockDamage;
            parentHealth.TryBlockDamage += HandleParentTryBlockDamage;
        }
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        Health parentHealth = GetComponentInParent<Health>();
        if (parentHealth != null)
        {
            parentHealth.OnDied -= HandleParentDied;
            parentHealth.TryBlockDamage -= HandleParentTryBlockDamage;
        }
    }

    private void Start()
    {
        if (shieldCollider == null)
        {
            Debug.LogError("[BulwarkShield] Collider is missing on shield GameObject.");
            anyError = true;
        }
        if (bulwarkAttack == null)
        {
            bulwarkAttack = GetComponentInParent<BulwarkAttack>();
        }

        if (anyError)
        {
            return;
        }
    }

    private void OnDestroy()
    {
        Health parentHealth = GetComponentInParent<Health>();
        if (parentHealth != null)
        {
            parentHealth.OnDied -= HandleParentDied;
            parentHealth.TryBlockDamage -= HandleParentTryBlockDamage;
        }
    }

    private void HandleParentDied()
    {
        enabled = false;
    }

    private bool HandleParentTryBlockDamage(Damage damage)
    {
        if (isBroken || !enabled)
        {
            return false;
        }

        // Attacks from behind bypass the shield and strike the Bulwark's health directly
        if (IsHitFromBehind(damage))
        {
            return false;
        }

        // Attack from front: intercept and absorb damage into the shield
        ReceiveDamage(damage);
        return true;
    }

    /// <summary>
    ///     Determines whether an attack is a backstab targeting the Bulwark from behind,
    ///     using the project's existing backstab detection rule (facing alignment Dot > 0.4f).
    /// </summary>
    public bool IsHitFromBehind(Damage damage)
    {
        // 1. Direct check if the attack was already stamped as a backstab
        if (damage != null && damage.isBackstab)
        {
            return true;
        }

        Transform rootTransform = transform.root;
        Vector3 targetFwd = rootTransform.forward;
        targetFwd.y = 0f;

        // 2. Existing backstab detection: check attacker facing alignment vs target facing (Dot > 0.4f)
        Vector3 attackerFwd = Vector3.zero;
        if (damage != null && damage.source is Component comp)
        {
            attackerFwd = comp.transform.forward;
        }
        else if (Player.Instance != null)
        {
            attackerFwd = Player.Instance.transform.forward;
        }
        else if (damage != null && damage.direction.sqrMagnitude > 0.001f)
        {
            attackerFwd = damage.direction;
        }

        attackerFwd.y = 0f;
        if (attackerFwd.sqrMagnitude > 0.001f && targetFwd.sqrMagnitude > 0.001f)
        {
            if (Vector3.Dot(attackerFwd.normalized, targetFwd.normalized) > 0.4f)
            {
                return true;
            }
        }

        // 3. Positional fallback: check if attack origin is behind the target
        Vector3 attackerPos = Vector3.zero;
        if (damage != null && damage.sourcePosition != Vector3.zero)
        {
            attackerPos = damage.sourcePosition;
        }
        else if (damage != null && damage.source is Component sourceComp)
        {
            attackerPos = sourceComp.transform.position;
        }
        else if (Player.Instance != null)
        {
            attackerPos = Player.Instance.transform.position;
        }

        if (attackerPos != Vector3.zero)
        {
            Vector3 toAttacker = attackerPos - rootTransform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude > 0.001f)
            {
                float dot = Vector3.Dot(targetFwd.normalized, toAttacker.normalized);
                if (dot < 0f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns whether this shield actively blocks attacks.
    /// </summary>
    public bool ShouldBlockAttack(Damage damage)
    {
        if (isBroken || !enabled)
        {
            return false;
        }

        // Attacks from behind are not blocked by the shield!
        if (IsHitFromBehind(damage))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Receives incoming damage from melee strikes or projectiles hitting the shield collider.
    /// </summary>
    public void ReceiveDamage(Damage damage)
    {
        if (isBroken || !enabled)
        {
            return;
        }

        // If the attack is from behind, damage the Bulwark, NOT the shield!
        if (IsHitFromBehind(damage))
        {
            Health parentHealth = GetComponentInParent<Health>();
            if (parentHealth != null)
            {
                parentHealth.ReceiveDamage(damage);
            }
            return;
        }

        float appliedDamage = damage != null ? damage.value : 0f;

        // Projectiles deal only 10% damage to shields.
        if (damage != null && damage.isProjectile)
        {
            float mult = config != null ? config.projectileDamageMultiplier : 0.1f;
            appliedDamage *= mult;
        }

        currentHp -= appliedDamage;

        Vector3 hitPos = damage != null && damage.sourcePosition != Vector3.zero ? damage.sourcePosition : transform.position;

        if (currentHp <= 0f)
        {
            BreakShield();
        }
        else
        {
            if (blockFeedback != null)
            {
                blockFeedback.PlayFeedbacks();
            }

            OnBlocked?.Invoke(damage, hitPos);

            if (bulwarkAttack != null)
            {
                bulwarkAttack.HandleShieldBlocked(damage);
            }
        }
    }

    /// <summary>
    ///     Destroys the shield when durability is depleted: disables collider, plays break feedback,
    ///     and hides the shield mesh so the enemy can no longer block.
    /// </summary>
    public void BreakShield()
    {
        if (isBroken)
        {
            return;
        }

        isBroken = true;
        currentHp = 0f;

        if (shieldCollider != null)
        {
            shieldCollider.enabled = false;
        }

        if (shieldRenderer != null)
        {
            shieldRenderer.enabled = false;
        }

        if (breakFeedback != null)
        {
            breakFeedback.PlayFeedbacks();
        }

        OnBroken?.Invoke();

        if (bulwarkAttack != null)
        {
            bulwarkAttack.HandleShieldBroken();
        }
    }

    /// <summary>
    ///     Allows configuring custom shield health (useful for tests or CSV overrides).
    /// </summary>
    public void SetShieldHp(float hp)
    {
        currentHp = hp;
        isBroken = currentHp <= 0f;
        if (shieldCollider != null)
        {
            shieldCollider.enabled = !isBroken;
        }
        if (shieldRenderer != null)
        {
            shieldRenderer.enabled = !isBroken;
        }
    }
}
