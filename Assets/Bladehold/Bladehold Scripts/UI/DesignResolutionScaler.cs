using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Lets a UI subtree authored in 1920×1080 units drop into any canvas: fills its parent while
///     scaling itself so one local unit equals one unit of a <see cref="designHeight" />-tall
///     reference. The settings panel uses it because it lives both under the 1080p pause canvas and
///     the 2160p main-menu canvas; without it, its fixed-width window would be half size on the
///     latter. Re-fits whenever the parent resizes (window resize, aspect change), in edit mode too.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class DesignResolutionScaler : MonoBehaviour
{
    [SerializeField] private float designHeight = 1080f;

    private RectTransform rectTransform;
    private Vector2 lastParentSize;
    private float lastScale;

    private void OnEnable()
    {
        rectTransform = (RectTransform)transform;
        Fit();
    }

    private void Update()
    {
        Fit();
    }

    private void Fit()
    {
        var parent = rectTransform.parent as RectTransform;
        if (parent == null || designHeight <= 0f)
        {
            return;
        }

        float scale = ReferenceHeight() / designHeight;
        Vector2 parentSize = parent.rect.size;
        if (parentSize == lastParentSize && Mathf.Approximately(scale, lastScale))
        {
            return;
        }
        lastParentSize = parentSize;
        lastScale = scale;

        rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = parentSize / scale;
        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>The root canvas's reference height when it scales with screen size; else the design height (no scaling).</summary>
    private float ReferenceHeight()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        CanvasScaler scaler = canvas != null ? canvas.rootCanvas.GetComponent<CanvasScaler>() : null;
        return scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
            ? scaler.referenceResolution.y
            : designHeight;
    }
}
