using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Builds the Spirit's meta upgrade window in the shared menu style (<see cref="BladeholdUIKit" />,
///     themed as <see cref="UIMenuId.MetaPerks" />): a dimmed backdrop, a framed window with a title
///     and close button, scrolling tier sections of perk cards on the left, a details panel on the
///     right and a wallet footer. Also builds the MetaPerkCard prefab and syncs the
///     Resources/MetaPerkCatalog asset. Re-runnable: it replaces everything under WindowRoot.
/// </summary>
public static class MetaUpgradesUIBuilder
{
    private const string CardPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/MetaPerkCard.prefab";
    private const string CatalogPath = "Assets/Bladehold/Resources/MetaPerkCatalog.asset";

    private const float WindowWidth = 1320f;
    private const float WindowHeight = 900f;
    private const float CardWidth = 140f;
    private const float CardHeight = 170f;
    private const int CardsPerRow = 5;
    private const float CardSpacing = 12f;

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
        BladeholdUIKit.Begin(UIMenuId.MetaPerks);
        MetaPerkCardUI cardPrefab = BuildCardPrefab();

        Transform rootT = ui.transform.Find("WindowRoot");
        if (rootT == null)
        {
            rootT = BladeholdUIKit.NewUI("WindowRoot", ui.transform);
        }
        GameObject root = rootT.gameObject;
        for (int i = rootT.childCount - 1; i >= 0; i--) Object.DestroyImmediate(rootT.GetChild(i).gameObject);
        foreach (MenuFocusController old in root.GetComponents<MenuFocusController>()) Object.DestroyImmediate(old);
        foreach (UIThemedGraphic old in root.GetComponents<UIThemedGraphic>()) Object.DestroyImmediate(old);
        Image oldImage = root.GetComponent<Image>();
        if (oldImage != null) Object.DestroyImmediate(oldImage);

        // Root: full-screen dimmer that blocks clicks to the world behind.
        RectTransform rootRect = (RectTransform)rootT;
        BladeholdUIKit.Stretch(rootRect);
        BladeholdUIKit.Scope(root, UIMenuId.MetaPerks);
        BladeholdUIKit.Img(rootRect, BladeholdUIKit.White, UIColorRole.Dimmer, raycast: true);
        BladeholdUIKit.SetFeedbacks(rootT);

        // Laid out in 1920×1080 units whatever the host canvas's reference.
        RectTransform space = BladeholdUIKit.NewUI("DesignSpace", rootT);
        BladeholdUIKit.Stretch(space);
        space.gameObject.AddComponent<DesignResolutionScaler>();

        RectTransform window = BladeholdUIKit.NewUI("Window", space);
        BladeholdUIKit.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, WindowHeight));
        BladeholdUIKit.WindowChrome(window);

        // Header: title (centre), close (right), divider.
        RectTransform header = BladeholdUIKit.NewUI("Header", window);
        BladeholdUIKit.AnchorTop(header, 0f, 92f);
        BladeholdUIKit.Title(header, "Sanctuary of Spirits", 40f, 300f);
        Button closeButton = BladeholdUIKit.CloseButton(header);
        BladeholdUIKit.Place((RectTransform)closeButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(56f, 56f));
        RectTransform divider = BladeholdUIKit.NewUI("HeaderDivider", window);
        BladeholdUIKit.AnchorTop(divider, 96f, 10f, 48f);
        BladeholdUIKit.Divider(divider);

        const float bodyTop = 122f;
        const float bodyBottom = 100f;
        float listWidth = CardsPerRow * CardWidth + (CardsPerRow - 1) * CardSpacing + 48f;

        // Left: scrollable tier sections in a well.
        RectTransform listWell = BladeholdUIKit.NewUI("ListWell", window);
        listWell.anchorMin = new Vector2(0f, 0f);
        listWell.anchorMax = new Vector2(0f, 1f);
        listWell.pivot = new Vector2(0f, 1f);
        listWell.offsetMin = new Vector2(40f, bodyBottom);
        listWell.offsetMax = new Vector2(40f + listWidth, -bodyTop);
        BladeholdUIKit.Well(listWell);

        RectTransform scroll = BladeholdUIKit.NewUI("TierScroll", listWell);
        BladeholdUIKit.Stretch(scroll, 12f, 6f, 6f, 6f);
        ScrollRect scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.decelerationRate = 0.12f;
        scroll.gameObject.AddComponent<ScrollRectAutoScroll>();

        RectTransform viewport = BladeholdUIKit.NewUI("Viewport", scroll);
        BladeholdUIKit.Stretch(viewport, 0f, 14f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        BladeholdUIKit.HitArea(viewport);

        RectTransform content = BladeholdUIKit.NewUI("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10f;
        contentLayout.padding = new RectOffset(4, 4, 0, 16);
        contentLayout.childControlWidth = contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.verticalScrollbar = BuildScrollbar(scroll);
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        string[] tierNames = { "I · Common Blessings", "II · Ancient Prowess", "III · Legendary Mastery" };
        int[] unlockCosts = { 0, 5, 10 };
        var sections = new List<(Transform grid, TMP_Text label, Button unlock, TMP_Text unlockText, int cost)>();
        for (int t = 0; t < 3; t++)
        {
            RectTransform section = BladeholdUIKit.NewUI($"Tier{t + 1}_Section", content);
            VerticalLayoutGroup sectionLayout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 12f;
            sectionLayout.childControlWidth = sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;

            // Section header: spaced label + rule (as in settings), unlock button on the right.
            RectTransform head = BladeholdUIKit.NewUI("Header", section);
            BladeholdUIKit.Size(head.gameObject, -1f, 54f);
            TextMeshProUGUI label = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("Label", head), tierNames[t], UIFontRole.Header, 22f, UIColorRole.AccentMuted, TextAlignmentOptions.BottomLeft);
            BladeholdUIKit.Stretch(label.rectTransform, 4f, 250f, 0f, 10f);
            label.characterSpacing = 4f;
            label.fontStyle = FontStyles.UpperCase;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform rule = BladeholdUIKit.NewUI("Rule", head);
            BladeholdUIKit.AnchorBottom(rule, 4f, 2f);
            BladeholdUIKit.Img(rule, BladeholdUIKit.White, UIColorRole.AccentMuted, 0.25f);

            Button unlock = null;
            TMP_Text unlockText = null;
            if (t > 0)
            {
                unlock = BladeholdUIKit.Button("UnlockButton", head, "Unlock", BladeholdUIKit.ButtonStyle.Primary, out TextMeshProUGUI unlockLabel, 18f);
                BladeholdUIKit.Place((RectTransform)unlock.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(236f, 38f));
                unlockText = unlockLabel;
            }

            RectTransform grid = BladeholdUIKit.NewUI("Cards", section);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(CardWidth, CardHeight);
            gridLayout.spacing = new Vector2(CardSpacing, CardSpacing);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = CardsPerRow;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.padding = new RectOffset(8, 0, 4, 4);

            sections.Add((grid, label, unlock, unlockText, unlockCosts[t]));
        }

        // Right: details panel.
        RectTransform details = BladeholdUIKit.NewUI("Details", window);
        details.anchorMin = new Vector2(0f, 0f);
        details.anchorMax = new Vector2(1f, 1f);
        details.pivot = new Vector2(0.5f, 1f);
        details.offsetMin = new Vector2(40f + listWidth + 24f, bodyBottom);
        details.offsetMax = new Vector2(-40f, -bodyTop);
        BladeholdUIKit.Well(details);

        RectTransform iconBack = BladeholdUIKit.NewUI("IconBack", details);
        BladeholdUIKit.Place(iconBack, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(150f, 150f));
        BladeholdUIKit.Img(iconBack, BladeholdUIKit.Diamond, UIColorRole.Accent, 0.12f);
        RectTransform iconRect = BladeholdUIKit.NewUI("Icon", details);
        BladeholdUIKit.Place(iconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -39f), new Vector2(128f, 128f));
        Image detailIcon = iconRect.gameObject.AddComponent<Image>();
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;

        TextMeshProUGUI detailName = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("Name", details), "Perk", UIFontRole.Header, 30f, UIColorRole.Accent, TextAlignmentOptions.Center);
        BladeholdUIKit.AnchorTop(detailName.rectTransform, 186f, 42f, 20f);
        detailName.enableAutoSizing = true;
        detailName.fontSizeMin = 18f;
        detailName.fontSizeMax = 30f;
        TextMeshProUGUI detailRank = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("Rank", details), "Tier I", UIFontRole.Body, 20f, UIColorRole.TextDim, TextAlignmentOptions.Center);
        BladeholdUIKit.AnchorTop(detailRank.rectTransform, 228f, 28f, 20f);
        RectTransform detailDivider = BladeholdUIKit.NewUI("Divider", details);
        BladeholdUIKit.AnchorTop(detailDivider, 264f, 8f, 40f);
        BladeholdUIKit.Divider(detailDivider);

        RectTransform body = BladeholdUIKit.NewUI("Body", details);
        BladeholdUIKit.Stretch(body, 30f, 30f, 288f, 112f);
        VerticalLayoutGroup bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 18f;
        bodyLayout.childAlignment = TextAnchor.UpperCenter;
        bodyLayout.childControlWidth = bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;
        TextMeshProUGUI detailCurrent = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("Current", body), "", UIFontRole.Body, 23f, UIColorRole.Text, TextAlignmentOptions.Top);
        TextMeshProUGUI detailNext = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("Next", body), "", UIFontRole.Body, 21f, UIColorRole.Accent, TextAlignmentOptions.Top);

        Button buyButton = BladeholdUIKit.Button("BuyButton", details, "Learn", BladeholdUIKit.ButtonStyle.Primary, out TextMeshProUGUI buyText, 24f);
        BladeholdUIKit.Place((RectTransform)buyButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(300f, 58f));

        // Footer: wallet (left).
        RectTransform footer = BladeholdUIKit.NewUI("Footer", window);
        BladeholdUIKit.AnchorBottom(footer, 22f, 60f, 40f);
        RectTransform footerLine = BladeholdUIKit.NewUI("Divider", footer);
        BladeholdUIKit.AnchorTop(footerLine, -10f, 8f, 8f);
        BladeholdUIKit.Divider(footerLine, 0.35f);
        RectTransform wallet = BladeholdUIKit.NewUI("Currencies", footer);
        wallet.anchorMin = new Vector2(0f, 0f);
        wallet.anchorMax = new Vector2(0f, 1f);
        wallet.pivot = new Vector2(0f, 0.5f);
        wallet.sizeDelta = new Vector2(600f, 0f);
        wallet.anchoredPosition = new Vector2(8f, 0f);
        HorizontalLayoutGroup walletLayout = wallet.gameObject.AddComponent<HorizontalLayoutGroup>();
        walletLayout.spacing = 10f;
        walletLayout.childAlignment = TextAnchor.MiddleLeft;
        walletLayout.childControlWidth = walletLayout.childControlHeight = true;
        walletLayout.childForceExpandWidth = walletLayout.childForceExpandHeight = false;
        TMP_Text bloodText = Currency(wallet, "GoblinBlood", "ICON_SM_Item_Bottle_03_Bonus", BladeholdUIKit.Hex("E06A5E"));
        RectTransform gap = BladeholdUIKit.NewUI("Gap", wallet);
        BladeholdUIKit.Size(gap.gameObject, 24f, 10f);
        TMP_Text metalText = Currency(wallet, "OrcishMetal", "ICON_SM_Item_Ingot_Iron_01", BladeholdUIKit.Hex("AADDB9"));

        // Pad focus: trap inside the window, B closes it. The default card is set at runtime.
        MenuFocusController focus = root.AddComponent<MenuFocusController>();
        var focusSo = new SerializedObject(focus);
        focusSo.FindProperty("restrictTo").objectReferenceValue = window;
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
        GameObject card = new GameObject("MetaPerkCard", typeof(RectTransform));
        card.layer = LayerMask.NameToLayer("UI");
        try
        {
            RectTransform cardRect = (RectTransform)card.transform;
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            BladeholdUIKit.HitArea(cardRect);
            CanvasGroup group = card.AddComponent<CanvasGroup>();
            BladeholdUIKit.SetFeedbacks(cardRect);

            // Visual scales on hover/press; the root keeps the grid cell still.
            RectTransform visual = BladeholdUIKit.NewUI("Visual", cardRect);
            BladeholdUIKit.Stretch(visual);
            Image bg = BladeholdUIKit.Img(visual, BladeholdUIKit.White, UIColorRole.Well);
            RectTransform sheen = BladeholdUIKit.NewUI("Sheen", visual);
            BladeholdUIKit.Stretch(sheen);
            BladeholdUIKit.Img(sheen, BladeholdUIKit.GradientV, UIColorRole.Accent, 0.05f);
            RectTransform frame = BladeholdUIKit.NewUI("Frame", visual);
            BladeholdUIKit.Stretch(frame, -2f, -2f, -2f, -2f);
            BladeholdUIKit.Img(frame, BladeholdUIKit.FrameSmall, UIColorRole.AccentMuted, 0.55f, sliced: true, ppu: 4f);
            RectTransform glow = BladeholdUIKit.NewUI("HoverGlow", visual);
            BladeholdUIKit.Stretch(glow, -4f, -4f, -4f, -4f);
            Image glowImage = BladeholdUIKit.Img(glow, BladeholdUIKit.FrameSmall, UIColorRole.Accent, sliced: true, ppu: 4f);

            RectTransform highlight = BladeholdUIKit.NewUI("SelectedHighlight", visual);
            BladeholdUIKit.Stretch(highlight, -6f, -6f, -6f, -6f);
            BladeholdUIKit.Img(highlight, BladeholdUIKit.FrameSmall, UIColorRole.Accent, sliced: true, ppu: 3f);
            RectTransform highlightFill = BladeholdUIKit.NewUI("Fill", highlight);
            BladeholdUIKit.Stretch(highlightFill, 6f, 6f, 6f, 6f);
            BladeholdUIKit.Img(highlightFill, BladeholdUIKit.GradientV, UIColorRole.Accent, 0.14f);
            highlight.gameObject.SetActive(false);

            RectTransform iconBack = BladeholdUIKit.NewUI("IconBack", visual);
            BladeholdUIKit.Place(iconBack, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(72f, 72f));
            BladeholdUIKit.Img(iconBack, BladeholdUIKit.Diamond, UIColorRole.Accent, 0.1f);
            RectTransform iconRect = BladeholdUIKit.NewUI("Icon", visual);
            BladeholdUIKit.Place(iconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(60f, 60f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI nameText = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("Name", visual), "Perk Name", UIFontRole.Header, 16f, UIColorRole.Text, TextAlignmentOptions.Center);
            BladeholdUIKit.AnchorTop(nameText.rectTransform, 84f, 38f, 8f);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 12f;
            nameText.fontSizeMax = 16f;

            RectTransform pips = BladeholdUIKit.NewUI("Pips", visual);
            BladeholdUIKit.AnchorTop(pips, 126f, 8f, 12f);
            HorizontalLayoutGroup pipLayout = pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            pipLayout.spacing = 4f;
            pipLayout.childAlignment = TextAnchor.MiddleCenter;
            pipLayout.childControlWidth = pipLayout.childControlHeight = true;
            pipLayout.childForceExpandWidth = pipLayout.childForceExpandHeight = false;
            RectTransform pipRect = BladeholdUIKit.NewUI("Pip", pips);
            Image pip = pipRect.gameObject.AddComponent<Image>();
            pip.sprite = BladeholdUIKit.White;
            pip.raycastTarget = false;
            BladeholdUIKit.Size(pipRect.gameObject, 18f, 6f);

            // Cost colour is set per state at runtime (MetaPerkCardUI), so it isn't a themed graphic.
            TextMeshProUGUI costText = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("Cost", visual), "10 Blood", UIFontRole.Body, 17f, UIColorRole.Cost, TextAlignmentOptions.Center);
            BladeholdUIKit.Unthemed(costText);
            BladeholdUIKit.AnchorBottom(costText.rectTransform, 6f, 24f, 8f);
            costText.textWrappingMode = TextWrappingModes.NoWrap;
            costText.enableAutoSizing = true;
            costText.fontSizeMin = 10f;
            costText.fontSizeMax = 17f;

            Button button = card.AddComponent<Button>();
            button.targetGraphic = bg;
            button.colors = BladeholdUIKit.Tint(Color.white, Color.white, BladeholdUIKit.Hex("D8D8D8"));
            BladeholdUIKit.Juice(button, visual, glowImage, 1.05f, 0.96f);

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

    /// <summary>Currency icon + amount for the wallet. Currency colours are their own identity, not theme roles.</summary>
    private static TMP_Text Currency(RectTransform wallet, string name, string iconName, Color color)
    {
        RectTransform iconRect = BladeholdUIKit.NewUI(name + "Icon", wallet);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = BladeholdUIKit.FindSprite(iconName);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        BladeholdUIKit.Size(iconRect.gameObject, 44f, 44f);
        TextMeshProUGUI amount = BladeholdUIKit.Txt(BladeholdUIKit.NewUI(name + "Text", wallet), "0", UIFontRole.Header, 30f, UIColorRole.Text, TextAlignmentOptions.MidlineLeft);
        BladeholdUIKit.Unthemed(amount);
        amount.color = color;
        BladeholdUIKit.Size(amount.gameObject, 90f, 44f);
        return amount;
    }

    private static Scrollbar BuildScrollbar(RectTransform scroll)
    {
        RectTransform bar = BladeholdUIKit.NewUI("Scrollbar", scroll);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(8f, 0f);
        BladeholdUIKit.Img(bar, BladeholdUIKit.White, UIColorRole.Text, 0.06f, raycast: true);
        Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        RectTransform area = BladeholdUIKit.NewUI("Sliding Area", bar);
        BladeholdUIKit.Stretch(area);
        RectTransform handle = BladeholdUIKit.NewUI("Handle", area);
        BladeholdUIKit.Stretch(handle);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = BladeholdUIKit.Img(handle, BladeholdUIKit.White, UIColorRole.AccentMuted, raycast: true);
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scrollbar.colors = BladeholdUIKit.Tint(new Color(1f, 1f, 1f, 0.7f), Color.white, Color.white);
        return scrollbar;
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
}
