using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Fort defense for gate cauldrons of boiling pitch.
///     Detects enemies passing beneath the gate, tips the cauldron to pour boiling oil,
///     and spawns a burning, slowing ground zone on the path below.
/// </summary>
public class BurningOilDefense : FortDefense
{
    [Header("Detection & Zone")]
    [SerializeField] private Transform groundSpawnPoint;
    [SerializeField] private float detectionRadius = 4f;
    [SerializeField] private LayerMask enemyLayers = ~0;
    [SerializeField] private GameObject oilZonePrefab;

    [Header("Cauldron Animation")]
    [SerializeField] private Transform bucketTransform;
    [SerializeField] private float tiltAngle = 60f;
    [SerializeField] private float tiltDuration = 0.4f;

    [Header("Base Tunables")]
    [SerializeField] private float baseCooldown = 14f;
    [SerializeField] private float basePuddleDuration = 6f;
    [SerializeField] private float baseDamagePerSecond = 20f;
    [SerializeField] private float slowFraction = 0.5f;

    [Header("Audio & Feedbacks")]
    [SerializeField] private AudioClip pourSound;
    [SerializeField] private AudioClip splashSound;
    [SerializeField] private MMF_Player pourFeedback;
    [SerializeField] private MMF_Player splashFeedback;

    private float cooldownTimer;
    private bool isPouring = false;
    private Quaternion originalBucketRotation;
    private readonly Collider[] detectionBuffer = new Collider[64];

    private void Awake()
    {
        defenseType = FortDefenseType.BurningOil;
        if (enemyLayers == ~0 || enemyLayers == 0)
        {
            int mask = LayerMask.GetMask("Enemy");
            enemyLayers = mask != 0 ? mask : (1 << 7);
        }

        if (bucketTransform != null)
        {
            originalBucketRotation = bucketTransform.localRotation;
        }
        else
        {
            // Try auto-resolving child bucket
            Transform b = transform.Find("SM_Wep_Hot_Oil_Bucket_02");
            if (b != null)
            {
                bucketTransform = b;
                originalBucketRotation = bucketTransform.localRotation;
            }
        }
    }

    private void Update()
    {
        if (isPouring) return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f)
        {
            if (HasEnemiesBelow())
            {
                StartCoroutine(PourOilRoutine());
            }
        }
    }

    private float GetEffectiveCooldown()
    {
        // Level 1: 14s, Level 2: 11s, Level 3: 8s, Level 4: 6s
        return Mathf.Max(5f, baseCooldown - (currentLevel - 1) * 3f);
    }

    private float GetEffectiveDuration()
    {
        // Level 1: 6s, Level 2: 8s, Level 3: 10s, Level 4: 12s
        return basePuddleDuration + (currentLevel - 1) * 2f;
    }

    private float GetEffectiveDPS()
    {
        // Level 1: 20, Level 2: 35, Level 3: 55, Level 4: 80
        return baseDamagePerSecond + (currentLevel - 1) * 18f;
    }

    private Vector3 GetGroundPosition()
    {
        if (groundSpawnPoint != null)
        {
            return groundSpawnPoint.position;
        }

        // Raycast down to find ground
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 20f, LayerMask.GetMask("Default", "Terrain", "Ground", "Environment")))
        {
            return hit.point;
        }

        return transform.position + Vector3.down * 4f;
    }

    private bool HasEnemiesBelow()
    {
        Vector3 checkPos = GetGroundPosition();
        int count = Physics.OverlapSphereNonAlloc(checkPos, detectionRadius, detectionBuffer, enemyLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = detectionBuffer[i];
            if (col == null) continue;

            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead)
            {
                if (Player.Instance == null || h.transform.root != Player.Instance.transform.root)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator PourOilRoutine()
    {
        isPouring = true;

        if (pourFeedback != null)
        {
            pourFeedback.PlayFeedbacks(transform.position);
        }
        else if (pourSound != null)
        {
            AudioSource.PlayClipAtPoint(pourSound, transform.position, 0.8f);
        }

        // Animate bucket tilt forward
        if (bucketTransform != null)
        {
            Quaternion targetRot = originalBucketRotation * Quaternion.Euler(tiltAngle, 0f, 0f);
            float elapsed = 0f;
            while (elapsed < tiltDuration)
            {
                elapsed += Time.deltaTime;
                bucketTransform.localRotation = Quaternion.Slerp(originalBucketRotation, targetRot, elapsed / tiltDuration);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(tiltDuration);
        }

        // Spawn burning ground zone
        Vector3 groundPos = GetGroundPosition();
        if (splashFeedback != null)
        {
            splashFeedback.PlayFeedbacks(groundPos);
        }
        else if (splashSound != null)
        {
            AudioSource.PlayClipAtPoint(splashSound, groundPos, 0.9f);
        }

        if (oilZonePrefab != null)
        {
            GameObject zoneObj = Instantiate(oilZonePrefab, groundPos, Quaternion.identity);
            BurningOilZone zone = zoneObj.GetComponent<BurningOilZone>();
            if (zone != null)
            {
                zone.Init(detectionRadius, GetEffectiveDuration(), GetEffectiveDPS(), slowFraction);
            }
        }
        else
        {
            // Create runtime fallback zone
            GameObject fallbackZone = new GameObject("BurningOilZone_Runtime");
            fallbackZone.transform.position = groundPos;
            BurningOilZone zone = fallbackZone.AddComponent<BurningOilZone>();
            zone.Init(detectionRadius, GetEffectiveDuration(), GetEffectiveDPS(), slowFraction);
        }

        // Hold tipped pose briefly
        yield return new WaitForSeconds(0.5f);

        // Reset bucket back to upright
        if (bucketTransform != null)
        {
            Quaternion targetRot = originalBucketRotation * Quaternion.Euler(tiltAngle, 0f, 0f);
            float elapsed = 0f;
            float returnDuration = 0.8f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                bucketTransform.localRotation = Quaternion.Slerp(targetRot, originalBucketRotation, elapsed / returnDuration);
                yield return null;
            }
            bucketTransform.localRotation = originalBucketRotation;
        }

        cooldownTimer = GetEffectiveCooldown();
        isPouring = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 groundPos = GetGroundPosition();
        Gizmos.DrawWireSphere(groundPos, detectionRadius);
        Gizmos.DrawLine(transform.position, groundPos);
    }
#endif
}
