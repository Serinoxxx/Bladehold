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
///     The boss intro banner and the loading screen are the exceptions: their old children are replaced by
///     a new layout (<see cref="BuildBossIntro" />, <see cref="BuildLoadingScreen" />) and rewired to their
///     view component.
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
    private const string DemoEndPath = Ui + "DemoEndScreen.prefab";
    private const string CampaignScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Campaign Map Scene.unity";
    private const string FishingScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Fishing Pond.unity";
    private const string FishingDraftCardPath = Ui + "FishingDraftCard.prefab";
    private const string BuffFishButtonPath = Ui + "FishingBuffFishButton.prefab";
    private const string MainMenuScenePath = "Assets/Bladehold/Bladehold Scenes/MainMenu.unity";
    private const string LoadingScreenPath = "Assets/Bladehold/Resources/LoadingScreenManager.prefab";
    private const string KeyArtPath = "Assets/Bladehold/Art/Backgrounds/MainCapsule.png";
    private const string LogoPath = "Assets/Bladehold/Art/Backgrounds/bladeholdLogo.png";
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
        RestyleDemoEnd();
        RestyleFishing();
        RestyleMainMenu();
        BuildLoadingScreen();
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
        // Plan 22: the band holds "N ENEMIES" + the composition list, and the timer row a whole bonus line.
        AutoSize(R.Need<TMP_Text>(content, "StanceBand/Title"), 18f, 30.5f);

        K.Paint(R.Need<Image>(content, "TimerRow/TimerIcon"), UIColorRole.TextDim);
        R.Ink(R.Need<TMP_Text>(content, "TimerRow/TimerText"), UIColorRole.Text, UIFontRole.Body);
        AutoSize(R.Need<TMP_Text>(content, "TimerRow/TimerText"), 18f, 32f);
        LayoutElement timerLe = R.Need<LayoutElement>(content, "TimerRow/TimerText");
        if (timerLe != null) timerLe.flexibleWidth = 1f;

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

    private static void AutoSize(TMP_Text text, float min, float max)
    {
        if (text == null) return;
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
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

    // ───────────────────────────────────────────────── main menu title screen

    /// <summary>
    ///     The title screen in MainMenu.unity: themed parchment / ghost buttons (Synty button animators replaced
    ///     by juice), the patch notes as a dark framed window, Grenze version label, gamepad focus on Play.
    ///     Also deletes the scene's old built-in loading screen: the main menu now loads through the shared
    ///     <see cref="Bladehold.UI.LoadingScreenManager" /> (see <see cref="BuildLoadingScreen" />).
    /// </summary>
    // ───────────────────────────────────────────────── demo end ("Thanks for playing")

    [MenuItem("Bladehold/UI/Restyle/Demo End Screen")]
    public static void RestyleDemoEnd()
    {
        K.Begin(UIMenuId.CampaignMap);
        Edit(DemoEndPath, root =>
        {
            K.Scope(root, UIMenuId.CampaignMap);

            // Dimmer + vignette over the campaign map, which stays visible behind the window.
            Transform panel = R.Need(root.transform, "Panel");
            R.ClearThemeDecor(panel);
            R.Flat(panel.GetComponent<Image>(), UIColorRole.Dimmer, 1.2f);
            RectTransform vignette = K.Decoration("ThemeVignette", panel);
            K.Stretch(vignette);
            K.Img(vignette, K.LoadSprite("Vignette_Background_01"), UIColorRole.Dimmer, 0.9f);
            vignette.SetSiblingIndex(0);

            // The torn parchment becomes the menu window: dark fill, sheen, large gold frame.
            RectTransform box = (RectTransform)R.Need(panel, "Box");
            R.WindowChrome(box);
            box.sizeDelta = new Vector2(1100f, 540f);
            VerticalLayoutGroup layout = box.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(110, 110, 56, 56);
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleCenter;

            // Title: spaced accent caps with flourishes hugging it and a gold rule under it.
            TMP_Text header = R.TitleInk(R.Need<TMP_Text>(box, "Header"), 6f);
            header.fontSize = 58f;
            R.ClearThemeDecor(header.transform);
            float half = header.GetPreferredValues(header.text.ToUpperInvariant()).x * 0.5f + 28f;
            R.Flourish("ThemeFlourishLeft", header.transform, true, new Vector2(0.5f, 0.5f), new Vector2(-half, -4f), new Vector2(130f, 52f));
            R.Flourish("ThemeFlourishRight", header.transform, false, new Vector2(0.5f, 0.5f), new Vector2(half, -4f), new Vector2(130f, 52f));
            RectTransform rule = K.Decoration("ThemeRule", header.transform);
            K.Place(rule, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(560f, 10f));
            R.Rule(K.Img(rule, K.Line, UIColorRole.AccentMuted), 0.55f);

            TMP_Text body = R.Ink(R.Need<TMP_Text>(box, "Body"), UIColorRole.Text, UIFontRole.Body);
            body.fontSize = 32f;
            body.lineSpacing = 2f;

            // Wishlist is the call to action (primary parchment); Play Again is the quiet ghost.
            Transform buttons = R.Need(box, "Buttons");
            HorizontalLayoutGroup row = buttons.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 36f;
            buttons.GetComponent<LayoutElement>().preferredHeight = 84f;
            foreach (string name in new[] { "WishlistButton", "PlayAgainButton" })
            {
                Button button = R.Need<Button>(buttons, name);
                Shadow shadow = button.GetComponent<Shadow>();
                if (shadow != null) Object.DestroyImmediate(shadow);
                LayoutElement size = button.GetComponent<LayoutElement>();
                size.preferredWidth = 380f;
                size.preferredHeight = 76f;
                ((RectTransform)button.transform).sizeDelta = new Vector2(380f, 76f);
                bool primary = name == "WishlistButton";
                TMP_Text label = R.Need<TMP_Text>(button.transform, "Label");
                R.Button(button, primary ? K.ButtonStyle.Primary : K.ButtonStyle.Ghost, label, null, 0.9f);
                label.fontSize = 28f;
                label.characterSpacing = 2f;
            }
        });
    }

    [MenuItem("Bladehold/UI/Restyle/Main Menu")]
    public static void RestyleMainMenu()
    {
        K.Begin(UIMenuId.MainMenu);
        Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);
            opened = true;
        }
        Bladehold.UI.MainMenuManager menu = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            menu = go.GetComponentInChildren<Bladehold.UI.MainMenuManager>(true);
            if (menu != null) break;
        }
        if (menu == null)
        {
            Debug.LogError("[ScreenRestyleBuilder] MainMenu scene has no MainMenuManager.");
            return;
        }

        Transform canvas = menu.transform;
        K.Scope(canvas.gameObject, UIMenuId.MainMenu);
        Transform oldLoading = canvas.Find("LoadingScreen");
        if (oldLoading != null) Object.DestroyImmediate(oldLoading.gameObject);

        Transform title = R.Need(canvas, "Screen_Title");

        // Buttons: Play is the one parchment call to action, the rest are framed ghosts; Quit is a smaller ghost.
        RectTransform buttons = (RectTransform)R.Need(title, "Buttons");
        buttons.sizeDelta = new Vector2(buttons.sizeDelta.x, 700f);
        VerticalLayoutGroup column = buttons.GetComponent<VerticalLayoutGroup>();
        column.spacing = 28f;
        Button play = TitleButton(buttons, "Button_Start", K.ButtonStyle.Primary);
        TitleButton(buttons, "Button_ReplayTutorial", K.ButtonStyle.Ghost);
        TitleButton(buttons, "Button_Settings", K.ButtonStyle.Ghost);

        Button quit = R.Need<Button>(buttons, "Button_Quit");
        RectTransform quitRect = (RectTransform)quit.transform;
        quitRect.pivot = new Vector2(0.5f, 0.5f);
        quitRect.sizeDelta = new Vector2(420f, 110f);
        TMP_Text quitLabel = R.Need<TMP_Text>(quit.transform, "Label_Button");
        R.Button(quit, K.ButtonStyle.Ghost, quitLabel, quit.GetComponent<Image>(), 0.45f);
        quitLabel.fontSize = 44f;
        quitLabel.characterSpacing = 6f;
        K.Paint(quitLabel, UIColorRole.TextDim, 1f, UIFontRole.Header);

        MenuFocusController focus = title.GetComponent<MenuFocusController>();
        if (focus == null) focus = title.gameObject.AddComponent<MenuFocusController>();
        var focusSo = new SerializedObject(focus);
        focusSo.FindProperty("defaultSelectable").objectReferenceValue = play;
        focusSo.FindProperty("disableCancel").boolValue = true;
        focusSo.ApplyModifiedPropertiesWithoutUndo();

        // Patch notes: a dark framed window left of the buttons, with readable (4K-reference) type. Placed from
        // the centre column so narrower screens (16:10) keep the gap to the buttons.
        RectTransform notes = (RectTransform)R.Need(title, "ChangelogPanel");
        K.Place(notes, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-440f, -330f), new Vector2(1100f, 1020f));
        R.WindowChrome(notes);
        Transform header = R.Need(notes, "HeaderPanel");
        ((RectTransform)header).sizeDelta = new Vector2(-80f, 96f);
        ((RectTransform)header).anchoredPosition = new Vector2(0f, -30f);
        TMP_Text notesTitle = R.TitleInk(R.Need<TMP_Text>(header, "TitleText"), 6f);
        notesTitle.fontSize = 56f;
        TMP_Text badge = R.Ink(R.Need<TMP_Text>(header, "VersionBadge"), UIColorRole.AccentMuted, UIFontRole.Body);
        badge.fontSize = 40f;
        Image rule = R.Rule(R.Need<Image>(header, "DividerLine"), 0.55f);
        ((RectTransform)rule.transform).sizeDelta = new Vector2(0f, 8f);

        RectTransform scroll = (RectTransform)R.Need(notes, "ScrollView");
        scroll.offsetMin = new Vector2(44f, 40f);
        scroll.offsetMax = new Vector2(-36f, -150f);
        TMP_Text body = R.Ink(R.Need<TMP_Text>(scroll, "Viewport/Content/Text_Changelog"), UIColorRole.Text, UIFontRole.Body);
        body.fontSize = 34f;
        body.lineSpacing = 0f;
        body.paragraphSpacing = 6f;
        Image track = R.Flat(R.Need<Image>(scroll, "Scrollbar"), UIColorRole.Well, 0.8f);
        ((RectTransform)track.transform).sizeDelta = new Vector2(10f, 0f);
        R.Flat(R.Need<Image>(scroll, "Scrollbar/Sliding Area/Handle"), UIColorRole.AccentMuted, 0.8f);

        TMP_Text version = R.Ink(R.Need<TMP_Text>(canvas, "VersionLabel"), UIColorRole.TextDim, UIFontRole.Body, 0.8f);
        version.fontSize = 36f;
        ((RectTransform)version.transform).anchoredPosition = new Vector2(-48f, 36f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ScreenRestyleBuilder] Main menu restyled and saved.");
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    /// <summary>A Synty title button reskinned in place: its root Image becomes the plate, the old parchment art is hidden.</summary>
    private static Button TitleButton(Transform buttons, string name, K.ButtonStyle style)
    {
        Button button = R.Need<Button>(buttons, name);
        Transform content = R.Need(button.transform, "Content");
        R.Need(content, "Background").gameObject.SetActive(false);
        TMP_Text label = R.Need<TMP_Text>(content, "Label_ButtonName");
        R.Button(button, style, label, button.GetComponent<Image>(), 0.6f);
        label.fontSize = 56f;
        label.characterSpacing = 6f;
        // The arrow follows the label's colour.
        K.Paint(R.Need<Image>(content, "ICON"), style == K.ButtonStyle.Primary ? UIColorRole.TextOnParchment : UIColorRole.AccentMuted);
        return button;
    }

    // ───────────────────────────────────────────────── loading screen

    /// <summary>
    ///     Rebuilds the transition loading screen (Resources/LoadingScreenManager.prefab), used for every scene
    ///     load including the one out of the main menu: the key art (or the area's preview art) slowly drifting
    ///     behind a dark lower band, an "Entering" eyebrow, the area name with a flourished rule, subtitle and lore,
    ///     and a framed progress bar with status and percentage. Rewires <see cref="Bladehold.UI.LoadingScreenUI" />.
    /// </summary>
    [MenuItem("Bladehold/UI/Restyle/Loading Screen")]
    public static void BuildLoadingScreen()
    {
        K.Begin(UIMenuId.Global);
        Edit(LoadingScreenPath, prefab =>
        {
            Bladehold.UI.LoadingScreenUI view = prefab.GetComponentInChildren<Bladehold.UI.LoadingScreenUI>(true);
            if (view == null)
            {
                Debug.LogError("[ScreenRestyleBuilder] LoadingScreenManager prefab has no LoadingScreenUI.");
                return;
            }
            Transform root = view.transform;
            for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            K.Scope(root.gameObject, UIMenuId.Global);
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // fixed type size; the band stretches sideways on wide screens

            // Art: the key art by default, the area's own art over it when it has one; both drift slowly.
            RectTransform drift = K.NewUI("BackdropDrift", root);
            K.Stretch(drift);
            Image backdrop = ArtLayer("Backdrop", drift, AssetDatabase.LoadAssetAtPath<Sprite>(KeyArtPath));
            Image preview = ArtLayer("Preview", drift, null);
            preview.gameObject.SetActive(false);

            // Shade the art so the text reads, darker towards the bottom band.
            RectTransform shade = K.NewUI("Shade", root);
            K.Stretch(shade);
            K.Img(shade, K.White, UIColorRole.Window, 0.45f);
            RectTransform fade = K.NewUI("BottomFade", root);
            fade.anchorMin = Vector2.zero;
            fade.anchorMax = new Vector2(1f, 0.72f);
            fade.offsetMin = fade.offsetMax = Vector2.zero;
            fade.localScale = new Vector3(1f, -1f, 1f); // the sprite is opaque at the top: flipped, it darkens towards the bottom
            K.Img(fade, K.GradientV, UIColorRole.Window);
            RectTransform vignette = K.NewUI("Vignette", root);
            K.Stretch(vignette);
            K.Img(vignette, K.LoadSprite("Vignette_Background_01"), UIColorRole.Window, 0.7f);

            // Logo, top left.
            RectTransform logo = K.NewUI("Logo", root);
            K.Place(logo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -44f), new Vector2(380f, 76f));
            Image logoImage = logo.gameObject.AddComponent<Image>();
            logoImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
            logoImage.color = new Color(1f, 1f, 1f, 0.85f);

            // Area info: a bottom-up column so a missing subtitle or blurb just closes the gap.
            RectTransform info = K.NewUI("Info", root);
            info.anchorMin = new Vector2(0.5f, 0f);
            info.anchorMax = new Vector2(0.5f, 0f);
            info.pivot = new Vector2(0.5f, 0f);
            info.sizeDelta = new Vector2(1500f, 0f);
            info.anchoredPosition = new Vector2(0f, 168f);
            VerticalLayoutGroup column = info.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.LowerCenter;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.spacing = 6f;
            info.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI eyebrow = K.Txt(K.NewUI("Eyebrow", info), "Entering", UIFontRole.Body, 28f, UIColorRole.AccentMuted, TextAlignmentOptions.Center);
            eyebrow.characterSpacing = 14f;
            eyebrow.fontStyle = FontStyles.UpperCase;
            eyebrow.textWrappingMode = TextWrappingModes.NoWrap;

            TextMeshProUGUI areaName = K.Txt(K.NewUI("AreaName", info), "Frozen Pass", UIFontRole.Keep, 88f, UIColorRole.Accent, TextAlignmentOptions.Center);
            areaName.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UnderlayHeaderFont);
            areaName.characterSpacing = 8f;
            areaName.fontStyle = FontStyles.UpperCase;
            areaName.textWrappingMode = TextWrappingModes.NoWrap;
            areaName.enableAutoSizing = true;
            areaName.fontSizeMin = 52f;
            areaName.fontSizeMax = 88f;
            K.Size(areaName.gameObject, -1f, 104f);

            // Flourished rule: line, diamond, line.
            RectTransform ornament = K.NewUI("Ornament", info);
            K.Size(ornament.gameObject, -1f, 34f);
            RectTransform left = K.NewUI("FlourishLeft", ornament);
            K.Place(left, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(300f, 34f));
            K.Img(left, K.LineLeft, UIColorRole.AccentMuted).preserveAspect = true;
            RectTransform diamond = K.NewUI("Diamond", ornament);
            K.Place(diamond, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            K.Img(diamond, K.Diamond, UIColorRole.Accent);
            RectTransform right = K.NewUI("FlourishRight", ornament);
            K.Place(right, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(300f, 34f));
            K.Img(right, K.LineRight, UIColorRole.AccentMuted).preserveAspect = true;

            TextMeshProUGUI subtitle = K.Txt(K.NewUI("Subtitle", info), "The Inner Gate", UIFontRole.Body, 36f, UIColorRole.Text, TextAlignmentOptions.Center);
            subtitle.characterSpacing = 2f;
            subtitle.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform blurbRow = K.NewUI("DescriptionRow", info);
            HorizontalLayoutGroup blurbPad = blurbRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            blurbPad.padding = new RectOffset(230, 230, 10, 0);
            blurbPad.childControlWidth = blurbPad.childControlHeight = true;
            blurbPad.childForceExpandWidth = true;
            TextMeshProUGUI description = K.Txt(K.NewUI("Description", blurbRow), "Hold the frozen mountain pass against the horde before the snow buries the road home.", UIFontRole.Body, 28f, UIColorRole.TextDim, TextAlignmentOptions.Top);
            description.lineSpacing = 6f;

            // Progress: a framed recessed track with a gold fill, status left and percentage right underneath.
            RectTransform progress = K.NewUI("Progress", root);
            progress.anchorMin = new Vector2(0.5f, 0f);
            progress.anchorMax = new Vector2(0.5f, 0f);
            progress.pivot = new Vector2(0.5f, 0f);
            progress.sizeDelta = new Vector2(960f, 70f);
            progress.anchoredPosition = new Vector2(0f, 64f);

            RectTransform barRect = K.NewUI("LoadingBar", progress);
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(0f, 14f);
            barRect.anchoredPosition = Vector2.zero;
            K.Img(barRect, K.White, UIColorRole.Well, 0.9f);
            RectTransform barFrame = K.NewUI("Frame", barRect);
            K.Stretch(barFrame, -4f, -4f, -4f, -4f);
            K.Img(barFrame, K.FrameSmall, UIColorRole.AccentMuted, 0.8f, sliced: true, ppu: 5f);
            RectTransform fillArea = K.NewUI("Fill Area", barRect);
            K.Stretch(fillArea, 3f, 3f, 3f, 3f);
            RectTransform fill = K.NewUI("Fill", fillArea);
            K.Stretch(fill);
            K.Img(fill, K.White, UIColorRole.Accent);
            RectTransform sheen = K.NewUI("Sheen", fill);
            K.Stretch(sheen);
            K.Img(sheen, K.GradientV, UIColorRole.Parchment, 0.35f);
            Slider bar = barRect.gameObject.AddComponent<Slider>();
            bar.fillRect = fill;
            bar.direction = Slider.Direction.LeftToRight;
            bar.transition = Selectable.Transition.None;
            bar.interactable = false;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            bar.navigation = nav;
            bar.minValue = 0f;
            bar.maxValue = 1f;
            bar.value = 0.4f;

            TextMeshProUGUI status = K.Txt(K.NewUI("LoadingText", progress), "Loading", UIFontRole.Body, 26f, UIColorRole.TextDim, TextAlignmentOptions.BottomLeft);
            K.Place(status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(700f, 40f));
            status.characterSpacing = 3f;
            status.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI percent = K.Txt(K.NewUI("PercentText", progress), "40%", UIFontRole.Header, 28f, UIColorRole.AccentMuted, TextAlignmentOptions.BottomRight);
            K.Place(percent.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(200f, 40f));
            percent.characterSpacing = 2f;
            percent.textWrappingMode = TextWrappingModes.NoWrap;

            var so = new SerializedObject(view);
            so.FindProperty("logoLoadingFill").objectReferenceValue = null;
            so.FindProperty("loadingBar").objectReferenceValue = bar;
            so.FindProperty("loadingText").objectReferenceValue = status;
            so.FindProperty("percentText").objectReferenceValue = percent;
            so.FindProperty("eyebrowText").objectReferenceValue = eyebrow;
            so.FindProperty("enteringTitleText").objectReferenceValue = areaName;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("previewImage").objectReferenceValue = preview;
            so.FindProperty("backdropDrift").objectReferenceValue = drift;
            so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.ApplyModifiedPropertiesWithoutUndo();
            backdrop.transform.SetAsFirstSibling();
        });
    }

    /// <summary>A full-bleed art layer that covers the screen at any aspect (cropping, never letterboxing).</summary>
    private static Image ArtLayer(string name, Transform parent, Sprite sprite)
    {
        RectTransform rt = K.NewUI(name, parent);
        K.Stretch(rt);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        AspectRatioFitter fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;
        return image;
    }

    // ───────────────────────────────────────────────── fishing pond

    [MenuItem("Bladehold/UI/Restyle/Fishing Pond")]
    public static void RestyleFishing()
    {
        K.Begin(UIMenuId.Fishing);
        Edit(FishingDraftCardPath, root => FishingCard((RectTransform)root.transform));
        Edit(BuffFishButtonPath, root =>
        {
            K.Scope(root, UIMenuId.Fishing);
            Shadow shadow = root.GetComponent<Shadow>();
            if (shadow != null) Object.DestroyImmediate(shadow);
            TMP_Text label = R.Need<TMP_Text>(root.transform, "Label");
            R.Button(root.GetComponent<Button>(), K.ButtonStyle.Ghost, label, null, 0.9f);
            R.Ink(label, UIColorRole.Text, UIFontRole.Body);
        });

        Scene scene = SceneManager.GetSceneByPath(FishingScenePath);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(FishingScenePath, OpenSceneMode.Additive);
            opened = true;
        }
        FishingHUDUI hud = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            hud = go.GetComponentInChildren<FishingHUDUI>(true);
            if (hud != null) break;
        }
        if (hud == null)
        {
            Debug.LogError("[ScreenRestyleBuilder] Fishing Pond scene has no FishingHUDUI.");
            return;
        }

        Transform t = hud.transform;
        K.Scope(hud.gameObject, UIMenuId.Fishing);

        // Start prompt: a framed well around the StartWave glyph + label (HintEntry), sized by its layout.
        RectTransform prompt = (RectTransform)R.Need(t, "PromptPanel");
        R.CardChrome(prompt, 0.7f, -2f, UIColorRole.Window).raycastTarget = false;
        TMP_Text promptLabel = R.Ink(R.Need<TMP_Text>(prompt, "StartHint/Label"), UIColorRole.Text, UIFontRole.Body);
        promptLabel.fontSize = 36f;
        promptLabel.textWrappingMode = TextWrappingModes.NoWrap;
        // HintEntry doesn't size its children: fit the label to its (localised) text so the panel grows with it.
        ContentSizeFitter labelFit = promptLabel.GetComponent<ContentSizeFitter>();
        if (labelFit == null) labelFit = promptLabel.gameObject.AddComponent<ContentSizeFitter>();
        labelFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        promptLabel.rectTransform.sizeDelta = new Vector2(promptLabel.rectTransform.sizeDelta.x, 56f);
        RectTransform glyph = (RectTransform)R.Need(prompt, "StartHint/InputGlyph");
        glyph.sizeDelta = new Vector2(56f, 56f);
        K.Size(glyph.gameObject, 56f, 56f);

        // Frenzy HUD: countdown (its colour is set per tick in FishingHUDUI), timer, stats panel.
        TMP_Text countdown = R.Need<TMP_Text>(t, "CountdownPanel");
        R.Ink(countdown, UIColorRole.Accent, UIFontRole.Header);
        K.Unthemed(countdown);
        Transform frenzy = R.Need(t, "FrenzyHUD");
        // The timer floats over the scene: the underlay header font keeps it legible on bright foliage.
        TMP_Text timer = R.Need<TMP_Text>(frenzy, "TimerText");
        timer.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UnderlayHeaderFont);
        timer.fontSharedMaterial = timer.font.material;
        R.Ink(timer, UIColorRole.Accent);
        R.Ink(R.Need<TMP_Text>(frenzy, "FishCountText"), UIColorRole.Text, UIFontRole.Body);
        RectTransform stats = (RectTransform)R.Need(frenzy, "StatsPanel");
        R.CardChrome(stats, 0.7f, -2f, UIColorRole.Window);
        R.SectionInk(R.Need<TMP_Text>(stats, "LevelText"), 2f);
        R.Flat(R.Need<Image>(stats, "XpBar/Background"), UIColorRole.Dimmer, 0.85f);
        R.Flat(R.Need<Image>(stats, "XpBar/Fill Area/Fill"), UIColorRole.Accent);
        // Currency colours identify the currency (as on the main HUD); only gold follows the theme.
        R.Ink(R.Need<TMP_Text>(stats, "Resources/GoldCounter"), UIColorRole.Cost, UIFontRole.Body);

        // Level-up draft.
        Transform draft = R.Need(t, "FishingDraftUI/DraftModal");
        R.Flat(draft.GetComponent<Image>(), UIColorRole.Dimmer, 1.25f);
        R.ClearThemeDecor(draft);
        TMP_Text header = R.TitleInk(R.Need<TMP_Text>(draft, "Header"), 6f);
        float half = header.GetPreferredValues(header.text.ToUpperInvariant()).x * 0.5f + 50f;
        RectTransform headerRect = header.rectTransform;
        Vector2 headerAnchor = new Vector2(0.5f, 1f);
        R.Flourish("ThemeFlourishLeft", draft, true, headerAnchor, new Vector2(-half, headerRect.anchoredPosition.y), new Vector2(360f, 76f));
        R.Flourish("ThemeFlourishRight", draft, false, headerAnchor, new Vector2(half, headerRect.anchoredPosition.y), new Vector2(360f, 76f));
        R.Ink(R.Need<TMP_Text>(draft, "Subtitle"), UIColorRole.TextDim, UIFontRole.Body);
        // The three cards are FishingDraftCard.prefab instances: they follow the prefab restyle above.

        // Tally window.
        Transform tally = R.Need(t, "FishingTallyUI/TallyModal");
        R.Flat(tally.GetComponent<Image>(), UIColorRole.Dimmer, 1.25f);
        Transform panel = R.Need(tally, "Panel");
        R.WindowChrome((RectTransform)panel);
        R.TitleInk(R.Need<TMP_Text>(panel, "Header"), 6f);
        R.Ink(R.Need<TMP_Text>(panel, "TotalFish"), UIColorRole.Text, UIFontRole.Body);
        R.Ink(R.Need<TMP_Text>(panel, "GoldReward"), UIColorRole.Cost, UIFontRole.Body);
        // The other rewards keep their currency colours, brightened from the parchment versions to the HUD's.
        string[] rewards = { "BloodReward", "MetalReward", "DiamondBonesReward" };
        string[] rewardHex = { "F2594C", "8CCCFF", "D9FFFF" };
        for (int i = 0; i < rewards.Length; i++)
        {
            TMP_Text reward = R.Need<TMP_Text>(panel, rewards[i]);
            K.Unthemed(reward);
            reward.color = K.Hex(rewardHex[i]);
        }
        R.Rule(R.Need<Image>(panel, "Divider"), 0.55f);
        R.Ink(R.Need<TMP_Text>(panel, "BuffFishSection/BuffFishStatus"), UIColorRole.TextDim, UIFontRole.Body);
        Button cont = R.Need<Button>(panel, "ContinueButton");
        if (cont.GetComponent<Shadow>() != null) Object.DestroyImmediate(cont.GetComponent<Shadow>());
        TMP_Text contLabel = R.Need<TMP_Text>(cont.transform, "Label");
        R.Button(cont, K.ButtonStyle.Primary, contLabel);
        contLabel.characterSpacing = 2f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ScreenRestyleBuilder] Fishing Pond scene restyled and saved.");
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    /// <summary>The fishing upgrade card (FishingDraftCard.prefab): a dark framed well with hover glow.</summary>
    private static void FishingCard(RectTransform card)
    {
        K.Scope(card.gameObject, UIMenuId.Fishing);
        Shadow shadow = card.GetComponent<Shadow>();
        if (shadow != null) Object.DestroyImmediate(shadow);
        Image face = R.CardChrome(card, 0.85f);

        RectTransform glow = K.Decoration("ThemeGlow", card);
        K.Stretch(glow, -4f, -4f, -4f, -4f);
        Image glowImage = K.Img(glow, K.FrameSmall, UIColorRole.Accent, sliced: true, ppu: 3f);

        RectTransform icon = (RectTransform)R.Need(card, "Icon");
        RectTransform medallion = K.Decoration("ThemeMedallion", card);
        K.Place(medallion, icon.anchorMin, new Vector2(0.5f, 0.5f), icon.anchoredPosition, icon.sizeDelta * 1.25f);
        K.Img(medallion, K.Diamond, UIColorRole.Accent, 0.12f);
        medallion.SetSiblingIndex(icon.GetSiblingIndex());
        K.Paint(icon.GetComponent<Image>(), UIColorRole.Accent);

        R.Ink(R.Need<TMP_Text>(card, "Title"), UIColorRole.Accent, UIFontRole.Header);
        R.Ink(R.Need<TMP_Text>(card, "Level"), UIColorRole.TextDim, UIFontRole.Body);
        R.Rule(R.Need<Image>(card, "Divider"), 0.55f);
        R.Ink(R.Need<TMP_Text>(card, "Description"), UIColorRole.Text, UIFontRole.Body);

        Button button = card.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = face;
        button.colors = K.Tint(Color.white, K.Hex("FFF8E8"), K.Hex("D8CCB0"));
        UISelectableJuice juice = button.GetComponent<UISelectableJuice>();
        if (juice == null) juice = button.gameObject.AddComponent<UISelectableJuice>();
        var so = new SerializedObject(juice);
        so.FindProperty("selectable").objectReferenceValue = button;
        so.FindProperty("scaleTarget").objectReferenceValue = card;
        so.FindProperty("highlight").objectReferenceValue = glowImage;
        so.FindProperty("hoverScale").floatValue = 1.04f;
        so.FindProperty("pressScale").floatValue = 0.96f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
