using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace Bladehold.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Screens")]
        public GameObject titleScreen;
        public GameObject characterSelectScreen;
        public GameObject levelSelectScreen;
        public GameObject settingsScreen;
        public GameObject upgradesScreen;
        public GameObject loadingScreen;

        [Header("Loading")]
        public Slider loadingBar;
        public TextMeshProUGUI loadingText;
        public string gameplaySceneName = "Bladehold Test Scene";

        private void Start()
        {
            CursorLockManager.SetUnlock("MainMenu_" + GetInstanceID(), true);
            ShowScreen(titleScreen);
        }

        private void OnDestroy()
        {
            CursorLockManager.SetUnlock("MainMenu_" + GetInstanceID(), false);
        }

        public void ShowScreen(GameObject screen)
        {
            if (titleScreen) titleScreen.SetActive(false);
            if (characterSelectScreen) characterSelectScreen.SetActive(false);
            if (levelSelectScreen) levelSelectScreen.SetActive(false);
            if (settingsScreen) settingsScreen.SetActive(false);
            if (upgradesScreen) upgradesScreen.SetActive(false);
            if (loadingScreen) loadingScreen.SetActive(false);

            if (screen) screen.SetActive(true);
        }

        public void OnPlayClicked()
        {
            ShowScreen(characterSelectScreen);
        }

        public void OnCharacterSelected()
        {
            ShowScreen(levelSelectScreen);
        }

        public void OnLevelSelected()
        {
            StartCoroutine(LoadGameplayScene());
        }

        public void OnSettingsClicked()
        {
            ShowScreen(settingsScreen);
        }

        public void OnUpgradesClicked()
        {
            ShowScreen(upgradesScreen);
        }
        
        public void OnBackToTitle()
        {
            ShowScreen(titleScreen);
        }

        public void OnQuitClicked()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private IEnumerator LoadGameplayScene()
        {
            ShowScreen(loadingScreen);
            
            // Wait a frame for UI to update
            yield return null;

            AsyncOperation op = SceneManager.LoadSceneAsync(gameplaySceneName);
            op.allowSceneActivation = false;

            while (!op.isDone)
            {
                if (loadingBar) loadingBar.value = op.progress;
                if (loadingText) loadingText.text = $"Loading... {(op.progress * 100):F0}%";

                if (op.progress >= 0.9f)
                {
                    if (loadingBar) loadingBar.value = 1f;
                    if (loadingText) loadingText.text = "Press Any Key To Continue";
                    
                    if (Input.anyKeyDown)
                    {
                        op.allowSceneActivation = true;
                    }
                }
                yield return null;
            }
        }
    }
}
