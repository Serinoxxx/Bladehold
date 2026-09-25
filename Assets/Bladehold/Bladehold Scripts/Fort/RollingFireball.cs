using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Rolling Fireball spawned by Pyroclast Catapults.
///     Rolls along the ground leaving a blazing trail of fire, damaging and igniting enemies in its path.
///     Implements IDamageable: The player can strike the fireball with melee weapons to redirect its path
///     and boost its rolling speed.
/// </summary>
public class RollingFireball : MonoBehaviour, IDamageable
{
    [Header("Rolling Config")]
    [SerializeField] private float currentSpeed = 9f;
    [SerializeField] private float maxSpeed = 24f;
    [SerializeField] private float speedBoostMultiplier = 1.35f;
    [SerializeField] private float rollDamage = 50f;
    [SerializeField] private float radius = 0.8f;
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float trailDropInterval = 0.22f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Visual & Audio")]
    [Tooltip("The rolling mesh child; spun by code as the ball rolls. The prefab root also carries the trigger SphereCollider melee hits register on.")]
    [SerializeField] private Transform visualChild;
    [SerializeField] private GameObject trailVfxPrefab;
    [SerializeField] private GameObject hitBurstVfxPrefab;
    [SerializeField] private AudioClip strikeRedirectSfx;
    [SerializeField] private AudioClip rollLoopSfx;

    private Vector3 moveDirection = Vector3.forward;
    private float aliveTime = 0f;
    private float nextTrailTime = 0f;
    private readonly Dictionary<Health, float> hitCooldowns = new Dictionary<Health, float>();
    private readonly Collider[] hitBuffer = new Collider[16];
    private AudioSource rollAudioSource;

    public float CurrentSpeed => currentSpeed;
    public Vector3 MoveDirection => moveDirection;
    public Health Health => null; // IDamageable interface

    /// <summary>Instantiates the authored fireball prefab (CatapultProjectile.rollingFireballPrefab) and sets it rolling.</summary>
    public static RollingFireball Spawn(RollingFireball prefab, Vector3 position, Vector3 direction, float speed = 9f, float damage = 50f, float life = 10f)
    {
        if (prefab == null)
        {
            Debug.LogError("[RollingFireball] No fireball prefab: assign CatapultProjectile.rollingFireballPrefab.");
            return null;
        }

        RollingFireball fb = Instantiate(prefab, position, Quaternion.identity);
        fb.Init(direction, speed, damage, life);
        return fb;
    }

    public void Init(Vector3 direction, float speed = 9f, float damage = 50f, float life = 10f)
    {
        direction.y = 0f;
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        currentSpeed = speed;
        rollDamage = damage;
        lifetime = life;
    }

    private void Start()
    {
        if (visualChild == null)
        {
            // Still rolls and burns, just invisible.
            Debug.LogError("[RollingFireball] visualChild is not assigned (the rolling mesh child).", this);
        }

        // Attach fire status particles if available
        if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.fireStatusVfx != null)
        {
            GameObject fireParticles = Instantiate(ElementalEffectsManager.Instance.fireStatusVfx, transform.position, Quaternion.identity, transform);
            fireParticles.transform.localScale = Vector3.one * (radius * 1.5f);
        }

        // Snap to ground initially
        AlignWithGround();
    }

    private void Update()
    {
        aliveTime += Time.deltaTime;
        if (aliveTime >= lifetime)
        {
            Explode();
            return;
        }

        // 1. Move forward
        float stepDist = currentSpeed * Time.deltaTime;
        transform.position += moveDirection * stepDist;

        // 2. Align to ground
        AlignWithGround();

        // 3. Roll visual mesh
        if (visualChild != null && moveDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 rotationAxis = Vector3.Cross(Vector3.up, moveDirection).normalized;
            float rollAngle = (stepDist / Mathf.Max(0.1f, radius)) * Mathf.Rad2Deg;
            visualChild.Rotate(rotationAxis, rollAngle, Space.World);
        }

        // 4. Drop blazing fire trail
        if (Time.time >= nextTrailTime)
        {
            nextTrailTime = Time.time + trailDropInterval;
            DropTrailSegment();
        }

        // 5. Detect and damage enemies
        CheckEnemyCollisions();
    }

    private void AlignWithGround()
    {
        Vector3 rayStart = transform.position + Vector3.up * 1.5f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 4f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position = new Vector3(transform.position.x, hit.point.y + radius, transform.position.z);
        }
    }

    private void DropTrailSegment()
    {
        GameObject segmentObj = new GameObject("FireTrailSegment");
        segmentObj.transform.position = transform.position - Vector3.up * (radius * 0.7f);

        FireTrailSegment segment = segmentObj.AddComponent<FireTrailSegment>();
        float dps = 18f;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            dps *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
        }
        segment.Init(dps, 3.5f, trailVfxPrefab);
    }

    private void CheckEnemyCollisions()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius * 1.15f, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            if (Player.Instance != null && (col.transform.root == Player.Instance.transform.root || col.gameObject == Player.Instance.gameObject))
            {
                continue;
            }

            Health enemyHealth = col.GetComponentInParent<Health>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                if (!hitCooldowns.TryGetValue(enemyHealth, out float nextAllowed) || Time.time >= nextAllowed)
                {
                    hitCooldowns[enemyHealth] = Time.time + 0.4f;

                    float actualDamage = rollDamage;
                    if (Player.Instance != null && Player.Instance.Stats != null)
                    {
                        actualDamage *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
                    }

                    Damage damage = new Damage
                    {
                        value = actualDamage,
                        type = DamageType.elemental,
                        elementId = "Fire",
                        sourcePosition = transform.position,
                        direction = moveDirection,
                        knockbackForce = 4f,
                        source = Player.Instance != null ? Player.Instance.Damageable : null,
                        isPlayerDamage = true
                    };

                    enemyHealth.ReceiveDamage(damage);
                    EnemyStatusManager.GetOrAdd(enemyHealth)?.ApplyStatus("Fire");
                }
            }
        }
    }

    /// <summary>
    ///     IDamageable implementation: When struck by the player's melee attack,
    ///     redirects the rolling direction and boosts rolling speed.
    /// </summary>
    public void ReceiveDamage(Damage damage)
    {
        if (!damage.isPlayerDamage) return;

        // Redirect direction
        Vector3 newDir = Vector3.zero;
        if (damage.direction.sqrMagnitude > 0.0001f)
        {
            newDir = damage.direction;
        }
        else if (Player.Instance != null)
        {
            newDir = transform.position - Player.Instance.transform.position;
        }
        newDir.y = 0f;

        if (newDir.sqrMagnitude > 0.0001f)
        {
            moveDirection = newDir.normalized;
        }

        // Boost speed
        currentSpeed = Mathf.Min(maxSpeed, currentSpeed * speedBoostMultiplier);

        // Feedback
        if (hitBurstVfxPrefab != null)
        {
            Instantiate(hitBurstVfxPrefab, transform.position, Quaternion.identity);
        }
        else if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.plasmaOverloadVfx != null)
        {
            Instantiate(ElementalEffectsManager.Instance.plasmaOverloadVfx, transform.position, Quaternion.identity);
        }

        if (strikeRedirectSfx != null)
        {
            AudioSource.PlayClipAtPoint(strikeRedirectSfx, transform.position, 1.0f);
        }
    }

    private void Explode()
    {
        // Final splash burst
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius * 3f, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;
            if (Player.Instance != null && col.transform.root == Player.Instance.transform.root) continue;

            Health enemy = col.GetComponentInParent<Health>();
            if (enemy != null && !enemy.IsDead)
            {
                Damage dmg = new Damage
                {
                    value = rollDamage * 1.5f,
                    type = DamageType.elemental,
                    elementId = "Fire",
                    sourcePosition = transform.position,
                    isPlayerDamage = true
                };
                enemy.ReceiveDamage(dmg);
                EnemyStatusManager.GetOrAdd(enemy)?.ApplyStatus("Fire");
            }
        }

        if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.fireStatusVfx != null)
        {
            Instantiate(ElementalEffectsManager.Instance.fireStatusVfx, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
