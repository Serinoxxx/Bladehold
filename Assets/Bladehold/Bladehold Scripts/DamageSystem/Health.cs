using System;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] HealthSO healthData;

    [SerializeField] float currentHealth;

    [SerializeField] MMF_Player damageFeedback;

    [SerializeField] MMF_Player deathFeedback;

    /// <summary>
    ///     Raised once, when health first reaches zero. Listeners (animation, movement, …) react to
    ///     death; <see cref="Health" /> itself stays unaware of what reacts, so the dependency only ever
    ///     points inward — nothing here knows about animators or agents.
    /// </summary>
    public event Action OnDied;

    /// <summary>
    ///     Raised each time damage lands (including the killing blow), carrying the <see cref="Damage" />
    ///     that was applied. Listeners (floating damage numbers, hit reactions, …) react to it; Health
    ///     stays unaware of them.
    /// </summary>
    public event Action<Damage> OnDamaged;

    /// <summary>
    ///     Raised whenever <see cref="CurrentHealth" /> changes for any reason — damage, revive, or the
    ///     initial fill in <c>Start</c>. Listeners that mirror the value (health bars, …) react to it;
    ///     Health stays unaware of them, same as <see cref="OnDamaged" />.
    /// </summary>
    public event Action OnHealthChanged;

    /// <summary>True once health has reached zero. Latches; further damage is ignored.</summary>
    public bool IsDead { get; private set; }

    /// <summary>The current health value, for listeners reacting to <see cref="OnHealthChanged" />.</summary>
    public float CurrentHealth => currentHealth;

    /// <summary>The effective maximum health: the per-instance override if one was set (see
    /// <see cref="SetMaxHealth" />), else the configured <c>healthData</c> value, or 0 if neither.</summary>
    public float MaxHealth => maxHealthOverride ?? (healthData != null ? healthData.maxHealth : 0f);

    private float? maxHealthOverride;

    /// <summary>
    ///     Per-instance max-health override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate, before Start runs — the same timing as
    ///     <c>GoldenGoblin.MarkGolden</c> — so the shared <see cref="HealthSO" /> is never mutated.
    /// </summary>
    public void SetMaxHealth(float value)
    {
        maxHealthOverride = value;
        currentHealth = value;
    }

    /// <summary>
    ///     Multiplies the effective max health, preserving the current-health <em>fraction</em> — unlike
    ///     <see cref="SetMaxHealth" />, which refills to full and is meant for pre-Start roster overrides.
    ///     Used by <c>PlayerMount</c> to apply the Barded Steed multiplier when mounting a possibly
    ///     wounded horse mid-run.
    /// </summary>
    public void ScaleMaxHealth(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        float fraction = MaxHealth > 0f ? currentHealth / MaxHealth : 1f;
        maxHealthOverride = MaxHealth * multiplier;
        currentHealth = maxHealthOverride.Value * fraction;
        OnHealthChanged?.Invoke();
    }

    /// <summary>
    ///     Checked once health would reach zero, before <see cref="IsDead" /> latches or <see cref="OnDied" />
    ///     fires. A handler that wants to intercept a lethal hit (e.g. a revive effect) returns <c>true</c> and
    ///     is responsible for restoring health itself via <see cref="Revive" />; the death is then skipped
    ///     entirely for this hit. Returning <c>false</c> lets death proceed normally. Multiple handlers are
    ///     supported (checked via the invocation list) so one handler's <c>true</c> can't be silently dropped
    ///     by another's <c>false</c>.
    /// </summary>
    public event Func<bool> TryPreventDeath;

    /// <summary>
    ///     Checked at the top of <see cref="ReceiveDamage" />, before any health is lost. A handler that
    ///     wants to negate the hit entirely (e.g. the "Solid" auto-block in <see cref="DamageBlocker" />)
    ///     returns <c>true</c>; the damage, feedback, and events are then all skipped for that hit. Same
    ///     invocation-list pattern as <see cref="TryPreventDeath" /> so one handler's <c>true</c> can't be
    ///     silently dropped by another's <c>false</c>.
    /// </summary>
    public event Func<Damage, bool> TryBlockDamage;

    /// <summary>
    ///     Checked after <see cref="TryBlockDamage" />, before any health is lost. Each handler returns
    ///     a multiplier on the incoming damage (1 = unchanged, 0.7 = 30% reduction); all handlers'
    ///     returns multiply together, clamped to at least 0. The <see cref="Damage.value" /> is scaled
    ///     in place — each hit is a fresh <see cref="Damage" /> instance — so <see cref="OnDamaged" />
    ///     listeners (damage numbers, telemetry, knockback) all see the mitigated value. Unlike the
    ///     two negate-hooks above this one shapes damage rather than cancelling it (e.g. the
    ///     Berserker's rage damage reduction).
    /// </summary>
    public event Func<Damage, float> ScaleDamageTaken;

    public void ReceiveDamage(Damage damage)
    {
        if (IsDead)
        {
            return;
        }

        if (BlockDamage(damage))
        {
            return;
        }

        damage.value *= DamageTakenMultiplier(damage);

        currentHealth -= damage.value;

        if (damageFeedback != null)
        {
            damageFeedback.PlayFeedbacks();
        }

        OnDamaged?.Invoke(damage);

        if (currentHealth <= 0f)
        {
            if (PreventDeath())
            {
                // The handler restored health via Revive, which raised OnHealthChanged itself.
                return;
            }

            currentHealth = 0f;
            IsDead = true;
            if (deathFeedback != null)
            {
                deathFeedback.PlayFeedbacks();
            }
            OnDied?.Invoke();
        }

        OnHealthChanged?.Invoke();
    }

    /// <summary>Restores health and clears the dead latch. Used by a <see cref="TryPreventDeath" /> handler.</summary>
    public void Revive(float amount)
    {
        currentHealth = Mathf.Clamp(amount, 0.01f, MaxHealth);
        IsDead = false;
        OnHealthChanged?.Invoke();
    }

    /// <summary>Restores health up to <see cref="MaxHealth" /> (e.g. lifesteal). Ignored while dead — death is signalled, never healed away.</summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        OnHealthChanged?.Invoke();
    }

    private bool BlockDamage(Damage damage)
    {
        if (TryBlockDamage == null)
        {
            return false;
        }

        foreach (Delegate handler in TryBlockDamage.GetInvocationList())
        {
            if (((Func<Damage, bool>)handler).Invoke(damage))
            {
                return true;
            }
        }
        return false;
    }

    private float DamageTakenMultiplier(Damage damage)
    {
        if (ScaleDamageTaken == null)
        {
            return 1f;
        }

        float multiplier = 1f;
        foreach (Delegate handler in ScaleDamageTaken.GetInvocationList())
        {
            multiplier *= ((Func<Damage, float>)handler).Invoke(damage);
        }
        return Mathf.Max(0f, multiplier);
    }

    private bool PreventDeath()
    {
        if (TryPreventDeath == null)
        {
            return false;
        }

        foreach (Delegate handler in TryPreventDeath.GetInvocationList())
        {
            if (((Func<bool>)handler).Invoke())
            {
                return true;
            }
        }
        return false;
    }

    private void Start()
    {
        currentHealth = MaxHealth;
        OnHealthChanged?.Invoke();
    }

}
