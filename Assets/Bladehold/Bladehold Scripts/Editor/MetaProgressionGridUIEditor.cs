using UnityEditor;
using UnityEngine;

namespace Bladehold.UI
{
    /// <summary>
    ///     Custom Inspector for MetaProgressionGridUI with one-click Edit Mode prefab grid generation.
    /// </summary>
    [CustomEditor(typeof(MetaProgressionGridUI))]
    public class MetaProgressionGridUIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MetaProgressionGridUI gridUI = (MetaProgressionGridUI)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "In Edit Mode, cards are instantiated as linked Prefab instances of MetaSkillCard.\n" +
                "Any visual/layout changes made to MetaSkillCard.prefab will update all cards in edit mode.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild Grid (Linked Prefab Instances)", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gridUI.gameObject, "Rebuild Meta Progression Grid");
                gridUI.RefreshUI();
                EditorUtility.SetDirty(gridUI.gameObject);
            }

            if (GUILayout.Button("Clear Grid", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(gridUI.gameObject, "Clear Meta Progression Grid");
                gridUI.ClearGrid();
                EditorUtility.SetDirty(gridUI.gameObject);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
