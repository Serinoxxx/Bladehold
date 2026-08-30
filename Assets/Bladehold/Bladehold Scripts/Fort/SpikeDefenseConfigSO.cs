using UnityEngine;

/// <summary>
///     Configurable tunables for the Fort Ground Spike Trap defense, which periodically thrusts
///     spikes upward to deal AoE damage in a box volume.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable Objects/Fort/Spike Defense Config", fileName = "SpikeDefenseConfig")]
public class SpikeDefenseConfigSO : ScriptableObject
{
    [Header("Trap Cycle")]
    [Tooltip("Seconds between spike thrust attacks.")]
    [Range(0.5f, 10f)]
    public float thrustInterval = 2.5f;

    [Tooltip("Duration in seconds that spikes stay extended/active during a thrust.")]
    [Range(0.1f, 2f)]
    public float activeThrustDuration = 0.5f;

    [Tooltip("Size of the damage box volume.")]
    public Vector3 boxSize = new Vector3(3f, 2f, 3f);

    [Tooltip("Center offset of the damage box volume relative to the trap origin.")]
    public Vector3 boxCenterOffset = new Vector3(0f, 1f, 0f);

    [Header("Damage")]
    [Tooltip("Base damage dealt per thrust at Level 1.")]
    public float baseDamage = 35f;

    [Tooltip("Per-level damage increment.")]
    public float damagePerLevel = 25f;

    [Tooltip("Upward knockback impulse applied to struck enemies.")]
    public float upwardKnockback = 4f;
}
