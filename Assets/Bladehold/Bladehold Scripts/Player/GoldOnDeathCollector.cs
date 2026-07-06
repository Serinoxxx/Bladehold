using UnityEngine;

/// <summary>
///     Grave Robber: a Reincarnate-tree upgrade. Listens to the player's real <see cref="Health.OnDied" />
///     (fires only when death wasn't cancelled by <see cref="DeathNova" />) and auto-collects
///     <see cref="StatType.GoldOnDeathPickupPercent" /> of the gold currently sitting on the ground into the
///     wallet, then removes those coins so they don't linger as a dead pickup once the death screen shows.
/// </summary>
public class GoldOnDeathCollector : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Optional; defaults to Player.Instance.Wallet.")]
    [SerializeField] private Wallet wallet;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (wallet == null)
        {
            wallet = Player.Instance != null ? Player.Instance.Wallet : null;
        }
        if (wallet == null)
        {
            Debug.LogError("GoldOnDeathCollector could not find a Wallet (set it or ensure Player.Instance.Wallet exists).");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("GoldOnDeathCollector could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;

        // The pickup percent is entirely Reincarnate-upgrade-granted, so the base starts at 0 (no pickup)
        // until a node raises it.
        stats.SetBase(StatType.GoldOnDeathPickupPercent, 0f);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        float percent = Mathf.Clamp01(stats.GetValue(StatType.GoldOnDeathPickupPercent));
        if (percent <= 0f)
        {
            return;
        }

        Coin[] groundCoins = Object.FindObjectsByType<Coin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (groundCoins.Length == 0)
        {
            return;
        }

        int total = 0;
        foreach (Coin coin in groundCoins)
        {
            total += coin.Amount;
        }

        int pickup = Mathf.RoundToInt(total * percent);
        if (pickup <= 0)
        {
            return;
        }

        wallet.Add(pickup);
        if (GameStats.Instance != null)
        {
            GameStats.Instance.AddGold(pickup);
        }

        foreach (Coin coin in groundCoins)
        {
            Destroy(coin.gameObject);
        }
    }
}
