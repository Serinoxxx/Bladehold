using System;
using System.IO;
using UnityEngine;

/// <summary>
///     Reads and writes <see cref="SaveData" /> as JSON under <c>Application.persistentDataPath</c>.
///     Progress lives in one of <see cref="SlotCount" /> save slots (<c>save_slot1.json</c> …); player
///     settings (the <see cref="SaveData.ResetSettings" /> half) are global and live in
///     <c>settings.json</c>, overlaid onto whichever slot is loaded, so every slot shares one set of options.
///     Loading is resilient: a missing or unreadable file yields fresh, default <see cref="SaveData" />
///     rather than throwing.
/// </summary>
public static class SaveSystem
{
    public const int SlotCount = 5;

    /// <summary>Pre-slot single save file; migrated into slot 1 the first time the slots are touched.</summary>
    private const string LegacyFileName = "save.json";
    private const string SettingsFileName = "settings.json";
    private const string ActiveSlotPrefsKey = "Bladehold.ActiveSaveSlot";

    /// <summary>
    ///     The slot <see cref="Load" />/<see cref="Save" /> target, or -1 for none (the main menu before a
    ///     slot is picked — Load then returns default progress plus the global settings, and Save only
    ///     writes settings). Starts at the last slot played so entering Play mode straight into a gameplay
    ///     scene keeps using it.
    /// </summary>
    public static int ActiveSlot
    {
        get
        {
            if (!_activeSlotResolved)
            {
                _activeSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotPrefsKey, 0), 0, SlotCount - 1);
                _activeSlotResolved = true;
            }
            return _activeSlot;
        }
    }

    public static bool HasActiveSlot => ActiveSlot >= 0;

    private static int _activeSlot;
    private static bool _activeSlotResolved;

    // A single in-memory SaveData shared by every owner (the Meta Area UIs, GameSettingsService, …). Without this each
    // caller would hold its own copy and the last one to Save() would clobber the others' fields. Statics
    // persist across scene reloads within a play session and reset when play stops, mirroring how the
    // persisted progress should behave.
    private static SaveData _cache;

    // Realtime at which play time was last banked into the active slot (see BankPlayTime).
    private static float _playTimeMark;
    private static bool _quitHooked;

    private static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_slot{slot + 1}.json");
    private static string SettingsPath => Path.Combine(Application.persistentDataPath, SettingsFileName);
    private static string LegacyPath => Path.Combine(Application.persistentDataPath, LegacyFileName);

    // Statics survive between Editor play sessions when Domain Reload is disabled; reset them at play start.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activeSlotResolved = false;
        _cache = null;
        _playTimeMark = 0f;
        if (!_quitHooked)
        {
            Application.quitting += OnQuitting;
            _quitHooked = true;
        }
    }

    private static void OnQuitting()
    {
        if (_cache != null && HasActiveSlot && File.Exists(SlotPath(ActiveSlot)))
        {
            Save(_cache);
        }
    }

    /// <summary>True if the slot has a save on disk.</summary>
    public static bool SlotExists(int slot)
    {
        MigrateLegacySave();
        return slot >= 0 && slot < SlotCount && File.Exists(SlotPath(slot));
    }

    /// <summary>
    ///     Reads a slot for display (the save slot screen) without making it active. Returns the live
    ///     cache for the active slot, null for an empty slot.
    /// </summary>
    public static SaveData PeekSlot(int slot)
    {
        if (!SlotExists(slot))
        {
            return null;
        }
        if (slot == ActiveSlot && _cache != null)
        {
            return _cache;
        }
        return ReadFile(SlotPath(slot));
    }

    /// <summary>Main menu: no slot is active until the player picks one on the save slot screen.</summary>
    public static void DeselectSlot()
    {
        SetActiveSlot(-1);
    }

    /// <summary>Makes an existing slot active (Continue) and stamps it as played now.</summary>
    public static void SelectSlot(int slot)
    {
        SetActiveSlot(slot);
        PlayerPrefs.SetInt(ActiveSlotPrefsKey, slot);
        PlayerPrefs.Save();
        Save(Load());
    }

    /// <summary>Starts a fresh save in the slot (New Game), overwriting anything there, and makes it active.</summary>
    public static void CreateSlot(int slot)
    {
        SetActiveSlot(slot);
        PlayerPrefs.SetInt(ActiveSlotPrefsKey, slot);
        PlayerPrefs.Save();

        SaveData fresh = new SaveData();
        fresh.CopySettingsFrom(ReadSettings());
        fresh.createdUtcTicks = DateTime.UtcNow.Ticks;
        WriteSlot(slot, fresh);
        _cache = fresh;
    }

    /// <summary>Deletes a slot's file. Deleting the active slot leaves no slot active.</summary>
    public static void DeleteSlot(int slot)
    {
        if (slot == ActiveSlot)
        {
            SetActiveSlot(-1);
        }
        try
        {
            string path = SlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to delete save slot {slot + 1}: {e.Message}");
        }
    }

    public static void Save(SaveData data)
    {
        // A holder of a stale SaveData (e.g. a GameSettingsService that loaded before the slot changed)
        // may only contribute settings — its progress half belongs to a slot that's no longer active.
        if (_cache != null && data != _cache)
        {
            _cache.CopySettingsFrom(data);
            data = _cache;
        }
        _cache = data;

        WriteJson(SettingsPath, data);

        // Only a slot that exists on disk takes progress writes, so nothing saving from the main menu (settings)
        // or a scene opened straight in the Editor with no saves can conjure a phantom save into the slot list.
        if (HasActiveSlot && File.Exists(SlotPath(ActiveSlot)))
        {
            BankPlayTime(data);
            data.lastPlayedUtcTicks = DateTime.UtcNow.Ticks;
            WriteSlot(ActiveSlot, data);
        }
    }

    public static SaveData Load()
    {
        if (_cache != null)
        {
            return _cache;
        }

        MigrateLegacySave();
        SaveData settings = ReadSettings();
        _cache = HasActiveSlot && File.Exists(SlotPath(ActiveSlot)) ? ReadFile(SlotPath(ActiveSlot)) : new SaveData();
        _cache.CopySettingsFrom(settings);
        _playTimeMark = Time.realtimeSinceStartup;
        return _cache;
    }

    private static void SetActiveSlot(int slot)
    {
        _activeSlot = slot;
        _activeSlotResolved = true;
        _cache = null;
        _playTimeMark = Time.realtimeSinceStartup;
    }

    private static void BankPlayTime(SaveData data)
    {
        float now = Time.realtimeSinceStartup;
        data.playTimeSeconds += Mathf.Max(0f, now - _playTimeMark);
        _playTimeMark = now;
    }

    private static void WriteSlot(int slot, SaveData data)
    {
        WriteJson(SlotPath(slot), data);
    }

    private static void WriteJson(string path, SaveData data)
    {
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write save file at '{path}': {e.Message}");
        }
    }

    private static SaveData ReadSettings()
    {
        return File.Exists(SettingsPath) ? ReadFile(SettingsPath) : new SaveData();
    }

    private static SaveData ReadFile(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();

            // Saves from before the tutorial existed belong to players who already know the game.
            if (!json.Contains("\"tutorialCompleted\""))
            {
                data.tutorialCompleted = true;
            }
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read save file at '{path}': {e.Message}");
            return new SaveData();
        }
    }

    /// <summary>Moves a pre-slot <c>save.json</c> into slot 1 (and its settings into settings.json), once.</summary>
    private static void MigrateLegacySave()
    {
        string legacy = LegacyPath;
        if (!File.Exists(legacy))
        {
            return;
        }
        try
        {
            SaveData data = ReadFile(legacy);
            if (!File.Exists(SettingsPath))
            {
                WriteJson(SettingsPath, data);
            }
            if (!File.Exists(SlotPath(0)))
            {
                if (data.createdUtcTicks == 0)
                {
                    data.createdUtcTicks = File.GetCreationTimeUtc(legacy).Ticks;
                }
                if (data.lastPlayedUtcTicks == 0)
                {
                    data.lastPlayedUtcTicks = File.GetLastWriteTimeUtc(legacy).Ticks;
                }
                WriteSlot(0, data);
            }
            File.Delete(legacy);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to migrate legacy save '{legacy}': {e.Message}");
        }
    }
}
