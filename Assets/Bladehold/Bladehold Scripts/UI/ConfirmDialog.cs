using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Generic reusable Yes/No confirmation modal (currently used for Delete Save in
///     <see cref="SettingsPanelView" />). Hidden and non-interactive until <see cref="Show" /> is
///     called; fades on unscaled time — the same <see cref="CanvasGroup" /> approach as
///     <see cref="DeathScreen" />'s fade — so it still works while the game is paused.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private float fadeDuration = 0.15f;

    private Action onConfirm;
    private Action onCancel;
    private bool anyError = false;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Awake()
    {
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (confirmButton == null)
        {
            Debug.LogError("Confirm button is not assigned in the inspector.");
            anyError = true;
        }
        if (cancelButton == null)
        {
            Debug.LogError("Cancel button is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        confirmButton.onClick.AddListener(HandleConfirm);
        cancelButton.onClick.AddListener(HandleCancel);
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancel);
    }

    public void Show(string message, Action onConfirmed, Action onCancelled = null, string confirmLabel = null)
    {
        if (anyError)
        {
            return;
        }

        // Default resolved here rather than in the signature so the localized text is fetched at
        // show time (a compile-time default couldn't react to language changes).
        if (string.IsNullOrEmpty(confirmLabel))
        {
            confirmLabel = Loc.Get("common.confirm");
        }

        onConfirm = onConfirmed;
        onCancel = onCancelled;

        if (messageText != null)
        {
            messageText.text = message;
        }

        // The dialog is shared between actions (Delete Save, Reset Settings), so the confirm label is
        // per-Show rather than authored on the button.
        TMP_Text confirmButtonLabel = confirmButton.GetComponentInChildren<TMP_Text>();
        if (confirmButtonLabel != null)
        {
            confirmButtonLabel.text = confirmLabel;
        }

        StopAllCoroutines();
        StartCoroutine(Fade(1f));
    }

    private void HandleConfirm()
    {
        Action callback = onConfirm;
        StopAllCoroutines();
        StartCoroutine(FadeOutThen(callback));
    }

    /// <summary>Public cancel entry point so pad-cancel (MenuFocusController.onCancel) can bind it in the inspector — same path as the Cancel button.</summary>
    public void Cancel() => HandleCancel();

    private void HandleCancel()
    {
        Action callback = onCancel;
        StopAllCoroutines();
        StartCoroutine(FadeOutThen(callback));
    }

    private IEnumerator FadeOutThen(Action callback)
    {
        yield return Fade(0f);
        callback?.Invoke();
    }

    private IEnumerator Fade(float target)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = target > 0.5f;

        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = fadeDuration > 0f ? Mathf.Lerp(start, target, elapsed / fadeDuration) : target;
            yield return null;
        }
        canvasGroup.alpha = target;

        if (target <= 0f)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }
}
