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
    ///     The attacker's own damage sink (e.g. its <see cref="Health" />), if the hit came from a
    ///     single identifiable source. Used by retaliation effects (e.g. the player's Counterstrike
    ///     skill) to hit back at whoever landed the blocked hit. Null for hits with no single
    ///     attacker (e.g. AoE ground effects) or where the source didn't bother stamping it.
    /// </summary>
    public IDamageable source;

    /// <summary>
    ///     True for hits that can never be parried (see <see cref="Parry" />), regardless of
    ///     <see cref="type" /> or facing — e.g. the Troll's ground slam, a wide AoE with no single
    ///     directional swing to read and block. Doesn't affect <see cref="DamageBlocker" />'s
    ///     omnidirectional auto-block, which isn't about facing an attacker.
    /// </summary>
    public bool unparryable;

    /// <summary>
    ///     True if the damage was dealt by a projectile (e.g. arrows, thrown axes, wand missiles) rather than a direct melee hit.
    /// </summary>
    public bool isProjectile;
}

public enum DamageType
{
    sharp = 0,
    blunt = 1,
    elemental = 2
}
