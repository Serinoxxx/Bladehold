using UnityEngine;

[CreateAssetMenu(fileName = "PowderKegAttackSO", menuName = "Scriptable Objects/PowderKegAttackSO")]
public class PowderKegAttackSO : ScriptableObject
{
    [Header("Gate Proximity Detonation")]
    [Tooltip("Distance (in metres) to the gate that triggers the detonation sequence.")]
    public float triggerGateRange = 2f;

    [Tooltip("Damage dealt to all targets (including the gate) when detonating near the gate.")]
    public float gateExplosionDamage = 25f;

    [Tooltip("Wind-up pause before gate slam detonation in seconds.")]
    public float slamWindupSeconds = 0.25f;

    [Header("Arrow Detonation & General Explosion")]
    [Tooltip("Base damage dealt when detonated by an arrow hit.")]
    public float baseExplosionDamage = 25f;

    [Tooltip("Radius of the explosion's damage area.")]
    public float explosionRadius = 4.5f;

    [Tooltip("Type of damage dealt. Elemental = fire, unparryable.")]
    public DamageType damageType = DamageType.elemental;

    [Tooltip("Knockback force applied to victims caught in the blast.")]
    public float knockbackForce = 12f;
}
