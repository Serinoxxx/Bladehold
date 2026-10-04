using UnityEngine;

/// <summary>
///     Enemy attribute: the player's ridden horse can't run through this enemy. Riding into it
///     (from inside <see cref="MountStopperSO.haltHalfAngle" /> of its facing) rears the horse in
///     place as if it hit a wall, and the horse can't push forward into it afterwards — steer round
///     it or back off. <see cref="HorseMotor" /> finds it through its crowd scan and asks
///     <see cref="Halts" />; this component never touches the horse itself.
/// </summary>
public class MountStopper : MonoBehaviour
{
    [SerializeField] private MountStopperSO data;
    [SerializeField] private Health health;
    [SerializeField] private KnockbackReceiver knockbackReceiver;

    private IShieldBlocker shieldBlocker;
    private bool anyError = false;

    /// <summary>Seconds the horse stays reared after hitting this enemy.</summary>
    public float RearLockSeconds => data != null ? data.rearLockSeconds : 0f;

    /// <summary>This enemy's Health, the source stamped on the horse's impact damage.</summary>
    public Health Health => health;

    /// <summary>
    ///     Damage a horse should take for running into this enemy at <paramref name="normalizedSpeed" />
    ///     (0..1 of its charge speed): <see cref="MountStopperSO.horseImpactDamage" /> scaled from
    ///     <see cref="MountStopperSO.minImpactDamageFraction" /> up to full. The horse applies it.
    /// </summary>
    public float HorseImpactDamage(float normalizedSpeed)
    {
        if (anyError || data.horseImpactDamage <= 0f) return 0f;
        return data.horseImpactDamage * Mathf.Lerp(data.minImpactDamageFraction, 1f, Mathf.Clamp01(normalizedSpeed));
    }

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("[MountStopper] MountStopperSO is not assigned in the inspector.", this);
            anyError = true;
        }
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError("[MountStopper] Health component is missing.", this);
                anyError = true;
            }
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }

        if (anyError)
        {
            return;
        }

        shieldBlocker = GetComponentInChildren<IShieldBlocker>(true);
        if (data.requireShieldBlock && shieldBlocker == null)
        {
            Debug.LogWarning("[MountStopper] Require Shield Block is on but there is no IShieldBlocker on this enemy; it will never stop a horse.", this);
        }
    }

    /// <summary>
    ///     Whether a horse at <paramref name="horse" /> (whose own Health is <paramref name="horseHealth" />)
    ///     is stopped by this enemy right now. Dead or knocked-down enemies never stop it.
    /// </summary>
    public bool Halts(Transform horse, Health horseHealth)
    {
        if (anyError || !enabled || health.IsDead) return false;
        if (knockbackReceiver != null && knockbackReceiver.IsIncapacitated) return false;

        Vector3 toHorse = horse.position - transform.position;
        toHorse.y = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (toHorse.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toHorse) > data.haltHalfAngle)
        {
            return false;
        }

        if (data.requireShieldBlock)
        {
            // Ask the shield the same question a hit from the horse would: broken, or coming from
            // behind, means it doesn't block — and doesn't stop the horse either.
            return shieldBlocker != null && shieldBlocker.ShouldBlockAttack(new Damage
            {
                source = horseHealth,
                sourcePosition = horse.position,
                direction = horse.forward,
            });
        }

        return true;
    }
}
