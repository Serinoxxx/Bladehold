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
    [SerializeField] private GameObject fireballVisualPrefab;
    [SerializeField] private GameObject trailVfxPrefab;
    [SerializeField] private GameObject hitBurstVfxPrefab;
    [SerializeField] private AudioClip strikeRedirectSfx;
    [SerializeField] private AudioClip rollLoopSfx;

    private Vector3 moveDirection = Vector3.forward;
    private float aliveTime = 0f;
    private float nextTrailTime = 0f;
    private readonly Dictionary<Health, float> hitCooldowns = new Dictionary<Health, float>();
    private readonly Collider[] hitBuffer = new Collider[16];
    private Transform visualChild;
    private AudioSource rollAudioSource;

    public float CurrentSpeed => currentSpeed;
    public Vector3 MoveDirection => moveDirection;
    public Health Health => null; // IDamageable interface

    public static RollingFireball Spawn(Vector3 position, Vector3 direction, float speed = 9f, float damage = 50f, float life = 10f, GameObject visualPrefab = null)
    {
        GameObject fbObj = new GameObject("RollingFireball");
        fbObj.transform.position = position;

        RollingFireball fb = fbObj.AddComponent<RollingFireball>();
        fb.Init(direction, speed, damage, life, visualPrefab);
        return fb;
    }

    public void Init(Vector3 direction, float speed = 9f, float damage = 50f, float life = 10f, GameObject visualPrefab = null)
    {
        direction.y = 0f;
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        currentSpeed = speed;
        rollDamage = damage;
        lifetime = life;
        fireballVisualPrefab = visualPrefab;

        CreateVisual();
    }

    private void Awake()
    {
        // Add a trigger collider so DamageTrigger sweeps can register hits on it
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.radius = radius * 1.2f;
        col.isTrigger = true;
    }

    private void Start()
    {
        if (visualChild == null)
        {
            CreateVisual();
        }

        // Snap to ground initially
        AlignWithGround();
    }

    private void CreateVisual()
    {
        if (visualChild != null) return;

        if (fireballVisualPrefab != null)
        {
            GameObject inst = Instantiate(fireballVisualPrefab, transform.position, Quaternion.identity, transform);
            visualChild = inst.transform;
        }
        else
        {
            // Procedural glowing fireball sphere
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "FireballSphereVisual";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * (radius * 2f);

            Collider c = sphere.GetComponent<Collider>();
            if (c != null)
            {
                if (Application.isPlaying) Destroy(c);
                else DestroyImmediate(c);
            }

            if (sphere.TryGetComponent(out Renderer rend))
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(1.0f, 0.35f, 0.05f);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.45f, 0.1f) * 2.5f);
                }
                rend.material = mat;
            }

            visualChild = sphere.transform;
        }

        // Attach fire status particles if available
        if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.fireStatusVfx != null)
        {
            GameObject fireParticles = Instantiate(ElementalEffectsManager.Instance.fireStatusVfx, transform.position, Quaternion.identity, transform);
            fireParticles.transform.localScale = Vector3.one * (radius * 1.5f);
        }
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

    private void OnDestroy()
    {
        if (visualChild != null)
        {
            Destroy(visualChild.gameObject);
        }
    }
}
