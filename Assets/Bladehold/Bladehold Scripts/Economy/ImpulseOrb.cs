using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     An Impulse Orb pickup dropped by a dying Impulse Goblin (<see cref="ImpulseGoblin" />). When
///     the player walks over it, it grants buff time + a stack to the player's
///     <see cref="ImpulseBuff" />, a DamageNumbersPro popup shows the seconds gained, and the orb is
///     consumed. Requires a trigger <see cref="Collider" />. Deliberately a sibling of
///     <see cref="Coin" /> rather than sharing a base class — two pickups don't justify the
///     abstraction; a third pickup type is the cue to extract one.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ImpulseOrb : MonoBehaviour
{
    [SerializeField] private DamageNumber pickupPopup;
    [Tooltip("World-space offset from the orb where the pickup popup spawns.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private MMF_Player pickupFeedback;
    [Tooltip("Seconds before an uncollected orb expires. Shorter than a coin's lifetime on purpose — a timed buff should feel urgent, and nothing else interacts with orbs on the ground. 0 = never expires.")]
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
    ///     Collects this orb on behalf of <paramref name="collector" /> (anything holding an
    ///     <see cref="ImpulseBuff" /> in its parents). Walk-over pickup routes through here; remote
    ///     collectors (the bow's Pickup Arrows) may call it directly. Returns false if already
    ///     collected or the collector has no buff.
    /// </summary>
    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null)
        {
            return false;
        }

        // Only the holder of an ImpulseBuff (the player) can pick the orb up.
        ImpulseBuff buff = collector.GetComponentInParent<ImpulseBuff>();
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
    ///     (the caller owns the blast; this just spends the orb). Returns false if already collected,
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
