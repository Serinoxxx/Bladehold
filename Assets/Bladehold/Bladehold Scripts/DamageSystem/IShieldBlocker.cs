/// <summary>
///     Implemented by shields or blocking targets that intercept attacks and halt the weapon's swing
///     (triggering the cut-through cap stop / recoil, as if the weapon struck too many enemies).
/// </summary>
public interface IShieldBlocker
{
    /// <summary>
    ///     Returns true if the incoming damage should be blocked and stop the attacker's weapon swing.
    /// </summary>
    bool ShouldBlockAttack(Damage damage);
}
