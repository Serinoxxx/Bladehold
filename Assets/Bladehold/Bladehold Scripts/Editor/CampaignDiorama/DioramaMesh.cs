using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
///     Flat-shaded, vertex-coloured mesh accumulator for the campaign diorama (terrain, base, mini castles).
///     Every triangle gets its own three vertices so normals stay faceted (the Synty low-poly look). Shapes
///     are emitted through <see cref="Matrix" />, so a castle part can be built at the origin and placed by
///     setting the matrix. Colours are sRGB in, linear out; alpha below 1 marks emissive (see the shader).
/// </summary>
public class DioramaMesh
{
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Color32> colors = new List<Color32>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();
    private bool usesUv;

    public Matrix4x4 Matrix = Matrix4x4.identity;
    public int VertexCount => vertices.Count;

    // 8-bit linear colour per vertex keeps the generated mesh asset small.
    private static Color32 Linear(Color c) => new Color(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b), c.a);

    /// <summary>One triangle (clockwise seen from the front, Unity's winding).</summary>
    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, bool alreadyWorld = false)
    {
        Color32 lin = Linear(color);
        int i = vertices.Count;
        vertices.Add(alreadyWorld ? a : Matrix.MultiplyPoint3x4(a));
        vertices.Add(alreadyWorld ? b : Matrix.MultiplyPoint3x4(b));
        vertices.Add(alreadyWorld ? c : Matrix.MultiplyPoint3x4(c));
        colors.Add(lin);
        colors.Add(lin);
        colors.Add(lin);
        uvs.Add(Vector2.zero);
        uvs.Add(Vector2.zero);
        uvs.Add(Vector2.zero);
        triangles.Add(i);
        triangles.Add(i + 1);
        triangles.Add(i + 2);
    }

    /// <summary>Quad a-b-c-d in clockwise order seen from the front.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Tri(a, b, c, color);
        Tri(a, c, d, color);
    }

    /// <summary>Axis-aligned (in matrix space) box, centre and full size.</summary>
    public void Box(Vector3 center, Vector3 size, Color color, bool bottom = false)
    {
        Vector3 h = size * 0.5f;
        Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
        Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
        Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
        Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
        Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
        Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
        Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);
        Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
        Quad(p000, p010, p110, p100, color); // front (-z)
        Quad(p101, p111, p011, p001, color); // back (+z)
        Quad(p001, p011, p010, p000, color); // left (-x)
        Quad(p100, p110, p111, p101, color); // right (+x)
        Quad(p010, p011, p111, p110, color); // top
        if (bottom) Quad(p000, p100, p101, p001, color);
    }

    /// <summary>Upright n-sided prism (tower body). Top cap optional; radiusTop lets it taper.</summary>
    public void Prism(Vector3 baseCenter, float radius, float height, int sides, Color color, bool capTop = true, float radiusTop = -1f, float angleOffset = 0f)
    {
        if (radiusTop < 0f) radiusTop = radius;
        Vector3 top = baseCenter + Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = angleOffset + i * Mathf.PI * 2f / sides;
            float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 b0 = baseCenter + d0 * radius;
            Vector3 b1 = baseCenter + d1 * radius;
            Vector3 t0 = top + d0 * radiusTop;
            Vector3 t1 = top + d1 * radiusTop;
            Quad(b0, t0, t1, b1, color);
            if (capTop) Tri(top, t1, t0, color);
        }
    }

    /// <summary>n-sided cone (tower roof), optionally with an overhanging rim.</summary>
    public void Cone(Vector3 baseCenter, float radius, float height, int sides, Color color, float angleOffset = 0f)
    {
        Vector3 apex = baseCenter + Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = angleOffset + i * Mathf.PI * 2f / sides;
            float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
            Vector3 b0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 b1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            Tri(b0, apex, b1, color);
            Tri(b1, baseCenter, b0, color); // underside, visible through the overhang
        }
    }

    /// <summary>Low-poly dome: stacked rings capped by a point.</summary>
    public void Dome(Vector3 baseCenter, float radius, float height, int sides, int rings, Color color)
    {
        for (int r = 0; r < rings; r++)
        {
            float t0 = (float)r / rings;
            float t1 = (float)(r + 1) / rings;
            float r0 = radius * Mathf.Cos(t0 * Mathf.PI * 0.5f);
            float r1 = radius * Mathf.Cos(t1 * Mathf.PI * 0.5f);
            float y0 = height * Mathf.Sin(t0 * Mathf.PI * 0.5f);
            float y1 = height * Mathf.Sin(t1 * Mathf.PI * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = baseCenter + d0 * r0 + Vector3.up * y0;
                Vector3 b1 = baseCenter + d1 * r0 + Vector3.up * y0;
                Vector3 c0 = baseCenter + d0 * r1 + Vector3.up * y1;
                Vector3 c1 = baseCenter + d1 * r1 + Vector3.up * y1;
                if (r == rings - 1) Tri(b0, c0, b1, color);
                else Quad(b0, c0, c1, b1, color);
            }
        }
    }

    /// <summary>Four-sided pyramid roof over a rectangle.</summary>
    public void Pyramid(Vector3 baseCenter, Vector2 size, float height, Color color)
    {
        Vector3 h = new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);
        Vector3 a = baseCenter + new Vector3(-h.x, 0f, -h.z);
        Vector3 b = baseCenter + new Vector3(h.x, 0f, -h.z);
        Vector3 c = baseCenter + new Vector3(h.x, 0f, h.z);
        Vector3 d = baseCenter + new Vector3(-h.x, 0f, h.z);
        Vector3 apex = baseCenter + Vector3.up * height;
        Tri(a, apex, b, color);
        Tri(b, apex, c, color);
        Tri(c, apex, d, color);
        Tri(d, apex, a, color);
    }

    /// <summary>Gabled roof along X over a rectangle (house/hall).</summary>
    public void Gable(Vector3 baseCenter, Vector2 size, float height, Color roof, Color gableEnd)
    {
        Vector3 h = new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);
        Vector3 a = baseCenter + new Vector3(-h.x, 0f, -h.z);
        Vector3 b = baseCenter + new Vector3(h.x, 0f, -h.z);
        Vector3 c = baseCenter + new Vector3(h.x, 0f, h.z);
        Vector3 d = baseCenter + new Vector3(-h.x, 0f, h.z);
        Vector3 r0 = baseCenter + new Vector3(-h.x, height, 0f);
        Vector3 r1 = baseCenter + new Vector3(h.x, height, 0f);
        Quad(a, r0, r1, b, roof);
        Quad(c, r1, r0, d, roof);
        Tri(d, r0, a, gableEnd);
        Tri(b, r1, c, gableEnd);
    }

    /// <summary>A ribbon of quads (for glow overlays); uv.x = distance along, uv.y = 0..1 across.</summary>
    public void Ribbon(IList<Vector3> left, IList<Vector3> right, IList<float> along)
    {
        for (int i = 0; i < left.Count - 1; i++)
        {
            int v = vertices.Count;
            vertices.Add(left[i]);
            vertices.Add(right[i]);
            vertices.Add(right[i + 1]);
            vertices.Add(left[i + 1]);
            for (int k = 0; k < 4; k++) colors.Add(new Color32(255, 255, 255, 255));
            usesUv = true;
            uvs.Add(new Vector2(along[i], 0f));
            uvs.Add(new Vector2(along[i], 1f));
            uvs.Add(new Vector2(along[i + 1], 1f));
            uvs.Add(new Vector2(along[i + 1], 0f));
            triangles.Add(v);
            triangles.Add(v + 3);
            triangles.Add(v + 2);
            triangles.Add(v);
            triangles.Add(v + 2);
            triangles.Add(v + 1);
        }
    }

    /// <summary>A flat quad with uv -1..1 (ring glow), centred, facing up.</summary>
    public void UvDisc(Vector3 center, float radius)
    {
        int v = vertices.Count;
        vertices.Add(Matrix.MultiplyPoint3x4(center + new Vector3(-radius, 0f, -radius)));
        vertices.Add(Matrix.MultiplyPoint3x4(center + new Vector3(-radius, 0f, radius)));
        vertices.Add(Matrix.MultiplyPoint3x4(center + new Vector3(radius, 0f, radius)));
        vertices.Add(Matrix.MultiplyPoint3x4(center + new Vector3(radius, 0f, -radius)));
        for (int k = 0; k < 4; k++) colors.Add(new Color32(255, 255, 255, 255));
        usesUv = true;
        uvs.Add(new Vector2(-1f, -1f));
        uvs.Add(new Vector2(-1f, 1f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(1f, -1f));
        triangles.Add(v);
        triangles.Add(v + 1);
        triangles.Add(v + 2);
        triangles.Add(v);
        triangles.Add(v + 2);
        triangles.Add(v + 3);
    }

    public Mesh ToMesh(string name)
    {
        Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        if (usesUv) mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
