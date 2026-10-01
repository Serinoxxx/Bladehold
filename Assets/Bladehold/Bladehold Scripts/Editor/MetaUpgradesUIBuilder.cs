using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Builds the Spirit's meta upgrade window: the MetaPerkCard prefab, the window layout under a
///     MetaUpgradesUI's WindowRoot (tier sections on the left, details panel on the right), and the
///     Resources/MetaPerkCatalog asset. Re-runnable: it replaces everything under WindowRoot.
/// </summary>
public static class MetaUpgradesUIBuilder
{
    private const string CardPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/MetaPerkCard.prefab";
    private const string CatalogPath = "Assets/Bladehold/Resources/MetaPerkCatalog.asset";
    private const string TexturinaPath = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset";
    private const string GrenzePath = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Grenze/Grenze-SemiBold SDF 1.asset";
    private const string SpriteHud = "Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/";
    private const string SpriteFw = "Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/";

    private static readonly Color PanelColor = Hex("374046");
    private static readonly Color DeepPanelColor = Hex("1C2226E6");
    private static readonly Color HeaderTextColor = Hex("A7D2E7");
    private static readonly Color CardNameColor = Hex("9FD6EC");
    private static readonly Color BodyTextColor = Hex("D9D1BF");
    private static readonly Color MutedTextColor = Hex("B0B0B0");
    private static readonly Color GoldColor = Hex("FFD170");
    private static readonly Color ButtonColor = Hex("4A5760");

    private const float CardWidth = 140f;
    private const float CardHeight = 160f;
    private const int CardsPerRow = 5;
    private const float CardSpacing = 10f;

    [MenuItem("Bladehold/UI/Rebuild Meta Upgrades Window")]
    public static void RebuildInOpenScene()
    {
        MetaUpgradesUI ui = Object.FindFirstObjectByType<MetaUpgradesUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            Debug.LogError("[MetaUpgradesUIBuilder] No MetaUpgradesUI in the open scene (open the Meta Area scene).");
            return;
        }
        Build(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
    }

    /// <summary>Rebuilds the window under <paramref name="ui" /> (creating WindowRoot if missing) and wires every field.</summary>
    public static void Build(MetaUpgradesUI ui)
    {
        SyncCatalog();
        MetaPerkCardUI cardPrefab = BuildCardPrefab();

        TMP_FontAsset texturina = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TexturinaPath);
        TMP_FontAsset grenze = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GrenzePath);

        Transform rootT = ui.transform.Find("WindowRoot");
        if (rootT == null)
        {
            rootT = NewRect("WindowRoot", ui.transform).transform;
        }
        GameObject root = rootT.gameObject;
        for (int i = rootT.childCount - 1; i >= 0; i--) Object.DestroyImmediate(rootT.GetChild(i).gameObject);
        foreach (MenuFocusController old in root.GetComponents<MenuFocusController>()) Object.DestroyImmediate(old);

        RectTransform rootRect = (RectTransform)rootT;
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(1280f, 860f);
        Image rootImg = root.GetComponent<Image>();
        if (rootImg == null) rootImg = root.AddComponent<Image>();
        if (rootImg.sprite == null) rootImg.sprite = LoadSprite(SpriteHud + "SPR_HUD_FantasyWarrior_Example_Background.png");

        Image frame = NewImage("Frame", rootT, LoadSprite(SpriteFw + "SPR_FantasyWarrior_Frame_Box_Small_03.png"), Color.white);
        Stretch(frame.rectTransform, 0f);
        frame.raycastTarget = false;

        // Header: currencies (left), title (centre), close (right), divider
        RectTransform currencies = NewRect("Currencies", rootT);
        TopLeft(currencies, 56f, -34f, 360f, 56f);
        HorizontalLayoutGroup curLayout = currencies.gameObject.AddComponent<HorizontalLayoutGroup>();
        curLayout.spacing = 10f;
        curLayout.childAlignment = TextAnchor.MiddleLeft;
        curLayout.childControlWidth = curLayout.childControlHeight = true;
        curLayout.childForceExpandWidth = curLayout.childForceExpandHeight = false;
        NewIcon("BloodIcon", currencies, FindSprite("ICON_SM_Item_Bottle_03_Bonus"), Color.white, 48f);
        TMP_Text bloodText = NewText("GoblinBloodText", currencies, texturina, "0", 30f, Hex("C23B3B"), TextAlignmentOptions.MidlineLeft);
        AddLayoutSize(bloodText.gameObject, 90f, 48f);
        NewIcon("MetalIcon", currencies, FindSprite("ICON_SM_Item_Ingot_Iron_01"), Hex("AADDB9"), 48f);
        TMP_Text metalText = NewText("OrcishMetalText", currencies, texturina, "0", 30f, Hex("9FCCA6"), TextAlignmentOptions.MidlineLeft);
        AddLayoutSize(metalText.gameObject, 90f, 48f);

        TMP_Text title = NewText("TitleText", rootT, texturina, "SANCTUARY OF SPIRITS", 30f, HeaderTextColor, TextAlignmentOptions.Center);
        TopCentre(title.rectTransform, -34f, 600f, 52f);

        Button closeButton = NewButton("CloseButton", rootT, LoadSprite(SpriteHud + "SPR_HUD_FantasyWarrior_Box_Small_Parchment_01.png"), Hex("AABDC9"));
        TopRight((RectTransform)closeButton.transform, -46f, -36f, 48f, 48f);
        TMP_Text closeText = NewText("CloseText", closeButton.transform, texturina, "X", 22f, Hex("4A3F35"), TextAlignmentOptions.Center);
        Stretch(closeText.rectTransform, 0f);

        Image divider = NewImage("HeaderDivider", rootT, LoadSprite(SpriteHud + "SPR_HUD_FantasyWarrior_Line_01.png"), Hex("B6D0E8"));
        divider.type = Image.Type.Sliced;
        TopStretch(divider.rectTransform, -104f, 48f, 4f);

        // Left: scrollable tier sections
        const float bodyTop = -124f;
        const float bodyBottom = 44f;
        float listWidth = CardsPerRow * CardWidth + (CardsPerRow - 1) * CardSpacing + 24f;
        Image listBg = NewImage("ListBackground", rootT, LoadSprite(SpriteHud + "SPR_HUD_FantasyWarrior_Box_Background_01.png"), Hex("1C222699"));
        listBg.type = Image.Type.Sliced;
        listBg.raycastTarget = false;
        listBg.rectTransform.anchorMin = new Vector2(0f, 0f);
        listBg.rectTransform.anchorMax = new Vector2(0f, 1f);
        listBg.rectTransform.pivot = new Vector2(0f, 1f);
        listBg.rectTransform.offsetMin = new Vector2(40f, bodyBottom - 8f);
        listBg.rectTransform.offsetMax = new Vector2(56f + listWidth, bodyTop + 8f);

        RectTransform scroll = NewRect("TierScroll", rootT);
        scroll.anchorMin = new Vector2(0f, 0f);
        scroll.anchorMax = new Vector2(0f, 1f);
        scroll.pivot = new Vector2(0f, 1f);
        scroll.offsetMin = new Vector2(48f, bodyBottom);
        scroll.offsetMax = new Vector2(48f + listWidth, bodyTop);
        ScrollRect scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scroll.gameObject.AddComponent<ScrollRectAutoScroll>();

        RectTransform viewport = NewRect("Viewport", scroll);
        Stretch(viewport, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        RectTransform content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 14f;
        contentLayout.padding = new RectOffset(12, 12, 2, 12);
        contentLayout.childControlWidth = contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = content;

        string[] tierNames = { "I · COMMON BLESSINGS", "II · ANCIENT PROWESS", "III · LEGENDARY MASTERY" };
        int[] unlockCosts = { 0, 5, 10 };
        var sections = new List<(Transform grid, TMP_Text label, Button unlock, TMP_Text unlockText, int cost)>();
        for (int t = 0; t < 3; t++)
        {
            RectTransform section = NewRect($"Tier{t + 1}_Section", content);
            VerticalLayoutGroup sectionLayout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 12f;
            sectionLayout.childControlWidth = sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;

            RectTransform header = NewRect("Header", section);
            AddLayoutSize(header.gameObject, -1f, 34f);
            HorizontalLayoutGroup headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 12f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            TMP_Text label = NewText("Label", header, texturina, tierNames[t], 19f, MutedTextColor, TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            Button unlock = null;
            TMP_Text unlockText = null;
            if (t > 0)
            {
                unlock = NewButton("UnlockButton", header, LoadSprite(SpriteFw + "SPR_FantasyWarrior_Bar_Horizontal_05.png"), ButtonColor);
                AddLayoutSize(unlock.gameObject, 240f, 34f);
                unlockText = NewText("Text", unlock.transform, grenze, "Unlock", 17f, Color.white, TextAlignmentOptions.Center);
                Stretch(unlockText.rectTransform, 0f);
            }

            RectTransform grid = NewRect("Cards", section);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(CardWidth, CardHeight);
            gridLayout.spacing = new Vector2(CardSpacing, CardSpacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = CardsPerRow;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.padding = new RectOffset(0, 0, 4, 0);

            sections.Add((grid, label, unlock, unlockText, unlockCosts[t]));
        }

        // Right: details panel
        RectTransform details = NewRect("Details", rootT);
        details.anchorMin = new Vector2(0f, 0f);
        details.anchorMax = new Vector2(1f, 1f);
        details.pivot = new Vector2(0.5f, 1f);
        details.offsetMin = new Vector2(48f + listWidth + 24f, bodyBottom);
        details.offsetMax = new Vector2(-48f, bodyTop);
        Image detailsBg = details.gameObject.AddComponent<Image>();
        detailsBg.sprite = LoadSprite(SpriteHud + "SPR_HUD_FantasyWarrior_Box_Background_01.png");
        detailsBg.type = Image.Type.Sliced;
        detailsBg.color = DeepPanelColor;
        Image detailsFrame = NewImage("Frame", details, LoadSprite(SpriteFw + "SPR_FantasyWarrior_Frame_Box_10.png"), Color.white);
        detailsFrame.type = Image.Type.Sliced;
        Stretch(detailsFrame.rectTransform, -10f);
        detailsFrame.raycastTarget = false;

        Image detailIcon = NewImage("Icon", details, null, Color.white);
        detailIcon.preserveAspect = true;
        TopCentre(detailIcon.rectTransform, -36f, 128f, 128f);

        TMP_Text detailName = NewText("Name", details, texturina, "Perk", 28f, HeaderTextColor, TextAlignmentOptions.Center);
        TopStretch(detailName.rectTransform, -176f, 20f, 40f);
        TMP_Text detailRank = NewText("Rank", details, grenze, "Tier I", 19f, MutedTextColor, TextAlignmentOptions.Center);
        TopStretch(detailRank.rectTransform, -216f, 20f, 28f);

        Image detailDivider = NewImage("Divider", details, LoadSprite(SpriteHud + "SPR_HUD_FantasyWarrior_Line_01.png"), Hex("B6D0E8AA"));
        detailDivider.type = Image.Type.Sliced;
        TopStretch(detailDivider.rectTransform, -254f, 50f, 3f);

        RectTransform body = NewRect("Body", details);
        body.anchorMin = new Vector2(0f, 0f);
        body.anchorMax = new Vector2(1f, 1f);
        body.offsetMin = new Vector2(28f, 116f);
        body.offsetMax = new Vector2(-28f, -272f);
        VerticalLayoutGroup bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 18f;
        bodyLayout.childAlignment = TextAnchor.UpperCenter;
        bodyLayout.childControlWidth = bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;
        TMP_Text detailCurrent = NewText("Current", body, grenze, "", 22f, BodyTextColor, TextAlignmentOptions.Top);
        detailCurrent.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text detailNext = NewText("Next", body, grenze, "", 20f, GoldColor, TextAlignmentOptions.Top);
        detailNext.textWrappingMode = TextWrappingModes.Normal;

        Button buyButton = NewButton("BuyButton", details, LoadSprite(SpriteFw + "SPR_FantasyWarrior_Bar_Horizontal_05.png"), ButtonColor);
        RectTransform buyRect = (RectTransform)buyButton.transform;
        buyRect.anchorMin = buyRect.anchorMax = new Vector2(0.5f, 0f);
        buyRect.pivot = new Vector2(0.5f, 0f);
        buyRect.anchoredPosition = new Vector2(0f, 46f);
        buyRect.sizeDelta = new Vector2(300f, 56f);
        TMP_Text buyText = NewText("Text", buyButton.transform, grenze, "Learn", 22f, Color.white, TextAlignmentOptions.Center);
        Stretch(buyText.rectTransform, 0f);

        // Pad focus: trap inside the window, B closes it. The default card is set at runtime.
        MenuFocusController focus = root.AddComponent<MenuFocusController>();
        var focusSo = new SerializedObject(focus);
        focusSo.FindProperty("restrictTo").objectReferenceValue = rootRect;
        focusSo.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddVoidPersistentListener(GetOnCancel(focus), ui.Close);

        // Wire MetaUpgradesUI
        var so = new SerializedObject(ui);
        so.FindProperty("windowRoot").objectReferenceValue = root;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("focusController").objectReferenceValue = focus;
        so.FindProperty("goblinBloodText").objectReferenceValue = bloodText;
        so.FindProperty("orcishMetalText").objectReferenceValue = metalText;
        so.FindProperty("perkCardPrefab").objectReferenceValue = cardPrefab;
        so.FindProperty("detailIcon").objectReferenceValue = detailIcon;
        so.FindProperty("detailName").objectReferenceValue = detailName;
        so.FindProperty("detailRank").objectReferenceValue = detailRank;
        so.FindProperty("detailCurrent").objectReferenceValue = detailCurrent;
        so.FindProperty("detailNext").objectReferenceValue = detailNext;
        so.FindProperty("buyButton").objectReferenceValue = buyButton;
        so.FindProperty("buyButtonText").objectReferenceValue = buyText;
        SerializedProperty tiersProp = so.FindProperty("tiers");
        tiersProp.arraySize = 3;
        for (int t = 0; t < 3; t++)
        {
            SerializedProperty el = tiersProp.GetArrayElementAtIndex(t);
            el.FindPropertyRelative("cardGrid").objectReferenceValue = sections[t].grid;
            el.FindPropertyRelative("label").objectReferenceValue = sections[t].label;
            el.FindPropertyRelative("unlockButton").objectReferenceValue = sections[t].unlock;
            el.FindPropertyRelative("unlockButtonText").objectReferenceValue = sections[t].unlockText;
            el.FindPropertyRelative("unlockMetalCost").intValue = sections[t].cost;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(false);
        Debug.Log("[MetaUpgradesUIBuilder] Rebuilt the meta upgrades window.");
    }

    /// <summary>Adds every MetaPerkDefinitionSO asset missing from Resources/MetaPerkCatalog (creating it if needed).</summary>
    public static MetaPerkCatalogSO SyncCatalog()
    {
        MetaPerkCatalogSO catalog = AssetDatabase.LoadAssetAtPath<MetaPerkCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MetaPerkCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var so = new SerializedObject(catalog);
        SerializedProperty perks = so.FindProperty("perks");
        var present = new HashSet<Object>();
        for (int i = 0; i < perks.arraySize; i++) present.Add(perks.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (string guid in AssetDatabase.FindAssets("t:MetaPerkDefinitionSO"))
        {
            var perk = AssetDatabase.LoadAssetAtPath<MetaPerkDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (perk == null || present.Contains(perk)) continue;
            perks.arraySize++;
            perks.GetArrayElementAtIndex(perks.arraySize - 1).objectReferenceValue = perk;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static MetaPerkCardUI BuildCardPrefab()
    {
        TMP_FontAsset texturina = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TexturinaPath);
        TMP_FontAsset grenze = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GrenzePath);

        GameObject card = new GameObject("MetaPerkCard", typeof(RectTransform));
        try
        {
            ((RectTransform)card.transform).sizeDelta = new Vector2(CardWidth, CardHeight);
            Image bg = card.AddComponent<Image>();
            bg.sprite = LoadSprite(SpriteHud + "SPR_HUD_FantasyWarrior_Box_Background_01.png");
            bg.type = Image.Type.Sliced;
            bg.color = PanelColor;
            CanvasGroup group = card.AddComponent<CanvasGroup>();
            Button button = card.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.colorMultiplier = 1.4f;
            button.colors = colors;

            Image frame = NewImage("Frame", card.transform, LoadSprite(SpriteFw + "SPR_FantasyWarrior_Frame_Box_10.png"), Color.white);
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            Stretch(frame.rectTransform, -10f);

            Image highlight = NewImage("SelectedHighlight", card.transform, LoadSprite(SpriteFw + "SPR_FantasyWarrior_Frame_Box_10.png"), GoldColor);
            highlight.type = Image.Type.Sliced;
            highlight.raycastTarget = false;
            Stretch(highlight.rectTransform, -14f);
            highlight.gameObject.SetActive(false);

            Image icon = NewImage("Icon", card.transform, null, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TopCentre(icon.rectTransform, -10f, 54f, 54f);

            TMP_Text nameText = NewText("Name", card.transform, texturina, "Perk Name", 16f, CardNameColor, TextAlignmentOptions.Center);
            TopStretch(nameText.rectTransform, -64f, 8f, 34f);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 12f;
            nameText.fontSizeMax = 16f;
            nameText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform pips = NewRect("Pips", card.transform);
            TopStretch(pips, -100f, 10f, 8f);
            HorizontalLayoutGroup pipLayout = pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            pipLayout.spacing = 4f;
            pipLayout.childAlignment = TextAnchor.MiddleCenter;
            pipLayout.childControlWidth = pipLayout.childControlHeight = true;
            pipLayout.childForceExpandWidth = pipLayout.childForceExpandHeight = false;
            Image pip = NewImage("Pip", pips, null, Color.white);
            pip.raycastTarget = false;
            AddLayoutSize(pip.gameObject, 20f, 8f);

            TMP_Text costText = NewText("Cost", card.transform, grenze, "10 Blood", 16f, GoldColor, TextAlignmentOptions.Center);
            TopStretch(costText.rectTransform, -110f, 12f, 22f);
            costText.enableAutoSizing = true;
            costText.fontSizeMin = 10f;
            costText.fontSizeMax = 16f;

            MetaPerkCardUI cardUI = card.AddComponent<MetaPerkCardUI>();
            var so = new SerializedObject(cardUI);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("canvasGroup").objectReferenceValue = group;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("costText").objectReferenceValue = costText;
            so.FindProperty("pipTemplate").objectReferenceValue = pip;
            so.FindProperty("selectedHighlight").objectReferenceValue = highlight.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(card, CardPrefabPath, out bool ok);
            if (!ok) Debug.LogError($"[MetaUpgradesUIBuilder] Failed to save {CardPrefabPath}.");
            return saved != null ? saved.GetComponent<MetaPerkCardUI>() : null;
        }
        finally
        {
            Object.DestroyImmediate(card);
        }
    }

    private static UnityEngine.Events.UnityEvent GetOnCancel(MenuFocusController focus)
    {
        var field = typeof(MenuFocusController).GetField("onCancel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var evt = (UnityEngine.Events.UnityEvent)field.GetValue(focus);
        if (evt == null)
        {
            evt = new UnityEngine.Events.UnityEvent();
            field.SetValue(focus, evt);
        }
        return evt;
    }

    // ---------- small builders ----------

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    private static void NewIcon(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        Image img = NewImage(name, parent, sprite, color);
        img.preserveAspect = true;
        img.raycastTarget = false;
        AddLayoutSize(img.gameObject, size, size);
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private static Button NewButton(string name, Transform parent, Sprite sprite, Color color)
    {
        Image img = NewImage(name, parent, sprite, color);
        img.type = Image.Type.Sliced;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        return b;
    }

    private static void AddLayoutSize(GameObject go, float width, float height)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
        if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void TopRight(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void TopCentre(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>Full width minus <paramref name="sideMargin" /> each side, top edge at <paramref name="y" />.</summary>
    private static void TopStretch(RectTransform rt, float y, float sideMargin, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(sideMargin, y - h);
        rt.offsetMax = new Vector2(-sideMargin, y);
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    private static Sprite FindSprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return null;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
