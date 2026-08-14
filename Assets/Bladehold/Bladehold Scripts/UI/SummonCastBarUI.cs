using UnityEngine;
using TMPro;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using System.Collections;

public class SummonCastBarUI : MonoBehaviour
{
    [Header("UI Elements")]
    public MMProgressBar progressBar;
    public TextMeshProUGUI castLabel;
    public CanvasGroup canvasGroup;

    [Header("Feedbacks")]
    public MMF_Player castStartedFeedback;
    public MMF_Player castCancelledFeedback;
    public MMF_Player castFinishedFeedback;



    [SerializeField]  private PlayerSummonMount playerSummonMount;
    private bool anyError;

    private void Start()
    {
        if (progressBar == null || canvasGroup == null)
        {
            Debug.LogError("SummonCastBarUI: Missing UI references.", this);
            anyError = true;
        }

        if (anyError) return;

        canvasGroup.alpha = 0f;
        
        if (castLabel != null)
        {
            castLabel.text = "Summoning Mount";
        }

        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        yield return null;

                playerSummonMount.OnCastStarted += HandleCastStarted;
                playerSummonMount.OnCastUpdated += HandleCastUpdated;
                playerSummonMount.OnCastFinished += HandleCastFinished;
                playerSummonMount.OnCastCancelled += HandleCastCancelled;
                
                playerSummonMount.OnDurationUpdated += HandleDurationUpdated;
                playerSummonMount.OnCooldownUpdated += HandleCooldownUpdated;
            }

    private void OnDestroy()
    {
        if (playerSummonMount != null)
        {
            playerSummonMount.OnCastStarted -= HandleCastStarted;
            playerSummonMount.OnCastUpdated -= HandleCastUpdated;
            playerSummonMount.OnCastFinished -= HandleCastFinished;
            playerSummonMount.OnCastCancelled -= HandleCastCancelled;
            
            playerSummonMount.OnDurationUpdated -= HandleDurationUpdated;
            playerSummonMount.OnCooldownUpdated -= HandleCooldownUpdated;
        }
    }

    private void HandleCastStarted(float maxTime)
    {
        canvasGroup.alpha = 1f;
        if (castLabel != null) castLabel.text = "Summoning Mount";
        progressBar.SetBar(0f, 0f, maxTime);
        
        if (castStartedFeedback != null) castStartedFeedback.PlayFeedbacks();
    }

    private void HandleCastUpdated(float current, float max)
    {
        progressBar.SetBar(current, 0f, max);
    }

    private void HandleCastFinished()
    {
        // We stay visible and transition to the duration bar
        if (castLabel != null) castLabel.text = "Mounted";
        if (castFinishedFeedback != null) castFinishedFeedback.PlayFeedbacks();
    }

    private void HandleCastCancelled()
    {
        canvasGroup.alpha = 0f;
        if (castCancelledFeedback != null) castCancelledFeedback.PlayFeedbacks();
    }
    
    private void HandleDurationUpdated(float current, float max)
    {
        canvasGroup.alpha = 1f;
        if (castLabel != null) castLabel.text = "Mounted";
        progressBar.SetBar(current, 0f, max);
    }
    
    private void HandleCooldownUpdated(float current, float max)
    {
        canvasGroup.alpha = 0f;
    }
}
