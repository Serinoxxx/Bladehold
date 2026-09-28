using UnityEngine;

/// <summary>
///     An expanding ring of hex fire from one of Captain Mogra's <see cref="BoneTotem" />s. Only the thin
///     band at the ring's edge hurts, once per ring. Dash through it (the dodge's i-frames) or stay beyond
///     its reach. Drawn as a <see cref="LineRenderer" /> circle whose width is the damaging band, so what
///     you see is exactly what hits.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class HexShockwaveRing : MonoBehaviour
{
    [SerializeField] private LineRenderer line;
    [Tooltip("Circle resolution.")]
    [SerializeField] private int segments = 72;
    [Tooltip("Vertical tolerance: a player more than this far above or below the ring isn't hit.")]
    [SerializeField] private float heightTolerance = 1.5f;
    [Tooltip("Fraction of the max radius over which the ring fades out at the end.")]
    [Range(0f, 1f)] [SerializeField] private float fadeFraction = 0.25f;

    private float radius;
    private float previousRadius;
    private float speed;
    private float maxRadius;
    private float width;
    private float damage;
    private IDamageable source;
    private bool hasHit;
    private bool running;
    private Color baseStart;
    private Color baseEnd;

    private void OnValidate()
    {
        if (line == null) line = GetComponent<LineRenderer>();
    }

    private void Awake()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line == null)
        {
            Debug.LogError($"{name}: HexShockwaveRing needs a LineRenderer.", this);
            enabled = false;
            return;
        }
        line.loop = true;
        line.useWorldSpace = true;
        baseStart = line.startColor;
        baseEnd = line.endColor;
    }

    /// <summary>Starts the ring at this object's position. Call right after Instantiate.</summary>
    public void Launch(float expandSpeed, float maxRingRadius, float bandWidth, float damageValue, IDamageable owner)
    {
        speed = Mathf.Max(0.1f, expandSpeed);
        maxRadius = Mathf.Max(0.5f, maxRingRadius);
        width = Mathf.Max(0.1f, bandWidth);
        damage = damageValue;
        source = owner;
        radius = 0.3f;
        previousRadius = radius;
        running = true;
        if (line != null)
        {
            line.positionCount = segments;
            line.widthMultiplier = width;
        }
        Redraw();
    }

    private void Update()
    {
        if (!running) return;

        previousRadius = radius;
        radius += speed * Time.deltaTime;
        if (radius >= maxRadius)
        {
            running = false;
            Destroy(gameObject);
            return;
        }

        Redraw();
        if (!hasHit) TryHitPlayer();
    }

    private void TryHitPlayer()
    {
        Player player = Player.Instance;
        if (player == null || player.Health == null || player.Health.IsDead) return;
        if (!IsOnBand(player.transform.position)) return;

        hasHit = true;
        player.Health.ReceiveDamage(new Damage
        {
            value = damage,
            type = DamageType.elemental,
            unparryable = true,
            source = source,
            sourcePosition = transform.position,
        });
    }

    /// <summary>
    ///     True if the point is within the damaging band this frame. The band is swept from last frame's
    ///     radius to this one, so a slow frame can't skip the ring over the player.
    /// </summary>
    public bool IsOnBand(Vector3 worldPosition)
    {
        Vector3 delta = worldPosition - transform.position;
        if (Mathf.Abs(delta.y) > heightTolerance) return false;
        delta.y = 0f;
        float d = delta.magnitude;
        return d >= previousRadius - width * 0.5f && d <= radius + width * 0.5f;
    }

    public float CurrentRadius => radius;

    private void Redraw()
    {
        if (line == null) return;
        Vector3 centre = transform.position + Vector3.up * 0.15f;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }

        float fadeStart = maxRadius * (1f - fadeFraction);
        float alpha = radius <= fadeStart ? 1f : Mathf.Clamp01(1f - (radius - fadeStart) / Mathf.Max(0.01f, maxRadius - fadeStart));
        line.startColor = new Color(baseStart.r, baseStart.g, baseStart.b, baseStart.a * alpha);
        line.endColor = new Color(baseEnd.r, baseEnd.g, baseEnd.b, baseEnd.a * alpha);
    }
}
