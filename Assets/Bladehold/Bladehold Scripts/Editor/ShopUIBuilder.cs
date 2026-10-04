using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
///     Rebuilds the Rest Area shop (Bladehold > UI > Rebuild Shop UI) in the shared menu style
///     (<see cref="BladeholdUIKit" />, themed as <see cref="UIMenuId.Shop" />): <c>ShopUI.prefab</c> gets
///     a full-screen dimmed panel with a framed window, title, close button, a centred row for the
///     offer cards and a gold wallet footer; <c>ShopSlotPrefab.prefab</c> becomes a framed card with an
///     icon medallion, name, description and a buy button showing the price.
///
///     The slot root and its InvalidFeedback / PurchaseFeedback children are kept (their MMF players
///     shake, flash and pop the root), only the visuals are replaced. Re-runnable: tweak the layout
///     here and rebuild rather than hand-editing the prefabs.
/// </summary>
public static class ShopUIBuilder
{
    private const string ShopPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/ShopUI.prefab";
    private const string SlotPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/ShopSlotPrefab.prefab";

    private const float WindowWidth = 1660f;
    private const float WindowHeight = 720f;
    private const float SlotWidth = 270f;
    private const float SlotHeight = 410f;

    [MenuItem("Bladehold/UI/Rebuild Shop UI")]
    public static void RebuildAll()
    {
        BladeholdUIKit.Begin(UIMenuId.Shop);
        BuildSlotPrefab();
        BuildShopPrefab();
    }

    // ───────────────────────────────────────────────── shop window

    private static void BuildShopPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
        try
        {
            ShopUI shop = root.GetComponent<ShopUI>();
            if (shop == null)
            {
                Debug.LogError("[ShopUIBuilder] ShopUI.prefab has no ShopUI on its root.");
                return;
            }

            Sprite coin = FindChildSprite(root.transform, "CoinIcon");
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }
            BladeholdUIKit.Scope(root, UIMenuId.Shop);

            // Panel: the object ShopUI toggles. Full-screen dimmer, so the backdrop opens and closes with it.
            RectTransform panel = BladeholdUIKit.NewUI("ShopPanel", root.transform);
            BladeholdUIKit.Stretch(panel);
            BladeholdUIKit.Img(panel, BladeholdUIKit.White, UIColorRole.Dimmer, raycast: true);
            BladeholdUIKit.SetFeedbacks(panel);

            RectTransform window = BladeholdUIKit.NewUI("Window", panel);
            BladeholdUIKit.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, WindowHeight));
            BladeholdUIKit.WindowChrome(window);

            RectTransform header = BladeholdUIKit.NewUI("Header", window);
            BladeholdUIKit.AnchorTop(header, 0f, 92f);
            BladeholdUIKit.Title(header, "Merchant's Wares", 40f, 260f);
            Button close = BladeholdUIKit.CloseButton(header);
            BladeholdUIKit.Place((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(56f, 56f));
            RectTransform divider = BladeholdUIKit.NewUI("HeaderDivider", window);
            BladeholdUIKit.AnchorTop(divider, 96f, 10f, 48f);
            BladeholdUIKit.Divider(divider);

            // Offers: one centred row (ShopUI scales it down when Deep Pockets + featured offers overflow).
            RectTransform slots = BladeholdUIKit.NewUI("SlotsContainer", window);
            BladeholdUIKit.Stretch(slots, 60f, 60f, 140f, 112f);
            HorizontalLayoutGroup row = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 28f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            // Footer: gold wallet (left), divider above.
            RectTransform footer = BladeholdUIKit.NewUI("Footer", window);
            BladeholdUIKit.AnchorBottom(footer, 22f, 60f, 40f);
            RectTransform footerLine = BladeholdUIKit.NewUI("Divider", footer);
            BladeholdUIKit.AnchorTop(footerLine, -10f, 8f, 8f);
            BladeholdUIKit.Divider(footerLine, 0.35f);
            RectTransform wallet = BladeholdUIKit.NewUI("GoldBar", footer);
            wallet.anchorMin = new Vector2(0f, 0f);
            wallet.anchorMax = new Vector2(0f, 1f);
            wallet.pivot = new Vector2(0f, 0.5f);
            wallet.sizeDelta = new Vector2(420f, 0f);
            wallet.anchoredPosition = new Vector2(8f, 0f);
            HorizontalLayoutGroup walletLayout = wallet.gameObject.AddComponent<HorizontalLayoutGroup>();
            walletLayout.spacing = 10f;
            walletLayout.childAlignment = TextAnchor.MiddleLeft;
            walletLayout.childControlWidth = walletLayout.childControlHeight = true;
            walletLayout.childForceExpandWidth = walletLayout.childForceExpandHeight = false;
            RectTransform coinRect = BladeholdUIKit.NewUI("CoinIcon", wallet);
            Image coinImage = coinRect.gameObject.AddComponent<Image>();
            coinImage.sprite = coin;
            coinImage.preserveAspect = true;
            coinImage.raycastTarget = false;
            coinImage.enabled = coin != null;
            BladeholdUIKit.Size(coinRect.gameObject, 40f, 40f);
            TextMeshProUGUI gold = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("GoldLabel", wallet), "Gold: 0", UIFontRole.Header, 28f, UIColorRole.Cost, TextAlignmentOptions.MidlineLeft);
            gold.textWrappingMode = TextWrappingModes.NoWrap;
            BladeholdUIKit.Size(gold.gameObject, 340f, 44f);

            // Pad: trap focus in the window, B closes. ShopUI points the default at the first offer.
            MenuFocusController focus = panel.gameObject.AddComponent<MenuFocusController>();
            var focusSo = new SerializedObject(focus);
            focusSo.FindProperty("defaultSelectable").objectReferenceValue = close;
            focusSo.FindProperty("restrictTo").objectReferenceValue = window;
            focusSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddVoidPersistentListener(GetOnCancel(focus), shop.CloseShop);

            var so = new SerializedObject(shop);
            so.FindProperty("shopPanel").objectReferenceValue = panel.gameObject;
            so.FindProperty("goldLabel").objectReferenceValue = gold;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("slotsContainer").objectReferenceValue = slots;
            so.FindProperty("slotPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            so.FindProperty("ultimateSlotsContainer").objectReferenceValue = null;
            so.FindProperty("focusController").objectReferenceValue = focus;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath, out bool ok);
            Debug.Log($"[ShopUIBuilder] ShopUI.prefab saved: {ok}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ───────────────────────────────────────────────── offer card

    private static void BuildSlotPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        try
        {
            ShopSlotUI slot = root.GetComponent<ShopSlotUI>();
            if (slot == null)
            {
                Debug.LogError("[ShopUIBuilder] ShopSlotPrefab has no ShopSlotUI on its root.");
                return;
            }

            // Keep the purchase/invalid MMF children (they animate the root); replace everything else.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name != "InvalidFeedback" && child.name != "PurchaseFeedback")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
            foreach (UIThemedGraphic old in root.GetComponents<UIThemedGraphic>()) Object.DestroyImmediate(old);

            RectTransform card = (RectTransform)root.transform;
            BladeholdUIKit.SetFeedbacks(card);
            card.sizeDelta = new Vector2(SlotWidth, SlotHeight);
            BladeholdUIKit.Size(root, SlotWidth, SlotHeight);

            // The root Image is the card fill (the invalid feedback flashes it red, then restores it).
            Image fill = root.GetComponent<Image>();
            if (fill == null) fill = root.AddComponent<Image>();
            fill.sprite = BladeholdUIKit.White;
            fill.type = Image.Type.Simple;
            fill.raycastTarget = true;
            BladeholdUIKit.Paint(fill, UIColorRole.Well);

            RectTransform sheen = BladeholdUIKit.NewUI("Sheen", card);
            BladeholdUIKit.Stretch(sheen);
            BladeholdUIKit.Img(sheen, BladeholdUIKit.GradientV, UIColorRole.Accent, 0.05f);
            RectTransform frame = BladeholdUIKit.NewUI("Frame", card);
            BladeholdUIKit.Stretch(frame, -4f, -4f, -4f, -4f);
            BladeholdUIKit.Img(frame, BladeholdUIKit.FrameSmall, UIColorRole.Frame, 0.8f, sliced: true, ppu: 3f);

            RectTransform medallion = BladeholdUIKit.NewUI("IconBack", card);
            BladeholdUIKit.Place(medallion, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(120f, 120f));
            BladeholdUIKit.Img(medallion, BladeholdUIKit.Diamond, UIColorRole.Accent, 0.12f);
            RectTransform iconRect = BladeholdUIKit.NewUI("ItemIcon", card);
            BladeholdUIKit.Place(iconRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(92f, 92f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI itemName = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("ItemName", card), "Item Name", UIFontRole.Header, 24f, UIColorRole.Accent, TextAlignmentOptions.Center);
            BladeholdUIKit.AnchorTop(itemName.rectTransform, 152f, 58f, 16f);
            itemName.enableAutoSizing = true;
            itemName.fontSizeMin = 16f;
            itemName.fontSizeMax = 24f;

            RectTransform rule = BladeholdUIKit.NewUI("DividerLine", card);
            BladeholdUIKit.AnchorTop(rule, 214f, 6f, 36f);
            BladeholdUIKit.Divider(rule, 0.6f);

            TextMeshProUGUI desc = BladeholdUIKit.Txt(BladeholdUIKit.NewUI("ItemDesc", card), "Restores health or grants a strength.", UIFontRole.Body, 19f, UIColorRole.Text, TextAlignmentOptions.Top);
            BladeholdUIKit.Stretch(desc.rectTransform, 20f, 20f, 230f, 84f);
            desc.enableAutoSizing = true;
            desc.fontSizeMin = 13f;
            desc.fontSizeMax = 19f;

            // Buy button: dark so the price reads in its cost / can't-afford colour (set by ShopSlotUI).
            Button buy = BladeholdUIKit.Button("BuyButton", card, "25 Gold", BladeholdUIKit.ButtonStyle.Ghost, out TextMeshProUGUI price, 24f);
            BladeholdUIKit.Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(210f, 50f));
            RectTransform buyFrame = BladeholdUIKit.NewUI("Frame", buy.transform.Find("Visual"));
            BladeholdUIKit.Stretch(buyFrame, -2f, -2f, -2f, -2f);
            BladeholdUIKit.Img(buyFrame, BladeholdUIKit.FrameSmall, UIColorRole.AccentMuted, 0.7f, sliced: true, ppu: 4f);
            buyFrame.SetSiblingIndex(1);
            BladeholdUIKit.Unthemed(price);
            price.name = "Text";


            var so = new SerializedObject(slot);
            so.FindProperty("nameText").objectReferenceValue = itemName;
            so.FindProperty("descText").objectReferenceValue = desc;
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("buyButton").objectReferenceValue = buy;
            so.FindProperty("costText").objectReferenceValue = price;
            so.FindProperty("cardBackground").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath, out bool ok);
            Debug.Log($"[ShopUIBuilder] ShopSlotPrefab.prefab saved: {ok}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite FindChildSprite(Transform root, string name)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.name == name && image.sprite != null) return image.sprite;
        }
        return BladeholdUIKit.FindSprite("ICON_SM_Item_Coin_01");
    }

    private static UnityEvent GetOnCancel(MenuFocusController focus)
    {
        var field = typeof(MenuFocusController).GetField("onCancel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var evt = (UnityEvent)field.GetValue(focus);
        if (evt == null)
        {
            evt = new UnityEvent();
            field.SetValue(focus, evt);
        }
        return evt;
    }
}
