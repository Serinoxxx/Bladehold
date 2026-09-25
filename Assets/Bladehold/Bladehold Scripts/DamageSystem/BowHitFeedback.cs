using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Reactive impact feedback for the bow — the <see cref="SwordHitFeedback" /> sibling: subscribes
///     to <see cref="PlayerBow.OnArrowImpact" /> and plays an <see cref="MMF_Player" /> (hit sound +
///     <see cref="MMF_PooledParticleBurst" /> blood) at the hit point, with distinct players for critical
///     hits and <see cref="VulnerableSpot" /> (headshot) hits. Vulnerable outranks crit when both apply;
///     each falls back to the normal player when unassigned. Blood sprays back along the arrow's flight
///     path: the chosen player is turned to face back up the arrow before it plays (its burst uses the
///     owner's rotation), and intensity = damage / <see cref="damageForMaxIntensity" />.
/// </summary>
public class BowHitFeedback : MonoBehaviour
{
    [Tooltip("The PlayerBow whose impacts this reacts to. Auto-wired from this object or its parents.")]
    [SerializeField] private PlayerBow bow;

    [Header("Feedbacks (MMF)")]
    [Tooltip("Normal arrow hit: sound + blood burst. Keep it on its own GameObject: it gets rotated to aim the spray.")]
    [SerializeField] private MMF_Player hitFeedback;
    [Tooltip("Optional: played instead of Hit Feedback on a critical hit.")]
    [SerializeField] private MMF_Player critHitFeedback;
    [Tooltip("Optional: played on a VulnerableSpot (headshot) hit; outranks the crit player when both apply.")]
    [SerializeField] private MMF_Player vulnerableHitFeedback;
    [Tooltip("Hit feedbacks play at full intensity from this much damage; the blood burst scales with intensity.")]
    [SerializeField] private float damageForMaxIntensity = 20f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (bow == null)
        {
            bow = GetComponentInParent<PlayerBow>();
        }
    }

    private void Start()
    {
        if (bow == null)
        {
            Debug.LogError("PlayerBow is not assigned or found in parents; arrow impacts will play no feedback.", this);
            anyError = true;
        }
        if (hitFeedback == null)
        {
            Debug.LogError("BowHitFeedback: hitFeedback is not assigned.", this);
        }

        if (anyError)
        {
            return;
        }

        bow.OnArrowImpact += HandleImpact;
    }

    private void OnDestroy()
    {
        if (bow != null)
        {
            bow.OnArrowImpact -= HandleImpact;
        }
    }

    private void HandleImpact(ArrowImpact impact)
    {
        MMF_Player feedback = PickFeedback(impact);
        if (feedback == null) return;

        if (impact.direction.sqrMagnitude > 0.0001f)
        {
            feedback.transform.rotation = Quaternion.LookRotation(-impact.direction);
        }
        float intensity = damageForMaxIntensity > 0f ? Mathf.Clamp01(impact.damage.value / damageForMaxIntensity) : 1f;
        feedback.PlayFeedbacks(impact.point, intensity);
    }

    private MMF_Player PickFeedback(ArrowImpact impact)
    {
        if (impact.hitVulnerableSpot && vulnerableHitFeedback != null)
        {
            return vulnerableHitFeedback;
        }
        if (impact.damage.isCritical && critHitFeedback != null)
        {
            return critHitFeedback;
        }
        return hitFeedback;
    }
}
