using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Draws the minimap's predicted enemy routes as marching chevrons: one mitred strip per route over a
///     repeating chevron texture. The texture coordinate is the distance <b>still to go</b> to the gate, so
///     routes that merge onto the same road march in step instead of strobing over each other, and the
///     chevrons always point (and move) toward the gate.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MinimapPathGraphic : MaskableGraphic
{
    [Tooltip("Repeating chevron (pointing +U / right), wrap mode Repeat.")]
    [SerializeField] private Texture chevronTexture;

    private readonly List<Vector2[]> routes = new List<Vector2[]>();
    private float width = 20f;
    private float spacingFactor = 1.35f;
    private float phase;

    public override Texture mainTexture => chevronTexture != null ? chevronTexture : s_WhiteTexture;

    /// <summary>Replaces the routes (each point in normalised 0..1 map space, spawn first, gate last).</summary>
    public void SetRoutes(List<Vector2[]> normalisedRoutes)
    {
        routes.Clear();
        if (normalisedRoutes != null) routes.AddRange(normalisedRoutes);
        SetVerticesDirty();
    }

    /// <summary>Width in canvas units, chevron spacing as a multiple of width, and the march phase (chevrons).</summary>
    public void SetStyle(float routeWidth, float chevronSpacing, float marchPhase)
    {
        if (Mathf.Approximately(routeWidth, width) && Mathf.Approximately(chevronSpacing, spacingFactor)
            && Mathf.Approximately(marchPhase, phase)) return;
        width = Mathf.Max(1f, routeWidth);
        spacingFactor = Mathf.Max(0.2f, chevronSpacing);
        phase = marchPhase;
        SetVerticesDirty();
    }

    private readonly List<Vector2> points = new List<Vector2>();
    private readonly List<float> remaining = new List<float>();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float half = width * 0.5f;
        float tile = width * spacingFactor;
        Color32 c = color;

        foreach (Vector2[] route in routes)
        {
            if (route == null || route.Length < 2) continue;

            points.Clear();
            for (int i = 0; i < route.Length; i++)
            {
                Vector2 p = new Vector2(r.xMin + route[i].x * r.width, r.yMin + route[i].y * r.height);
                if (points.Count > 0 && (p - points[points.Count - 1]).sqrMagnitude < 0.25f) continue;
                points.Add(p);
            }
            if (points.Count < 2) continue;

            // Distance still to travel from each point (phase-aligns merging routes).
            remaining.Clear();
            for (int i = 0; i < points.Count; i++) remaining.Add(0f);
            for (int i = points.Count - 2; i >= 0; i--) remaining[i] = remaining[i + 1] + Vector2.Distance(points[i], points[i + 1]);

            int start = vh.currentVertCount;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 dirIn = i > 0 ? (points[i] - points[i - 1]).normalized : (points[1] - points[0]).normalized;
                Vector2 dirOut = i < points.Count - 1 ? (points[i + 1] - points[i]).normalized : dirIn;
                Vector2 tangent = (dirIn + dirOut);
                if (tangent.sqrMagnitude < 1e-4f) tangent = dirOut;
                tangent.Normalize();
                Vector2 miter = new Vector2(-tangent.y, tangent.x);
                Vector2 normal = new Vector2(-dirOut.y, dirOut.x);
                float miterLen = half / Mathf.Max(0.35f, Vector2.Dot(miter, normal));

                float u = -remaining[i] / tile - phase;
                vh.AddVert(points[i] + miter * miterLen, c, new Vector4(u, 1f));
                vh.AddVert(points[i] - miter * miterLen, c, new Vector4(u, 0f));
            }
            for (int i = 0; i < points.Count - 1; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 2, a + 1);
                vh.AddTriangle(a + 1, a + 2, a + 3);
            }
        }
    }
}
