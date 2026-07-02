using System;
using System.IO;
using UnityEngine;

/// <summary>
///     Reads and writes <see cref="SaveData" /> as JSON under <c>Application.persistentDataPath</c>.
///     Loading is resilient: a missing or unreadable file yields fresh, default <see cref="SaveData" />
///     rather than throwing.
/// </summary>
public static class SaveSystem
{
    /// <summary>The real game's save file. Loading/saving targets this unless <see cref="UseFile" /> redirects it.</summary>
    public const string DefaultFileName = "save.json";

    private static string _fileName = DefaultFileName;

    private static string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

    /// <summary>True if the current save file exists on disk.</summary>
    public static bool SaveFileExists => File.Exists(FilePath);

    // A single in-memory SaveData shared by every owner (Wallet, SkillTreeService, …). Without this each
    // caller would hold its own copy and the last one to Save() would clobber the others' fields. Statics
    // persist across scene reloads within a play session and reset when play stops, mirroring how the
    // persisted progress should behave.
    private static SaveData _cache;

    /// <summary>
    ///     Redirects all loading/saving to a different file under <c>Application.persistentDataPath</c>,
    ///     dropping the in-memory cache so the next <see cref="Load" /> reads the new file. Must run before
    ///     anything Loads — i.e. from an <c>Awake</c> that executes ahead of <see cref="Wallet" /> and the
    ///     tree services (see <see cref="SkillTreePreview" />).
    /// </summary>
    public static void UseFile(string fileName)
    {
        if (_fileName == fileName)
        {
            return;
        }
        _fileName = fileName;
        _cache = null;
    }

    /// <summary>Deletes the current save file and forgets the cache, so the next <see cref="Load" /> starts fresh.</summary>
    public static void DeleteCurrentSave()
    {
        _cache = null;
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to delete save file at '{FilePath}': {e.Message}");
        }
    }

    // Statics survive between Editor play sessions when Domain Reload is disabled; reset them at play start
    // so a preview scene's UseFile redirect (or its cached data) can never leak into a real run.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _fileName = DefaultFileName;
        _cache = null;
    }

    public static void Save(SaveData data)
    {
        _cache = data;
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write save file at '{FilePath}': {e.Message}");
        }
    }

    public static SaveData Load()
    {
        if (_cache != null)
        {
            return _cache;
        }

        if (!File.Exists(FilePath))
        {
            _cache = new SaveData();
            return _cache;
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            _cache = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read save file at '{FilePath}': {e.Message}");
            _cache = new SaveData();
        }

        return _cache;
    }
}
