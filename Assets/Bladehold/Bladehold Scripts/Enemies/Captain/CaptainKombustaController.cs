using System;
using System.Collections;
using System.Collections.Generic;
using HighlightPlus;
using UnityEngine;

/// <summary>
///     Master controller for Clan Captain: Captain Kombusta.
///     Specializes in:
///     1. Dynamite Volley: When player is at distance >= 5m, throws 10 dynamite sticks, one every second.
///        Each dynamite detonates in a telegraphed 2m radius causing explosion and 20 damage.
///     2. Self-Immolation: If the player gets in melee range (<= 3.5m) for 3 continuous seconds,
///        he sets himself on fire, burning the player over time as long as they stay in range.
///     3. Clan Captain Hierarchy: Scales with War Banner difficulty tier, Morale Break on defeat, bonus rewards.
/// </summary>
public class CaptainKombustaController : MonoBehaviour
{
    [Header("Captain Identity")]
    [SerializeField] private string captainName = "Captain Kombusta";
    [SerializeField] private BannerDifficultyTier difficultyTier = BannerDifficultyTier.Standard;

    [Header("Core Dependencies")]
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private AIAttack attack;
    [SerializeField] private Animator animator;
    [SerializeField] private HighlightEffect highlightEffect;
    [SerializeField] private Transform firePoint;

    [Header("Attack Configuration")]
    [SerializeField] private CaptainKombustaSO attackData;

    private float? damageOverride;
    private float meleeTimer = 0f;
    private bool isOnFire = false;
    private float burnDurationTimer = 0f;
    private float burnTickTimer = 0f;

    private float nextDynamiteSpecialTime = 0f;
    private bool isPerformingSpecial = false;
    private GameObject activeFireVfxInstance;

    private Transform playerTransform;
    private Health playerHealth;

    public string CaptainName => captainName;
    public BannerDifficultyTier DifficultyTier => difficultyTier;
    public bool IsOnFire => isOnFire;
    public bool IsPerformingSpecial => isPerformingSpecial;
    public float MeleeTimer => meleeTimer;

    public event Action<CaptainKombustaController> OnCaptainDied;

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
        }

        Player playerInstance = Player.Instance;
        if (playerInstance != null)
        {
            playerTransform = playerInstance.transform;
            if (playerInstance.Health != null)
            {
                playerHealth = playerInstance.Health;
            }
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

        if (firePoint == null)
        {
            Transform found = transform.Find("Dynamite Fire Point");
            if (found != null) firePoint = found;
            else firePoint = transform;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDeath;
        }

        if (activeFireVfxInstance != null)
        {
            Destroy(activeFireVfxInstance);
        }
    }

    public void SetDamage(float value)
    {
        damageOverride = value;
        if (attack != null)
        {
            attack.SetDamage(value);
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

        float baseHp = attackData != null ? attackData.baseMaxHealth : 350f;
        float baseDmg = attackData != null ? attackData.baseMeleeDamage : 15f;
        float baseSpeed = attackData != null ? attackData.baseMoveSpeed : 3.8f;

        if (health != null)
        {
            float scaledHp = baseHp * multiplier;
            health.SetMaxHealth(scaledHp);
        }

        if (movement != null)
        {
            movement.SetSpeed(baseSpeed * Mathf.Min(1.25f, 1f + (multiplier - 1f) * 0.35f));
        }

        if (attack != null)
        {
            attack.SetDamage((damageOverride ?? baseDmg) * multiplier);
        }

        if (highlightEffect != null && !isOnFire)
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
        if (health == null || health.IsDead) return;

        // Resolve player reference if missing
        if (playerTransform == null && Player.Instance != null)
        {
            playerTransform = Player.Instance.transform;
            playerHealth = Player.Instance.Health;
        }

        if (playerTransform == null || (playerHealth != null && playerHealth.IsDead)) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 1. Long-Range Special Attack: 10 Dynamite Volley (at distance >= 5m)
        float triggerDistance = attackData != null ? attackData.dynamiteTriggerDistance : 5.0f;
        if (!isPerformingSpecial && distToPlayer >= triggerDistance && Time.time >= nextDynamiteSpecialTime)
        {
            StartCoroutine(PerformDynamiteVolleyRoutine());
        }

        // 2. Close-Range Proximity & Self-Immolation
        UpdateMeleeProximityAndBurn(distToPlayer);
    }

    private void UpdateMeleeProximityAndBurn(float distToPlayer)
    {
        float meleeThreshold = attackData != null ? attackData.meleeRangeThreshold : 3.5f;
        float igniteDelay = attackData != null ? attackData.meleeIgniteDelay : 3.0f;
        bool inMeleeRange = distToPlayer <= meleeThreshold;

        if (!isOnFire)
        {
            if (inMeleeRange)
            {
                meleeTimer += Time.deltaTime;
                if (meleeTimer >= igniteDelay)
                {
                    IgniteSelf();
                }
            }
            else
            {
                meleeTimer = Mathf.Max(0f, meleeTimer - Time.deltaTime);
            }
        }
        else
        {
            // Captain is actively on fire!
            burnDurationTimer -= Time.deltaTime;
            if (burnDurationTimer <= 0f)
            {
                ExtinguishFire();
                return;
            }

            // Burn player over time IF they stay in range
            float auraRadius = attackData != null ? attackData.burnAuraRadius : 4.0f;
            if (distToPlayer <= auraRadius)
            {
                burnTickTimer -= Time.deltaTime;
                if (burnTickTimer <= 0f)
                {
                    float interval = attackData != null ? attackData.burnTickInterval : 0.5f;
                    burnTickTimer = interval;
                    DealBurnTickToPlayer();
                }
            }
            else
            {
                burnTickTimer = 0f;
            }
        }
    }

    /// <summary>
    ///     Executes the special attack: throwing 10 dynamite sticks, one every second.
    ///     Each dynamite explodes with a 2m radius dealing 20 damage.
    /// </summary>
    private IEnumerator PerformDynamiteVolleyRoutine()
    {
        isPerformingSpecial = true;
        if (movement != null) movement.SetMovementPaused(true);

        int count = attackData != null ? attackData.dynamiteCount : 10;
        float flightTime = attackData != null ? attackData.dynamiteFlightTime : 2.0f;
        float interval = attackData != null ? attackData.dynamiteInterval : 3.0f;
        // Enforce at least 1.0s gap after telegraph finishes before next bomb
        interval = Mathf.Max(interval, flightTime + 1.0f);

        float damage = damageOverride ?? (attackData != null ? attackData.dynamiteDamage : 20.0f);
        float radius = attackData != null ? attackData.dynamiteExplosionRadius : 2.0f;
        float arcHeight = attackData != null ? attackData.dynamiteArcHeight : 2.5f;
        float knockback = attackData != null ? attackData.explosionKnockback : 8.0f;

        // Banner multiplier scaling for dynamite damage
        float mult = BannerDifficultyHelper.GetStatMultiplier(difficultyTier);
        float scaledDamage = damage * mult;

        Debug.Log($"[CaptainKombustaController] {captainName} unleashes DYNAMITE VOLLEY ({count} dynamite sticks, {flightTime}s telegraph + {interval - flightTime}s cooldown)!");

        for (int i = 0; i < count; i++)
        {
            if (health == null || health.IsDead || playerTransform == null || (playerHealth != null && playerHealth.IsDead))
            {
                break;
            }

            // Aim towards player
            Vector3 aimDir = (playerTransform.position - transform.position);
            aimDir.y = 0;
            if (aimDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(aimDir);
            }

            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }

            // Target ground position at player's location, ignoring player/enemies so we hit the actual terrain/ground
            Vector3 targetGround = playerTransform.position;
            Vector3 groundNormal = Vector3.up;

            int characterMask = LayerMask.GetMask("Player", "Enemy", "Ignore Raycast");
            int groundMask = ~characterMask;

            if (Physics.Raycast(targetGround + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
            {
                targetGround = hit.point;
                groundNormal = hit.normal;
            }
            else if (Physics.Raycast(targetGround + Vector3.up * 0.5f, Vector3.down, out RaycastHit hitClose, 5f, groundMask, QueryTriggerInteraction.Ignore))
            {
                targetGround = hitClose.point;
                groundNormal = hitClose.normal;
            }

            Vector3 spawnPos = firePoint != null ? firePoint.position : (transform.position + Vector3.up * 1.5f + transform.forward * 0.5f);

            // Spawn dynamite projectile
            GameObject dynObj = null;
            if (attackData != null && attackData.dynamitePrefab != null)
            {
                dynObj = Instantiate(attackData.dynamitePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                dynObj = new GameObject("DynamiteProjectileInstance");
            }

            DynamiteProjectile projectile = dynObj.GetComponent<DynamiteProjectile>();
            if (projectile == null)
            {
                projectile = dynObj.AddComponent<DynamiteProjectile>();
            }

            GameObject telegraphPrefab = attackData != null ? attackData.telegraphPrefab : null;
            GameObject vfxPrefab = attackData != null ? attackData.explosionVfxPrefab : null;
            AudioClip sfxExplosion = attackData != null ? attackData.explosionSfx : null;
            AudioClip sfxFuse = attackData != null ? attackData.fuseSfx : null;

            projectile.Launch(
                spawnPos,
                targetGround,
                flightTime,
                arcHeight,
                radius,
                scaledDamage,
                knockback,
                health,
                telegraphPrefab,
                vfxPrefab,
                sfxExplosion,
                sfxFuse,
                groundNormal
            );

            yield return new WaitForSeconds(interval);
        }

        if (movement != null) movement.SetMovementPaused(false);
        float cooldown = attackData != null ? attackData.dynamiteSpecialCooldown : 12.0f;
        nextDynamiteSpecialTime = Time.time + cooldown;
        isPerformingSpecial = false;
    }

    /// <summary>
    ///     Lights Captain Kombusta on fire after staying in melee range for 3 seconds.
    /// </summary>
    public void IgniteSelf()
    {
        if (isOnFire) return;

        isOnFire = true;
        meleeTimer = 0f;
        burnDurationTimer = attackData != null ? attackData.burnDuration : 8.0f;
        burnTickTimer = 0f;

        Debug.Log($"[CaptainKombustaController] {captainName} IGNITED HIMSELF ON FIRE!");

        // Spawn fire particle effect attached to Kombusta
        if (attackData != null && attackData.fireAuraVfxPrefab != null)
        {
            activeFireVfxInstance = Instantiate(attackData.fireAuraVfxPrefab, transform);
            activeFireVfxInstance.transform.localPosition = new Vector3(0f, 1f, 0f);
        }

        if (highlightEffect != null)
        {
            highlightEffect.outlineColor = new Color(1f, 0.3f, 0.0f);
            highlightEffect.glowHQColor = new Color(1f, 0.1f, 0.0f);
            highlightEffect.Refresh();
        }

        if (attackData != null && attackData.igniteSfx != null)
        {
            AudioSource.PlayClipAtPoint(attackData.igniteSfx, transform.position, 1.0f);
        }
    }

    /// <summary>
    ///     Extinguishes the fire aura after its duration expires.
    /// </summary>
    public void ExtinguishFire()
    {
        if (!isOnFire) return;

        isOnFire = false;
        meleeTimer = 0f;

        if (activeFireVfxInstance != null)
        {
            Destroy(activeFireVfxInstance);
            activeFireVfxInstance = null;
        }

        if (highlightEffect != null)
        {
            Color tierCol = BannerDifficultyHelper.GetTierColor(difficultyTier);
            highlightEffect.outlineColor = tierCol;
            highlightEffect.glowHQColor = tierCol;
            highlightEffect.Refresh();
        }

        Debug.Log($"[CaptainKombustaController] {captainName}'s fire extinguished.");
    }

    private void DealBurnTickToPlayer()
    {
        if (playerTransform == null || playerHealth == null || playerHealth.IsDead) return;

        float dps = attackData != null ? attackData.burnDamagePerSecond : 10.0f;
        float interval = attackData != null ? attackData.burnTickInterval : 0.5f;
        float mult = BannerDifficultyHelper.GetStatMultiplier(difficultyTier);
        float tickDmg = dps * interval * mult;

        Damage dmg = new Damage
        {
            value = tickDmg,
            type = DamageType.elemental,
            elementId = "FIRE",
            unparryable = true,
            source = health,
            sourcePosition = transform.position
        };

        playerHealth.ReceiveDamage(dmg);
    }

    private void HandleDeath()
    {
        Debug.Log($"[CaptainKombustaController] {captainName} DEFEATED!");

        ExtinguishFire();

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

        // Drop bonus gold and goblin blood based on tier
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
