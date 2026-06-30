using TMPro;
using UnityEngine;

/// <summary>
///     Displays the player's current coin total. Binds to the player's <see cref="Wallet" /> through
///     the <see cref="Player" /> singleton and refreshes whenever the total changes.
/// </summary>
public class CoinUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private Wallet wallet;
    private bool anyError = false;

    private void OnValidate()
    {
        if (label == null)
        {
            label = GetComponent<TMP_Text>();
        }
    }

    private void Start()
    {
        if (label == null)
        {
            Debug.LogError("TMP_Text label is not assigned or found on the GameObject.");
            anyError = true;
        }

        Player player = Player.Instance;
        if (player == null || player.Wallet == null)
        {
            Debug.LogError("No player Wallet found for CoinUI to display.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        wallet = player.Wallet;
        wallet.OnCoinsChanged += UpdateLabel;

        // Show the starting total immediately.
        UpdateLabel(wallet.Coins);
    }

    private void OnDestroy()
    {
        if (wallet != null)
        {
            wallet.OnCoinsChanged -= UpdateLabel;
        }
    }

    private void UpdateLabel(int coins)
    {
        label.text = coins.ToString();
    }
}
