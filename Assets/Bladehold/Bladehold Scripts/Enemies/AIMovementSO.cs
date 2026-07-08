using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "AIMovementSO", menuName = "Scriptable Objects/AIMovementSO")]
public class AIMovementSO : ScriptableObject
{
    [Header("Pathfinding")]
    [Tooltip("NavMeshAgent movement speed.")]
    public float speed = 5f;
    [Tooltip("Seconds between SetDestination calls (throttles pathfinding cost).")]
    public float updateInterval = 0.1f;

    [Header("Performance — Avoidance")]
    [Tooltip("Obstacle-avoidance quality while within Far Distance of the player (the dense front ring, where yielding matters). Applied to the agent in code, overriding the prefab's setting.")]
    public ObstacleAvoidanceType nearAvoidance = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    [Tooltip("Obstacle-avoidance quality beyond Far Distance (open-field march, where mutual avoidance is wasted cost).")]
    public ObstacleAvoidanceType farAvoidance = ObstacleAvoidanceType.NoObstacleAvoidance;
    [Tooltip("Distance from the player that separates the near/far avoidance tiers and repath rates.")]
    public float farDistance = 15f;
    [Tooltip("Each agent rolls a random avoidance priority in [min, max]. Unequal priorities let goblins shoulder past each other instead of mutually oscillating — cheaper and better-looking than a uniform value.")]
    [Range(0, 99)] public int avoidancePriorityMin = 30;
    [Range(0, 99)] public int avoidancePriorityMax = 70;

    [Header("Performance — Repath")]
    [Tooltip("Seconds between SetDestination calls while beyond Far Distance (Update Interval applies when near).")]
    public float farRepathInterval = 0.4f;

    [Header("Performance — Animation")]
    [Tooltip("Set the enemy Animator's culling mode to CullUpdateTransforms so off-screen enemies skip skeleton/skinning work. The state machine keeps running, so Death/Cheer/Attack triggers fired off-screen never pop.")]
    public bool cullOffscreenAnimators = true;
    [Tooltip("Within this distance of the player, locomotion animation updates every frame; beyond it, updates are frame-sliced.")]
    public float animationFullRateDistance = 15f;
    [Tooltip("Beyond Animation Full Rate Distance, each enemy ticks its locomotion animation every Nth frame (staggered per enemy). 1 = never sliced.")]
    [Min(1)] public int animationFarFrameInterval = 3;

    [Header("Facing")]
    [Tooltip("Degrees per second the enemy keeps turning toward its target after the NavMeshAgent has stopped at its stopping distance (agents only auto-rotate while moving).")]
    public float stoppedTurnSpeed = 240f;

    [Header("Animation Gaits")]
    [Tooltip("Top speed of the walk gait. Gait thresholds are derived halfway between these three speeds.")]
    public float walkSpeed = 1.6f;
    [Tooltip("Default running speed.")]
    public float runSpeed = 3.5f;
    [Tooltip("Top sprint speed.")]
    public float sprintSpeed = 7f;

    [Header("Animation Lean")]
    [Tooltip("Optional curve mapping normalised speed (speed / sprintSpeed) to lean amount. Leave empty to disable lean.")]
    public AnimationCurve leanCurve;
}
