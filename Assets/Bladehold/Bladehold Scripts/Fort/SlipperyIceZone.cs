using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Slippery Ice Zone created on impact by Glacial Catapults.
///     Enemies caught on the ice slip over and fall down (KnockbackReceiver.TriggerKnockdown).
///     The player slides on the ice with boosted move speed and reduced friction (PlayerIceSlideController).
/// </summary>
public class SlipperyIceZone : MonoBehaviour
{
    [SerializeField] private float radius = 5.5f;
    [SerializeField] private float duration = 10.0f;
    [SerializeField] private float enemySlipCooldown = 3.5f;
    [SerializeField] private AudioClip slipSfx;
    [Tooltip("The ice pad child, authored for a 1m radius; x/z are scaled to the zone radius.")]
    [SerializeField] private Transform visualRoot;

    private readonly Dictionary<KnockbackReceiver, float> slipCooldowns = new Dictionary<KnockbackReceiver, float>();
    private readonly Collider[] hitBuffer = new Collider[32];
    private float aliveTime = 0f;
    private bool playerInside = false;
    private Vector3 visualBaseScale = Vector3.one;

    public float Radius => radius;
    public float Duration => duration;

    /// <summary>Instantiates the authored ice zone prefab (CatapultProjectile.slipperyIceZonePrefab).</summary>
    public static SlipperyIceZone Spawn(SlipperyIceZone prefab, Vector3 position, float rad = 5.5f, float dur = 10f)
    {
        if (prefab == null)
        {
            Debug.LogError("[SlipperyIceZone] No ice zone prefab: assign CatapultProjectile.slipperyIceZonePrefab.");
            return null;
        }

        SlipperyIceZone zone = Instantiate(prefab, position, Quaternion.identity);
        zone.Init(rad, dur);
        return zone;
    }

    public void Init(float rad, float dur)
    {
        radius = rad;
        duration = dur;
        ScaleVisual();
    }

    private void Awake()
    {
        if (visualRoot != null) visualBaseScale = visualRoot.localScale;
    }

    private void Start()
    {
        if (visualRoot == null)
        {
            // Still slips enemies, just invisible.
            Debug.LogError("[SlipperyIceZone] visualRoot is not assigned (the ice pad child, authored for a 1m radius).", this);
        }
        ScaleVisual();

        // Spawn chilled status vfx if available
        if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.iceStatusVfx != null)
        {
            GameObject frostVfx = Instantiate(ElementalEffectsManager.Instance.iceStatusVfx, transform.position + Vector3.up * 0.2f, Quaternion.identity, transform);
            frostVfx.transform.localScale = Vector3.one * (radius * 0.5f);
        }
    }

    private void ScaleVisual()
    {
        if (visualRoot == null) return;
        visualRoot.localScale = new Vector3(visualBaseScale.x * radius, visualBaseScale.y, visualBaseScale.z * radius);
    }

    private void Update()
    {
        aliveTime += Time.deltaTime;
        if (aliveTime >= duration)
        {
            Destroy(gameObject);
            return;
        }

        TickZone();
    }

    private void TickZone()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, hitBuffer);
        bool foundPlayer = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            // 1. Check Player
            if (Player.Instance != null && (col.transform.root == Player.Instance.transform.root || col.gameObject == Player.Instance.gameObject))
            {
                foundPlayer = true;
                continue;
            }

            // 2. Check Enemies -> Slip Over!
            Health enemyHealth = col.GetComponentInParent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;

            KnockbackReceiver receiver = enemyHealth.GetComponent<KnockbackReceiver>();
            if (receiver != null && !receiver.IsIncapacitated)
            {
                if (!slipCooldowns.TryGetValue(receiver, out float nextAllowed) || Time.time >= nextAllowed)
                {
                    slipCooldowns[receiver] = Time.time + enemySlipCooldown;
                    receiver.TriggerKnockdown(1.8f);

                    // Apply Ice status as well
                    EnemyStatusManager.GetOrAdd(enemyHealth)?.ApplyStatus("Ice", 0.5f);

                    if (slipSfx != null)
                    {
                        AudioSource.PlayClipAtPoint(slipSfx, receiver.transform.position, 0.7f);
                    }
                }
            }
        }

        // Handle Player Ice Slide Controller
        if (foundPlayer && !playerInside)
        {
            playerInside = true;
            if (Player.Instance != null)
            {
                PlayerIceSlideController.GetOrAdd(Player.Instance)?.RegisterIceZone();
            }
        }
        else if (!foundPlayer && playerInside)
        {
            playerInside = false;
            if (Player.Instance != null)
            {
                PlayerIceSlideController.GetOrAdd(Player.Instance)?.UnregisterIceZone();
            }
        }
    }

    private void OnDestroy()
    {
        if (playerInside && Player.Instance != null)
        {
            PlayerIceSlideController.GetOrAdd(Player.Instance)?.UnregisterIceZone();
            playerInside = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1.0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
