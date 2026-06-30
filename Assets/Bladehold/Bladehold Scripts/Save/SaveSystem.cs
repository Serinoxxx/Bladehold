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
    private const string _fileName = "save.json";

    private static string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

    // A single in-memory SaveData shared by every owner (Wallet, SkillTreeService, …). Without this each
    // caller would hold its own copy and the last one to Save() would clobber the others' fields. Statics
    // persist across scene reloads within a play session and reset when play stops, mirroring how the
    // persisted progress should behave.
    private static SaveData _cache;

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
