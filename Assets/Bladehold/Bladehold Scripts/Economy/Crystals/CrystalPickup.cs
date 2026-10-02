using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A dropped elemental crystal (plan 17). Walk over it (or ride over it) to bank it in
///     <see cref="RunSession" />. One prefab for all three elements: <see cref="Init" /> tints the
///     renderers and the glow light to the element. Same shape as <see cref="AmmoPickup" />.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CrystalPickup : MonoBehaviour
{
    [SerializeField] private DamageNumber pickupPopup;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.6f, 0f);
    [Tooltip("Played on collection (chime, sparkle).")]
    [SerializeField] private MMF_Player pickupFeedback;
    [Tooltip("Renderers tinted to the element colour (emission too, if the material has it).")]
    [SerializeField] private Renderer[] tintRenderers;
    [SerializeField] private Light glowLight;
    [Tooltip("Seconds before an uncollected crystal fades. 0 = never.")]
    [SerializeField] private float lifetime = 90f;

    [Header("Hover")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float rotateSpeed = 90f;

    private StructureElement element = StructureElement.Fire;
    private int amount = 1;
    private bool collected;
    private Vector3 initialPosition;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public StructureElement Element => element;

    /// <summary>Spawns a crystal of <paramref name="crystal" /> at <paramref name="position" />.</summary>
    public static CrystalPickup Spawn(CrystalPickup prefab, Vector3 position, StructureElement crystal, int count)
    {
        if (prefab == null || crystal == StructureElement.None || count <= 0) return null;
        CrystalPickup pickup = Instantiate(prefab, position + Vector3.up * 0.6f, Quaternion.identity);
        pickup.Init(crystal, count);
        return pickup;
    }

    public void Init(StructureElement crystal, int count)
    {
        element = crystal;
        amount = Mathf.Max(1, count);
        Color tint = crystal.Tint();
        var block = new MaterialPropertyBlock();
        if (tintRenderers != null)
        {
            foreach (Renderer r in tintRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, tint);
                block.SetColor(EmissionId, tint * 2f);
                r.SetPropertyBlock(block);
            }
        }
        if (glowLight != null) glowLight.color = tint;
    }

    private void Start()
    {
        initialPosition = transform.position;
        if (pickupPopup == null) Debug.LogError($"[CrystalPickup] {name}: pickupPopup is not assigned.", this);
        if (pickupFeedback == null) Debug.LogError($"[CrystalPickup] {name}: pickupFeedback is not assigned.", this);
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (bobHeight > 0f && bobSpeed > 0f)
        {
            Vector3 p = transform.position;
            p.y = initialPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = p;
        }
        if (rotateSpeed != 0f) transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null) return false;

        HorsePickupProxy proxy = collector.GetComponentInParent<HorsePickupProxy>();
        if (proxy != null && proxy.Target != null) collector = proxy.Target;
        if (collector.GetComponentInParent<Player>() == null) return false;

        collected = true;
        RunSession.AddCrystals(element, amount);
        if (pickupPopup != null)
        {
            pickupPopup.Spawn(transform.position + popupOffset, $"+{amount} {element.CrystalName()}");
        }
        if (pickupFeedback != null) pickupFeedback.PlayFeedbacks(transform.position);
        Destroy(gameObject);
        return true;
    }
}
