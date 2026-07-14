using UnityEngine;

[CreateAssetMenu(fileName = "WhirlwindAttackSO", menuName = "Scriptable Objects/WhirlwindAttackSO")]
public class WhirlwindAttackSO : ScriptableObject
{
    [Tooltip("Radius of the whirlwind: both the damage pulse and the projectile-shatter zone.")]
    public float radius = 3.5f;

    [Tooltip("Seconds between damage pulses.")]
    public float pulseInterval = 1f;

    [Tooltip("Damage dealt per pulse to everything caught in the whirlwind.")]
    public float damage = 10f;

    [Tooltip("Type of damage dealt. A spinning wall of steel has no single swing to read — pulses are stamped unparryable regardless.")]
    public DamageType damageType = DamageType.elemental;
}
