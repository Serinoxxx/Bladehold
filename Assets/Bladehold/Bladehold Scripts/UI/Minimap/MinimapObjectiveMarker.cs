using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     An objective entity on the minimap: a prisoner cage, the supply cart, a battering ram, a catapult, the
///     Siegebreaker, a captain, the Golden Goblin... anything the active objective reports through
///     <see cref="ISurvivorsObjective.GetActiveWaypointTargets" /> (or, during prep, the picked card's
///     <see cref="IObjectivePreview" /> locations). Uses the same icon and tint as the HUD waypoint, on a dark
///     disc with a pulsing ring so it reads over the terrain and the enemy dots. Follows a moving target
///     (cart, ram, boss) every frame.
///     <para>
///         Built from code by <see cref="MinimapUI" /> (no prefab), created and removed as targets come and go.
///     </para>
/// </summary>
public class MinimapObjectiveMarker : MinimapMarker
{
    private const int SpriteResolution = 64;

    private static Sprite discSprite;
    private static Sprite ringSprite;

    private Image disc;
    private Image icon;
    private Image pulse;
    private Transform target;
    private Vector3 lastPosition;
    private string label;
    private bool preview;
    private Color tint = Color.white;
    private float size;

    public Transform Target => target;
    public bool IsPreview => preview;
    public override Vector3 WorldPosition
    {
        get
        {
            if (target != null) lastPosition = target.position;
            return lastPosition;
        }
    }

    /// <summary>Builds a marker of <paramref name="size" /> canvas units (expanded-map size) under <paramref name="parent" />.</summary>
    public static MinimapObjectiveMarker Create(RectTransform parent, float size)
    {
        var go = new GameObject("ObjectiveMarker", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(size, size);
        rect.pivot = new Vector2(0.5f, 0.5f);

        MinimapObjectiveMarker marker = go.AddComponent<MinimapObjectiveMarker>();
        marker.Build(rect, size);
        return marker;
    }

    private void Build(RectTransform rect, float markerSize)
    {
        size = markerSize;
        pulse = NewImage("Pulse", rect, Ring, size, false);
        disc = NewImage("Disc", rect, Disc, size, true); // the hover target
        disc.color = new Color(0.07f, 0.05f, 0.04f, 0.88f);
        icon = NewImage("Icon", rect, null, size * 0.68f, false);
        icon.preserveAspect = true;

        Image ring = NewImage("Highlight", rect, Ring, size * 1.35f, false);
        ring.color = Color.white;
        highlight = ring.gameObject;
    }

    private static Image NewImage(string name, RectTransform parent, Sprite sprite, float size, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = raycast;
        return img;
    }

    /// <summary>Points the marker at <paramref name="waypoint" />, drawn with <paramref name="iconSprite" />.</summary>
    public void Bind(ObjectiveWaypointTarget waypoint, Sprite iconSprite, bool isPreview)
    {
        target = waypoint.Transform;
        if (target != null) lastPosition = target.position;
        label = waypoint.Label;
        preview = isPreview;
        tint = waypoint.TintColor;
        tint.a = 1f;

        icon.sprite = iconSprite != null ? iconSprite : Disc;
        icon.color = tint;
        float iconSize = size * (iconSprite != null ? 0.68f : 0.4f);
        icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        pulse.color = tint;

        // Previews (where next wave's objective will be) are dimmer and still.
        float alpha = isPreview ? 0.6f : 1f;
        disc.color = new Color(disc.color.r, disc.color.g, disc.color.b, 0.88f * alpha);
        Color c = icon.color;
        c.a *= alpha;
        icon.color = c;
        pulse.enabled = !isPreview;
    }

    public override void Refresh(MinimapConfigSO config)
    {
    }

    private void Update()
    {
        if (preview || pulse == null) return;
        float t = Mathf.Repeat(Time.unscaledTime * 0.9f, 1f);
        pulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.9f, t);
        Color c = tint;
        c.a = (1f - t) * 0.9f;
        pulse.color = c;
    }

    public override void GetTooltip(out string title, out string body)
    {
        ISurvivorsObjective objective = SurvivorsObjectiveManager.Instance != null ? SurvivorsObjectiveManager.Instance.CurrentObjective : null;
        title = !string.IsNullOrEmpty(label) ? label : Loc.Get("minimap.objective", "Objective");

        if (preview)
        {
            body = Loc.Get("minimap.objective_preview_body", "Next wave's objective happens here.");
            return;
        }
        if (objective != null && objective.IsActive)
        {
            body = $"<b>{objective.Title}</b>\n{objective.ProgressText}";
            return;
        }
        body = Loc.Get("minimap.objective_body", "Objective target.");
    }

    // ---- Generated sprites (no art dependency) -----------------------------------------------------

    private static Sprite Disc => discSprite != null ? discSprite : (discSprite = MakeCircle("MinimapObjectiveDisc", 0f));
    private static Sprite Ring => ringSprite != null ? ringSprite : (ringSprite = MakeCircle("MinimapObjectiveRing", 0.78f));

    /// <summary>An anti-aliased white circle; <paramref name="innerFraction" /> above 0 hollows it into a ring.</summary>
    private static Sprite MakeCircle(string name, float innerFraction)
    {
        int n = SpriteResolution;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        float r = n * 0.5f;
        float inner = r * innerFraction;
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d);
                if (innerFraction > 0f) a = Mathf.Min(a, Mathf.Clamp01(d - inner));
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        return s;
    }
}
