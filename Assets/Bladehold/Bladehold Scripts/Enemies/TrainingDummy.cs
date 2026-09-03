using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Training Dummy Goblin placed in the Rest Area and Meta Area.
///     Features:
///     - 1000 HP pool that absorbs player attacks and combos without attacking back.
///     - Resets position and restores full 1000 HP if not attacked for 10 seconds.
///     - Spawns a poof particle VFX and sound effect at both origin and destination upon resetting.
///     - Prevents actual death so it can be endlessly attacked and tested.
/// </summary>
[RequireComponent(typeof(Health))]
public class TrainingDummy : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private float maxHealth = 1000f;
    [SerializeField] private float resetIdleDelay = 10f;
    [SerializeField] private GameObject poofVfxPrefab;
    [SerializeField] private AudioClip poofSfx;

    [Header("UI")]
    [SerializeField] private TextMeshPro healthText;

    private Health health;
    private NavMeshAgent agent;
    private KnockbackReceiver knockbackReceiver;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float timeSinceLastAttacked;
    private bool hasTakenDamage;

    public float CurrentHealth => health != null ? health.CurrentHealth : maxHealth;
    public float MaxHealth => maxHealth;
    public float TimeSinceLastAttacked => timeSinceLastAttacked;

    public void Initialize()
    {
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        if (spawnPosition == Vector3.zero)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        // Ensure higher knockback resistance so hits slide/push without endless ragdoll flings
        if (knockbackReceiver != null)
        {
            knockbackReceiver.SetResistance(25f);
        }

        // Disable offensive and roaming components
        AIAttack attack = GetComponent<AIAttack>();
        if (attack != null) attack.enabled = false;

        AIMovement movement = GetComponent<AIMovement>();
        if (movement != null) movement.enabled = false;

        AITargetSelector targetSelector = GetComponent<AITargetSelector>();
        if (targetSelector != null) targetSelector.enabled = false;

        if (health != null)
        {
            health.SetMaxHealth(maxHealth);
            health.Revive(maxHealth);
            health.OnDamaged -= HandleDamaged;
            health.OnDamaged += HandleDamaged;
            health.TryPreventDeath -= HandleTryPreventDeath;
            health.TryPreventDeath += HandleTryPreventDeath;
        }

        UpdateHealthLabel();
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.TryPreventDeath -= HandleTryPreventDeath;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        timeSinceLastAttacked = 0f;
        hasTakenDamage = true;
        UpdateHealthLabel();
    }

    private bool HandleTryPreventDeath()
    {
        // Intercept lethal hit, poof back to spawn and restore full HP immediately
        ResetToSpawn();
        return true; // Cancels lethal death
    }

    private void Update()
    {
        bool moved = Vector3.Distance(transform.position, spawnPosition) > 0.2f;
        bool damaged = hasTakenDamage || (health != null && health.CurrentHealth < maxHealth);

        if (moved || damaged)
        {
            timeSinceLastAttacked += Time.deltaTime;

            if (timeSinceLastAttacked >= resetIdleDelay)
            {
                ResetToSpawn();
            }
        }
        else
        {
            timeSinceLastAttacked = 0f;
        }

        UpdateHealthLabel();
    }

    private void LateUpdate()
    {
        // Face health label towards active camera
        if (healthText != null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                healthText.transform.rotation = Quaternion.LookRotation(healthText.transform.position - cam.transform.position);
            }
        }
    }

    /// <summary>
    ///     Resets dummy position to initial spawn point with poof effects at origin and destination,
    ///     and restores 1000 HP.
    /// </summary>
    public void ResetToSpawn()
    {
        if (spawnPosition == Vector3.zero && transform.position != Vector3.zero)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        Vector3 originPos = transform.position;

        // 1. Poof at origin (where the dummy was standing)
        SpawnPoofEffect(originPos);

        // 2. Warp/reposition to spawn point
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(spawnPosition);
        }
        else
        {
            transform.position = spawnPosition;
        }

        transform.rotation = spawnRotation;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 3. Poof at destination (initial spawn position)
        SpawnPoofEffect(spawnPosition);

        // 4. Restore 1000 HP
        if (health != null)
        {
            health.Revive(maxHealth);
            health.Heal(maxHealth);
        }

        timeSinceLastAttacked = 0f;
        hasTakenDamage = false;
        UpdateHealthLabel();

        Debug.Log("[TrainingDummy] Dummy reset to spawn point with poof effects and full 1000 HP restored.");
    }

    private void SpawnPoofEffect(Vector3 position)
    {
        if (poofVfxPrefab != null)
        {
            GameObject fx = Instantiate(poofVfxPrefab, position + Vector3.up * 0.7f, Quaternion.identity);
            Destroy(fx, 3.0f);
        }

        if (poofSfx != null)
        {
            AudioSource.PlayClipAtPoint(poofSfx, position + Vector3.up * 0.7f, 1.0f);
        }
    }

    private void UpdateHealthLabel()
    {
        if (healthText != null && health != null)
        {
            healthText.text = $"<b>Training Dummy</b>\n<color=#FF5555>{Mathf.CeilToInt(health.CurrentHealth)}</color> / {Mathf.CeilToInt(maxHealth)} HP";
        }
    }
}
