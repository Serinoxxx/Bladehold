using System.Collections.Generic;
using UnityEngine;

public enum HexRunePattern
{
    /// <summary>A filled inner disc, then an outer ring. Safe: the gap between them, or well outside.</summary>
    Rings,
    /// <summary>Four arms spreading out from the target (+ or ×). Safe: between the arms.</summary>
    Cross,
    /// <summary>A checkerboard in two halves, one after the other. Safe: the grid corners, or swap squares.</summary>
    Checkerboard,
    /// <summary>Three arms spiralling out from the target. Safe: step between the arms.</summary>
    Spiral,
}

/// <summary>One rune of a pattern: its ground offset from the pattern centre and when it appears.</summary>
public struct HexRuneSpot
{
    /// <summary>XZ offset from the pattern centre, in metres (y is always 0).</summary>
    public Vector3 offset;
    /// <summary>Seconds after the cast that this rune's telegraph appears. It erupts a full telegraph later.</summary>
    public float appearAt;
}

/// <summary>
///     Captain Mogra's rune layouts, as pure code so the benchmark can check each one leaves somewhere
///     safe to stand. Every rune gets the full telegraph from the moment it appears. Sequenced patterns
///     stagger when runes appear, and never shorten how long a rune glows.
/// </summary>
public static class HexRunePatterns
{
    /// <summary>Inner disc radius of the Rings pattern (ring of 6 runes plus the centre).</summary>
    public const float RingsInnerRadius = 2.4f;
    /// <summary>Outer ring radius of the Rings pattern.</summary>
    public const float RingsOuterRadius = 7.5f;
    public const float CrossArmLength = 9.6f;
    public const float CrossSpacing = 2.4f;
    public const float CheckerSpacing = 2.8f;
    public const int CheckerHalfCells = 2;

    /// <summary>
    ///     Builds a pattern. <paramref name="forward" /> orients it (the caster's aim), <paramref name="stepSeconds" />
    ///     spaces sequenced steps, <paramref name="telegraphSeconds" /> is how long each rune glows (the
    ///     checkerboard's second half appears as the first half erupts), and <paramref name="variant" /> picks
    ///     a sub-variant (+/× cross, spiral handedness).
    /// </summary>
    public static List<HexRuneSpot> Build(HexRunePattern pattern, Vector3 forward, float runeRadius, float stepSeconds, float telegraphSeconds, int variant)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);

        List<HexRuneSpot> spots = new List<HexRuneSpot>();
        switch (pattern)
        {
            case HexRunePattern.Rings:
                BuildRings(spots, forward, right, runeRadius, stepSeconds);
                break;
            case HexRunePattern.Cross:
                BuildCross(spots, forward, right, stepSeconds, variant);
                break;
            case HexRunePattern.Checkerboard:
                BuildCheckerboard(spots, forward, right, telegraphSeconds);
                break;
            case HexRunePattern.Spiral:
                BuildSpiral(spots, forward, right, stepSeconds, variant);
                break;
        }
        return spots;
    }

    private static void BuildRings(List<HexRuneSpot> spots, Vector3 forward, Vector3 right, float runeRadius, float stepSeconds)
    {
        spots.Add(new HexRuneSpot { offset = Vector3.zero, appearAt = 0f });
        AddCircle(spots, forward, right, RingsInnerRadius, 6, 0f, 0f);
        // Enough runes that neighbours overlap, so the outer ring is a solid band, not a picket fence.
        int outerCount = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * RingsOuterRadius / (runeRadius * 1.6f)));
        // Outer ring after the inner disc: the timing tells you which way to go.
        AddCircle(spots, forward, right, RingsOuterRadius, outerCount, 15f, stepSeconds * 2f);
    }

    private static void BuildCross(List<HexRuneSpot> spots, Vector3 forward, Vector3 right, float stepSeconds, int variant)
    {
        float baseAngle = (variant & 1) == 0 ? 0f : 45f;
        spots.Add(new HexRuneSpot { offset = Vector3.zero, appearAt = 0f });
        int steps = Mathf.RoundToInt(CrossArmLength / CrossSpacing);
        for (int arm = 0; arm < 4; arm++)
        {
            Vector3 dir = Rotate(forward, right, baseAngle + arm * 90f);
            for (int i = 1; i <= steps; i++)
            {
                spots.Add(new HexRuneSpot { offset = dir * (i * CrossSpacing), appearAt = i * stepSeconds * 0.5f });
            }
        }
    }

    private static void BuildCheckerboard(List<HexRuneSpot> spots, Vector3 forward, Vector3 right, float telegraphSeconds)
    {
        for (int x = -CheckerHalfCells; x <= CheckerHalfCells; x++)
        {
            for (int z = -CheckerHalfCells; z <= CheckerHalfCells; z++)
            {
                bool firstHalf = ((x + z) & 1) == 0;
                spots.Add(new HexRuneSpot
                {
                    offset = right * (x * CheckerSpacing) + forward * (z * CheckerSpacing),
                    // The second half lights up as the first erupts: step onto a spent square.
                    appearAt = firstHalf ? 0f : telegraphSeconds,
                });
            }
        }
    }

    private static void BuildSpiral(List<HexRuneSpot> spots, Vector3 forward, Vector3 right, float stepSeconds, int variant)
    {
        float hand = (variant & 1) == 0 ? 1f : -1f;
        const int arms = 3;
        const int perArm = 8;
        for (int arm = 0; arm < arms; arm++)
        {
            for (int k = 0; k < perArm; k++)
            {
                float angle = hand * (arm * (360f / arms) + k * 38f);
                float radius = 1.2f + k * 1.15f;
                spots.Add(new HexRuneSpot { offset = Rotate(forward, right, angle) * radius, appearAt = k * stepSeconds * 0.5f });
            }
        }
    }

    private static void AddCircle(List<HexRuneSpot> spots, Vector3 forward, Vector3 right, float radius, int count, float angleOffset, float appearAt)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = angleOffset + i * (360f / count);
            spots.Add(new HexRuneSpot { offset = Rotate(forward, right, angle) * radius, appearAt = appearAt });
        }
    }

    /// <summary>The XZ direction <paramref name="degrees" /> clockwise from forward (towards right).</summary>
    private static Vector3 Rotate(Vector3 forward, Vector3 right, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return forward * Mathf.Cos(rad) + right * Mathf.Sin(rad);
    }

    /// <summary>True if <paramref name="point" /> (an XZ offset from the centre) is inside any rune of the pattern.</summary>
    public static bool IsHit(List<HexRuneSpot> spots, Vector3 point, float runeRadius)
    {
        point.y = 0f;
        float r2 = runeRadius * runeRadius;
        foreach (HexRuneSpot spot in spots)
        {
            if ((spot.offset - point).sqrMagnitude <= r2) return true;
        }
        return false;
    }

    /// <summary>
    ///     The nearest point to the pattern centre (sampled on a 0.25 m grid out to <paramref name="searchRadius" />)
    ///     that no rune ever touches, or null if the pattern covers everything within reach.
    /// </summary>
    public static Vector3? NearestSafePoint(List<HexRuneSpot> spots, float runeRadius, float searchRadius)
    {
        Vector3? best = null;
        float bestDist = float.MaxValue;
        const float step = 0.25f;
        for (float x = -searchRadius; x <= searchRadius; x += step)
        {
            for (float z = -searchRadius; z <= searchRadius; z += step)
            {
                Vector3 p = new Vector3(x, 0f, z);
                float d = p.magnitude;
                if (d > searchRadius || d >= bestDist) continue;
                if (IsHit(spots, p, runeRadius)) continue;
                best = p;
                bestDist = d;
            }
        }
        return best;
    }
}
