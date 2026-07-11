using UnityEngine;

/// <summary>
///     The Berserker's rage meter — the class's core loop: dealing and (especially) taking damage
///     fills it, idleness drains it, and the fuller it is the harder the Berserker hits
///     (<see cref="DamageMultiplier" />, read by <see cref="DamageTrigger" /> and
///     <see cref="PlayerThrownAxe" /> the way they read <see cref="ImpulseBuff" />), the faster he
///     moves (a quantized <see cref="StatType.MoveSpeed" /> percent modifier —
///     <see cref="PlayerStats" /> has no modifier removal, so changes are applied as signed deltas),
///     and the less damage he takes (a <see cref="Health.ScaleDamageTaken" /> handler).
///
///     Effect magnitudes are innate, tuned on <see cref="RageSO" />; the skill tree improves
///     <see cref="StatType.RageGainMultiplier" /> (build faster) and
///     <see cref="StatType.RageRetentionMultiplier" /> (linger longer — scales the decay delay up and
///     the drain rate down), both base 1.0 registered here.
///
///     Lives on the player root, enabled only in the Berserker's class slot — a disabled component
///     never runs Start, so the Swordsman pays nothing. Gain sources resolve through
///     <see cref="PlayerClassController.ActiveMeleeTrigger" /> and the sibling
///     <see cref="PlayerThrownAxe" />; cosmetic listeners (the HUD rage bar) poll
///     <see cref="RageFraction" /> (the <see cref="SwordChargeFeedback" /> pattern).
/// </summary>
public class RageBuff : MonoBehaviour
{
    [SerializeField] private RageSO config;
    [Tooltip("Optional; defaults to the Health on this GameObject.")]
    [SerializeField] private Health health;
    [Tooltip("Optional; defaults to the PlayerStats on this GameObject.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional: the melee trigger whose hits build rage. Defaults to the class controller's active melee trigger.")]
    [SerializeField] private DamageTrigger meleeTrigger;
    [Tooltip("Optional: the thrown axe whose hits build rage. Defaults to the one on this GameObject.")]
    [SerializeField] private PlayerThrownAxe thrownAxe;

    private float rage;
    private float lastGainTime = Mathf.NegativeInfinity;
    private float appliedMoveSpeedPercent;
    private bool anyError = false;

    /// <summary>Current rage points, 0..MaxRage — for the HUD bar and the DevConsole readout.</summary>
    public float CurrentRage => rage;

    /// <summary>Rage points at a full meter.</summary>
    public float MaxRage => config != null ? config.maxRage : 0f;

    /// <summary>Current fill of the meter, 0..1. Every rage effect scales linearly with this.</summary>
    public float RageFraction => config != null && config.maxRage > 0f ? Mathf.Clamp01(rage / config.maxRage) : 0f;

    /// <summary>True while any rage is banked (and effects are non-neutral).</summary>
    public bool IsActive => !anyError && rage > 0f;

    /// <summary>Damage multiplier for the Berserker's attacks: 1 at empty, 1 + damageBonusAtFullRage at full.</summary>
    public float DamageMultiplier => 1f + RageFraction * (config != null ? config.damageBonusAtFullRage : 0f);

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
        if (thrownAxe == null)
        {
            thrownAxe = GetComponent<PlayerThrownAxe>();
        }
    }

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("RageSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("RageBuff could not find Health on the GameObject.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("RageBuff could not find PlayerStats on the GameObject.");
            anyError = true;
        }

        if (meleeTrigger == null)
        {
            PlayerClassController classController = GetComponent<PlayerClassController>();
            meleeTrigger = classController != null ? classController.ActiveMeleeTrigger : null;
        }

        if (anyError)
        {
            return;
        }

        // Gain speed and retention are the upgradeable half of rage (base 1.0, the MoveSpeed
        // multiplier convention); the effect magnitudes stay on the SO.
        stats.SetBase(StatType.RageGainMultiplier, 1f);
        stats.SetBase(StatType.RageRetentionMultiplier, 1f);

        // Rage sources: dealing damage with either weapon, and taking damage.
        if (meleeTrigger != null)
        {
            meleeTrigger.OnHit += HandleDamageDealt;
        }
        else
        {
            Debug.LogWarning("RageBuff found no melee DamageTrigger — melee hits won't build rage.");
        }
        if (thrownAxe != null)
        {
            thrownAxe.OnHit += HandleDamageDealt;
        }
        health.OnDamaged += HandleDamageTaken;
        health.ScaleDamageTaken += HandleScaleDamageTaken;
    }

    private void OnDestroy()
    {
        if (meleeTrigger != null)
        {
            meleeTrigger.OnHit -= HandleDamageDealt;
        }
        if (thrownAxe != null)
        {
            thrownAxe.OnHit -= HandleDamageDealt;
        }
        if (health != null)
        {
            health.OnDamaged -= HandleDamageTaken;
            health.ScaleDamageTaken -= HandleScaleDamageTaken;
        }
    }

    private void OnDisable()
    {
        // Benched mid-scene (not the normal path — class swaps reload): drop the meter and retract
        // the move-speed modifier so nothing lingers on the shared stats.
        rage = 0f;
        ApplyMoveSpeedModifier();
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        // Idle decay: retention stretches the grace window and thins the drain.
        float retention = Mathf.Max(0.01f, stats.GetValue(StatType.RageRetentionMultiplier));
        if (rage > 0f && Time.time - lastGainTime >= config.decayDelaySeconds * retention)
        {
            rage = Mathf.Max(0f, rage - config.decayPerSecond / retention * Time.deltaTime);
        }

        ApplyMoveSpeedModifier();
    }

    private void HandleDamageDealt(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        Gain(damage.value * config.ragePerDamageDealt);
    }

    private void HandleDamageTaken(Damage damage)
    {
        Gain(damage.value * config.ragePerDamageTaken);
    }

    private void Gain(float amount)
    {
        if (anyError || amount <= 0f)
        {
            return;
        }

        rage = Mathf.Min(config.maxRage, rage + amount * stats.GetValue(StatType.RageGainMultiplier));
        lastGainTime = Time.time;
    }

    private float HandleScaleDamageTaken(Damage damage)
    {
        return 1f - Mathf.Clamp01(config.damageReductionAtFullRage * RageFraction);
    }

    /// <summary>
    ///     Keeps a MoveSpeed percent modifier equal to the rage bonus, quantized to 1% steps —
    ///     PlayerStats has no RemoveModifier, so the tracked amount is adjusted with signed deltas,
    ///     and quantizing keeps PlayerMoveSpeedBinder's reflection writes off the per-frame path.
    /// </summary>
    private void ApplyMoveSpeedModifier()
    {
        if (stats == null || config == null)
        {
            return;
        }

        float target = Mathf.Round(RageFraction * config.moveSpeedBonusAtFullRage * 100f) / 100f;
        if (Mathf.Approximately(target, appliedMoveSpeedPercent))
        {
            return;
        }

        stats.AddModifier(StatType.MoveSpeed, ModifierKind.Percent, target - appliedMoveSpeedPercent);
        appliedMoveSpeedPercent = target;
    }
}
