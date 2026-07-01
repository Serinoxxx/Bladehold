using System;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] HealthSO healthData;

    [SerializeField] float currentHealth;

    [SerializeField] MMF_Player damageFeedback;

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

    /// <summary>True once health has reached zero. Latches; further damage is ignored.</summary>
    public bool IsDead { get; private set; }

    /// <summary>The configured maximum health, or 0 if <c>healthData</c> isn't assigned.</summary>
    public float MaxHealth => healthData != null ? healthData.maxHealth : 0f;

    /// <summary>
    ///     Checked once health would reach zero, before <see cref="IsDead" /> latches or <see cref="OnDied" />
    ///     fires. A handler that wants to intercept a lethal hit (e.g. a revive effect) returns <c>true</c> and
    ///     is responsible for restoring health itself via <see cref="Revive" />; the death is then skipped
    ///     entirely for this hit. Returning <c>false</c> lets death proceed normally. Multiple handlers are
    ///     supported (checked via the invocation list) so one handler's <c>true</c> can't be silently dropped
    ///     by another's <c>false</c>.
    /// </summary>
    public event Func<bool> TryPreventDeath;

    public void ReceiveDamage(Damage damage)
    {
        if (IsDead)
        {
            return;
        }

        currentHealth -= damage.value;
        damageFeedback.PlayFeedbacks();

        OnDamaged?.Invoke(damage);

        if (currentHealth <= 0f)
        {
            if (PreventDeath())
            {
                return;
            }

            currentHealth = 0f;
            IsDead = true;
            OnDied?.Invoke();
        }
    }

    /// <summary>Restores health and clears the dead latch. Used by a <see cref="TryPreventDeath" /> handler.</summary>
    public void Revive(float amount)
    {
        currentHealth = Mathf.Clamp(amount, 0.01f, MaxHealth);
        IsDead = false;
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
        currentHealth = healthData.maxHealth;
    }

}
