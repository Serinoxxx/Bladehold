using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TestDraftCatalogLoading
{
    [MenuItem("Bladehold/Tests/Draft Catalog Loading (Edit Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before running draft catalog loading tests.");

        TextAsset catalog = Resources.Load<TextAsset>("DraftUpgrades");
        Require(catalog != null, "DraftUpgrades must be bundled as a Resources TextAsset.");
        var go = new GameObject("DraftCatalogLoadingTest") { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false);
        TextAsset customCatalog = null;
        try
        {
            var service = go.AddComponent<DraftUpgradeService>();
            var serialized = new SerializedObject(service);
            Require(serialized.FindProperty("draftUpgradesCsv").objectReferenceValue == null,
                "The resource-loading test must not have a serialized CSV reference.");

            int rowCount = 0;
            using (var reader = new StringReader(catalog.text))
            {
                reader.ReadLine();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string id = CsvUtil.SplitLine(line)[0].Trim();
                    Require(service.GetById(id) != null, "Bundled draft not loaded: " + id);
                    rowCount++;
                }
            }
            Require(rowCount > 0 && service.AllDefinitions.Count == rowCount,
                "Loaded draft count must match the bundled catalog.");
            foreach (DraftCategory category in Enum.GetValues(typeof(DraftCategory)))
            {
                bool found = false;
                foreach (var definition in service.AllDefinitions)
                    if (definition.category == category) found = true;
                Require(found, "No bundled drafts for category " + category);
            }

            Object.DestroyImmediate(service);
            service = go.AddComponent<DraftUpgradeService>();
            customCatalog = new TextAsset(
                "id,displayName,category,weapon,element,isUltimate,maxLevel,description,upgradeText,stat,kind,amount,icon\n" +
                "test_custom,Custom,Weapon,sword,,0,1,Custom draft,,SwordDamage,Flat,2,\n");
            serialized = new SerializedObject(service);
            serialized.FindProperty("draftUpgradesCsv").objectReferenceValue = customCatalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(service.AllDefinitions.Count == 1 && service.GetById("test_custom") != null,
                "An assigned CSV must take precedence over the bundled catalog.");
            Debug.Log($"[DraftCatalogLoading] PASS: {rowCount} bundled drafts load without a scene reference; assigned CSV override preserved.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            if (customCatalog != null) Object.DestroyImmediate(customCatalog);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[DraftCatalogLoading] " + message);
    }
}
