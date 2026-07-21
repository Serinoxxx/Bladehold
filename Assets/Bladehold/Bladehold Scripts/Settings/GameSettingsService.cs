using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
///     Scene singleton owning applying and persisting player-facing settings: audio volumes (via the
///     Feel <see cref="AudioMixer" />'s exposed <c>MasterVolume</c>/<c>MusicVolume</c>/<c>SfxVolume</c>
///     parameters), mouse sensitivity/invert (via the player's <see cref="InputSettingsBinder" />),
///     field of view (via the player's <see cref="BowAimCamera" />), the max-simultaneous-ragdolls
///     performance cap (via <see cref="EnemyRagdoll.MaxActive" />), and button-remap overrides.
///     Settings live on the shared <see cref="SaveData" /> like gold/upgrades —
///     applied once on <see cref="Start" /> and again immediately whenever a setter is called, so the
///     settings UI never touches <see cref="SaveData" /> or the mixer directly.
///
///     Master volume also drives <see cref="AudioListener.volume" /> directly, in addition to the
///     mixer parameter: nothing in the project currently routes its audio sources through this mixer's
///     groups (see TODO.md), so Master needs a path that works regardless of that wiring. Music/Sfx
///     only take audible effect once sources are routed to the matching mixer group.
/// </summary>
public class GameSettingsService : MonoBehaviour
{
    public static GameSettingsService Instance;

    [Tooltip("Mixer exposing MasterVolume/MusicVolume/SfxVolume float parameters (in dB), e.g. MMSoundManagerAudioMixer.")]
    [SerializeField] private AudioMixer mixer;
    
    [Tooltip("Optional: the scene's Global Volume, for applying post-processing settings.")]
    [SerializeField] private UnityEngine.Rendering.Volume globalVolume;

    private SaveData saveData;

    public float MasterVolume => saveData.masterVolume;
    public float MusicVolume => saveData.musicVolume;
    public float SfxVolume => saveData.sfxVolume;
    public float Sensitivity => saveData.mouseSensitivity;
    public bool InvertX => saveData.invertLookX;
    public bool InvertY => saveData.invertLookY;
    public int MaxRagdolls => saveData.maxRagdolls;
    public float FieldOfView => saveData.fieldOfView;
    public float GameSpeed => saveData.gameSpeed;
    public string LanguageCode => saveData.languageCode;
    public float GamepadSensitivity => saveData.gamepadLookSensitivity;
    public bool PostProcessingEnabled => saveData.postProcessingEnabled;
    public float PostProcessingBloom => saveData.postProcessingBloom;
    public float PostProcessingVignette => saveData.postProcessingVignette;
    public float PostProcessingExposure => saveData.postProcessingExposure;

    public static float TargetTimeScale => Instance != null ? Instance.GameSpeed : 1f;

    /// <summary>Raised whenever any setting changes, so UI showing current values can refresh.</summary>
    public event Action OnSettingsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        saveData = SaveSystem.Load();
    }

    private void Start()
    {
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Re-applies every persisted setting to the live systems. Called once on <see cref="Start" />.</summary>
    public void ApplyAll()
    {
        if (globalVolume == null)
        {
            globalVolume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        }

        ApplyMasterVolume(saveData.masterVolume);
        SetMixerVolume("MusicVolume", saveData.musicVolume);
        SetMixerVolume("SfxVolume", saveData.sfxVolume);
        ApplyMaxRagdolls(saveData.maxRagdolls);
        ApplyGameSpeed(saveData.gameSpeed);
        ApplyPostProcessing();

        Loc.SetLanguage(saveData.languageCode);

        InputSettingsBinder inputSettings = Player.Instance != null ? Player.Instance.InputSettings : null;
        if (inputSettings != null)
        {
            inputSettings.ApplySensitivity(saveData.mouseSensitivity);
            inputSettings.ApplyGamepadSensitivity(saveData.gamepadLookSensitivity);
            inputSettings.ApplyInvertX(saveData.invertLookX);
            inputSettings.ApplyInvertY(saveData.invertLookY);
            inputSettings.LoadBindingOverridesFromJson(saveData.inputBindingOverridesJson);
        }

        if (Player.Instance != null && Player.Instance.AimCamera != null)
        {
            Player.Instance.AimCamera.SetRestingFieldOfView(saveData.fieldOfView);
        }
    }

    public void SetMasterVolume(float value)
    {
        saveData.masterVolume = Mathf.Clamp01(value);
        ApplyMasterVolume(saveData.masterVolume);
        Persist();
    }

    public void SetMusicVolume(float value)
    {
        saveData.musicVolume = Mathf.Clamp01(value);
        SetMixerVolume("MusicVolume", saveData.musicVolume);
        Persist();
    }

    public void SetSfxVolume(float value)
    {
        saveData.sfxVolume = Mathf.Clamp01(value);
        SetMixerVolume("SfxVolume", saveData.sfxVolume);
        Persist();
    }

    public void SetMaxRagdolls(int value)
    {
        saveData.maxRagdolls = Mathf.Clamp(value, 0, 50);
        ApplyMaxRagdolls(saveData.maxRagdolls);
        Persist();
    }

    public void SetSensitivity(float value)
    {
        saveData.mouseSensitivity = Mathf.Clamp(value, 0f, 10f);
        if (Player.Instance != null && Player.Instance.InputSettings != null)
        {
            Player.Instance.InputSettings.ApplySensitivity(saveData.mouseSensitivity);
        }
        Persist();
    }

    public void SetFieldOfView(float value)
    {
        saveData.fieldOfView = Mathf.Clamp(value, 30f, 100f);
        if (Player.Instance != null && Player.Instance.AimCamera != null)
        {
            Player.Instance.AimCamera.SetRestingFieldOfView(saveData.fieldOfView);
        }
        Persist();
    }

    public void SetGameSpeed(float value)
    {
        saveData.gameSpeed = Mathf.Clamp(value, 0.1f, 2f);
        ApplyGameSpeed(saveData.gameSpeed);
        Persist();
    }

    public void SetPostProcessingEnabled(bool value)
    {
        saveData.postProcessingEnabled = value;
        ApplyPostProcessing();
        Persist();
    }

    public void SetPostProcessingBloom(float value)
    {
        saveData.postProcessingBloom = Mathf.Clamp(value, 0f, 5f);
        ApplyPostProcessing();
        Persist();
    }

    public void SetPostProcessingVignette(float value)
    {
        saveData.postProcessingVignette = Mathf.Clamp01(value);
        ApplyPostProcessing();
        Persist();
    }

    public void SetPostProcessingExposure(float value)
    {
        saveData.postProcessingExposure = Mathf.Clamp(value, -5f, 5f);
        ApplyPostProcessing();
        Persist();
    }

    /// <summary>Switches the UI language ("" = auto-detect from the OS) and persists the choice.</summary>
    public void SetLanguage(string code)
    {
        saveData.languageCode = code ?? "";
        Loc.SetLanguage(saveData.languageCode);
        Persist();
    }

    public void SetGamepadSensitivity(float value)
    {
        saveData.gamepadLookSensitivity = Mathf.Clamp(value, 30f, 360f);
        if (Player.Instance != null && Player.Instance.InputSettings != null)
        {
            Player.Instance.InputSettings.ApplyGamepadSensitivity(saveData.gamepadLookSensitivity);
        }
        Persist();
    }

    public void SetInvertX(bool value)
    {
        saveData.invertLookX = value;
        if (Player.Instance != null && Player.Instance.InputSettings != null)
        {
            Player.Instance.InputSettings.ApplyInvertX(value);
        }
        Persist();
    }

    public void SetInvertY(bool value)
    {
        saveData.invertLookY = value;
        if (Player.Instance != null && Player.Instance.InputSettings != null)
        {
            Player.Instance.InputSettings.ApplyInvertY(value);
        }
        Persist();
    }

    /// <summary>Called by <see cref="RebindButtonView" /> after a binding is successfully remapped.</summary>
    public void PersistInputOverrides()
    {
        if (Player.Instance == null || Player.Instance.InputSettings == null)
        {
            return;
        }

        saveData.inputBindingOverridesJson = Player.Instance.InputSettings.SaveBindingOverridesToJson();
        SaveSystem.Save(saveData);
        InputDeviceWatcher.NotifyBindingsChanged();
    }

    /// <summary>
    ///     Restores every setting — audio, controls, video, performance, and button remaps — to its
    ///     authored default, re-applies them to the live systems, and persists. Progress is untouched
    ///     (that's Delete Save's job).
    /// </summary>
    public void ResetToDefaults()
    {
        saveData.ResetSettings();

        // ApplyAll only loads override json (a no-op when empty) — actively remove the live overrides too.
        if (Player.Instance != null && Player.Instance.InputSettings != null)
        {
            Player.Instance.InputSettings.ClearBindingOverrides();
        }

        ApplyAll();
        Persist();
        InputDeviceWatcher.NotifyBindingsChanged();
    }

    private void ApplyMaxRagdolls(int value)
    {
        EnemyRagdoll.MaxActive = value;
    }

    private void ApplyGameSpeed(float value)
    {
        if (Time.timeScale > 0f)
        {
            Time.timeScale = value;
        }
    }

    private void ApplyMasterVolume(float value)
    {
        AudioListener.volume = value;
        SetMixerVolume("MasterVolume", value);
    }

    private void SetMixerVolume(string exposedParam, float linear01)
    {
        if (mixer == null)
        {
            return;
        }

        float dB = linear01 > 0.0001f ? Mathf.Log10(linear01) * 20f : -80f;
        mixer.SetFloat(exposedParam, dB);
    }

    private void Persist()
    {
        SaveSystem.Save(saveData);
        OnSettingsChanged?.Invoke();
    }

    private void ApplyPostProcessing()
    {
        if (globalVolume == null || globalVolume.profile == null)
        {
            return;
        }

        // Toggle entire volume weight or active state
        globalVolume.weight = saveData.postProcessingEnabled ? 1f : 0f;

        if (saveData.postProcessingEnabled)
        {
            if (globalVolume.profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            {
                bloom.intensity.overrideState = true;
                bloom.intensity.value = saveData.postProcessingBloom;
            }
            if (globalVolume.profile.TryGet(out UnityEngine.Rendering.Universal.Vignette vignette))
            {
                vignette.intensity.overrideState = true;
                vignette.intensity.value = saveData.postProcessingVignette;
            }
            if (globalVolume.profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments colorAdjustments))
            {
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = saveData.postProcessingExposure;
            }
            else
            {
                colorAdjustments = globalVolume.profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.postExposure.value = saveData.postProcessingExposure;
            }
        }
    }
}
