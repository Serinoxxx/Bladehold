using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Settings sub-panel shown from the pause menu: audio sliders, sensitivity/invert controls, a
///     generically-built list of every remappable binding on the vendored Controls asset, and Delete
///     Save. Every control reads and writes through <see cref="GameSettingsService" /> — this view
///     never touches <see cref="SaveData" /> or the vendored input asset directly. Refreshes from
///     current settings whenever shown.
/// </summary>
public class SettingsPanelView : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Controls")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Toggle invertXToggle;
    [SerializeField] private Toggle invertYToggle;

    [Header("Performance")]
    [SerializeField] private Slider maxRagdollsSlider;
    [Tooltip("Parent under which one RebindButtonView is instantiated per remappable binding.")]
    [SerializeField] private Transform rebindListParent;
    [SerializeField] private RebindButtonView rebindRowPrefab;

    [Header("Delete save")]
    [SerializeField] private Button deleteSaveButton;
    [SerializeField] private ConfirmDialog confirmDialog;

    private bool rebindRowsBuilt = false;
    private bool anyError = false;

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
        if (maxRagdollsSlider == null)
        {
            Debug.LogError("Max Ragdolls slider is not assigned in the inspector.");
            anyError = true;
        }
        if (deleteSaveButton == null || confirmDialog == null)
        {
            Debug.LogError("Delete Save button/ConfirmDialog is not assigned in the inspector.");
            anyError = true;
        }
        if (rebindListParent == null || rebindRowPrefab == null)
        {
            Debug.LogError("Rebind list parent/prefab is not assigned in the inspector.");
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
        maxRagdollsSlider.onValueChanged.AddListener(HandleMaxRagdollsChanged);
        if (invertXToggle != null) invertXToggle.onValueChanged.AddListener(HandleInvertXChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(HandleInvertYChanged);
        deleteSaveButton.onClick.AddListener(HandleDeleteSaveClicked);
    }

    private void OnEnable()
    {
        if (anyError)
        {
            return;
        }
        RefreshFromSettings();
        BuildRebindRowsIfNeeded();
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
        if (maxRagdollsSlider != null) maxRagdollsSlider.onValueChanged.RemoveListener(HandleMaxRagdollsChanged);
        if (invertXToggle != null) invertXToggle.onValueChanged.RemoveListener(HandleInvertXChanged);
        if (invertYToggle != null) invertYToggle.onValueChanged.RemoveListener(HandleInvertYChanged);
        if (deleteSaveButton != null) deleteSaveButton.onClick.RemoveListener(HandleDeleteSaveClicked);
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
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite)
                {
                    // The composite header itself isn't bindable — only its parts are.
                    continue;
                }

                string displayLabel = binding.isPartOfComposite ? $"{action.name} {binding.name}" : action.name;
                RebindButtonView row = Instantiate(rebindRowPrefab, rebindListParent);
                row.Bind(action, i, displayLabel);
            }
        }

        rebindRowsBuilt = true;
    }

    private void HandleMasterVolumeChanged(float value) => GameSettingsService.Instance?.SetMasterVolume(value);
    private void HandleMusicVolumeChanged(float value) => GameSettingsService.Instance?.SetMusicVolume(value);
    private void HandleSfxVolumeChanged(float value) => GameSettingsService.Instance?.SetSfxVolume(value);
    private void HandleSensitivityChanged(float value) => GameSettingsService.Instance?.SetSensitivity(value);
    private void HandleMaxRagdollsChanged(float value) => GameSettingsService.Instance?.SetMaxRagdolls(Mathf.RoundToInt(value));
    private void HandleInvertXChanged(bool value) => GameSettingsService.Instance?.SetInvertX(value);
    private void HandleInvertYChanged(bool value) => GameSettingsService.Instance?.SetInvertY(value);

    private void HandleDeleteSaveClicked()
    {
        confirmDialog.Show(
            "Delete all saved progress? This cannot be undone.",
            () =>
            {
                SaveSystem.DeleteCurrentSave();
                Time.timeScale = 1f; // ensure the reloaded scene doesn't start paused.
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            });
    }
}
