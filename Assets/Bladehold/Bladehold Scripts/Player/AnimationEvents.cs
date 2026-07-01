using UnityEngine;

public class AnimationEvents : MonoBehaviour
{
    [SerializeField] DamageTrigger oneHandedSwordDamageTrigger;
    [SerializeField] SwordHitFeedback swordHitFeedback;

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
