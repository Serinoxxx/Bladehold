using UnityEngine;
using TMPro;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using System.Collections;

/// <summary>
///     The mount summon cast bar: fills while channeling, then shows the ride timer for a timed mount.
///     A cancelled summon keeps the bar up with "SUMMONING CANCELLED" and the reason (moved, hit, pressed
///     again), so players learn that moving breaks the cast; <see cref="castCancelledFeedback" /> flashes the
///     bar red, holds it, then fades the canvas group out on unscaled time.
/// </summary>
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

    [Tooltip("Ride durations at or above this many seconds count as unlimited: the bar hides once mounted instead of showing a ride timer.")]
    [SerializeField] private float unlimitedDurationSeconds = 600f;
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
            castLabel.text = Loc.Get("hud.mount.summoning", "Summoning Mount");
        }

        if (playerSummonMount == null)
        {
            TryResolvePlayerSummonMount();
        }

        StartCoroutine(InitRoutine());
    }

    private void TryResolvePlayerSummonMount()
    {
        if (Player.Instance != null)
        {
            playerSummonMount = Player.Instance.transform.root.GetComponentInChildren<PlayerSummonMount>(true);
        }

        if (playerSummonMount == null)
        {
            playerSummonMount = FindFirstObjectByType<PlayerSummonMount>();
        }
    }

    private IEnumerator InitRoutine()
    {
        int retries = 10;
        while (playerSummonMount == null && retries > 0)
        {
            TryResolvePlayerSummonMount();
            if (playerSummonMount != null) break;
            retries--;
            yield return null;
        }

        if (playerSummonMount == null)
        {
            Debug.LogWarning("[SummonCastBarUI] PlayerSummonMount not found in scene. Mount cast bar will remain inactive.", this);
            yield break;
        }

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
        StopCancelledFeedback();
        canvasGroup.alpha = 1f;
        if (castLabel != null) castLabel.text = Loc.Get("hud.mount.summoning", "Summoning Mount");
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
        if (castLabel != null) castLabel.text = Loc.Get("hud.mount.mounted", "Mounted");
        if (castFinishedFeedback != null) castFinishedFeedback.PlayFeedbacks();
    }

    private void HandleCastCancelled(MountCastCancelReason reason)
    {
        // Dying says enough on its own; the bar just goes.
        if (reason == MountCastCancelReason.Died || castCancelledFeedback == null)
        {
            canvasGroup.alpha = 0f;
            return;
        }

        canvasGroup.alpha = 1f;
        if (castLabel != null)
        {
            castLabel.text = $"{Loc.Get("hud.mount.summon_cancelled", "SUMMONING CANCELLED")}\n<size=70%>{CancelReasonText(reason)}</size>";
        }

        // Red flash, hold, then fade the canvas group out (authored on the MMF, unscaled time).
        castCancelledFeedback.PlayFeedbacks();
    }

    private static string CancelReasonText(MountCastCancelReason reason)
    {
        switch (reason)
        {
            case MountCastCancelReason.Moved: return Loc.Get("hud.mount.cancel_moved", "You moved. Stand still to summon");
            case MountCastCancelReason.Damaged: return Loc.Get("hud.mount.cancel_damaged", "You were hit");
            case MountCastCancelReason.PressedAgain: return Loc.Get("hud.mount.cancel_pressed", "You cancelled it");
            default: return string.Empty;
        }
    }

    // A new cast or a ride timer takes the bar back from a still-fading cancelled message.
    private void StopCancelledFeedback()
    {
        if (castCancelledFeedback != null && castCancelledFeedback.IsPlaying)
        {
            castCancelledFeedback.StopFeedbacks();
            castCancelledFeedback.RestoreInitialValues();
        }
    }
    
    private void HandleDurationUpdated(float current, float max)
    {
        if (max >= unlimitedDurationSeconds)
        {
            // An unlimited ride (the basic warhorse) has no timer worth showing.
            canvasGroup.alpha = 0f;
            return;
        }

        StopCancelledFeedback();
        canvasGroup.alpha = 1f;
        if (castLabel != null) castLabel.text = Loc.Get("hud.mount.mounted", "Mounted");
        progressBar.SetBar(current, 0f, max);
    }
    
    private void HandleCooldownUpdated(float current, float max)
    {
        canvasGroup.alpha = 0f;
    }
}
