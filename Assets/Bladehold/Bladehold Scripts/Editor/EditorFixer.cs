using UnityEditor;
using UnityEngine;

public static class EditorFixer
{
    [MenuItem("Tools/Fix Player Classes")]
    public static void Fix()
    {
        // 1. Create Mage Skill Tree
        string mageTreePath = "Assets/Bladehold/Bladehold Scripts/Player/SkillTreeSOMage.asset";
        SkillTreeSO mageTree = AssetDatabase.LoadAssetAtPath<SkillTreeSO>(mageTreePath);
        if (mageTree == null)
        {
            mageTree = ScriptableObject.CreateInstance<SkillTreeSO>();
            AssetDatabase.CreateAsset(mageTree, mageTreePath);
            Debug.Log("Created SkillTreeSOMage");
        }
        
        var so = new SerializedObject(mageTree);
        so.FindProperty("locKeyPrefix").stringValue = "skill.mage";
        so.ApplyModifiedProperties();

        // 2. Fix Mage Class Definition
        string mageDefPath = "Assets/Bladehold/Bladehold Scripts/Player/ClassDefinitionSO Mage.asset";
        ClassDefinitionSO mageDef = AssetDatabase.LoadAssetAtPath<ClassDefinitionSO>(mageDefPath);
        if (mageDef != null)
        {
            mageDef.skillTree = mageTree;
            mageDef.keySkillIds = new string[] { "wand_unlock", "light_unlock", "fire_explode" };
            EditorUtility.SetDirty(mageDef);
            Debug.Log("Fixed ClassDefinitionSO Mage");
        }
        
        // 3. Update Player.prefab
        string playerPrefabPath = "Assets/Bladehold/Bladehold Prefabs/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        if (playerPrefab != null)
        {
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(playerPrefabPath))
            {
                var prefabRoot = editScope.prefabContentsRoot;
                PlayerClassController pcc = prefabRoot.GetComponent<PlayerClassController>();
                if (pcc == null)
                {
                    pcc = prefabRoot.AddComponent<PlayerClassController>();
                    Debug.Log("Added missing PlayerClassController to Player prefab");
                }
                
                if (pcc != null)
                {
                    var swordsmanDef = AssetDatabase.LoadAssetAtPath<ClassDefinitionSO>("Assets/Bladehold/Bladehold Scripts/Player/ClassDefinitionSO Swordsman.asset");
                    var berserkerDef = AssetDatabase.LoadAssetAtPath<ClassDefinitionSO>("Assets/Bladehold/Bladehold Scripts/Player/ClassDefinitionSO Berserker.asset");
                    // mageDef is already loaded at the top of Fix()
                    
                    bool hasSwordsman = false;
                    bool hasBerserker = false;
                    bool hasMage = false;
                    
                    var serializedObject = new SerializedObject(pcc);
                    var slotsProp = serializedObject.FindProperty("slots");
                    
                    for(int i = 0; i < slotsProp.arraySize; i++)
                    {
                        var slotDef = slotsProp.GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue;
                        if (slotDef == swordsmanDef) hasSwordsman = true;
                        if (slotDef == berserkerDef) hasBerserker = true;
                        if (slotDef == mageDef) hasMage = true;
                    }
                    
                    if (!hasSwordsman && swordsmanDef != null)
                    {
                        slotsProp.arraySize++;
                        var newSlot = slotsProp.GetArrayElementAtIndex(slotsProp.arraySize - 1);
                        newSlot.FindPropertyRelative("definition").objectReferenceValue = swordsmanDef;
                        Debug.Log("Added Swordsman slot to Player prefab");
                    }
                    
                    if (!hasBerserker && berserkerDef != null)
                    {
                        slotsProp.arraySize++;
                        var newSlot = slotsProp.GetArrayElementAtIndex(slotsProp.arraySize - 1);
                        newSlot.FindPropertyRelative("definition").objectReferenceValue = berserkerDef;
                        Debug.Log("Added Berserker slot to Player prefab");
                    }
                    
                    if (!hasMage && mageDef != null)
                    {
                        slotsProp.arraySize++;
                        var newSlot = slotsProp.GetArrayElementAtIndex(slotsProp.arraySize - 1);
                        newSlot.FindPropertyRelative("definition").objectReferenceValue = mageDef;
                        Debug.Log("Added Mage slot to Player prefab");
                    }
                    
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
        
        // 4. Auto-populate missing icons in all SkillTreeSO assets
        FixSkillTreeIcons();
        
        AssetDatabase.SaveAssets();
    }

    private static void FixSkillTreeIcons()
    {
        string[] treeGuids = AssetDatabase.FindAssets("t:SkillTreeSO");
        foreach (string guid in treeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillTreeSO tree = AssetDatabase.LoadAssetAtPath<SkillTreeSO>(path);
            if (tree == null) continue;

            var so = new SerializedObject(tree);
            var csvProp = so.FindProperty("csv");
            if (csvProp == null || csvProp.objectReferenceValue == null) continue;

            TextAsset csv = csvProp.objectReferenceValue as TextAsset;
            if (csv == null) continue;

            string[] lines = csv.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1) continue;

            var iconsProp = so.FindProperty("icons");
            System.Collections.Generic.HashSet<string> existingIcons = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < iconsProp.arraySize; i++)
            {
                var spriteRef = iconsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (spriteRef != null)
                {
                    existingIcons.Add(spriteRef.name);
                }
            }

            bool changed = false;
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cols = lines[i].Split(',');
                if (cols.Length > 13)
                {
                    string iconName = cols[13].Trim();
                    if (!string.IsNullOrEmpty(iconName) && !existingIcons.Contains(iconName))
                    {
                        // Find sprite by name
                        string[] spriteGuids = AssetDatabase.FindAssets(iconName + " t:Sprite");
                        if (spriteGuids.Length > 0)
                        {
                            string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[0]);
                            Sprite foundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                            if (foundSprite != null && foundSprite.name == iconName)
                            {
                                iconsProp.arraySize++;
                                iconsProp.GetArrayElementAtIndex(iconsProp.arraySize - 1).objectReferenceValue = foundSprite;
                                existingIcons.Add(iconName);
                                changed = true;
                                Debug.Log($"Added missing icon '{iconName}' to {tree.name}");
                            }
                        }
                    }
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(tree);
            }
        }
    }

    [MenuItem("Tools/Fix Earth Splitter Telegraph")]
    public static void FixEarthSplitterTelegraph()
    {
        string path = "Assets/Bladehold/Bladehold Prefabs/EarthSplitterTelegraph.prefab";
        using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            var root = editScope.prefabContentsRoot;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;

            var allPS = root.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allPS)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    if (renderer.renderMode == ParticleSystemRenderMode.Billboard)
                    {
                        renderer.alignment = ParticleSystemRenderSpace.Local;
                    }
                }
                Debug.Log($"Fixed ParticleSystem on {ps.gameObject.name}: SimSpace=Local, ScalingMode=Hierarchy, Alignment={(renderer != null ? renderer.alignment.ToString() : "N/A")}");
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("EarthSplitterTelegraph prefab fixed and saved successfully!");
    }
}

