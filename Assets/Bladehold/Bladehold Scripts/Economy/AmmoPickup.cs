using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     An Ammunition Pack powerup dropped by defeated enemies or found on the battlefield.
///     When collected by the player, it restores a bundle of ammo (default +5) up to MaxAmmo.
///     If the player is already at full capacity, the pack remains on the ground until needed.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AmmoPickup : MonoBehaviour
{
    [Tooltip("Ammunition granted when collected.")]
    [SerializeField] private int ammoAmount = 5;

    [Tooltip("Optional DamageNumbersPro popup indicating ammo gained.")]
    [SerializeField] private DamageNumber pickupPopup;

    [Tooltip("World-space offset from the pack where the pickup popup spawns.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);

    [SerializeField] private MMF_Player pickupFeedback;

    [Tooltip("Seconds before an uncollected pack expires. 0 = never expires.")]
    [SerializeField] private float lifetime = 60f;

    [Header("Hover Animation (Optional)")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.1f;
    [SerializeField] private float rotateSpeed = 60f;

    private bool collected;
    private Vector3 initialPosition;

    private void Start()
    {
        initialPosition = transform.position;
        if (lifetime > 0f)
        {
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
    ///     Collects this ammunition bundle on behalf of <paramref name="collector" />.
    ///     Returns false if already collected, collector is not the player, or player is at max ammo.
    /// </summary>
    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null)
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

        PlayerAmmo ammo = player.Ammo != null ? player.Ammo : PlayerAmmo.Instance;
        if (ammo == null)
        {
            return false;
        }

        if (ammo.CurrentAmmo >= ammo.MaxAmmo)
        {
            // Already full — remain on ground until needed
            return false;
        }

        collected = true;
        int added = ammo.AddAmmo(ammoAmount);

        if (pickupPopup != null && added > 0)
        {
            pickupPopup.Spawn(transform.position + popupOffset, added);
        }

        if (pickupFeedback != null)
        {
            pickupFeedback.PlayFeedbacks();
        }

#if UNITY_EDITOR
        AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Bow/bow_crossbow_arrow_draw_slide1_01.wav");
        if (clip != null)
        {
            MoreMountains.Tools.MMSoundManagerPlayOptions options = MoreMountains.Tools.MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MoreMountains.Tools.MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = transform.position;
            options.Volume = 0.9f;
            options.Pitch = Random.Range(1.1f, 1.3f);
            MoreMountains.Tools.MMSoundManagerSoundPlayEvent.Trigger(clip, options);
        }
#endif

        Destroy(gameObject);
        return true;
    }
}
