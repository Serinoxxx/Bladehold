using UnityEngine;

/// <summary>
///     Tunables for <see cref="EnemyPrewarmer" />: the behind-the-loading-screen rehearsal that spawns,
///     renders and kills one of each enemy a battle scene can field, so the first real spawn and first
///     real death don't pay the one-time costs (shader compiles, particle/pool instantiation, audio
///     loads, first-run code paths) mid-fight.
/// </summary>
[CreateAssetMenu(fileName = "EnemyPrewarmConfig", menuName = "Scriptable Objects/EnemyPrewarmConfigSO")]
public class EnemyPrewarmConfigSO : ScriptableObject
{
    [Header("Staging")]
    [Tooltip("Metres in front of the main camera the rehearsal enemies stand. Far enough that they can't reach the player before they're cleared, close enough to be inside the camera frustum (frustum-culled objects warm nothing).")]
    [Min(2f)] public float stagingDistance = 22f;
    [Tooltip("Sideways spacing between staged enemies, in metres.")]
    [Min(0.5f)] public float spacing = 1.6f;
    [Tooltip("NavMesh search radius when snapping the staging spots onto the NavMesh.")]
    [Min(0.5f)] public float navMeshSampleRadius = 6f;

    [Header("Timing")]
    [Tooltip("Frames the staged enemies are rendered alive before the rehearsal kills (Start, MMF init, first draw).")]
    [Min(1)] public int aliveFrames = 3;
    [Tooltip("Real seconds the rehearsal corpses are kept (death feedbacks, ragdoll landing, blood) before they're destroyed.")]
    [Min(0.1f)] public float deathSeconds = 0.9f;

    [Header("Pools")]
    [Tooltip("Instances pre-made for every pooled particle burst (blood etc.) found on a staged enemy's MMF players, so a mass kill doesn't grow the pool mid-fight.")]
    [Min(0)] public int particlePoolSize = 8;
}
