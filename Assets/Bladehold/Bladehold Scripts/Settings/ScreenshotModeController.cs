using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
///     Photo Mode: only reachable from the pause menu (see <see cref="PauseMenuController" />), so it
///     reuses the already-frozen/cursor-unlocked state rather than managing its own. Detaches the Main
///     Camera to a free-fly rig (<see cref="ScreenshotFlyCamera" />) and exposes live setters for the
///     sun light and the scene's post-processing <see cref="Volume" /> for <see cref="ScreenshotModePanel" />
///     to bind sliders to. Everything touched here — camera pose/FOV, light, and any Volume overrides
///     added on the fly (<c>ColorAdjustments</c>/<c>DepthOfField</c> aren't on the authored profile yet)
///     — is cached on <see cref="Enter" /> and restored on <see cref="Exit" />, so nothing leaks into
///     normal gameplay.
/// </summary>
public class ScreenshotModeController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ScreenshotFlyCamera flyCamera;
    [Tooltip("Optional: the scene's directional sun light, for the intensity/pitch sliders.")]
    [SerializeField] private Light sunLight;
    [Tooltip("Optional: the scene's Global Volume, for the post-processing sliders.")]
    [SerializeField] private Volume globalVolume;
    [Tooltip("Canvas groups hidden for the single frame a screenshot is captured, so the UI doesn't appear in it.")]
    [SerializeField] private CanvasGroup[] hideOnCapture;
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
    private float cachedBloomIntensity, cachedVignetteIntensity;
    private bool cachedBloomOverride, cachedVignetteOverride;
    private bool colorAdjustmentsExisted, depthOfFieldExisted;
    private float cachedExposure, cachedContrast, cachedSaturation;
    private float cachedFocusDistance, cachedAperture;

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

        RestoreVolumeOverrides();

        OnActiveChanged?.Invoke(false);
    }

    public void SetSunIntensity(float value)
    {
        if (sunLight != null) sunLight.intensity = value;
    }

    public void SetSunPitch(float degrees)
    {
        if (sunLight == null) return;
        Vector3 euler = sunLight.transform.eulerAngles;
        euler.x = degrees;
        sunLight.transform.eulerAngles = euler;
    }

    public void SetBloomIntensity(float value)
    {
        if (bloom != null) bloom.intensity.value = value;
    }

    public void SetVignetteIntensity(float value)
    {
        if (vignette != null) vignette.intensity.value = value;
    }

    public void SetExposure(float value)
    {
        if (colorAdjustments != null) colorAdjustments.postExposure.value = value;
    }

    public void SetContrast(float value)
    {
        if (colorAdjustments != null) colorAdjustments.contrast.value = value;
    }

    public void SetSaturation(float value)
    {
        if (colorAdjustments != null) colorAdjustments.saturation.value = value;
    }

    public void SetFocusDistance(float value)
    {
        if (depthOfField != null) depthOfField.focusDistance.value = value;
    }

    public void SetAperture(float value)
    {
        if (depthOfField != null) depthOfField.aperture.value = value;
    }

    public void SetFieldOfView(float value)
    {
        if (mainCamera != null) mainCamera.fieldOfView = value;
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
        else
        {
            cachedExposure = colorAdjustments.postExposure.value;
            cachedContrast = colorAdjustments.contrast.value;
            cachedSaturation = colorAdjustments.saturation.value;
        }

        depthOfFieldExisted = profile.TryGet(out depthOfField);
        if (!depthOfFieldExisted)
        {
            depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            depthOfField.mode.overrideState = true;
        }
        else
        {
            cachedFocusDistance = depthOfField.focusDistance.value;
            cachedAperture = depthOfField.aperture.value;
        }
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
                colorAdjustments.contrast.value = cachedContrast;
                colorAdjustments.saturation.value = cachedSaturation;
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
                depthOfField.aperture.value = cachedAperture;
            }
            else if (globalVolume.profile.Has<DepthOfField>())
            {
                globalVolume.profile.Remove<DepthOfField>();
            }
        }
    }
}
