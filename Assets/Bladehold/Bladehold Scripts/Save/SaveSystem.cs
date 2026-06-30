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

    public static void Save(SaveData data)
    {
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
        if (!File.Exists(FilePath))
        {
            return new SaveData();
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read save file at '{FilePath}': {e.Message}");
            return new SaveData();
        }
    }
}
