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
    [SerializeField] private GameObject iceVisualPrefab;

    private readonly Dictionary<KnockbackReceiver, float> slipCooldowns = new Dictionary<KnockbackReceiver, float>();
    private readonly Collider[] hitBuffer = new Collider[32];
    private float aliveTime = 0f;
    private bool playerInside = false;
    private GameObject visualInstance;

    public float Radius => radius;
    public float Duration => duration;

    public static SlipperyIceZone Spawn(Vector3 position, float rad = 5.5f, float dur = 10f, GameObject visualPrefab = null, AudioClip slipAudio = null)
    {
        GameObject zoneObj = new GameObject("SlipperyIceZone");
        zoneObj.transform.position = position;

        SlipperyIceZone zone = zoneObj.AddComponent<SlipperyIceZone>();
        zone.Init(rad, dur, visualPrefab, slipAudio);
        return zone;
    }

    public void Init(float rad, float dur, GameObject visualPrefab = null, AudioClip slipAudio = null)
    {
        radius = rad;
        duration = dur;
        iceVisualPrefab = visualPrefab;
        slipSfx = slipAudio;

        CreateVisual();
    }

    private void Start()
    {
        if (visualInstance == null)
        {
            CreateVisual();
        }
    }

    private void CreateVisual()
    {
        if (visualInstance != null) return;

        if (iceVisualPrefab != null)
        {
            visualInstance = Instantiate(iceVisualPrefab, transform.position, Quaternion.identity, transform);
        }
        else
        {
            // Procedural ice disc pad
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "IceDiscVisual";
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            disc.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);

            // Remove cylinder collider so it doesn't block player/enemies
            Collider discCol = disc.GetComponent<Collider>();
            if (discCol != null)
            {
                if (Application.isPlaying) Destroy(discCol);
                else DestroyImmediate(discCol);
            }

            // Frost tint
            if (disc.TryGetComponent(out Renderer rend))
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.5f, 0.85f, 1.0f, 0.55f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
                rend.material = mat;
            }

            visualInstance = disc;
        }

        // Spawn chilled status vfx if available
        if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.iceStatusVfx != null)
        {
            GameObject frostVfx = Instantiate(ElementalEffectsManager.Instance.iceStatusVfx, transform.position + Vector3.up * 0.2f, Quaternion.identity, transform);
            frostVfx.transform.localScale = Vector3.one * (radius * 0.5f);
        }
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

        if (visualInstance != null)
        {
            Destroy(visualInstance);
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
