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
}

public enum DamageType
{
    sharp = 0,
    blunt = 1,
    elemental = 2
}
