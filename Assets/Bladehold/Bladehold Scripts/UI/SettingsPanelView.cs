using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Settings sub-panel shown from the pause menu, split into two tabs: <b>General</b> (audio
///     sliders, sensitivity/invert controls, a field of view slider, max ragdolls) and
///     <b>Controls</b> (a generically-built list of every remappable binding on the vendored
///     Controls asset, one row per action with separate Keyboard/Mouse and Gamepad columns).
///     Reset Settings (settings back to defaults, progress untouched) and Delete Save (progress
///     wiped, settings kept) sit below the tabs. Every control reads and writes through
///     <see cref="GameSettingsService" /> — this view never touches <see cref="SaveData" /> or the
///     vendored input asset directly. Refreshes from current settings whenever shown, always
///     reopening on the General tab.
/// </summary>
public class SettingsPanelView : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private Button generalTabButton;
    [SerializeField] private Button controlsTabButton;
    [SerializeField] private GameObject generalTabContent;
    [SerializeField] private GameObject controlsTabContent;
    [SerializeField] private Color tabSelectedColor = new Color(0.3f, 0.45f, 0.55f);
    [SerializeField] private Color tabUnselectedColor = new Color(0.2f, 0.2f, 0.24f);

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Controls")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Toggle invertXToggle;
    [SerializeField] private Toggle invertYToggle;

    [Header("Video")]
    [SerializeField] private Slider fieldOfViewSlider;

    [Header("Performance")]
    [SerializeField] private Slider maxRagdollsSlider;
    [Tooltip("Parent under which one RebindButtonView is instantiated per remappable action row.")]
    [SerializeField] private Transform rebindListParent;
    [SerializeField] private RebindButtonView rebindRowPrefab;

    [Header("Reset / delete")]
    [SerializeField] private Button resetSettingsButton;
    [SerializeField] private Button deleteSaveButton;
    [SerializeField] private ConfirmDialog confirmDialog;

    private readonly List<RebindButtonView> rebindRows = new List<RebindButtonView>();
    private bool rebindRowsBuilt = false;
    private bool anyError = false;

    /// <summary>One rebind row being assembled: an action's KBM and Gamepad bindings paired by display label.</summary>
    private class RowSlot
    {
        public string label;
        public int kbmIndex = -1;
        public int gamepadIndex = -1;
    }

    private void Start()
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
        if (maxRagdollsSlider == null)
        {
            Debug.LogError("Max Ragdolls slider is not assigned in the inspector.");
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

        if (anyError)
        {
            return;
        }

        masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
        fieldOfViewSlider.onValueChanged.AddListener(HandleFieldOfViewChanged);
        maxRagdollsSlider.onValueChanged.AddListener(HandleMaxRagdollsChanged);
        if (invertXToggle != null) invertXToggle.onValueChanged.AddListener(HandleInvertXChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(HandleInvertYChanged);
        resetSettingsButton.onClick.AddListener(HandleResetSettingsClicked);
        deleteSaveButton.onClick.AddListener(HandleDeleteSaveClicked);
        generalTabButton.onClick.AddListener(ShowGeneralTab);
        controlsTabButton.onClick.AddListener(ShowControlsTab);
    }

    private void OnEnable()
    {
        if (anyError)
        {
            return;
        }
        RefreshFromSettings();
        BuildRebindRowsIfNeeded();
        ShowGeneralTab();
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
        if (fieldOfViewSlider != null) fieldOfViewSlider.onValueChanged.RemoveListener(HandleFieldOfViewChanged);
        if (maxRagdollsSlider != null) maxRagdollsSlider.onValueChanged.RemoveListener(HandleMaxRagdollsChanged);
        if (invertXToggle != null) invertXToggle.onValueChanged.RemoveListener(HandleInvertXChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.RemoveListener(HandleInvertYChanged);
        if (resetSettingsButton != null) resetSettingsButton.onClick.RemoveListener(HandleResetSettingsClicked);
        if (deleteSaveButton != null) deleteSaveButton.onClick.RemoveListener(HandleDeleteSaveClicked);
        if (generalTabButton != null) generalTabButton.onClick.RemoveListener(ShowGeneralTab);
        if (controlsTabButton != null) controlsTabButton.onClick.RemoveListener(ShowControlsTab);
    }

    private void ShowGeneralTab() => ShowTab(general: true);
    private void ShowControlsTab() => ShowTab(general: false);

    private void ShowTab(bool general)
    {
        if (generalTabContent != null) generalTabContent.SetActive(general);
        if (controlsTabContent != null) controlsTabContent.SetActive(!general);
        TintTabButton(generalTabButton, general);
        TintTabButton(controlsTabButton, !general);
    }

    private void TintTabButton(Button tabButton, bool selected)
    {
        if (tabButton != null && tabButton.targetGraphic != null)
        {
            tabButton.targetGraphic.color = selected ? tabSelectedColor : tabUnselectedColor;
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
        if (invertXToggle != null) invertXToggle.SetIsOnWithoutNotify(settings.InvertX);
        if (invertYToggle != null) invertYToggle.SetIsOnWithoutNotify(settings.InvertY);
    }

    private void BuildRebindRowsIfNeeded()
    {
        if (rebindRowsBuilt)
        {
            return;
        }

        InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
            ? Player.Instance.InputSettings.GetRebindableActionMap()
            : null;

        if (map == null)
        {
            return;
        }

        foreach (InputAction action in map.actions)
        {
            foreach (RowSlot slot in BuildRowSlots(action))
            {
                RebindButtonView row = Instantiate(rebindRowPrefab, rebindListParent);
                row.Bind(action, slot.kbmIndex, slot.gamepadIndex, slot.label);
                rebindRows.Add(row);
            }
        }

        rebindRowsBuilt = true;
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
    private void HandleFieldOfViewChanged(float value) => GameSettingsService.Instance?.SetFieldOfView(value);
    private void HandleMaxRagdollsChanged(float value) => GameSettingsService.Instance?.SetMaxRagdolls(Mathf.RoundToInt(value));
    private void HandleInvertXChanged(bool value) => GameSettingsService.Instance?.SetInvertX(value);
    private void HandleInvertYChanged(bool value) => GameSettingsService.Instance?.SetInvertY(value);

    private void HandleResetSettingsClicked()
    {
        confirmDialog.Show(
            "Reset all settings to their defaults? Progress is not affected.",
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
            },
            confirmLabel: "Reset");
    }

    private void HandleDeleteSaveClicked()
    {
        confirmDialog.Show(
            "Delete all saved progress? Settings are kept. This cannot be undone.",
            () =>
            {
                // Wipe only the progress half of the save — settings survive a save wipe.
                SaveData data = SaveSystem.Load();
                data.ResetProgress();
                SaveSystem.Save(data);

                RunState.StartingWave = 1; // a fresh save shouldn't resume mid-run.
                Time.timeScale = 1f; // ensure the reloaded scene doesn't start paused.
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            },
            confirmLabel: "Delete");
    }
}
