using System.IO;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using Unity.Cinemachine;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
///     Editor-time generator for the pause/settings/Photo Mode UI (Bladehold > Generate Settings Menu).
///     Builds a functional (unstyled) hierarchy wired up to the runtime scripts in
///     <c>Assets/Bladehold/Bladehold Scripts/Settings/</c> and <c>UI/</c>, so the remaining work is
///     purely visual — reskin the six shared control prefabs it creates under
///     <c>Assets/Bladehold/Bladehold Prefabs/UI/</c> (<c>MenuButton</c>/<c>MenuLabel</c>/
///     <c>MenuSlider</c>/<c>MenuToggle</c>/<c>MenuIconButton</c>/<c>MenuValueInput</c>) and every
///     control in the generated menu — being an instance of one of those prefabs — picks up the
///     change everywhere at once (the icon button's circular-arrow sprite is generated too, as
///     <c>ResetIcon.png</c>). Every Settings-panel slider row also gets a <c>MenuValueInput</c> text
///     field kept in sync by <see cref="SliderValueField" />, so exact numbers can be typed in
///     instead of dragging. Re-running the command is a no-op if a "PauseMenuCanvas" already exists in
///     the scene (delete it, and "GameMenu" if starting fully fresh, to regenerate).
///
///     Uses Unity's own built-in UI skin sprites (<see cref="AssetDatabase.GetBuiltinExtraResource{T}" />)
///     for button/slider/toggle graphics — the same defaults "GameObject > UI > ..." uses — purely as a
///     functional placeholder look.
/// </summary>
public static class SettingsMenuGenerator
{
    private const string PrefabFolder = "Assets/Bladehold/Bladehold Prefabs/UI";
    private const string ButtonPrefabPath = PrefabFolder + "/MenuButton.prefab";
    private const string LabelPrefabPath = PrefabFolder + "/MenuLabel.prefab";
    private const string SliderPrefabPath = PrefabFolder + "/MenuSlider.prefab";
    private const string TogglePrefabPath = PrefabFolder + "/MenuToggle.prefab";
    private const string RebindRowPrefabPath = PrefabFolder + "/RebindRow.prefab";
    private const string IconButtonPrefabPath = PrefabFolder + "/MenuIconButton.prefab";
    private const string ValueInputPrefabPath = PrefabFolder + "/MenuValueInput.prefab";
    private const string ResetIconPath = PrefabFolder + "/ResetIcon.png";
    private const string MixerAssetPath = "Assets/Feel/MMTools/Core/MMAudio/MMSoundManager/Settings/MMSoundManagerAudioMixer.mixer";

    // Shared by RebindRow and the header row above the rebind list so their columns line up; narrower
    // than the slider rows' 220 label so the two binding buttons keep usable width.
    private const float RebindActionLabelWidth = 160f;

    private static GameObject buttonPrefab;
    private static GameObject labelPrefab;
    private static GameObject sliderPrefab;
    private static GameObject togglePrefab;
    private static GameObject iconButtonPrefab;
    private static GameObject valueInputPrefab;

    [MenuItem("Bladehold/Generate Settings Menu")]
    private static void Generate()
    {
        if (GameObject.Find("PauseMenuCanvas") != null)
        {
            EditorUtility.DisplayDialog(
                "Settings Menu Already Exists",
                "A 'PauseMenuCanvas' already exists in this scene. Delete it (and 'GameMenu', for a full reset) before regenerating.",
                "OK");
            return;
        }

        Undo.SetCurrentGroupName("Generate Settings Menu");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureEventSystem();
        BuildPrefabs();

        // Built inactive and only activated once every cross-reference below is wired — several of these
        // scripts validate their serialized fields in Awake, which Unity runs synchronously and
        // immediately for a component added to an active GameObject, i.e. before this method gets a
        // chance to assign anything to it.
        GameObject gameMenu = CreateUndoable("GameMenu", null);
        gameMenu.SetActive(false);
        PauseMenuController pauseController = gameMenu.AddComponent<PauseMenuController>();
        ScreenshotModeController screenshotController = gameMenu.AddComponent<ScreenshotModeController>();
        GameSettingsService settingsService = gameMenu.AddComponent<GameSettingsService>();

        WireGameMenu(pauseController, screenshotController, settingsService);
        WirePlayer();

        GameObject canvasGO = BuildCanvas(pauseController, screenshotController);

        gameMenu.SetActive(true);
        canvasGO.SetActive(true);

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log("Generated the pause/settings/Photo Mode menu. Reskin MenuButton/MenuLabel/MenuSlider/MenuToggle/"
            + "MenuIconButton under Assets/Bladehold/Bladehold Prefabs/UI to restyle everything at once. See TODO.md "
            + "for anything this couldn't wire automatically (e.g. routing audio sources through the mixer).");
    }

    // ---- Top-level wiring -------------------------------------------------

    private static void WireGameMenu(PauseMenuController pauseController, ScreenshotModeController screenshotController, GameSettingsService settingsService)
    {
        InputReader inputReader = Object.FindFirstObjectByType<InputReader>();
        PlayerCameraPivot cameraPivot = Object.FindFirstObjectByType<PlayerCameraPivot>();
        CinemachineBrain cameraBrain = Object.FindFirstObjectByType<CinemachineBrain>();

        Object[] toDisable = { inputReader, cameraPivot, cameraBrain };
        SetObjectArrayField(pauseController, "componentsToDisable", toDisable);
        SetField(pauseController, "screenshotMode", screenshotController);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            ScreenshotFlyCamera flyCamera = GetOrAddComponent<ScreenshotFlyCamera>(mainCamera.gameObject);
            flyCamera.enabled = false;
            SetField(screenshotController, "mainCamera", mainCamera);
            SetField(screenshotController, "flyCamera", flyCamera);
        }
        else
        {
            Debug.LogWarning("SettingsMenuGenerator: no Main Camera found — assign ScreenshotModeController.mainCamera/flyCamera manually.");
        }

        Light sun = RenderSettings.sun != null ? RenderSettings.sun : FindDirectionalLight();
        if (sun != null)
        {
            SetField(screenshotController, "sunLight", sun);
        }

        Volume globalVolume = Object.FindFirstObjectByType<Volume>();
        if (globalVolume != null)
        {
            SetField(screenshotController, "globalVolume", globalVolume);
        }

        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
        if (mixer != null)
        {
            SetField(settingsService, "mixer", mixer);
        }
        else
        {
            Debug.LogWarning($"SettingsMenuGenerator: no AudioMixer found at '{MixerAssetPath}' — assign GameSettingsService.mixer manually.");
        }
    }

    private static void WirePlayer()
    {
        Player player = Object.FindFirstObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning("SettingsMenuGenerator: no Player found in the scene — add InputSettingsBinder to the Player prefab manually.");
            return;
        }

        // Same premature-Awake concern as the GameMenu/Canvas construction above — briefly deactivate so
        // adding InputSettingsBinder doesn't validate its fields before they're assigned below.
        bool wasActive = player.gameObject.activeSelf;
        player.gameObject.SetActive(false);

        InputSettingsBinder binder = GetOrAddComponent<InputSettingsBinder>(player.gameObject);
        PlayerCameraPivot cameraPivot = Object.FindFirstObjectByType<PlayerCameraPivot>();
        InputReader inputReader = player.GetComponent<InputReader>() ?? Object.FindFirstObjectByType<InputReader>();

        if (cameraPivot != null) SetField(binder, "cameraPivot", cameraPivot);
        if (inputReader != null) SetField(binder, "inputReader", inputReader);

        player.gameObject.SetActive(wasActive);

        Debug.Log("Added InputSettingsBinder to the Player instance in this scene. Apply it to Player.prefab (Overrides > Apply All) to make it permanent.");
    }

    private static Light FindDirectionalLight()
    {
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }
        return null;
    }

    // ---- Canvas / menu hierarchy -------------------------------------------

    private static GameObject BuildCanvas(PauseMenuController pauseController, ScreenshotModeController screenshotController)
    {
        GameObject canvasGO = CreateUndoable("PauseMenuCanvas", null, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.SetActive(false); // reactivated by Generate() once every field below is wired.
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform pauseViewRt = CreateUIObject("PauseMenuView", canvasGO.transform, typeof(RectTransform), typeof(CanvasGroup));
        StretchFull(pauseViewRt);
        PauseMenuView pauseView = pauseViewRt.gameObject.AddComponent<PauseMenuView>();

        RectTransform backdrop = CreateUIObject("Backdrop", pauseViewRt, typeof(RectTransform), typeof(Image));
        StretchFull(backdrop);
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        GameObject mainButtonsPanel = BuildMainButtonsPanel(pauseViewRt);
        GameObject settingsPanel = BuildSettingsPanel(pauseViewRt);
        GameObject photoModePanel = BuildPhotoModePanel(pauseViewRt, screenshotController);

        Button resumeButton, settingsButton, photoModeButton, quitButton, backButton;
        resumeButton = mainButtonsPanel.transform.Find("Content/ResumeButton").GetComponent<Button>();
        settingsButton = mainButtonsPanel.transform.Find("Content/SettingsButton").GetComponent<Button>();
        photoModeButton = mainButtonsPanel.transform.Find("Content/PhotoModeButton").GetComponent<Button>();
        quitButton = mainButtonsPanel.transform.Find("Content/QuitButton").GetComponent<Button>();
        backButton = settingsPanel.transform.Find("Content/BackButton").GetComponent<Button>();

        SetField(pauseView, "canvasGroup", pauseViewRt.GetComponent<CanvasGroup>());
        SetField(pauseView, "resumeButton", resumeButton);
        SetField(pauseView, "settingsButton", settingsButton);
        SetField(pauseView, "backFromSettingsButton", backButton);
        SetField(pauseView, "photoModeButton", photoModeButton);
        SetField(pauseView, "quitButton", quitButton);
        SetField(pauseView, "settingsPanel", settingsPanel);
        SetField(pauseView, "mainButtonsPanel", mainButtonsPanel);
        SetField(pauseView, "backdrop", backdrop.gameObject);
        SetField(pauseView, "screenshotMode", screenshotController);
        SetField(pauseView, "photoModePanelRoot", photoModePanel);

        // Hidden for the captured frame so screenshots come out clean.
        SetObjectArrayField(screenshotController, "hideOnCapture", new Object[] { pauseViewRt.GetComponent<CanvasGroup>() });

        mainButtonsPanel.SetActive(true);
        settingsPanel.SetActive(false);
        photoModePanel.SetActive(false);

        SetButtonLabel(resumeButton, "Resume");
        SetButtonLabel(settingsButton, "Settings");
        SetButtonLabel(photoModeButton, "Photo Mode");
        SetButtonLabel(quitButton, "Quit");
        SetButtonLabel(backButton, "< Back");

        return canvasGO;
    }

    private static GameObject BuildMainButtonsPanel(Transform parent)
    {
        RectTransform panel = CreateFloatingPanel("MainButtonsPanel", parent, new Vector2(320f, 260f), new Vector2(0.5f, 0.5f), Vector2.zero);
        RectTransform content = CreateVerticalContainer("Content", panel, 14f, new RectOffset(20, 20, 20, 20));

        AddButtonRow(content, "ResumeButton", 44f);
        AddButtonRow(content, "SettingsButton", 44f);
        AddButtonRow(content, "PhotoModeButton", 44f);
        AddButtonRow(content, "QuitButton", 44f);

        return panel.gameObject;
    }

    private static GameObject BuildSettingsPanel(Transform parent)
    {
        RectTransform panel = CreateFloatingPanel("SettingsPanel", parent, new Vector2(600f, 700f), new Vector2(0.5f, 0.5f), Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

        RectTransform content = CreateVerticalContainer("Content", panel, 10f, new RectOffset(24, 24, 20, 20));

        AddButtonRow(content, "BackButton", 32f);

        // Tab bar: General (audio/look/video/performance) | Controls (the rebind list).
        RectTransform tabsRow = CreateUIObject("TabsRow", content, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        HorizontalLayoutGroup tabsLayout = tabsRow.GetComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.childControlWidth = true;
        tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = true;
        tabsRow.GetComponent<LayoutElement>().preferredHeight = 36f;

        GameObject generalTabGO = Instantiate(buttonPrefab, tabsRow);
        generalTabGO.name = "GeneralTabButton";
        GameObject controlsTabGO = Instantiate(buttonPrefab, tabsRow);
        controlsTabGO.name = "ControlsTabButton";

        RectTransform generalContent = CreateVerticalContainer("GeneralTabContent", content, 10f, new RectOffset(0, 0, 0, 0));
        generalContent.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        // Slider defaults mirror SaveData's authored defaults; at runtime RefreshFromSettings overrides
        // each from the saved value anyway, so these only matter for a freshly-generated menu.
        CreateSliderRow(generalContent, "Master Volume", out Slider masterSlider, 0f, 1f, 0.5f, 2);
        CreateSliderRow(generalContent, "Music Volume", out Slider musicSlider, 0f, 1f, 0.5f, 2);
        CreateSliderRow(generalContent, "SFX Volume", out Slider sfxSlider, 0f, 1f, 0.5f, 2);
        CreateSliderRow(generalContent, "Sensitivity", out Slider sensitivitySlider, 0f, 10f, 0.5f, 1);
        CreateSliderRow(generalContent, "Max Ragdolls", out Slider maxRagdollsSlider, 0f, 50f, 12f, 0);
        maxRagdollsSlider.wholeNumbers = true;
        CreateToggleRow(generalContent, "Invert X", out Toggle invertXToggle);
        CreateToggleRow(generalContent, "Invert Y", out Toggle invertYToggle);
        CreateSliderRow(generalContent, "Field of View", out Slider fieldOfViewSlider, 30f, 100f, 90f, 0);

        RectTransform controlsContent = CreateVerticalContainer("ControlsTabContent", content, 6f, new RectOffset(0, 0, 0, 0));
        controlsContent.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        CreateRebindHeaderRow(controlsContent);
        RectTransform rebindScroll = CreateScrollView(controlsContent, "RebindScrollView", 220f, out Transform rebindContent);
        rebindScroll.GetComponent<LayoutElement>().flexibleHeight = 1f;

        AddButtonRow(content, "ResetSettingsButton", 40f);
        AddButtonRow(content, "DeleteSaveButton", 40f);

        GameObject confirmDialogGO = BuildConfirmDialog(panel);

        SettingsPanelView view = panel.gameObject.AddComponent<SettingsPanelView>();
        SetField(view, "generalTabButton", generalTabGO.GetComponent<Button>());
        SetField(view, "controlsTabButton", controlsTabGO.GetComponent<Button>());
        SetField(view, "generalTabContent", generalContent.gameObject);
        SetField(view, "controlsTabContent", controlsContent.gameObject);
        SetField(view, "masterVolumeSlider", masterSlider);
        SetField(view, "musicVolumeSlider", musicSlider);
        SetField(view, "sfxVolumeSlider", sfxSlider);
        SetField(view, "sensitivitySlider", sensitivitySlider);
        SetField(view, "maxRagdollsSlider", maxRagdollsSlider);
        SetField(view, "invertXToggle", invertXToggle);
        SetField(view, "invertYToggle", invertYToggle);
        SetField(view, "fieldOfViewSlider", fieldOfViewSlider);
        SetField(view, "rebindListParent", rebindContent);
        SetField(view, "rebindRowPrefab", GetOrCreateRebindRowPrefab().GetComponent<RebindButtonView>());
        SetField(view, "resetSettingsButton", content.Find("ResetSettingsButton").GetComponent<Button>());
        SetField(view, "deleteSaveButton", content.Find("DeleteSaveButton").GetComponent<Button>());
        SetField(view, "confirmDialog", confirmDialogGO.GetComponent<ConfirmDialog>());

        SetButtonLabel(content.Find("ResetSettingsButton").GetComponent<Button>(), "Reset Settings");
        SetButtonLabel(content.Find("DeleteSaveButton").GetComponent<Button>(), "Delete Save");
        SetButtonLabel(generalTabGO.GetComponent<Button>(), "General");
        SetButtonLabel(controlsTabGO.GetComponent<Button>(), "Controls");

        // SettingsPanelView.OnEnable re-selects the General tab at runtime; this just makes the
        // generated scene match that default.
        generalContent.gameObject.SetActive(true);
        controlsContent.gameObject.SetActive(false);

        return panel.gameObject;
    }

    /// <summary>
    ///     Column headers over the rebind list, mirroring RebindRow's layout (action label width +
    ///     two equal flexible columns) so the headers line up with the buttons below.
    /// </summary>
    private static void CreateRebindHeaderRow(Transform parent)
    {
        RectTransform row = CreateUIObject("HeaderRow", parent, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 0, 0); // matches the scroll content's horizontal padding.
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = 26f;

        GameObject actionHeaderGO = Instantiate(labelPrefab, row);
        actionHeaderGO.name = "ActionHeader";
        LayoutElement actionLayout = actionHeaderGO.AddComponent<LayoutElement>();
        actionLayout.preferredWidth = RebindActionLabelWidth;
        actionLayout.flexibleWidth = 0f;
        actionHeaderGO.GetComponent<TextMeshProUGUI>().text = "";

        AddRebindColumnHeader(row, "KbmHeader", "Keyboard / Mouse");
        AddRebindColumnHeader(row, "GamepadHeader", "Gamepad");
    }

    private static void AddRebindColumnHeader(Transform parent, string name, string text)
    {
        GameObject headerGO = Instantiate(labelPrefab, parent);
        headerGO.name = name;
        headerGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
        TextMeshProUGUI headerText = headerGO.GetComponent<TextMeshProUGUI>();
        headerText.text = text;
        headerText.fontSize = 18f;
        headerText.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject BuildConfirmDialog(Transform parent)
    {
        RectTransform dialog = CreateFloatingPanel("ConfirmDialogRoot", parent, new Vector2(440f, 200f), new Vector2(0.5f, 0.5f), Vector2.zero);
        dialog.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.97f);
        CanvasGroup canvasGroup = dialog.gameObject.AddComponent<CanvasGroup>();

        RectTransform content = CreateVerticalContainer("Content", dialog, 16f, new RectOffset(24, 24, 24, 24));

        GameObject messageGO = Instantiate(labelPrefab, content);
        messageGO.name = "MessageText";
        LayoutElement messageLayout = messageGO.AddComponent<LayoutElement>();
        messageLayout.preferredHeight = 80f;
        messageLayout.flexibleWidth = 1f;
        TextMeshProUGUI messageText = messageGO.GetComponent<TextMeshProUGUI>();
        messageText.text = "Are you sure?";
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableWordWrapping = true;

        RectTransform buttonsRow = CreateUIObject("ButtonsRow", content, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        HorizontalLayoutGroup rowLayout = buttonsRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        buttonsRow.GetComponent<LayoutElement>().preferredHeight = 44f;

        GameObject confirmButtonGO = Instantiate(buttonPrefab, buttonsRow);
        confirmButtonGO.name = "ConfirmButton";
        // Placeholder only — ConfirmDialog.Show sets the label per action (Delete / Reset).
        SetButtonLabel(confirmButtonGO.GetComponent<Button>(), "Confirm");

        GameObject cancelButtonGO = Instantiate(buttonPrefab, buttonsRow);
        cancelButtonGO.name = "CancelButton";
        SetButtonLabel(cancelButtonGO.GetComponent<Button>(), "Cancel");

        ConfirmDialog confirmDialog = dialog.gameObject.AddComponent<ConfirmDialog>();
        SetField(confirmDialog, "canvasGroup", canvasGroup);
        SetField(confirmDialog, "messageText", messageText);
        SetField(confirmDialog, "confirmButton", confirmButtonGO.GetComponent<Button>());
        SetField(confirmDialog, "cancelButton", cancelButtonGO.GetComponent<Button>());

        return dialog.gameObject;
    }

    private static GameObject BuildPhotoModePanel(Transform parent, ScreenshotModeController screenshotController)
    {
        // Stretches the full screen height along the right edge: the slider rows live in a scroll view
        // that takes whatever height is left, so Take Photo / Exit stay pinned (and visible) at the
        // bottom no matter how many settings are added or how short the screen is.
        RectTransform panel = CreateUIObject("PhotoModePanelRoot", parent, typeof(RectTransform), typeof(Image));
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(1f, 0.5f);
        panel.sizeDelta = new Vector2(400f, -40f);
        panel.anchoredPosition = new Vector2(-20f, 0f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

        RectTransform content = CreateVerticalContainer("Content", panel, 8f, new RectOffset(16, 16, 16, 16));

        RectTransform scrollRoot = CreateScrollView(content, "SettingsScrollView", 200f, out Transform scrollContent);
        scrollRoot.GetComponent<LayoutElement>().flexibleHeight = 1f;

        // Order here is just panel order; each row carries its own PhotoSetting, so it also drives the
        // ScreenshotModePanel.rows wiring below.
        (PhotoSetting setting, string label, float min, float max, float defaultValue)[] settings =
        {
            (PhotoSetting.SunIntensity, "Sun Intensity", 0f, 8f, 2f),
            (PhotoSetting.SunPitch, "Sun Pitch", -10f, 90f, 50f),
            (PhotoSetting.SunYaw, "Sun Yaw", 0f, 360f, 0f),
            (PhotoSetting.Bloom, "Bloom", 0f, 5f, 0f),
            (PhotoSetting.Vignette, "Vignette", 0f, 1f, 0f),
            (PhotoSetting.Exposure, "Exposure", -5f, 5f, 0f),
            (PhotoSetting.Contrast, "Contrast", -100f, 100f, 0f),
            (PhotoSetting.Saturation, "Saturation", -100f, 100f, 0f),
            (PhotoSetting.FocusDistance, "Focus Distance", 0.1f, 20f, 10f),
            (PhotoSetting.Aperture, "Aperture", 1f, 32f, 5.6f),
            (PhotoSetting.FieldOfView, "Field of View", 20f, 90f, 60f),
        };

        var sliders = new Slider[settings.Length];
        var resetButtons = new Button[settings.Length];
        for (int i = 0; i < settings.Length; i++)
        {
            CreatePhotoSliderRow(scrollContent, settings[i].label, settings[i].min, settings[i].max, settings[i].defaultValue,
                out sliders[i], out resetButtons[i]);
        }

        RectTransform buttonsRow = CreateUIObject("ButtonsRow", content, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        HorizontalLayoutGroup rowLayout = buttonsRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        buttonsRow.GetComponent<LayoutElement>().preferredHeight = 44f;

        GameObject captureButtonGO = Instantiate(buttonPrefab, buttonsRow);
        captureButtonGO.name = "CaptureButton";
        SetButtonLabel(captureButtonGO.GetComponent<Button>(), "Take Photo");

        GameObject exitButtonGO = Instantiate(buttonPrefab, buttonsRow);
        exitButtonGO.name = "ExitButton";
        SetButtonLabel(exitButtonGO.GetComponent<Button>(), "Exit Photo Mode");

        GameObject savedLabelGO = AddLabel(content, "SavedLabel", "", 16f);

        ScreenshotModePanel panelView = panel.gameObject.AddComponent<ScreenshotModePanel>();
        SetField(panelView, "screenshotMode", screenshotController);
        SetField(panelView, "captureButton", captureButtonGO.GetComponent<Button>());
        SetField(panelView, "exitButton", exitButtonGO.GetComponent<Button>());
        SetField(panelView, "savedLabel", savedLabelGO.GetComponent<TextMeshProUGUI>());

        SerializedObject panelSO = new SerializedObject(panelView);
        SerializedProperty rowsProp = panelSO.FindProperty("rows");
        rowsProp.arraySize = settings.Length;
        for (int i = 0; i < settings.Length; i++)
        {
            SerializedProperty element = rowsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("setting").intValue = (int)settings[i].setting;
            element.FindPropertyRelative("slider").objectReferenceValue = sliders[i];
            element.FindPropertyRelative("resetButton").objectReferenceValue = resetButtons[i];
        }
        panelSO.ApplyModifiedProperties();

        return panel.gameObject;
    }

    private static void CreatePhotoSliderRow(Transform parent, string labelText, float min, float max, float value, out Slider slider, out Button resetButton)
    {
        RectTransform row = CreateLabeledRow(parent, labelText, sliderPrefab, 32f, 0f, out GameObject controlInstance);
        // Narrower label than the settings panel's 220 so the slider keeps usable width with the reset
        // button also in the row.
        row.GetChild(0).GetComponent<LayoutElement>().preferredWidth = 150f;

        slider = controlInstance.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        GameObject resetGO = Instantiate(iconButtonPrefab, row);
        resetGO.name = "ResetButton";
        LayoutElement resetLayout = resetGO.AddComponent<LayoutElement>();
        resetLayout.preferredWidth = 26f;
        resetLayout.flexibleWidth = 0f;
        resetButton = resetGO.GetComponent<Button>();
    }

    // ---- Shared control prefabs ---------------------------------------------

    private static void BuildPrefabs()
    {
        EnsureFolder();
        buttonPrefab = GetOrCreateButtonPrefab();
        labelPrefab = GetOrCreateLabelPrefab();
        sliderPrefab = GetOrCreateSliderPrefab();
        togglePrefab = GetOrCreateTogglePrefab();
        iconButtonPrefab = GetOrCreateIconButtonPrefab();
        valueInputPrefab = GetOrCreateValueInputPrefab();
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Bladehold/Bladehold Prefabs", "UI");
        }
    }

    private static Sprite GetBuiltinSprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

    private static GameObject GetOrCreateButtonPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
        if (existing != null) return existing;

        GameObject root = new GameObject("MenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 40f);

        Image image = root.GetComponent<Image>();
        image.sprite = GetBuiltinSprite("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.2f, 0.2f, 0.24f);

        root.GetComponent<Button>().targetGraphic = image;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(root.transform, false);
        StretchFull(labelGO.GetComponent<RectTransform>());
        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = "Button";
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontSize = 22f;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ButtonPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject GetOrCreateLabelPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(LabelPrefabPath);
        if (existing != null) return existing;

        GameObject root = new GameObject("MenuLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 30f);

        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
        text.text = "Label";
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;
        text.fontSize = 20f;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, LabelPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject GetOrCreateSliderPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SliderPrefabPath);
        if (existing != null) return existing;

        Sprite background = GetBuiltinSprite("UI/Skin/Background.psd");
        Sprite fillSprite = GetBuiltinSprite("UI/Skin/UISprite.psd");
        Sprite knob = GetBuiltinSprite("UI/Skin/Knob.psd");

        GameObject root = new GameObject("MenuSlider", typeof(RectTransform), typeof(Slider));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 20f);

        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(root.transform, false);
        RectTransform bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.sizeDelta = Vector2.zero;
        Image bgImage = bgGO.GetComponent<Image>();
        bgImage.sprite = background;
        bgImage.type = Image.Type.Sliced;
        bgImage.color = new Color(0.15f, 0.15f, 0.18f);

        GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(root.transform, false);
        RectTransform fillAreaRt = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = new Vector2(5f, 0f);
        fillAreaRt.offsetMax = new Vector2(-15f, 0f);

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.sizeDelta = Vector2.zero;
        Image fillImage = fillGO.GetComponent<Image>();
        fillImage.sprite = fillSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = new Color(0.4f, 0.7f, 0.9f);

        GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGO.transform.SetParent(root.transform, false);
        RectTransform handleAreaRt = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        handleGO.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
        Image handleImage = handleGO.GetComponent<Image>();
        handleImage.sprite = knob;

        Slider slider = root.GetComponent<Slider>();
        slider.targetGraphic = handleImage;
        slider.fillRect = fillRt;
        slider.handleRect = handleGO.GetComponent<RectTransform>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, SliderPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject GetOrCreateTogglePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TogglePrefabPath);
        if (existing != null) return existing;

        Sprite background = GetBuiltinSprite("UI/Skin/Background.psd");
        Sprite checkmark = GetBuiltinSprite("UI/Skin/Checkmark.psd");

        GameObject root = new GameObject("MenuToggle", typeof(RectTransform), typeof(Toggle));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(28f, 28f);

        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(root.transform, false);
        StretchFull(bgGO.GetComponent<RectTransform>());
        Image bgImage = bgGO.GetComponent<Image>();
        bgImage.sprite = background;
        bgImage.type = Image.Type.Sliced;
        bgImage.color = new Color(0.15f, 0.15f, 0.18f);

        GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(bgGO.transform, false);
        RectTransform checkRt = checkGO.GetComponent<RectTransform>();
        StretchFull(checkRt);
        checkRt.offsetMin = new Vector2(4f, 4f);
        checkRt.offsetMax = new Vector2(-4f, -4f);
        Image checkImage = checkGO.GetComponent<Image>();
        checkImage.sprite = checkmark;
        checkImage.color = new Color(0.4f, 0.7f, 0.9f);

        Toggle toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = false;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TogglePrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject GetOrCreateIconButtonPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(IconButtonPrefabPath);
        if (existing != null) return existing;

        GameObject root = new GameObject("MenuIconButton", typeof(RectTransform), typeof(Image), typeof(Button));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(28f, 28f);

        Image image = root.GetComponent<Image>();
        image.sprite = GetBuiltinSprite("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.2f, 0.2f, 0.24f);
        root.GetComponent<Button>().targetGraphic = image;

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(root.transform, false);
        RectTransform iconRt = iconGO.GetComponent<RectTransform>();
        StretchFull(iconRt);
        iconRt.offsetMin = new Vector2(4f, 4f);
        iconRt.offsetMax = new Vector2(-4f, -4f);
        Image iconImage = iconGO.GetComponent<Image>();
        iconImage.sprite = GetOrCreateResetIconSprite();
        iconImage.color = new Color(0.85f, 0.85f, 0.9f);
        iconImage.raycastTarget = false;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, IconButtonPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject GetOrCreateValueInputPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ValueInputPrefabPath);
        if (existing != null) return existing;

        Sprite background = GetBuiltinSprite("UI/Skin/InputFieldBackground.psd");

        GameObject root = new GameObject("MenuValueInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 28f);

        Image image = root.GetComponent<Image>();
        image.sprite = background;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.15f, 0.15f, 0.18f);

        GameObject textAreaGO = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaGO.transform.SetParent(root.transform, false);
        RectTransform textAreaRt = textAreaGO.GetComponent<RectTransform>();
        StretchFull(textAreaRt);
        textAreaRt.offsetMin = new Vector2(6f, 2f);
        textAreaRt.offsetMax = new Vector2(-6f, -2f);

        GameObject placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderGO.transform.SetParent(textAreaRt, false);
        StretchFull(placeholderGO.GetComponent<RectTransform>());
        TextMeshProUGUI placeholder = placeholderGO.GetComponent<TextMeshProUGUI>();
        placeholder.text = "";
        placeholder.fontSize = 18f;
        placeholder.color = new Color(1f, 1f, 1f, 0.3f);
        placeholder.alignment = TextAlignmentOptions.MidlineRight;

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(textAreaRt, false);
        StretchFull(textGO.GetComponent<RectTransform>());
        TextMeshProUGUI text = textGO.GetComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineRight;

        TMP_InputField inputField = root.GetComponent<TMP_InputField>();
        inputField.targetGraphic = image;
        inputField.textViewport = textAreaRt;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.contentType = TMP_InputField.ContentType.DecimalNumber;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ValueInputPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    /// <summary>
    ///     A circular "reset" arrow drawn once into a small PNG — TMP's default font atlas has no ↺
    ///     glyph and Unity ships no builtin refresh sprite, so the generator makes its own. Replace the
    ///     PNG to restyle it, same as the control prefabs.
    /// </summary>
    private static Sprite GetOrCreateResetIconSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(ResetIconPath);
        if (existing != null) return existing;

        const int size = 64;
        const float radius = 22f;
        const float thickness = 5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        // Ring with a gap at the top; the arrowhead sits at the gap's edge pointing into it.
        Vector2 tip = center + radius * Dir(105f);
        Vector2 baseInner = center + (radius - 10f) * Dir(60f);
        Vector2 baseOuter = center + (radius + 10f) * Dir(60f);

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                Vector2 fromCenter = point - center;
                float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;

                bool inGap = angle > 55f && angle < 125f;
                float alpha = inGap ? 0f : Mathf.Clamp01(thickness * 0.5f + 0.5f - Mathf.Abs(fromCenter.magnitude - radius));
                if (InTriangle(point, tip, baseInner, baseOuter)) alpha = 1f;

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        File.WriteAllBytes(ResetIconPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(ResetIconPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ResetIconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(ResetIconPath);
    }

    private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float Cross(Vector2 from, Vector2 to) => from.x * to.y - from.y * to.x;

        float d1 = Cross(b - a, point - a);
        float d2 = Cross(c - b, point - b);
        float d3 = Cross(a - c, point - c);
        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static GameObject GetOrCreateRebindRowPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(RebindRowPrefabPath);
        if (existing != null)
        {
            // A pre-two-column prefab (single BindingButton) no longer matches RebindButtonView's
            // fields — rebuild it rather than returning a stale hierarchy.
            if (existing.transform.Find("KbmBindingButton") != null) return existing;
            AssetDatabase.DeleteAsset(RebindRowPrefabPath);
        }

        GameObject root = new GameObject("RebindRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 36f);

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        root.GetComponent<LayoutElement>().preferredHeight = 36f;

        // Nested as real prefab instances (not plain copies) so reskinning MenuLabel/MenuButton later
        // cascades into every rebind row too, same as everywhere else this generator places them.
        GameObject nameLabelGO = (GameObject)PrefabUtility.InstantiatePrefab(labelPrefab, root.transform);
        nameLabelGO.name = "ActionLabel";
        LayoutElement nameLayout = nameLabelGO.AddComponent<LayoutElement>();
        nameLayout.preferredWidth = RebindActionLabelWidth;
        nameLayout.flexibleWidth = 0f;
        TextMeshProUGUI nameLabel = nameLabelGO.GetComponent<TextMeshProUGUI>();
        nameLabel.text = "Action";

        Button kbmButton = AddRebindBindingButton(root.transform, "KbmBindingButton", out TextMeshProUGUI kbmLabel);
        Button gamepadButton = AddRebindBindingButton(root.transform, "GamepadBindingButton", out TextMeshProUGUI gamepadLabel);

        RebindButtonView view = root.AddComponent<RebindButtonView>();
        SetField(view, "label", nameLabel);
        SetField(view, "kbmBindingPathLabel", kbmLabel);
        SetField(view, "kbmButton", kbmButton);
        SetField(view, "gamepadBindingPathLabel", gamepadLabel);
        SetField(view, "gamepadButton", gamepadButton);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, RebindRowPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static Button AddRebindBindingButton(Transform parent, string name, out TextMeshProUGUI bindingLabel)
    {
        GameObject buttonGO = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, parent);
        buttonGO.name = name;
        LayoutElement buttonLayout = buttonGO.AddComponent<LayoutElement>();
        buttonLayout.flexibleWidth = 1f;
        buttonLayout.preferredHeight = 32f;
        bindingLabel = buttonGO.GetComponentInChildren<TextMeshProUGUI>(true);
        bindingLabel.text = "<Unbound>";
        return buttonGO.GetComponent<Button>();
    }

    // ---- Layout helpers ------------------------------------------------------

    private static GameObject CreateUndoable(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Generate Settings Menu");
        return go;
    }

    private static RectTransform CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = CreateUndoable(name, parent, components);
        return go.GetComponent<RectTransform>();
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject Instantiate(GameObject prefab, Transform parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Generate Settings Menu");
        return instance;
    }

    private static RectTransform CreateFloatingPanel(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 anchoredPosition)
    {
        RectTransform rt = CreateUIObject(name, parent, typeof(RectTransform));
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
        return rt;
    }

    private static RectTransform CreateVerticalContainer(string name, Transform parent, float spacing, RectOffset padding)
    {
        RectTransform rt = CreateUIObject(name, parent, typeof(RectTransform), typeof(VerticalLayoutGroup));
        StretchFull(rt);
        VerticalLayoutGroup layout = rt.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = true;
        // Control heights so each child's LayoutElement.preferredHeight actually applies — left
        // uncontrolled, every row keeps the 100px default RectTransform height and tall panels
        // overflow the screen.
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        return rt;
    }

    private static GameObject AddLabel(Transform parent, string name, string text, float fontSize)
    {
        GameObject labelGO = Instantiate(labelPrefab, parent);
        labelGO.name = name;
        LayoutElement layout = labelGO.AddComponent<LayoutElement>();
        layout.preferredHeight = fontSize + 10f;
        layout.flexibleWidth = 1f;
        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        return labelGO;
    }

    private static void AddButtonRow(Transform parent, string name, float height)
    {
        GameObject buttonGO = Instantiate(buttonPrefab, parent);
        buttonGO.name = name;
        LayoutElement layout = buttonGO.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.flexibleWidth = 1f;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        // includeInactive — the canvas hierarchy is built inactive, and the default lookup skips
        // inactive objects, silently leaving the prefab's placeholder "Button" text.
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = text;
    }

    private static RectTransform CreateLabeledRow(Transform parent, string labelText, GameObject controlPrefab, float rowHeight, float controlWidth, out GameObject controlInstance)
    {
        RectTransform row = CreateUIObject("Row " + labelText, parent, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = rowHeight;

        GameObject labelGO = Instantiate(labelPrefab, row);
        LayoutElement labelLayout = labelGO.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 220f;
        labelLayout.flexibleWidth = 0f;
        labelGO.GetComponent<TextMeshProUGUI>().text = labelText;

        controlInstance = Instantiate(controlPrefab, row);
        LayoutElement controlLayout = controlInstance.AddComponent<LayoutElement>();
        if (controlWidth > 0f)
        {
            controlLayout.preferredWidth = controlWidth;
            controlLayout.flexibleWidth = 0f;
        }
        else
        {
            controlLayout.flexibleWidth = 1f;
        }
        controlLayout.preferredHeight = rowHeight * 0.7f;

        return row;
    }

    private static void CreateSliderRow(Transform parent, string labelText, out Slider slider, float min, float max, float value, int decimalPlaces)
    {
        RectTransform row = CreateLabeledRow(parent, labelText, sliderPrefab, 32f, 0f, out GameObject controlInstance);
        slider = controlInstance.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        GameObject valueInputGO = Instantiate(valueInputPrefab, row);
        LayoutElement valueLayout = valueInputGO.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 56f;
        valueLayout.flexibleWidth = 0f;
        valueLayout.preferredHeight = 32f * 0.7f;

        SliderValueField sync = row.gameObject.AddComponent<SliderValueField>();
        SetField(sync, "slider", slider);
        SetField(sync, "inputField", valueInputGO.GetComponent<TMP_InputField>());
        SetIntField(sync, "decimalPlaces", decimalPlaces);
    }

    private static void CreateToggleRow(Transform parent, string labelText, out Toggle toggle)
    {
        CreateLabeledRow(parent, labelText, togglePrefab, 32f, 28f, out GameObject controlInstance);
        toggle = controlInstance.GetComponent<Toggle>();
    }

    private static RectTransform CreateScrollView(Transform parent, string name, float height, out Transform content)
    {
        RectTransform root = CreateUIObject(name, parent, typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        root.GetComponent<LayoutElement>().preferredHeight = height;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

        RectTransform viewport = CreateUIObject("Viewport", root, typeof(RectTransform), typeof(RectMask2D));
        StretchFull(viewport);

        RectTransform contentRt = CreateUIObject("Content", viewport, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentRt.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentRt.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scrollRect = root.GetComponent<ScrollRect>();
        scrollRect.content = contentRt;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        content = contentRt;
        return root;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        CreateUndoable("EventSystem", null, typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    // ---- Serialized field assignment (private [SerializeField] fields) -------

    private static void SetField(Object target, string fieldName, Object value)
    {
        if (target == null) return;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"SettingsMenuGenerator: field '{fieldName}' not found on {target.GetType().Name}.");
            return;
        }

        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetIntField(Object target, string fieldName, int value)
    {
        if (target == null) return;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"SettingsMenuGenerator: field '{fieldName}' not found on {target.GetType().Name}.");
            return;
        }

        prop.intValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetObjectArrayField(Object target, string fieldName, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"SettingsMenuGenerator: array field '{fieldName}' not found on {target.GetType().Name}.");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        so.ApplyModifiedProperties();
    }
}
