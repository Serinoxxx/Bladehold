using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Keeps the gamepad's selected control visible inside a <see cref="ScrollRect" /> (the settings
///     panel's rebind list): whenever pad navigation selects a descendant of the content that sits
///     outside the viewport, the content is nudged just far enough to bring it back in. Mouse users
///     are unaffected (they scroll by wheel/drag). Attach next to the ScrollRect.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class ScrollRectAutoScroll : MonoBehaviour
{
    [Tooltip("Extra padding (px, viewport space) kept between the selected control and the viewport edge.")]
    [SerializeField] private float margin = 12f;

    private ScrollRect scrollRect;
    private GameObject lastSelected;

    private void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    private void Update()
    {
        if (!InputDeviceWatcher.GamepadActive || EventSystem.current == null)
        {
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || selected == lastSelected)
        {
            return;
        }
        lastSelected = selected;

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
        if (content == null || !selected.transform.IsChildOf(content))
        {
            return;
        }

        // Selected rect in viewport-local space: scroll only the axis overflow, by the overflow amount.
        var target = (RectTransform)selected.transform;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
        Rect view = viewport.rect;

        // Move the content by exactly the overflow, per axis: an item sticking out the top means the
        // content must shift down (negative y) by that amount, and so on.
        Vector2 shift = Vector2.zero;
        if (scrollRect.vertical)
        {
            float overflowTop = bounds.max.y + margin - view.yMax;
            float overflowBottom = view.yMin - (bounds.min.y - margin);
            if (overflowTop > 0f) shift.y = -overflowTop;
            else if (overflowBottom > 0f) shift.y = overflowBottom;
        }
        if (scrollRect.horizontal)
        {
            float overflowRight = bounds.max.x + margin - view.xMax;
            float overflowLeft = view.xMin - (bounds.min.x - margin);
            if (overflowRight > 0f) shift.x = -overflowRight;
            else if (overflowLeft > 0f) shift.x = overflowLeft;
        }

        if (shift != Vector2.zero)
        {
            content.anchoredPosition += shift;
        }
    }
}
