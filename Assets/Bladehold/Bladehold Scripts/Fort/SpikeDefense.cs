using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Fort defense for ground spike traps.
///     Periodically thrusts lethal spikes upward from the ground, dealing AoE physical damage
///     to all enemies within its box volume, then smoothly retracts until the next thrust.
/// </summary>
public class SpikeDefense : FortDefense
{
    [Header("Configuration")]
    [SerializeField] private SpikeDefenseConfigSO config;

    [Header("Fallback Specs (if config unassigned)")]
    [SerializeField] private float baseDamage = 35f;
    [SerializeField] private float damagePerLevel = 25f;
    [SerializeField] private float thrustInterval = 2.5f;
    [SerializeField] private float activeThrustDuration = 0.5f;
    [SerializeField] private Vector3 boxSize = new Vector3(3f, 2f, 3f);
    [SerializeField] private Vector3 boxCenterOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float upwardKnockback = 4f;

    [Header("Spike Mesh Movement")]
    [Tooltip("Optional transform of the spike model that moves up and down during thrusts.")]
    [SerializeField] private Transform spikeMeshTransform;
    [SerializeField] private Vector3 retractedLocalOffset = new Vector3(0f, -0.4f, 0f);
    [SerializeField] private Vector3 extendedLocalOffset = new Vector3(0f, 0.3f, 0f);

    [Header("Feedbacks & Audio")]
    [SerializeField] private AudioClip thrustSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private GameObject bloodSplatterPrefab;
    [SerializeField] private GameObject thrustVfxPrefab;
    [SerializeField] private MMF_Player thrustFeedback;

    private readonly Collider[] overlapBuffer = new Collider[64];
    private readonly HashSet<Health> hitEnemiesThisThrust = new HashSet<Health>();
    private Vector3 initialMeshLocalPos;
    private Coroutine trapCycleRoutine;
    private bool isThrusting = false;

    private void Awake()
    {
        defenseType = FortDefenseType.Spikes;
        if (config == null)
        {
            config = Resources.Load<SpikeDefenseConfigSO>("SpikeDefenseConfig");
        }

        if (spikeMeshTransform != null)
        {
            initialMeshLocalPos = spikeMeshTransform.localPosition;
            spikeMeshTransform.localPosition = initialMeshLocalPos + retractedLocalOffset;
        }
    }

    private void OnEnable()
    {
        if (trapCycleRoutine == null)
        {
            trapCycleRoutine = StartCoroutine(TrapCycleRoutine());
        }
    }

    private void OnDisable()
    {
        if (trapCycleRoutine != null)
        {
            StopCoroutine(trapCycleRoutine);
            trapCycleRoutine = null;
        }
    }

    private float GetEffectiveDamage()
    {
        float baseDmg = config != null ? config.baseDamage : baseDamage;
        float perLvl = config != null ? config.damagePerLevel : damagePerLevel;
        return baseDmg + (currentLevel - 1) * perLvl;
    }

    private float GetThrustInterval()
    {
        return config != null ? config.thrustInterval : thrustInterval;
    }

    private float GetActiveDuration()
    {
        return config != null ? config.activeThrustDuration : activeThrustDuration;
    }

    private Vector3 GetBoxSize()
    {
        return config != null ? config.boxSize : boxSize;
    }

    private Vector3 GetBoxCenterOffset()
    {
        return config != null ? config.boxCenterOffset : boxCenterOffset;
    }

    private float GetUpwardKnockback()
    {
        return config != null ? config.upwardKnockback : upwardKnockback;
    }

    private IEnumerator TrapCycleRoutine()
    {
        // Initial random stagger so all traps don't pop at the exact same millisecond
        yield return new WaitForSeconds(Random.Range(0.2f, 0.8f));

        while (true)
        {
            yield return new WaitForSeconds(GetThrustInterval());

            yield return StartCoroutine(PerformThrust());
        }
    }

    private IEnumerator PerformThrust()
    {
        isThrusting = true;
        hitEnemiesThisThrust.Clear();

        // 1. Audio & Feedback
        if (thrustSound != null)
        {
            AudioSource.PlayClipAtPoint(thrustSound, transform.position, 1.0f);
        }
        if (thrustVfxPrefab != null)
        {
            Instantiate(thrustVfxPrefab, transform.TransformPoint(GetBoxCenterOffset()), Quaternion.identity);
        }
        if (thrustFeedback != null)
        {
            thrustFeedback.PlayFeedbacks(transform.position);
        }

        // 2. Animate spikes thrusting upwards rapidly (0.08s)
        float thrustSpeed = 0.08f;
        float elapsed = 0f;
        while (elapsed < thrustSpeed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / thrustSpeed);
            if (spikeMeshTransform != null)
            {
                spikeMeshTransform.localPosition = Vector3.Lerp(
                    initialMeshLocalPos + retractedLocalOffset,
                    initialMeshLocalPos + extendedLocalOffset,
                    t
                );
            }
            yield return null;
        }

        // 3. Deal Box AoE Damage to all enemies inside
        ApplyTrapDamage();

        // 4. Hold extended for active duration
        yield return new WaitForSeconds(GetActiveDuration());

        // 5. Retract spikes smoothly back into ground (0.3s)
        float retractSpeed = 0.3f;
        elapsed = 0f;
        while (elapsed < retractSpeed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / retractSpeed);
            if (spikeMeshTransform != null)
            {
                spikeMeshTransform.localPosition = Vector3.Lerp(
                    initialMeshLocalPos + extendedLocalOffset,
                    initialMeshLocalPos + retractedLocalOffset,
                    t
                );
            }
            yield return null;
        }

        if (spikeMeshTransform != null)
        {
            spikeMeshTransform.localPosition = initialMeshLocalPos + retractedLocalOffset;
        }

        isThrusting = false;
    }

    private void ApplyTrapDamage()
    {
        Vector3 boxCenter = transform.TransformPoint(GetBoxCenterOffset());
        Vector3 halfExtents = GetBoxSize() * 0.5f;

        int hitCount = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, overlapBuffer, transform.rotation);
        float damageValue = GetEffectiveDamage();
        float knockback = GetUpwardKnockback();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;

            // Skip player
            if (Player.Instance != null && col.transform.root == Player.Instance.transform.root) continue;

            Health enemyHealth = col.GetComponentInParent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;

            if (hitEnemiesThisThrust.Contains(enemyHealth)) continue;
            hitEnemiesThisThrust.Add(enemyHealth);

            Damage damage = new Damage
            {
                value = damageValue,
                type = DamageType.sharp,
                isCritical = false,
                sourcePosition = transform.position,
                direction = Vector3.up,
                knockbackForce = knockback,
                unparryable = true,
                isPlayerDamage = true,
                elementId = RunSession.ElementalSlots.GetValueOrDefault("SLOT_FORTRESS", "")
            };

            enemyHealth.ReceiveDamage(damage);

            if (bloodSplatterPrefab != null)
            {
                Instantiate(bloodSplatterPrefab, col.transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }

            if (hitSound != null)
            {
                AudioSource.PlayClipAtPoint(hitSound, col.transform.position, 0.8f);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isThrusting ? Color.red : new Color(1f, 0.5f, 0f, 0.75f);
        Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(GetBoxCenterOffset()), transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, GetBoxSize());
    }
}
