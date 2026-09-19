using System.Collections;
using System.Collections.Generic;
using HighlightPlus;
using UnityEngine;

/// <summary>
///     Bannerman unit aura controller:
///     Projects a localized buff aura based on the wave banner selection (or default).
///     Enemies inside the radius receive the buff and render a corresponding glow:
///     - Red glow for Damage / Berserk
///     - Green glow for Healing / Regen
///     - Yellow glow for Shield
///     The banner can be destroyed independently of the Bannerman by shooting it.
///     Destroying the banner or killing the Bannerman immediately disables the buff aura.
/// </summary>
public class BannermanAura : MonoBehaviour
{
    [SerializeField] private BannermanAuraSO auraData;
    [SerializeField] private DestructibleBanner banner;
    [SerializeField] private Health health;

    [Header("Visuals")]
    [Tooltip("Optional aura boundary ring or VFX object attached to the Bannerman.")]
    [SerializeField] private GameObject auraVfx;

    private const int MaxOverlapResults = 128;
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];

    private class BuffedUnitState
    {
        public Health unitHealth;
        public HighlightEffect highlightEffect;
        public AIAttack attack;
        public bool hasModifiedMaxHealth;
        public float addedHealth;
    }

    private readonly Dictionary<Health, BuffedUnitState> activeBuffedUnits = new Dictionary<Health, BuffedUnitState>();
    private readonly HashSet<Health> currentFrameUnits = new HashSet<Health>();
    private readonly List<Health> unitsToRemove = new List<Health>();

    private BannerBuffType activeBuffType = BannerBuffType.None;
    private HighlightProfile activeProfile;
    private bool isDead = false;
    private bool isBannerDestroyed = false;
    private bool anyError = false;
    private Coroutine auraCoroutine;

    public BannerBuffType ActiveBuffType => activeBuffType;

    public void SetDamage(float value)
    {
        // Support WaveSpawner dispatch if needed
    }

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (banner == null)
        {
            banner = GetComponentInChildren<DestructibleBanner>();
        }
    }

    private void Start()
    {
        if (auraData == null)
        {
            Debug.LogError("[BannermanAura] BannermanAuraSO is not assigned in the inspector.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("[BannermanAura] Health component is not assigned or found.", this);
            anyError = true;
        }

        if (anyError) return;

        // Resolve active buff from GameLoopManager, or fallback to SO default
        if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentWaveBuff != BannerBuffType.None)
        {
            activeBuffType = GameLoopManager.Instance.CurrentWaveBuff;
        }
        else
        {
            activeBuffType = auraData.defaultBuffType;
        }

        // Select the appropriate highlight profile
        switch (activeBuffType)
        {
            case BannerBuffType.Berserk:
                activeProfile = auraData.damageBuffProfile;
                break;
            case BannerBuffType.Regen:
                activeProfile = auraData.healingBuffProfile;
                break;
            case BannerBuffType.Shield:
                activeProfile = auraData.shieldBuffProfile;
                break;
            default:
                activeProfile = auraData.damageBuffProfile;
                break;
        }

        if (banner != null)
        {
            banner.Initialize(auraData.bannerHealth);
            banner.OnBannerDestroyed += HandleBannerDestroyed;
        }

        health.OnDied += HandleDied;

        auraCoroutine = StartCoroutine(AuraRoutine());
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (banner != null)
        {
            banner.OnBannerDestroyed -= HandleBannerDestroyed;
        }

        ClearAllBuffs();
    }

    private void OnDisable()
    {
        ClearAllBuffs();
    }

    private void HandleBannerDestroyed()
    {
        isBannerDestroyed = true;
        if (auraVfx != null)
        {
            auraVfx.SetActive(false);
        }
        ClearAllBuffs();
    }

    private void HandleDied()
    {
        isDead = true;
        if (auraVfx != null)
        {
            auraVfx.SetActive(false);
        }
        ClearAllBuffs();
        enabled = false;
    }

    private IEnumerator AuraRoutine()
    {
        var wait = new WaitForSeconds(auraData.checkInterval);

        while (!isDead && !isBannerDestroyed)
        {
            UpdateAura();
            yield return wait;
        }
    }

    private void UpdateAura()
    {
        if (isDead || isBannerDestroyed)
        {
            ClearAllBuffs();
            return;
        }

        currentFrameUnits.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, auraData.auraRadius, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            Health targetHealth = col.GetComponent<Health>();
            if (targetHealth == null)
            {
                targetHealth = col.GetComponentInParent<Health>();
            }

            // Must be an alive entity, not self, and not the player
            if (targetHealth == null || targetHealth == health || targetHealth.IsDead) continue;
            if (Player.Instance != null && (targetHealth == Player.Instance.Health || targetHealth.transform.root == Player.Instance.transform.root)) continue;

            currentFrameUnits.Add(targetHealth);

            if (!activeBuffedUnits.TryGetValue(targetHealth, out BuffedUnitState state))
            {
                // New unit entering aura
                state = ApplyBuffToUnit(targetHealth);
                if (state != null)
                {
                    activeBuffedUnits[targetHealth] = state;
                }
            }

            // Periodic tick effect (e.g. healing)
            if (activeBuffType == BannerBuffType.Regen && targetHealth != null && !targetHealth.IsDead)
            {
                targetHealth.Heal(auraData.healingPerSecond * auraData.checkInterval);
            }
        }

        // Detect units that left the aura or died
        unitsToRemove.Clear();
        foreach (var kvp in activeBuffedUnits)
        {
            if (!currentFrameUnits.Contains(kvp.Key) || kvp.Key == null || kvp.Key.IsDead)
            {
                unitsToRemove.Add(kvp.Key);
            }
        }

        for (int i = 0; i < unitsToRemove.Count; i++)
        {
            Health unit = unitsToRemove[i];
            if (activeBuffedUnits.TryGetValue(unit, out BuffedUnitState state))
            {
                RemoveBuffFromUnit(state);
                activeBuffedUnits.Remove(unit);
            }
        }
    }

    private BuffedUnitState ApplyBuffToUnit(Health targetHealth)
    {
        var state = new BuffedUnitState
        {
            unitHealth = targetHealth
        };

        // 1. Apply mechanic buff
        switch (activeBuffType)
        {
            case BannerBuffType.Berserk:
                state.attack = targetHealth.GetComponent<AIAttack>();
                if (state.attack != null)
                {
                    state.attack.SetDamageMultiplier(auraData.damageBuffMultiplier);
                }
                break;

            case BannerBuffType.Shield:
                float bonus = targetHealth.MaxHealth * auraData.shieldFraction;
                targetHealth.SetMaxHealth(targetHealth.MaxHealth + bonus);
                targetHealth.Heal(bonus);
                state.hasModifiedMaxHealth = true;
                state.addedHealth = bonus;
                break;
        }

        // 2. Apply HighlightEffect
        HighlightEffect effect = targetHealth.GetComponent<HighlightEffect>();
        if (effect == null)
        {
            effect = targetHealth.gameObject.AddComponent<HighlightEffect>();
        }

        if (effect != null && activeProfile != null)
        {
            effect.profile = activeProfile;
            effect.ProfileReload();
            effect.SetHighlighted(true);
            state.highlightEffect = effect;
        }

        return state;
    }

    private void RemoveBuffFromUnit(BuffedUnitState state)
    {
        if (state == null) return;

        // 1. Revert mechanic buff
        if (state.attack != null)
        {
            state.attack.SetDamageMultiplier(1f);
        }

        if (state.hasModifiedMaxHealth && state.unitHealth != null)
        {
            state.unitHealth.SetMaxHealth(Mathf.Max(1f, state.unitHealth.MaxHealth - state.addedHealth));
        }

        // 2. Revert highlight
        if (state.highlightEffect != null)
        {
            state.highlightEffect.SetHighlighted(false);
        }
    }

    private void ClearAllBuffs()
    {
        foreach (var kvp in activeBuffedUnits)
        {
            RemoveBuffFromUnit(kvp.Value);
        }
        activeBuffedUnits.Clear();
        currentFrameUnits.Clear();
    }
}
