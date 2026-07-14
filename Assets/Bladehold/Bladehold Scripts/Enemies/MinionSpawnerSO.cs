using UnityEngine;

[CreateAssetMenu(fileName = "MinionSpawnerSO", menuName = "Scriptable Objects/MinionSpawnerSO")]
public class MinionSpawnerSO : ScriptableObject
{
    [Tooltip("Roster CSV id of the minion type to spawn (its row's stat overrides are applied to every minion).")]
    public string minionId = "dwarf";

    [Tooltip("Seconds between spawn batches.")]
    public float spawnInterval = 8f;

    [Tooltip("Minions spawned per batch (clamped so Max Alive Minions is never exceeded).")]
    public int spawnCount = 2;

    [Tooltip("Max minions from this spawner alive at once — the golem stops production at the cap so a camped golem can't flood the arena (and the wave total, which each registered minion grows) unboundedly.")]
    public int maxAliveMinions = 6;

    [Tooltip("Minions appear NavMesh-snapped within this ring around the golem.")]
    public float spawnRadius = 2.5f;
}
