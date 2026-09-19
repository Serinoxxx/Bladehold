using System;
using System.Collections;
using System.Collections.Generic;
using HighlightPlus;
using UnityEngine;

/// <summary>
///     Master controller for Clan Captains (e.g. Captain Fraglob).
///     Summoned when Enraged, Nightmare, or Omega War Banners are chosen.
///     Commands the horde with:
///     - Rallying War Cry (AoE minion speed/damage buff)
///     - Seismic Stomp (Telegraphed ground slam with knockback)
///     - Bulwark Stance (Shield barrier triggered on high difficulty tiers)
///     - Morale Break on defeat (stuns nearby goblins)
/// </summary>
public class CaptainEnemyController : MonoBehaviour
{
    [Header("Captain Identity")]
    [SerializeField] private string captainName = "Captain Fraglob";
    [SerializeField] private BannerDifficultyTier difficultyTier = BannerDifficultyTier.Standard;

    [Header("Core Dependencies")]
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private AIAttack attack;
    [SerializeField] private Animator animator;
    [SerializeField] private HighlightEffect highlightEffect;

    [Header("Base Stat Tuning")]
    [SerializeField] private float baseMaxHealth = 250f;
    [SerializeField] private float baseDamage = 20f;
    [SerializeField] private float baseMoveSpeed = 4.2f;

    [Header("Ability 1: Rallying War Cry")]
    [SerializeField] private float warCryCooldown = 12f;
    [SerializeField] private float warCryRadius = 12f;
    [SerializeField] private float warCryDuration = 6f;
    [SerializeField] private float warCrySpeedBuffPercent = 0.30f;
    [SerializeField] private float warCryDamageBuffPercent = 0.25f;
    [SerializeField] private AudioClip warCrySfx;

    [Header("Ability 2: Seismic Stomp")]
    [SerializeField] private float stompCooldown = 9f;
    [SerializeField] private float stompRange = 6.5f;
    [SerializeField] private float stompRadius = 5f;
    [SerializeField] private float stompWindup = 1.2f;
    [SerializeField] private float baseStompDamage = 25f;
    [SerializeField] private float stompKnockbackForce = 12f;
    [SerializeField] private GameObject stompTelegraphPrefab;
    [SerializeField] private AudioClip stompSfx;

    [Header("Ability 3: Bulwark Shield (Nightmare & Omega)")]
    [SerializeField] private float bulwarkThresholdHealthPercent = 0.5f;
    [SerializeField] private float bulwarkShieldAmount = 150f;
    private bool bulwarkUsed = false;
    private float currentBulwarkShield = 0f;

    private float warCryTimer = 3f; // Initial delay
    private float stompTimer = 5f;  // Initial delay
    private bool isPerformingAbility = false;

    public string CaptainName => captainName;
    public BannerDifficultyTier DifficultyTier => difficultyTier;
    public float CurrentBulwarkShield => currentBulwarkShield;
    public bool IsBulwarkActive => currentBulwarkShield > 0f;

    public event Action<CaptainEnemyController> OnCaptainDied;

    private void Awake()
    {
        EnsureDependencies();
    }

    private void Start()
    {
        EnsureDependencies();
        if (health != null)
        {
            health.OnDied -= HandleDeath;
            health.OnDied += HandleDeath;
            health.TryBlockDamage -= HandleTryBlockDamage;
            health.TryBlockDamage += HandleTryBlockDamage;
        }

        ApplyDifficultyScaling();
    }

    private void EnsureDependencies()
    {
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<AIMovement>();
        if (attack == null) attack = GetComponent<AIAttack>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (highlightEffect == null) highlightEffect = GetComponentInChildren<HighlightEffect>();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDeath;
            health.TryBlockDamage -= HandleTryBlockDamage;
        }
    }

    /// <summary>
    ///     Initializes or dynamically configures the captain with a specific tier and custom name.
    /// </summary>
    public void Initialize(BannerDifficultyTier tier, string customName = null)
    {
        EnsureDependencies();
        difficultyTier = tier;
        if (!string.IsNullOrEmpty(customName))
        {
            captainName = customName;
        }

        ApplyDifficultyScaling();
    }

    private void ApplyDifficultyScaling()
    {
        float multiplier = BannerDifficultyHelper.GetStatMultiplier(difficultyTier);

        if (health != null)
        {
            float scaledHp = baseMaxHealth * multiplier;
            health.SetMaxHealth(scaledHp);
        }

        if (movement != null)
        {
            movement.SetSpeed(baseMoveSpeed * Mathf.Min(1.25f, 1f + (multiplier - 1f) * 0.4f));
        }

        if (attack != null)
        {
            attack.SetDamage(baseDamage * multiplier);
        }

        if (highlightEffect != null)
        {
            Color tierCol = BannerDifficultyHelper.GetTierColor(difficultyTier);
            highlightEffect.outlineColor = tierCol;
            highlightEffect.glowHQColor = tierCol;
            highlightEffect.highlighted = true;
            highlightEffect.Refresh();
        }
    }

    private void Update()
    {
        if (health == null || health.IsDead || isPerformingAbility) return;

        warCryTimer -= Time.deltaTime;
        stompTimer -= Time.deltaTime;

        // Check Bulwark Shield trigger for Nightmare / Omega tiers
        if (!bulwarkUsed && (difficultyTier >= BannerDifficultyTier.Nightmare) && health.CurrentHealth <= (health.MaxHealth * bulwarkThresholdHealthPercent))
        {
            ActivateBulwarkShield();
        }

        // Ability 1: Rallying War Cry
        if (warCryTimer <= 0f)
        {
            warCryTimer = warCryCooldown;
            StartCoroutine(PerformWarCryRoutine());
            return;
        }

        // Ability 2: Seismic Stomp (triggered when player is within range)
        if (stompTimer <= 0f && Player.Instance != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, Player.Instance.transform.position);
            if (distToPlayer <= stompRange)
            {
                stompTimer = stompCooldown;
                StartCoroutine(PerformSeismicStompRoutine());
            }
        }
    }

    private void ActivateBulwarkShield()
    {
        bulwarkUsed = true;
        float mult = BannerDifficultyHelper.GetStatMultiplier(difficultyTier);
        currentBulwarkShield = bulwarkShieldAmount * mult;

        Debug.Log($"[CaptainEnemyController] {captainName} activated Bulwark Shield ({currentBulwarkShield} HP)!");

        if (highlightEffect != null)
        {
            highlightEffect.outlineColor = Color.cyan;
            highlightEffect.glowHQColor = Color.blue;
            highlightEffect.Refresh();
        }
    }

    private bool HandleTryBlockDamage(Damage dmg)
    {
        if (currentBulwarkShield > 0f)
        {
            if (currentBulwarkShield >= dmg.value)
            {
                currentBulwarkShield -= dmg.value;
                return true; // Completely absorbed
            }
            else
            {
                dmg.value -= currentBulwarkShield;
                currentBulwarkShield = 0f;
                Debug.Log($"[CaptainEnemyController] {captainName}'s Bulwark Shield shattered!");
                if (highlightEffect != null)
                {
                    highlightEffect.outlineColor = BannerDifficultyHelper.GetTierColor(difficultyTier);
                    highlightEffect.Refresh();
                }
                return false; // Partially absorbed, remainder passes through
            }
        }
        return false;
    }

    private IEnumerator PerformWarCryRoutine()
    {
        isPerformingAbility = true;
        if (movement != null) movement.SetMovementPaused(true);

        if (animator != null)
        {
            animator.SetTrigger("Cheer");
        }

        Debug.Log($"[CaptainEnemyController] {captainName} unleashes RALLYING WAR CRY!");

        // Buff nearby allied minions
        Collider[] colliders = Physics.OverlapSphere(transform.position, warCryRadius);
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            if (col.CompareTag("Player")) continue;

            AIMovement minionMove = col.GetComponentInParent<AIMovement>();
            AIAttack minionAttack = col.GetComponentInParent<AIAttack>();
            Health minionHealth = col.GetComponentInParent<Health>();

            if (minionHealth != null && !minionHealth.IsDead && minionMove != null)
            {
                StartCoroutine(ApplyMinionWarCryBuff(minionMove, minionAttack, warCryDuration));
            }
        }

        yield return new WaitForSeconds(1.2f);

        if (movement != null) movement.SetMovementPaused(false);
        isPerformingAbility = false;
    }

    private IEnumerator ApplyMinionWarCryBuff(AIMovement minionMove, AIAttack minionAttack, float duration)
    {
        if (minionMove == null) yield break;

        minionMove.SetSpeedMultiplier(1f + warCrySpeedBuffPercent);
        if (minionAttack != null) minionAttack.SetDamageMultiplier(1f + warCryDamageBuffPercent);

        yield return new WaitForSeconds(duration);

        if (minionMove != null) minionMove.SetSpeedMultiplier(1f);
        if (minionAttack != null) minionAttack.SetDamageMultiplier(1f);
    }

    private IEnumerator PerformSeismicStompRoutine()
    {
        isPerformingAbility = true;
        if (movement != null) movement.SetMovementPaused(true);

        Vector3 stompPosition = transform.position;

        // Visual telegraph (or fallback)
        GameObject telegraphInstance = null;
        if (stompTelegraphPrefab != null)
        {
            telegraphInstance = Instantiate(stompTelegraphPrefab, stompPosition, Quaternion.identity);
        }

        Debug.Log($"[CaptainEnemyController] {captainName} preparing Seismic Stomp...");
        yield return new WaitForSeconds(stompWindup);

        if (telegraphInstance != null)
        {
            Destroy(telegraphInstance);
        }

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        // Deal shockwave damage & knockback if player is inside radius
        if (Player.Instance != null && Player.Instance.Health != null && !Player.Instance.Health.IsDead)
        {
            float dist = Vector3.Distance(transform.position, Player.Instance.transform.position);
            if (dist <= stompRadius)
            {
                float mult = BannerDifficultyHelper.GetStatMultiplier(difficultyTier);
                float finalDamage = baseStompDamage * mult;

                Damage dmg = new Damage
                {
                    value = finalDamage,
                    sourcePosition = transform.position,
                    knockbackForce = stompKnockbackForce,
                    unparryable = true
                };

                Player.Instance.Health.ReceiveDamage(dmg);
                Debug.Log($"[CaptainEnemyController] Seismic Stomp HIT player for {finalDamage} damage!");
            }
        }

        yield return new WaitForSeconds(0.6f);

        if (movement != null) movement.SetMovementPaused(false);
        isPerformingAbility = false;
    }

    private void HandleDeath()
    {
        Debug.Log($"[CaptainEnemyController] {captainName} DEFEATED!");

        // Morale Break: stagger/halt nearby enemies for 2 seconds
        Collider[] colliders = Physics.OverlapSphere(transform.position, 15f);
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject || col.CompareTag("Player")) continue;
            AIMovement m = col.GetComponentInParent<AIMovement>();
            if (m != null)
            {
                StartCoroutine(StaggerMinionRoutine(m, 2.0f));
            }
        }

        // Drop bonus resources based on tier
        int bonusGold = 50 * BannerDifficultyHelper.GetRewardMultiplier(difficultyTier);
        int bonusBlood = 3 * BannerDifficultyHelper.GetRewardMultiplier(difficultyTier);
        RunSession.AddInRunGold(bonusGold);
        RunSession.AddGoblinBlood(bonusBlood);

        OnCaptainDied?.Invoke(this);
    }

    private IEnumerator StaggerMinionRoutine(AIMovement m, float duration)
    {
        if (m == null) yield break;
        m.SetMovementPaused(true);
        yield return new WaitForSeconds(duration);
        if (m != null) m.SetMovementPaused(false);
    }
}
