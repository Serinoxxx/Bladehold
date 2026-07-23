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
    [Min(1)] public int baseGoblinCount = 8;
    [Tooltip("Extra goblins added to the total each subsequent wave (wave N total = base + (N-1) * this).")]
    [Min(0)] public int goblinsAddedPerWave = 4;

    [Header("Pacing")]
    [Tooltip("Maximum goblins alive at once. As they're killed, replacements spawn until the wave total is reached.")]
    [Min(1)] public int maxConcurrent = 12;
    [Tooltip("Seconds counted down before each wave begins (the \"Wave starts in {n}\" intermission).")]
    [Min(0)] public int timeBetweenWaves = 5;
    [Tooltip("Seconds between individual goblin spawns within a group burst.")]
    [Min(0f)] public float spawnInterval = 0.2f;
    [Tooltip("Seconds between periodic group spawns (e.g. 30 seconds for periodic wave bursts).")]
    [Min(0f)] public float spawnBatchInterval = 30f;
    [Tooltip("Maximum number of goblins to spawn in a single periodic group batch (capped by maxConcurrent).")]
    [Min(1)] public int spawnBatchSize = 12;

    [Header("Audio")]
    [Tooltip("Horn audio clip played once when a periodic group wave batch spawns. Designers plug in clip here.")]
    public AudioClip groupSpawnHornSound;
    [Tooltip("Volume multiplier for the group spawn horn sound.")]
    [Range(0f, 1f)] public float hornVolume = 1f;

    /// <summary>Total goblins that must be killed to clear the given (1-based) wave number.</summary>
    public int GoblinsForWave(int waveNumber)
    {
        int wavesAfterFirst = Mathf.Max(0, waveNumber - 1);
        return baseGoblinCount + wavesAfterFirst * goblinsAddedPerWave;
    }
}
