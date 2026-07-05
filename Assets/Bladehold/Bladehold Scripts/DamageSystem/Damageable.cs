using UnityEngine;

public interface IDamageable
{
    public void ReceiveDamage(Damage damage);
}

public class Damage
{
    public float value;
    public DamageType type;

    /// <summary>True when this hit rolled a critical strike (value already scaled). Listeners may render it differently.</summary>
    public bool isCritical;

    /// <summary>
    ///     Magnitude of the knockback impulse to apply to the target (0 = none). A
    ///     <see cref="KnockbackReceiver" /> on the target reacts to it via <see cref="Health.OnDamaged" />.
    /// </summary>
    public float knockbackForce;

    /// <summary>World position the hit came from; the target is pushed away from this point.</summary>
    public Vector3 sourcePosition;

    /// <summary>
    ///     Impulse rating of this hit, compared against the target's impulse resistance by its
    ///     <see cref="ImpulseReceiver" />: at resistance or above the target is ragdoll-flung, within 1
    ///     below it is knocked down, further below nothing extra happens. 0 = no impulse on this hit.
    ///     Stamped by <see cref="DamageTrigger" /> while the player's Impulse buff is active.
    /// </summary>
    public float impulsePower;

    /// <summary>
    ///     Launch speed in m/s (applied as a velocity change on the ragdoll bodies) when the impulse
    ///     wins the resistance check. Already includes power/stack/charge amplification.
    /// </summary>
    public float impulseForce;
}

public enum DamageType
{
    sharp = 0,
    blunt = 1,
    elemental = 2
}
