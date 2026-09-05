using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
///     Displays the player's current coin total. Binds to the player's <see cref="Wallet" /> through
///     the <see cref="Player" /> singleton and refreshes whenever the total changes.
/// </summary>
public class CoinUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional: played (e.g. a label pop/scale) whenever the total increases.")]
    [SerializeField] private MMF_Player gainFeedback;

    private Wallet wallet;
    private int previousCoins;
    private bool hasPreviousCoins;
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

        if (anyError)
        {
            return;
        }

        RunSession.OnInRunGoldChanged -= UpdateLabel;
        RunSession.OnInRunGoldChanged += UpdateLabel;

        // Show the starting total immediately.
        UpdateLabel(RunSession.InRunGold);
    }

    private void OnDestroy()
    {
        RunSession.OnInRunGoldChanged -= UpdateLabel;
    }

    private void UpdateLabel(int coins)
    {
        label.text = coins.ToString();

        if (gainFeedback != null && hasPreviousCoins && coins > previousCoins)
        {
            gainFeedback.PlayFeedbacks();
        }
        previousCoins = coins;
        hasPreviousCoins = true;
    }
}
