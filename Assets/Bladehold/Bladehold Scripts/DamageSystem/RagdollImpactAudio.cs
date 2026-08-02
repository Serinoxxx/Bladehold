using UnityEngine;
using MoreMountains.Tools;

/// <summary>
///     Attached to ragdoll bones (or the Pelvis) by <see cref="EnemyRagdoll"/>.
///     Listens to physical collisions while ragdolled and plays thud/body impact sound effects
///     with pitch/volume variance via <see cref="MMSoundManagerSoundPlayEvent"/>.
/// </summary>
public class RagdollImpactAudio : MonoBehaviour
{
    [SerializeField] private float minImpactSpeed = 2.5f;
    [SerializeField] private float cooldownSeconds = 0.12f;
    [SerializeField] private float minPitch = 0.85f;
    [SerializeField] private float maxPitch = 1.15f;

    private EnemyRagdoll ownerRagdoll;
    private float nextPlayTime;
    private AudioClip[] impactClips;

    public void Init(EnemyRagdoll ragdoll, AudioClip[] clips)
    {
        ownerRagdoll = ragdoll;
        impactClips = clips;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (ownerRagdoll != null && !ownerRagdoll.IsRagdolled) return;
        if (Time.time < nextPlayTime) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed) return;

        nextPlayTime = Time.time + cooldownSeconds;

        AudioClip clipToPlay = null;
        if (impactClips != null && impactClips.Length > 0)
        {
            clipToPlay = impactClips[Random.Range(0, impactClips.Length)];
        }

        if (clipToPlay != null)
        {
            float volume = Mathf.Clamp01(impactSpeed / 15f) * 0.9f + 0.1f;
            float pitch = Random.Range(minPitch, maxPitch);

            MMSoundManagerSoundPlayEvent.Trigger(
                clipToPlay,
                MMSoundManager.MMSoundManagerTracks.Sfx,
                collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position,
                loop: false,
                volume: volume,
                pitch: pitch
            );
        }
    }
}
