using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Reactive feedback for impulse-stamped hits: subscribes to <see cref="DamageTrigger.OnHit" /> the
///     same way <see cref="SwordHitFeedback" /> does and plays <see cref="hitFeedback" /> (a light pulse,
///     optionally a burst) at the hit point, with intensity scaled by <see cref="Damage.knockbackForce" />.
///     Fires on every impulse-stamped hit; knockdown-vs-fling is decided enemy-side by
///     <see cref="ImpulseReceiver" />. <see cref="damageTrigger" /> must be assigned explicitly — the
///     VampiricBlade precedent, since the player has other DamageTriggers.
/// </summary>
public class ImpulseHitFeedback : MonoBehaviour
{
    [SerializeField] private DamageTrigger damageTrigger;

    [Tooltip("Light pulse (MMF Light on a child light) and any burst. The player's GameObject is moved to the hit point before playing, so keep the light as its child.")]
    [SerializeField] private MMF_Player hitFeedback;

    [Tooltip("Damage.knockbackForce that maps to a full-intensity pulse; higher forces clamp to it.")]
    [SerializeField] private float forceForMaxPulse = 25f;

    private bool anyError = false;

    /// <summary>
    ///     Re-points at the equipped melee weapon's DamageTrigger. Called by
    ///     <see cref="PlayerWeaponManager" /> in Awake, before Start subscribes.
    /// </summary>
    public void SetDamageTrigger(DamageTrigger trigger)
    {
        damageTrigger = trigger;
    }

    private void Start()
    {
        if (damageTrigger == null)
        {
            Debug.LogError("ImpulseHitFeedback: DamageTrigger is not assigned.", this);
            anyError = true;
        }
        if (hitFeedback == null)
        {
            Debug.LogError("ImpulseHitFeedback: hitFeedback is not assigned.", this);
        }

        if (anyError)
        {
            return;
        }

        damageTrigger.OnHit += HandleHit;
    }

    private void OnDestroy()
    {
        if (damageTrigger != null)
        {
            damageTrigger.OnHit -= HandleHit;
        }
    }

    private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        if (damage.knockbackForce <= 0f || hitFeedback == null)
        {
            return;
        }

        hitFeedback.transform.position = hitPoint;
        hitFeedback.PlayFeedbacks(hitPoint, Mathf.Clamp01(damage.knockbackForce / forceForMaxPulse));
    }
}
