using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
///     Scene singleton owning applying and persisting player-facing settings: audio volumes (via the
///     Feel <see cref="AudioMixer" />'s exposed <c>MasterVolume</c>/<c>MusicVolume</c>/<c>SfxVolume</c>
///     parameters), mouse sensitivity/invert (via the player's <see cref="InputSettingsBinder" />), and
///     button-remap overrides. Settings live on the shared <see cref="SaveData" /> like gold/upgrades —
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

    private SaveData saveData;

    public float MasterVolume => saveData.masterVolume;
    public float MusicVolume => saveData.musicVolume;
    public float SfxVolume => saveData.sfxVolume;
    public float Sensitivity => saveData.mouseSensitivity;
    public bool InvertX => saveData.invertLookX;
    public bool InvertY => saveData.invertLookY;

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
        ApplyMasterVolume(saveData.masterVolume);
        SetMixerVolume("MusicVolume", saveData.musicVolume);
        SetMixerVolume("SfxVolume", saveData.sfxVolume);

        InputSettingsBinder inputSettings = Player.Instance != null ? Player.Instance.InputSettings : null;
        if (inputSettings != null)
        {
            inputSettings.ApplySensitivity(saveData.mouseSensitivity);
            inputSettings.ApplyInvertX(saveData.invertLookX);
            inputSettings.ApplyInvertY(saveData.invertLookY);
            inputSettings.LoadBindingOverridesFromJson(saveData.inputBindingOverridesJson);
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

    public void SetSensitivity(float value)
    {
        saveData.mouseSensitivity = value;
        if (Player.Instance != null && Player.Instance.InputSettings != null)
        {
            Player.Instance.InputSettings.ApplySensitivity(value);
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
    }

    public void ResetInputOverrides()
    {
        saveData.inputBindingOverridesJson = "";
        SaveSystem.Save(saveData);

        if (Player.Instance != null && Player.Instance.InputSettings != null)
        {
            Player.Instance.InputSettings.ClearBindingOverrides();
        }

        OnSettingsChanged?.Invoke();
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
}
