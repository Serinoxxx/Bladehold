using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Shared elemental effects on the Player prefab. The burst feedbacks are MMF players (sound + VFX)
///     played at a world position via <see cref="PlayAt" />. The raw VFX/clip fields below are legacy:
///     they still serve callers that haven't moved to MMF yet (plan 09 batches B-D), then get deleted.
/// </summary>
public class ElementalEffectsManager : MonoBehaviour
{
    public static ElementalEffectsManager Instance { get; private set; }

    [Header("Burst Feedbacks (MMF, played at the burst position)")]
    [Tooltip("Ice Shards burst when a ranged kill shatters an enemy (bow and throwing axe).")]
    public MMF_Player iceShatterFeedback;
    [Tooltip("Thermal Shock explosion (Inferno ultimate burst).")]
    public MMF_Player thermalShockFeedback;
    [Tooltip("Superconductor lightning zap (Storm Eye ultimate strikes).")]
    public MMF_Player superconductorFeedback;

    [Header("Status VFX Prefabs (legacy, see summary)")]
    public GameObject fireStatusVfx;
    public GameObject iceStatusVfx;
    public GameObject frozenStatusVfx;
    public GameObject discordRingVfx;

    [Header("Duo Synergy Explode VFX Prefabs")]
    public GameObject thermalShockVfx;
    public GameObject plasmaOverloadVfx;
    public GameObject superconductorVfx;

    [Header("Audio (legacy, see summary)")]
    public AudioClip statusAppliedSfx;
    public AudioClip frozenSfx;
    public AudioClip thermalShockSfx;
    public AudioClip plasmaOverloadSfx;
    public AudioClip superconductorSfx;
    public AudioClip discordAppliedSfx;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (iceShatterFeedback == null) Debug.LogError("ElementalEffectsManager: iceShatterFeedback is not assigned.", this);
        if (thermalShockFeedback == null) Debug.LogError("ElementalEffectsManager: thermalShockFeedback is not assigned.", this);
        if (superconductorFeedback == null) Debug.LogError("ElementalEffectsManager: superconductorFeedback is not assigned.", this);
    }

    /// <summary>Plays one of this manager's burst feedbacks at a world position. Start logs any unassigned ones.</summary>
    public void PlayAt(MMF_Player feedback, Vector3 position)
    {
        if (feedback != null)
        {
            feedback.PlayFeedbacks(position);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
