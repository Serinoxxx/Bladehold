using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Controls the swimming behavior, health, damage reception, bleed status, and death of a fish.
/// </summary>
public class FishController : MonoBehaviour, IDamageable
{
    [Header("Fish Classification")]
    public bool isBuffFish = false;
    public ResourceFishType resourceType = ResourceFishType.Gold;
    public BuffFishType buffType = BuffFishType.Speedy;

    [Header("Stats")]
    [SerializeField] private float maxHp = 1f;
    private float currentHp;
    private bool isDead = false;

    [Header("Orbit Swimming Config")]
    public Vector3 orbitCenter = Vector3.zero;
    public float orbitRadius = 8f;
    public float orbitSpeed = 25f; // degrees per second
    public float currentAngle = 0f;
    public float depthOffset = 0f;
    public bool clockwise = true;

    [Header("Visuals")]
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private GameObject deathVfxPrefab;

    private readonly List<Coroutine> activeBleedRoutines = new List<Coroutine>();
    private int currentBleedStacks = 0;
    private Vector3 baseScale = Vector3.one;
    // Fishsploshion chain link that is dealing the current hit: 0 unless mid-ReceiveFishsploshionDamage.
    private int incomingChainDepth = 0;

    public bool IsDead => isDead;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;

    private void Awake()
    {
        baseScale = transform.localScale;
        if (meshRenderer == null)
        {
            meshRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public void Setup(
        Vector3 center,
        float radius,
        float speed,
        float angle,
        float depth,
        bool cw,
        bool buff,
        ResourceFishType resType,
        BuffFishType bType,
        float hpMultiplier = 1f)
    {
        orbitCenter = center;
        orbitRadius = radius;
        orbitSpeed = speed;
        currentAngle = angle;
        depthOffset = depth;
        clockwise = cw;
        isBuffFish = buff;
        resourceType = resType;
        buffType = bType;

        maxHp = Mathf.Max(1f, maxHp * hpMultiplier);
        currentHp = maxHp;
        isDead = false;

        // Apply scale modifiers (Diamond 2.5x, Fat Fish upgrade)
        UpdateScale();
        UpdatePosition();
    }

    private void Update()
    {
        if (isDead) return;

        // Spec: fish freeze once the frenzy ends.
        if (FishingManager.Instance != null && FishingManager.Instance.CurrentState == FishingState.Finished) return;

        // Icey Water slow modifier
        float slowModifier = 1f;
        if (FishingUpgradeManager.Instance != null)
        {
            slowModifier = Mathf.Clamp01(1f - FishingUpgradeManager.Instance.IceyWaterSlowRatio);
        }

        float deltaAngle = orbitSpeed * slowModifier * Time.deltaTime;
        if (!clockwise) deltaAngle = -deltaAngle;

        currentAngle = (currentAngle + deltaAngle) % 360f;
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 newPos = orbitCenter + new Vector3(Mathf.Cos(rad) * orbitRadius, depthOffset, Mathf.Sin(rad) * orbitRadius);
        transform.position = newPos;

        // Orient along tangent of circle
        Vector3 tangent = clockwise
            ? new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad))
            : new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));

        if (tangent.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }

    public void UpdateScale()
    {
        float fatBonus = FishingUpgradeManager.Instance != null ? FishingUpgradeManager.Instance.FatFishBonusPercent : 0f;
        float diamondMult = (!isBuffFish && resourceType == ResourceFishType.Diamond) ? 2.5f : 1f;
        transform.localScale = baseScale * (1f + fatBonus) * diamondMult;
    }

    public void ReceiveDamage(Damage damage)
    {
        if (isDead) return;

        float dmgVal = damage != null ? damage.value : 1f;
        currentHp -= dmgVal;

        // Damage flash
        StartCoroutine(HitFlashRoutine());

        // Bleed upgrade logic
        if (FishingUpgradeManager.Instance != null && FishingUpgradeManager.Instance.BleedMaxStacks > 0)
        {
            ApplyBleed();
        }

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    ///     A hit from a Fishsploshion blast. If it kills, the fish's own blast is link
    ///     <paramref name="chainDepth" /> of the chain, and only goes off within Chain Reaction's limit.
    /// </summary>
    public void ReceiveFishsploshionDamage(Damage damage, int chainDepth)
    {
        if (isDead) return;

        incomingChainDepth = chainDepth;
        ReceiveDamage(damage);
        incomingChainDepth = 0;
    }

    private void ApplyBleed()
    {
        int maxStacks = FishingUpgradeManager.Instance.BleedMaxStacks;
        if (currentBleedStacks < maxStacks)
        {
            currentBleedStacks++;
            Coroutine routine = StartCoroutine(BleedTickRoutine());
            activeBleedRoutines.Add(routine);
        }
    }

    private IEnumerator BleedTickRoutine()
    {
        float duration = FishingUpgradeManager.Instance.BleedDuration;
        float dps = FishingUpgradeManager.Instance.BleedDps;
        float elapsed = 0f;

        while (elapsed < duration && !isDead)
        {
            yield return new WaitForSeconds(1f);
            elapsed += 1f;
            if (isDead) break;
            if (FishingManager.Instance != null && FishingManager.Instance.CurrentState == FishingState.Finished) yield break;

            currentHp -= dps;
            if (currentHp <= 0f)
            {
                Die();
                yield break;
            }
        }

        currentBleedStacks = Mathf.Max(0, currentBleedStacks - 1);
    }

    private IEnumerator HitFlashRoutine()
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            Color origColor = meshRenderer.material.color;
            meshRenderer.material.color = Color.white;
            yield return new WaitForSeconds(0.08f);
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.color = origColor;
            }
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        StopAllCoroutines();

        // Fishsploshion upgrade logic: player kills always blast; blast kills only within Chain Reaction's limit.
        FishingUpgradeManager upgrades = FishingUpgradeManager.Instance;
        if (upgrades != null && upgrades.FishsploshionDamage > 0 && incomingChainDepth <= upgrades.ChainReactionMaxChains)
        {
            upgrades.QueueFishsploshion(transform.position, incomingChainDepth);
        }

        // Notify FishingManager
        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.OnFishKilled(this);
        }

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

}
