using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
///     The adjustable Photo Mode settings — the shared vocabulary between
///     <see cref="ScreenshotModeController" /> (which knows how to apply each one) and
///     <see cref="ScreenshotModePanel" /> (which pairs each with a slider + reset button).
/// </summary>
public enum PhotoSetting
{
    SunIntensity,
    SunPitch,
    SunYaw,
    Bloom,
    Vignette,
    Exposure,
    Contrast,
    Saturation,
    FocusDistance,
    Aperture,
    FieldOfView,
    ToggleLensFlare,
    ToggleHUD,
    ToggleVignette,
    ToggleDepthOfField,
}

/// <summary>
///     Photo Mode: only reachable from the pause menu (see <see cref="PauseMenuController" />), so it
///     reuses the already-frozen/cursor-unlocked state rather than managing its own. Detaches the Main
///     Camera to a free-fly rig (<see cref="ScreenshotFlyCamera" />) and exposes the
///     <see cref="PhotoSetting" /> values on the sun light, the scene's post-processing
///     <see cref="Volume" />, and the camera through <see cref="Set" />/<see cref="Get" /> for
///     <see cref="ScreenshotModePanel" /> to bind sliders to. Everything touched here — camera
///     pose/FOV, light, and any Volume overrides added on the fly (<c>ColorAdjustments</c>/
///     <c>DepthOfField</c> aren't on the authored profile yet) — is cached on <see cref="Enter" />
///     (readable per-setting via <see cref="GetEnterValue" />, which is what the panel's reset buttons
///     restore) and restored on <see cref="Exit" />, so nothing leaks into normal gameplay.
/// </summary>
public class ScreenshotModeController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ScreenshotFlyCamera flyCamera;
    [Tooltip("Optional: the scene's directional sun light, for the intensity/pitch/yaw sliders.")]
    [SerializeField] private Light sunLight;
    [Tooltip("Optional: the scene's Global Volume, for the post-processing sliders.")]
    [SerializeField] private Volume globalVolume;
    [Tooltip("Canvas groups hidden for the single frame a screenshot is captured, so the UI doesn't appear in it.")]
    [SerializeField] private CanvasGroup[] hideOnCapture;
    [Tooltip("Optional: HUD canvas group to toggle during photo mode.")]
    [SerializeField] private CanvasGroup hudCanvasGroup;
    [SerializeField] private string screenshotFolderName = "Screenshots";

    public bool IsActive { get; private set; }

    /// <summary>Raised on enter/exit so menu views can swap which panel is shown.</summary>
    public event Action<bool> OnActiveChanged;

    /// <summary>Raised after a screenshot has been written to disk, carrying the full file path.</summary>
    public event Action<string> OnScreenshotSaved;

    private bool anyError = false;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private float originalFov;

    private float originalSunIntensity;
    private Quaternion originalSunRotation;

    private Bloom bloom;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private DepthOfField depthOfField;
    private bool colorAdjustmentsExisted, depthOfFieldExisted;
    private float cachedBloomIntensity, cachedVignetteIntensity;
    private bool cachedBloomOverride, cachedVignetteOverride;
    private float cachedExposure, cachedContrast, cachedSaturation;
    private bool cachedExposureOverride, cachedContrastOverride, cachedSaturationOverride;
    private float cachedFocusDistance, cachedAperture;
    private bool cachedFocusDistanceOverride, cachedApertureOverride;
    
    private UnityEngine.Rendering.LensFlareComponentSRP lensFlare;
    private bool cachedLensFlareActive;

    private bool originalHudActive;
    private bool cachedVignetteActive;
    private bool cachedDepthOfFieldActive;

    private void OnValidate()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        if (flyCamera == null && mainCamera != null)
        {
            flyCamera = mainCamera.GetComponent<ScreenshotFlyCamera>();
        }
        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<Volume>();
        }
        if (sunLight == null)
        {
            sunLight = RenderSettings.sun;
        }
    }

    private void Start()
    {
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera is not assigned or found for ScreenshotModeController.");
            anyError = true;
        }
        if (flyCamera == null)
        {
            Debug.LogError("ScreenshotFlyCamera is not assigned or found on the Main Camera.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        flyCamera.enabled = false;
    }

    public void Enter()
    {
        if (anyError || IsActive || PauseMenuController.Instance == null || !PauseMenuController.Instance.IsPaused)
        {
            return;
        }

        IsActive = true;

        Transform camTransform = mainCamera.transform;
        originalParent = camTransform.parent;
        originalLocalPosition = camTransform.localPosition;
        originalLocalRotation = camTransform.localRotation;
        originalFov = mainCamera.fieldOfView;
        camTransform.SetParent(null, true);

        if (sunLight != null)
        {
            originalSunIntensity = sunLight.intensity;
            originalSunRotation = sunLight.transform.rotation;
            lensFlare = sunLight.GetComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
            if (lensFlare == null)
            {
                lensFlare = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Rendering.LensFlareComponentSRP>();
            }
            if (lensFlare != null)
            {
                cachedLensFlareActive = lensFlare.enabled;
            }
        }
        else
        {
            lensFlare = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Rendering.LensFlareComponentSRP>();
            if (lensFlare != null)
            {
                cachedLensFlareActive = lensFlare.enabled;
            }
        }

        if (hudCanvasGroup == null)
        {
            GameObject hudGO = GameObject.Find("Bladehold HUD") ?? GameObject.Find("HUD Canvas") ?? GameObject.Find("HUD");
            if (hudGO != null) hudCanvasGroup = hudGO.GetComponent<CanvasGroup>();
        }

        if (hudCanvasGroup != null)
        {
            originalHudActive = hudCanvasGroup.alpha > 0.01f;
        }

        CacheAndEnsureVolumeOverrides();

        flyCamera.Bind(PauseMenuController.Instance.Actions);
        flyCamera.enabled = true;

        PauseMenuController.Instance.Actions.EnableScreenshotFly();
        PauseMenuController.Instance.Actions.Capture.performed += HandleCapturePerformed;

        OnActiveChanged?.Invoke(true);
    }

    public void Exit()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;

        flyCamera.enabled = false;
        if (PauseMenuController.Instance != null)
        {
            PauseMenuController.Instance.Actions.Capture.performed -= HandleCapturePerformed;
            PauseMenuController.Instance.Actions.DisableScreenshotFly();
        }

        Transform camTransform = mainCamera.transform;
        camTransform.SetParent(originalParent, false);
        camTransform.localPosition = originalLocalPosition;
        camTransform.localRotation = originalLocalRotation;
        mainCamera.fieldOfView = originalFov;

        if (sunLight != null)
        {
            sunLight.intensity = originalSunIntensity;
            sunLight.transform.rotation = originalSunRotation;
        }

        if (lensFlare != null)
        {
            lensFlare.enabled = cachedLensFlareActive;
        }

        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = originalHudActive ? 1f : 0f;
            hudCanvasGroup.interactable = originalHudActive;
            hudCanvasGroup.blocksRaycasts = originalHudActive;
        }

        RestoreVolumeOverrides();

        OnActiveChanged?.Invoke(false);
    }

    public void Set(PhotoSetting setting, float value)
    {
        switch (setting)
        {
            case PhotoSetting.SunIntensity:
                if (sunLight != null) sunLight.intensity = value;
                break;
            case PhotoSetting.SunPitch:
                SetSunAngles(value, null);
                break;
            case PhotoSetting.SunYaw:
                SetSunAngles(null, value);
                break;
            case PhotoSetting.Bloom:
                if (bloom != null) ApplyOverride(bloom.intensity, value);
                break;
            case PhotoSetting.Vignette:
                if (vignette != null) ApplyOverride(vignette.intensity, value);
                break;
            case PhotoSetting.Exposure:
                if (colorAdjustments != null) ApplyOverride(colorAdjustments.postExposure, value);
                break;
            case PhotoSetting.Contrast:
                if (colorAdjustments != null) ApplyOverride(colorAdjustments.contrast, value);
                break;
            case PhotoSetting.Saturation:
                if (colorAdjustments != null) ApplyOverride(colorAdjustments.saturation, value);
                break;
            case PhotoSetting.FocusDistance:
                if (depthOfField != null) ApplyOverride(depthOfField.focusDistance, value);
                break;
            case PhotoSetting.Aperture:
                if (depthOfField != null) ApplyOverride(depthOfField.aperture, value);
                break;
            case PhotoSetting.FieldOfView:
                if (mainCamera != null) mainCamera.fieldOfView = value;
                break;
            case PhotoSetting.ToggleLensFlare:
                if (lensFlare != null) lensFlare.enabled = value > 0.5f;
                break;
            case PhotoSetting.ToggleHUD:
                if (hudCanvasGroup != null)
                {
                    bool hudOn = value > 0.5f;
                    hudCanvasGroup.alpha = hudOn ? 1f : 0f;
                    hudCanvasGroup.interactable = hudOn;
                    hudCanvasGroup.blocksRaycasts = hudOn;
                }
                break;
            case PhotoSetting.ToggleVignette:
                if (vignette != null) vignette.active = value > 0.5f;
                break;
            case PhotoSetting.ToggleDepthOfField:
                if (depthOfField != null) depthOfField.active = value > 0.5f;
                break;
        }
    }

    /// <summary>The setting's current live value — what the panel's sliders sync to on enter.</summary>
    public float Get(PhotoSetting setting)
    {
        switch (setting)
        {
            case PhotoSetting.SunIntensity: return sunLight != null ? sunLight.intensity : 0f;
            case PhotoSetting.SunPitch: return sunLight != null ? NormalizeAngle(sunLight.transform.eulerAngles.x) : 0f;
            case PhotoSetting.SunYaw: return sunLight != null ? sunLight.transform.eulerAngles.y : 0f;
            case PhotoSetting.Bloom: return bloom != null ? bloom.intensity.value : 0f;
            case PhotoSetting.Vignette: return vignette != null ? vignette.intensity.value : 0f;
            case PhotoSetting.Exposure: return colorAdjustments != null ? colorAdjustments.postExposure.value : 0f;
            case PhotoSetting.Contrast: return colorAdjustments != null ? colorAdjustments.contrast.value : 0f;
            case PhotoSetting.Saturation: return colorAdjustments != null ? colorAdjustments.saturation.value : 0f;
            case PhotoSetting.FocusDistance: return depthOfField != null ? depthOfField.focusDistance.value : 0f;
            case PhotoSetting.Aperture: return depthOfField != null ? depthOfField.aperture.value : 0f;
            case PhotoSetting.FieldOfView: return mainCamera != null ? mainCamera.fieldOfView : 60f;
            case PhotoSetting.ToggleLensFlare: return lensFlare != null && lensFlare.enabled ? 1f : 0f;
            case PhotoSetting.ToggleHUD: return hudCanvasGroup != null && hudCanvasGroup.alpha > 0.01f ? 1f : 0f;
            case PhotoSetting.ToggleVignette: return vignette != null && vignette.active ? 1f : 0f;
            case PhotoSetting.ToggleDepthOfField: return depthOfField != null && depthOfField.active ? 1f : 0f;
            default: return 0f;
        }
    }

    /// <summary>The value the setting had when Photo Mode was entered — what a reset button restores.</summary>
    public float GetEnterValue(PhotoSetting setting)
    {
        switch (setting)
        {
            case PhotoSetting.SunIntensity: return originalSunIntensity;
            case PhotoSetting.SunPitch: return NormalizeAngle(originalSunRotation.eulerAngles.x);
            case PhotoSetting.SunYaw: return originalSunRotation.eulerAngles.y;
            case PhotoSetting.Bloom: return cachedBloomIntensity;
            case PhotoSetting.Vignette: return cachedVignetteIntensity;
            case PhotoSetting.Exposure: return cachedExposure;
            case PhotoSetting.Contrast: return cachedContrast;
            case PhotoSetting.Saturation: return cachedSaturation;
            case PhotoSetting.FocusDistance: return cachedFocusDistance;
            case PhotoSetting.Aperture: return cachedAperture;
            case PhotoSetting.FieldOfView: return originalFov;
            case PhotoSetting.ToggleLensFlare: return cachedLensFlareActive ? 1f : 0f;
            case PhotoSetting.ToggleHUD: return originalHudActive ? 1f : 0f;
            case PhotoSetting.ToggleVignette: return cachedVignetteActive ? 1f : 0f;
            case PhotoSetting.ToggleDepthOfField: return cachedDepthOfFieldActive ? 1f : 0f;
            default: return 0f;
        }
    }

    public void Capture()
    {
        if (!IsActive)
        {
            return;
        }

        StartCoroutine(CaptureRoutine());
    }

    private void HandleCapturePerformed(InputAction.CallbackContext context)
    {
        Capture();
    }

    private void SetSunAngles(float? pitch, float? yaw)
    {
        if (sunLight == null)
        {
            return;
        }

        Vector3 euler = sunLight.transform.eulerAngles;
        sunLight.transform.rotation = Quaternion.Euler(pitch ?? NormalizeAngle(euler.x), yaw ?? euler.y, 0f);
    }

    /// <summary>Maps Unity's 0-360 euler read-back into the signed range the pitch slider uses.</summary>
    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f) degrees -= 360f;
        if (degrees < -180f) degrees += 360f;
        return degrees;
    }

    /// <summary>
    ///     Sets a Volume parameter and forces its override on — an authored profile may have the
    ///     parameter's override unchecked, which would silently swallow the slider's value.
    ///     Override states are cached on enter and put back on exit like the values themselves.
    /// </summary>
    private static void ApplyOverride(FloatParameter parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    private IEnumerator CaptureRoutine()
    {
        foreach (CanvasGroup group in hideOnCapture)
        {
            if (group != null) group.alpha = 0f;
        }

        yield return new WaitForEndOfFrame();

        string folder = Path.Combine(Application.persistentDataPath, screenshotFolderName);
        Directory.CreateDirectory(folder);
        string fileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string fullPath = Path.Combine(folder, fileName);
        ScreenCapture.CaptureScreenshot(fullPath);

        // CaptureScreenshot writes over the following frames; wait a couple more before restoring the
        // UI so it doesn't sneak into the capture.
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        foreach (CanvasGroup group in hideOnCapture)
        {
            if (group != null) group.alpha = 1f;
        }

        OnScreenshotSaved?.Invoke(fullPath);
    }

    private void CacheAndEnsureVolumeOverrides()
    {
        if (globalVolume == null || globalVolume.profile == null)
        {
            return;
        }

        VolumeProfile profile = globalVolume.profile;

        if (profile.TryGet(out bloom))
        {
            cachedBloomIntensity = bloom.intensity.value;
            cachedBloomOverride = bloom.intensity.overrideState;
        }
        if (profile.TryGet(out vignette))
        {
            cachedVignetteIntensity = vignette.intensity.value;
            cachedVignetteOverride = vignette.intensity.overrideState;
        }

        colorAdjustmentsExisted = profile.TryGet(out colorAdjustments);
        if (!colorAdjustmentsExisted)
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
        }
        cachedExposure = colorAdjustments.postExposure.value;
        cachedExposureOverride = colorAdjustments.postExposure.overrideState;
        cachedContrast = colorAdjustments.contrast.value;
        cachedContrastOverride = colorAdjustments.contrast.overrideState;
        cachedSaturation = colorAdjustments.saturation.value;
        cachedSaturationOverride = colorAdjustments.saturation.overrideState;

        depthOfFieldExisted = profile.TryGet(out depthOfField);
        if (!depthOfFieldExisted)
        {
            depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            depthOfField.mode.overrideState = true;
        }
        cachedFocusDistance = depthOfField.focusDistance.value;
        cachedFocusDistanceOverride = depthOfField.focusDistance.overrideState;
        cachedAperture = depthOfField.aperture.value;
        cachedApertureOverride = depthOfField.aperture.overrideState;
        cachedDepthOfFieldActive = depthOfField.active;
        
        if (vignette != null) cachedVignetteActive = vignette.active;
    }

    private void RestoreVolumeOverrides()
    {
        if (globalVolume == null || globalVolume.profile == null)
        {
            return;
        }

        if (bloom != null)
        {
            bloom.intensity.value = cachedBloomIntensity;
            bloom.intensity.overrideState = cachedBloomOverride;
        }
        if (vignette != null)
        {
            vignette.intensity.value = cachedVignetteIntensity;
            vignette.intensity.overrideState = cachedVignetteOverride;
        }

        if (colorAdjustments != null)
        {
            if (colorAdjustmentsExisted)
            {
                colorAdjustments.postExposure.value = cachedExposure;
                colorAdjustments.postExposure.overrideState = cachedExposureOverride;
                colorAdjustments.contrast.value = cachedContrast;
                colorAdjustments.contrast.overrideState = cachedContrastOverride;
                colorAdjustments.saturation.value = cachedSaturation;
                colorAdjustments.saturation.overrideState = cachedSaturationOverride;
            }
            else if (globalVolume.profile.Has<ColorAdjustments>())
            {
                globalVolume.profile.Remove<ColorAdjustments>();
            }
        }

        if (depthOfField != null)
        {
            if (depthOfFieldExisted)
            {
                depthOfField.focusDistance.value = cachedFocusDistance;
                depthOfField.focusDistance.overrideState = cachedFocusDistanceOverride;
                depthOfField.aperture.value = cachedAperture;
                depthOfField.aperture.overrideState = cachedApertureOverride;
                depthOfField.active = cachedDepthOfFieldActive;
            }
            else if (globalVolume.profile.Has<DepthOfField>())
            {
                globalVolume.profile.Remove<DepthOfField>();
            }
        }
        
        if (vignette != null) vignette.active = cachedVignetteActive;
    }
}
