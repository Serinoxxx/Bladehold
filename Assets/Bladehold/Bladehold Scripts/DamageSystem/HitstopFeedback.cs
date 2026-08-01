using UnityEngine;
using MoreMountains.Feedbacks;

namespace Bladehold
{
    /// <summary>
    /// Listens to a DamageTrigger and plays an MMF_Player to apply hitstop, screen shake, 
    /// or other "candy" when a melee strike lands.
    /// </summary>
    public class HitstopFeedback : MonoBehaviour
    {
        [SerializeField] private DamageTrigger damageTrigger;
        
        [Tooltip("The MMF_Player to play when a hit lands. Add your MMTimeManager feedbacks here.")]
        [SerializeField] private MMF_Player hitFeedback;

        [Tooltip("Cooldown between triggers so hitting 5 enemies at once doesn't restart the feedback 5 times.")]
        [SerializeField] private float cooldown = 0.15f;

        private float lastHitTime;

        private void OnValidate()
        {
            if (damageTrigger == null)
                damageTrigger = GetComponent<DamageTrigger>();
            
            if (hitFeedback == null)
                hitFeedback = GetComponent<MMF_Player>();
        }

        private void OnEnable()
        {
            if (damageTrigger != null)
            {
                damageTrigger.OnHit += HandleHit;
            }
        }

        private void OnDisable()
        {
            if (damageTrigger != null)
            {
                damageTrigger.OnHit -= HandleHit;
            }
        }

        private void HandleHit(IDamageable target, Damage damage, Vector3 hitPoint)
        {
            if (hitFeedback == null) return;
            
            // Prevent triggering multiple times in a single frame/cleave
            if (Time.unscaledTime - lastHitTime < cooldown) return;
            
            lastHitTime = Time.unscaledTime;
            
            // Note: If you add spatial feedbacks (particles, sound) later, you might want to 
            // position the feedback object at the hitPoint before playing.
            // hitFeedback.transform.position = hitPoint;
            
            hitFeedback.PlayFeedbacks(hitPoint);
        }
    }
}
