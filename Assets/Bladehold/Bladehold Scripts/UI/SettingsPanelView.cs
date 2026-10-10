using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Settings sub-panel shown from the pause menu, split into tabs: <b>General</b> (audio, language,
///     game speed, max ragdolls), <b>Keyboard &amp; Mouse</b> (mouse sensitivity, invert, and the
///     keyboard/mouse column of every remappable binding on the vendored Controls asset),
///     <b>Controller</b> (pad look sensitivity, stick dead zone, invert, and the gamepad bindings) and
///     <b>Graphics</b> (field of view and post processing). The Controller and Graphics tabs are
///     optional: an older generated copy without a Controller tab keeps a single Controls tab whose
///     rebind rows show both device columns side by side. Invert X/Y is one shared setting, so the
///     two device tabs carry mirrored toggles.
///     Reset Settings (settings back to defaults, progress untouched) and Delete Save (progress
///     wiped, settings kept) sit below the tabs. Every control reads and writes through
///     <see cref="GameSettingsService" /> — this view never touches <see cref="SaveData" /> or the
///     vendored input asset directly. Refreshes from current settings whenever shown, always
///     reopening on the General tab. Tabs cycle with Q/E or LB/RB (glyphs shown either side of the
///     tab bar), and under a pad each tab switch focuses that tab's first control.
/// </summary>
public class SettingsPanelView : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private Button generalTabButton;
    [Tooltip("Keyboard & Mouse tab (the whole Controls tab on copies without a Controller tab).")]
    [SerializeField] private Button controlsTabButton;
    [Tooltip("Optional Controller tab. When set, keyboard and gamepad bindings split across the two device tabs.")]
    [SerializeField] private Button gamepadTabButton;
    [SerializeField] private Button postProcessingTabButton;
    [SerializeField] private GameObject generalTabContent;
    [SerializeField] private GameObject controlsTabContent;
    [SerializeField] private GameObject gamepadTabContent;
    [SerializeField] private GameObject postProcessingTabContent;
    [SerializeField] private Color tabSelectedColor = new Color(0.831f, 0.776f, 0.639f, 1f);
    [SerializeField] private Color tabUnselectedColor = new Color(0.329f, 0.282f, 0.239f, 1f);
    [SerializeField] private Color tabSelectedTextColor = new Color(0.220f, 0.180f, 0.140f, 1f);
    [SerializeField] private Color tabUnselectedTextColor = new Color(0.774f, 0.745f, 0.660f, 1f);
    [Tooltip("Optional glyphs either side of the tab bar showing the previous/next tab shortcut (Q/E, LB/RB).")]
    [SerializeField] private InputGlyph tabPrevGlyph;
    [SerializeField] private InputGlyph tabNextGlyph;
    [Tooltip("Optional: the panel's focus controller — its default moves to each tab's first control so pad focus follows tab switches.")]
    [SerializeField] private MenuFocusController focusController;

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Controls")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Slider gamepadSensitivitySlider;
    [Tooltip("Optional: thumbstick dead zone slider. When empty, a row is cloned from the Gamepad Look Sensitivity row at runtime.")]
    [SerializeField] private Slider stickDeadzoneSlider;
    [Tooltip("Optional: controller aim assist strength. Cloned below the dead zone row at runtime when unassigned.")]
    [SerializeField] private Slider aimAssistStrengthSlider;
    [Tooltip("Optional: controller aim assist window (degrees). Cloned below the strength row at runtime when unassigned.")]
    [SerializeField] private Slider aimAssistWindowSlider;
    [SerializeField] private Toggle invertXToggle;
    [SerializeField] private Toggle invertYToggle;
    [Tooltip("Optional: Controller-tab mirrors of Invert X/Y (same shared setting).")]
    [SerializeField] private Toggle gamepadInvertXToggle;
    [SerializeField] private Toggle gamepadInvertYToggle;

    [Header("Language")]
    [Tooltip("Optional: UI language picker. Options are built in code — 'Auto (System)' followed by every Loc.SupportedLanguages entry in its own native name.")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("Video")]
    [SerializeField] private Slider fieldOfViewSlider;

    [Header("Performance")]
    [SerializeField] private Slider maxRagdollsSlider;
    [SerializeField] private Slider gameSpeedSlider;
    [Tooltip("Parent under which one RebindButtonView is instantiated per remappable action row.")]
    [SerializeField] private Transform rebindListParent;
    [SerializeField] private RebindButtonView rebindRowPrefab;
    [Tooltip("Optional: control above the rebind grid (pad Up from its first row lands here).")]
    [SerializeField] private Selectable rebindGridAbove;
    [Tooltip("Optional: control below the rebind grid (pad Down from its last row lands here).")]
    [SerializeField] private Selectable rebindGridBelow;
    [Tooltip("Optional: Controller-tab rebind list. When set, rebindListParent gets keyboard/mouse rows only and this gets gamepad rows only.")]
    [SerializeField] private Transform gamepadRebindListParent;
    [SerializeField] private Selectable gamepadRebindGridAbove;

    [Header("Post Processing")]
    [SerializeField] private Toggle postProcessingEnabledToggle;
    [SerializeField] private Slider postProcessingBloomSlider;
    [SerializeField] private Slider postProcessingVignetteSlider;
    [SerializeField] private Slider postProcessingExposureSlider;

    [Header("Reset / delete")]
    [SerializeField] private Button resetSettingsButton;
    [SerializeField] private Button deleteSaveButton;
    [SerializeField] private ConfirmDialog confirmDialog;

    private readonly List<RebindButtonView> rebindRows = new List<RebindButtonView>();
    private readonly List<RebindButtonView> gamepadRebindRows = new List<RebindButtonView>();
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<GameObject> tabContents = new List<GameObject>();
    private bool rebindRowsBuilt = false;
    private bool anyError = false;
    private InputActionMap tabActionMap;
    private InputAction tabPrevAction;
    private InputAction tabNextAction;
    private int currentTab;

    /// <summary>One rebind row being assembled: an action's KBM and Gamepad bindings paired by display label.</summary>
    private class RowSlot
    {
        public string label;
        public int kbmIndex = -1;
        public int gamepadIndex = -1;
    }

    private void Awake()
    {
        if (masterVolumeSlider == null || musicVolumeSlider == null || sfxVolumeSlider == null)
        {
            Debug.LogError("Volume sliders are not all assigned in the inspector.");
            anyError = true;
        }
        if (sensitivitySlider == null)
        {
            Debug.LogError("Sensitivity slider is not assigned in the inspector.");
            anyError = true;
        }
        if (fieldOfViewSlider == null)
        {
            Debug.LogError("Field of View slider is not assigned in the inspector.");
            anyError = true;
        }
        if (maxRagdollsSlider == null || gameSpeedSlider == null)
        {
            Debug.LogError("Max Ragdolls slider / Game Speed slider is not assigned in the inspector.");
            anyError = true;
        }
        if (resetSettingsButton == null || deleteSaveButton == null || confirmDialog == null)
        {
            Debug.LogError("Reset Settings button/Delete Save button/ConfirmDialog is not assigned in the inspector.");
            anyError = true;
        }
        if (rebindListParent == null || rebindRowPrefab == null)
        {
            Debug.LogError("Rebind list parent/prefab is not assigned in the inspector.");
            anyError = true;
        }
        if (generalTabButton == null || controlsTabButton == null || generalTabContent == null || controlsTabContent == null)
        {
            Debug.LogError("Tab buttons/contents are not all assigned in the inspector.");
            anyError = true;
        }
        if (postProcessingTabButton == null || postProcessingTabContent == null)
        {
            Debug.LogWarning("Post Processing tab is not assigned. Please regenerate the UI.");
        }

        if (anyError)
        {
            return;
        }

        masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        // The range lives in code (SaveData), not on the many prefab/scene copies of this row, so an old
        // 0-10 slider left in some scene can never offer 0 (mouse look off) or the old too-fast range.
        sensitivitySlider.minValue = SaveData.MinMouseSensitivity;
        sensitivitySlider.maxValue = SaveData.MaxMouseSensitivity;
        SliderValueField sensitivityField = sensitivitySlider.GetComponentInParent<SliderValueField>();
        if (sensitivityField != null) sensitivityField.DecimalPlaces = 3;
        sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
        if (gamepadSensitivitySlider != null) gamepadSensitivitySlider.onValueChanged.AddListener(HandleGamepadSensitivityChanged);
        if (stickDeadzoneSlider == null && gamepadSensitivitySlider != null) stickDeadzoneSlider = BuildStickDeadzoneRow(gamepadSensitivitySlider);
        if (stickDeadzoneSlider != null)
        {
            stickDeadzoneSlider.wholeNumbers = false;
            stickDeadzoneSlider.minValue = SaveData.MinStickDeadzone;
            stickDeadzoneSlider.maxValue = SaveData.MaxStickDeadzone;
            SliderValueField deadzoneField = stickDeadzoneSlider.GetComponentInParent<SliderValueField>();
            if (deadzoneField != null) deadzoneField.DecimalPlaces = 2;
            stickDeadzoneSlider.onValueChanged.AddListener(HandleStickDeadzoneChanged);
        }
        if (aimAssistStrengthSlider == null && stickDeadzoneSlider != null)
        {
            aimAssistStrengthSlider = CloneSliderRow(stickDeadzoneSlider, "Row Aim Assist Strength", "settings.aim_assist_strength");
        }
        if (aimAssistStrengthSlider != null)
        {
            ConfigureSlider(aimAssistStrengthSlider, SaveData.MinAimAssistStrength, SaveData.MaxAimAssistStrength, 2);
            aimAssistStrengthSlider.onValueChanged.AddListener(HandleAimAssistStrengthChanged);
        }
        if (aimAssistWindowSlider == null && aimAssistStrengthSlider != null)
        {
            aimAssistWindowSlider = CloneSliderRow(aimAssistStrengthSlider, "Row Aim Assist Window", "settings.aim_assist_window");
        }
        if (aimAssistWindowSlider != null)
        {
            ConfigureSlider(aimAssistWindowSlider, SaveData.MinAimAssistWindow, SaveData.MaxAimAssistWindow, 1);
            aimAssistWindowSlider.onValueChanged.AddListener(HandleAimAssistWindowChanged);
        }
        if (languageDropdown != null)
        {
            BuildLanguageOptions();
            languageDropdown.onValueChanged.AddListener(HandleLanguageChanged);
        }
        fieldOfViewSlider.onValueChanged.AddListener(HandleFieldOfViewChanged);
        maxRagdollsSlider.onValueChanged.AddListener(HandleMaxRagdollsChanged);
        gameSpeedSlider.onValueChanged.AddListener(HandleGameSpeedChanged);
        if (invertXToggle != null) invertXToggle.onValueChanged.AddListener(HandleInvertXChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(HandleInvertYChanged);
        if (gamepadInvertXToggle != null) gamepadInvertXToggle.onValueChanged.AddListener(HandleInvertXChanged);
        if (gamepadInvertYToggle != null) gamepadInvertYToggle.onValueChanged.AddListener(HandleInvertYChanged);
        
        if (postProcessingEnabledToggle != null) postProcessingEnabledToggle.onValueChanged.AddListener(HandlePostProcessingEnabledChanged);
        if (postProcessingBloomSlider != null) postProcessingBloomSlider.onValueChanged.AddListener(HandlePostProcessingBloomChanged);
        if (postProcessingVignetteSlider != null) postProcessingVignetteSlider.onValueChanged.AddListener(HandlePostProcessingVignetteChanged);
        if (postProcessingExposureSlider != null) postProcessingExposureSlider.onValueChanged.AddListener(HandlePostProcessingExposureChanged);

        resetSettingsButton.onClick.AddListener(HandleResetSettingsClicked);
        deleteSaveButton.onClick.AddListener(HandleDeleteSaveClicked);
        generalTabButton.onClick.AddListener(ShowGeneralTab);
        controlsTabButton.onClick.AddListener(ShowControlsTab);
        if (gamepadTabButton != null) gamepadTabButton.onClick.AddListener(ShowGamepadTab);
        if (postProcessingTabButton != null) postProcessingTabButton.onClick.AddListener(ShowPostProcessingTab);
        AddTab(generalTabButton, generalTabContent);
        AddTab(controlsTabButton, controlsTabContent);
        AddTab(gamepadTabButton, gamepadTabContent);
        AddTab(postProcessingTabButton, postProcessingTabContent);

        tabActionMap = new InputActionMap("SettingsTabs");
        tabPrevAction = tabActionMap.AddAction("TabPrev", InputActionType.Button);
        tabPrevAction.AddBinding("<Keyboard>/q");
        tabPrevAction.AddBinding("<Gamepad>/leftShoulder");
        tabNextAction = tabActionMap.AddAction("TabNext", InputActionType.Button);
        tabNextAction.AddBinding("<Keyboard>/e");
        tabNextAction.AddBinding("<Gamepad>/rightShoulder");
        if (tabPrevGlyph != null) tabPrevGlyph.SetAction(tabPrevAction);
        if (tabNextGlyph != null) tabNextGlyph.SetAction(tabNextAction);

        // Pad B leaves the panel. Scene copies whose B wiring lost its target (the main menu's pointed at a
        // pause view that isn't there) fall back to Back().
        if (focusController != null && !focusController.HasCancelHandler)
        {
            focusController.AddCancelListener(Back);
        }
    }

    private void OnEnable()
    {
        if (anyError)
        {
            return;
        }
        RefreshFromSettings();
        BuildRebindRowsIfNeeded();
        ShowTab(0, instant: true);
        if (tabActionMap != null) tabActionMap.Enable();
    }

    private void OnDisable()
    {
        if (tabActionMap != null) tabActionMap.Disable();
    }

    private void Update()
    {
        if (anyError || tabActionMap == null || RebindButtonView.AnyRebindActive || (confirmDialog != null && confirmDialog.IsOpen))
        {
            return;
        }
        if (tabPrevAction.WasPressedThisFrame())
        {
            CycleTab(-1);
        }
        else if (tabNextAction.WasPressedThisFrame())
        {
            CycleTab(+1);
        }
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
        if (gamepadSensitivitySlider != null) gamepadSensitivitySlider.onValueChanged.RemoveListener(HandleGamepadSensitivityChanged);
        if (stickDeadzoneSlider != null) stickDeadzoneSlider.onValueChanged.RemoveListener(HandleStickDeadzoneChanged);
        if (aimAssistStrengthSlider != null) aimAssistStrengthSlider.onValueChanged.RemoveListener(HandleAimAssistStrengthChanged);
        if (aimAssistWindowSlider != null) aimAssistWindowSlider.onValueChanged.RemoveListener(HandleAimAssistWindowChanged);
        if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(HandleLanguageChanged);
        if (fieldOfViewSlider != null) fieldOfViewSlider.onValueChanged.RemoveListener(HandleFieldOfViewChanged);
        if (maxRagdollsSlider != null) maxRagdollsSlider.onValueChanged.RemoveListener(HandleMaxRagdollsChanged);
        if (gameSpeedSlider != null) gameSpeedSlider.onValueChanged.RemoveListener(HandleGameSpeedChanged);
        if (invertXToggle != null) invertXToggle.onValueChanged.RemoveListener(HandleInvertXChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.RemoveListener(HandleInvertYChanged);
        if (gamepadInvertXToggle != null) gamepadInvertXToggle.onValueChanged.RemoveListener(HandleInvertXChanged);
        if (gamepadInvertYToggle != null) gamepadInvertYToggle.onValueChanged.RemoveListener(HandleInvertYChanged);

        if (postProcessingEnabledToggle != null) postProcessingEnabledToggle.onValueChanged.RemoveListener(HandlePostProcessingEnabledChanged);
        if (postProcessingBloomSlider != null) postProcessingBloomSlider.onValueChanged.RemoveListener(HandlePostProcessingBloomChanged);
        if (postProcessingVignetteSlider != null) postProcessingVignetteSlider.onValueChanged.RemoveListener(HandlePostProcessingVignetteChanged);
        if (postProcessingExposureSlider != null) postProcessingExposureSlider.onValueChanged.RemoveListener(HandlePostProcessingExposureChanged);

        if (resetSettingsButton != null) resetSettingsButton.onClick.RemoveListener(HandleResetSettingsClicked);
        if (deleteSaveButton != null) deleteSaveButton.onClick.RemoveListener(HandleDeleteSaveClicked);
        if (generalTabButton != null) generalTabButton.onClick.RemoveListener(ShowGeneralTab);
        if (controlsTabButton != null) controlsTabButton.onClick.RemoveListener(ShowControlsTab);
        if (gamepadTabButton != null) gamepadTabButton.onClick.RemoveListener(ShowGamepadTab);
        if (postProcessingTabButton != null) postProcessingTabButton.onClick.RemoveListener(ShowPostProcessingTab);
        if (tabActionMap != null) tabActionMap.Dispose();
    }

    /// <summary>
    ///     Leaves the settings panel: back to the pause menu's buttons in game, back to the title screen on
    ///     the main menu. Public so it can be wired as a persistent B / Back listener.
    /// </summary>
    public void Back()
    {
        if (RebindButtonView.AnyRebindActive)
        {
            return;
        }
        PauseMenuView pauseView = GetComponentInParent<PauseMenuView>(true);
        if (pauseView != null)
        {
            pauseView.ShowMainButtons();
            return;
        }
        Bladehold.UI.MainMenuManager mainMenu = FindFirstObjectByType<Bladehold.UI.MainMenuManager>();
        if (mainMenu != null)
        {
            mainMenu.OnBackToTitle();
            return;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    ///     Clones the gamepad look sensitivity row (label, slider, typed value field, row highlight) into a
    ///     Stick Dead Zone row right below it, so every existing settings panel copy gets the setting
    ///     without regenerating its hierarchy. The row sits in an automatic-navigation list, so pad focus
    ///     reaches it like its neighbours.
    /// </summary>
    private static Slider BuildStickDeadzoneRow(Slider template) =>
        CloneSliderRow(template, "Row Stick Dead Zone", "settings.stick_deadzone");

    /// <summary>Clones <paramref name="template" />'s row right below it with a new label key; returns the clone's slider.</summary>
    private static Slider CloneSliderRow(Slider template, string rowName, string labelKey)
    {
        SliderValueField templateRow = template.GetComponentInParent<SliderValueField>();
        Transform row = templateRow != null ? templateRow.transform : template.transform.parent;
        if (row == null || row.parent == null)
        {
            return null;
        }
        GameObject clone = Instantiate(row.gameObject, row.parent);
        clone.name = rowName;
        clone.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
        LocalizedText label = clone.GetComponentInChildren<LocalizedText>(true);
        if (label != null)
        {
            label.SetKey(labelKey);
        }
        Slider slider = clone.GetComponentInChildren<Slider>(true);
        if (slider != null)
        {
            slider.onValueChanged.RemoveAllListeners();
        }
        return slider;
    }

    private static void ConfigureSlider(Slider slider, float min, float max, int decimals)
    {
        slider.wholeNumbers = false;
        slider.minValue = min;
        slider.maxValue = max;
        SliderValueField field = slider.GetComponentInParent<SliderValueField>();
        if (field != null) field.DecimalPlaces = decimals;
    }

    private void AddTab(Button button, GameObject content)
    {
        if (button != null && content != null)
        {
            tabButtons.Add(button);
            tabContents.Add(content);
        }
    }

    private void ShowGeneralTab() => ShowTab(tabContents.IndexOf(generalTabContent));
    private void ShowControlsTab() => ShowTab(tabContents.IndexOf(controlsTabContent));
    private void ShowGamepadTab() => ShowTab(tabContents.IndexOf(gamepadTabContent));
    private void ShowPostProcessingTab() => ShowTab(tabContents.IndexOf(postProcessingTabContent));

    private int TabCount => tabContents.Count;

    private void CycleTab(int direction)
    {
        int next = (currentTab + direction + TabCount) % TabCount;
        ShowTab(next);
        Button tabButton = tabButtons[next];
        UISelectableJuice juice = tabButton != null ? tabButton.GetComponent<UISelectableJuice>() : null;
        if (juice != null)
        {
            // Same squash + click sound as pressing the tab, so a shortcut switch feels like a press.
            juice.OnSubmit(null);
        }
    }

    private void ShowTab(int index, bool instant = false)
    {
        if (index < 0 || index >= TabCount)
        {
            return;
        }
        currentTab = index;
        for (int i = 0; i < TabCount; i++)
        {
            tabContents[i].SetActive(i == index);
            TintTabButton(tabButtons[i], i == index, instant);
        }
        FocusFirstControl(tabContents[index]);
    }

    /// <summary>Points pad focus (and the focus controller's default) at the tab's first navigable control.</summary>
    private void FocusFirstControl(GameObject content)
    {
        if (content == null)
        {
            return;
        }
        foreach (Selectable candidate in content.GetComponentsInChildren<Selectable>())
        {
            if (candidate.IsInteractable() && candidate.navigation.mode != Navigation.Mode.None && !(candidate is Scrollbar))
            {
                if (focusController != null)
                {
                    focusController.SetDefaultSelectable(candidate);
                }
                else if (InputDeviceWatcher.GamepadActive)
                {
                    candidate.Select();
                }
                return;
            }
        }
    }

    private void TintTabButton(Button tabButton, bool selected, bool instant)
    {
        if (tabButton == null)
        {
            return;
        }
        SettingsTabButton styled = tabButton.GetComponent<SettingsTabButton>();
        if (styled != null)
        {
            styled.SetSelected(selected, instant);
            return;
        }
        if (tabButton.targetGraphic != null)
        {
            tabButton.targetGraphic.color = selected ? tabSelectedColor : tabUnselectedColor;
            var txt = tabButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.color = selected ? tabSelectedTextColor : tabUnselectedTextColor;
            }
        }
    }

    private void RefreshFromSettings()
    {
        GameSettingsService settings = GameSettingsService.Instance;
        if (settings == null)
        {
            return;
        }

        masterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
        musicVolumeSlider.SetValueWithoutNotify(settings.MusicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(settings.SfxVolume);
        sensitivitySlider.SetValueWithoutNotify(settings.Sensitivity);
        fieldOfViewSlider.SetValueWithoutNotify(settings.FieldOfView);
        maxRagdollsSlider.SetValueWithoutNotify(settings.MaxRagdolls);
        gameSpeedSlider.SetValueWithoutNotify(settings.GameSpeed);
        if (invertXToggle != null) invertXToggle.SetIsOnWithoutNotify(settings.InvertX);
        if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(settings.InvertY);
        if (gamepadInvertXToggle != null) gamepadInvertXToggle.SetIsOnWithoutNotify(settings.InvertX);
        if (gamepadInvertYToggle != null) gamepadInvertYToggle.SetIsOnWithoutNotify(settings.InvertY);
        if (gamepadSensitivitySlider != null) gamepadSensitivitySlider.SetValueWithoutNotify(settings.GamepadSensitivity);
        if (stickDeadzoneSlider != null) stickDeadzoneSlider.SetValueWithoutNotify(settings.StickDeadzone);
        if (aimAssistStrengthSlider != null) aimAssistStrengthSlider.SetValueWithoutNotify(settings.AimAssistStrength);
        if (aimAssistWindowSlider != null) aimAssistWindowSlider.SetValueWithoutNotify(settings.AimAssistWindow);
        if (languageDropdown != null) languageDropdown.SetValueWithoutNotify(LanguageCodeToIndex(settings.LanguageCode));
        if (postProcessingEnabledToggle != null) postProcessingEnabledToggle.SetIsOnWithoutNotify(settings.PostProcessingEnabled);
        if (postProcessingBloomSlider != null) postProcessingBloomSlider.SetValueWithoutNotify(settings.PostProcessingBloom);
        if (postProcessingVignetteSlider != null) postProcessingVignetteSlider.SetValueWithoutNotify(settings.PostProcessingVignette);
        if (postProcessingExposureSlider != null) postProcessingExposureSlider.SetValueWithoutNotify(settings.PostProcessingExposure);
    }

    /// <summary>
    ///     Dropdown option 0 is "Auto (System)" (persisted as ""); the rest are
    ///     <see cref="Loc.SupportedLanguages" /> in order, each shown in its own native name —
    ///     a language name is its own localization, so these stay hardcoded.
    /// </summary>
    private static readonly string[] LanguageNativeNames =
        { "English", "Français", "Italiano", "Deutsch", "Español", "Русский", "简体中文", "日本語", "한국어" };

    private void BuildLanguageOptions()
    {
        var options = new List<string> { Loc.Get("settings.language_auto") };
        for (int i = 0; i < Loc.SupportedLanguages.Length; i++)
        {
            options.Add(i < LanguageNativeNames.Length ? LanguageNativeNames[i] : Loc.SupportedLanguages[i]);
        }
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(options);
    }

    private static int LanguageCodeToIndex(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return 0;
        }
        int index = System.Array.IndexOf(Loc.SupportedLanguages, code);
        return index >= 0 ? index + 1 : 0;
    }

    private void HandleLanguageChanged(int index)
    {
        string code = index <= 0 || index > Loc.SupportedLanguages.Length ? "" : Loc.SupportedLanguages[index - 1];
        GameSettingsService.Instance?.SetLanguage(code);
    }

    private void BuildRebindRowsIfNeeded()
    {
        if (rebindRowsBuilt)
        {
            return;
        }

        InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
            ? Player.Instance.InputSettings.GetRebindableActionMap()
            : new Controls().Player.Get();

        if (map == null)
        {
            return;
        }

        foreach (InputAction action in map.actions)
        {
            foreach (RowSlot slot in BuildRowSlots(action))
            {
                if (gamepadRebindListParent == null)
                {
                    RebindButtonView row = Instantiate(rebindRowPrefab, rebindListParent);
                    row.Bind(action, slot.kbmIndex, slot.gamepadIndex, slot.label);
                    rebindRows.Add(row);
                    continue;
                }
                // Split tabs: one single-column row per device, and no row for a device the action has no binding on.
                if (slot.kbmIndex >= 0)
                {
                    RebindButtonView row = Instantiate(rebindRowPrefab, rebindListParent);
                    row.Bind(action, slot.kbmIndex, -1, slot.label);
                    row.ShowSingleColumn(gamepad: false);
                    rebindRows.Add(row);
                }
                if (slot.gamepadIndex >= 0)
                {
                    RebindButtonView row = Instantiate(rebindRowPrefab, gamepadRebindListParent);
                    row.Bind(action, -1, slot.gamepadIndex, slot.label);
                    row.ShowSingleColumn(gamepad: true);
                    gamepadRebindRows.Add(row);
                }
            }
        }

        WireRebindGridNavigation();
        rebindRowsBuilt = true;
    }

    /// <summary>
    ///     Explicit gamepad navigation over the generated rebind grid — Unity's automatic mode gets
    ///     lost in the label + two-button rows (it happily jumps columns diagonally). Each column
    ///     links vertically to the nearest enabled button; the two columns of a row link
    ///     horizontally. Split device tabs are single-column lists with no sideways links.
    /// </summary>
    private void WireRebindGridNavigation()
    {
        if (gamepadRebindListParent != null)
        {
            for (int i = 0; i < rebindRows.Count; i++)
            {
                SetColumnNavigation(rebindRows[i].KbmButton, FindColumnNeighbor(rebindRows, i, -1, true, rebindGridAbove), FindColumnNeighbor(rebindRows, i, +1, true, rebindGridBelow), null, null);
            }
            for (int i = 0; i < gamepadRebindRows.Count; i++)
            {
                SetColumnNavigation(gamepadRebindRows[i].GamepadButton, FindColumnNeighbor(gamepadRebindRows, i, -1, false, gamepadRebindGridAbove), FindColumnNeighbor(gamepadRebindRows, i, +1, false, rebindGridBelow), null, null);
            }
            return;
        }
        for (int i = 0; i < rebindRows.Count; i++)
        {
            SetColumnNavigation(rebindRows[i].KbmButton, FindColumnNeighbor(rebindRows, i, -1, true, rebindGridAbove), FindColumnNeighbor(rebindRows, i, +1, true, rebindGridBelow), null, rebindRows[i].GamepadButton);
            SetColumnNavigation(rebindRows[i].GamepadButton, FindColumnNeighbor(rebindRows, i, -1, false, rebindGridAbove), FindColumnNeighbor(rebindRows, i, +1, false, rebindGridBelow), rebindRows[i].KbmButton, null);
        }
    }

    private static Selectable FindColumnNeighbor(List<RebindButtonView> rows, int rowIndex, int direction, bool kbmColumn, Selectable offEnd)
    {
        for (int i = rowIndex + direction; i >= 0 && i < rows.Count; i += direction)
        {
            Button candidate = kbmColumn ? rows[i].KbmButton : rows[i].GamepadButton;
            if (candidate != null && candidate.interactable)
            {
                return candidate;
            }
        }
        // Off either end of the grid: hand off to the controls around it.
        return offEnd;
    }

    private static void SetColumnNavigation(Button button, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (button == null)
        {
            return;
        }
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up;
        nav.selectOnDown = down;
        nav.selectOnLeft = left;
        nav.selectOnRight = right;
        button.navigation = nav;
    }

    /// <summary>
    ///     Pairs an action's bindings into rows with a Keyboard/Mouse and a Gamepad column: bindings
    ///     sharing a display label (e.g. "Aim" on RMB and LT) fill the two columns of one row; a
    ///     second binding for a column that's already taken (e.g. the arrow-key alternates to WASD)
    ///     gets its own "(Alt)" row with the other column empty.
    /// </summary>
    private static List<RowSlot> BuildRowSlots(InputAction action)
    {
        var slots = new List<RowSlot>();
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite)
            {
                // The composite header itself isn't bindable — only its parts are.
                continue;
            }

            string displayLabel = binding.isPartOfComposite ? $"{action.name} {binding.name}" : action.name;
            bool isGamepad = IsGamepadBinding(binding);

            RowSlot slot = slots.Find(s => s.label == displayLabel && (isGamepad ? s.gamepadIndex : s.kbmIndex) < 0);
            if (slot == null)
            {
                string label = slots.Exists(s => s.label == displayLabel) ? $"{displayLabel} (Alt)" : displayLabel;
                slot = new RowSlot { label = label };
                slots.Add(slot);
            }

            if (isGamepad) slot.gamepadIndex = i;
            else slot.kbmIndex = i;
        }
        return slots;
    }

    /// <summary>
    ///     Column identity comes from the binding's authored path, not its current override, so a
    ///     row keeps its column even if the player rebinds it to a different device's control.
    /// </summary>
    private static bool IsGamepadBinding(InputBinding binding)
    {
        return binding.path != null && binding.path.StartsWith("<Gamepad>");
    }

    private void HandleMasterVolumeChanged(float value) => GameSettingsService.Instance?.SetMasterVolume(value);
    private void HandleMusicVolumeChanged(float value) => GameSettingsService.Instance?.SetMusicVolume(value);
    private void HandleSfxVolumeChanged(float value) => GameSettingsService.Instance?.SetSfxVolume(value);
    private void HandleSensitivityChanged(float value) => GameSettingsService.Instance?.SetSensitivity(value);
    private void HandleGamepadSensitivityChanged(float value) => GameSettingsService.Instance?.SetGamepadSensitivity(value);
    private void HandleStickDeadzoneChanged(float value) => GameSettingsService.Instance?.SetStickDeadzone(value);
    private void HandleAimAssistStrengthChanged(float value) => GameSettingsService.Instance?.SetAimAssistStrength(value);
    private void HandleAimAssistWindowChanged(float value) => GameSettingsService.Instance?.SetAimAssistWindow(value);
    private void HandleFieldOfViewChanged(float value) => GameSettingsService.Instance?.SetFieldOfView(value);
    private void HandleMaxRagdollsChanged(float value) => GameSettingsService.Instance?.SetMaxRagdolls(Mathf.RoundToInt(value));
    private void HandleGameSpeedChanged(float value) => GameSettingsService.Instance?.SetGameSpeed(value);
    private void HandleInvertXChanged(bool value)
    {
        GameSettingsService.Instance?.SetInvertX(value);
        if (invertXToggle != null) invertXToggle.SetIsOnWithoutNotify(value);
        if (gamepadInvertXToggle != null) gamepadInvertXToggle.SetIsOnWithoutNotify(value);
    }

    private void HandleInvertYChanged(bool value)
    {
        GameSettingsService.Instance?.SetInvertY(value);
        if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(value);
        if (gamepadInvertYToggle != null) gamepadInvertYToggle.SetIsOnWithoutNotify(value);
    }
    
    private void HandlePostProcessingEnabledChanged(bool value) => GameSettingsService.Instance?.SetPostProcessingEnabled(value);
    private void HandlePostProcessingBloomChanged(float value) => GameSettingsService.Instance?.SetPostProcessingBloom(value);
    private void HandlePostProcessingVignetteChanged(float value) => GameSettingsService.Instance?.SetPostProcessingVignette(value);
    private void HandlePostProcessingExposureChanged(float value) => GameSettingsService.Instance?.SetPostProcessingExposure(value);

    private void HandleResetSettingsClicked()
    {
        confirmDialog.Show(
            Loc.Get("settings.reset_confirm"),
            () =>
            {
                GameSettingsService settings = GameSettingsService.Instance;
                if (settings == null)
                {
                    return;
                }

                settings.ResetToDefaults();
                RefreshFromSettings();
                foreach (RebindButtonView row in rebindRows)
                {
                    row.RefreshPathLabel();
                }
                foreach (RebindButtonView row in gamepadRebindRows)
                {
                    row.RefreshPathLabel();
                }
            },
            confirmLabel: Loc.Get("settings.reset"));
    }

    private void HandleDeleteSaveClicked()
    {
        confirmDialog.Show(
            Loc.Get("settings.delete_save_confirm"),
            () =>
            {
                // Wipe only the progress half of the save — settings survive a save wipe.
                SaveData data = SaveSystem.Load();
                data.ResetProgress();
                SaveSystem.Save(data);

                RunSession.ClearRun(); // a fresh save shouldn't resume mid-run.
                Time.timeScale = GameSettingsService.TargetTimeScale; // ensure the reloaded scene doesn't start paused.
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            },
            confirmLabel: "Delete");
    }
}
