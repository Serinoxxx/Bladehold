using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Attached to individual ragdoll bones by <see cref="EnemyRagdoll"/>.
///     Listens to physical collisions while ragdolled, scaling particle splash effects, blood decals,
///     and sound effects by the collision impact speed and <see cref="RagdollBodyPartType"/>.
/// </summary>
public class RagdollBloodImpact : MonoBehaviour
{
    private EnemyRagdoll ownerRagdoll;
    private RagdollConfigSO config;
    [SerializeField] private RagdollBodyPartType bodyPartType;
    private AudioClip[] impactSounds;
    private float nextImpactTime;

    public RagdollBodyPartType BodyPartType => bodyPartType;

    public void Init(EnemyRagdoll ragdoll, RagdollConfigSO ragdollConfig, RagdollBodyPartType partType, AudioClip[] sounds)
    {
        ownerRagdoll = ragdoll;
        config = ragdollConfig;
        bodyPartType = partType;
        impactSounds = sounds;
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

        // 1. Spawn Blood Particle Effect
        if (config.bloodParticlePrefab != null)
        {
            Quaternion particleRotation = Quaternion.LookRotation(normal);
            ParticleSystem fx = ParticlePool.Get(config.bloodParticlePrefab, point, particleRotation);
            if (fx != null)
            {
                // Scale particle system transform & emission speed by body part & speed
                float fxScale = bodyPartMultiplier * Mathf.Lerp(0.6f, 1.5f, speedFactor);
                fx.transform.localScale = Vector3.one * fxScale;

                ParticleSystem.MainModule main = fx.main;
                main.startSpeedMultiplier *= Mathf.Lerp(0.8f, 1.8f, speedFactor);

                int particleCount = Mathf.RoundToInt(Mathf.Lerp(5, 30, speedFactor) * bodyPartMultiplier);
                fx.Emit(particleCount);

                ParticlePool.Release(config.bloodParticlePrefab, fx, 2.5f);
            }
        }

        // 2. Spawn Blood Decal
        float decalSize = bodyPartMultiplier * Mathf.Lerp(config.minDecalSize, config.maxDecalSize, speedFactor);
        BloodDecalManager.SpawnDecal(point, normal, decalSize, config);

        // 3. Play Impact Sound Effect
        if (impactSounds != null && impactSounds.Length > 0)
        {
            AudioClip clipToPlay = impactSounds[Random.Range(0, impactSounds.Length)];
            if (clipToPlay != null)
            {
                float volume = Mathf.Clamp01(impactSpeed / 15f) * 0.9f + 0.1f;
                float pitch = Random.Range(0.85f, 1.15f);

                MMSoundManagerSoundPlayEvent.Trigger(
                    clipToPlay,
                    MMSoundManager.MMSoundManagerTracks.Sfx,
                    point,
                    loop: false,
                    volume: volume,
                    pitch: pitch
                );
            }
        }
    }

    /// <summary>
    /// Triggers a direct blood impact/splash at the specified world position and normal.
    /// </summary>
    public void TriggerDirectImpact(Vector3 point, Vector3 normal, float impactSpeed)
    {
        if (config != null && config.bloodParticlePrefab != null)
        {
            Quaternion particleRotation = Quaternion.LookRotation(normal);
            ParticleSystem fx = ParticlePool.Get(config.bloodParticlePrefab, point, particleRotation);
            if (fx != null)
            {
                fx.transform.localScale = Vector3.one * 1.2f;
                fx.Emit(20);
                ParticlePool.Release(config.bloodParticlePrefab, fx, 2.5f);
            }
        }
        if (config != null)
        {
            BloodDecalManager.SpawnDecal(point, normal, 1.2f, config);
        }
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
