using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Warning modal for deleting a save slot: names the slot and what's in it, then requires holding
///     <see cref="HoldToConfirmButton" /> to actually delete, so it can't happen by accident. Fades on
///     unscaled time like <see cref="ConfirmDialog" />.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SaveSlotDeleteDialog : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private HoldToConfirmButton deleteButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private MenuFocusController focus;
    [SerializeField] private float fadeDuration = 0.15f;

    public bool IsOpen { get; private set; }

    private Action onDeleted;
    private Action onClosed;
    private bool anyError;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (focus == null)
        {
            focus = GetComponent<MenuFocusController>();
        }
    }

    private void Awake()
    {
        if (canvasGroup == null || deleteButton == null || cancelButton == null || titleText == null || messageText == null)
        {
            Debug.LogError("[SaveSlotDeleteDialog] CanvasGroup, title/message text, delete or cancel button is not assigned.", this);
            anyError = true;
            return;
        }

        SetVisible(0f);
        deleteButton.onConfirmed.AddListener(HandleDelete);
        cancelButton.onClick.AddListener(Cancel);
    }

    private void OnDestroy()
    {
        if (deleteButton != null) deleteButton.onConfirmed.RemoveListener(HandleDelete);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
    }

    public void Show(int slot, SaveData data, Action deleted, Action closed)
    {
        if (anyError)
        {
            return;
        }
        onDeleted = deleted;
        onClosed = closed;
        IsOpen = true;

        titleText.text = string.Format(Loc.Get("saveslots.delete_title", "Delete Slot {0}?"), slot + 1);
        string played = SaveSlotView.FormatPlayTime(data != null ? data.playTimeSeconds : 0d);
        messageText.text = string.Format(
            Loc.Get("saveslots.delete_body", "{0} of progress will be lost forever.\nThis cannot be undone."), played);

        deleteButton.ResetHold();
        if (focus != null)
        {
            focus.enabled = true;
            focus.SetDefaultSelectable(cancelButton);
        }
        StopAllCoroutines();
        StartCoroutine(Fade(1f));
    }

    /// <summary>Cancel button and pad B (via <see cref="MenuFocusController" />.onCancel).</summary>
    public void Cancel()
    {
        Close(onClosed);
    }

    private void HandleDelete()
    {
        Action deleted = onDeleted;
        Close(() =>
        {
            deleted?.Invoke();
        });
    }

    private void Close(Action then)
    {
        if (!IsOpen)
        {
            return;
        }
        IsOpen = false;
        if (focus != null)
        {
            focus.enabled = false;
        }
        StopAllCoroutines();
        StartCoroutine(FadeOutThen(then));
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
            float a = Mathf.Lerp(start, target, elapsed / fadeDuration);
            canvasGroup.alpha = a;
            transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, a);
            yield return null;
        }
        SetVisible(target);
    }

    private void SetVisible(float alpha)
    {
        canvasGroup.alpha = alpha;
        canvasGroup.interactable = alpha > 0.5f;
        canvasGroup.blocksRaycasts = alpha > 0.5f;
        transform.localScale = Vector3.one;
        if (focus != null && alpha <= 0f)
        {
            focus.enabled = false;
        }
    }
}
