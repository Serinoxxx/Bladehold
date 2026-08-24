using UnityEngine;

/// <summary>
///     Configurable tunables for the Fort Spike Barricade defense, including contact damage,
///     ragdoll multipliers, embed penetration depth, and corpse dangling physics.
/// </summary>
[CreateAssetMenu(menuName = "Scriptable Objects/Fort/Spike Defense Config", fileName = "SpikeDefenseConfig")]
public class SpikeDefenseConfigSO : ScriptableObject
{
    [Header("Impale & Embedding")]
    [Tooltip("How deep (in meters) the corpse embeds into the spike barricade upon a lethal impale.")]
    [Range(0.2f, 3.5f)]
    public float embedDepth = 1.5f;

    [Tooltip("Duration in seconds that the impaled bone stays locked to the spike before settling.")]
    [Range(1f, 15f)]
    public float impaleDuration = 6.0f;

    [Tooltip("If true, non-pinned ragdoll limbs remain dynamic physics bodies with gravity so the corpse dangles naturally.")]
    public bool allowLimbDangle = true;

    [Header("Damage & Timing")]
    [Tooltip("Base contact damage per tick at Level 1.")]
    public float baseDamage = 16f;

    [Tooltip("Per-level damage increment.")]
    public float damagePerLevel = 14f;

    [Tooltip("Damage multiplier applied when struck while ragdolled/airborne.")]
    public float ragdollMultiplier = 5f;

    [Tooltip("Minimum time between damage ticks per individual enemy.")]
    public float hitCooldownPerEnemy = 0.5f;
}
