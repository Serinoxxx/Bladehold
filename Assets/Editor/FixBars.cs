using UnityEngine;
using UnityEditor;
using MoreMountains.Tools;

public class FixBarsEditor : EditorWindow
{
    [MenuItem("Tools/Fix MMProgressBars")]
    public static void FixBars()
    {
        var bars = FindObjectsOfType<MMProgressBar>(true);
        foreach (var bar in bars)
        {
            if (bar.LerpForegroundBarCurveDecreasing == null || bar.LerpForegroundBarCurveDecreasing.keys.Length == 0)
                bar.LerpForegroundBarCurveDecreasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                
            if (bar.LerpForegroundBarCurveIncreasing == null || bar.LerpForegroundBarCurveIncreasing.keys.Length == 0)
                bar.LerpForegroundBarCurveIncreasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                
            if (bar.LerpDecreasingDelayedBarCurve == null || bar.LerpDecreasingDelayedBarCurve.keys.Length == 0)
                bar.LerpDecreasingDelayedBarCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                
            if (bar.LerpIncreasingDelayedBarCurve == null || bar.LerpIncreasingDelayedBarCurve.keys.Length == 0)
                bar.LerpIncreasingDelayedBarCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            if (bar.BumpScaleAnimationCurve == null || bar.BumpScaleAnimationCurve.keys.Length == 0)
                bar.BumpScaleAnimationCurve = new AnimationCurve(new Keyframe(1, 1), new Keyframe(0.3f, 1.05f), new Keyframe(1, 1));
                
            if (bar.BumpColorAnimationCurve == null || bar.BumpColorAnimationCurve.keys.Length == 0)
                bar.BumpColorAnimationCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 1f), new Keyframe(1, 0));
                
            if (bar.BumpIntensityMultiplier == null || bar.BumpIntensityMultiplier.keys.Length == 0)
                bar.BumpIntensityMultiplier = new AnimationCurve(new Keyframe(-1, 1), new Keyframe(1, 1));

            EditorUtility.SetDirty(bar);
        }
        Debug.Log($"Fixed curves for {bars.Length} MMProgressBars.");
    }
}
