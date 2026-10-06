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

    // HP is counted in hits: an arrow deals its damage divided by arrowDamagePerHit, while Bleed ticks
    // and Fishsploshion blasts deal their flat value straight off it.
    [Header("Stats")]
    [SerializeField] private float maxHp = 1f;
    private float currentHp;
    private float arrowDamagePerHit = 1f;
    private bool isDead = false;

    [Header("Orbit Swimming Config")]
    public Vector3 orbitCenter = Vector3.zero;
    public float orbitRadius = 8f;
    public float orbitSpeed = 25f; // degrees per second
    public float currentAngle = 0f;
    public float depthOffset = 0f;
    public bool clockwise = true;

    // Swim-in: a new fish starts at the pond rim and spirals in to orbitRadius over entryDuration.
    private float swimRadius;
    private float entryStartRadius;
    private float entryStartDepth;
    private float entryDuration;
    private float entryElapsed;

    [Header("Visuals")]
    [SerializeField] private Renderer meshRenderer;
    [SerializeField] private GameObject deathVfxPrefab;
    [Tooltip("Outline/glow that marks the fish's type. FishingManager loads the pond's profile into it and tints it per type.")]
    [SerializeField] private HighlightPlus.HighlightEffect typeHighlight;
    [Tooltip("Drawn health bar, hidden until the fish survives a hit. Shown for a moment after each one.")]
    [SerializeField] private MoreMountains.Tools.MMHealthBar healthBar;

    private readonly List<Coroutine> activeBleedRoutines = new List<Coroutine>();
    private int currentBleedStacks = 0;
    private Vector3 baseScale = Vector3.one;
    // Fishsploshion chain link that is dealing the current hit: 0 unless mid-ReceiveFishsploshionDamage.
    private int incomingChainDepth = 0;
    // Set when a counted catch sends the corpse flying to the player; Die then leaves the destroy to the flight.
    private bool isFlyingToCatcher = false;

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

    private void OnValidate()
    {
        if (typeHighlight == null) typeHighlight = GetComponentInChildren<HighlightPlus.HighlightEffect>(true);
        if (healthBar == null) healthBar = GetComponent<MoreMountains.Tools.MMHealthBar>();
    }

    private void Start()
    {
        if (healthBar == null) Debug.LogError($"[FishController] '{name}' has no MMHealthBar (healthBar); damaged fish show no health.", this);
    }

    /// <summary>
    ///     Outlines the fish in its type colour; <paramref name="glow" /> adds a halo for the special ones
    ///     (buff and Diamond fish) so they stand out in a busy pond.
    /// </summary>
    public void ApplyTypeHighlight(HighlightPlus.HighlightProfile profile, Color color, bool glow)
    {
        if (typeHighlight == null)
        {
            Debug.LogError($"[FishController] '{name}' has no HighlightEffect (typeHighlight); fish types can't be told apart.", this);
            return;
        }
        if (profile == null)
        {
            Debug.LogError("[FishController] No fish highlight profile passed in (FishingManager.fishHighlightProfile).", this);
            return;
        }

        typeHighlight.profile = profile;
        typeHighlight.ProfileReload();
        // Highlight Plus only builds its overlay material when this is above 0; without it the hit
        // flash in TakeHit renders with a null material and throws every frame of the flash.
        typeHighlight.hitFxInitialIntensity = 1f;
        typeHighlight.outlineColor = color;
        typeHighlight.glowHQColor = color;
        if (!glow) typeHighlight.glow = 0f;
        typeHighlight.SetHighlighted(true);
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
        float hits,
        float damagePerHit,
        float entryRadius,
        float entryDepth,
        float entrySpeed)
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

        maxHp = Mathf.Max(1f, hits);
        arrowDamagePerHit = Mathf.Max(0.01f, damagePerHit);
        currentHp = maxHp;
        isDead = false;

        // Start at the rim; a fish already past it (or no swim speed) starts on its orbit.
        entryStartRadius = Mathf.Max(entryRadius, radius);
        entryStartDepth = entryDepth;
        entryDuration = entrySpeed > 0f ? (entryStartRadius - radius) / entrySpeed : 0f;
        entryElapsed = 0f;
        swimRadius = entryDuration > 0f ? entryStartRadius : radius;

        // Apply scale modifiers (Diamond 2.5x, Fat Fish upgrade)
        UpdateScale();
        UpdatePosition();
        transform.rotation = Quaternion.LookRotation(Tangent(currentAngle * Mathf.Deg2Rad), Vector3.up);
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

        if (entryElapsed < entryDuration)
        {
            entryElapsed += slowModifier * Time.deltaTime;
        }

        Vector3 previous = transform.position;
        UpdatePosition();

        // Face the way it's actually swimming, so a fish spiralling in points inward.
        Vector3 heading = transform.position - previous;
        heading.y = 0f;
        if (heading.sqrMagnitude > 0.000001f)
        {
            transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        }
    }

    private void UpdatePosition()
    {
        float depth = depthOffset;
        swimRadius = orbitRadius;
        if (entryElapsed < entryDuration)
        {
            // Ease out: dart in from the rim, then settle onto the orbit.
            float t = 1f - Mathf.Pow(1f - entryElapsed / entryDuration, 2f);
            swimRadius = Mathf.Lerp(entryStartRadius, orbitRadius, t);
            depth = Mathf.Lerp(entryStartDepth, depthOffset, t);
        }

        float rad = currentAngle * Mathf.Deg2Rad;
        transform.position = orbitCenter + new Vector3(Mathf.Cos(rad) * swimRadius, depth, Mathf.Sin(rad) * swimRadius);
    }

    private Vector3 Tangent(float rad)
    {
        return clockwise
            ? new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad))
            : new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
    }

    public void UpdateScale()
    {
        float fatBonus = FishingUpgradeManager.Instance != null ? FishingUpgradeManager.Instance.FatFishBonusPercent : 0f;
        // The once-per-visit big fish read as big: Diamond 2.5x, Arcane 2x (placeholder look, plan 21 phase 5).
        float diamondMult = isBuffFish ? 1f : resourceType == ResourceFishType.Diamond ? 2.5f : resourceType == ResourceFishType.ArcaneFish ? 2f : 1f;
        transform.localScale = baseScale * (1f + fatBonus) * diamondMult;
    }

    public void ReceiveDamage(Damage damage)
    {
        float dmgVal = damage != null ? damage.value : arrowDamagePerHit;
        TakeHit(dmgVal / arrowDamagePerHit);
    }

    /// <summary>
    ///     A hit from a Fishsploshion blast. If it kills, the fish's own blast is link
    ///     <paramref name="chainDepth" /> of the chain, and only goes off within Chain Reaction's limit.
    /// </summary>
    public void ReceiveFishsploshionDamage(Damage damage, int chainDepth)
    {
        if (isDead) return;

        incomingChainDepth = chainDepth;
        TakeHit(damage != null ? damage.value : 1f);
        incomingChainDepth = 0;
    }

    private void TakeHit(float hits)
    {
        if (isDead) return;

        currentHp -= hits;
        if (currentHp > 0f) ShowHealth();

        // Damage flash (Highlight Plus hit effect: no per-fish material instance, works on any shader)
        if (typeHighlight != null)
        {
            typeHighlight.HitFX(Color.white, 0.15f, 1f);
        }

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
            ShowHealth();
        }

        currentBleedStacks = Mathf.Max(0, currentBleedStacks - 1);
    }

    // Only hits the fish lives through show the bar: a one-shot kill never flashes one.
    private void ShowHealth()
    {
        if (healthBar != null) healthBar.UpdateBar(currentHp, 0f, maxHp, true);
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

        if (!isFlyingToCatcher) Destroy(gameObject);
    }

    /// <summary>
    ///     Sends the dead fish arcing out of the water to <paramref name="target" /> (re-aimed every frame
    ///     as the player moves), then calls <paramref name="onArrive" /> with where it landed and destroys it.
    /// </summary>
    public void FlyTo(Transform target, Vector3 targetOffset, float duration, float arcHeight, Action<Vector3> onArrive)
    {
        if (!isDead || target == null)
        {
            Debug.LogError($"[FishController] FlyTo on '{name}' needs a dead fish and a target.", this);
            return;
        }

        isFlyingToCatcher = true;

        // Arrows and blasts pass through a corpse in flight, and its bar stays hidden.
        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;
        if (healthBar != null)
        {
            healthBar.ShowBar(false);
            healthBar.enabled = false;
        }

        StartCoroutine(FlyRoutine(target, targetOffset, duration, arcHeight, onArrive));
    }

    private IEnumerator FlyRoutine(Transform target, Vector3 targetOffset, float duration, float arcHeight, Action<Vector3> onArrive)
    {
        Vector3 start = transform.position;
        Vector3 end = start;
        Vector3 spinAxis = UnityEngine.Random.onUnitSphere;
        float spinSpeed = UnityEngine.Random.Range(540f, 900f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) end = target.position + targetOffset;

            // Quadratic Bezier through a control point lifted above the midpoint.
            Vector3 control = (start + end) * 0.5f + Vector3.up * arcHeight;
            Vector3 a = Vector3.Lerp(start, control, t);
            Vector3 b = Vector3.Lerp(control, end, t);
            transform.position = Vector3.Lerp(a, b, t);
            transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);
            yield return null;
        }

        onArrive?.Invoke(transform.position);
        Destroy(gameObject);
    }
}
