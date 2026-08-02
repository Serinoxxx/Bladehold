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

    private PlayerSummonMount playerSummonMount;
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

        if (Player.Instance != null)
        {
            playerSummonMount = Player.Instance.GetComponent<PlayerSummonMount>();
            if (playerSummonMount != null)
            {
                playerSummonMount.OnCastStarted += HandleCastStarted;
                playerSummonMount.OnCastUpdated += HandleCastUpdated;
                playerSummonMount.OnCastFinished += HandleCastFinished;
                playerSummonMount.OnCastCancelled += HandleCastCancelled;
            }
        }
    }

    private void OnDestroy()
    {
        if (playerSummonMount != null)
        {
            playerSummonMount.OnCastStarted -= HandleCastStarted;
            playerSummonMount.OnCastUpdated -= HandleCastUpdated;
            playerSummonMount.OnCastFinished -= HandleCastFinished;
            playerSummonMount.OnCastCancelled -= HandleCastCancelled;
        }
    }

    private void HandleCastStarted(float maxTime)
    {
        canvasGroup.alpha = 1f;
        progressBar.UpdateBar(0f, 0f, maxTime);
        
        if (castStartedFeedback != null) castStartedFeedback.PlayFeedbacks();
    }

    private void HandleCastUpdated(float current, float max)
    {
        progressBar.UpdateBar(current, 0f, max);
    }

    private void HandleCastFinished()
    {
        canvasGroup.alpha = 0f;
        if (castFinishedFeedback != null) castFinishedFeedback.PlayFeedbacks();
    }

    private void HandleCastCancelled()
    {
        canvasGroup.alpha = 0f;
        if (castCancelledFeedback != null) castCancelledFeedback.PlayFeedbacks();
    }
}
