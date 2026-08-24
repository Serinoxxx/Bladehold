using System.Collections;
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
    [Header("Configuration")]
    [SerializeField] private SpikeDefenseConfigSO config;

    [Header("Fallback Specs (if config unassigned)")]
    [SerializeField] private float baseDamage = 16f;
    [SerializeField] private float damagePerLevel = 14f;
    [SerializeField] private float ragdollMultiplier = 5f;
    [SerializeField] private float hitCooldownPerEnemy = 0.5f;
    [SerializeField] private float embedDepth = 1.5f;
    [SerializeField] private float impaleDuration = 6.0f;

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
        if (config == null)
        {
            config = Resources.Load<SpikeDefenseConfigSO>("SpikeDefenseConfig");
        }
    }

    private float GetEffectiveDamage()
    {
        float baseDmg = config != null ? config.baseDamage : baseDamage;
        float perLvl = config != null ? config.damagePerLevel : damagePerLevel;
        return baseDmg + (currentLevel - 1) * perLvl;
    }

    private float GetRagdollMultiplier()
    {
        return config != null ? config.ragdollMultiplier : ragdollMultiplier;
    }

    private float GetHitCooldown()
    {
        return config != null ? config.hitCooldownPerEnemy : hitCooldownPerEnemy;
    }

    private float GetEmbedDepth()
    {
        return config != null ? config.embedDepth : embedDepth;
    }

    private float GetImpaleDuration()
    {
        return config != null ? config.impaleDuration : impaleDuration;
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
        nextHitTimes[health] = now + GetHitCooldown();

        float dmg = GetEffectiveDamage();
        if (isRagdolled)
        {
            dmg *= GetRagdollMultiplier(); // 5x damage for ragdolled enemies!
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
    ///     penetrating embedDepth meters into the barricade while letting limbs dangle naturally.
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

        // Calculate penetration/embedding vector into the spike structure
        float penetration = GetEmbedDepth();
        Vector3 toCenter = (transform.position - hitPoint);
        toCenter.y = 0f;
        Vector3 embedDir = toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : transform.forward;
        Vector3 embeddedPos = hitPoint + embedDir * penetration;

        // If ragdoll is present, penetrate and lock the struck bone
        if (ragdoll != null)
        {
            ragdoll.BuildIfNeeded();

            Animator anim = ragdoll.GetComponentInChildren<Animator>();
            if (anim != null && anim.enabled)
            {
                anim.enabled = false;
            }

            if (!ragdoll.IsRagdolled)
            {
                ragdoll.EnterRagdoll(Vector3.zero, Vector3.zero);
            }

            Rigidbody struckBone = ragdoll.GetBoneRigidbody(hitCollider, hitPoint);
            if (struckBone == null)
            {
                struckBone = ragdoll.Pelvis;
            }

            if (struckBone != null)
            {
                // Embed bone deeply into the spike structure (setting both transform and Rigidbody)
                Vector3 delta = embeddedPos - struckBone.position;
                struckBone.transform.position = embeddedPos;
                struckBone.position = embeddedPos;
                struckBone.linearVelocity = Vector3.zero;
                struckBone.angularVelocity = Vector3.zero;
                struckBone.isKinematic = true;

                // Also shift root/other bodies so the entire humanoid model penetrates into the barricade
                foreach (var rb in struckBone.transform.root.GetComponentsInChildren<Rigidbody>())
                {
                    if (rb != struckBone)
                    {
                        rb.transform.position += delta;
                        rb.position += delta;
                        rb.linearVelocity = Vector3.zero;
                    }
                }
            }

            bool allowDangle = config == null || config.allowLimbDangle;
            if (allowDangle && Application.isPlaying)
            {
                StartCoroutine(ImpaleHoldRoutine(ragdoll, struckBone, GetImpaleDuration()));
            }
            else
            {
                ragdoll.FreezeCorpse();
            }
        }
        else
        {
            // Non-ragdoll fallback: embed root transform
            health.transform.position = embeddedPos;
        }

        Debug.Log($"[SpikeDefense] Enemy '{health.name}' IMPALED and embedded {penetration:F1}m into spike barricade!");
    }

    private IEnumerator ImpaleHoldRoutine(EnemyRagdoll ragdoll, Rigidbody pinnedBone, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ragdoll == null) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ragdoll != null)
        {
            ragdoll.FreezeCorpse();
        }
    }
}
