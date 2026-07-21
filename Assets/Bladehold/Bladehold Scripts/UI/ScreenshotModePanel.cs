using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Photo Mode's on-screen controls: one row per <see cref="PhotoSetting" /> (slider + reset
///     button), plus Take Photo/Exit — all delegated straight to
///     <see cref="ScreenshotModeController" />, which owns caching/restoring the original values so
///     nothing here leaks into normal gameplay. Shown/hidden by <see cref="PauseMenuView" /> via
///     <see cref="ScreenshotModeController.OnActiveChanged" />; because being shown *is* Photo Mode
///     being entered, <see cref="OnEnable" /> syncs every slider to the scene's current values, and
///     each reset button restores its setting to the value captured on enter.
/// </summary>
public class ScreenshotModePanel : MonoBehaviour
{
    [Serializable]
    private class SettingRow
    {
        public PhotoSetting setting;
        [Tooltip("Optional: slider for float settings.")]
        public Slider slider;
        [Tooltip("Optional: toggle for boolean settings.")]
        public Toggle toggle;
        [Tooltip("Optional: resets the slider to the value the setting had when Photo Mode was entered.")]
        public Button resetButton;
    }

    [SerializeField] private ScreenshotModeController screenshotMode;
    [Tooltip("One row per adjustable setting; built and wired by Bladehold > Generate Settings Menu.")]
    [SerializeField] private SettingRow[] rows;

    [Header("Actions")]
    [SerializeField] private Button captureButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text savedLabel;
    [SerializeField] private float savedLabelSeconds = 2f;

    private bool anyError = false;
    private float savedLabelTimer;

    private void OnValidate()
    {
        if (screenshotMode == null)
        {
            screenshotMode = GetComponentInParent<ScreenshotModeController>();
        }
    }

    private void Start()
    {
        if (screenshotMode == null)
        {
            Debug.LogError("ScreenshotModeController is not assigned or found for ScreenshotModePanel.");
            anyError = true;
        }
        if (captureButton == null || exitButton == null)
        {
            Debug.LogError("Capture/Exit buttons are not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        rows ??= new SettingRow[0];

        foreach (SettingRow row in rows)
        {
            PhotoSetting setting = row.setting;
            
            if (row.slider != null)
            {
                Slider slider = row.slider;
                slider.onValueChanged.AddListener(value => screenshotMode.Set(setting, value));
                if (row.resetButton != null)
                {
                    row.resetButton.onClick.AddListener(() =>
                        slider.value = Mathf.Clamp(screenshotMode.GetEnterValue(setting), slider.minValue, slider.maxValue));
                }
            }
            else if (row.toggle != null)
            {
                Toggle toggle = row.toggle;
                toggle.onValueChanged.AddListener(value => screenshotMode.Set(setting, value ? 1f : 0f));
                if (row.resetButton != null)
                {
                    row.resetButton.onClick.AddListener(() =>
                        toggle.isOn = screenshotMode.GetEnterValue(setting) > 0.5f);
                }
            }
        }

        captureButton.onClick.AddListener(screenshotMode.Capture);
        exitButton.onClick.AddListener(screenshotMode.Exit);

        screenshotMode.OnScreenshotSaved += HandleScreenshotSaved;

        if (savedLabel != null) savedLabel.gameObject.SetActive(false);

        // This panel starts inactive, so its first activation (= the first Photo Mode enter) runs
        // OnEnable before Start; sync again now that everything is validated.
        SyncRowsFromScene();
    }

    private void OnEnable()
    {
        SyncRowsFromScene();
    }

    private void OnDestroy()
    {
        if (screenshotMode != null)
        {
            screenshotMode.OnScreenshotSaved -= HandleScreenshotSaved;
            captureButton?.onClick.RemoveListener(screenshotMode.Capture);
            exitButton?.onClick.RemoveListener(screenshotMode.Exit);
        }

        if (rows == null)
        {
            return;
        }

        // The slider/reset listeners are closures, so they can't be removed individually — this panel
        // is the only thing that wires them, so clearing is safe.
        foreach (SettingRow row in rows)
        {
            if (row.slider != null) row.slider.onValueChanged.RemoveAllListeners();
            if (row.toggle != null) row.toggle.onValueChanged.RemoveAllListeners();
            if (row.resetButton != null) row.resetButton.onClick.RemoveAllListeners();
        }
    }

    private void Update()
    {
        if (savedLabelTimer <= 0f || savedLabel == null)
        {
            return;
        }

        savedLabelTimer -= Time.unscaledDeltaTime;
        if (savedLabelTimer <= 0f)
        {
            savedLabel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    ///     Points every slider at the scene's actual current value so the panel opens truthful — the
    ///     first drag adjusts *from* the real value instead of jumping to a stale slider position.
    /// </summary>
    private void SyncRowsFromScene()
    {
        if (screenshotMode == null || !screenshotMode.IsActive || rows == null)
        {
            return;
        }

        foreach (SettingRow row in rows)
        {
            float sceneValue = screenshotMode.Get(row.setting);

            if (row.slider != null)
            {
                float clamped = Mathf.Clamp(sceneValue, row.slider.minValue, row.slider.maxValue);
                row.slider.SetValueWithoutNotify(clamped);
            }
            else if (row.toggle != null)
            {
                row.toggle.SetIsOnWithoutNotify(sceneValue > 0.5f);
            }
        }
    }

    private void HandleScreenshotSaved(string path)
    {
        if (savedLabel == null)
        {
            return;
        }

        savedLabel.text = Loc.Format("screenshot.saved_to", path);
        savedLabel.gameObject.SetActive(true);
        savedLabelTimer = savedLabelSeconds;
    }
}
