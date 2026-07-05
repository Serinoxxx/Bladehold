using UnityEngine;

/// <summary>
///     Physical build tunables for the runtime ragdoll (<see cref="EnemyRagdoll" />). Ragdolls are
///     never authored on the enemy prefabs — <see cref="EnemyRagdoll" /> builds bodies, joints, and
///     colliders from the Humanoid rig the first time an enemy is flung, sized from the actual bone
///     lengths so one asset serves every enemy scale (goblin 1.0, brute 1.25, …).
/// </summary>
[CreateAssetMenu(fileName = "RagdollConfigSO", menuName = "Scriptable Objects/RagdollConfigSO")]
public class RagdollConfigSO : ScriptableObject
{
    [Tooltip("Physics layer the ragdoll bone objects are moved to. Must exist in Tags & Layers; the collision matrix should disable Ragdoll×Ragdoll and Ragdoll×(character layer) while keeping Ragdoll×Default for the ground.")]
    public string ragdollLayerName = "Ragdoll";

    [Tooltip("Total mass distributed across the bodies by the per-part fractions (hips/chest heaviest, forearms lightest).")]
    public float totalMass = 40f;

    [Tooltip("Linear damping on every body.")]
    public float linearDamping = 0.05f;

    [Tooltip("Angular damping on every body; higher = the tumble settles faster.")]
    public float angularDamping = 0.6f;

    [Tooltip("Limb capsule radius as a fraction of the bone's length.")]
    public float limbRadiusRatio = 0.25f;

    [Tooltip("Head sphere collider radius in metres (before rig scale).")]
    public float headRadius = 0.12f;

    [Tooltip("Torso box collider depth in metres (before rig scale); width/height come from the rig.")]
    public float torsoThickness = 0.2f;

    [Header("Joint limits (applied uniformly to every CharacterJoint)")]
    public float lowTwistLimit = -40f;
    public float highTwistLimit = 20f;
    public float swing1Limit = 45f;
    public float swing2Limit = 30f;

    [Tooltip("CharacterJoint projection snaps a joint back when it drifts apart under load — keeps hard flings from dislocating limbs.")]
    public bool enableProjection = true;

    [Tooltip("Caps how fast a body may be pushed out of an overlap — prevents physics explosions when a fling clips geometry.")]
    public float maxDepenetrationVelocity = 5f;
}
