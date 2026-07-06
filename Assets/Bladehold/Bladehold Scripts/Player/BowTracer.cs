using UnityEngine;

/// <summary>
///     The visual for one hitscan arrow: a <see cref="LineRenderer" /> streak from the bow to the hit
///     point that holds briefly, fades out, and destroys itself. <see cref="PlayerBow" /> instantiates
///     one per arrow (and per bounce) and calls <see cref="Show" /> — the prefab owns all the looks
///     (width, material, gradient); this script only drives positions and the fade.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BowTracer : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [Tooltip("Seconds the streak stays at full opacity before fading.")]
    [SerializeField] private float holdSeconds = 0.05f;
    [Tooltip("Seconds the streak takes to fade out after the hold.")]
    [SerializeField] private float fadeSeconds = 0.2f;

    private float shownTime;
    private Color startColor;
    private Color endColor;
    private bool shown;

    private void OnValidate()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    /// <summary>Places the streak between two world points and starts the hold+fade countdown.</summary>
    public void Show(Vector3 from, Vector3 to)
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, from);
        lineRenderer.SetPosition(1, to);

        startColor = lineRenderer.startColor;
        endColor = lineRenderer.endColor;
        shownTime = Time.time;
        shown = true;

        Destroy(gameObject, holdSeconds + fadeSeconds);
    }

    private void Update()
    {
        if (!shown)
        {
            return;
        }

        float elapsed = Time.time - shownTime - holdSeconds;
        if (elapsed <= 0f)
        {
            return;
        }

        float alpha = fadeSeconds > 0f ? Mathf.Clamp01(1f - elapsed / fadeSeconds) : 0f;
        Color start = startColor;
        Color end = endColor;
        start.a *= alpha;
        end.a *= alpha;
        lineRenderer.startColor = start;
        lineRenderer.endColor = end;
    }
}
