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
            currentHealth = 0f;
            IsDead = true;
            OnDied?.Invoke();
        }
    }

    private void Start()
    {
        currentHealth = healthData.maxHealth;
    }

}
