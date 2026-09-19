using UnityEngine;

/// <summary>
///     Shootable explosive barrel held by the Powder Keg unit above its head.
///     Implements <see cref=\"IDamageable\"/> so arrows and ranged projectiles that hit the
///     barrel directly detonate the unit immediately, dealing area-of-effect damage to all nearby units.
/// </summary>
public class PowderKegBarrel : MonoBehaviour, IDamageable
{
    [SerializeField] private PowderKegAttack attackController;

    private void Awake()
    {
        if (attackController == null)
        {
            attackController = GetComponentInParent<PowderKegAttack>();
        }
    }

    public void ReceiveDamage(Damage damage)
    {
        if (attackController == null)
        {
            attackController = GetComponentInParent<PowderKegAttack>();
        }

        if (attackController == null) return;

        // An arrow or projectile hitting the barrel triggers instant detonation
        if (damage != null && (damage.isProjectile || damage.IsPlayerOwned))
        {
            attackController.DetonateFromArrow(damage.sourcePosition);
        }
    }
}
