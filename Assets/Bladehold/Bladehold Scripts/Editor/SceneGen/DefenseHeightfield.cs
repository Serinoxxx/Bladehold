using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The analytic ground model behind a defense scene: a signed distance to the valley edge drives the
///     mountains, and ravines/ramps are carved on top. Every other stage (splat, scatter, bridges, pits,
///     the play-area boundary, validation) queries this same model, so they all agree on where the
///     edge, the rims and the floor are. Heights are metres relative to the battlefield (y = 0).
/// </summary>
public class DefenseHeightfield
{
    private readonly DefenseSceneSpecSO spec;
    private readonly Vector2[] noiseOffsets = new Vector2[8];
    private readonly float[] ravinePhase;

    public DefenseSceneSpecSO Spec => spec;

    public DefenseHeightfield(DefenseSceneSpecSO spec)
    {
        this.spec = spec;
        var rng = new System.Random(spec.seed);
        for (int i = 0; i < noiseOffsets.Length; i++)
            noiseOffsets[i] = new Vector2(rng.Next(0, 20000) + 0.37f, rng.Next(0, 20000) + 0.71f);
        ravinePhase = new float[spec.ravines.Count];
        for (int i = 0; i < ravinePhase.Length; i++) ravinePhase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
    }

    // ---------------------------------------------------------------- noise

    private float Perlin(float x, float z, float freq, int channel)
    {
        Vector2 o = noiseOffsets[channel];
        return Mathf.PerlinNoise(x * freq + o.x, z * freq + o.y);
    }

    /// <summary>Fractal noise in 0..1.</summary>
    public float Fbm(float x, float z, float freq, int octaves, int channel)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Perlin(x, z, freq, channel) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.03f;
        }
        return sum / norm;
    }

    /// <summary>Ridged fractal noise in 0..1 (sharp crests, for mountain silhouettes).</summary>
    private float Ridged(float x, float z, float freq, int octaves, int channel)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1f - Mathf.Abs(Perlin(x, z, freq, channel) * 2f - 1f);
            sum += n * n * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.1f;
        }
        return sum / norm;
    }

    private static float Smooth(float a, float b, float x)
    {
        if (Mathf.Approximately(a, b)) return x < a ? 0f : 1f;
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Smoother(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    // ---------------------------------------------------------------- valley shape

    /// <summary>Half width of the walkable valley at a given Z, including the edge wobble.</summary>
    public float HalfWidth(float z)
    {
        float notch = spec.gateNotchHalfWidth;
        float w;
        if (z <= -16f) w = spec.courtyardHalfWidth;
        else if (z < -4f) w = Mathf.Lerp(spec.courtyardHalfWidth, notch, Smooth(-16f, -4f, z));
        else if (z < 2f) w = notch;
        else w = Mathf.Lerp(notch, spec.fieldHalfWidth, Smooth(2f, 2f + spec.flareLength, z));

        float wobbleMask = Smooth(10f, 10f + spec.flareLength, z);
        w += spec.edgeNoise * (Fbm(0f, z, 0.018f, 3, 0) * 2f - 1f) * wobbleMask;
        return w;
    }

    /// <summary>
    ///     Signed distance to the valley edge: negative inside the play space, positive out in the rock.
    ///     A rounded box whose width varies with Z; the far end and the courtyard's back are capped.
    /// </summary>
    public float ValleyDistance(float x, float z)
    {
        float ex = Mathf.Abs(x) - HalfWidth(z);
        bool front = z > 0f;
        float ez = front ? z - spec.fieldEndZ : -spec.courtyardDepth - z;
        float r = front ? spec.cornerRadius : 12f;
        float qx = ex + r, qz = ez + r;
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qz, 0f) * Mathf.Max(qz, 0f));
        float inside = Mathf.Min(Mathf.Max(qx, qz), 0f);
        return outside + inside - r;
    }

    /// <summary>Numeric gradient of ValleyDistance: points away from the valley.</summary>
    public Vector2 ValleyOutward(float x, float z)
    {
        const float e = 0.75f;
        var g = new Vector2(ValleyDistance(x + e, z) - ValleyDistance(x - e, z),
            ValleyDistance(x, z + e) - ValleyDistance(x, z - e));
        return g.sqrMagnitude > 1e-6f ? g.normalized : Vector2.up;
    }

    // ---------------------------------------------------------------- ravines

    public float RavineCentreZ(int ravine, float x)
    {
        RavineSpec r = spec.ravines[ravine];
        return r.z + r.meanderAmplitude * Mathf.Sin(x / r.meanderWavelength * Mathf.PI * 2f + ravinePhase[ravine]);
    }

    /// <summary>Direction of the ravine centreline at x (unit XZ).</summary>
    public Vector2 RavineTangent(int ravine, float x)
    {
        float dz = RavineCentreZ(ravine, x + 0.5f) - RavineCentreZ(ravine, x - 0.5f);
        return new Vector2(1f, dz).normalized;
    }

    /// <summary>
    ///     1 inside the valley along the ravine, fading to 0 a few metres into the rock so the gorge ends
    ///     in a steep wall rather than a walkable slope.
    /// </summary>
    public float RavineExtentMask(int ravine, float x)
    {
        float d = ValleyDistance(x, RavineCentreZ(ravine, x));
        return 1f - Smooth(6f, 11f, d);
    }

    /// <summary>X span of the ravine (where its extent mask is on).</summary>
    public Vector2 RavineXSpan(int ravine)
    {
        float lo = 0f, hi = 0f;
        for (float x = 0f; x < spec.terrainSize; x += 0.5f)
        {
            if (RavineExtentMask(ravine, x) < 0.5f) break;
            hi = x;
        }
        for (float x = 0f; x > -spec.terrainSize; x -= 0.5f)
        {
            if (RavineExtentMask(ravine, x) < 0.5f) break;
            lo = x;
        }
        return new Vector2(lo, hi);
    }

    public float RampLength(RampSpec ramp, RavineSpec r) => r.depth / Mathf.Tan(ramp.slopeDegrees * Mathf.Deg2Rad);

    /// <summary>Ramp lane geometry at x: how far along the ramp (0..len) and the lane's centre Z.</summary>
    public void RampLane(int ravine, RampSpec ramp, float x, out float along, out float laneCentreZ)
    {
        RavineSpec r = spec.ravines[ravine];
        along = (x - ramp.x) * Mathf.Sign(ramp.direction == 0 ? 1 : ramp.direction);
        // The lane is cut into the gate-side (-Z) bank, its inner edge flush with the floor edge.
        laneCentreZ = RavineCentreZ(ravine, x) - r.floorWidth * 0.5f - ramp.width * 0.5f;
    }

    /// <summary>True when (x,z) is on a ravine floor or lower wall (below the rim by more than a metre).</summary>
    public bool InRavine(float x, float z, out int ravine)
    {
        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            if (Mathf.Abs(z - RavineCentreZ(i, x)) < r.topWidth * 0.5f + 0.5f && RavineExtentMask(i, x) > 0.05f)
            {
                ravine = i;
                return true;
            }
        }
        ravine = -1;
        return false;
    }

    /// <summary>Near a ravine rim: within <paramref name="margin" /> of the top edge on either side.</summary>
    public bool NearRavine(float x, float z, float margin)
    {
        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            if (Mathf.Abs(z - RavineCentreZ(i, x)) < r.topWidth * 0.5f + margin && RavineExtentMask(i, x) > 0.05f)
                return true;
        }
        return false;
    }

    public bool InRampLane(float x, float z, float margin)
    {
        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            foreach (RampSpec ramp in r.exitRamps)
            {
                RampLane(i, ramp, x, out float along, out float laneZ);
                if (along < -margin - 2f || along > RampLength(ramp, r) + margin) continue;
                if (Mathf.Abs(z - laneZ) < ramp.width * 0.5f + margin) return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- flatness masks

    /// <summary>1 where the ground must be dead flat (plots, gate apron, bridge rims), 0 where it may roll.</summary>
    public float FlatMask(float x, float z)
    {
        float m = 0f;
        foreach (Vector2 p in spec.towerPlots)
            m = Mathf.Max(m, 1f - Smooth(5f, 10f, Vector2.Distance(p, new Vector2(x, z))));
        m = Mathf.Max(m, 1f - Smooth(18f, 32f, new Vector2(x, z).magnitude));
        if (z < 4f) m = 1f;
        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            float dz = Mathf.Abs(z - RavineCentreZ(i, x)) - r.topWidth * 0.5f;
            m = Mathf.Max(m, 1f - Smooth(4f, 14f, dz));
        }
        return m;
    }

    // ---------------------------------------------------------------- height

    public float Mountains(float x, float z, float d)
    {
        if (d <= 0f) return 0f;
        float nearNotch = 1f - Smooth(6f, 30f, Mathf.Abs(z));
        float fw = Mathf.Lerp(spec.foothillWidth, spec.notchFoothillWidth, nearNotch);
        float mw = Mathf.Lerp(spec.mountainWidth, 28f, nearNotch);
        float foothill = spec.foothillHeight * Smooth(0f, fw, d);
        float rise = Mathf.Pow(Smooth(fw * 0.5f, fw + mw, d), 1.25f);
        float ridge = 0.45f + 0.55f * Ridged(x, z, 0.016f, 5, 1);
        float detail = (Fbm(x, z, 0.045f, 4, 2) - 0.5f) * 13f * Mathf.Clamp01(d / 18f);
        return foothill + spec.mountainHeight * rise * ridge + detail;
    }

    /// <summary>Final ground height in metres relative to the battlefield.</summary>
    public float Height(float x, float z)
    {
        float d = ValleyDistance(x, z);
        float roll = spec.fieldUndulation * (Fbm(x, z, 0.02f, 2, 3) * 2f - 1f) * (1f - FlatMask(x, z));
        float h = roll + Mountains(x, z, d);

        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            float extent = RavineExtentMask(i, x);
            if (extent <= 0f) continue;

            float halfTop = r.topWidth * 0.5f, halfFloor = r.floorWidth * 0.5f;
            float dz = Mathf.Abs(z - RavineCentreZ(i, x));
            float floor = -r.depth + (Fbm(x, z, 0.3f, 2, 4) - 0.5f) * 0.3f;

            if (dz < halfTop)
            {
                float t = (halfTop - dz) / Mathf.Max(0.01f, halfTop - halfFloor);
                h = Mathf.Lerp(h, floor, Smoother(t) * extent);
            }

            foreach (RampSpec ramp in r.exitRamps)
            {
                RampLane(i, ramp, x, out float along, out float laneZ);
                float len = RampLength(ramp, r);
                if (along < -1f || along > len + 1f) continue;
                float rampH = -r.depth + r.depth * Mathf.Clamp01(along / len);
                float side = Smooth(ramp.width * 0.5f - 0.2f, ramp.width * 0.5f + 0.6f, Mathf.Abs(z - laneZ));
                float carved = Mathf.Lerp(rampH, h, side);
                h = Mathf.Min(h, Mathf.Lerp(h, carved, extent));
            }
        }
        return h;
    }

    /// <summary>Road polylines painted from the spawn area over each bridge to the gate apron.</summary>
    public List<List<Vector2>> Roads()
    {
        var roads = new List<List<Vector2>>();
        float spawnZ = 0f;
        foreach (Vector2 s in spec.enemySpawns) spawnZ = Mathf.Max(spawnZ, s.y);
        if (spawnZ <= 0f) spawnZ = spec.fieldEndZ - 25f;

        for (int i = 0; i < spec.ravines.Count; i++)
        {
            RavineSpec r = spec.ravines[i];
            foreach (BridgeSpec b in r.bridges)
            {
                float zc = RavineCentreZ(i, b.x);
                float half = r.topWidth * 0.5f + 5f;
                // Side roads fold into the centre road short of the plots, so only one road meets the gate.
                var road = new List<Vector2>
                {
                    new Vector2(b.x * 0.9f, spawnZ),
                    new Vector2(b.x, zc + half + 12f),
                    new Vector2(b.x, zc + half),
                    new Vector2(b.x, zc - half),
                    new Vector2(b.x, zc - half - 6f)
                };
                if (Mathf.Abs(b.x) > 3f)
                {
                    road.Add(new Vector2(b.x * 0.55f, zc - half - 16f));
                    road.Add(new Vector2(0f, Mathf.Min(34f, zc - half - 18f)));
                }
                else road.Add(new Vector2(0f, 3f));
                roads.Add(road);
            }
        }

        // No centre bridge on the inner ravine: the side roads meet short of the plots, so run one trunk
        // road from there to the gate apron. (Layouts with a centre bridge already have it.)
        if (roads.Count > 0)
        {
            bool reachesGate = false;
            float meetZ = float.MaxValue;
            foreach (List<Vector2> r in roads)
            {
                Vector2 end = r[r.Count - 1];
                reachesGate |= end.y <= 3.01f;
                if (Mathf.Abs(end.x) < 0.01f) meetZ = Mathf.Min(meetZ, end.y);
            }
            if (!reachesGate && meetZ < float.MaxValue)
                roads.Add(new List<Vector2> { new Vector2(0f, meetZ), new Vector2(0f, 3f) });
        }
        return roads;
    }

    public static float DistanceToPolyline(Vector2 p, List<Vector2> line)
    {
        float best = float.MaxValue;
        for (int i = 0; i < line.Count - 1; i++)
        {
            Vector2 a = line[i], b = line[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }
}
