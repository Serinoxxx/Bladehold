using UnityEngine;

/// <summary>
///     The visual for one thrown axe: a spinning prop that flies from the hand to the throw line's
///     end point (the damage already landed hitscan-style — the <see cref="BowTracer" /> convention),
///     lingers a beat at the impact, then destroys itself. <see cref="PlayerThrownAxe" /> instantiates
///     one per throw and calls <see cref="Show" />; the prefab owns all the looks (mesh, trail).
/// </summary>
public class AxeProjectileVisual : MonoBehaviour
{
    [Tooltip("Metres per second the axe flies from the hand to the end point. 0 = it appears at the end point instantly.")]
    [SerializeField] private float travelSpeed = 30f;
    [Tooltip("Local axis the axe tumbles around in flight.")]
    [SerializeField] private Vector3 spinAxis = Vector3.right;
    [Tooltip("Tumble speed in degrees per second.")]
    [SerializeField] private float spinDegreesPerSecond = 1080f;
    [Tooltip("Seconds the axe stays visible at the impact point before despawning.")]
    [SerializeField] private float lingerSeconds = 0.15f;

    private Vector3 from;
    private Vector3 direction;
    private float distance;
    private float travelTime;
    private float shownTime;
    private bool shown;

    /// <summary>Launches the prop between two world points and starts the travel+linger countdown.</summary>
    public void Show(Vector3 from, Vector3 to)
    {
        this.from = from;
        Vector3 delta = to - from;
        distance = delta.magnitude;
        direction = distance > 0.0001f ? delta / distance : Vector3.forward;
        travelTime = travelSpeed > 0f ? distance / travelSpeed : 0f;
        shownTime = Time.time;
        shown = true;

        transform.position = from;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        Destroy(gameObject, travelTime + lingerSeconds);
    }

    private void Update()
    {
        if (!shown)
        {
            return;
        }

        float flown = travelSpeed > 0f ? Mathf.Min(travelSpeed * (Time.time - shownTime), distance) : distance;
        transform.position = from + direction * flown;
        transform.Rotate(spinAxis, spinDegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
