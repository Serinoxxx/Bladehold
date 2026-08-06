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

    [SerializeField] private Animator animator;
    private Transform leftFoot;
    private Transform rightFoot;
    private float lastFootstepTime = -1f;
    private static readonly int moveSpeedHash = Animator.StringToHash("MoveSpeed");

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animator != null && animator.isHuman)
        {
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
    }

    /// <summary>Called from a footstep animation event on the locomotion clips (both feet call this).</summary>
    public void Footstep()
    {
        if (footstepFeedback == null)
        {
            return;
        }

        if (Player.Instance != null && Player.Instance.Health != null && Player.Instance.Health.IsDead)
        {
            return;
        }

        if (animator != null && animator.GetFloat(moveSpeedHash) < 0.1f)
        {
            return;
        }

        if (Time.time - lastFootstepTime < 0.1f)
        {
            return;
        }

        lastFootstepTime = Time.time;

        Vector3 stepPos = transform.position;
        if (leftFoot != null && rightFoot != null)
        {
            stepPos = (leftFoot.position.y < rightFoot.position.y) ? leftFoot.position : rightFoot.position;
        }
        footstepFeedback.PlayFeedbacks(stepPos);
    }
}
