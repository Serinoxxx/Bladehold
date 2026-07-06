using UnityEngine;

/// <summary>
///     The visual for one hitscan arrow: a <see cref="LineRenderer" /> streak that flies from the bow
///     toward the hit point at <see cref="travelSpeed" /> (a fixed-length tail trailing the head, so
///     the shot reads as a projectile even though the damage already landed instantly), holds briefly
///     at the impact, fades out, and destroys itself. <see cref="PlayerBow" /> instantiates one per
///     arrow (and per bounce) and calls <see cref="Show" /> — the prefab owns all the looks (width,
///     material, gradient); this script only drives positions and the fade.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BowTracer : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [Tooltip("Metres per second the streak's head travels from the bow to the hit point. 0 = the whole line appears instantly.")]
    [SerializeField] private float travelSpeed = 90f;
    [Tooltip("Length of the visible streak trailing the head while in flight, in metres.")]
    [SerializeField] private float tailLength = 3f;
    [Tooltip("Seconds the streak stays at full opacity at the impact point before fading.")]
    [SerializeField] private float holdSeconds = 0.05f;
    [Tooltip("Seconds the streak takes to fade out after the hold.")]
    [SerializeField] private float fadeSeconds = 0.2f;

    private Vector3 from;
    private Vector3 direction;
    private float distance;
    private float travelTime;
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

    /// <summary>Launches the streak between two world points and starts the travel+hold+fade countdown.</summary>
    public void Show(Vector3 from, Vector3 to)
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        this.from = from;
        Vector3 delta = to - from;
        distance = delta.magnitude;
        direction = distance > 0.0001f ? delta / distance : Vector3.forward;
        travelTime = travelSpeed > 0f ? distance / travelSpeed : 0f;

        startColor = lineRenderer.startColor;
        endColor = lineRenderer.endColor;
        shownTime = Time.time;
        shown = true;

        lineRenderer.positionCount = 2;
        UpdateStreak(0f);

        Destroy(gameObject, travelTime + holdSeconds + fadeSeconds);
    }

    private void Update()
    {
        if (!shown)
        {
            return;
        }

        float sinceShown = Time.time - shownTime;
        UpdateStreak(sinceShown);

        float elapsed = sinceShown - travelTime - holdSeconds;
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

    /// <summary>Positions the streak's tail→head span for how far the shot has flown by now.</summary>
    private void UpdateStreak(float sinceShown)
    {
        float head = travelSpeed > 0f ? Mathf.Min(travelSpeed * sinceShown, distance) : distance;
        // Instant mode shows the whole flight path (the pre-velocity look); flight mode trails a tail.
        float tail = travelSpeed > 0f ? Mathf.Max(head - tailLength, 0f) : 0f;
        lineRenderer.SetPosition(0, from + direction * tail);
        lineRenderer.SetPosition(1, from + direction * head);
    }
}
