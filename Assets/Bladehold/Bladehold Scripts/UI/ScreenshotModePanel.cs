using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Photo Mode's on-screen controls: sun + basic post-processing sliders, FOV, and Capture — all
///     delegated straight to <see cref="ScreenshotModeController" />, which owns caching/restoring the
///     original values so nothing here leaks into normal gameplay. Shown/hidden by
///     <see cref="PauseMenuView" /> via <see cref="ScreenshotModeController.OnActiveChanged" />; this
///     view just wires its own controls.
/// </summary>
public class ScreenshotModePanel : MonoBehaviour
{
    [SerializeField] private ScreenshotModeController screenshotMode;

    [Header("Sun")]
    [SerializeField] private Slider sunIntensitySlider;
    [SerializeField] private Slider sunPitchSlider;

    [Header("Post-processing")]
    [SerializeField] private Slider bloomSlider;
    [SerializeField] private Slider vignetteSlider;
    [SerializeField] private Slider exposureSlider;
    [SerializeField] private Slider contrastSlider;
    [SerializeField] private Slider saturationSlider;
    [SerializeField] private Slider focusDistanceSlider;
    [SerializeField] private Slider apertureSlider;
    [SerializeField] private Slider fovSlider;

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

        if (sunIntensitySlider != null) sunIntensitySlider.onValueChanged.AddListener(screenshotMode.SetSunIntensity);
        if (sunPitchSlider != null) sunPitchSlider.onValueChanged.AddListener(screenshotMode.SetSunPitch);
        if (bloomSlider != null) bloomSlider.onValueChanged.AddListener(screenshotMode.SetBloomIntensity);
        if (vignetteSlider != null) vignetteSlider.onValueChanged.AddListener(screenshotMode.SetVignetteIntensity);
        if (exposureSlider != null) exposureSlider.onValueChanged.AddListener(screenshotMode.SetExposure);
        if (contrastSlider != null) contrastSlider.onValueChanged.AddListener(screenshotMode.SetContrast);
        if (saturationSlider != null) saturationSlider.onValueChanged.AddListener(screenshotMode.SetSaturation);
        if (focusDistanceSlider != null) focusDistanceSlider.onValueChanged.AddListener(screenshotMode.SetFocusDistance);
        if (apertureSlider != null) apertureSlider.onValueChanged.AddListener(screenshotMode.SetAperture);
        if (fovSlider != null) fovSlider.onValueChanged.AddListener(screenshotMode.SetFieldOfView);

        captureButton.onClick.AddListener(screenshotMode.Capture);
        exitButton.onClick.AddListener(screenshotMode.Exit);

        screenshotMode.OnScreenshotSaved += HandleScreenshotSaved;

        if (savedLabel != null) savedLabel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (screenshotMode != null)
        {
            screenshotMode.OnScreenshotSaved -= HandleScreenshotSaved;
            captureButton?.onClick.RemoveListener(screenshotMode.Capture);
            exitButton?.onClick.RemoveListener(screenshotMode.Exit);
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

    private void HandleScreenshotSaved(string path)
    {
        if (savedLabel == null)
        {
            return;
        }

        savedLabel.text = $"Saved to {path}";
        savedLabel.gameObject.SetActive(true);
        savedLabelTimer = savedLabelSeconds;
    }
}
