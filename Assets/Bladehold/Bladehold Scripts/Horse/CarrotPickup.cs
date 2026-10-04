using System;
using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A carrot on the ground that refills the warhorse's charge stamina by <see cref="staminaAmount" />
///     (scaled by <see cref="StatType.HorseStaminaGainMultiplier" />, so the Sack of Carrots boosts it
///     too). Rare enemy drop through the shared <see cref="PowerupDropSO" />, and placed by hand in the
///     tutorial (<see cref="CollectCarrotsStep" />). Collected on foot (the stamina banks for the next
///     summon via <see cref="PlayerMount.AddMountStamina" />) or by the ridden horse through
///     <see cref="HorsePickupProxy" />. Like <see cref="AmmoPickup" />, it stays on the ground while the
///     bar is full, unless <see cref="collectWhenFull" /> (tutorial carrots, so a step can't stall).
///     Never collected once the run's horse is lost. Requires a trigger <see cref="Collider" />.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CarrotPickup : MonoBehaviour
{
    [Tooltip("Raw charge stamina restored (the horse's pool is HorseSO.maxStamina, 100 on the warhorse).")]
    [Min(0f)] [SerializeField] private float staminaAmount = 25f;

    [Tooltip("Eat the carrot even when the stamina bar is already full. On for the tutorial's placed carrots.")]
    [SerializeField] private bool collectWhenFull;

    [Tooltip("Optional DamageNumbersPro popup showing the stamina restored.")]
    [SerializeField] private DamageNumber pickupPopup;

    [Tooltip("World-space offset from the carrot where the pickup popup spawns.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);

    [SerializeField] private MMF_Player pickupFeedback;

    [Tooltip("Seconds before an uncollected carrot expires. 0 = never expires (placed carrots).")]
    [SerializeField] private float lifetime = 60f;

    [Header("Hover Animation (Optional)")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.1f;
    [SerializeField] private float rotateSpeed = 60f;

    /// <summary>Raised once when this carrot is eaten.</summary>
    public event Action<CarrotPickup> OnCollected;

    public bool IsCollected => collected;

    private bool collected;
    private Vector3 initialPosition;

    private void Start()
    {
        initialPosition = transform.position;
        if (lifetime > 0f)
        {
            // A pickup destroys the carrot first, making this pending destroy a harmless no-op.
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        if (bobHeight > 0f && bobSpeed > 0f)
        {
            float newY = initialPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        if (rotateSpeed != 0f)
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    /// <summary>
    ///     Eats this carrot on behalf of <paramref name="collector" /> (the player, or the horse they
    ///     ride). Returns false if already eaten, the collector isn't the player, the run's horse is
    ///     lost, or the bar is full and <see cref="collectWhenFull" /> is off.
    /// </summary>
    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null || RunSession.MountLost)
        {
            return false;
        }

        HorsePickupProxy proxy = collector.GetComponentInParent<HorsePickupProxy>();
        if (proxy != null && proxy.Target != null)
        {
            collector = proxy.Target;
        }

        Player player = collector.GetComponentInParent<Player>();
        if (player == null)
        {
            return false;
        }

        // PlayerMount sits on the player root; Player is on the Synty character child.
        PlayerMount mount = player.transform.root.GetComponentInChildren<PlayerMount>(true);
        if (mount == null)
        {
            return false;
        }

        float before = mount.MountStaminaFraction;
        if (before >= 1f && !collectWhenFull)
        {
            return false;
        }

        collected = true;
        float gain = player.Stats != null ? Mathf.Max(0f, player.Stats.GetValue(StatType.HorseStaminaGainMultiplier)) : 1f;
        mount.AddMountStamina(staminaAmount * gain);

        int restoredPercent = Mathf.RoundToInt((mount.MountStaminaFraction - before) * 100f);
        if (pickupPopup != null && restoredPercent > 0)
        {
            pickupPopup.Spawn(transform.position + popupOffset, string.Format(Loc.Get("carrot.popup_gained", "+{0}% charge"), restoredPercent));
        }

        if (pickupFeedback != null)
        {
            pickupFeedback.PlayFeedbacks();
        }

        OnCollected?.Invoke(this);
        Destroy(gameObject);
        return true;
    }
}
