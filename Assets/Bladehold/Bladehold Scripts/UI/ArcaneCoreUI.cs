using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
///     Displays the run's Arcane Cores (the ultimate currency, plan 21 phase 5) in the HUD's currency panel.
///     Binds to <see cref="RunSession.OnArcaneCoresChanged" /> and refreshes whenever the count changes.
/// </summary>
public class ArcaneCoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional: played whenever the core count increases.")]
    [SerializeField] private MMF_Player gainFeedback;

    private int previousCores;
    private bool hasPreviousCores;
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
            Debug.LogError("[ArcaneCoreUI] TMP_Text label is not assigned or found on GameObject.");
            anyError = true;
        }

        if (anyError) return;

        RunSession.OnArcaneCoresChanged -= UpdateLabel;
        RunSession.OnArcaneCoresChanged += UpdateLabel;

        UpdateLabel(RunSession.ArcaneCores);
    }

    private void OnDestroy()
    {
        RunSession.OnArcaneCoresChanged -= UpdateLabel;
    }

    private void UpdateLabel(int cores)
    {
        if (label != null)
        {
            label.text = cores.ToString();
        }

        if (gainFeedback != null && hasPreviousCores && cores > previousCores)
        {
            gainFeedback.PlayFeedbacks();
        }

        previousCores = cores;
        hasPreviousCores = true;
    }
}
