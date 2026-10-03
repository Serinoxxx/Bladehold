using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
///     Rebuilds the visual hierarchy of every <see cref="SettingsPanelView" /> (Bladehold > UI > Rebuild
///     Settings Panel) into the styled, responsive layout: a centred dark window with a gold frame
///     that stretches to the screen height, a title bar with Back, a General / Controls / Graphics
///     tab bar with Q/E · LB/RB glyphs, one scrolling column of section-headed rows per tab, a
///     footer with pad hints and Reset / Delete Save, and a matching confirm dialog. Also regenerates
///     <c>RebindRow.prefab</c> (glyph columns). The panel's root GameObject — and with it
///     <see cref="SettingsPanelView" />, <see cref="MenuFocusController" /> and their cancel wiring —
///     is kept; only its children are replaced. References into the old children from outside the
///     panel (e.g. <c>PauseMenuView.backFromSettingsButton</c>) and persistent onClick listeners on
///     the old buttons are re-pointed to the same-named new objects.
///
///     Re-runnable: the layout is code, so tweak it here and rebuild rather than hand-editing the
///     generated objects. Touches <c>PauseMenuCanvas.prefab</c> and the open scene.
/// </summary>
public static class SettingsPanelBuilder
{
    private const string PauseMenuPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/PauseMenuCanvas.prefab";
    private const string RebindRowPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/RebindRow.prefab";
    private const string MenuButtonPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/MenuButton.prefab";
    private const string InputGlyphPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/Glyphs/InputGlyph.prefab";
    private const string HintEntryPrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/Glyphs/HintEntry.prefab";
    private const string HudSprites = "Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_";
    private const string HeaderFontPath = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset";
    private const string KeycapPath = "Assets/Bladehold/Bladehold Images/UI/UI_Keycap_Sliced.png";
    private const string CheckmarkPath = "Assets/Bladehold/Bladehold Images/UI/Checkmark.png";
    private const string BodyFontPath = "Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Grenze/Grenze-SemiBold SDF.asset";

    private const float WindowWidth = 1120f;
    private const float RowHeight = 58f;

    // Palette (see the modify-ui skill): text on dark, gold highlight, parchment, damage red.
    private static readonly Color Dimmer = Hex("0D0D14B8");
    private static readonly Color WindowColor = Hex("16120EF5");
    private static readonly Color FrameGold = Hex("B8925AE6");
    private static readonly Color Gold = Hex("FFD170");
    private static readonly Color GoldMuted = Hex("C9A35E");
    private static readonly Color TextOnDark = Hex("D9D1BF");
    private static readonly Color TextDim = Hex("A3978A");
    private static readonly Color Parchment = Hex("D4C6A3");
    private static readonly Color TextOnParchment = Hex("54483D");
    private static readonly Color Well = Hex("0B0907D9");
    private static readonly Color DangerRed = Hex("A45845");

    private static Sprite keycapSliced, checkmark, gradientH, white, frameLarge, frameSmall, parchmentSmall, line, lineLeft, lineRight, diamond, triangle, gradientV;
    private static TMP_FontAsset headerFont, bodyFont;
    private static GameObject inputGlyphPrefab, hintEntryPrefab;
    /// <summary>Hover tick for the panel/row being built; Juice() assigns it to every control.</summary>
    private static MMF_Player currentHover;

    [MenuItem("Bladehold/UI/Rebuild Settings Panel (prefab + open scene)")]
    public static void RebuildAll()
    {
        LoadAssets();
        BuildRebindRowPrefab();

        GameObject root = PrefabUtility.LoadPrefabContents(PauseMenuPrefabPath);
        try
        {
            foreach (SettingsPanelView view in root.GetComponentsInChildren<SettingsPanelView>(true))
            {
                Rebuild(view, root.transform);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PauseMenuPrefabPath, out bool ok);
            Debug.Log($"[SettingsPanelBuilder] PauseMenuCanvas.prefab saved: {ok}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        RebuildOpenScene();
    }

    /// <summary>Rebuilds non-prefab SettingsPanelViews in the open scene (e.g. MainMenu's own copy).</summary>
    [MenuItem("Bladehold/UI/Rebuild Settings Panel (open scene only)")]
    public static void RebuildOpenScene()
    {
        LoadAssets();
        bool any = false;
        foreach (SettingsPanelView view in Object.FindObjectsByType<SettingsPanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(view))
            {
                continue; // comes from PauseMenuCanvas.prefab, already rebuilt there.
            }
            Rebuild(view, null);
            any = true;
        }
        if (any)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    // ───────────────────────────────────────────────── panel

    private static void Rebuild(SettingsPanelView view, Transform contextRoot)
    {
        Transform panel = view.transform;
        var remaps = CollectExternalReferences(panel, contextRoot);
        var listeners = CollectButtonListeners(panel);

        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(panel.GetChild(i).gameObject);
        }

        // Root: full-screen dimmer.
        var rootRt = (RectTransform)panel;
        Stretch(rootRt);
        Image rootImage = panel.GetComponent<Image>();
        if (rootImage == null) rootImage = panel.gameObject.AddComponent<Image>();
        rootImage.sprite = null;
        rootImage.color = Dimmer;
        rootImage.raycastTarget = true;
        foreach (LayoutGroup group in panel.GetComponents<LayoutGroup>())
        {
            Object.DestroyImmediate(group);
        }

        MMF_Player click = CloneClickFeedback(panel);
        currentHover = CloneHoverFeedback(panel);

        // Everything below is laid out in 1920×1080 units whatever the host canvas's reference.
        RectTransform space = NewUI("DesignSpace", panel);
        Stretch(space);
        space.gameObject.AddComponent<DesignResolutionScaler>();

        // Window: fixed width, stretches with screen height.
        RectTransform window = NewUI("Window", space);
        window.anchorMin = new Vector2(0.5f, 0f);
        window.anchorMax = new Vector2(0.5f, 1f);
        window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(WindowWidth, -80f);
        window.anchoredPosition = Vector2.zero;
        AddImage(window, white, WindowColor);
        RectTransform sheen = NewUI("Sheen", window);
        Stretch(sheen);
        AddImage(sheen, gradientV, Hex("FFD17008"), raycast: false);
        RectTransform frame = NewUI("Frame", window);
        Stretch(frame, -10f, -10f, -10f, -10f);
        AddImage(frame, frameLarge, FrameGold, sliced: true, ppuMultiplier: 2.2f, raycast: false);

        // Header: Back (left), title with flourishes (centre).
        RectTransform header = NewUI("Header", window);
        AnchorTop(header, 0f, 92f);
        TextMeshProUGUI title = Text(NewUI("Title", header), "Settings", headerFont, 44, Gold, TextAlignmentOptions.Center);
        Stretch(title.rectTransform);
        title.characterSpacing = 6f;
        title.fontStyle = FontStyles.UpperCase;
        Localize(title, "settings.title");
        RectTransform flourishL = NewUI("FlourishLeft", header);
        Place(flourishL, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, -2f), new Vector2(200f, 40f));
        AddImage(flourishL, lineLeft, GoldMuted, raycast: false).preserveAspect = true;
        RectTransform flourishR = NewUI("FlourishRight", header);
        Place(flourishR, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, -2f), new Vector2(200f, 40f));
        AddImage(flourishR, lineRight, GoldMuted, raycast: false).preserveAspect = true;

        Button back = BuildButton("BackButton", header, "settings.back", "< Back", ButtonStyle.Ghost, click);
        Place((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(170f, 50f));

        // Tab bar.
        RectTransform tabs = NewUI("TabsRow", window);
        AnchorTop(tabs, 96f, 60f);
        var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsLayout.spacing = 12f;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = false;
        tabsLayout.childForceExpandHeight = false;
        InputGlyph prevGlyph = Glyph("TabPrevGlyph", tabs, 44f);
        Button generalTab = BuildTab("GeneralTabButton", tabs, "settings.tab_general", "General", click);
        Button controlsTab = BuildTab("ControlsTabButton", tabs, "settings.tab_controls", "Controls", click);
        Button graphicsTab = BuildTab("GraphicsTabButton", tabs, "settings.tab_graphics", "Graphics", click);
        InputGlyph nextGlyph = Glyph("TabNextGlyph", tabs, 44f);

        RectTransform divider = NewUI("Divider", window);
        AnchorTop(divider, 162f, 10f, 48f);
        AddImage(divider, line, Hex("C9A35E80"), sliced: true, raycast: false);

        // Body: one scroll view per tab, between the tab bar and the footer.
        RectTransform body = NewUI("Body", window);
        Stretch(body, 40f, 40f, 178f, 104f);

        // General
        RectTransform general = ScrollTab("GeneralTabContent", body, out RectTransform generalList);
        Section(generalList, "settings.section_audio", "Audio");
        Slider master = SliderRow(generalList, "Master Volume", "settings.master_volume", 0f, 1f, false, 2, click);
        Slider music = SliderRow(generalList, "Music Volume", "settings.music_volume", 0f, 1f, false, 2, click);
        Slider sfx = SliderRow(generalList, "SFX Volume", "settings.sfx_volume", 0f, 1f, false, 2, click);
        Section(generalList, "settings.section_gameplay", "Gameplay");
        TMP_Dropdown language = DropdownRow(generalList, "Language", "settings.language", click);
        Slider gameSpeed = SliderRow(generalList, "Game Speed", "settings.game_speed", 0.1f, 2f, false, 1, click);
        Slider ragdolls = SliderRow(generalList, "Max Ragdolls", "settings.max_ragdolls", 0f, 50f, true, 0, click);

        // Controls
        RectTransform controls = ScrollTab("ControlsTabContent", body, out RectTransform controlsList);
        Section(controlsList, "settings.section_camera", "Camera");
        Slider sensitivity = SliderRow(controlsList, "Sensitivity", "settings.sensitivity", 0f, 10f, false, 1, click);
        Slider padSensitivity = SliderRow(controlsList, "Gamepad Look Sensitivity", "settings.gamepad_sensitivity", 30f, 360f, true, 0, click);
        Toggle invertX = ToggleRow(controlsList, "Invert X", "settings.invert_x", click);
        Toggle invertY = ToggleRow(controlsList, "Invert Y", "settings.invert_y", click);
        Section(controlsList, "settings.section_bindings", "Key Bindings");
        BindingHeaderRow(controlsList);
        RectTransform rebindList = NewUI("RebindList", controlsList);
        var rebindLayout = rebindList.gameObject.AddComponent<VerticalLayoutGroup>();
        rebindLayout.spacing = 4f;
        rebindLayout.childControlWidth = true;
        rebindLayout.childControlHeight = true;
        rebindLayout.childForceExpandWidth = true;
        rebindLayout.childForceExpandHeight = false;

        // Graphics
        RectTransform graphics = ScrollTab("GraphicsTabContent", body, out RectTransform graphicsList);
        Section(graphicsList, "settings.section_display", "Display");
        Slider fov = SliderRow(graphicsList, "Field of View", "settings.field_of_view", 30f, 100f, false, 0, click);
        Section(graphicsList, "settings.section_post", "Post Processing");
        Toggle ppEnabled = ToggleRow(graphicsList, "Post Processing", "settings.post_enabled", click);
        Slider bloom = SliderRow(graphicsList, "Bloom", "settings.bloom", 0f, 5f, false, 1, click);
        Slider vignette = SliderRow(graphicsList, "Vignette", "settings.vignette", 0f, 1f, false, 2, click);
        Slider exposure = SliderRow(graphicsList, "Exposure", "settings.exposure", -5f, 5f, false, 1, click);

        // Footer: pad hints (left), Reset / Delete (right).
        RectTransform footer = NewUI("Footer", window);
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.offsetMin = new Vector2(40f, 24f);
        footer.offsetMax = new Vector2(-40f, 90f);
        RectTransform footerLine = NewUI("Divider", footer);
        AnchorTop(footerLine, -6f, 10f, 8f);
        AddImage(footerLine, line, Hex("C9A35E60"), sliced: true, raycast: false);
        BuildHintBar(footer);
        Button reset = BuildButton("ResetSettingsButton", footer, "settings.reset_settings", "Reset Settings", ButtonStyle.Parchment, click);
        Place((RectTransform)reset.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-250f, -4f), new Vector2(230f, 52f));
        Button delete = BuildButton("DeleteSaveButton", footer, "settings.delete_save", "Delete Save", ButtonStyle.Danger, click);
        Place((RectTransform)delete.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(230f, 52f));

        ConfirmDialog dialog = BuildConfirmDialog(space, click);

        // Wire the view.
        var so = new SerializedObject(view);
        Set(so, "generalTabButton", generalTab);
        Set(so, "controlsTabButton", controlsTab);
        Set(so, "postProcessingTabButton", graphicsTab);
        Set(so, "generalTabContent", general.gameObject);
        Set(so, "controlsTabContent", controls.gameObject);
        Set(so, "postProcessingTabContent", graphics.gameObject);
        Set(so, "tabPrevGlyph", prevGlyph);
        Set(so, "tabNextGlyph", nextGlyph);
        Set(so, "focusController", view.GetComponent<MenuFocusController>());
        Set(so, "masterVolumeSlider", master);
        Set(so, "musicVolumeSlider", music);
        Set(so, "sfxVolumeSlider", sfx);
        Set(so, "sensitivitySlider", sensitivity);
        Set(so, "gamepadSensitivitySlider", padSensitivity);
        Set(so, "invertXToggle", invertX);
        Set(so, "invertYToggle", invertY);
        Set(so, "languageDropdown", language);
        Set(so, "fieldOfViewSlider", fov);
        Set(so, "maxRagdollsSlider", ragdolls);
        Set(so, "gameSpeedSlider", gameSpeed);
        Set(so, "rebindListParent", rebindList);
        Set(so, "rebindRowPrefab", AssetDatabase.LoadAssetAtPath<RebindButtonView>(RebindRowPrefabPath));
        Set(so, "rebindGridAbove", invertY);
        Set(so, "rebindGridBelow", reset);
        Set(so, "postProcessingEnabledToggle", ppEnabled);
        Set(so, "postProcessingBloomSlider", bloom);
        Set(so, "postProcessingVignetteSlider", vignette);
        Set(so, "postProcessingExposureSlider", exposure);
        Set(so, "resetSettingsButton", reset);
        Set(so, "deleteSaveButton", delete);
        Set(so, "confirmDialog", dialog);
        so.ApplyModifiedPropertiesWithoutUndo();

        MenuFocusController focus = view.GetComponent<MenuFocusController>();
        if (focus != null)
        {
            var fso = new SerializedObject(focus);
            Set(fso, "defaultSelectable", master);
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        ApplyExternalReferences(panel, remaps);
        ApplyButtonListeners(panel, listeners);
        EditorUtility.SetDirty(view);
        Debug.Log($"[SettingsPanelBuilder] Rebuilt '{panel.name}' ({remaps.Count} external refs, {listeners.Count} listener sets re-pointed).");
    }

    private static RectTransform ScrollTab(string name, RectTransform parent, out RectTransform list)
    {
        RectTransform tab = NewUI(name, parent);
        Stretch(tab);
        var scroll = tab.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;

        RectTransform viewport = NewUI("Viewport", tab);
        Stretch(viewport, 0f, 22f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddImage(viewport, white, new Color(0f, 0f, 0f, 0.001f)); // raycast target for wheel/drag over gaps

        list = NewUI("Content", viewport);
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0.5f, 1f);
        list.sizeDelta = Vector2.zero;
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 8, 4, 16);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Slim gold scrollbar.
        RectTransform bar = NewUI("Scrollbar", tab);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(8f, 0f);
        AddImage(bar, white, Hex("FFFFFF10"));
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        RectTransform area = NewUI("Sliding Area", bar);
        Stretch(area);
        RectTransform handle = NewUI("Handle", area);
        Stretch(handle);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = AddImage(handle, white, GoldMuted);
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scrollbar.transition = Selectable.Transition.ColorTint;
        scrollbar.colors = Tint(new Color(1f, 1f, 1f, 0.7f), Color.white, Color.white);

        scroll.content = list;
        scroll.viewport = viewport;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        tab.gameObject.AddComponent<ScrollRectAutoScroll>();
        return tab;
    }

    private static void Section(RectTransform list, string locKey, string english)
    {
        RectTransform section = NewUI("Section " + english, list);
        section.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
        TextMeshProUGUI text = Text(NewUI("Label", section), english, headerFont, 22, GoldMuted, TextAlignmentOptions.BottomLeft);
        Place(text.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4f, 8f), new Vector2(360f, 40f));
        text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.sizeDelta = new Vector2(360f, -10f);
        text.rectTransform.anchoredPosition = new Vector2(4f, 4f);
        text.characterSpacing = 4f;
        text.fontStyle = FontStyles.UpperCase;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        Localize(text, locKey);
        // Rule running from under the label to the right edge.
        RectTransform rule = NewUI("Rule", section);
        rule.anchorMin = new Vector2(0f, 0f);
        rule.anchorMax = new Vector2(1f, 0f);
        rule.pivot = new Vector2(0.5f, 0f);
        rule.sizeDelta = new Vector2(0f, 2f);
        rule.anchoredPosition = new Vector2(0f, 4f);
        AddImage(rule, white, Hex("C9A35E40"), raycast: false);
    }

    private static RectTransform Row(RectTransform list, string english, string locKey, out SettingsRowHighlight highlight, out TextMeshProUGUI label)
    {
        RectTransform row = NewUI("Row " + english, list);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
        Image bg = AddImage(row, gradientH, Hex("FFD17038"));
        RectTransform accent = NewUI("Accent", row);
        accent.anchorMin = new Vector2(0f, 0.12f);
        accent.anchorMax = new Vector2(0f, 0.88f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.sizeDelta = new Vector2(4f, 0f);
        Image accentImage = AddImage(accent, white, Gold, raycast: false);
        label = Text(NewUI("Label", row), english, bodyFont, 26, TextOnDark, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(0.42f, 1f);
        label.rectTransform.offsetMin = new Vector2(20f, 0f);
        label.rectTransform.offsetMax = new Vector2(0f, 0f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Localize(label, locKey);
        highlight = row.gameObject.AddComponent<SettingsRowHighlight>();
        highlight.Configure(bg, accentImage, label, null);
        return row;
    }

    private static RectTransform ControlArea(RectTransform row)
    {
        RectTransform area = NewUI("Control", row);
        area.anchorMin = new Vector2(0.44f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.offsetMin = new Vector2(0f, 0f);
        area.offsetMax = new Vector2(-16f, 0f);
        return area;
    }

    private static Slider SliderRow(RectTransform list, string english, string locKey, float min, float max, bool whole, int decimals, MMF_Player click)
    {
        RectTransform row = Row(list, english, locKey, out SettingsRowHighlight highlight, out TextMeshProUGUI _);
        RectTransform area = ControlArea(row);

        RectTransform sliderRt = NewUI("Slider", area);
        sliderRt.anchorMin = new Vector2(0f, 0.5f);
        sliderRt.anchorMax = new Vector2(1f, 0.5f);
        sliderRt.offsetMin = new Vector2(10f, -16f);
        sliderRt.offsetMax = new Vector2(-118f, 16f);
        AddImage(sliderRt, white, new Color(0f, 0f, 0f, 0.001f)); // wider grab area than the track

        RectTransform track = NewUI("Track", sliderRt);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 10f);
        AddImage(track, white, Well, raycast: false);
        RectTransform trackEdge = NewUI("Edge", track);
        Stretch(trackEdge, -1f, -1f, -1f, -1f);
        AddImage(trackEdge, white, Hex("C9A35E30"), raycast: false).transform.SetAsFirstSibling();

        RectTransform fillArea = NewUI("Fill Area", sliderRt);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 10f);
        RectTransform fill = NewUI("Fill", fillArea);
        Stretch(fill);
        AddImage(fill, white, GoldMuted, raycast: false);

        RectTransform handleArea = NewUI("Handle Slide Area", sliderRt);
        Stretch(handleArea);
        RectTransform handle = NewUI("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(30f, 0f);
        RectTransform handleVisual = NewUI("Diamond", handle);
        Place(handleVisual, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
        Image handleImage = AddImage(handleVisual, diamond, Gold, raycast: false);
        handleImage.preserveAspect = true;
        RectTransform handleGlow = NewUI("Glow", handleVisual);
        Place(handleGlow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
        handleGlow.SetAsFirstSibling();
        Image glow = AddImage(handleGlow, diamond, Hex("FFD17066"), raycast: false);
        glow.preserveAspect = true;
        // Handle needs its own raycast target for dragging.
        AddImage(handle, white, new Color(0f, 0f, 0f, 0.001f));

        var slider = sliderRt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.value = Mathf.Lerp(min, max, 0.5f);
        slider.transition = Selectable.Transition.ColorTint;
        slider.colors = Tint(Color.white, Hex("FFF1CC"), Hex("E0B860"));
        Juice(slider, handleVisual, glow, null, 1.15f, 0.95f);

        // Value field.
        TMP_InputField field = BuildValueField(area);
        var sync = row.gameObject.AddComponent<SliderValueField>();
        var sso = new SerializedObject(sync);
        Set(sso, "slider", slider);
        Set(sso, "inputField", field);
        Set(sso, "decimalPlaces", decimals);
        sso.ApplyModifiedPropertiesWithoutUndo();

        highlight.Configure(row.GetComponent<Image>(), row.Find("Accent").GetComponent<Image>(), row.Find("Label").GetComponent<TMP_Text>(), slider);
        return slider;
    }

    private static TMP_InputField BuildValueField(RectTransform area)
    {
        RectTransform fieldRt = NewUI("Value", area);
        Place(fieldRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(96f, 40f));
        Image bg = AddImage(fieldRt, white, Well);
        RectTransform edge = NewUI("Frame", fieldRt);
        Stretch(edge, -4f, -4f, -4f, -4f);
        AddImage(edge, frameSmall, Hex("C9A35E70"), sliced: true, ppuMultiplier: 4f, raycast: false);
        RectTransform textArea = NewUI("Text Area", fieldRt);
        Stretch(textArea, 8f, 8f, 4f, 4f);
        textArea.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = Text(NewUI("Placeholder", textArea), "", bodyFont, 22, TextDim, TextAlignmentOptions.Center);
        Stretch(placeholder.rectTransform);
        TextMeshProUGUI text = Text(NewUI("Text", textArea), "", bodyFont, 22, TextOnDark, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        var field = fieldRt.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = textArea;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.targetGraphic = bg;
        field.contentType = TMP_InputField.ContentType.DecimalNumber;
        field.caretColor = Gold;
        field.customCaretColor = true;
        field.selectionColor = Hex("FFD17055");
        field.navigation = new Navigation { mode = Navigation.Mode.None }; // pads adjust via the slider
        field.colors = Tint(Color.white, Hex("FFF6E0"), Hex("FFE8B0"));
        field.fontAsset = bodyFont;
        field.pointSize = 22;
        return field;
    }

    private static Toggle ToggleRow(RectTransform list, string english, string locKey, MMF_Player click)
    {
        RectTransform row = Row(list, english, locKey, out SettingsRowHighlight highlight, out TextMeshProUGUI label);
        RectTransform area = ControlArea(row);

        RectTransform toggleRt = NewUI("Toggle", area);
        Place(toggleRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(44f, 44f));
        // Checkbox: dark well in a gold frame, gold diamond when on.
        Image well = AddImage(toggleRt, white, Well);
        RectTransform frame = NewUI("Frame", toggleRt);
        Stretch(frame, -4f, -4f, -4f, -4f);
        AddImage(frame, frameSmall, Hex("C9A35E90"), sliced: true, ppuMultiplier: 4f, raycast: false);
        RectTransform hot = NewUI("HoverFrame", toggleRt);
        Stretch(hot, -4f, -4f, -4f, -4f);
        Image hotImage = AddImage(hot, frameSmall, Gold, sliced: true, ppuMultiplier: 4f, raycast: false);
        RectTransform check = NewUI("Check", toggleRt);
        Place(check, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
        Image checkImage = AddImage(check, checkmark, Gold, raycast: false);
        checkImage.preserveAspect = true;

        var toggle = toggleRt.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = well;
        toggle.graphic = checkImage;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;
        toggle.colors = Tint(Color.white, Hex("FFF6E0"), Hex("FFE8B0"));
        toggle.isOn = false;
        Juice(toggle, toggleRt, hotImage, click, 1.1f, 0.9f);

        highlight.Configure(row.GetComponent<Image>(), row.Find("Accent").GetComponent<Image>(), label, toggle);
        return toggle;
    }

    private static TMP_Dropdown DropdownRow(RectTransform list, string english, string locKey, MMF_Player click)
    {
        RectTransform row = Row(list, english, locKey, out SettingsRowHighlight highlight, out TextMeshProUGUI label);
        RectTransform area = ControlArea(row);

        RectTransform ddRt = NewUI("Dropdown", area);
        ddRt.anchorMin = new Vector2(0f, 0.5f);
        ddRt.anchorMax = new Vector2(1f, 0.5f);
        ddRt.offsetMin = new Vector2(10f, -21f);
        ddRt.offsetMax = new Vector2(0f, 21f);
        Image bg = AddImage(ddRt, white, Well);
        RectTransform frame = NewUI("Frame", ddRt);
        Stretch(frame, -4f, -4f, -4f, -4f);
        Image frameImage = AddImage(frame, frameSmall, Hex("C9A35E90"), sliced: true, ppuMultiplier: 4f, raycast: false);
        TextMeshProUGUI caption = Text(NewUI("Label", ddRt), "Auto (System)", bodyFont, 24, TextOnDark, TextAlignmentOptions.Left);
        Stretch(caption.rectTransform, 16f, 44f, 0f, 0f);
        RectTransform arrow = NewUI("Arrow", ddRt);
        Place(arrow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(18f, 18f));
        AddImage(arrow, triangle, Gold, raycast: false).preserveAspect = true;

        // Template list.
        RectTransform template = NewUI("Template", ddRt);
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.sizeDelta = new Vector2(0f, 300f);
        template.anchoredPosition = new Vector2(0f, -4f);
        AddImage(template, white, Hex("120F0CFA"));
        var templateScroll = template.gameObject.AddComponent<ScrollRect>();
        templateScroll.horizontal = false;
        templateScroll.movementType = ScrollRect.MovementType.Clamped;
        templateScroll.scrollSensitivity = 30f;
        RectTransform tFrame = NewUI("Frame", template);
        Stretch(tFrame, -4f, -4f, -4f, -4f);
        AddImage(tFrame, frameSmall, Hex("C9A35EB0"), sliced: true, ppuMultiplier: 4f, raycast: false);
        RectTransform viewport = NewUI("Viewport", template);
        Stretch(viewport, 4f, 4f, 4f, 4f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = NewUI("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 44f);
        RectTransform item = NewUI("Item", content);
        item.anchorMin = new Vector2(0f, 0.5f);
        item.anchorMax = new Vector2(1f, 0.5f);
        item.sizeDelta = new Vector2(0f, 44f);
        RectTransform itemBg = NewUI("Item Background", item);
        Stretch(itemBg);
        Image itemBgImage = AddImage(itemBg, white, Color.white);
        RectTransform check = NewUI("Item Checkmark", item);
        Place(check, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(16f, 16f));
        AddImage(check, diamond, Gold, raycast: false).preserveAspect = true;
        TextMeshProUGUI itemLabel = Text(NewUI("Item Label", item), "Option", bodyFont, 24, TextOnDark, TextAlignmentOptions.Left);
        Stretch(itemLabel.rectTransform, 40f, 10f, 0f, 0f);
        var itemToggle = item.gameObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBgImage;
        itemToggle.graphic = check.GetComponent<Image>();
        itemToggle.isOn = true;
        itemToggle.colors = Tint(Hex("FFFFFF00"), Hex("FFD17033"), Hex("FFD17055"), Hex("FFD17026"));
        templateScroll.content = content;
        templateScroll.viewport = viewport;
        template.gameObject.SetActive(false);

        var dropdown = ddRt.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = bg;
        dropdown.template = template;
        dropdown.captionText = caption;
        dropdown.itemText = itemLabel;
        dropdown.colors = Tint(Color.white, Hex("FFF6E0"), Hex("FFE8B0"));
        Juice(dropdown, ddRt, frameImage, click, 1.02f, 0.98f);
        // Juice fades the frame from 0 — keep a base frame visible underneath.
        RectTransform baseFrame = NewUI("BaseFrame", ddRt);
        Stretch(baseFrame, -4f, -4f, -4f, -4f);
        baseFrame.SetSiblingIndex(1);
        AddImage(baseFrame, frameSmall, Hex("C9A35E50"), sliced: true, ppuMultiplier: 4f, raycast: false);

        highlight.Configure(row.GetComponent<Image>(), row.Find("Accent").GetComponent<Image>(), label, dropdown);
        return dropdown;
    }

    private static void BindingHeaderRow(RectTransform list)
    {
        RectTransform row = NewUI("BindingHeader", list);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        HeaderCell(row, "ActionHeader", "settings.header_action", "Action", 0f, RebindLabelMax, TextAlignmentOptions.Left, 20f);
        HeaderCell(row, "KbmHeader", "settings.header_kbm", "Keyboard / Mouse", RebindKbmMin, RebindKbmMax, TextAlignmentOptions.Center, 0f);
        HeaderCell(row, "GamepadHeader", "settings.header_gamepad", "Gamepad", RebindPadMin, RebindPadMax, TextAlignmentOptions.Center, 0f);
    }

    private static void HeaderCell(RectTransform row, string name, string key, string english, float min, float max, TextAlignmentOptions align, float inset)
    {
        TextMeshProUGUI text = Text(NewUI(name, row), english, headerFont, 18, TextDim, align);
        text.rectTransform.anchorMin = new Vector2(min, 0f);
        text.rectTransform.anchorMax = new Vector2(max, 1f);
        text.rectTransform.offsetMin = new Vector2(inset, 0f);
        text.rectTransform.offsetMax = Vector2.zero;
        text.characterSpacing = 3f;
        text.fontStyle = FontStyles.UpperCase;
        Localize(text, key);
    }

    private const float RebindLabelMax = 0.36f;
    private const float RebindKbmMin = 0.38f;
    private const float RebindKbmMax = 0.67f;
    private const float RebindPadMin = 0.69f;
    private const float RebindPadMax = 0.98f;

    private static void BuildHintBar(RectTransform footer)
    {
        RectTransform bar = NewUI("ControlHints", footer);
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0f, 0.5f); // scale from the left edge
        bar.offsetMin = new Vector2(0f, 0f);
        bar.offsetMax = new Vector2(0f, -8f);
        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        // HintEntry rows carry a fixed 160px label; overlap that slack so three hints clear Reset/Delete.
        layout.spacing = -36f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        bar.localScale = new Vector3(0.85f, 0.85f, 1f);
        var hints = bar.gameObject.AddComponent<ControlHintBar>();
        var so = new SerializedObject(hints);
        Set(so, "entryPrefab", hintEntryPrefab.GetComponent<HintEntryView>());
        SerializedProperty entries = so.FindProperty("entries");
        string[,] data =
        {
            { "", "<Gamepad>/leftStick", "hint.navigate", "Navigate" },
            { "", "<Gamepad>/buttonSouth", "hint.select", "Select" },
            { "", "<Gamepad>/buttonEast", "hint.back", "Back" },
        };
        entries.arraySize = data.GetLength(0);
        for (int i = 0; i < data.GetLength(0); i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("actionName").stringValue = "";
            e.FindPropertyRelative("kbmPath").stringValue = data[i, 0];
            e.FindPropertyRelative("gamepadPath").stringValue = data[i, 1];
            e.FindPropertyRelative("locKey").stringValue = data[i, 2];
            e.FindPropertyRelative("english").stringValue = data[i, 3];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ConfirmDialog BuildConfirmDialog(Transform panel, MMF_Player click)
    {
        RectTransform root = NewUI("ConfirmDialogRoot", panel);
        Stretch(root);
        AddImage(root, white, Hex("0D0D14C0"));
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform box = NewUI("Box", root);
        Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 270f));
        AddImage(box, white, WindowColor);
        RectTransform frame = NewUI("Frame", box);
        Stretch(frame, -10f, -10f, -10f, -10f);
        AddImage(frame, frameLarge, FrameGold, sliced: true, ppuMultiplier: 2.2f, raycast: false);

        TextMeshProUGUI message = Text(NewUI("MessageText", box), "Are you sure?", bodyFont, 28, TextOnDark, TextAlignmentOptions.Center);
        Stretch(message.rectTransform, 40f, 40f, 36f, 110f);
        message.enableAutoSizing = true;
        message.fontSizeMin = 18f;
        message.fontSizeMax = 28f;
        Button confirm = BuildButton("ConfirmButton", box, null, "Confirm", ButtonStyle.Danger, click);
        Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 36f), new Vector2(230f, 54f));
        Button cancel = BuildButton("CancelButton", box, "settings.cancel", "Cancel", ButtonStyle.Parchment, click);
        Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(12f, 36f), new Vector2(230f, 54f));

        var dialog = root.gameObject.AddComponent<ConfirmDialog>();
        var dso = new SerializedObject(dialog);
        Set(dso, "canvasGroup", group);
        Set(dso, "messageText", message);
        Set(dso, "confirmButton", confirm);
        Set(dso, "cancelButton", cancel);
        dso.ApplyModifiedPropertiesWithoutUndo();

        var focus = root.gameObject.AddComponent<MenuFocusController>();
        var fso = new SerializedObject(focus);
        Set(fso, "defaultSelectable", cancel);
        Set(fso, "restrictTo", box);
        fso.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddVoidPersistentListener(GetCancelEvent(focus), dialog.Cancel);
        return dialog;
    }

    private static UnityEvent GetCancelEvent(MenuFocusController focus)
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

    // ───────────────────────────────────────────────── rebind row prefab

    private static void BuildRebindRowPrefab()
    {
        var go = new GameObject("RebindRow", typeof(RectTransform));
        try
        {
            var row = (RectTransform)go.transform;
            row.sizeDelta = new Vector2(1000f, 56f);
            go.AddComponent<LayoutElement>().preferredHeight = 56f;
            Image bg = AddImage(row, gradientH, Hex("FFD17038"));
            RectTransform accent = NewUI("Accent", row);
            accent.anchorMin = new Vector2(0f, 0.12f);
            accent.anchorMax = new Vector2(0f, 0.88f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(4f, 0f);
            Image accentImage = AddImage(accent, white, Gold, raycast: false);

            TextMeshProUGUI label = Text(NewUI("Label", row), "Action", bodyFont, 24, TextOnDark, TextAlignmentOptions.Left);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(RebindLabelMax, 1f);
            label.rectTransform.offsetMin = new Vector2(20f, 0f);
            label.rectTransform.offsetMax = Vector2.zero;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            MMF_Player click = CloneClickFeedback(row);
            currentHover = CloneHoverFeedback(row);
            Button kbm = RebindColumn(row, "KbmButton", RebindKbmMin, RebindKbmMax, click, out TextMeshProUGUI kbmText, out InputGlyph kbmGlyph);
            Button pad = RebindColumn(row, "GamepadButton", RebindPadMin, RebindPadMax, click, out TextMeshProUGUI padText, out InputGlyph padGlyph);

            var highlight = go.AddComponent<SettingsRowHighlight>();
            highlight.Configure(bg, accentImage, label, kbm);

            var view = go.AddComponent<RebindButtonView>();
            var so = new SerializedObject(view);
            Set(so, "label", label);
            Set(so, "kbmBindingPathLabel", kbmText);
            Set(so, "kbmButton", kbm);
            Set(so, "kbmGlyph", kbmGlyph);
            Set(so, "gamepadBindingPathLabel", padText);
            Set(so, "gamepadButton", pad);
            Set(so, "gamepadGlyph", padGlyph);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, RebindRowPrefabPath, out bool ok);
            Debug.Log($"[SettingsPanelBuilder] RebindRow.prefab saved: {ok}");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static Button RebindColumn(RectTransform row, string name, float min, float max, MMF_Player click, out TextMeshProUGUI text, out InputGlyph glyph)
    {
        RectTransform col = NewUI(name, row);
        col.anchorMin = new Vector2(min, 0.5f);
        col.anchorMax = new Vector2(max, 0.5f);
        col.sizeDelta = new Vector2(0f, 46f);
        RectTransform visual = NewUI("Visual", col);
        Stretch(visual);
        Image bg = AddImage(visual, white, Well, raycast: false);
        RectTransform frame = NewUI("Frame", visual);
        Stretch(frame, -4f, -4f, -4f, -4f);
        AddImage(frame, frameSmall, Hex("C9A35E2E"), sliced: true, ppuMultiplier: 4f, raycast: false);
        RectTransform hot = NewUI("HoverFrame", visual);
        Stretch(hot, -4f, -4f, -4f, -4f);
        Image hotImage = AddImage(hot, frameSmall, Gold, sliced: true, ppuMultiplier: 4f, raycast: false);

        glyph = Glyph("Glyph", visual, 36f, stretchKeycap: true);
        RectTransform glyphRt = (RectTransform)glyph.transform;
        glyphRt.anchorMin = glyphRt.anchorMax = new Vector2(0.5f, 0.5f);
        glyphRt.anchoredPosition = Vector2.zero;

        text = Text(NewUI("PathLabel", visual), "—", bodyFont, 22, TextOnDark, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8f, 8f, 0f, 0f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        // Raycast target on the (unscaled) column so the hit area doesn't wobble with the juice.
        AddImage(col, white, new Color(0f, 0f, 0f, 0.001f));
        var button = col.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.colors = Tint(Color.white, Hex("FFF1CC"), Hex("FFE0A0"), Hex("FFFFFF59"));
        Juice(button, visual, hotImage, click, 1.05f, 0.95f);
        return button;
    }

    // ───────────────────────────────────────────────── buttons

    private enum ButtonStyle { Parchment, Danger, Ghost }

    private static Button BuildTab(string name, RectTransform parent, string key, string english, MMF_Player click)
    {
        RectTransform tab = NewUI(name, parent);
        var le = tab.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 230f;
        le.preferredHeight = 52f;
        AddImage(tab, white, new Color(0f, 0f, 0f, 0.001f));
        RectTransform visual = NewUI("Visual", tab);
        Stretch(visual);
        Image bg = AddImage(visual, parchmentSmall, Parchment, sliced: true, ppuMultiplier: 2f, raycast: false);
        RectTransform hot = NewUI("HoverGlow", visual);
        Stretch(hot, -4f, -4f, -4f, -4f);
        Image hotImage = AddImage(hot, frameSmall, Gold, sliced: true, ppuMultiplier: 4f, raycast: false);
        TextMeshProUGUI label = Text(NewUI("Label", visual), english, headerFont, 24, TextOnDark, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        Localize(label, key);
        RectTransform underline = NewUI("Underline", visual);
        underline.anchorMin = new Vector2(0.1f, 0f);
        underline.anchorMax = new Vector2(0.9f, 0f);
        underline.pivot = new Vector2(0.5f, 0.5f);
        underline.sizeDelta = new Vector2(0f, 4f);
        underline.anchoredPosition = new Vector2(0f, -8f);
        AddImage(underline, white, Gold, raycast: false);

        var button = tab.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.colors = Tint(Color.white, Hex("FFF8E8"), Hex("E8DCC0"));
        var styled = tab.gameObject.AddComponent<SettingsTabButton>();
        var so = new SerializedObject(styled);
        Set(so, "background", bg);
        Set(so, "label", label);
        Set(so, "underline", underline);
        Set(so, "backgroundSelected", Parchment);
        Set(so, "backgroundUnselected", Hex("2A241ECC"));
        Set(so, "labelSelected", TextOnParchment);
        Set(so, "labelUnselected", TextOnDark);
        so.ApplyModifiedPropertiesWithoutUndo();
        Juice(button, visual, hotImage, click, 1.05f, 0.95f);
        return button;
    }

    private static Button BuildButton(string name, RectTransform parent, string key, string english, ButtonStyle style, MMF_Player click)
    {
        RectTransform rt = NewUI(name, parent);
        AddImage(rt, white, new Color(0f, 0f, 0f, 0.001f));
        RectTransform visual = NewUI("Visual", rt);
        Stretch(visual);
        Color fill = style == ButtonStyle.Danger ? DangerRed : style == ButtonStyle.Ghost ? Hex("2A241ECC") : Parchment;
        Color textColor = style == ButtonStyle.Parchment ? TextOnParchment : TextOnDark;
        Image bg = style == ButtonStyle.Ghost
            ? AddImage(visual, white, fill, raycast: false)
            : AddImage(visual, parchmentSmall, fill, sliced: true, ppuMultiplier: 2f, raycast: false);
        RectTransform hot = NewUI("HoverGlow", visual);
        Stretch(hot, -4f, -4f, -4f, -4f);
        Image hotImage = AddImage(hot, frameSmall, Gold, sliced: true, ppuMultiplier: 4f, raycast: false);
        TextMeshProUGUI label = Text(NewUI("Label", visual), english, headerFont, 22, textColor, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 8f, 8f, 0f, 0f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 22f;
        if (!string.IsNullOrEmpty(key))
        {
            Localize(label, key);
        }

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.colors = Tint(Color.white, Hex("FFF8E8"), Hex("D8CCB0"));
        Juice(button, visual, hotImage, click, 1.05f, 0.95f);
        return button;
    }

    // ───────────────────────────────────────────────── juice / feedback

    private static void Juice(Selectable selectable, RectTransform scaleTarget, Graphic highlight, MMF_Player click, float hover, float press)
    {
        var juice = selectable.gameObject.AddComponent<UISelectableJuice>();
        var so = new SerializedObject(juice);
        Set(so, "selectable", selectable);
        Set(so, "scaleTarget", scaleTarget);
        Set(so, "highlight", highlight);
        Set(so, "clickFeedback", click);
        Set(so, "hoverFeedback", currentHover);
        Set(so, "hoverScale", hover);
        Set(so, "pressScale", press);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Copies MenuButton.prefab's authored click MMF (shared UI click sound) under the given parent.</summary>
    private static MMF_Player CloneClickFeedback(Transform parent)
    {
        GameObject menuButton = AssetDatabase.LoadAssetAtPath<GameObject>(MenuButtonPrefabPath);
        MMF_Player source = menuButton != null ? menuButton.GetComponentInChildren<MMF_Player>(true) : null;
        if (source == null)
        {
            Debug.LogWarning("[SettingsPanelBuilder] MenuButton.prefab has no click MMF_Player; controls will be silent.");
            return null;
        }
        GameObject copy = Object.Instantiate(source.gameObject, parent);
        copy.name = "ClickMMF";
        return copy.GetComponent<MMF_Player>();
    }

    /// <summary>
    ///     The hover tick: the shared UI click played quiet and pitched up, with a short cooldown so
    ///     sweeping the mouse down a list ticks rather than buzzes. Swap the clip on the generated
    ///     HoverMMF (or here) when a dedicated hover sound exists.
    /// </summary>
    private static MMF_Player CloneHoverFeedback(Transform parent)
    {
        MMF_Player hover = CloneClickFeedback(parent);
        if (hover == null)
        {
            return null;
        }
        hover.gameObject.name = "HoverMMF";
        hover.CooldownDuration = 0.06f;
        foreach (MMF_Feedback feedback in hover.FeedbacksList)
        {
            if (feedback is MMF_Sound sound)
            {
                sound.MinVolume = 0.3f;
                sound.MaxVolume = 0.35f;
                sound.MinPitch = 1.55f;
                sound.MaxPitch = 1.7f;
            }
        }
        return hover;
    }

    private static InputGlyph Glyph(string name, Transform parent, float size, bool stretchKeycap = false)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(inputGlyphPrefab, parent);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        // Synty glyphs are white silhouettes: warm them slightly, and put dark key names on the light keycap.
        var image = go.GetComponent<Image>();
        image.color = Hex("EDE4CF");
        image.pixelsPerUnitMultiplier = 2.6f;
        var overlay = go.GetComponentInChildren<TMP_Text>(true);
        if (overlay != null)
        {
            overlay.color = Hex("221C16");
            overlay.font = bodyFont;
            overlay.fontSizeMax = size * 0.55f;
            overlay.textWrappingMode = TextWrappingModes.NoWrap;
        }
        var glyph = go.GetComponent<InputGlyph>();
        if (stretchKeycap)
        {
            var so = new SerializedObject(glyph);
            Set(so, "stretchKeycap", keycapSliced);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return glyph;
    }

    // ───────────────────────────────────────────────── external references

    private struct RefRemap
    {
        public Object owner;
        public string propertyPath;
        public string targetName;
        public System.Type targetType;
    }

    private static List<RefRemap> CollectExternalReferences(Transform panel, Transform contextRoot)
    {
        var result = new List<RefRemap>();
        IEnumerable<Component> components = contextRoot != null
            ? contextRoot.GetComponentsInChildren<Component>(true)
            : (IEnumerable<Component>)Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Component c in components)
        {
            if (c == null || c is Transform || c.transform.IsChildOf(panel))
            {
                continue;
            }
            var so = new SerializedObject(c);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null)
                {
                    continue;
                }
                Object o = p.objectReferenceValue;
                Transform t = o is Component comp ? comp.transform : o is GameObject g ? g.transform : null;
                if (t != null && t != panel && t.IsChildOf(panel))
                {
                    result.Add(new RefRemap { owner = c, propertyPath = p.propertyPath, targetName = t.name, targetType = o.GetType() });
                }
            }
        }
        return result;
    }

    private static void ApplyExternalReferences(Transform panel, List<RefRemap> remaps)
    {
        foreach (RefRemap remap in remaps)
        {
            Transform match = FindDeep(panel, remap.targetName);
            Object replacement = null;
            if (match != null)
            {
                replacement = remap.targetType == typeof(GameObject) ? match.gameObject : (Object)match.GetComponent(remap.targetType);
            }
            var so = new SerializedObject(remap.owner);
            so.FindProperty(remap.propertyPath).objectReferenceValue = replacement;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (replacement == null)
            {
                Debug.LogWarning($"[SettingsPanelBuilder] {remap.owner.name}.{remap.propertyPath} pointed at '{remap.targetName}', which the new layout doesn't have — now null.", remap.owner);
            }
        }
    }

    private struct ListenerCopy
    {
        public string buttonName;
        public List<KeyValuePair<Object, string>> calls;
    }

    private static List<ListenerCopy> CollectButtonListeners(Transform panel)
    {
        var result = new List<ListenerCopy>();
        foreach (Button b in panel.GetComponentsInChildren<Button>(true))
        {
            int count = b.onClick.GetPersistentEventCount();
            if (count == 0)
            {
                continue;
            }
            var calls = new List<KeyValuePair<Object, string>>();
            for (int i = 0; i < count; i++)
            {
                Object target = b.onClick.GetPersistentTarget(i);
                // Calls into the panel itself would point at destroyed objects — only keep outward ones.
                Transform t = target is Component comp ? comp.transform : target is GameObject g ? g.transform : null;
                if (target != null && (t == null || !t.IsChildOf(panel) || t == panel))
                {
                    calls.Add(new KeyValuePair<Object, string>(target, b.onClick.GetPersistentMethodName(i)));
                }
            }
            if (calls.Count > 0)
            {
                result.Add(new ListenerCopy { buttonName = b.name, calls = calls });
            }
        }
        return result;
    }

    private static void ApplyButtonListeners(Transform panel, List<ListenerCopy> listeners)
    {
        foreach (ListenerCopy copy in listeners)
        {
            Transform match = FindDeep(panel, copy.buttonName);
            Button button = match != null ? match.GetComponent<Button>() : null;
            foreach (KeyValuePair<Object, string> call in copy.calls)
            {
                var method = call.Key.GetType().GetMethod(call.Value, System.Type.EmptyTypes);
                if (button == null || method == null)
                {
                    Debug.LogWarning($"[SettingsPanelBuilder] Couldn't carry over onClick {call.Key.name}.{call.Value} on '{copy.buttonName}' — rewire it by hand.");
                    continue;
                }
                var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), call.Key, method);
                UnityEventTools.AddVoidPersistentListener(button.onClick, action);
            }
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name && t != root)
            {
                return t;
            }
        }
        return null;
    }

    // ───────────────────────────────────────────────── primitives

    private static void LoadAssets()
    {
        white = LoadSprite("Box_White_01");
        frameLarge = LoadSprite("Frame_Box_Medium_02");
        frameSmall = LoadSprite("Frame_Box_Small_01");
        parchmentSmall = LoadSprite("Box_Small_Parchment_01");
        line = LoadSprite("Line_01");
        lineLeft = LoadSprite("Line_04_Left");
        lineRight = LoadSprite("Line_04_Right");
        diamond = AssetDatabase.LoadAssetAtPath<Sprite>(HudSprites + "Mask_Diamond_01.png");
        triangle = LoadSprite("Triangle_Small_01_Clean");
        gradientV = LoadSprite("Gradient_Vertical_Smooth_01");
        keycapSliced = LoadKeycap();
        checkmark = AssetDatabase.LoadAssetAtPath<Sprite>(CheckmarkPath);
        gradientH = LoadSprite("Gradient_Horizontal_Smooth_01");
        headerFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeaderFontPath);
        bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        inputGlyphPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InputGlyphPrefabPath);
        hintEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HintEntryPrefabPath);
    }

    /// <summary>The generated 9-slice keycap (rounded white face with a darker lip) — imports it as a bordered sprite on first use.</summary>
    private static Sprite LoadKeycap()
    {
        var importer = AssetImporter.GetAtPath(KeycapPath) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != new Vector4(20f, 22f, 20f, 18f)))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(20f, 22f, 20f, 18f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(KeycapPath);
    }

    private static Sprite LoadSprite(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudSprites + name + ".png");
        if (sprite == null)
        {
            Debug.LogWarning($"[SettingsPanelBuilder] Missing sprite {name}.");
        }
        return sprite;
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image AddImage(Component target, Sprite sprite, Color color, bool sliced = false, float ppuMultiplier = 1f, bool raycast = true)
    {
        var image = target.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = ppuMultiplier;
        image.raycastTarget = raycast;
        return image;
    }

    private static TextMeshProUGUI Text(RectTransform rt, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Localize(TMP_Text text, string key)
    {
        var loc = text.gameObject.AddComponent<LocalizedText>();
        var so = new SerializedObject(loc);
        Set(so, "key", key);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Full-width strip hanging from the top edge, <paramref name="top" /> down, <paramref name="height" /> tall.</summary>
    private static void AnchorTop(RectTransform rt, float top, float height, float sideInset = 0f)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(sideInset, -top - height);
        rt.offsetMax = new Vector2(-sideInset, -top);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static ColorBlock Tint(Color normal, Color highlighted, Color pressed, Color? disabled = null)
    {
        ColorBlock block = ColorBlock.defaultColorBlock;
        block.normalColor = normal;
        block.highlightedColor = highlighted;
        block.selectedColor = highlighted;
        block.pressedColor = pressed;
        block.disabledColor = disabled ?? new Color(1f, 1f, 1f, 0.35f);
        block.fadeDuration = 0.08f;
        return block;
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[SettingsPanelBuilder] No field '{field}' on {so.targetObject.GetType().Name}."); return; }
        p.objectReferenceValue = value;
    }

    private static void Set(SerializedObject so, string field, string value) { SerializedProperty p = so.FindProperty(field); if (p != null) p.stringValue = value; else Debug.LogError($"[SettingsPanelBuilder] No field '{field}'."); }
    private static void Set(SerializedObject so, string field, int value) { SerializedProperty p = so.FindProperty(field); if (p != null) p.intValue = value; else Debug.LogError($"[SettingsPanelBuilder] No field '{field}'."); }
    private static void Set(SerializedObject so, string field, float value) { SerializedProperty p = so.FindProperty(field); if (p != null) p.floatValue = value; else Debug.LogError($"[SettingsPanelBuilder] No field '{field}'."); }
    private static void Set(SerializedObject so, string field, Color value) { SerializedProperty p = so.FindProperty(field); if (p != null) p.colorValue = value; else Debug.LogError($"[SettingsPanelBuilder] No field '{field}'."); }
}
