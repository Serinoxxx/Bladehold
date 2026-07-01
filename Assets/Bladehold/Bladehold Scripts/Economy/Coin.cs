using DamageNumbersPro;
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

    private bool collected;

    /// <summary>How many coins this pickup is worth.</summary>
    public int Amount => amount;

    /// <summary>Sets how many coins this pickup is worth (called by whatever spawns it).</summary>
    public void SetAmount(int value)
    {
        amount = value;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
        {
            return;
        }

        // Only the holder of a Wallet (the player) can pick the coin up.
        Wallet wallet = other.GetComponentInParent<Wallet>();
        if (wallet == null)
        {
            return;
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

        Destroy(gameObject);
    }
}
