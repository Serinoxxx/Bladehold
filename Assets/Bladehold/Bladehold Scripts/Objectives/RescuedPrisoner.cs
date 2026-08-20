using System;
using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Prisoner NPC inside a cage. When released, plays a cheer animation and sound,
///     triggers spring feedback, and vanishes in a puff of smoke after a short delay.
/// </summary>
public class RescuedPrisoner : MonoBehaviour
{
    [Header("Animation & Juiciness")]
    [Tooltip("Animator driving the prisoner model.")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator trigger parameter for cheering (e.g. 'Cheer').")]
    [SerializeField] private string cheerTriggerName = "Cheer";

    [Tooltip("MMF_Player played when released (e.g. spring scale bounce, celebratory glow).")]
    [SerializeField] private MMF_Player cheerFeedback;

    [Header("Visual Effects & Audio")]
    [Tooltip("Prefab spawned when the prisoner disappears (e.g. Loot_Poof / dust cloud).")]
    [SerializeField] private GameObject smokePoofPrefab;

    [Tooltip("Audio clip played when cheer animation begins.")]
    [SerializeField] private AudioClip cheerSound;

    [Tooltip("Audio clip played when the prisoner disappears in smoke.")]
    [SerializeField] private AudioClip poofSound;

    [Tooltip("Seconds the prisoner cheers before vanishing in smoke.")]
    [SerializeField] private float cheerDuration = 2.5f;

    private bool isReleased;

    public bool IsReleased => isReleased;
    public event Action<RescuedPrisoner> OnReleased;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    /// <summary>Called when the surrounding cage is broken.</summary>
    public void Release()
    {
        if (isReleased) return;
        isReleased = true;

        // Trigger cheer animation
        if (animator != null)
        {
            animator.SetTrigger(cheerTriggerName);
        }

        // Trigger MMF feedback
        if (cheerFeedback != null)
        {
            cheerFeedback.PlayFeedbacks();
        }

        // Play cheering voice / audio
        if (cheerSound != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = transform.position;
            options.Volume = 0.9f;
            options.Pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            MMSoundManagerSoundPlayEvent.Trigger(cheerSound, options);
        }

        OnReleased?.Invoke(this);
        StartCoroutine(CheerAndDespawnRoutine());
    }

    private IEnumerator CheerAndDespawnRoutine()
    {
        yield return new WaitForSeconds(cheerDuration);

        // Spawn puff of smoke VFX
        if (smokePoofPrefab != null)
        {
            Instantiate(smokePoofPrefab, transform.position + Vector3.up * 0.8f, Quaternion.identity);
        }

        // Play puff of smoke sound
        if (poofSound != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = transform.position;
            options.Volume = 1.0f;
            MMSoundManagerSoundPlayEvent.Trigger(poofSound, options);
        }

        Destroy(gameObject);
    }
}
