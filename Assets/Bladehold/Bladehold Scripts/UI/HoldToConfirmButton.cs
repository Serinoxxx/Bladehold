using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
///     A button that only fires after being held for <see cref="holdDuration" /> — for destructive actions
///     like deleting a save slot, where a stray click must not be enough. Mouse: press and hold on it.
///     Gamepad/keyboard: hold Submit while it's selected. A radial/bar <see cref="fill" /> shows progress and
///     drains back when released early. Runs on unscaled time.
/// </summary>
public class HoldToConfirmButton : Selectable, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Seconds the button must be held to confirm.")]
    [SerializeField] private float holdDuration = 1.5f;
    [Tooltip("How fast the fill drains back after an early release, in fills per second.")]
    [SerializeField] private float drainSpeed = 3f;
    [Tooltip("Filled Image showing hold progress (Image Type = Filled).")]
    [SerializeField] private Image fill;
    [Tooltip("Played when the hold starts.")]
    [SerializeField] private MMF_Player holdStartFeedback;
    [Tooltip("Played when the hold completes and the action fires.")]
    [SerializeField] private MMF_Player confirmFeedback;

    public UnityEvent onConfirmed = new UnityEvent();

    private bool pointerHeld;
    private float progress;
    private bool fired;

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetHold();
    }

    public void ResetHold()
    {
        pointerHeld = false;
        progress = 0f;
        fired = false;
        UpdateFill();
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        if (eventData.button == PointerEventData.InputButton.Left && IsInteractable())
        {
            pointerHeld = true;
        }
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        pointerHeld = false;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        pointerHeld = false;
    }

    private void Update()
    {
        bool held = IsInteractable() && (pointerHeld || SubmitHeldWhileSelected());
        if (held && !fired)
        {
            if (progress <= 0f)
            {
                holdStartFeedback?.PlayFeedbacks();
            }
            progress += Time.unscaledDeltaTime / Mathf.Max(0.01f, holdDuration);
            if (progress >= 1f)
            {
                progress = 1f;
                fired = true;
                pointerHeld = false;
                confirmFeedback?.PlayFeedbacks();
                onConfirmed.Invoke();
            }
        }
        else if (!held)
        {
            if (!fired)
            {
                progress = Mathf.Max(0f, progress - Time.unscaledDeltaTime * drainSpeed);
            }
            // Releasing after a completed hold re-arms, so a dialog that stays open can be used again.
            fired = fired && (pointerHeld || SubmitHeldWhileSelected());
            if (!fired && progress >= 1f)
            {
                progress = 0f;
            }
        }
        UpdateFill();
    }

    private bool SubmitHeldWhileSelected()
    {
        EventSystem es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject != gameObject)
        {
            return false;
        }
        InputSystemUIInputModule module = es.currentInputModule as InputSystemUIInputModule;
        return module != null && module.submit != null && module.submit.action != null && module.submit.action.IsPressed();
    }

    private void UpdateFill()
    {
        if (fill != null)
        {
            fill.fillAmount = progress;
        }
    }
}
