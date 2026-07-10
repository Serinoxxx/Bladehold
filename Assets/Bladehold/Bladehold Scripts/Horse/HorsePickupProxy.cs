using UnityEngine;

/// <summary>
///     Lets the horse collect pickups on behalf of its rider: while the player is mounted,
///     <see cref="Target" /> points at the player root, and every pickup's <c>TryCollect</c>
///     redirects its collector there (see <c>Coin</c>/<c>HealthPack</c>/<c>ImpulseOrb</c>/
///     <c>LightningOrb</c>) — so coins land in the Wallet and buffs on the player, never on the
///     horse. Riderless and knight-ridden horses have a null <see cref="Target" /> and collect
///     nothing. <see cref="HorseHealth" /> is exposed for the "Stable Diet" rule, where Health
///     Packs the horse runs over also heal the horse.
/// </summary>
public class HorsePickupProxy : MonoBehaviour
{
    [SerializeField] private Health horseHealth;

    /// <summary>The mounted player's root, or null when nobody (or the knight) is riding.</summary>
    public GameObject Target { get; private set; }

    /// <summary>The horse's own Health, for the Stable Diet horse-heal rule.</summary>
    public Health HorseHealth => horseHealth;

    private void OnValidate()
    {
        if (horseHealth == null)
        {
            horseHealth = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (horseHealth == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
        }
    }

    /// <summary>Called by <c>PlayerMount</c> when the player mounts.</summary>
    public void SetRider(GameObject playerRoot)
    {
        Target = playerRoot;
    }

    /// <summary>Called by <c>PlayerMount</c> on dismount.</summary>
    public void ClearRider()
    {
        Target = null;
    }
}
