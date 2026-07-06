using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A Lightning Orb pickup dropped by a dying Storm Witch (<see cref="LightningOrbDropper" />). When
///     the player walks over it, it grants buff time + a stack to the player's
///     <see cref="ChainLightningBuff" />, a DamageNumbersPro popup shows the seconds gained, and the orb
///     is consumed. Requires a trigger <see cref="Collider" />. A sibling of <see cref="ImpulseOrb" />
///     rather than sharing a base class, per that script's own precedent.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LightningOrb : MonoBehaviour
{
    [SerializeField] private DamageNumber pickupPopup;
    [Tooltip("World-space offset from the orb where the pickup popup spawns.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private MMF_Player pickupFeedback;
    [Tooltip("Seconds before an uncollected orb expires. 0 = never expires.")]
    [SerializeField] private float lifetime = 30f;

    private bool collected;

    private void Start()
    {
        if (lifetime > 0f)
        {
            // A pickup destroys the orb first, making this pending destroy a harmless no-op.
            Destroy(gameObject, lifetime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    /// <summary>
    ///     Collects this orb on behalf of <paramref name="collector" /> (anything holding a
    ///     <see cref="ChainLightningBuff" /> in its parents). Walk-over pickup routes through here;
    ///     remote collectors (the bow's Pickup Arrows) may call it directly. Returns false if already
    ///     collected or the collector has no buff.
    /// </summary>
    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null)
        {
            return false;
        }

        // Only the holder of a ChainLightningBuff (the player) can pick the orb up.
        ChainLightningBuff buff = collector.GetComponentInParent<ChainLightningBuff>();
        if (buff == null)
        {
            return false;
        }

        collected = true;

        float granted = buff.CollectOrb();

        if (pickupPopup != null && granted > 0f)
        {
            pickupPopup.Spawn(transform.position + popupOffset, Mathf.RoundToInt(granted));
        }

        if (pickupFeedback != null)
        {
            pickupFeedback.PlayFeedbacks();
        }

        Destroy(gameObject);
        return true;
    }

    /// <summary>
    ///     Consumes this orb without granting the buff — the Unstable Orbs skill detonates it instead
    ///     (the caller owns the chain; this just spends the orb). Returns false if already collected,
    ///     so a detonation and a walk-over pickup can never both fire.
    /// </summary>
    public bool TryDetonate()
    {
        if (collected)
        {
            return false;
        }
        collected = true;

        if (pickupFeedback != null)
        {
            pickupFeedback.PlayFeedbacks();
        }

        Destroy(gameObject);
        return true;
    }
}
