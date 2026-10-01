using UnityEngine;

/// <summary>
///     A shield-bearer's guard against missiles (the shielded skeletons): projectile hits are scaled by
///     <see cref="ProjectileResistanceSO.projectileMultiplier" />, from any direction. Hooks this enemy's own
///     <see cref="Health.ScaleDamageTaken" /> (the <see cref="ArmorPlating" /> pattern), so damage numbers show
///     the reduced value. Ballista bolts (<see cref="Damage.piercesShields" />) and melee hit at full.
/// </summary>
public class ProjectileResistance : MonoBehaviour
{
    [SerializeField] private ProjectileResistanceSO data;
    [SerializeField] private Health health;

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("ProjectileResistanceSO is not assigned in the inspector.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.", this);
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.ScaleDamageTaken += HandleScaleDamageTaken;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.ScaleDamageTaken -= HandleScaleDamageTaken;
        }
    }

    private float HandleScaleDamageTaken(Damage damage)
    {
        if (anyError || damage == null || !damage.isProjectile)
        {
            return 1f;
        }
        if (damage.piercesShields && data.pierceIgnoresResistance)
        {
            return 1f;
        }
        return data.projectileMultiplier;
    }
}
