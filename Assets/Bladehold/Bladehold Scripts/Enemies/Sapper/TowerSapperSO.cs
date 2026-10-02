using UnityEngine;

/// <summary>Tunables for <see cref="TowerSapper" />: how it finds a tower and how fast it drains it.</summary>
[CreateAssetMenu(fileName = "TowerSapperSO", menuName = "Scriptable Objects/Enemies/Tower Sapper")]
public class TowerSapperSO : ScriptableObject
{
    [Header("Finding a tower")]
    [Tooltip("Only towers within this distance are considered. Beyond it the sapper fights like a normal goblin.")]
    public float searchRadius = 60f;
    [Tooltip("Seconds between looks for a new tower while it has none.")]
    public float rescanInterval = 0.5f;

    [Header("Draining")]
    [Tooltip("Flat distance from the tower's centre at which the sapper can start draining. Keep it inside the turrets' blind spot (Arrow 3m) so a tower can't shoot its own sapper.")]
    public float drainRange = 2.8f;
    [Tooltip("Seconds between drain ticks.")]
    public float drainInterval = 1f;
    [Tooltip("Supply removed from the tower each tick.")]
    public int drainPerTick = 4;

    [Header("Animation")]
    [Tooltip("Animator trigger fired on each drain tick (the hacking swing).")]
    public string drainTrigger = "Attack";

    [Tooltip("HP a sapper hacks off a shut wall per drain tick when a wall is in its way (plan 17).")]
    [Min(0f)] public float wallDamagePerTick = 8f;
}
