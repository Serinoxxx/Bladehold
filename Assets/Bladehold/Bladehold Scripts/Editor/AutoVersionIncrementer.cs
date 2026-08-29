using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class AutoVersionIncrementer : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string currentVersion = PlayerSettings.bundleVersion;
        if (string.IsNullOrEmpty(currentVersion))
        {
            currentVersion = "0.1.0";
        }

        string[] parts = currentVersion.Split('.');
        if (parts.Length >= 3 && int.TryParse(parts[2], out int patch))
        {
            parts[2] = (patch + 1).ToString();
            PlayerSettings.bundleVersion = string.Join(".", parts);
        }
        else
        {
            PlayerSettings.bundleVersion = currentVersion + ".1";
        }

        Debug.Log($"[AutoVersionIncrementer] Incremented build version to: {PlayerSettings.bundleVersion}");

        try
        {
            string rootChangelog = System.IO.Path.Combine(Application.dataPath, "../CHANGELOG.md");
            string streamingDir = Application.streamingAssetsPath;
            if (!System.IO.Directory.Exists(streamingDir))
            {
                System.IO.Directory.CreateDirectory(streamingDir);
            }
            if (System.IO.File.Exists(rootChangelog))
            {
                System.IO.File.Copy(rootChangelog, System.IO.Path.Combine(streamingDir, "CHANGELOG.md"), true);
                AssetDatabase.Refresh();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AutoVersionIncrementer] Could not copy CHANGELOG.md to StreamingAssets: {ex.Message}");
        }
    }
}
