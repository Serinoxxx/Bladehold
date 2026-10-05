using UnityEngine;

/// <summary>
///     Chunky model-railway trees for the campaign diorama, in the same flat-shaded vertex-coloured style as
///     the mini castles (Synty foliage reads as olive specks at this scale). Every tree is emitted into one
///     shared <see cref="DioramaMesh" />, so a whole forest is a single draw.
/// </summary>
public static class DioramaFlora
{
    public enum Kind
    {
        Broadleaf,
        Pine,
        SnowPine,
        Dead,
        Cactus,
        Blossom,
        Mushroom
    }

    private static Color Vary(Color c, System.Random r, float amount = 0.1f)
    {
        float k = 1f + ((float)r.NextDouble() - 0.5f) * 2f * amount;
        float hueShift = ((float)r.NextDouble() - 0.5f) * amount * 0.4f;
        return new Color(Mathf.Clamp01(c.r * k + hueShift), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k - hueShift), c.a);
    }

    /// <summary>Low-poly sphere (squashable) for canopies.</summary>
    public static void Blob(DioramaMesh m, Vector3 center, float radius, float squash, int sides, Color color)
    {
        const int rings = 4;
        for (int ring = 0; ring < rings; ring++)
        {
            float lat0 = -Mathf.PI * 0.5f + Mathf.PI * ring / rings;
            float lat1 = -Mathf.PI * 0.5f + Mathf.PI * (ring + 1) / rings;
            float r0 = Mathf.Cos(lat0) * radius, r1 = Mathf.Cos(lat1) * radius;
            float y0 = Mathf.Sin(lat0) * radius * squash, y1 = Mathf.Sin(lat1) * radius * squash;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides + ring * 0.4f;
                float a1 = (i + 1) * Mathf.PI * 2f / sides + ring * 0.4f;
                Vector3 b0 = center + new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0);
                Vector3 b1 = center + new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0);
                Vector3 t0 = center + new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1);
                Vector3 t1 = center + new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1);
                if (ring == 0) m.Tri(b0, t0, t1, color);
                else if (ring == rings - 1) m.Tri(b0, t0, b1, color);
                else m.Quad(b0, t0, t1, b1, color);
            }
        }
    }

    public static void Tree(DioramaMesh m, Kind kind, Vector3 pos, float height, Color leaf, Color leafAlt, Color trunk, System.Random r)
    {
        // Two palettes rather than a blend, so mixed groves (green and blossom, green and pink) stay crisp.
        float pick = (float)r.NextDouble() * 0.25f;
        Color l = Vary(Color.Lerp(leaf, leafAlt, r.NextDouble() < 0.5 ? pick : 1f - pick), r);
        Color t = Vary(trunk, r, 0.08f);
        float spin = (float)r.NextDouble() * Mathf.PI;
        switch (kind)
        {
            case Kind.Pine:
            case Kind.SnowPine:
            {
                m.Prism(pos - Vector3.up * 0.05f, height * 0.05f, height * 0.25f, 5, t, false);
                float y = height * 0.16f;
                float[] radii = { 0.3f, 0.24f, 0.17f };
                float[] heights = { 0.42f, 0.38f, 0.36f };
                for (int i = 0; i < 3; i++)
                {
                    Color tier = Color.Lerp(l, l * 1.12f, i * 0.4f);
                    tier.a = 1f;
                    m.Cone(pos + Vector3.up * y, height * radii[i], height * heights[i], 7, tier, spin + i);
                    y += height * heights[i] * 0.55f;
                }
                if (kind == Kind.SnowPine)
                {
                    m.Cone(pos + Vector3.up * (y + height * 0.02f), height * 0.115f, height * 0.17f, 7, new Color(0.95f, 0.97f, 1f), spin);
                }
                break;
            }
            case Kind.Dead:
            {
                m.Prism(pos - Vector3.up * 0.05f, height * 0.05f, height * 0.85f, 5, t, true, height * 0.02f);
                for (int i = 0; i < 3; i++)
                {
                    float a = spin + i * 2.1f;
                    float y = height * (0.4f + 0.15f * i);
                    Matrix4x4 keep = m.Matrix;
                    m.Matrix = keep * Matrix4x4.TRS(pos + Vector3.up * y, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 50f), Vector3.one);
                    m.Box(new Vector3(height * 0.12f, 0f, 0f), new Vector3(height * 0.26f, height * 0.025f, height * 0.025f), t);
                    m.Matrix = keep;
                }
                break;
            }
            case Kind.Cactus:
            {
                Color green = Vary(leaf, r, 0.08f);
                m.Prism(pos - Vector3.up * 0.04f, height * 0.11f, height, 6, green, true, height * 0.09f);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (r.NextDouble() < 0.25) continue;
                    float y = height * (0.35f + (float)r.NextDouble() * 0.2f);
                    Vector3 elbow = pos + new Vector3(side * height * 0.2f, y, 0f);
                    m.Box(pos + new Vector3(side * height * 0.11f, y, 0f), new Vector3(height * 0.2f, height * 0.09f, height * 0.09f), green);
                    m.Prism(elbow, height * 0.06f, height * 0.3f, 5, green);
                }
                break;
            }
            case Kind.Mushroom:
            {
                m.Prism(pos - Vector3.up * 0.04f, height * 0.12f, height * 0.6f, 6, new Color(0.9f, 0.86f, 0.74f), false, height * 0.1f);
                m.Dome(pos + Vector3.up * height * 0.55f, height * 0.42f, height * 0.38f, 8, 3, l);
                m.Cone(pos + Vector3.up * height * 0.55f, height * 0.42f, 0.001f, 8, new Color(0.85f, 0.8f, 0.68f));
                for (int i = 0; i < 4; i++)
                {
                    float a = spin + i * 1.6f;
                    m.Box(pos + new Vector3(Mathf.Cos(a) * height * 0.22f, height * 0.83f, Mathf.Sin(a) * height * 0.22f), Vector3.one * height * 0.06f, new Color(0.96f, 0.94f, 0.88f));
                }
                break;
            }
            default:
            {
                // Broadleaf / Blossom: a short trunk under two or three overlapping canopy blobs.
                m.Prism(pos - Vector3.up * 0.05f, height * 0.06f, height * 0.45f, 5, t, false, height * 0.045f);
                float rMain = height * 0.3f;
                Vector3 c = pos + Vector3.up * (height * 0.42f + rMain * 0.7f);
                Blob(m, c, rMain, 0.9f, 7, l);
                int extra = 1 + r.Next(2);
                for (int i = 0; i < extra; i++)
                {
                    float a = spin + i * 2.4f;
                    Color l2 = Vary(l, r, 0.06f);
                    Blob(m, c + new Vector3(Mathf.Cos(a) * rMain * 0.6f, -rMain * 0.25f + (float)r.NextDouble() * rMain * 0.4f, Mathf.Sin(a) * rMain * 0.6f), rMain * 0.68f, 0.9f, 6, l2);
                }
                break;
            }
        }
    }
}
