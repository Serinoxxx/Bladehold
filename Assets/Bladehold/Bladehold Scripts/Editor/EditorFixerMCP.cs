using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class EditorFixerMCP
{
    [MenuItem("Tools/Run MCP Fix")]
    public static void Fix()
    {
        // 1. Create RageSO
        string ragePath = "Assets/Bladehold/Config/RageSO.asset";
        RageSO rageSO = AssetDatabase.LoadAssetAtPath<RageSO>(ragePath);
        if (rageSO == null)
        {
            rageSO = ScriptableObject.CreateInstance<RageSO>();
            AssetDatabase.CreateAsset(rageSO, ragePath);
            Debug.Log("Created RageSO");
        }

        // 2. Create MageImbuementSO
        string imbuementPath = "Assets/Bladehold/Config/MageImbuementSO.asset";
        MageImbuementSO imbuementSO = AssetDatabase.LoadAssetAtPath<MageImbuementSO>(imbuementPath);
        if (imbuementSO == null)
        {
            imbuementSO = ScriptableObject.CreateInstance<MageImbuementSO>();
            AssetDatabase.CreateAsset(imbuementSO, imbuementPath);
            Debug.Log("Created MageImbuementSO");
        }

        // 3. Edit Player Prefab
        string playerPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Player.prefab";
        using (var editScope = new PrefabUtility.EditPrefabContentsScope(playerPrefabPath))
        {
            var prefabRoot = editScope.prefabContentsRoot;

            // Ensure PlayerClassController exists
            PlayerClassController pcc = prefabRoot.GetComponent<PlayerClassController>();
            if (pcc == null) pcc = prefabRoot.AddComponent<PlayerClassController>();

            // Ensure Weapon bone
            Transform rightHand = GetRecursive(prefabRoot.transform, "prop_r");
            if (rightHand == null)
            {
                Debug.LogError("Could not find prop_r bone!");
                return;
            }

            // Create child objects for organization
            Transform classCompsParent = GetRecursive(prefabRoot.transform, "Class Components");
            if (classCompsParent == null)
            {
                GameObject ccObj = new GameObject("Class Components");
                ccObj.transform.SetParent(prefabRoot.transform, false);
                classCompsParent = ccObj.transform;
            }

            Transform berserkerRoot = GetRecursive(classCompsParent, "Berserker");
            if (berserkerRoot == null)
            {
                GameObject bObj = new GameObject("Berserker");
                bObj.transform.SetParent(classCompsParent, false);
                berserkerRoot = bObj.transform;
            }

            Transform mageRoot = GetRecursive(classCompsParent, "Mage");
            if (mageRoot == null)
            {
                GameObject mObj = new GameObject("Mage");
                mObj.transform.SetParent(classCompsParent, false);
                mageRoot = mObj.transform;
            }

            // Ensure components for Berserker on the child object
            RageBuff rageBuff = berserkerRoot.GetComponent<RageBuff>();
            if (rageBuff == null) rageBuff = berserkerRoot.gameObject.AddComponent<RageBuff>();
            
            PainIntoPower painIntoPower = berserkerRoot.GetComponent<PainIntoPower>();
            if (painIntoPower == null) painIntoPower = berserkerRoot.gameObject.AddComponent<PainIntoPower>();

            PlayerThrownAxe thrownAxe = berserkerRoot.GetComponent<PlayerThrownAxe>();
            if (thrownAxe == null) thrownAxe = berserkerRoot.gameObject.AddComponent<PlayerThrownAxe>();

            // Ensure components for Mage on the child object
            PlayerWand playerWand = mageRoot.GetComponent<PlayerWand>();
            if (playerWand == null) playerWand = mageRoot.gameObject.AddComponent<PlayerWand>();

            MageImbuement mageImbuement = mageRoot.GetComponentInChildren<MageImbuement>(true);
            if (mageImbuement == null)
            {
                mageImbuement = mageRoot.gameObject.AddComponent<MageImbuement>();
            }

            // Create/Assign Weapons
            GameObject axeObj = FindOrCreateWeapon(rightHand, "SM_Wep_Axe_01");
            GameObject wandObj = FindOrCreateWeapon(rightHand, "SM_Wep_Wand_01"); // using a generic wand name, maybe we should use staff

            // Get default references
            DamageTrigger defaultMeleeTrigger = rightHand.GetComponentInChildren<DamageTrigger>(true);
            SwordHitFeedback defaultHitFeedback = rightHand.GetComponentInChildren<SwordHitFeedback>(true);

            // Wire up PlayerClassController Slots
            var serializedObject = new SerializedObject(pcc);
            var slotsProp = serializedObject.FindProperty("slots");

            // Make sure we have 3 slots
            while (slotsProp.arraySize < 3) slotsProp.arraySize++;

            // Wire Swordsman (Slot 0)
            var swordsmanSlot = slotsProp.GetArrayElementAtIndex(0);
            swordsmanSlot.FindPropertyRelative("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ClassDefinitionSO>("Assets/Bladehold/Bladehold Scripts/Player/ClassDefinitionSO Swordsman.asset");
            
            // Swordsman weapons
            var smWeapons = swordsmanSlot.FindPropertyRelative("weaponObjects");
            smWeapons.arraySize = 0; // We'll just leave it empty if we don't know the default, or grab the Sword and Bow
            Transform defaultSword = rightHand.Find("SM_Wep_Sword_01");
            Transform defaultBow = rightHand.Find("SM_Wep_Bow_01");
            if (defaultSword != null) { smWeapons.arraySize++; smWeapons.GetArrayElementAtIndex(smWeapons.arraySize - 1).objectReferenceValue = defaultSword.gameObject; }
            if (defaultBow != null) { smWeapons.arraySize++; smWeapons.GetArrayElementAtIndex(smWeapons.arraySize - 1).objectReferenceValue = defaultBow.gameObject; }

            // Swordsman components
            var smComps = swordsmanSlot.FindPropertyRelative("classComponents");
            smComps.arraySize = 0;
            var playerBow = prefabRoot.GetComponent<PlayerBow>();
            var freezingDraw = prefabRoot.GetComponent<FreezingDraw>();
            if (playerBow != null) { smComps.arraySize++; smComps.GetArrayElementAtIndex(smComps.arraySize - 1).objectReferenceValue = playerBow; }
            if (freezingDraw != null) { smComps.arraySize++; smComps.GetArrayElementAtIndex(smComps.arraySize - 1).objectReferenceValue = freezingDraw; }

            swordsmanSlot.FindPropertyRelative("meleeTrigger").objectReferenceValue = defaultMeleeTrigger;
            swordsmanSlot.FindPropertyRelative("hitFeedback").objectReferenceValue = defaultHitFeedback;

            // Wire Berserker (Slot 1)
            var berserkerSlot = slotsProp.GetArrayElementAtIndex(1);
            berserkerSlot.FindPropertyRelative("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ClassDefinitionSO>("Assets/Bladehold/Bladehold Scripts/Player/ClassDefinitionSO Berserker.asset");
            
            var bkWeapons = berserkerSlot.FindPropertyRelative("weaponObjects");
            bkWeapons.arraySize = 1;
            bkWeapons.GetArrayElementAtIndex(0).objectReferenceValue = axeObj;

            var bkComps = berserkerSlot.FindPropertyRelative("classComponents");
            bkComps.arraySize = 3;
            bkComps.GetArrayElementAtIndex(0).objectReferenceValue = thrownAxe;
            bkComps.GetArrayElementAtIndex(1).objectReferenceValue = rageBuff;
            bkComps.GetArrayElementAtIndex(2).objectReferenceValue = painIntoPower;

            berserkerSlot.FindPropertyRelative("meleeTrigger").objectReferenceValue = defaultMeleeTrigger;
            berserkerSlot.FindPropertyRelative("hitFeedback").objectReferenceValue = defaultHitFeedback;

            // Wire Mage (Slot 2)
            var mageSlot = slotsProp.GetArrayElementAtIndex(2);
            mageSlot.FindPropertyRelative("definition").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ClassDefinitionSO>("Assets/Bladehold/Bladehold Scripts/Player/ClassDefinitionSO Mage.asset");
            
            var mgWeapons = mageSlot.FindPropertyRelative("weaponObjects");
            mgWeapons.arraySize = 1;
            mgWeapons.GetArrayElementAtIndex(0).objectReferenceValue = wandObj;

            var mgComps = mageSlot.FindPropertyRelative("classComponents");
            mgComps.arraySize = 2;
            mgComps.GetArrayElementAtIndex(0).objectReferenceValue = playerWand;
            mgComps.GetArrayElementAtIndex(1).objectReferenceValue = mageImbuement;

            mageSlot.FindPropertyRelative("meleeTrigger").objectReferenceValue = defaultMeleeTrigger;
            mageSlot.FindPropertyRelative("hitFeedback").objectReferenceValue = defaultHitFeedback;

            serializedObject.ApplyModifiedProperties();

            // Wire SOs using serialized object to bypass private fields
            var rageSOObj = new SerializedObject(rageBuff);
            rageSOObj.FindProperty("config").objectReferenceValue = rageSO;
            rageSOObj.ApplyModifiedProperties();

            var imbuementSOObj = new SerializedObject(mageImbuement);
            imbuementSOObj.FindProperty("config").objectReferenceValue = imbuementSO;
            imbuementSOObj.ApplyModifiedProperties();

            Debug.Log("Successfully wired Player.prefab for all classes.");
        }
        
        AssetDatabase.SaveAssets();
    }

    private static Transform GetRecursive(Transform current, string name)
    {
        if (current.name == name) return current;
        foreach (Transform child in current)
        {
            var result = GetRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static GameObject FindOrCreateWeapon(Transform parent, string weaponPrefabName)
    {
        Transform existing = parent.Find(weaponPrefabName);
        if (existing != null) return existing.gameObject;

        string[] guids = AssetDatabase.FindAssets(weaponPrefabName + " t:Prefab");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = weaponPrefabName; // Strip (Clone)
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.SetActive(false); // Initially disabled
                return instance;
            }
        }
        
        // Fallback: create empty
        GameObject empty = new GameObject(weaponPrefabName);
        empty.transform.SetParent(parent, false);
        empty.SetActive(false);
        return empty;
    }
}
