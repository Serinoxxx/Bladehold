using MoreMountains.Feedbacks;
using UnityEngine;

public class AnimationEvents : MonoBehaviour
{
    [SerializeField] DamageTrigger oneHandedSwordDamageTrigger;
    [SerializeField] SwordHitFeedback swordHitFeedback;
    [Tooltip("Played on every footstep animation event (assign a Sound feedback with several clips/random pitch — left/right foot both call the same method).")]
    [SerializeField] MMF_Player footstepFeedback;

    /// <summary>
    ///     Points the attack events at the active class's weapon. Called by
    ///     <see cref="PlayerClassController" /> in Awake, before any clip event can fire. The event
    ///     method names below stay as-is — they're baked into the attack clips, so every class's clip
    ///     calls the same methods and this component routes them to the active weapon.
    /// </summary>
    public void SetMeleeTrigger(DamageTrigger trigger)
    {
        oneHandedSwordDamageTrigger = trigger;
    }

    /// <summary>See <see cref="SetMeleeTrigger" />.</summary>
    public void SetHitFeedback(SwordHitFeedback feedback)
    {
        swordHitFeedback = feedback;
    }

    public void OneHandedSwordAttack()
    {
        oneHandedSwordDamageTrigger.Activate();
    }

    /// <summary>Called from an animation event earlier in the swing, before the hitbox activates.</summary>
    public void PlaySwordWoosh()
    {
        swordHitFeedback.PlayWoosh();
    }

    /// <summary>Called from a footstep animation event on the locomotion clips (both feet call this).</summary>
    public void Footstep()
    {
        if (footstepFeedback != null)
        {
            footstepFeedback.PlayFeedbacks();
        }
    }
}
