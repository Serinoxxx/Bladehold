using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line build entry point. Invoke with:
/// Unity.exe -batchmode -quit -projectPath "<project>" -executeMethod BatchBuild.BuildWindows -logFile build.log
/// </summary>
public static class BatchBuild
{
    private const string GameplayScene = "Assets/Bladehold/Bladehold Scenes/Bladehold Test Scene.unity";

    public static void BuildWindows()
    {
        var options = new BuildPlayerOptions
        {
            // EditorBuildSettings still points at the deleted SampleScene, so pass the real scene explicitly.
            scenes = new[] { GameplayScene },
            locationPathName = "Builds/Windows/Bladehold.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[BatchBuild] Result: {summary.result}, size: {summary.totalSize / (1024 * 1024)} MB, " +
                  $"time: {summary.totalTime}, errors: {summary.totalErrors}, warnings: {summary.totalWarnings}");

        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
