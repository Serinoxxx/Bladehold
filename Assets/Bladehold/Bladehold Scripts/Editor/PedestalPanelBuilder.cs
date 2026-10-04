using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Rebuilds the floating world-space info card of every Meta Area pedestal prefab
///     (<see cref="WeaponPedestal" />, <see cref="ArmourPedestal" />, <see cref="MountPedestal" />) in the
///     shared menu style (<see cref="BladeholdUIKit" />, themed as <see cref="UIMenuId.Pedestals" />):
///     a framed window that sizes itself to its content, with the item name, its status, a
///     description, the type's details block (stats / upgrades / ultimate / perks) and the unlock price.
///     Every pedestal type gets the same card, so the armoury reads as one set.
///
///     Keeps each pedestal's WorldUI Canvas and CanvasGroup (the proximity fade) and replaces only
///     their children, then rewires the pedestal's label fields by name. Bladehold > UI > Rebuild
///     Pedestal Panels.
/// </summary>
public static class PedestalPanelBuilder
{
    private const string PedestalFolder = "Assets/Bladehold/Bladehold Prefabs/Meta Scene";

    /// <summary>Card width in canvas units; with <see cref="CanvasScale" /> it's about 2.7 m in the world.</summary>
    private const float CardWidth = 440f;
    private const float CanvasScale = 0.0062f;
    /// <summary>The card's bottom edge sits this far (metres) below the canvas origin and it grows upward, so long text never sinks it into the plinth.</summary>
    private const float BottomBelowOrigin = 1.1f;

    [MenuItem("Bladehold/UI/Rebuild Pedestal Panels")]
    public static void RebuildAll()
    {
        BladeholdUIKit.Begin(UIMenuId.Pedestals);
        int built = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PedestalFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || FindPedestal(asset) == null)
            {
                continue;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (Rebuild(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                    Debug.Log($"[PedestalPanelBuilder] {path} saved: {ok}");
                    built++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Debug.Log($"[PedestalPanelBuilder] Rebuilt {built} pedestal panels.");
    }

    private static MonoBehaviour FindPedestal(GameObject root)
    {
        MonoBehaviour pedestal = root.GetComponent<WeaponPedestal>();
        if (pedestal == null) pedestal = root.GetComponent<ArmourPedestal>();
        if (pedestal == null) pedestal = root.GetComponent<MountPedestal>();
        return pedestal;
    }

    private static bool Rebuild(GameObject root)
    {
        MonoBehaviour pedestal = FindPedestal(root);
        var so = new SerializedObject(pedestal);
        Canvas canvas = so.FindProperty("worldCanvas").objectReferenceValue as Canvas;
        if (canvas == null)
        {
            Debug.LogError($"[PedestalPanelBuilder] {root.name} has no worldCanvas assigned; skipped.");
            return false;
        }

        BladeholdUIKit.Scope(root, UIMenuId.Pedestals);
        Transform canvasT = canvas.transform;
        for (int i = canvasT.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(canvasT.GetChild(i).gameObject);
        }
        foreach (LayoutGroup group in canvasT.GetComponents<LayoutGroup>()) Object.DestroyImmediate(group);
        foreach (ContentSizeFitter fitter in canvasT.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);
        foreach (Graphic graphic in canvasT.GetComponents<Graphic>()) Object.DestroyImmediate(graphic);

        RectTransform canvasRect = (RectTransform)canvasT;
        canvasRect.sizeDelta = new Vector2(CardWidth, 300f);
        canvasRect.localScale = Vector3.one * CanvasScale;
        CanvasGroup fade = canvasT.GetComponent<CanvasGroup>();
        if (fade == null) fade = canvasT.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;
        fade.interactable = false;

        // Card: fixed width, bottom edge pinned, grows upward with its content.
        RectTransform card = BladeholdUIKit.NewUI("Card", canvasT);
        BladeholdUIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -BottomBelowOrigin / CanvasScale), new Vector2(CardWidth, 300f));
        BladeholdUIKit.WindowChrome(card, raycast: false);
        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 26, 24);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter cardFit = card.gameObject.AddComponent<ContentSizeFitter>();
        cardFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI nameLabel = Label(card, "NameLabel", "Item Name", UIFontRole.Header, 36f, UIColorRole.Accent, TextAlignmentOptions.Center);
        nameLabel.fontStyle = FontStyles.UpperCase;
        nameLabel.characterSpacing = 3f;
        nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 22f;
        nameLabel.fontSizeMax = 36f;
        TextMeshProUGUI statusLabel = Label(card, "StatusLabel", "LOCKED", UIFontRole.Header, 18f, UIColorRole.TextDim, TextAlignmentOptions.Center);
        statusLabel.characterSpacing = 6f;
        BladeholdUIKit.Unthemed(statusLabel); // coloured per state by the pedestal.

        Rule(card);
        TextMeshProUGUI description = Label(card, "DescriptionLabel", "Description", UIFontRole.Body, 21f, UIColorRole.Text, TextAlignmentOptions.Top);

        // Type-specific detail blocks, in reading order; only the fields this pedestal has are built.
        TextMeshProUGUI combatStats = OptionalLabel(so, "combatStatsLabel", card, "CombatStatsLabel", UIColorRole.TextDim);
        TextMeshProUGUI stats = OptionalLabel(so, "statsLabel", card, "StatsLabel", UIColorRole.TextDim);
        TextMeshProUGUI perks = OptionalLabel(so, "perksLabel", card, "PerksLabel", UIColorRole.Text);
        TextMeshProUGUI upgrades = OptionalLabel(so, "upgradesLabel", card, "UpgradesLabel", UIColorRole.Text);
        TextMeshProUGUI ultimate = OptionalLabel(so, "ultimateLabel", card, "UltimateLabel", UIColorRole.Accent);

        Rule(card);
        TextMeshProUGUI costLabel = Label(card, "CostLabel", "50 Metal", UIFontRole.Header, 26f, UIColorRole.Cost, TextAlignmentOptions.Center);
        BladeholdUIKit.Unthemed(costLabel); // coloured per state by the pedestal.

        Set(so, "panelCanvasGroup", fade);
        Set(so, "nameLabel", nameLabel);
        Set(so, "statusLabel", statusLabel);
        Set(so, "descriptionLabel", description);
        Set(so, "costLabel", costLabel);
        Set(so, "combatStatsLabel", combatStats);
        Set(so, "statsLabel", stats);
        Set(so, "perksLabel", perks);
        Set(so, "upgradesLabel", upgrades);
        Set(so, "ultimateLabel", ultimate);
        SerializedProperty currency = so.FindProperty("currencyIcon");
        if (currency != null) currency.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static TextMeshProUGUI Label(RectTransform parent, string name, string text, UIFontRole font, float size, UIColorRole role, TextAlignmentOptions align)
    {
        TextMeshProUGUI label = BladeholdUIKit.Txt(BladeholdUIKit.NewUI(name, parent), text, font, size, role, align);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.lineSpacing = -6f;
        return label;
    }

    private static TextMeshProUGUI OptionalLabel(SerializedObject so, string field, RectTransform parent, string name, UIColorRole role)
    {
        if (so.FindProperty(field) == null)
        {
            return null;
        }
        return Label(parent, name, "", UIFontRole.Body, 19f, role, TextAlignmentOptions.TopLeft);
    }

    private static void Rule(RectTransform parent)
    {
        RectTransform rule = BladeholdUIKit.NewUI("Divider", parent);
        BladeholdUIKit.Size(rule.gameObject, -1f, 8f);
        BladeholdUIKit.Divider(rule);
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
    }
}
