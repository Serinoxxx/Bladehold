using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The player's Chain Lightning buff: a timed power-up granted by picking up Lightning Orbs
///     (<see cref="LightningOrb" />, dropped by Storm Witches). While active, the sword's hits chain to
///     nearby enemies via <see cref="ChainLightning" />.
///
///     Each orb adds <see cref="StatType.ChainLightningOrbDuration" /> seconds to the remaining time and
///     one stack; every stack beyond the first adds extra bounce damage (<see cref="ChainLightningSO" />).
///     All stacks expire together when the timer runs out. <see cref="PlayerStats" /> has no timed
///     modifiers, so the buff owns its own countdown (the <see cref="ImpulseBuff" /> idiom); scene reload
///     resets it naturally. Unlocking is the <see cref="StatType.ChainLightningOrbDuration" /> stat itself
///     (base 0 = orbs grant nothing), per the "expose upgradeable numbers as stats" convention.
/// </summary>
public class ChainLightningBuff : MonoBehaviour
{
    [SerializeField] private ChainLightningSO config;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;

    [Header("Buff feedback (cosmetic, optional)")]
    [Tooltip("Played when the buff activates (first orb of a run of stacks).")]
    [SerializeField] private MMF_Player activationFeedback;
    [Tooltip("Played when the buff expires.")]
    [SerializeField] private MMF_Player deactivationFeedback;
    [Tooltip("Looping aura child object, shown while the buff is active.")]
    [SerializeField] private GameObject auraVisual;

    /// <summary>Raised whenever the buff's remaining time or stack count changes (pickup, expiry).</summary>
    public event Action OnChanged;

    public float RemainingSeconds { get; private set; }
    public float MaxSeconds { get; private set; }
    public int StackCount { get; private set; }

    private bool anyError = false;

    public bool IsActive => !anyError && RemainingSeconds > 0f;

    /// <summary>How many additional enemies a chain can bounce to, including stack bonuses.</summary>
    public int CurrentBounces => anyError ? 0 : Mathf.RoundToInt(stats.GetValue(StatType.ChainLightningBounces));

    /// <summary>Fraction of the triggering hit's damage each bounce deals, including stack bonuses. The
    /// clamp keeps buff-independent chains (<see cref="ChainLightning.ForceChain" /> at 0 stacks) at 1x.</summary>
    public float CurrentDamagePercent => anyError
        ? 0f
        : stats.GetValue(StatType.ChainLightningDamagePercent) * (1f + Mathf.Max(0, StackCount - 1) * config.damagePerExtraStackPercent);

    /// <summary>Chance (0-1) each bounce crits.</summary>
    public float CurrentCritChance => anyError ? 0f : stats.GetValue(StatType.ChainLightningCritChance);

    /// <summary>World-space radius each hop searches for the next target.</summary>
    public float ChainRadius => anyError ? 0f : config.chainRadius;

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("ChainLightningSO is not assigned in the inspector.");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("ChainLightningBuff could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.ChainLightningOrbDuration, config.baseOrbDurationSeconds);
        stats.SetBase(StatType.ChainLightningBounces, config.baseBounces);
        stats.SetBase(StatType.ChainLightningDamagePercent, config.baseDamagePercent);
        stats.SetBase(StatType.ChainLightningCritChance, config.baseCritChance);
        // Conduit (incoming Storm Witch lightning) is part of the lightning skill family, so its
        // bases live here with the rest; LightningBall reads them via Player.Instance.Stats.
        stats.SetBase(StatType.ConduitDamageReductionPercent, 0f);
        stats.SetBase(StatType.ConduitChainChance, 0f);

        if (auraVisual != null)
        {
            auraVisual.SetActive(false);
        }
    }

    private void Update()
    {
        if (RemainingSeconds <= 0f)
        {
            return;
        }

        RemainingSeconds -= Time.deltaTime;
        if (RemainingSeconds <= 0f)
        {
            Deactivate();
        }
    }

    /// <summary>
    ///     Grants one orb's worth of buff time and a stack. Returns the seconds granted (0 while the
    ///     feature is still locked, i.e. no skill node has raised <see cref="StatType.ChainLightningOrbDuration" />).
    /// </summary>
    public float CollectOrb()
    {
        if (anyError)
        {
            return 0f;
        }

        float granted = stats.GetValue(StatType.ChainLightningOrbDuration);
        if (granted <= 0f)
        {
            return 0f;
        }

        bool wasActive = IsActive;
        RemainingSeconds += granted;
        MaxSeconds = RemainingSeconds;
        StackCount++;

        if (!wasActive)
        {
            if (activationFeedback != null)
            {
                activationFeedback.PlayFeedbacks();
            }
            if (auraVisual != null)
            {
                auraVisual.SetActive(true);
            }
        }

        OnChanged?.Invoke();
        return granted;
    }

    private void Deactivate()
    {
        RemainingSeconds = 0f;
        StackCount = 0;

        if (deactivationFeedback != null)
        {
            deactivationFeedback.PlayFeedbacks();
        }
        if (auraVisual != null)
        {
            auraVisual.SetActive(false);
        }

        OnChanged?.Invoke();
    }
}
