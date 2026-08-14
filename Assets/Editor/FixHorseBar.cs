using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MoreMountains.Tools;

public static class FixHorseBar
{
    [InitializeOnLoadMethod]
    public static void FixPrefab()
    {
        string path = "Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var contents = editingScope.prefabContentsRoot;
                bool modified = false;

                foreach (var bar in contents.GetComponentsInChildren<MMProgressBar>(true))
                {
                    if (bar.FillMode == MMProgressBar.FillModes.FillAmount)
                    {
                        if (bar.ForegroundBar != null)
                        {
                            var img = bar.ForegroundBar.GetComponent<Image>();
                            if (img != null)
                            {
                                if (img.type != Image.Type.Filled || img.fillMethod != Image.FillMethod.Horizontal)
                                {
                                    img.type = Image.Type.Filled;
                                    img.fillMethod = Image.FillMethod.Horizontal;
                                    img.fillOrigin = (int)Image.OriginHorizontal.Left;
                                    EditorUtility.SetDirty(img);
                                    modified = true;
                                    Debug.Log($"Fixed Image type on {bar.name} ForegroundBar.");
                                }
                            }
                        }
                        
                        // Also fix delayed bars if any
                        if (bar.DelayedBarDecreasing != null)
                        {
                            var img = bar.DelayedBarDecreasing.GetComponent<Image>();
                            if (img != null)
                            {
                                if (img.type != Image.Type.Filled || img.fillMethod != Image.FillMethod.Horizontal)
                                {
                                    img.type = Image.Type.Filled;
                                    img.fillMethod = Image.FillMethod.Horizontal;
                                    img.fillOrigin = (int)Image.OriginHorizontal.Left;
                                    EditorUtility.SetDirty(img);
                                    modified = true;
                                    Debug.Log($"Fixed Image type on {bar.name} DelayedBarDecreasing.");
                                }
                            }
                        }
                    }
                }

                if (modified)
                {
                    Debug.Log("Successfully fixed Bladehold HUD.prefab Image Types for MMProgressBars.");
                }
            }
        }
    }
}
