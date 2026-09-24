using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
///     Displays the player's current Supply currency total in the HUD.
///     Binds to <see cref="RunSession.OnInRunSupplyChanged" /> and refreshes whenever the total changes.
/// </summary>
public class SupplyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional: played whenever the supply total increases.")]
    [SerializeField] private MMF_Player gainFeedback;

    private int previousSupply;
    private bool hasPreviousSupply;
    private bool anyError = false;

    private void OnValidate()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>();
        }
    }

    private void Start()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>();
        }

        if (label == null)
        {
            Debug.LogError("[SupplyUI] TMP_Text label is not assigned or found on GameObject.");
            anyError = true;
        }

        if (anyError) return;

        RunSession.OnInRunSupplyChanged -= UpdateLabel;
        RunSession.OnInRunSupplyChanged += UpdateLabel;

        UpdateLabel(RunSession.InRunSupply);
    }

    private void OnDestroy()
    {
        RunSession.OnInRunSupplyChanged -= UpdateLabel;
    }

    public void Refresh()
    {
        UpdateLabel(RunSession.InRunSupply);
    }

    private void UpdateLabel(int supply)
    {
        if (label != null)
        {
            label.text = supply.ToString();
        }

        if (gainFeedback != null && hasPreviousSupply && supply > previousSupply)
        {
            gainFeedback.PlayFeedbacks();
        }

        previousSupply = supply;
        hasPreviousSupply = true;
    }
}
