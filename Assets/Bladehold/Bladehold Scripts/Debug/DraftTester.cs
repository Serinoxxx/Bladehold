
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Config/test-only harness to easily test Draft Upgrades in Editor.
///     Draws an IMGUI panel to apply/remove draft skills dynamically.
/// </summary>
public class DraftTester : MonoBehaviour
{
    private bool guiVisible = true;
    private Vector2 scrollPos;
    private DraftCategory currentCategoryFilter = DraftCategory.Weapon;

    private void Start()
    {
        DraftUpgradeService.GetOrCreateInstance();
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
        {
            guiVisible = !guiVisible;
        }
    }

    private void OnGUI()
    {
        if (!guiVisible) return;

        float width = 320f;
        float padding = 10f;
        // Position it on the left side of the screen
        Rect rect = new Rect(padding, padding, width, Screen.height - 2f * padding);
        
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("Draft Skill Tester (Press F2 to hide)", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

        if (GUILayout.Button("Reset All Run Upgrades & Reload Scene", GUILayout.Height(30)))
        {
            RunSession.StartNewRun();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        GUILayout.Space(10);
        GUILayout.Label("Category Filter:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Weapon")) currentCategoryFilter = DraftCategory.Weapon;
        if (GUILayout.Button("Elemental")) currentCategoryFilter = DraftCategory.Elemental;
        if (GUILayout.Button("Fortress")) currentCategoryFilter = DraftCategory.Fortress;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        DraftUpgradeService service = DraftUpgradeService.Instance;
        if (service != null && service.AllDefinitions != null)
        {
            foreach (var def in service.AllDefinitions)
            {
                if (def.category != currentCategoryFilter) continue;

                int currentLevel = RunSession.GetUpgradeLevel(def.id);
                
                GUILayout.BeginHorizontal(GUI.skin.box);
                
                GUILayout.BeginVertical();
                string title = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
                GUILayout.Label($"{title} (Lv {currentLevel}/{def.maxLevel})");
                if (def.isUltimate) GUILayout.Label("[ULTIMATE]", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.yellow }, fontSize = 10 });
                if (!string.IsNullOrEmpty(def.targetSlot)) GUILayout.Label($"[SLOT: {def.targetSlot}]", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.cyan }, fontSize = 10 });
                GUILayout.EndVertical();

                if (currentLevel < def.maxLevel)
                {
                    if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(30)))
                    {
                        service.ApplyUpgrade(def);
                    }
                }
                else
                {
                    GUILayout.Label("MAX", GUILayout.Width(30));
                }

                GUILayout.EndHorizontal();
            }
        }
        else
        {
            GUILayout.Label("DraftUpgradeService not ready.");
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}

