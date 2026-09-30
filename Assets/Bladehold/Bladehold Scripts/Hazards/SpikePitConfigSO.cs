using UnityEngine;

/// <summary>
///     Tunables for <see cref="SpikePit" /> volumes (ravine floors under bridges). One asset is shared by
///     every pit in every scene, so pit lethality is tuned here, not per instance.
/// </summary>
[CreateAssetMenu(fileName = "SpikePitConfig", menuName = "Scriptable Objects/Hazards/Spike Pit Config")]
public class SpikePitConfigSO : ScriptableObject
{
    [Tooltip("Damage dealt once when something lands in the pit. Normal goblins have 10 HP, so 12 kills them outright.")]
    public float impactDamage = 12f;

    public DamageType damageType = DamageType.sharp;

    [Tooltip("Seconds a victim must be out of the pit before a second fall can hurt it again.")]
    public float reentryCooldown = 2f;

    [Tooltip("Seconds between overlap polls. Falls are slow enough that 10 Hz never misses a landing.")]
    public float pollInterval = 0.1f;
}
