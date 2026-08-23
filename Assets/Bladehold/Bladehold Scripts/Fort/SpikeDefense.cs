using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Fort defense for ground spike barricades and caltrop fortifications.
///     Deals physical contact damage to walking enemies, 5x damage to ragdolled / airborne enemies,
///     and embeds / impales enemies into the spike structure if the hit is lethal.
/// </summary>
public class SpikeDefense : FortDefense
{
    [Header("Spike Damage")]
    [SerializeField] private float baseDamage = 16f;
    [SerializeField] private float ragdollMultiplier = 5f;
    [SerializeField] private float hitCooldownPerEnemy = 0.5f;

    [Header("Impale & Audio Feedback")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip impaleSound;
    [SerializeField] private GameObject bloodSplatterPrefab;
    [SerializeField] private MMF_Player contactFeedback;
    [SerializeField] private MMF_Player impaleFeedback;

    private readonly Dictionary<Health, float> nextHitTimes = new Dictionary<Health, float>();
    private readonly HashSet<Health> impaledTargets = new HashSet<Health>();

    private void Awake()
    {
        defenseType = FortDefenseType.Spikes;
    }

    private float GetEffectiveDamage()
    {
        // Level 1: 16, Level 2: 26, Level 3: 40, Level 4: 60
        return baseDamage + (currentLevel - 1) * 14f;
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessSpikeContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ProcessSpikeContact(other);
    }

    private void ProcessSpikeContact(Collider hitCollider)
    {
        if (hitCollider == null) return;

        Health health = hitCollider.GetComponentInParent<Health>();
        if (health == null) return;

        // Skip player and allies
        if (Player.Instance != null && health.transform.root == Player.Instance.transform.root) return;

        EnemyRagdoll ragdoll = hitCollider.GetComponentInParent<EnemyRagdoll>();
        KnockbackReceiver kb = hitCollider.GetComponentInParent<KnockbackReceiver>();

        bool isRagdolled = (ragdoll != null && ragdoll.IsRagdolled) ||
                           (kb != null && (kb.State == KnockbackReceiver.KnockbackState.Airborne || kb.State == KnockbackReceiver.KnockbackState.KnockedDown));

        // If already dead and ragdolled into spikes, check if we should impale the flying corpse
        if (health.IsDead)
        {
            if (isRagdolled && !impaledTargets.Contains(health))
            {
                ImpaleTarget(health, ragdoll, kb, hitCollider, hitCollider.ClosestPoint(transform.position));
            }
            return;
        }

        // Check per-enemy tick cooldown
        float now = Time.time;
        if (nextHitTimes.TryGetValue(health, out float nextAllowedTime) && now < nextAllowedTime)
        {
            return;
        }
        nextHitTimes[health] = now + hitCooldownPerEnemy;

        float dmg = GetEffectiveDamage();
        if (isRagdolled)
        {
            dmg *= ragdollMultiplier; // 5x damage for ragdolled enemies!
        }

        bool isLethal = (health.CurrentHealth - dmg) <= 0f;

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
        Vector3 hitDirection = (hitPoint - transform.position).normalized;

        Damage damage = new Damage
        {
            value = dmg,
            type = DamageType.sharp,
            isCritical = isRagdolled,
            sourcePosition = transform.position,
            direction = hitDirection,
            hitCollider = hitCollider,
            isPlayerDamage = true
        };

        health.ReceiveDamage(damage);

        if (isLethal || health.IsDead)
        {
            // Enemy killed by spikes -> Impale and embed into the spike structure!
            ImpaleTarget(health, ragdoll, kb, hitCollider, hitPoint);
        }
        else
        {
            if (contactFeedback != null)
            {
                contactFeedback.PlayFeedbacks(hitPoint);
            }
            else if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, hitPoint, 0.7f);
            }
        }
    }

    /// <summary>
    ///     Sticks and embeds a killed enemy's corpse directly into the spikes,
    ///     freezing velocity and locking the struck bone in place.
    /// </summary>
    private void ImpaleTarget(Health health, EnemyRagdoll ragdoll, KnockbackReceiver kb, Collider hitCollider, Vector3 hitPoint)
    {
        if (health == null || impaledTargets.Contains(health)) return;
        impaledTargets.Add(health);

        if (impaleFeedback != null)
        {
            impaleFeedback.PlayFeedbacks(hitPoint);
        }
        else if (impaleSound != null)
        {
            AudioSource.PlayClipAtPoint(impaleSound, hitPoint, 1.0f);
        }
        else if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, hitPoint, 1.0f);
        }

        // Spawn blood splatter at impale point
        if (bloodSplatterPrefab != null)
        {
            Instantiate(bloodSplatterPrefab, hitPoint, Quaternion.identity);
        }

        // If ragdoll is present, lock the struck bone to the spike
        if (ragdoll != null)
        {
            ragdoll.BuildIfNeeded();
            Rigidbody struckBone = ragdoll.GetBoneRigidbody(hitCollider, hitPoint);
            if (struckBone != null)
            {
                // Snap bone position to hit point and make kinematic
                struckBone.position = hitPoint;
                struckBone.linearVelocity = Vector3.zero;
                struckBone.angularVelocity = Vector3.zero;
                struckBone.isKinematic = true;
            }

            ragdoll.FreezeCorpse();
        }

        Debug.Log($"[SpikeDefense] Enemy '{health.name}' IMPALED and embedded into spike barricade!");
    }
}
