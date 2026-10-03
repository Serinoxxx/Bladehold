using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Hover/focus/press juice for any <see cref="Selectable" /> (button, toggle, slider, dropdown):
///     eases a target's scale up while hovered or pad-focused and squashes it while pressed, fades an
///     optional highlight graphic (glow, outline, accent bar) in on hover/focus, and plays optional
///     <see cref="MMF_Player" />s on hover and on click/submit. Runs on unscaled time so it works on
///     paused menus. Disabled (non-interactable) selectables stay still. For a plain Button that only
///     needs a click sound, <see cref="UIClickFeedback" /> is the lighter option.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UISelectableJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private Selectable selectable;
    [Tooltip("What scales. Defaults to this transform; point it at an inner visual to keep layout hit areas still.")]
    [SerializeField] private RectTransform scaleTarget;
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float pressScale = 0.96f;
    [Tooltip("Higher = snappier easing toward the target scale/alpha.")]
    [SerializeField] private float easeSpeed = 18f;

    [Tooltip("Optional graphic faded from 0 to its authored alpha while hovered/focused.")]
    [SerializeField] private Graphic highlight;

    [Tooltip("Optional: played once each time the pointer enters or pad focus arrives.")]
    [SerializeField] private MMF_Player hoverFeedback;
    [Tooltip("Optional: played on click or pad submit (leave empty on Buttons that already have UIClickFeedback).")]
    [SerializeField] private MMF_Player clickFeedback;

    private bool hovered;
    private bool focused;
    private bool pressed;
    private float highlightMaxAlpha = 1f;
    private float highlightAlpha;

    private void OnValidate()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }
        if (scaleTarget == null)
        {
            scaleTarget = transform as RectTransform;
        }
    }

    private void Awake()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }
        if (scaleTarget == null)
        {
            scaleTarget = transform as RectTransform;
        }
        if (highlight != null)
        {
            highlightMaxAlpha = highlight.color.a;
            SetHighlightAlpha(0f);
        }
    }

    private void OnDisable()
    {
        hovered = false;
        focused = false;
        pressed = false;
        highlightAlpha = 0f;
        if (scaleTarget != null)
        {
            scaleTarget.localScale = Vector3.one;
        }
        if (highlight != null)
        {
            SetHighlightAlpha(0f);
        }
    }

    private void Update()
    {
        bool live = selectable == null || selectable.IsInteractable();
        bool lit = live && (hovered || focused);
        float targetScale = !live ? 1f : pressed ? pressScale : lit ? hoverScale : 1f;
        float t = 1f - Mathf.Exp(-easeSpeed * Time.unscaledDeltaTime);

        if (scaleTarget != null)
        {
            float s = Mathf.Lerp(scaleTarget.localScale.x, targetScale, t);
            scaleTarget.localScale = new Vector3(s, s, 1f);
        }
        if (highlight != null)
        {
            highlightAlpha = Mathf.Lerp(highlightAlpha, lit ? 1f : 0f, t);
            SetHighlightAlpha(highlightAlpha);
        }
    }

    private void SetHighlightAlpha(float normalized)
    {
        Color c = highlight.color;
        c.a = highlightMaxAlpha * normalized;
        highlight.color = c;
    }

    private bool Interactable => selectable == null || selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hovered && !focused && Interactable && hoverFeedback != null)
        {
            hoverFeedback.PlayFeedbacks();
        }
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => pressed = Interactable;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    public void OnSelect(BaseEventData eventData)
    {
        // Only pad focus counts — a mouse click also selects, and hover already covers that case.
        if (!InputDeviceWatcher.GamepadActive)
        {
            return;
        }
        if (!focused && !hovered && Interactable && hoverFeedback != null)
        {
            hoverFeedback.PlayFeedbacks();
        }
        focused = true;
    }

    public void OnDeselect(BaseEventData eventData) => focused = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            PlayClick();
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClick();
        // Pad submit has no pointer-down, so give it the same squash: drop to press scale, ease back.
        if (Interactable && scaleTarget != null)
        {
            scaleTarget.localScale = new Vector3(pressScale, pressScale, 1f);
        }
    }

    private void PlayClick()
    {
        if (Interactable && clickFeedback != null)
        {
            clickFeedback.PlayFeedbacks();
        }
    }
}
