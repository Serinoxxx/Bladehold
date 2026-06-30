using UnityEngine;

[CreateAssetMenu(fileName = "AIMovementSO", menuName = "Scriptable Objects/AIMovementSO")]
public class AIMovementSO : ScriptableObject
{
    [Header("Pathfinding")]
    [Tooltip("NavMeshAgent movement speed.")]
    public float speed = 5f;
    [Tooltip("Seconds between SetDestination calls (throttles pathfinding cost).")]
    public float updateInterval = 0.1f;

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
