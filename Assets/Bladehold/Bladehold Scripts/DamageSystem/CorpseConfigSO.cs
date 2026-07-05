using UnityEngine;

/// <summary>
///     Tunables for corpse cleanup (<see cref="CorpseDespawner" /> / <see cref="CorpseManager" />).
///     Corpses linger for the battlefield-litter feel, then sink into the ground and despawn so a
///     long run's kill count can't accumulate into unbounded scene weight.
/// </summary>
[CreateAssetMenu(fileName = "CorpseConfigSO", menuName = "Scriptable Objects/CorpseConfigSO")]
public class CorpseConfigSO : ScriptableObject
{
    [Tooltip("Seconds after death before the corpse's Animator is disabled (once the death clip has settled). The biggest corpse saving — an enabled Animator keeps evaluating forever.")]
    public float animatorDisableDelay = 5f;

    [Tooltip("Seconds a corpse lies around before sinking away. 0 = corpses never despawn on their own (the Max Corpses cap still applies).")]
    public float corpseLifetime = 20f;

    [Tooltip("How far the corpse sinks below its resting position before being destroyed.")]
    public float sinkDepth = 1.5f;

    [Tooltip("Seconds the sink takes.")]
    public float sinkDuration = 1.5f;

    [Tooltip("Maximum corpses kept in the scene at once; the oldest sinks early when exceeded (enforced by CorpseManager). 0 = uncapped.")]
    public int maxCorpses = 50;
}
