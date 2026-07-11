using UnityEngine;

public class AnimationEvents : MonoBehaviour
{
    [SerializeField] DamageTrigger oneHandedSwordDamageTrigger;
    [SerializeField] SwordHitFeedback swordHitFeedback;

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
}
