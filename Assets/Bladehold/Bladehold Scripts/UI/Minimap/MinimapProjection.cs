using UnityEngine;

/// <summary>
///     World (XZ) ↔ minimap mapping: a square of <see cref="SizeMeters" /> centred on <see cref="Center" />,
///     turned by <see cref="YawDegrees" /> so the map's up is that world heading. Normalised map coords run
///     0..1 left→right and bottom→top, so a marker's anchor is just <see cref="WorldToMap" />.
/// </summary>
public readonly struct MinimapProjection
{
    public readonly Vector3 Center;
    public readonly float SizeMeters;
    public readonly float YawDegrees;
    private readonly Vector3 right;
    private readonly Vector3 up;

    public MinimapProjection(Vector3 center, float sizeMeters, float yawDegrees)
    {
        Center = center;
        SizeMeters = Mathf.Max(1f, sizeMeters);
        YawDegrees = yawDegrees;
        Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);
        right = yaw * Vector3.right;
        up = yaw * Vector3.forward;
    }

    public bool IsValid => SizeMeters > 1f;

    /// <summary>Map-up as a world heading (flat).</summary>
    public Vector3 Up => up;

    public Vector2 WorldToMap(Vector3 world)
    {
        Vector3 d = world - Center;
        return new Vector2(Vector3.Dot(d, right) / SizeMeters + 0.5f, Vector3.Dot(d, up) / SizeMeters + 0.5f);
    }

    /// <summary>The map-local (unscaled, metres) offset of a world point from the centre.</summary>
    public Vector2 WorldToLocalMeters(Vector3 world)
    {
        Vector3 d = world - Center;
        return new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, up));
    }

    /// <summary>UI z-rotation (degrees, counter-clockwise) that points a map icon along a world direction.</summary>
    public float HeadingToUiAngle(Vector3 worldDirection)
    {
        float worldYaw = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
        return -(worldYaw - YawDegrees);
    }
}
