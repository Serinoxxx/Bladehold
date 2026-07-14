using UnityEngine;

/// <summary>
///     The Ancient Queen's armor: light hits glance off. Hooks this enemy's own
///     <see cref="Health.ScaleDamageTaken" /> (the <see cref="RageBuff" /> multiplier-hook precedent —
///     the scaled value is what every <see cref="Health.OnDamaged" /> listener sees, so damage numbers
///     stay truthful) and scales any hit below <see cref="ArmorPlatingSO.lightHitThreshold" /> by
///     <see cref="ArmorPlatingSO.lightHitMultiplier" />; heavy/charged hits pass through untouched.
/// </summary>
public class ArmorPlating : MonoBehaviour
{
    [SerializeField] private ArmorPlatingSO data;
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
            Debug.LogError("ArmorPlatingSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
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
        if (anyError)
        {
            return 1f;
        }

        return damage.value < data.lightHitThreshold ? data.lightHitMultiplier : 1f;
    }
}
