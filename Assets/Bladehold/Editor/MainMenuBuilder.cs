using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bladehold.UI;

namespace Bladehold.Editor
{
    public class MainMenuBuilder : UnityEditor.EditorWindow
    {
        [MenuItem("Bladehold/Build Main Menu")]
        public static void BuildMenu()
        {
            string sourceScene = "Assets/Synty/InterfaceFantasyWarriorHUD/Samples/Scenes/00_Demo_FantasyWarrior_Title.unity";
            string targetScene = "Assets/Bladehold/Bladehold Scenes/MainMenu.unity";
            
            if (!AssetDatabase.CopyAsset(sourceScene, targetScene))
            {
                Debug.LogWarning("Scene might already exist or failed to copy.");
            }
            
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(targetScene);
            
            // Clean up and construct
            GameObject canvasObj = GameObject.Find("UI_Canvas");
            if (canvasObj == null)
            {
                Debug.LogError("No Canvas found in Synty Title scene!");
                return;
            }
            
            // Add MainMenuManager
            MainMenuManager manager = canvasObj.GetComponent<MainMenuManager>();
            if (manager == null) manager = canvasObj.AddComponent<MainMenuManager>();
            
            // The Synty scene usually has some Title screens. Let's find them or create empty parents.
            GameObject titleScreen = FindOrCreateChild(canvasObj, "TitleScreen");
            GameObject characterSelectScreen = FindOrCreateChild(canvasObj, "CharacterSelectScreen");
            GameObject levelSelectScreen = FindOrCreateChild(canvasObj, "LevelSelectScreen");
            GameObject settingsScreen = FindOrCreateChild(canvasObj, "SettingsScreen");
            GameObject loadingScreen = FindOrCreateChild(canvasObj, "LoadingScreen");
            
            manager.titleScreen = titleScreen;
            manager.characterSelectScreen = characterSelectScreen;
            manager.levelSelectScreen = levelSelectScreen;
            manager.settingsScreen = settingsScreen;
            manager.loadingScreen = loadingScreen;
            manager.gameplaySceneName = "Bladehold Test Scene";
            
            // Build Settings Menu
            SettingsMenu settings = settingsScreen.GetComponent<SettingsMenu>();
            if (settings == null) settings = settingsScreen.AddComponent<SettingsMenu>();
            // Add some basic text to settings
            settings.resolutionText = CreateText(settingsScreen, "ResolutionText", new Vector2(0, 100));
            settings.frameRateText = CreateText(settingsScreen, "FrameRateText", new Vector2(0, 50));
            settings.vsyncText = CreateText(settingsScreen, "VSyncText", new Vector2(0, 0));
            CreateButton(settingsScreen, "Back", new Vector2(0, -100), manager, "OnBackToTitle");
            
            // Build Character Select Rotunda
            GameObject rotundaRoot = new GameObject("CharacterRotundaRoot");
            CharacterRotunda charRotunda = rotundaRoot.AddComponent<CharacterRotunda>();
            charRotunda.rotundaCenter = new GameObject("Center").transform;
            charRotunda.rotundaCenter.SetParent(rotundaRoot.transform);
            charRotunda.characterNameText = CreateText(characterSelectScreen, "ClassNameText", new Vector2(0, 200));
            charRotunda.characterNames = new string[] { "Swordsman", "Berserker (Locked)", "Mage (Locked)" };
            CreateButton(characterSelectScreen, "Next Character", new Vector2(200, 0), charRotunda, "Next");
            CreateButton(characterSelectScreen, "Prev Character", new Vector2(-200, 0), charRotunda, "Previous");
            CreateButton(characterSelectScreen, "Select", new Vector2(0, -200), manager, "OnCharacterSelected");
            CreateButton(characterSelectScreen, "Back", new Vector2(0, -250), manager, "OnBackToTitle");
            
            // Build Level Carousel
            UILevelCarousel levelCarousel = levelSelectScreen.AddComponent<UILevelCarousel>();
            levelCarousel.items = new RectTransform[3];
            for (int i=0; i<3; i++) {
                GameObject img = new GameObject("LevelImg_" + i, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                img.transform.SetParent(levelSelectScreen.transform, false);
                levelCarousel.items[i] = img.GetComponent<RectTransform>();
            }
            CreateButton(levelSelectScreen, "Next Level", new Vector2(200, -200), levelCarousel, "Next");
            CreateButton(levelSelectScreen, "Prev Level", new Vector2(-200, -200), levelCarousel, "Previous");
            CreateButton(levelSelectScreen, "Play Level", new Vector2(0, -200), manager, "OnLevelSelected");
            CreateButton(levelSelectScreen, "Back", new Vector2(0, -250), manager, "OnPlayClicked"); // go back to char select
            
            // Loading Screen
            manager.loadingBar = CreateSlider(loadingScreen, "LoadingBar", new Vector2(0, -50));
            manager.loadingText = CreateText(loadingScreen, "LoadingText", new Vector2(0, 0));
            
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MainMenu Built and Saved.");
        }
        
        static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            Transform t = parent.transform.Find(name);
            if (t != null) return t.gameObject;
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            ((RectTransform)go.transform).anchorMin = Vector2.zero;
            ((RectTransform)go.transform).anchorMax = Vector2.one;
            ((RectTransform)go.transform).sizeDelta = Vector2.zero;
            return go;
        }
        
        static TextMeshProUGUI CreateText(GameObject parent, string name, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(300, 50);
            TextMeshProUGUI txt = go.GetComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 36;
            txt.text = name;
            return txt;
        }
        
        static Slider CreateSlider(GameObject parent, string name, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(400, 20);
            return go.GetComponent<Slider>();
        }
        
        static void CreateButton(GameObject parent, string label, Vector2 pos, MonoBehaviour target, string methodName)
        {
            GameObject go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(160, 50);
            Button btn = go.GetComponent<Button>();
            
            // Add Text
            TextMeshProUGUI txt = CreateText(go, "Text", Vector2.zero);
            txt.text = label;
            txt.color = Color.black;
            
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(btn.onClick, new UnityEngine.Events.UnityAction<string>(target.SendMessage), methodName);
        }
    }
}
