using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
///     Displays the run's gold (<see cref="RunSession.InRunGold" />, the only gold currency) and refreshes
///     whenever it changes.
/// </summary>
public class CoinUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional: played (e.g. a label pop/scale) whenever the total increases.")]
    [SerializeField] private MMF_Player gainFeedback;

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

    public void Refresh()
    {
        UpdateLabel(RunSession.InRunGold);
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
