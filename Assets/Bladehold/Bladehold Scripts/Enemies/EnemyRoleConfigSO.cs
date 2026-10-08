using UnityEngine;

/// <summary>
///     The job a wave enemy is given when it spawns, so a horde splits its pressure instead of every
///     goblin doing the same thing. Read by <see cref="AITargetSelector" />; rolled by
///     <see cref="AITargetSelector.AssignSpawnRole" /> from <see cref="EnemyRoleConfigSO" /> weights.
/// </summary>
public enum EnemyRole
{
    /// <summary>No role: the legacy targeting (player, or the gate during Hold the Gate). Specials, scene-placed enemies.</summary>
    None = 0,
    /// <summary>Chases the player wherever they are.</summary>
    Hunter = 1,
    /// <summary>Holds a post round the active objective's object (cage, siege engine, wagon) and engages a player who comes near it.</summary>
    Guard = 2,
    /// <summary>Pushes to the gate (and the walls in its way) and attacks it; escorts the battering ram while one is rolling.</summary>
    Assault = 3
}

/// <summary>
///     Tunables for the enemy role split (Hunter / Guard / Assault). Loaded from
///     <c>Resources/EnemyRoleConfig</c>; with no asset the code defaults below apply.
/// </summary>
[CreateAssetMenu(fileName = "EnemyRoleConfig", menuName = "Scriptable Objects/Enemies/Enemy Role Config")]
public class EnemyRoleConfigSO : ScriptableObject
{
    private const string ResourcePath = "EnemyRoleConfig";

    [Header("Spawn role weights")]
    [Tooltip("Relative weight of Hunters (chase the player).")]
    [Min(0f)] public float hunterWeight = 40f;
    [Tooltip("Relative weight of Guards (hold the active objective's object). When the objective has no object, this weight is dropped and Hunter/Assault share the roll in proportion.")]
    [Min(0f)] public float guardWeight = 25f;
    [Tooltip("Relative weight of Assault (push to the gate/walls and attack them). With no gate in the scene they fall back to the player.")]
    [Min(0f)] public float assaultWeight = 35f;

    [Header("Guard")]
    [Tooltip("Inner radius (m) of the ring Guards stand on round the objective object.")]
    [Min(0f)] public float guardRingMin = 3f;
    [Tooltip("Outer radius (m) of the ring Guards stand on round the objective object.")]
    [Min(0f)] public float guardRingMax = 7f;
    [Tooltip("A player within this distance (m) of the guarded object draws its Guards onto them. Beyond it (and beyond the normal player engage range), they return to their posts.")]
    [Min(0f)] public float guardEngageRadius = 12f;
    [Tooltip("How fast each Guard's post walks round the ring (degrees per second), so they patrol instead of standing still. 0 = static posts.")]
    public float guardPatrolDegreesPerSecond = 6f;
    [Tooltip("Seconds between re-reads of the guarded object's position (it can move: the wagon). Keeps the objective lookup off the per-frame path.")]
    [Min(0.05f)] public float guardAnchorRefreshSeconds = 0.5f;

    /// <summary>Rolls a spawn role. <paramref name="guardAvailable" /> false drops the Guard weight (redistributed proportionally).</summary>
    public EnemyRole Roll(bool guardAvailable)
    {
        float guard = guardAvailable ? guardWeight : 0f;
        float total = hunterWeight + guard + assaultWeight;
        if (total <= 0f) return EnemyRole.None;

        float roll = Random.value * total;
        if (roll < hunterWeight) return EnemyRole.Hunter;
        if (roll < hunterWeight + guard) return EnemyRole.Guard;
        return EnemyRole.Assault;
    }

    private static EnemyRoleConfigSO cached;

    /// <summary>The asset at <c>Resources/EnemyRoleConfig</c>, or a defaults instance (warned once) when it's missing.</summary>
    public static EnemyRoleConfigSO Current
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<EnemyRoleConfigSO>(ResourcePath);
                if (cached == null)
                {
                    Debug.LogWarning($"[EnemyRoleConfigSO] No asset at Resources/{ResourcePath}; using code defaults.");
                    cached = CreateInstance<EnemyRoleConfigSO>();
                    cached.hideFlags = HideFlags.DontSave;
                }
            }
            return cached;
        }
    }

    private void OnValidate()
    {
        if (guardRingMax < guardRingMin) guardRingMax = guardRingMin;
    }
}
