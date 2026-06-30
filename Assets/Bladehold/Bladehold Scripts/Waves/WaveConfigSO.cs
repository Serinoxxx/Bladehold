using UnityEngine;

/// <summary>
///     Tunable parameters for wave-based goblin spawning, read by <see cref="WaveSpawner" />. As with
///     <see cref="AIMovementSO" /> and <see cref="EnemySO" />, tune spawning by editing the asset rather
///     than the spawner script.
/// </summary>
[CreateAssetMenu(fileName = "WaveConfigSO", menuName = "Scriptable Objects/WaveConfigSO")]
public class WaveConfigSO : ScriptableObject
{
    [Header("Wave size")]
    [Tooltip("Total goblins to kill in wave 1.")]
    [Min(1)] public int baseGoblinCount = 5;
    [Tooltip("Extra goblins added to the total each subsequent wave (wave N total = base + (N-1) * this).")]
    [Min(0)] public int goblinsAddedPerWave = 3;

    [Header("Pacing")]
    [Tooltip("Maximum goblins alive at once. As they're killed, replacements spawn until the wave total is reached.")]
    [Min(1)] public int maxConcurrent = 10;
    [Tooltip("Seconds counted down before each wave begins (the \"Wave starts in {n}\" intermission).")]
    [Min(0)] public int timeBetweenWaves = 5;
    [Tooltip("Seconds between individual goblin spawns, so they trickle in rather than appearing all at once.")]
    [Min(0f)] public float spawnInterval = 0.5f;

    /// <summary>Total goblins that must be killed to clear the given (1-based) wave number.</summary>
    public int GoblinsForWave(int waveNumber)
    {
        int wavesAfterFirst = Mathf.Max(0, waveNumber - 1);
        return baseGoblinCount + wavesAfterFirst * goblinsAddedPerWave;
    }
}
