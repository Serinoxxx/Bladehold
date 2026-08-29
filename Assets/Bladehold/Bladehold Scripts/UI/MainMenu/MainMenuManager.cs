using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace Bladehold.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        public static bool OpenUpgradesOnLoad = false;

        [Header("Screens")]
        public GameObject titleScreen;
        public GameObject characterSelectScreen;
        public GameObject levelSelectScreen;
        public GameObject settingsScreen;
        public GameObject upgradesScreen;
        public GameObject loadingScreen;

        [Header("Loading")]
        public Image logoLoadingFill;
        public Slider loadingBar;
        public TextMeshProUGUI loadingText;
        public string gameplaySceneName = "Bladehold Survivors Scene";

        [Header("Shader Prewarming")]
        [Tooltip("ShaderVariantCollection asset to progressively prewarm before or during loading.")]
        [SerializeField] private ShaderVariantCollection prewarmVariants;
        [Tooltip("How many shader variants to compile per frame when warming up.")]
        [SerializeField] private int variantsPerFrame = 25;
        [Tooltip("If true, starts prewarming variants gently in the background as soon as the Main Menu loads.")]
        [SerializeField] private bool prewarmInBackgroundOnStart = true;

        private void Awake()
        {
            EnsureLoadingReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureLoadingReferences();
        }
#endif

        private void EnsureLoadingReferences()
        {
            if (logoLoadingFill == null && loadingScreen != null)
            {
                var fills = loadingScreen.GetComponentsInChildren<Image>(true);
                foreach (var fill in fills)
                {
                    if (fill != null && fill.gameObject.name == "LogoLoadingFill")
                    {
                        logoLoadingFill = fill;
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            CursorLockManager.SetUnlock("MainMenu_" + GetInstanceID(), true);
            if (prewarmInBackgroundOnStart && prewarmVariants != null && !prewarmVariants.isWarmedUp)
            {
                StartCoroutine(BackgroundPrewarmRoutine());
            }

            if (OpenUpgradesOnLoad)
            {
                OpenUpgradesOnLoad = false;
                ShowScreen(upgradesScreen);
            }
            else
            {
                ShowScreen(titleScreen);
            }
        }

        private IEnumerator BackgroundPrewarmRoutine()
        {
            if (prewarmVariants == null) yield break;
            int total = prewarmVariants.variantCount;
            while (!prewarmVariants.isWarmedUp && prewarmVariants.warmedUpVariantCount < total)
            {
                prewarmVariants.WarmUpProgressively(Mathf.Max(1, variantsPerFrame / 2));
                yield return null;
            }
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

        public void OnLevelSelected(string targetSceneName = null)
        {
            StartCoroutine(LoadGameplayScene(targetSceneName));
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

        private IEnumerator LoadGameplayScene(string targetSceneName = null)
        {
            EnsureLoadingReferences();
            ShowScreen(loadingScreen);
            if (logoLoadingFill) logoLoadingFill.fillAmount = 0f;
            if (loadingBar) loadingBar.value = 0f;
            
            // Wait a frame for UI to update
            yield return null;

            // 1. Progressive shader prewarm phase
            if (prewarmVariants != null && !prewarmVariants.isWarmedUp && prewarmVariants.variantCount > 0)
            {
                int totalVariants = prewarmVariants.variantCount;
                if (loadingText) loadingText.text = "Prewarming Shaders...";

                while (!prewarmVariants.isWarmedUp && prewarmVariants.warmedUpVariantCount < totalVariants)
                {
                    prewarmVariants.WarmUpProgressively(variantsPerFrame);
                    float shaderProgress = Mathf.Clamp01((float)prewarmVariants.warmedUpVariantCount / totalVariants);
                    float shaderFill = shaderProgress * 0.35f;
                    if (logoLoadingFill) logoLoadingFill.fillAmount = shaderFill;
                    if (loadingBar) loadingBar.value = shaderFill;
                    if (loadingText) loadingText.text = $"Prewarming Shaders... {(shaderProgress * 100):F0}%";
                    yield return null;
                }
            }

            // 2. Scene loading phase
            string sceneToLoad = !string.IsNullOrEmpty(targetSceneName) ? targetSceneName : gameplaySceneName;
            if (string.IsNullOrEmpty(sceneToLoad))
            {
                sceneToLoad = "Bladehold Survivors Scene";
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad);
            if (op == null)
            {
                Debug.LogError($"[MainMenuManager] Failed to load scene '{sceneToLoad}'! Check Build Settings.");
                yield break;
            }

            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                float sceneProgress = Mathf.Clamp01(op.progress / 0.9f);
                float overallProgress = (prewarmVariants != null && prewarmVariants.variantCount > 0)
                    ? 0.35f + (sceneProgress * 0.65f)
                    : sceneProgress;

                if (logoLoadingFill) logoLoadingFill.fillAmount = overallProgress;
                if (loadingBar) loadingBar.value = overallProgress;
                if (loadingText) loadingText.text = $"Loading Scene... {(overallProgress * 100):F0}%";
                yield return null;
            }

            if (logoLoadingFill) logoLoadingFill.fillAmount = 1f;
            if (loadingBar) loadingBar.value = 1f;
        }
    }
}
