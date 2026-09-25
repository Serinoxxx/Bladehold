using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Attached to individual ragdoll bones by <see cref="EnemyRagdoll"/>.
///     Listens to physical collisions while ragdolled and plays the ragdoll's
///     <see cref="EnemyRagdoll.BloodImpactFeedback"/> (turned to face the contact normal) plus a blood decal,
///     both scaled by the collision speed and <see cref="RagdollBodyPartType"/>.
/// </summary>
public class RagdollBloodImpact : MonoBehaviour
{
    private EnemyRagdoll ownerRagdoll;
    private RagdollConfigSO config;
    [SerializeField] private RagdollBodyPartType bodyPartType;
    private float nextImpactTime;

    public RagdollBodyPartType BodyPartType => bodyPartType;

    public void Init(EnemyRagdoll ragdoll, RagdollConfigSO ragdollConfig, RagdollBodyPartType partType)
    {
        ownerRagdoll = ragdoll;
        config = ragdollConfig;
        bodyPartType = partType;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (ownerRagdoll != null && !ownerRagdoll.IsRagdolled) return;
        if (config == null) return;
        if (Time.time < nextImpactTime) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < config.minImpactSpeed) return;

        nextImpactTime = Time.time + config.impactCooldown;

        // Calculate impact factor (0.0 to 1.0)
        float speedFactor = Mathf.Clamp01((impactSpeed - config.minImpactSpeed) / Mathf.Max(0.01f, config.maxImpactSpeed - config.minImpactSpeed));

        // Body part base scale multiplier
        float bodyPartMultiplier = GetBodyPartMultiplier(bodyPartType);

        // Contact point and normal
        Vector3 point = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Vector3 normal = collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector3.up;

        // 1. Blood splash. Intensity 1 = a full-speed torso hit (the biggest body part); smaller parts
        // and slower hits land further down the feedback's ranges.
        MMF_Player feedback = ownerRagdoll != null ? ownerRagdoll.BloodImpactFeedback : null;
        if (feedback != null)
        {
            float maxMultiplier = Mathf.Max(0.01f, Mathf.Max(config.torsoBaseScale, config.headBaseScale, config.limbBaseScale));
            feedback.transform.rotation = Quaternion.LookRotation(normal);
            feedback.PlayFeedbacks(point, speedFactor * bodyPartMultiplier / maxMultiplier);
        }

        // 2. Spawn Blood Decal
        float decalSize = bodyPartMultiplier * Mathf.Lerp(config.minDecalSize, config.maxDecalSize, speedFactor);
        BloodDecalManager.SpawnDecal(point, normal, decalSize, config);
    }

    private float GetBodyPartMultiplier(RagdollBodyPartType type)
    {
        switch (type)
        {
            case RagdollBodyPartType.Torso:
                return config.torsoBaseScale;
            case RagdollBodyPartType.Head:
                return config.headBaseScale;
            case RagdollBodyPartType.Limb:
            default:
                return config.limbBaseScale;
        }
    }
}
