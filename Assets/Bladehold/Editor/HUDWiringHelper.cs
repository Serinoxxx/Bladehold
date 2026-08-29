using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using MoreMountains.Feedbacks;
using UnityEngine.SceneManagement;

public static class HUDWiringHelper
{
    [MenuItem("Tools/Wire Bladehold HUD")]
    public static void WireHUD()
    {
        var oldHUD = GameObject.Find("HUD Canvas") ?? GameObject.Find("HUD");
        if (oldHUD != null) {
            oldHUD.SetActive(false);
            Debug.Log("Disabled old HUD Canvas.");
        }
        
        var existingHUD = GameObject.Find("Bladehold HUD");
        if (existingHUD != null) {
            Undo.DestroyObjectImmediate(existingHUD);
            Debug.Log("Deleted existing Bladehold HUD.");
        }

        string[] guids = AssetDatabase.FindAssets("Bladehold HUD t:Prefab");
        if (guids.Length == 0) {
            Debug.LogError("Prefab Bladehold HUD not found!");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject newHUD = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(newHUD, "Instantiate HUD");
        
        // 1. WaveClearedBannerUI
        Transform bannerTransform = newHUD.transform.Find("Screen_HUD_Adventure_01/ScreenSpace/Top/Wave Cleared Text");
        if (bannerTransform != null) {
            var bannerUI = bannerTransform.gameObject.AddComponent<WaveClearedBannerUI>();
            var so = new SerializedObject(bannerUI);
            so.FindProperty("bannerRoot").objectReferenceValue = bannerTransform.gameObject;
            
            var texts = bannerTransform.GetComponentsInChildren<TMP_Text>(true);
            so.FindProperty("waveClearedText").objectReferenceValue = texts.FirstOrDefault(t => t.name == "Label_QuestComplete" || t.text.Contains("CLEARED") || t.text.Contains("WAVE"));
            so.FindProperty("questNameText").objectReferenceValue = texts.FirstOrDefault(t => t.name == "Label_QuestName");
            so.FindProperty("goldEarnedText").objectReferenceValue = texts.FirstOrDefault(t => t.name == "Label_CurrencyNum" || t.transform.parent.name.Contains("Currency"));
            so.FindProperty("enemiesKilledText").objectReferenceValue = texts.FirstOrDefault(t => t.name == "Label_EnemiesKilled" || t.transform.parent.name.Contains("XP"));
            
            var mmf = bannerTransform.GetComponent<MMF_Player>();
            if (mmf == null) {
                mmf = bannerTransform.gameObject.AddComponent<MMF_Player>();
                // We'd manually configure the MMF player later or just leave it blank for manual tuning
            }
            so.FindProperty("bannerAnimationFeedback").objectReferenceValue = mmf;
            so.ApplyModifiedProperties();
        }

        // 2. ObjectiveTrackerUI
        Transform objListTransform = newHUD.transform.Find("Screen_HUD_Adventure_01/ScreenSpace/Top Left/HUD_FantasyWarrior_Objectives_02/Content");
        if (objListTransform != null) {
            var objTracker = objListTransform.gameObject.AddComponent<ObjectiveTrackerUI>();
            var so = new SerializedObject(objTracker);
            
            var texts = objListTransform.GetComponentsInChildren<TMP_Text>(true);
            so.FindProperty("objectiveHeaderText").objectReferenceValue = texts.FirstOrDefault(t => t.text.Contains("HOLD THE GATE") || t.name.Contains("Label_Objectives"));
            
            Transform obj00 = newHUD.transform.Find("Screen_HUD_Adventure_01/ScreenSpace/Top Left/HUD_FantasyWarrior_Objectives_02/Content/Objective_List/Objective_00");
            if (obj00 != null) {
                so.FindProperty("objectiveProgressText").objectReferenceValue = obj00.GetComponentInChildren<TMP_Text>(true);
            }
            so.ApplyModifiedProperties();
        }

        // 3. UltimateBarUI
        Transform ultMeter = newHUD.transform.Find("Screen_HUD_Adventure_01/ScreenSpace/Ult Meter");
        if (ultMeter != null) {
            var ultUI = ultMeter.gameObject.AddComponent<UltimateBarUI>();
        }

        // 4. CoinUI
        Transform currency = newHUD.transform.Find("Screen_HUD_Adventure_01/ScreenSpace/Top Left/Horse Stats/Currency");
        if (currency != null) {
            var coinUI = currency.gameObject.AddComponent<CoinUI>();
        }

        // 5. PlayerHealthBarUI
        Transform playerHealth = newHUD.transform.Find("Screen_HUD_Adventure_01/ScreenSpace/Top Left/Player Health Bar");
        if (playerHealth != null) {
            playerHealth.gameObject.AddComponent<PlayerHealthBarUI>();
        }

        // 6. Fortress Gate Health
        Transform gateHealth = newHUD.transform.Find("Screen_HUD_Adventure_01/ScreenSpace/Top Right/Fortress Gate Health");
        if (gateHealth != null) {
            var gateHealthUI = gateHealth.gameObject.AddComponent<HealthBarUI>();
            var so = new SerializedObject(gateHealthUI);
            var gate = GameObject.FindObjectOfType<Gate>();
            if (gate != null) {
                so.FindProperty("health").objectReferenceValue = gate.GetComponent<Health>();
            }
            so.ApplyModifiedProperties();
        }

        Debug.Log("Bladehold HUD wiring completed via script.");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
