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
    }
}
