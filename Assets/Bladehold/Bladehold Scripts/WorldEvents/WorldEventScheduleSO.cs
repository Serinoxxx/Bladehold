using UnityEngine;

/// <summary>
///     When <see cref="WorldEventDirector" /> rolls a world event: at most one per wave, a random delay into
///     the wave, and a per-wave chance. Shared by every battle scene so pacing is tuned in one place.
/// </summary>
[CreateAssetMenu(fileName = "WorldEventSchedule", menuName = "Scriptable Objects/World Events/Schedule")]
public class WorldEventScheduleSO : ScriptableObject
{
    [Tooltip("Chance (0-1) that a wave gets an event at all.")]
    [Range(0f, 1f)] public float chancePerWave = 0.6f;

    [Tooltip("Seconds into the wave before the event can start (random between x and y).")]
    public Vector2 startDelayRange = new Vector2(20f, 40f);

    [Tooltip("Never roll the same event twice in a row when another is allowed.")]
    public bool avoidRepeat = true;

    [Tooltip("Waves (1-based) before this never roll an event: let the player learn the map first.")]
    [Min(1)] public int firstEligibleWave = 2;
}
