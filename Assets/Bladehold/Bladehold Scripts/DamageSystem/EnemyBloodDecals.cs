using UnityEngine;

/// <summary>
///     Spawns blood decals on the ground under an enemy whenever its <see cref="Health" /> takes damage.
///     Listens to <see cref="Health.OnDamaged" /> following the inbound dependency convention.
///     Uses <see cref="BloodDecalManager" /> and decal tunables from <see cref="RagdollConfigSO" />.
/// </summary>
public class EnemyBloodDecals : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private RagdollConfigSO config;

    [Header("Decal Spawning Tunables")]
    [Tooltip("Layer mask for raycasting down to the floor/ground.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Minimum damage required to trigger a blood decal.")]
    [SerializeField] private float minDamageForDecal = 0.1f;

    [Tooltip("Minimum blood decal diameter in meters.")]
    [SerializeField] private float minDecalSize = 0.4f;

    [Tooltip("Maximum blood decal diameter in meters.")]
    [SerializeField] private float maxDecalSize = 1.3f;

    [Tooltip("Damage amount that yields the maximum decal size.")]
    [SerializeField] private float damageForMaxSize = 25f;

    [Tooltip("Multiplier applied to decal size on critical hits.")]
    [SerializeField] private float critSizeMultiplier = 1.25f;

    [Tooltip("Minimum cooldown in seconds between damage blood decals on this enemy to avoid flooding from high-tick AoE/burns.")]
    [SerializeField] private float spawnCooldown = 0.1f;

    [Tooltip("Maximum downward raycast distance to locate the ground beneath the enemy.")]
    [SerializeField] private float maxRaycastDistance = 3.0f;

    [Tooltip("Height above the enemy transform origin from which to cast downward.")]
    [SerializeField] private float raycastHeightOffset = 0.5f;

    [Tooltip("Maximum horizontal radial jitter for decal placement around the enemy's feet.")]
    [SerializeField] private float groundJitterRadius = 0.25f;

    private float nextSpawnTime;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (config == null)
        {
            EnemyRagdoll ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll != null)
            {
                config = ragdoll.Config;
            }
        }
    }

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (config == null)
        {
            EnemyRagdoll ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll != null)
            {
                config = ragdoll.Config;
            }
        }
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (config == null)
        {
            EnemyRagdoll ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll != null)
            {
                config = ragdoll.Config;
            }
        }

        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDamaged += HandleDamaged;
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError($"[EnemyBloodDecals] Health component is missing on '{gameObject.name}'.", this);
            anyError = true;
        }

        if (config == null)
        {
            EnemyRagdoll ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll != null && ragdoll.Config != null)
            {
                config = ragdoll.Config;
            }
        }

        if (anyError)
        {
            return;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || damage == null || damage.value < minDamageForDecal)
        {
            return;
        }

        if (Application.isPlaying)
        {
            if (Time.time < nextSpawnTime)
            {
                return;
            }
            nextSpawnTime = Time.time + spawnCooldown;
        }

        // Resolve active config
        RagdollConfigSO activeConfig = config;
        if (activeConfig == null)
        {
            EnemyRagdoll ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll != null)
            {
                activeConfig = ragdoll.Config;
            }
        }

        if (activeConfig == null || activeConfig.bloodDecalMaterials == null || activeConfig.bloodDecalMaterials.Length == 0)
        {
            return;
        }

        // Determine ground position beneath enemy with slight radial jitter
        Vector2 randomCircle = Random.insideUnitCircle * groundJitterRadius;
        Vector3 rayOrigin = transform.position + new Vector3(randomCircle.x, raycastHeightOffset, randomCircle.y);
        Vector3 spawnPoint;
        Vector3 spawnNormal;

        // Exclude trigger colliders and ragdoll bone colliders
        int layerMask = groundLayers.value & ~(1 << LayerMask.NameToLayer("Ragdoll"));
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxRaycastDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
            spawnPoint = hit.point;
            spawnNormal = hit.normal;
        }
        else
        {
            spawnPoint = transform.position;
            spawnNormal = Vector3.up;
        }

        // Calculate decal size scaled with incoming damage
        float damageFraction = damageForMaxSize > 0f ? Mathf.Clamp01(damage.value / damageForMaxSize) : 0.5f;
        float size = Mathf.Lerp(minDecalSize, maxDecalSize, damageFraction);
        if (damage.isCritical)
        {
            size *= critSizeMultiplier;
        }

        BloodDecalManager.SpawnDecal(spawnPoint, spawnNormal, size, activeConfig);
    }
}
