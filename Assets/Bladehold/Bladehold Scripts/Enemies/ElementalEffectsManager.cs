using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
///     Shared elemental effects on the Player prefab. The burst feedbacks are MMF players (sound + VFX)
///     played at a world position via <see cref="PlayAt" />. The status VFX prefabs are state visuals,
///     parented to an enemy (or a tower arrow, fireball or ice zone) for as long as the effect lasts.
/// </summary>
public class ElementalEffectsManager : MonoBehaviour
{
    public static ElementalEffectsManager Instance { get; private set; }

    [Header("Burst Feedbacks (MMF, played at the burst position)")]
    [Tooltip("Ice Shards burst when a ranged kill shatters an enemy (bow and throwing axe).")]
    public MMF_Player iceShatterFeedback;
    [Tooltip("Thermal Shock explosion (Inferno ultimate burst).")]
    public MMF_Player thermalShockFeedback;
    [Tooltip("Superconductor lightning zap (Storm Eye ultimate strikes, Static Edge, the Superconductor duo).")]
    public MMF_Player superconductorFeedback;
    [Tooltip("Plasma Overload duo explosion (lightning on a burning enemy).")]
    public MMF_Player plasmaOverloadFeedback;

    [Header("Status Applied Feedbacks (MMF, played at the enemy)")]
    [Tooltip("Fire or Ice status lands on an enemy.")]
    public MMF_Player statusAppliedFeedback;
    [Tooltip("Deep Freeze: an iced enemy freezes solid.")]
    public MMF_Player frozenFeedback;
    [Tooltip("Discord: an enemy carries two or more elements.")]
    public MMF_Player discordAppliedFeedback;

    [Header("Status VFX Prefabs (parented to the enemy while the status lasts)")]
    public GameObject fireStatusVfx;
    public GameObject iceStatusVfx;
    public GameObject frozenStatusVfx;
    public GameObject discordRingVfx;
    [Tooltip("Lightning crackle riding on lightning-upgraded tower arrows.")]
    [FormerlySerializedAs("superconductorVfx")]
    public GameObject lightningTrailVfx;

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
        if (plasmaOverloadFeedback == null) Debug.LogError("ElementalEffectsManager: plasmaOverloadFeedback is not assigned.", this);
        if (statusAppliedFeedback == null) Debug.LogError("ElementalEffectsManager: statusAppliedFeedback is not assigned.", this);
        if (frozenFeedback == null) Debug.LogError("ElementalEffectsManager: frozenFeedback is not assigned.", this);
        if (discordAppliedFeedback == null) Debug.LogError("ElementalEffectsManager: discordAppliedFeedback is not assigned.", this);
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
