using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>
///     Every enemy on the minimap in one mesh: an outlined circle per dot. A horde is hundreds of enemies,
///     so dots are batched here rather than being one UI object each.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MinimapDotsGraphic : MaskableGraphic
{
    public struct Dot
    {
        public Vector2 position; // normalised 0..1 map space
        public float size;       // diameter, canvas units
        public Color32 color;
    }

    [Tooltip("Soft round sprite used for each dot.")]
    [SerializeField] private Sprite dotSprite;

    private readonly List<Dot> dots = new List<Dot>();
    private Color32 outlineColor = new Color32(20, 10, 8, 220);
    private float outlineFraction = 0.28f;

    public override Texture mainTexture => dotSprite != null ? dotSprite.texture : s_WhiteTexture;

    public List<Dot> Dots => dots;

    public void SetOutline(Color outline, float fraction)
    {
        outlineColor = outline;
        outlineFraction = Mathf.Max(0f, fraction);
    }

    /// <summary>Call after editing <see cref="Dots" />.</summary>
    public void Rebuild()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Vector4 uv = dotSprite != null ? DataUtility.GetOuterUV(dotSprite) : new Vector4(0f, 0f, 1f, 1f);

        // Outlines first so every fill draws over every outline (touching dots stay readable).
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Dot d in dots)
            {
                Vector2 c = new Vector2(r.xMin + d.position.x * r.width, r.yMin + d.position.y * r.height);
                float half = d.size * 0.5f * (pass == 0 ? 1f + outlineFraction : 1f);
                Color32 col = pass == 0 ? outlineColor : d.color;
                if (pass == 0) col.a = (byte)(col.a * d.color.a / 255);
                int i = vh.currentVertCount;
                vh.AddVert(new Vector3(c.x - half, c.y - half), col, new Vector4(uv.x, uv.y));
                vh.AddVert(new Vector3(c.x - half, c.y + half), col, new Vector4(uv.x, uv.w));
                vh.AddVert(new Vector3(c.x + half, c.y + half), col, new Vector4(uv.z, uv.w));
                vh.AddVert(new Vector3(c.x + half, c.y - half), col, new Vector4(uv.z, uv.y));
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i + 2, i + 3, i);
            }
        }
    }
}
