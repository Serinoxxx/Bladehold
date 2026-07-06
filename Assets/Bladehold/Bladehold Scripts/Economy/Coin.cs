using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A coin pickup dropped by a dying enemy. When the player walks over it, the coins are added to
///     the player's <see cref="Wallet" /> and the run's <see cref="GameStats" />, a DamageNumbersPro
///     popup shows the amount, and the coin is consumed. Requires a trigger <see cref="Collider" />.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    [SerializeField] private int amount = 1;
    [SerializeField] private DamageNumber pickupPopup;
    [Tooltip("World-space offset from the coin where the pickup popup spawns.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private MMF_Player coinPickupFeedback;
    [Tooltip("Seconds before an uncollected coin expires and disappears, so long fights can't pile up unbounded trigger colliders. Generous by default so Grave Robber still sees recently dropped gold on death. 0 = never expires.")]
    [SerializeField] private float lifetime = 90f;

    private bool collected;

    private void Start()
    {
        if (lifetime > 0f)
        {
            // A pickup destroys the coin first, making this pending destroy a harmless no-op.
            Destroy(gameObject, lifetime);
        }
    }

    /// <summary>How many coins this pickup is worth.</summary>
    public int Amount => amount;

    /// <summary>Sets how many coins this pickup is worth (called by whatever spawns it).</summary>
    public void SetAmount(int value)
    {
        amount = value;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    /// <summary>
    ///     Collects this coin on behalf of <paramref name="collector" /> (anything holding a
    ///     <see cref="Wallet" /> in its parents). Walk-over pickup routes through here; remote
    ///     collectors (the bow's Pickup Arrows, Grave Robber) may call it directly. Returns false if
    ///     already collected or the collector has no Wallet.
    /// </summary>
    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null)
        {
            return false;
        }

        // Only the holder of a Wallet (the player) can pick the coin up.
        Wallet wallet = collector.GetComponentInParent<Wallet>();
        if (wallet == null)
        {
            return false;
        }

        collected = true;

        wallet.Add(amount);

        if (GameStats.Instance != null)
        {
            GameStats.Instance.AddGold(amount);
        }

        if (pickupPopup != null)
        {
            pickupPopup.Spawn(transform.position + popupOffset, amount);
        }

        if (coinPickupFeedback != null)
        {
            coinPickupFeedback.PlayFeedbacks();
        }

        Destroy(gameObject);
        return true;
    }
}
