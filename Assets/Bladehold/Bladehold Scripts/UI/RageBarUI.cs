using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
///     HUD bar for the Berserker's rage meter. Polls <see cref="RageBuff.RageFraction" /> every frame
///     (rage decays continuously — the <see cref="SwordChargeFeedback" /> polling pattern) and drives
///     an <see cref="MMProgressBar" /> plus an optional TMP label. Class-conditional by design: when the
///     player has no enabled <see cref="RageBuff" /> (the Swordsman), the whole object hides itself.
///     Plays an <see cref="MMF_Player" /> feedback when rage increases (e.g. shaking), and a pulse feedback when full.
/// </summary>
public class RageBarUI : MonoBehaviour
{
    [Tooltip("The player's RageBuff. Defaults to the one on Player.Instance.")]
    [SerializeField] private RageBuff rage;
    [Tooltip("MMProgressBar whose fill tracks the meter.")]
    [SerializeField] private MMProgressBar progressBar;
    [Tooltip("Optional numeric label, e.g. \"62\".")]
    [SerializeField] private TMP_Text label;
    
    [Header("Feedbacks")]
    [Tooltip("Feedback played when rage increases.")]
    [SerializeField] private MMF_Player gainFeedback;
    [Tooltip("Feedback played constantly while rage is full.")]
    [SerializeField] private MMF_Player fullFeedback;
    [Tooltip("Feedback played when rage drops from full to not-full.")]
    [SerializeField] private MMF_Player dropFromFullFeedback;

    private bool anyError = false;
    private float lastRage = 0f;
    private bool wasFull = false;

    private void OnValidate()
    {
        if (progressBar == null)
        {
            progressBar = GetComponent<MMProgressBar>();
        }
    }

    private void Start()
    {
        if (rage == null && Player.Instance != null)
        {
            rage = Player.Instance.GetComponent<RageBuff>();
        }

        if (rage == null || !rage.isActiveAndEnabled)
        {
            gameObject.SetActive(false);
            return;
        }

        if (progressBar == null)
        {
            Debug.LogError("RageBarUI: MMProgressBar is not assigned or found on the GameObject.");
            anyError = true;
            return;
        }

        lastRage = rage.CurrentRage;
        progressBar.UpdateBar(rage.CurrentRage, 0f, rage.MaxRage);
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        float currentRage = rage.CurrentRage;
        float maxRage = rage.MaxRage;
        
        progressBar.UpdateBar(currentRage, 0f, maxRage);

        if (label != null)
        {
            label.text = Mathf.RoundToInt(currentRage).ToString();
        }

        bool isFull = currentRage >= maxRage && maxRage > 0f;

        if (currentRage > lastRage)
        {
            if (gainFeedback != null)
            {
                gainFeedback.PlayFeedbacks();
            }
        }

        if (isFull && !wasFull)
        {
            if (fullFeedback != null)
            {
                fullFeedback.PlayFeedbacks();
            }
        }
        else if (!isFull && wasFull)
        {
            if (fullFeedback != null)
            {
                fullFeedback.StopFeedbacks();
            }
            if (dropFromFullFeedback != null)
            {
                dropFromFullFeedback.PlayFeedbacks();
            }
        }

        lastRage = currentRage;
        wasFull = isFull;
    }
}
