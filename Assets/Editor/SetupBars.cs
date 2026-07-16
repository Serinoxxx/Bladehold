using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using MoreMountains.Tools;

public class SetupBarsEditor : EditorWindow
{
    [MenuItem("Tools/Setup MMProgressBars")]
    public static void SetupBars()
    {
        var playerBar = GameObject.Find("PlayerHealthGroup");
        var horseHealthBar = GameObject.Find("HorseHealthGroup");
        var horseStaminaBar = GameObject.Find("HorseStaminaGroup");

        if (playerBar != null) SetupBar(playerBar.transform, "PlayerHealthBar");
        if (horseHealthBar != null) SetupBar(horseHealthBar.transform, "HorseHealthBar");
        if (horseStaminaBar != null) SetupBar(horseStaminaBar.transform, "HorseStaminaBar");
        
        Debug.Log("Bars setup completed.");
    }

    private static void SetupBar(Transform parent, string barName)
    {
        // Find or create MyBar under parent
        Transform myBar = parent.Find(barName);
        if (myBar == null)
        {
            GameObject go = new GameObject(barName);
            myBar = go.transform;
            myBar.SetParent(parent, false);
            myBar.localPosition = Vector3.zero;
        }

        // Clean up old children
        for (int i = myBar.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(myBar.GetChild(i).gameObject);
        }

        // BackgroundBar
        GameObject bgBar = new GameObject("BackgroundBar");
        bgBar.transform.SetParent(myBar, false);
        Image bgImg = bgBar.AddComponent<Image>();
        bgImg.color = Color.black;
        bgImg.rectTransform.sizeDelta = new Vector2(300, bgImg.rectTransform.sizeDelta.y);
        bgImg.rectTransform.pivot = new Vector2(0, 0.5f);
        bgImg.rectTransform.localPosition = Vector3.zero;

        // DelayedBarDecreasing
        GameObject dDecBar = new GameObject("DelayedBarDecreasing");
        dDecBar.transform.SetParent(myBar, false);
        Image dDecImg = dDecBar.AddComponent<Image>();
        dDecImg.color = new Color(1f, 0.5f, 0f); // orange
        dDecImg.rectTransform.sizeDelta = new Vector2(300, dDecImg.rectTransform.sizeDelta.y);
        dDecImg.rectTransform.pivot = new Vector2(0, 0.5f);
        dDecImg.rectTransform.localPosition = Vector3.zero;

        // DelayedBarIncreasing
        GameObject dIncBar = new GameObject("DelayedBarIncreasing");
        dIncBar.transform.SetParent(myBar, false);
        Image dIncImg = dIncBar.AddComponent<Image>();
        dIncImg.color = Color.yellow;
        dIncImg.rectTransform.sizeDelta = new Vector2(300, dIncImg.rectTransform.sizeDelta.y);
        dIncImg.rectTransform.pivot = new Vector2(0, 0.5f);
        dIncImg.rectTransform.localPosition = Vector3.zero;

        // ForegroundBar
        GameObject fgBar = new GameObject("ForegroundBar");
        fgBar.transform.SetParent(myBar, false);
        Image fgImg = fgBar.AddComponent<Image>();
        fgImg.color = Color.green;
        fgImg.rectTransform.sizeDelta = new Vector2(300, fgImg.rectTransform.sizeDelta.y);
        fgImg.rectTransform.pivot = new Vector2(0, 0.5f);
        fgImg.rectTransform.localPosition = Vector3.zero;

        // Add MMProgressBar
        MMProgressBar mmBar = myBar.gameObject.GetComponent<MMProgressBar>();
        if (mmBar == null) mmBar = myBar.gameObject.AddComponent<MMProgressBar>();

        // Setup Bindings
        mmBar.ForegroundBar = fgBar.transform;
        mmBar.DelayedBarDecreasing = dDecBar.transform;
        mmBar.DelayedBarIncreasing = dIncBar.transform;

        // Setup Fill Settings
        mmBar.SetInitialFillValueOnStart = true;
        mmBar.InitialFillValue = 1f;
        mmBar.BarFillMode = MMProgressBar.BarFillModes.FixedDuration;

        // Set dirty to save
        EditorUtility.SetDirty(myBar.gameObject);
        
        // Link to UI script
        if (parent.name == "PlayerHealthGroup")
        {
            var ui = parent.GetComponent<PlayerHealthBarUI>();
            if (ui != null)
            {
                SerializedObject so = new SerializedObject(ui);
                so.FindProperty("progressBar").objectReferenceValue = mmBar;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ui);
            }
        }
        else if (parent.name == "HorseHealthGroup")
        {
            // Assuming HorseHealthBarUI has progressBar
            var ui = parent.GetComponent("HorseHealthBarUI");
            if (ui != null)
            {
                SerializedObject so = new SerializedObject(ui);
                var prop = so.FindProperty("progressBar");
                if (prop != null) prop.objectReferenceValue = mmBar;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ui);
            }
        }
        else if (parent.name == "HorseStaminaGroup")
        {
            var ui = parent.GetComponent<HorseStaminaUI>();
            if (ui != null)
            {
                SerializedObject so = new SerializedObject(ui);
                so.FindProperty("progressBar").objectReferenceValue = mmBar;
                so.FindProperty("fillImage").objectReferenceValue = fgImg;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ui);
            }
        }
    }
}
