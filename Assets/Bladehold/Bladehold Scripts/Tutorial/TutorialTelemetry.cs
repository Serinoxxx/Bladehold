using GameAnalyticsSDK;

/// <summary>
///     GameAnalytics progression events for the tutorial funnel: one Start/Complete pair per step
///     (<c>Tutorial:&lt;scene&gt;:&lt;step&gt;</c>) and a Fail event on skip, so drop-off per step shows up
///     in the GA progression dashboard.
/// </summary>
public static class TutorialTelemetry
{
    public static void StepStarted(string scene, string stepId)
    {
        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "Tutorial", Clean(scene), Clean(stepId));
    }

    public static void StepCompleted(string scene, string stepId)
    {
        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "Tutorial", Clean(scene), Clean(stepId));
    }

    public static void Skipped(string scene)
    {
        GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, "Tutorial", Clean(scene), "Skipped");
    }

    // GA progression ids allow [A-Za-z0-9 -_.()!?] only.
    private static string Clean(string id)
    {
        if (string.IsNullOrEmpty(id)) return "Unknown";
        return System.Text.RegularExpressions.Regex.Replace(id, @"[^A-Za-z0-9_\-\.]", "");
    }
}
