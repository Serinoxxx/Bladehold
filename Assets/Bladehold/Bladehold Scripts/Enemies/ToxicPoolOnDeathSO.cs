using UnityEngine;

[CreateAssetMenu(fileName = "ToxicPoolOnDeathSO", menuName = "Scriptable Objects/ToxicPoolOnDeathSO")]
public class ToxicPoolOnDeathSO : ScriptableObject
{
    [Tooltip("Radius of the toxic pool left where the mutant dies.")]
    public float poolRadius = 2.5f;

    [Tooltip("Seconds the pool persists before it evaporates.")]
    public float poolDuration = 6f;

    [Tooltip("Seconds between damage ticks while something lingers in the pool.")]
    public float tickInterval = 0.75f;

    [Tooltip("Damage dealt per tick to everything caught in the pool.")]
    public float tickDamage = 2f;

    [Tooltip("Type of damage dealt. Elemental — a pool has no swing to read, so it can never be parried anyway; it's stamped unparryable too.")]
    public DamageType damageType = DamageType.elemental;
}
