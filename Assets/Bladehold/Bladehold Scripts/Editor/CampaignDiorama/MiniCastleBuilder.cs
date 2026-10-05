using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Builds the board-game-sized castle for one campaign node as flat-shaded vertex-coloured geometry
///     (<see cref="DioramaMesh" />), in castle space: origin on the ground at the centre, front facing -Z
///     (towards the camera). Which castle depends on the node type; its colours and roof shape come from
///     the node's biome look. Banner cloths and lights are returned as mount points for the builder to
///     place as separate objects (banners sway and get recoloured at runtime).
/// </summary>
public static class MiniCastleBuilder
{
    public enum Kind
    {
        Fortress,
        Stronghold,
        Outpost,
        Camp,
        GrandKeep,
        NecroSpire,
        Palace
    }

    public struct LightMount
    {
        public Vector3 position;
        public Color color;
        public float intensity;
        public float range;
    }

    public class Result
    {
        public readonly List<Vector3> bannerMounts = new List<Vector3>();
        public readonly List<float> bannerSizes = new List<float>();
        public readonly List<LightMount> lights = new List<LightMount>();
        public float topHeight;
        public float frontExtent = 1.1f;
        public float footprintRadius = 1.2f;
    }

    private static readonly Color Dark = new Color(0.1f, 0.08f, 0.07f);
    private static readonly Color Wood = new Color(0.45f, 0.31f, 0.19f);
    private static readonly Color WoodDark = new Color(0.3f, 0.2f, 0.13f);
    private static readonly Color Pole = new Color(0.32f, 0.24f, 0.17f);

    public static Kind KindFor(CampaignNodeSO node)
    {
        switch (node.nodeType)
        {
            case CampaignNodeType.FishingPond: return Kind.Outpost;
            case CampaignNodeType.RestArea: return Kind.Camp;
            case CampaignNodeType.PreBoss: return Kind.GrandKeep;
            case CampaignNodeType.NecromancerEncounter:
            case CampaignNodeType.NecromancerBoss: return Kind.NecroSpire;
            case CampaignNodeType.PrincessBoss: return Kind.Palace;
            default: return node.HasCaptain ? Kind.Stronghold : Kind.Fortress;
        }
    }

    public static Result Build(DioramaMesh m, Kind kind, CampaignDioramaThemeSO.BiomeLook look, System.Random rng)
    {
        Result r = new Result();
        switch (kind)
        {
            case Kind.Outpost: Outpost(m, look, r); break;
            case Kind.Camp: Camp(m, look, r, rng); break;
            case Kind.GrandKeep: Fortress(m, look, r, 1.18f, true, true); break;
            case Kind.NecroSpire: NecroSpire(m, look, r); break;
            case Kind.Palace: Palace(m, look, r); break;
            case Kind.Stronghold: Fortress(m, look, r, 1.05f, true, false); break;
            default: Fortress(m, look, r, 1f, false, false); break;
        }
        return r;
    }

    // ───────────────────────────────────────────── shared parts

    private static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    private static Color Emissive(Color c, float strength = 0.15f) => new Color(c.r, c.g, c.b, strength);

    private static void Pad(DioramaMesh m, float radius, Color color, float height = 0.07f)
    {
        m.Prism(Vector3.zero + Vector3.down * 0.15f, radius, 0.15f + height, 10, color, true, radius * 0.97f, Mathf.PI / 10f);
    }

    private static void Roof(DioramaMesh m, Vector3 at, float radius, CampaignDioramaThemeSO.BiomeLook look, float coneHeight, int sides = 8)
    {
        switch (look.roofShape)
        {
            case CampaignDioramaThemeSO.RoofShape.Dome:
                m.Prism(at, radius * 1.05f, 0.03f, sides, look.trim);
                m.Dome(at + Vector3.up * 0.03f, radius * 1.0f, radius * 0.95f, sides, 3, look.roof);
                m.Prism(at + Vector3.up * (0.03f + radius * 0.95f), 0.012f, 0.1f, 4, look.trim);
                break;
            case CampaignDioramaThemeSO.RoofShape.Battlement:
                Merlons(m, at, radius, look.wall, sides);
                break;
            default:
                m.Prism(at, radius * 1.08f, 0.035f, sides, look.trim);
                m.Cone(at + Vector3.up * 0.035f, radius * 1.22f, coneHeight, sides, look.roof);
                break;
        }
    }

    private static void Merlons(DioramaMesh m, Vector3 at, float radius, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / count;
            Vector3 p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.9f;
            Matrix4x4 keep = m.Matrix;
            m.Matrix = keep * Matrix4x4.TRS(p, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), Vector3.one);
            m.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.06f, 0.08f, radius * 0.55f), color);
            m.Matrix = keep;
        }
    }

    /// <summary>A round tower with windows and a roof; returns the height of its tip.</summary>
    private static float Tower(DioramaMesh m, Vector3 at, float radius, float height, CampaignDioramaThemeSO.BiomeLook look, float coneHeight, int windowRows = 1)
    {
        m.Prism(at, radius * 1.12f, 0.08f, 8, Shade(look.wall, 0.82f));
        m.Prism(at, radius, height, 8, look.wall, false);
        for (int row = 0; row < windowRows; row++)
        {
            float y = height * (0.55f + 0.22f * row);
            for (int i = 0; i < 3; i++)
            {
                float a = -Mathf.PI * 0.5f + (i - 1) * 0.9f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Window(m, at + dir * radius * 0.97f + Vector3.up * y, dir, look.glow, 0.045f, 0.075f);
            }
        }
        Vector3 top = at + Vector3.up * height;
        Roof(m, top, radius, look, coneHeight);
        switch (look.roofShape)
        {
            case CampaignDioramaThemeSO.RoofShape.Dome: return height + radius + 0.1f;
            case CampaignDioramaThemeSO.RoofShape.Battlement: return height + 0.08f;
            default: return height + coneHeight + 0.035f;
        }
    }

    private static void Window(DioramaMesh m, Vector3 at, Vector3 outward, Color glow, float w, float h)
    {
        Matrix4x4 keep = m.Matrix;
        m.Matrix = keep * Matrix4x4.TRS(at, Quaternion.LookRotation(-outward, Vector3.up), Vector3.one);
        m.Box(Vector3.zero, new Vector3(w, h, 0.03f), Emissive(glow, 0.1f));
        m.Matrix = keep;
    }

    private static void Wall(DioramaMesh m, Vector3 a, Vector3 b, float height, float thickness, Color color, bool merlons = true)
    {
        Vector3 mid = (a + b) * 0.5f;
        Vector3 d = b - a;
        float len = d.magnitude;
        Matrix4x4 keep = m.Matrix;
        m.Matrix = keep * Matrix4x4.TRS(mid, Quaternion.LookRotation(d.normalized, Vector3.up), Vector3.one);
        m.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(thickness, height, len), color);
        if (merlons)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.11f));
            for (int i = 0; i < n; i += 2)
            {
                float z = -len * 0.5f + (i + 0.5f) * len / n;
                m.Box(new Vector3(0f, height + 0.035f, z), new Vector3(thickness + 0.015f, 0.07f, len / n * 0.9f), color);
            }
        }
        m.Matrix = keep;
    }

    private static void Banner(Result r, DioramaMesh m, Vector3 top, float poleHeight, float size)
    {
        m.Box(top + Vector3.up * poleHeight * 0.5f, new Vector3(0.022f, poleHeight, 0.022f), Pole);
        m.Box(top + Vector3.up * (poleHeight + 0.015f), new Vector3(0.04f, 0.03f, 0.04f), new Color(0.85f, 0.68f, 0.3f));
        r.bannerMounts.Add(top + Vector3.up * (poleHeight - 0.02f));
        r.bannerSizes.Add(size);
    }

    private static void Gate(DioramaMesh m, Vector3 frontCenter, float width, float height, CampaignDioramaThemeSO.BiomeLook look)
    {
        m.Box(frontCenter + new Vector3(0f, height * 0.5f, -0.02f), new Vector3(width, height, 0.06f), Dark);
        m.Box(frontCenter + new Vector3(0f, height + 0.02f, -0.03f), new Vector3(width + 0.08f, 0.05f, 0.08f), look.trim);
    }

    // ───────────────────────────────────────────── castles

    private static void Fortress(DioramaMesh m, CampaignDioramaThemeSO.BiomeLook look, Result r, float scale, bool captain, bool grand)
    {
        Matrix4x4 root = m.Matrix;
        m.Matrix = root * Matrix4x4.Scale(Vector3.one * scale);

        Pad(m, 1.12f, Shade(look.wall, 0.62f));
        const float y0 = 0.07f;
        float half = 0.72f;
        float wallH = 0.4f;
        Color wallCol = look.wall;

        Vector3[] corners =
        {
            new Vector3(-half, y0, -half), new Vector3(half, y0, -half),
            new Vector3(half, y0, half), new Vector3(-half, y0, half)
        };
        for (int i = 0; i < 4; i++)
        {
            Wall(m, corners[i], corners[(i + 1) % 4], wallH, 0.12f, wallCol);
        }

        // Courtyard floor inside the walls
        m.Box(new Vector3(0f, y0 + 0.01f, 0f), new Vector3(half * 2f - 0.1f, 0.02f, half * 2f - 0.1f), Shade(look.road, 0.9f));

        // Gatehouse on the front wall
        Gate(m, new Vector3(0f, y0, -half - 0.07f), 0.22f, 0.27f, look);
        float gateTowerTop = 0f;
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            gateTowerTop = Tower(m, new Vector3(x, y0, -half - 0.04f), 0.11f, 0.56f, look, 0.24f);
        }

        // Corner towers
        float cornerTop = 0f;
        float towerH = grand ? 0.88f : 0.74f;
        for (int i = 0; i < 4; i++)
        {
            cornerTop = Tower(m, corners[i], 0.2f, towerH, look, 0.42f, grand ? 2 : 1);
        }

        // Keep
        float keepH = grand ? 1.25f : captain ? 1.05f : 0.92f;
        Vector3 keepC = new Vector3(0f, y0, 0.12f);
        Vector2 keepSize = grand ? new Vector2(0.78f, 0.66f) : new Vector2(0.66f, 0.56f);
        m.Box(keepC + Vector3.up * keepH * 0.5f, new Vector3(keepSize.x, keepH, keepSize.y), Shade(wallCol, 1.04f));
        m.Box(keepC + Vector3.up * (keepH + 0.02f), new Vector3(keepSize.x + 0.06f, 0.04f, keepSize.y + 0.06f), look.trim);
        for (int row = 0; row < (grand ? 3 : 2); row++)
        {
            float y = keepH * (0.4f + 0.22f * row);
            for (int i = -1; i <= 1; i++)
            {
                Window(m, keepC + new Vector3(i * keepSize.x * 0.3f, y, -keepSize.y * 0.5f - 0.005f), Vector3.back, look.glow, 0.06f, 0.1f);
            }
        }
        Vector3 keepTop = keepC + Vector3.up * (keepH + 0.04f);
        float top;
        switch (look.roofShape)
        {
            case CampaignDioramaThemeSO.RoofShape.Dome:
                m.Dome(keepTop, keepSize.y * 0.55f, keepSize.y * 0.55f, 10, 3, look.roof);
                top = keepTop.y + keepSize.y * 0.55f;
                break;
            case CampaignDioramaThemeSO.RoofShape.Battlement:
                Merlons(m, keepTop, keepSize.x * 0.62f, wallCol, 12);
                top = keepTop.y + 0.08f;
                break;
            default:
                m.Pyramid(keepTop, keepSize + new Vector2(0.1f, 0.1f), 0.5f, look.roof);
                top = keepTop.y + 0.5f;
                break;
        }

        if (captain || grand)
        {
            // A taller central spire marks a captain's or the elite horde's seat.
            float spireH = grand ? 0.75f : 0.55f;
            Vector3 spireBase = new Vector3(0f, keepTop.y - 0.02f, keepC.z);
            float spireTop = Tower(m, spireBase, grand ? 0.17f : 0.15f, spireH, look, 0.5f);
            top = spireBase.y + (spireTop);
            if (grand)
            {
                foreach (Vector2 c in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
                {
                    Tower(m, new Vector3(c.x * keepSize.x * 0.5f, keepTop.y - 0.02f, keepC.z + c.y * keepSize.y * 0.5f), 0.075f, 0.32f, look, 0.3f);
                }
            }
            Banner(r, m, new Vector3(0f, top, keepC.z), 0.34f, grand ? 1.5f : 1.3f);
            top += 0.34f;
        }
        else
        {
            Banner(r, m, new Vector3(0f, top - 0.02f, keepC.z), 0.3f, 1.1f);
            top += 0.3f;
        }

        // Smaller banners on the two back towers
        Banner(r, m, new Vector3(-half, y0 + cornerTop - 0.02f, half), 0.18f, 0.75f);
        Banner(r, m, new Vector3(half, y0 + cornerTop - 0.02f, half), 0.18f, 0.75f);

        m.Matrix = root;
        r.topHeight = top * scale;
        r.frontExtent = 1.0f * scale;
        r.footprintRadius = 1.15f * scale;
        ScaleMounts(r, scale);
    }

    private static void ScaleMounts(Result r, float scale)
    {
        for (int i = 0; i < r.bannerMounts.Count; i++) r.bannerMounts[i] *= scale;
        for (int i = 0; i < r.bannerSizes.Count; i++) r.bannerSizes[i] *= scale;
        for (int i = 0; i < r.lights.Count; i++)
        {
            LightMount l = r.lights[i];
            l.position *= scale;
            r.lights[i] = l;
        }
    }

    private static void Outpost(DioramaMesh m, CampaignDioramaThemeSO.BiomeLook look, Result r)
    {
        Pad(m, 0.78f, Shade(look.wall, 0.62f));
        const float y0 = 0.07f;

        // Lakeside watchtower with a fisher's cottage and a jetty running out into the water (+X).
        float towerTop = Tower(m, new Vector3(-0.22f, y0, 0.15f), 0.27f, 1.0f, look, 0.5f, 2);
        Vector3 cottage = new Vector3(0.3f, y0, -0.12f);
        m.Box(cottage + Vector3.up * 0.16f, new Vector3(0.46f, 0.32f, 0.34f), Shade(look.wall, 1.05f));
        m.Gable(cottage + Vector3.up * 0.32f, new Vector2(0.54f, 0.42f), 0.24f, look.roof, Shade(look.wall, 0.95f));
        Window(m, cottage + new Vector3(-0.1f, 0.15f, -0.175f), Vector3.back, look.glow, 0.06f, 0.08f);
        m.Box(cottage + new Vector3(0.1f, 0.1f, -0.175f), new Vector3(0.08f, 0.2f, 0.02f), WoodDark);
        m.Box(cottage + new Vector3(0.15f, 0.4f, 0.05f), new Vector3(0.06f, 0.14f, 0.06f), Shade(look.wall, 0.7f)); // chimney

        Banner(r, m, new Vector3(-0.22f, y0 + towerTop - 0.02f, 0.15f), 0.26f, 0.95f);
        r.topHeight = y0 + towerTop + 0.26f;
        r.frontExtent = 0.75f;
        r.footprintRadius = 0.85f;
    }

    private static void Camp(DioramaMesh m, CampaignDioramaThemeSO.BiomeLook look, Result r, System.Random rng)
    {
        Pad(m, 0.95f, Shade(look.road, 0.85f), 0.04f);
        const float y0 = 0.04f;

        // Palisade ring with a gap at the front for the gate.
        int stakes = 44;
        for (int i = 0; i < stakes; i++)
        {
            float a = i * Mathf.PI * 2f / stakes;
            float deg = a * Mathf.Rad2Deg;
            if (deg > 255f && deg < 285f) continue;
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.86f + Vector3.up * y0;
            float h = 0.26f + (float)rng.NextDouble() * 0.06f;
            m.Prism(p, 0.035f, h, 5, i % 2 == 0 ? Wood : Shade(Wood, 0.88f), false);
            m.Cone(p + Vector3.up * h, 0.035f, 0.06f, 5, Shade(Wood, 0.8f));
        }
        // Gate posts
        foreach (float a in new[] { 252f, 288f })
        {
            float rad = a * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 0.86f + Vector3.up * y0;
            m.Prism(p, 0.05f, 0.42f, 6, WoodDark);
        }

        // Tents
        Color[] canvas = { new Color(0.86f, 0.8f, 0.66f), look.roof, new Color(0.72f, 0.3f, 0.22f) };
        Vector3[] tents = { new Vector3(-0.42f, y0, 0.25f), new Vector3(0.4f, y0, 0.3f), new Vector3(-0.05f, y0, 0.5f) };
        for (int i = 0; i < tents.Length; i++)
        {
            m.Cone(tents[i], 0.24f, 0.36f, 6, canvas[i % canvas.Length], i * 0.4f);
            m.Prism(tents[i] + Vector3.up * 0.36f, 0.012f, 0.08f, 4, WoodDark);
        }

        // Campfire in the middle: logs, stones and a glowing flame.
        Vector3 fire = new Vector3(0.05f, y0, -0.15f);
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f;
            m.Box(fire + new Vector3(Mathf.Cos(a) * 0.12f, 0.02f, Mathf.Sin(a) * 0.12f), new Vector3(0.05f, 0.04f, 0.05f), new Color(0.45f, 0.43f, 0.4f));
        }
        Matrix4x4 keep = m.Matrix;
        m.Matrix = keep * Matrix4x4.TRS(fire + Vector3.up * 0.03f, Quaternion.Euler(0f, 35f, 0f), Vector3.one);
        m.Box(Vector3.zero, new Vector3(0.18f, 0.035f, 0.035f), WoodDark);
        m.Matrix = keep * Matrix4x4.TRS(fire + Vector3.up * 0.03f, Quaternion.Euler(0f, -40f, 0f), Vector3.one);
        m.Box(Vector3.zero, new Vector3(0.18f, 0.035f, 0.035f), WoodDark);
        m.Matrix = keep;
        m.Cone(fire + Vector3.up * 0.04f, 0.06f, 0.16f, 5, new Color(1f, 0.55f, 0.15f, 0f));
        m.Cone(fire + Vector3.up * 0.04f, 0.035f, 0.22f, 5, new Color(1f, 0.85f, 0.4f, 0f), 0.6f);
        r.lights.Add(new LightMount { position = fire + Vector3.up * 0.3f, color = new Color(1f, 0.6f, 0.25f), intensity = 1.1f, range = 1.5f });

        // Watchtower at the back
        Vector3 wt = new Vector3(0.55f, y0, -0.2f);
        foreach (Vector2 c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
        {
            m.Box(wt + new Vector3(c.x * 0.08f, 0.3f, c.y * 0.08f), new Vector3(0.03f, 0.6f, 0.03f), WoodDark);
        }
        m.Box(wt + Vector3.up * 0.6f, new Vector3(0.24f, 0.04f, 0.24f), Wood);
        m.Box(wt + Vector3.up * 0.68f, new Vector3(0.24f, 0.08f, 0.24f), Shade(Wood, 0.85f));
        m.Pyramid(wt + Vector3.up * 0.78f, new Vector2(0.3f, 0.3f), 0.18f, look.roof);
        Banner(r, m, wt + Vector3.up * 0.96f, 0.22f, 0.85f);

        r.topHeight = 1.2f;
        r.frontExtent = 0.92f;
        r.footprintRadius = 1.0f;
    }

    private static void NecroSpire(DioramaMesh m, CampaignDioramaThemeSO.BiomeLook look, Result r)
    {
        Pad(m, 1.05f, Shade(look.wall, 0.7f));
        const float y0 = 0.07f;
        Color stone = look.wall;
        Color glow = Emissive(look.glow, 0f);

        // Tapering three-stage spire
        float y = y0;
        float[] radii = { 0.46f, 0.34f, 0.24f };
        float[] heights = { 0.55f, 0.55f, 0.6f };
        for (int s = 0; s < 3; s++)
        {
            float rTop = radii[s] * 0.85f;
            m.Prism(new Vector3(0f, y, 0f), radii[s], heights[s], 6, Shade(stone, 1f - s * 0.06f), false, rTop, Mathf.PI / 6f);
            m.Prism(new Vector3(0f, y + heights[s], 0f), rTop * 1.18f, 0.05f, 6, look.trim, true, rTop * 1.05f, Mathf.PI / 6f);
            for (int i = 0; i < 3; i++)
            {
                float a = -Mathf.PI * 0.5f + (i - 1) * 0.95f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Window(m, new Vector3(0f, y + heights[s] * 0.5f, 0f) + dir * radii[s] * 0.86f, dir, look.glow, 0.05f, 0.13f);
            }
            // Crown spikes on each stage
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI / 6f + i * Mathf.PI / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rTop * 1.05f + Vector3.up * (y + heights[s] + 0.05f);
                m.Cone(p, 0.045f, 0.16f + s * 0.03f, 4, Shade(stone, 0.8f));
            }
            y += heights[s] + 0.05f;
        }
        m.Cone(new Vector3(0f, y, 0f), 0.13f, 0.6f, 6, look.roof);
        m.Prism(new Vector3(0f, y + 0.6f, 0f), 0.05f, 0.05f, 4, glow); // glowing tip
        r.lights.Add(new LightMount { position = new Vector3(0f, y + 0.4f, 0f), color = look.glow, intensity = 2.5f, range = 3f });

        // Obelisks with glowing caps around the base
        for (int i = 0; i < 5; i++)
        {
            float a = -Mathf.PI * 0.5f + (i - 2) * 0.75f + Mathf.PI;
            if (i == 2) continue;
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.78f + Vector3.up * y0;
            m.Prism(p, 0.07f, 0.42f, 4, Shade(stone, 0.9f), false, 0.035f);
            m.Prism(p + Vector3.up * 0.42f, 0.035f, 0.05f, 4, glow);
        }
        // Dark doorway
        m.Box(new Vector3(0f, y0 + 0.16f, -0.44f), new Vector3(0.18f, 0.3f, 0.06f), Dark);
        m.Box(new Vector3(0f, y0 + 0.33f, -0.46f), new Vector3(0.26f, 0.05f, 0.06f), look.trim);

        Banner(r, m, new Vector3(0.24f, y0 + 1.15f, 0.12f), 0.24f, 0.8f);
        r.topHeight = y + 0.66f;
        r.frontExtent = 0.95f;
        r.footprintRadius = 1.1f;
    }

    private static void Palace(DioramaMesh m, CampaignDioramaThemeSO.BiomeLook look, Result r)
    {
        Pad(m, 1.12f, Shade(look.wall, 0.8f));
        const float y0 = 0.07f;
        Color wall = look.wall;

        // Low front terrace wall
        Wall(m, new Vector3(-0.8f, y0, -0.65f), new Vector3(0.8f, y0, -0.65f), 0.22f, 0.1f, wall);
        Gate(m, new Vector3(0f, y0, -0.72f), 0.2f, 0.2f, look);

        // Main hall with a drum and dome
        Vector3 hall = new Vector3(0f, y0, 0.1f);
        m.Box(hall + Vector3.up * 0.32f, new Vector3(1.0f, 0.64f, 0.7f), wall);
        m.Box(hall + Vector3.up * 0.66f, new Vector3(1.06f, 0.04f, 0.76f), look.trim);
        for (int i = -2; i <= 2; i++)
        {
            Window(m, hall + new Vector3(i * 0.18f, 0.36f, -0.355f), Vector3.back, look.glow, 0.06f, 0.16f);
        }
        m.Prism(hall + Vector3.up * 0.68f, 0.32f, 0.26f, 12, Shade(wall, 1.03f), false);
        m.Prism(hall + Vector3.up * 0.94f, 0.35f, 0.03f, 12, look.trim);
        m.Dome(hall + Vector3.up * 0.97f, 0.34f, 0.4f, 12, 4, look.trim);
        m.Prism(hall + Vector3.up * 1.37f, 0.015f, 0.18f, 4, look.trim);

        // Four slender spires
        float spireTop = 0f;
        foreach (Vector2 c in new[] { new Vector2(-0.55f, -0.25f), new Vector2(0.55f, -0.25f), new Vector2(0.62f, 0.42f), new Vector2(-0.62f, 0.42f) })
        {
            Vector3 p = new Vector3(c.x, y0, c.y + 0.1f);
            float h = c.y > 0f ? 1.15f : 0.95f;
            m.Prism(p, 0.12f, 0.08f, 8, look.trim);
            m.Prism(p, 0.1f, h, 8, wall, false);
            Window(m, p + new Vector3(0f, h * 0.7f, -0.1f), Vector3.back, look.glow, 0.035f, 0.08f);
            m.Prism(p + Vector3.up * h, 0.12f, 0.03f, 8, look.trim);
            m.Cone(p + Vector3.up * (h + 0.03f), 0.14f, 0.52f, 8, look.roof);
            spireTop = Mathf.Max(spireTop, h + 0.55f);
            if (c.y > 0f) Banner(r, m, p + Vector3.up * (h + 0.53f), 0.16f, 0.7f);
        }
        r.lights.Add(new LightMount { position = hall + new Vector3(0f, 0.5f, -0.6f), color = look.glow, intensity = 1.6f, range = 2.2f });

        Banner(r, m, hall + Vector3.up * 1.53f, 0.22f, 1.0f);
        r.topHeight = y0 + Mathf.Max(spireTop, 1.75f);
        r.frontExtent = 1.0f;
        r.footprintRadius = 1.15f;
    }

    /// <summary>A jetty along +X from the castle pad out to <paramref name="length" />, with a boat tied at the end (water is 0.1 below the pad).</summary>
    public static void Dock(DioramaMesh m, float length)
    {
        const float y = -0.06f;
        int planks = Mathf.Max(3, Mathf.RoundToInt((length - 0.6f) / 0.11f));
        for (int i = 0; i < planks; i++)
        {
            float x = 0.62f + i * 0.11f;
            m.Box(new Vector3(x, y, 0f), new Vector3(0.1f, 0.025f, 0.2f), i % 2 == 0 ? Wood : Shade(Wood, 0.9f));
        }
        float end = 0.62f + (planks - 1) * 0.11f;
        for (float x = 0.7f; x <= end + 0.01f; x += 0.35f)
        {
            m.Box(new Vector3(x, y - 0.08f, -0.11f), new Vector3(0.03f, 0.2f, 0.03f), WoodDark);
            m.Box(new Vector3(x, y - 0.08f, 0.11f), new Vector3(0.03f, 0.2f, 0.03f), WoodDark);
        }
        Matrix4x4 keep = m.Matrix;
        m.Matrix = keep * Matrix4x4.TRS(new Vector3(end + 0.05f, -0.1f, 0.25f), Quaternion.Euler(0f, 18f, 0f), Vector3.one);
        m.Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.06f, 0.13f), Wood);
        m.Box(new Vector3(0.2f, 0.035f, 0f), new Vector3(0.06f, 0.05f, 0.08f), Wood);
        m.Box(new Vector3(-0.02f, 0.07f, 0f), new Vector3(0.03f, 0.02f, 0.12f), WoodDark);
        m.Matrix = keep;
    }

    /// <summary>A swallow-tailed banner cloth, pivot at the pole edge, hanging +X. Double sided.</summary>
    public static Mesh BannerMesh(float size)
    {
        DioramaMesh m = new DioramaMesh();
        float w = 0.24f * size;
        float h = 0.15f * size;
        Vector3 a = new Vector3(0f, 0f, 0f);
        Vector3 b = new Vector3(0f, -h, 0f);
        Vector3 c = new Vector3(w, -h, 0f);
        Vector3 notch = new Vector3(w * 0.72f, -h * 0.5f, 0f);
        Vector3 d = new Vector3(w, 0f, 0f);
        Color white = Color.white;
        // Front (-Z) then back (+Z)
        m.Tri(a, d, notch, white);
        m.Tri(a, notch, b, white);
        m.Tri(b, notch, c, white);
        m.Tri(notch, d, a, white);
        m.Tri(b, notch, a, white);
        m.Tri(c, notch, b, white);
        return m.ToMesh("DioramaBanner");
    }
}
