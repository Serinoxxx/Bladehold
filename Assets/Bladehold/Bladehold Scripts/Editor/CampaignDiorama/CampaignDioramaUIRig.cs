using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using K = BladeholdUIKit;

/// <summary>
///     Rewires the campaign map canvas for the 3D diorama (called by <see cref="CampaignDioramaBuilder" />,
///     idempotent): the flat background goes, soft shades keep the title and currencies readable, the node
///     labels / tier headers / location marker move onto a full-screen overlay that follows the castles, the
///     node button prefab becomes a plaque that hangs under its castle (the whole castle is its hit area), and
///     the tooltip gains a framed level-preview screenshot.
/// </summary>
public static class CampaignDioramaUIRig
{
    private const string NodeButtonPath = "Assets/Bladehold/Bladehold Prefabs/UI/CampaignNodeButton.prefab";
    private const string ShadeTexturePath = "Assets/Bladehold/Bladehold Images/UI/Campaign/DioramaShade.png";
    private const float TooltipWidth = 460f;

    public static void Rewire(CampaignDiorama diorama)
    {
        CampaignMapUI map = Object.FindFirstObjectByType<CampaignMapUI>(FindObjectsInactive.Include);
        if (map == null)
        {
            Debug.LogError("[CampaignDiorama] The campaign map scene has no CampaignMapUI.");
            return;
        }
        K.Begin(UIMenuId.CampaignMap);
        Transform canvas = map.transform;
        Sprite shade = ShadeSprite();

        Transform background = canvas.Find("BackgroundPanel");
        if (background != null) background.gameObject.SetActive(false);

        RectTransform overlay = canvas.Find("MapOverlay") as RectTransform;
        if (overlay == null) overlay = K.NewUI("MapOverlay", canvas);
        K.Stretch(overlay);
        overlay.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);

        RectTransform top = Child(overlay, "TopShade");
        K.AnchorTop(top, 0f, 300f);
        ShadeImage(top, shade, 0.9f, false);
        RectTransform bottom = Child(overlay, "BottomShade");
        K.AnchorBottom(bottom, 0f, 170f);
        ShadeImage(bottom, shade, 0.7f, true);

        RectTransform headers = FindDeep(canvas, "TierHeadersContainer");
        RectTransform nodes = FindDeep(canvas, "NodesContainer");
        RectTransform marker = FindDeep(canvas, "LocationMarker");
        if (headers == null) headers = K.NewUI("TierHeadersContainer", overlay);
        if (nodes == null) nodes = K.NewUI("NodesContainer", overlay);
        headers.SetParent(overlay, false);
        nodes.SetParent(overlay, false);
        K.AnchorTop(headers, 96f, 80f);
        headers.pivot = new Vector2(0f, 1f);
        K.Stretch(nodes);
        nodes.pivot = new Vector2(0.5f, 0.5f);
        top.SetSiblingIndex(0);
        bottom.SetSiblingIndex(1);
        headers.SetSiblingIndex(2);
        nodes.SetSiblingIndex(3);
        if (marker != null)
        {
            marker.SetParent(overlay, false);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.SetAsLastSibling();
        }

        Transform scroll = canvas.Find("MapScrollRect");
        if (scroll != null) Object.DestroyImmediate(scroll.gameObject);

        SerializedObject so = new SerializedObject(map);
        so.FindProperty("diorama").objectReferenceValue = diorama;
        so.FindProperty("nodesContainer").objectReferenceValue = nodes;
        so.FindProperty("tierHeadersContainer").objectReferenceValue = headers;
        if (marker != null) so.FindProperty("locationMarker").objectReferenceValue = marker;
        so.FindProperty("locationMarkerOffset").vector2Value = new Vector2(0f, 10f);
        so.ApplyModifiedPropertiesWithoutUndo();

        Transform tooltip = canvas.Find("CampaignTooltip");
        if (tooltip != null) AddTooltipPreview(tooltip);
        else Debug.LogError("[CampaignDiorama] CampaignTooltip not found; the level preview slot wasn't added.");

        RebuildNodeButton();
    }

    private static RectTransform Child(Transform parent, string name)
    {
        RectTransform rt = parent.Find(name) as RectTransform;
        return rt != null ? rt : K.NewUI(name, parent);
    }

    private static RectTransform FindDeep(Transform root, string name)
    {
        foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.name == name) return rt;
        }
        return null;
    }

    private static void ShadeImage(RectTransform rt, Sprite sprite, float alpha, bool flip)
    {
        Image image = rt.GetComponent<Image>();
        if (image == null) image = K.Img(rt, sprite, UIColorRole.Window, alpha);
        image.sprite = sprite;
        image.raycastTarget = false;
        K.Paint(image, UIColorRole.Window, alpha);
        rt.localScale = new Vector3(1f, flip ? -1f : 1f, 1f);
    }

    /// <summary>A vertical alpha ramp (opaque at the top), generated once.</summary>
    private static Sprite ShadeSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(ShadeTexturePath);
        if (existing != null) return existing;
        if (System.IO.File.Exists(ShadeTexturePath))
        {
            ImportAsSprite(ShadeTexturePath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(ShadeTexturePath);
        }

        string folder = System.IO.Path.GetDirectoryName(ShadeTexturePath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        }
        const int h = 128;
        Texture2D tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            float a = Mathf.Pow(t, 1.8f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        System.IO.File.WriteAllBytes(ShadeTexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ShadeTexturePath);
        ImportAsSprite(ShadeTexturePath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(ShadeTexturePath);
    }

    private static void ImportAsSprite(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static void AddTooltipPreview(Transform tooltip)
    {
        RectTransform tipRect = (RectTransform)tooltip;
        tipRect.sizeDelta = new Vector2(TooltipWidth, tipRect.sizeDelta.y);
        VerticalLayoutGroup vlg = tooltip.GetComponent<VerticalLayoutGroup>();
        float inner = TooltipWidth - (vlg != null ? vlg.padding.left + vlg.padding.right : 0f);

        RectTransform preview = tooltip.Find("Preview") as RectTransform;
        if (preview == null) preview = K.NewUI("Preview", tooltip);
        Transform frame = tooltip.Find("Frame");
        preview.SetSiblingIndex(frame != null ? frame.GetSiblingIndex() + 1 : 0);

        Image bg = preview.GetComponent<Image>();
        if (bg == null) bg = K.Img(preview, K.White, UIColorRole.Well);
        bg.raycastTarget = false;
        LayoutElement le = preview.GetComponent<LayoutElement>();
        if (le == null) le = preview.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = Mathf.Round(inner * 9f / 16f);
        le.flexibleWidth = 1f;

        RectTransform shot = Child(preview, "Shot");
        K.Stretch(shot, 3f, 3f, 3f, 3f);
        Image shotImage = shot.GetComponent<Image>();
        if (shotImage == null) shotImage = shot.gameObject.AddComponent<Image>();
        shotImage.color = Color.white;
        shotImage.raycastTarget = false;
        shotImage.preserveAspect = false;

        RectTransform shotFrame = preview.Find("ShotFrame") as RectTransform;
        if (shotFrame == null)
        {
            shotFrame = K.NewUI("ShotFrame", preview);
            K.Img(shotFrame, K.FrameSmall, UIColorRole.Frame, 0.9f, true, 3f);
        }
        K.Stretch(shotFrame, -2f, -2f, -2f, -2f);
        shotFrame.SetAsLastSibling();

        SerializedObject so = new SerializedObject(tooltip.GetComponent<CampaignTooltipUI>());
        so.FindProperty("previewContainer").objectReferenceValue = preview.gameObject;
        so.FindProperty("previewImage").objectReferenceValue = shotImage;
        so.ApplyModifiedPropertiesWithoutUndo();
        preview.gameObject.SetActive(false);
    }

    /// <summary>
    ///     The node button becomes a transparent hit area over the castle with a plaque hanging under it.
    ///     The root's pivot sits where the plaque meets the castle's front edge (the site's label anchor).
    /// </summary>
    private static void RebuildNodeButton()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(NodeButtonPath);
        try
        {
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(210f, 190f);
            rt.pivot = new Vector2(0.5f, 60f / 190f);

            Image hit = root.GetComponent<Image>();
            Sprite plaqueSprite = hit.sprite != null && hit.sprite != K.White ? hit.sprite : K.LoadSprite("Box_Background_01");
            K.Unthemed(hit);
            hit.sprite = K.White;
            hit.color = new Color(0f, 0f, 0f, 0.001f);
            hit.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;

            RectTransform plaque = root.transform.Find("Plaque") as RectTransform;
            if (plaque == null) plaque = K.NewUI("Plaque", root.transform);
            K.Place(plaque, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(200f, 58f));
            plaque.SetSiblingIndex(0);
            Image plaqueImage = plaque.GetComponent<Image>();
            if (plaqueImage == null) plaqueImage = plaque.gameObject.AddComponent<Image>();
            plaqueImage.sprite = plaqueSprite;
            plaqueImage.type = Image.Type.Sliced;
            plaqueImage.pixelsPerUnitMultiplier = 2f;
            plaqueImage.raycastTarget = false;

            foreach (string child in new[] { "BorderFrame", "ActiveGlow", "TierText", "TitleText", "CaptainText", "NodeIcon", "LockOverlay", "CompletedBadge" })
            {
                Transform t = root.transform.Find(child);
                if (t != null) t.SetParent(plaque, false);
            }

            Anchor(plaque, "TitleText", new Vector2(0.25f, 0.4f), new Vector2(0.97f, 0.96f));
            Anchor(plaque, "CaptainText", new Vector2(0.25f, 0.05f), new Vector2(0.97f, 0.44f));
            Transform icon = plaque.Find("NodeIcon");
            if (icon != null) K.Place((RectTransform)icon, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24f, 0f), new Vector2(46f, 46f));
            Transform lockIcon = plaque.Find("LockOverlay/LockIcon");
            if (lockIcon != null) K.Place((RectTransform)lockIcon, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(24f, 24f));
            Transform tick = plaque.Find("CompletedBadge");
            if (tick != null) K.Place((RectTransform)tick, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6f, -6f), new Vector2(30f, 30f));

            TMP_Text title = plaque.Find("TitleText")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.enableAutoSizing = true;
                title.fontSizeMin = 13f;
                title.fontSizeMax = 20f;
                title.textWrappingMode = TextWrappingModes.Normal;
            }
            TMP_Text captain = plaque.Find("CaptainText")?.GetComponent<TMP_Text>();
            if (captain != null)
            {
                captain.alignment = TextAlignmentOptions.MidlineLeft;
                captain.enableAutoSizing = true;
                captain.fontSizeMin = 10f;
                captain.fontSizeMax = 15f;
            }

            CampaignNodeButtonUI ui = root.GetComponent<CampaignNodeButtonUI>();
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("backgroundImage").objectReferenceValue = plaqueImage;
            so.FindProperty("rectTransform").objectReferenceValue = rt;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, NodeButtonPath, out bool ok);
            if (!ok) Debug.LogError("[CampaignDiorama] Saving CampaignNodeButton.prefab failed.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Anchor(Transform parent, string child, Vector2 min, Vector2 max)
    {
        RectTransform rt = parent.Find(child) as RectTransform;
        if (rt == null) return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
