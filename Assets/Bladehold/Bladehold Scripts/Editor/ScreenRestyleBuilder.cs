using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using R = BladeholdUIKitRestyle;
using K = BladeholdUIKit;

/// <summary>
///     Brings the older screens into the shared menu look (<see cref="BladeholdUIKit" />, the settings /
///     shop style): dark wells with thin gold frames instead of parchment cards, Texturina accent titles
///     with flourishes, Grenze body text on theme roles, parchment / ghost buttons with hover glow.
///     Unlike the shop builder these screens keep their hierarchies, so every serialized reference, MMF
///     player and runtime binding survives: the restyle only swaps sprites, colours and fonts and adds
///     <c>Theme…</c> decoration children (cleared and re-added on every run, so it's re-runnable).
///
///     The boss intro banner is the exception: its old children are replaced by a new layout
///     (<see cref="BuildBossIntro" />) and rewired to <see cref="EnemyIntroUI" />.
///
///     Menu: Bladehold > UI > Restyle. Tune here and re-run rather than hand-editing the prefabs.
/// </summary>
public static class ScreenRestyleBuilder
{
    private const string Ui = "Assets/Bladehold/Bladehold Prefabs/UI/";
    private const string HudPath = Ui + "Bladehold HUD.prefab";
    private const string CardPath = Ui + "Card.prefab";
    private const string WaveCardPath = Ui + "WaveCard.prefab";
    private const string SidebarRowPath = Ui + "SidebarSkillRow.prefab";
    private const string DeathScreenPath = Ui + "DeathScreen.prefab";
    private const string SaveSlotsPath = Ui + "SaveSlotsScreen.prefab";
    private const string SaveSlotPath = Ui + "SaveSlot.prefab";
    private const string NodeButtonPath = Ui + "CampaignNodeButton.prefab";
    private const string TierHeaderPath = Ui + "CampaignTierHeader.prefab";
    private const string CampaignScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Campaign Map Scene.unity";
    private const string UnderlayHeaderFont = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Black Underlay.asset";
    private const string SkullSprite = "ICON_FantasyWarrior_Map_Skull_01_Underlay";

    [MenuItem("Bladehold/UI/Restyle/All Screens")]
    public static void RestyleAll()
    {
        RestyleDraft();
        RestyleDeathScreen();
        RestyleSaveSlots();
        BuildBossIntro();
        RestyleCampaignMap();
    }

    // ───────────────────────────────────────────────── shared pieces

    private static void Edit(string path, System.Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            if (ok) Debug.Log($"[ScreenRestyleBuilder] Saved {path}.");
            else Debug.LogError($"[ScreenRestyleBuilder] Failed to save {path}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>A label/value stat row: dim label, bright value.</summary>
    private static void StatRows(Transform parent)
    {
        foreach (TMP_Text text in parent.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Label") R.Ink(text, UIColorRole.TextDim, UIFontRole.Body);
            else if (text.name == "Value") R.Ink(text, UIColorRole.Text, UIFontRole.Body);
        }
    }

    /// <summary>The player info sidebar (draft modal and death screen share the component and layout).</summary>
    private static void Sidebar(RectTransform sidebar)
    {
        R.CardChrome(sidebar);
        foreach (TMP_Text text in sidebar.GetComponentsInChildren<TMP_Text>(true))
        {
            switch (text.name)
            {
                case "ClassName": R.TitleInk(text, 4f); break;
                case "HPLabel":
                case "GateHPLabel": R.Ink(text, UIColorRole.Text, UIFontRole.Body); break;
                case "SkillsHeader": R.SectionInk(text); break;
            }
        }
        StatRows(sidebar.Find("StatsSection"));
        foreach (Image image in sidebar.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null) continue;
            if (image.sprite.name.Contains("Line_04")) K.Paint(image, UIColorRole.AccentMuted);
            else if (image.sprite.name.Contains("_Line_")) R.Rule(image, 0.45f).type = Image.Type.Simple;
        }
        Image scroll = R.Need<Image>(sidebar, "SkillsScrollView");
        if (scroll != null) R.Flat(scroll, UIColorRole.Dimmer, 0.6f);
    }

    // ───────────────────────────────────────────────── draft + wave cards

    [MenuItem("Bladehold/UI/Restyle/Draft and Wave Cards")]
    public static void RestyleDraft()
    {
        K.Begin(UIMenuId.Draft);
        Edit(CardPath, RestyleSkillCard);
        Edit(WaveCardPath, RestyleWaveCard);
        Edit(SidebarRowPath, root =>
        {
            R.Flat(root.GetComponent<Image>(), UIColorRole.Ghost, 0.9f);
            R.Ink(R.Need<TMP_Text>(root.transform, "Name"), UIColorRole.Text, UIFontRole.Body);
            R.Ink(R.Need<TMP_Text>(root.transform, "LevelBadge"), UIColorRole.Accent, UIFontRole.Body);
        });
        Edit(HudPath, RestyleDraftModal);
    }

    private static void RestyleSkillCard(GameObject root)
    {
        K.Scope(root, UIMenuId.Draft);
        R.Invisible(root.GetComponent<Image>());

        // Visual is the card face and the Mask: the frame sits inside its rect so the mask doesn't clip it.
        RectTransform visual = (RectTransform)R.Need(root.transform, "Visual");
        R.CardChrome(visual, 0.85f, 3f);
        visual.GetComponent<Mask>().showMaskGraphic = true;

        Image glow = R.Need<Image>(visual, "Glow");
        K.Paint(glow, UIColorRole.Accent, glow.color.a);

        RectTransform icon = (RectTransform)R.Need(visual, "Icon");
        RectTransform medallion = K.Decoration("ThemeMedallion", visual);
        Vector2 iconCentre = icon.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - icon.pivot, icon.sizeDelta);
        K.Place(medallion, icon.anchorMin, new Vector2(0.5f, 0.5f), iconCentre, icon.sizeDelta * 1.25f);
        K.Img(medallion, K.Diamond, UIColorRole.Accent, 0.12f);
        medallion.SetSiblingIndex(icon.GetSiblingIndex());

        R.Ink(R.Need<TMP_Text>(visual, "Title"), UIColorRole.Accent, UIFontRole.Header);
        TMP_Text level = R.Ink(R.Need<TMP_Text>(visual, "LevelBadge"), UIColorRole.TextDim, UIFontRole.Body);
        level.fontSize = 28f;
        TMP_Text desc = R.Ink(R.Need<TMP_Text>(visual, "Description"), UIColorRole.Text, UIFontRole.Body);
        desc.fontSize = 32f;
        desc.enableAutoSizing = true;
        desc.fontSizeMin = 22f;
        desc.fontSizeMax = 32f;

        RectTransform rule = K.Decoration("ThemeRule", visual);
        RectTransform levelRect = level.rectTransform;
        K.Place(rule, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, levelRect.anchoredPosition.y - levelRect.sizeDelta.y * 0.5f - 30f), new Vector2(320f, 8f));
        R.Rule(K.Img(rule, K.Line, UIColorRole.AccentMuted), 0.55f);
    }

    private static void RestyleWaveCard(GameObject root)
    {
        K.Scope(root, UIMenuId.Draft);
        R.Invisible(root.GetComponent<Image>());
        RectTransform visual = (RectTransform)R.Need(root.transform, "Visual");
        R.CardChrome(visual, 0.85f, 0f);
        Image glow = R.Need<Image>(visual, "Glow");
        K.Paint(glow, UIColorRole.Accent, glow.color.a);

        Transform content = R.Need(visual, "Content");
        Image band = R.Need<Image>(content, "StanceBand");
        band.sprite = K.ParchmentSmall;
        band.type = Image.Type.Sliced;
        band.pixelsPerUnitMultiplier = 2f;
        K.Unthemed(band);
        R.Ink(R.Need<TMP_Text>(content, "StanceBand/Title"), UIColorRole.Text, UIFontRole.Header);

        K.Paint(R.Need<Image>(content, "TimerRow/TimerIcon"), UIColorRole.TextDim);
        R.Ink(R.Need<TMP_Text>(content, "TimerRow/TimerText"), UIColorRole.Text, UIFontRole.Body);

        foreach (Image skull in R.Need(content, "SkullRow").GetComponentsInChildren<Image>(true)) K.Unthemed(skull);

        K.Paint(R.Need<Image>(content, "ClanRow/ClanIcon"), UIColorRole.Text);
        R.Ink(R.Need<TMP_Text>(content, "ClanRow/ClanText"), UIColorRole.Text, UIFontRole.Body);
        R.Ink(R.Need<TMP_Text>(content, "CaptainRow/CaptainText"), UIColorRole.Danger, UIFontRole.Body);
        R.Rule(R.Need<Image>(content, "Divider"), 0.55f);
        R.SectionInk(R.Need<TMP_Text>(content, "RewardHeaderRow/RewardHeader"));
        R.Ink(R.Need<TMP_Text>(content, "RewardHeaderRow/Multiplier"), UIColorRole.Cost, UIFontRole.Header);

        foreach (string row in new[] { "GoldRow", "SupplyRow", "BonusRow", "RerollRow", "FreshRow" })
        {
            Transform r = R.Need(content, row);
            if (r == null) continue;
            R.Flat(r.GetComponent<Image>(), UIColorRole.Ghost, 0.8f);
            foreach (TMP_Text text in r.GetComponentsInChildren<TMP_Text>(true)) R.Ink(text, UIColorRole.Text, UIFontRole.Body);
        }

        // Skull colours are set per card at runtime; bake theme-friendly ones for a dark card.
        var so = new SerializedObject(root.GetComponent<WaveCardUI>());
        so.FindProperty("skullEmptyColor").colorValue = K.Theme.Get(UIColorRole.TextDim, 0.25f);
        SerializedProperty tiers = so.FindProperty("skullTierColors");
        if (tiers.arraySize > 0) tiers.GetArrayElementAtIndex(0).colorValue = K.Theme.Get(UIColorRole.Text);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestyleDraftModal(GameObject hud)
    {
        Transform modal = R.Need(hud.transform, "SurvivorsCardSelectModal");
        if (modal == null) return;
        K.Scope(modal.gameObject, UIMenuId.Draft);
        R.Flat(modal.GetComponent<Image>(), UIColorRole.Dimmer, 1.25f);

        // Header: the parchment box goes; a flourished accent title stays (its MMF still animates the box rect).
        Transform header = R.Need(modal, "GameObject");
        R.ClearThemeDecor(header);
        R.Invisible(header.GetComponent<Image>());
        header.GetComponent<Image>().raycastTarget = false;
        RectTransform headerRect = (RectTransform)header;
        headerRect.sizeDelta = new Vector2(1900f, headerRect.sizeDelta.y);
        TMP_Text title = R.TitleInk(R.Need<TMP_Text>(header, "HeaderTitle"), 3f, false);
        title.fontSize = 64f;
        R.Flourish("ThemeFlourishLeft", header, true, new Vector2(0f, 0.5f), new Vector2(420f, 0f), new Vector2(400f, 80f));
        R.Flourish("ThemeFlourishRight", header, false, new Vector2(1f, 0.5f), new Vector2(-420f, 0f), new Vector2(400f, 80f));

        // Cards: the Card.prefab instances follow the prefab; only the per-instance banish buttons live here.
        foreach (SurvivorsCardUI card in modal.GetComponentsInChildren<SurvivorsCardUI>(true))
        {
            Transform banish = card.transform.Find("Banish_Btn");
            if (banish == null) continue;
            R.Button(banish.GetComponent<Button>(), K.ButtonStyle.Danger, banish.GetComponentInChildren<TMP_Text>(true));
            banish.GetComponentInChildren<TMP_Text>(true).characterSpacing = 3f;
        }

        Transform reroll = R.Need(modal, "Reroll_Btn");
        if (reroll != null)
        {
            TMP_Text label = reroll.Find("Text").GetComponent<TMP_Text>();
            R.Button(reroll.GetComponent<Button>(), K.ButtonStyle.Ghost, label, null, 0.9f);
            R.Ink(label, UIColorRole.Accent, UIFontRole.Header);
            K.Paint(R.Need<Image>(reroll, "DiceIcon"), UIColorRole.Accent);
        }

        Transform sidebar = R.Need(modal, "Sidebar_PlayerInfo");
        if (sidebar != null) Sidebar((RectTransform)sidebar);
    }

    // ───────────────────────────────────────────────── death / victory screen

    [MenuItem("Bladehold/UI/Restyle/Death Screen")]
    public static void RestyleDeathScreen()
    {
        K.Begin(UIMenuId.DeathScreen);
        Edit(DeathScreenPath, root =>
        {
            K.Scope(root, UIMenuId.DeathScreen);

            Transform banner = R.Need(root.transform, "FailureBanner");
            if (banner != null)
            {
                R.Flat(banner.GetComponent<Image>(), UIColorRole.Window, 0.95f);
                Image border = R.Need<Image>(banner, "Border");
                border.sprite = K.FrameLarge;
                border.type = Image.Type.Sliced;
                border.pixelsPerUnitMultiplier = 2.2f;
                K.Paint(border, UIColorRole.Frame);
                TMP_Text bannerText = R.Ink(R.Need<TMP_Text>(banner, "Text (TMP)"), UIColorRole.Text, UIFontRole.Header);
                bannerText.margin = new Vector4(48f, 16f, 48f, 16f);
                bannerText.enableAutoSizing = true;
                bannerText.fontSizeMin = 28f;
                bannerText.fontSizeMax = 48f;
            }

            Transform screen = R.Need(root.transform, "GameObject");
            R.ClearThemeDecor(screen);
            R.Flat(screen.GetComponent<Image>(), UIColorRole.Window);
            RectTransform vignette = K.Decoration("ThemeVignette", screen);
            K.Stretch(vignette);
            K.Img(vignette, K.LoadSprite("Vignette_Background_01"), UIColorRole.Dimmer, 0.9f);
            vignette.SetSiblingIndex(0);

            Transform titleRow = R.Need(screen, "TitleBand/TitleRow");
            K.Paint(R.Need<Image>(titleRow, "OrnamentLeft"), UIColorRole.AccentMuted);
            K.Paint(R.Need<Image>(titleRow, "OrnamentRight"), UIColorRole.AccentMuted);
            R.TitleInk(R.Need<TMP_Text>(titleRow, "TitleText"), 6f);
            R.Ink(R.Need<TMP_Text>(screen, "TitleBand/SubtitleText"), UIColorRole.TextDim, UIFontRole.Body);

            Transform panels = R.Need(screen, "Panels");
            RectTransform stats = (RectTransform)R.Need(panels, "Survivors_StatsPanel");
            R.CardChrome(stats);
            R.SectionInk(R.Need<TMP_Text>(stats, "Header"), 5f).fontSize = 30f;
            StatRows(stats);

            RectTransform rewards = (RectTransform)R.Need(panels, "SectorRewardsPanel");
            R.CardChrome(rewards);
            R.SectionInk(R.Need<TMP_Text>(rewards, "Header"), 5f).fontSize = 30f;
            R.SectionInk(R.Need<TMP_Text>(rewards, "CaptainsHeader"));
            R.Ink(R.Need<TMP_Text>(rewards, "CaptainsText"), UIColorRole.Text, UIFontRole.Body);
            R.Ink(R.Need<TMP_Text>(rewards, "TowerRefundText"), UIColorRole.TextDim, UIFontRole.Body);
            R.Rule(R.Need<Image>(rewards, "Divider"), 0.45f).type = Image.Type.Simple;
            Transform template = R.Need(rewards, "RewardList/RewardRowTemplate");
            if (template != null)
            {
                R.Ink(R.Need<TMP_Text>(template, "Label"), UIColorRole.TextDim, UIFontRole.Body);
                R.Ink(R.Need<TMP_Text>(template, "Value"), UIColorRole.Cost, UIFontRole.Body);
            }

            RectTransform sidebar = (RectTransform)R.Need(panels, "Sidebar_PlayerInfo");
            Sidebar(sidebar);
            R.Need<TMP_Text>(sidebar, "HeaderSection/ClassName").fontSize = 30f;

            // Panels end above the button row; the bar sits clear of the screen edge.
            RectTransform panelsRect = (RectTransform)panels;
            panelsRect.offsetMin = new Vector2(panelsRect.offsetMin.x, 170f);
            RectTransform bar = (RectTransform)R.Need(screen, "Button Bar");
            bar.localPosition = new Vector3(bar.localPosition.x, bar.localPosition.y, 0f); // was z -110
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(bar.anchoredPosition.x, 40f);
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, 96f);

            // Button bar: the parchment strip goes, the buttons become primary plates in a centred row.
            Transform group = R.Need(screen, "Button Bar/Group");
            R.Invisible(group.GetComponent<Image>());
            group.GetComponent<Image>().raycastTarget = false;
            HorizontalLayoutGroup groupLayout = group.GetComponent<HorizontalLayoutGroup>();
            groupLayout.childAlignment = TextAnchor.MiddleCenter;
            groupLayout.spacing = 40f;
            groupLayout.padding = new RectOffset(0, 0, 0, 0);
            groupLayout.childControlWidth = groupLayout.childControlHeight = false;
            groupLayout.childForceExpandWidth = groupLayout.childForceExpandHeight = false;
            foreach (Button button in group.GetComponentsInChildren<Button>(true))
            {
                ((RectTransform)button.transform).sizeDelta = new Vector2(520f, 76f);
                K.Size(button.gameObject, 520f, 76f);
                Transform content = button.transform.Find("Content");
                Transform background = content != null ? content.Find("Background") : null;
                if (background != null) Object.DestroyImmediate(background.gameObject);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                R.Button(button, K.ButtonStyle.Primary, label);
                label.characterSpacing = 2f;
                foreach (Image icon in button.GetComponentsInChildren<Image>(true))
                {
                    if (icon.name.StartsWith("ICON_")) K.Paint(icon, UIColorRole.TextOnParchment);
                }
            }
        });
    }

    // ───────────────────────────────────────────────── save slots

    [MenuItem("Bladehold/UI/Restyle/Save Slots")]
    public static void RestyleSaveSlots()
    {
        K.Begin(UIMenuId.SaveSlots);
        Edit(SaveSlotPath, root =>
        {
            K.Scope(root, UIMenuId.SaveSlots);
            Transform t = root.transform;
            R.CardChrome((RectTransform)t, addFrame: false);
            Image frame = R.Need<Image>(t, "Frame");
            frame.sprite = K.FrameSmall;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 2f;
            K.Paint(frame, UIColorRole.Frame, 0.85f);
            K.Stretch((RectTransform)frame.transform);
            Image glow = R.Need<Image>(t, "Glow");
            K.Paint(glow, UIColorRole.Accent, glow.color.a);

            R.TitleInk(R.Need<TMP_Text>(t, "SlotTitle"), 4f).fontSize = 64f;
            R.Rule(R.Need<Image>(t, "Divider"), 0.55f);
            foreach (TMP_Text text in R.Need(t, "UsedGroup").GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Label") R.Ink(text, UIColorRole.TextDim, UIFontRole.Body);
                else if (text.name == "Value") R.Ink(text, UIColorRole.Text, UIFontRole.Body);
            }
            R.Rule(R.Need<Image>(t, "UsedGroup/SummaryDivider"), 0.45f);
            R.Ink(R.Need<TMP_Text>(t, "UsedGroup/Summary"), UIColorRole.Cost, UIFontRole.Body);
            K.Paint(R.Need<Image>(t, "EmptyGroup/Emblem"), UIColorRole.AccentMuted, 0.35f);
            R.Ink(R.Need<TMP_Text>(t, "EmptyGroup/EmptyText"), UIColorRole.TextDim, UIFontRole.Body);

            Image plate = R.Need<Image>(t, "ActionPlate");
            plate.sprite = K.ParchmentSmall;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 1f;
            K.Paint(plate, UIColorRole.Parchment);
            R.Ink(R.Need<TMP_Text>(t, "ActionPlate/ActionText"), UIColorRole.TextOnParchment, UIFontRole.Header);

            Button delete = R.Need<Button>(t, "DeleteButton");
            R.Button(delete, K.ButtonStyle.Ghost, null);
            K.Paint(R.Need<Image>(t, "DeleteButton/Icon"), UIColorRole.Danger);
        });

        Edit(SaveSlotsPath, root =>
        {
            K.Scope(root, UIMenuId.SaveSlots);
            Transform t = root.transform;
            R.Flat(root.GetComponent<Image>(), UIColorRole.Dimmer, 1.2f);

            Transform header = R.Need(t, "HeaderPanel");
            R.ClearThemeDecor(header);
            R.Invisible(R.Need<Image>(header, "TitleBox"));
            R.Invisible(R.Need<Image>(header, "Frame"));
            TMP_Text headerTitle = R.TitleInk(R.Need<TMP_Text>(header, "TitleText"), 8f);
            headerTitle.fontSize = 84f;
            // Flourishes hug the (localised) title: measured with the uppercase style applied.
            float half = headerTitle.GetPreferredValues(headerTitle.text.ToUpperInvariant()).x * 0.5f + 50f;
            R.Flourish("ThemeFlourishLeft", header, true, new Vector2(0.5f, 0.5f), new Vector2(-half, -4f), new Vector2(360f, 76f));
            R.Flourish("ThemeFlourishRight", header, false, new Vector2(0.5f, 0.5f), new Vector2(half, -4f), new Vector2(360f, 76f));

            Button back = R.Need<Button>(t, "BackButton");
            Shadow shadow = back.GetComponent<Shadow>();
            if (shadow != null) Object.DestroyImmediate(shadow);
            R.Button(back, K.ButtonStyle.Ghost, R.Need<TMP_Text>(back.transform, "Label"), null, 0.9f);

            Transform dialog = R.Need(t, "DeleteDialog");
            R.Flat(dialog.GetComponent<Image>(), UIColorRole.Dimmer, 1.25f);
            RectTransform content = (RectTransform)R.Need(dialog, "Content");
            Transform oldFrame = content.Find("Frame");
            if (oldFrame != null) Object.DestroyImmediate(oldFrame.gameObject);
            R.WindowChrome(content);
            K.Paint(R.Need<Image>(content, "Skull"), UIColorRole.Danger);
            R.TitleInk(R.Need<TMP_Text>(content, "Title"), 4f, false);
            R.Ink(R.Need<TMP_Text>(content, "Message"), UIColorRole.Text, UIFontRole.Body);

            Button cancel = R.Need<Button>(content, "Buttons/CancelButton");
            if (cancel.GetComponent<Shadow>() != null) Object.DestroyImmediate(cancel.GetComponent<Shadow>());
            R.Button(cancel, K.ButtonStyle.Ghost, R.Need<TMP_Text>(cancel.transform, "Label"), null, 0.9f);

            // Hold-to-delete: a ghost plate the danger fill grows across while held.
            Transform hold = R.Need(content, "Buttons/HoldToDeleteButton");
            R.ClearThemeDecor(hold);
            if (hold.GetComponent<Shadow>() != null) Object.DestroyImmediate(hold.GetComponent<Shadow>());
            R.Flat(hold.GetComponent<Image>(), UIColorRole.Ghost);
            RectTransform holdFrame = K.Decoration("ThemeFrame", hold);
            K.Stretch(holdFrame, -2f, -2f, -2f, -2f);
            K.Img(holdFrame, K.FrameSmall, UIColorRole.Danger, sliced: true, ppu: 4f);
            holdFrame.SetSiblingIndex(0);
            K.Paint(R.Need<Image>(hold, "Fill"), UIColorRole.Danger, 0.9f);
            R.Ink(R.Need<TMP_Text>(hold, "Label"), UIColorRole.Text, UIFontRole.Header);
        });
    }

    // ───────────────────────────────────────────────── boss intro banner

    [MenuItem("Bladehold/UI/Restyle/Boss Intro Banner")]
    public static void BuildBossIntro()
    {
        K.Begin(UIMenuId.Hud);
        Edit(HudPath, hud =>
        {
            EnemyIntroUI intro = hud.GetComponentInChildren<EnemyIntroUI>(true);
            if (intro == null)
            {
                Debug.LogError("[ScreenRestyleBuilder] Bladehold HUD has no EnemyIntroUI.");
                return;
            }
            Transform root = intro.transform;
            for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            K.Scope(root.gameObject, UIMenuId.Hud);
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Letterbox bars (cinematic intros only), slid in from the edges.
            RectTransform top = K.NewUI("LetterboxTop", root);
            K.AnchorTop(top, 0f, 150f);
            Image topImage = top.gameObject.AddComponent<Image>();
            topImage.color = new Color(0f, 0f, 0f, 0.92f);
            topImage.raycastTarget = false;
            RectTransform bottom = K.NewUI("LetterboxBottom", root);
            K.AnchorBottom(bottom, 0f, 150f);
            Image bottomImage = bottom.gameObject.AddComponent<Image>();
            bottomImage.color = new Color(0f, 0f, 0f, 0.92f);
            bottomImage.raycastTarget = false;

            // Stage: a full-width strip in the upper third holding the band and the name block.
            RectTransform stage = K.NewUI("Stage", root);
            stage.anchorMin = new Vector2(0f, 1f);
            stage.anchorMax = new Vector2(1f, 1f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(0f, 340f);
            stage.anchoredPosition = new Vector2(0f, -560f);

            // Band: dark centre fading out to both sides, gold rules along its edges.
            RectTransform band = K.NewUI("Band", stage);
            K.Stretch(band);
            RectTransform bandLeft = K.NewUI("FadeLeft", band);
            bandLeft.anchorMin = new Vector2(0f, 0f);
            bandLeft.anchorMax = new Vector2(0.5f, 1f);
            bandLeft.offsetMin = bandLeft.offsetMax = Vector2.zero;
            bandLeft.localScale = new Vector3(-1f, 1f, 1f); // mirrored in place: opaque at the centre, fading left
            K.Img(bandLeft, K.GradientH, UIColorRole.Window);
            RectTransform bandRight = K.NewUI("FadeRight", band);
            bandRight.anchorMin = new Vector2(0.5f, 0f);
            bandRight.anchorMax = new Vector2(1f, 1f);
            bandRight.offsetMin = bandRight.offsetMax = Vector2.zero;
            K.Img(bandRight, K.GradientH, UIColorRole.Window);
            // The sprite is opaque at its left edge, so the right half reads centre-out as-is.
            foreach (bool upper in new[] { true, false })
            {
                RectTransform rule = K.NewUI(upper ? "RuleTop" : "RuleBottom", band);
                rule.anchorMin = new Vector2(0.18f, upper ? 1f : 0f);
                rule.anchorMax = new Vector2(0.82f, upper ? 1f : 0f);
                rule.pivot = new Vector2(0.5f, 0.5f);
                rule.sizeDelta = new Vector2(0f, 12f);
                rule.anchoredPosition = Vector2.zero;
                Image ruleImage = K.Img(rule, K.Line, UIColorRole.AccentMuted, 0.85f, sliced: true);
                ruleImage.raycastTarget = false;
            }

            // Name block: eyebrow, name with flourishes, then skulls + subtitle on one row.
            RectTransform block = K.NewUI("NameBlock", stage);
            K.Stretch(block);
            block.gameObject.AddComponent<CanvasGroup>();

            TextMeshProUGUI eyebrow = K.Txt(K.NewUI("Eyebrow", block), "A clan captain has arrived", UIFontRole.Body, 40f, UIColorRole.AccentMuted, TextAlignmentOptions.Center);
            K.Place(eyebrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 112f), new Vector2(2400f, 56f));
            eyebrow.characterSpacing = 12f;
            eyebrow.fontStyle = FontStyles.UpperCase;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;

            TextMeshProUGUI name = K.Txt(K.NewUI("EnemyName", block), "Captain Kombusta", UIFontRole.Keep, 128f, UIColorRole.Accent, TextAlignmentOptions.Center);
            name.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UnderlayHeaderFont);
            K.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(2400f, 170f));
            name.characterSpacing = 8f;
            name.fontStyle = FontStyles.UpperCase;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMin = 72f;
            name.fontSizeMax = 128f;

            RectTransform flourishLeft = K.NewUI("FlourishLeft", block);
            K.Place(flourishLeft, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-700f, 4f), new Vector2(420f, 84f));
            K.Img(flourishLeft, K.LineLeft, UIColorRole.AccentMuted).preserveAspect = true;
            RectTransform flourishRight = K.NewUI("FlourishRight", block);
            K.Place(flourishRight, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(700f, 4f), new Vector2(420f, 84f));
            K.Img(flourishRight, K.LineRight, UIColorRole.AccentMuted).preserveAspect = true;

            RectTransform details = K.NewUI("Details", block);
            K.Place(details, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -108f), new Vector2(0f, 60f));
            HorizontalLayoutGroup row = details.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 26f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            ContentSizeFitter fit = details.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform skullRow = K.NewUI("Skulls", details);
            HorizontalLayoutGroup skullLayout = skullRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            skullLayout.spacing = 10f;
            skullLayout.childAlignment = TextAnchor.MiddleCenter;
            skullLayout.childControlWidth = skullLayout.childControlHeight = true;
            skullLayout.childForceExpandWidth = skullLayout.childForceExpandHeight = false;
            Sprite skull = K.FindSprite(SkullSprite);
            var skulls = new Image[4];
            for (int i = 0; i < skulls.Length; i++)
            {
                RectTransform s = K.NewUI("Skull" + (i + 1), skullRow);
                skulls[i] = s.gameObject.AddComponent<Image>();
                skulls[i].sprite = skull;
                skulls[i].preserveAspect = true;
                skulls[i].raycastTarget = false;
                skulls[i].color = BannerDifficultyHelper.GetTierColor(BannerDifficultyTier.Nightmare);
                K.Size(s.gameObject, 50f, 50f);
            }

            TextMeshProUGUI subtitle = K.Txt(K.NewUI("Subtitle", details), "Nightmare  ·  1.5x rewards", UIFontRole.Body, 44f, UIColorRole.Text, TextAlignmentOptions.MidlineLeft);
            subtitle.characterSpacing = 3f;
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;

            var so = new SerializedObject(intro);
            so.FindProperty("canvasGroup").objectReferenceValue = group;
            so.FindProperty("topBar").objectReferenceValue = top;
            so.FindProperty("bottomBar").objectReferenceValue = bottom;
            so.FindProperty("band").objectReferenceValue = band;
            so.FindProperty("nameContainer").objectReferenceValue = block;
            so.FindProperty("eyebrowText").objectReferenceValue = eyebrow;
            so.FindProperty("enemyNameText").objectReferenceValue = name;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("flourishLeft").objectReferenceValue = flourishLeft;
            so.FindProperty("flourishRight").objectReferenceValue = flourishRight;
            so.FindProperty("skullRow").objectReferenceValue = skullRow.gameObject;
            SerializedProperty icons = so.FindProperty("skullIcons");
            icons.arraySize = skulls.Length;
            for (int i = 0; i < skulls.Length; i++) icons.GetArrayElementAtIndex(i).objectReferenceValue = skulls[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    // ───────────────────────────────────────────────── campaign map

    [MenuItem("Bladehold/UI/Restyle/Campaign Map")]
    public static void RestyleCampaignMap()
    {
        K.Begin(UIMenuId.CampaignMap);
        Edit(NodeButtonPath, root =>
        {
            K.Scope(root, UIMenuId.CampaignMap);
            // The label plaque under each diorama castle (CampaignDioramaUIRig moved these under Plaque).
            foreach (string frameName in new[] { "Plaque/BorderFrame", "Plaque/ActiveGlow" })
            {
                Image frame = R.Need<Image>(root.transform, frameName);
                frame.sprite = K.FrameSmall;
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 5f;
                if (frameName.EndsWith("ActiveGlow")) K.Paint(frame, UIColorRole.Accent, 0.35f);
            }
            R.Ink(R.Need<TMP_Text>(root.transform, "Plaque/TitleText"), UIColorRole.Text);
        });
        Edit(TierHeaderPath, root =>
        {
            TMP_Text text = root.GetComponent<TMP_Text>();
            R.Ink(text, UIColorRole.AccentMuted, UIFontRole.Header, 0.9f);
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 2f;
        });

        Scene scene = SceneManager.GetSceneByPath(CampaignScenePath);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(CampaignScenePath, OpenSceneMode.Additive);
            opened = true;
        }
        CampaignMapUI map = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            map = go.GetComponentInChildren<CampaignMapUI>(true);
            if (map != null) break;
        }
        if (map == null)
        {
            Debug.LogError("[ScreenRestyleBuilder] Campaign map scene has no CampaignMapUI.");
            return;
        }

        Transform t = map.transform;
        K.Scope(map.gameObject, UIMenuId.CampaignMap);
        R.Flat(R.Need<Image>(t, "BackgroundPanel"), UIColorRole.Window);

        Transform title = R.Need(t, "MapHeaderTitle");
        R.TitleInk(title.GetComponent<TMP_Text>(), 6f);
        TMP_Text subtitle = R.Ink(R.Need<TMP_Text>(title, "MapSubtitle"), UIColorRole.TextDim, UIFontRole.Body);
        subtitle.fontStyle = FontStyles.Normal;
        subtitle.characterSpacing = 1f;
        subtitle.fontSize = 18f;

        foreach (Transform chip in R.Need(t, "CurrenciesBar"))
        {
            R.CardChrome((RectTransform)chip, 0.55f, -2f);
            foreach (TMP_Text text in chip.GetComponentsInChildren<TMP_Text>(true)) R.Ink(text, UIColorRole.Text, UIFontRole.Body);
        }

        Transform tooltip = R.Need(t, "CampaignTooltip");
        R.Flat(tooltip.GetComponent<Image>(), UIColorRole.Window);
        Image tooltipFrame = R.Need<Image>(tooltip, "Frame");
        tooltipFrame.sprite = K.FrameSmall;
        tooltipFrame.type = Image.Type.Sliced;
        tooltipFrame.pixelsPerUnitMultiplier = 3f;
        K.Paint(tooltipFrame, UIColorRole.Frame, 0.9f);
        R.Ink(R.Need<TMP_Text>(tooltip, "TitleText"), UIColorRole.Accent, UIFontRole.Header);
        R.Ink(R.Need<TMP_Text>(tooltip, "SubtitleText"), UIColorRole.TextDim, UIFontRole.Body);
        R.Rule(R.Need<Image>(tooltip, "Divider"), 0.6f);
        R.Rule(R.Need<Image>(tooltip, "FooterDivider"), 0.6f);
        R.Ink(R.Need<TMP_Text>(tooltip, "CaptainSection/ClanBuff"), UIColorRole.Text, UIFontRole.Body);
        R.Ink(R.Need<TMP_Text>(tooltip, "RewardsSection/RewardsSummary"), UIColorRole.Cost, UIFontRole.Body);
        R.Ink(R.Need<TMP_Text>(tooltip, "LoreText"), UIColorRole.Text, UIFontRole.Body, 0.85f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ScreenRestyleBuilder] Campaign map scene restyled and saved.");
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }
}
