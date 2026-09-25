using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Reactive feedback for a melee weapon: subscribes to <see cref="DamageTrigger.OnHit" />/
///     <see cref="DamageTrigger.OnBlocked" /> the same way <see cref="DamageNumberSpawner" /> reacts to
///     <see cref="Health.OnDamaged" /> - DamageTrigger stays unaware of what plays when it hits or blocks.
///     Each event is an <see cref="MMF_Player" /> (sound + <see cref="MMF_PooledParticleBurst" /> blood)
///     played at the hit point, with intensity = damage / <see cref="damageForMaxIntensity" /> so the burst
///     grows with the hit. Only <see cref="damageTrigger" /> is required; missing feedbacks log in Start.
/// </summary>
public class SwordHitFeedback : MonoBehaviour
{
    [SerializeField] private DamageTrigger damageTrigger;

    [Header("Blocked reaction")]
    [Tooltip("The player rig's Animator. Set when a swing is blocked by the cut-through cap.")]
    [SerializeField] private Animator animator;

    [Header("Feedbacks (MMF)")]
    [Tooltip("Swing woosh, from an animation event earlier in the swing.")]
    [SerializeField] private MMF_Player wooshFeedback;
    [Tooltip("Normal hit: sound + blood burst at the hit point.")]
    [SerializeField] private MMF_Player hitFeedback;
    [Tooltip("Optional: played instead of Hit Feedback on a critical hit.")]
    [SerializeField] private MMF_Player critHitFeedback;
    [Tooltip("Optional: played instead of Hit Feedback when the target has no blood, e.g. a smashable Chest.")]
    [SerializeField] private MMF_Player inanimateHitFeedback;
    [Tooltip("Hit feedbacks play at full intensity from this much damage; the blood burst scales with intensity.")]
    [SerializeField] private float damageForMaxIntensity = 20f;

    private int blockedTriggerHash;
    private bool anyError = false;

    private void OnValidate()
    {
        if (damageTrigger == null)
        {
            damageTrigger = GetComponent<DamageTrigger>();
        }
    }

    private void Start()
    {
        if (damageTrigger == null)
        {
            Debug.LogError("DamageTrigger is not assigned or found on the GameObject.", this);
            anyError = true;
        }
        if (wooshFeedback == null)
        {
            Debug.LogError($"SwordHitFeedback on {name}: wooshFeedback is not assigned.", this);
        }
        if (hitFeedback == null)
        {
            Debug.LogError($"SwordHitFeedback on {name}: hitFeedback is not assigned.", this);
        }

        if (anyError)
        {
            return;
        }

        blockedTriggerHash = Animator.StringToHash("Blocked");

        damageTrigger.OnHit += HandleHit;
        damageTrigger.OnBlocked += HandleBlocked;
    }

    private void OnDestroy()
    {
        if (damageTrigger != null)
        {
            damageTrigger.OnHit -= HandleHit;
            damageTrigger.OnBlocked -= HandleBlocked;
        }
    }

    /// <summary>Called from an animation event earlier in the swing, before the hitbox activates.</summary>
    public void PlayWoosh()
    {
        if (wooshFeedback != null)
        {
            wooshFeedback.PlayFeedbacks(transform.position);
        }
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 point)
    {
        MMF_Player feedback = PickHitFeedback(target, damage.isCritical);
        if (feedback == null) return;

        float intensity = damageForMaxIntensity > 0f ? Mathf.Clamp01(damage.value / damageForMaxIntensity) : 1f;
        feedback.PlayFeedbacks(point, intensity);
    }

    private MMF_Player PickHitFeedback(IDamageable target, bool isCritical)
    {
        bool isInanimate = target is Component targetComponent && targetComponent.GetComponentInParent<Chest>() != null;
        if (isInanimate && inanimateHitFeedback != null)
        {
            return inanimateHitFeedback;
        }
        if (isCritical && critHitFeedback != null)
        {
            return critHitFeedback;
        }
        return hitFeedback;
    }

    private void HandleBlocked()
    {
        if (animator != null)
        {
            animator.SetTrigger(blockedTriggerHash);
        }
    }
}
