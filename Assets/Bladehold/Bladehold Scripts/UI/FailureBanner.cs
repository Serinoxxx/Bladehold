using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
///     A full-screen "failure reason" banner shown for a few seconds when the run ends, before the
///     <see cref="DeathScreen" /> fades in — e.g. "The gate was destroyed. We were overrun." or
///     "The hero has fallen. All hope is lost." Purely presentational: it owns its own
///     <see cref="CanvasGroup" /> and fade timings, displays whatever message it's given, and never
///     listens to game events itself — <see cref="DeathScreen" /> orchestrates the sequence (banner
///     first, then the skill-tree screen) and supplies the per-loss-condition message, the same way
///     it already owns the per-condition headline strings.
///
///     Must NOT be parented under the death screen's <see cref="CanvasGroup" /> — group alphas
///     multiply, and the death screen sits at alpha 0 while the banner plays.
///
///     All fades run on unscaled time, since a gate loss freezes <c>Time.timeScale</c>.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class FailureBanner : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Label the failure reason is written to.")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("Seconds to fade the banner in.")]
    [SerializeField] private float fadeInDuration = 1f;
    [Tooltip("Seconds the banner stays fully visible before fading out.")]
    [SerializeField] private float holdDuration = 2.5f;
    [Tooltip("Seconds to fade the banner back out before the death screen appears.")]
    [SerializeField] private float fadeOutDuration = 0.75f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (messageText == null)
        {
            messageText = GetComponentInChildren<TMP_Text>();
        }
    }

    private void Start()
    {
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is not assigned or found on the FailureBanner.");
            anyError = true;
        }
        if (messageText == null)
        {
            Debug.LogError("Message text is not assigned on the FailureBanner.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Invisible and inert until DeathScreen plays it; it's never interactive.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    ///     Fades the banner in, holds, and fades it back out. Run to completion by the caller
    ///     (<see cref="DeathScreen" /> yields on it before fading itself in).
    /// </summary>
    public IEnumerator PlayRoutine(string message)
    {
        if (anyError)
        {
            yield break;
        }

        messageText.text = message;

        yield return Fade(0f, 1f, fadeInDuration);

        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return Fade(1f, 0f, fadeOutDuration);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
