using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace Bladehold.UI
{
    /// <summary>
    ///     The boot scene (build index 0): title screen with Start, Replay Tutorial, Settings and Quit. Start
    ///     (and Replay Tutorial) opens the <see cref="SaveSlotsScreen" />; once a slot is picked it prewarms
    ///     shaders behind a loading bar, then loads the Meta Area hub, where every run begins, or the
    ///     tutorial's first scene until the slot's <see cref="SaveData.tutorialCompleted" /> is set.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Screens")]
        public GameObject titleScreen;
        public GameObject settingsScreen;
        public SaveSlotsScreen saveSlotsScreen;
        public GameObject loadingScreen;

        [Header("Loading")]
        public Image logoLoadingFill;
        public Slider loadingBar;
        public TextMeshProUGUI loadingText;
        public TextMeshProUGUI enteringTitleText;
        [Tooltip("Scene Start loads: the Meta Area hub.")]
        public string metaAreaSceneName = "Bladehold Meta Area Scene";

        [Header("Shader Prewarming")]
        [Tooltip("ShaderVariantCollection asset to progressively prewarm before or during loading.")]
        [SerializeField] private ShaderVariantCollection prewarmVariants;
        [Tooltip("How many shader variants to compile per frame when warming up.")]
        [SerializeField] private int variantsPerFrame = 25;
        [Tooltip("If true, starts prewarming variants gently in the background as soon as the Main Menu loads.")]
        [SerializeField] private bool prewarmInBackgroundOnStart = true;

        private void Awake()
        {
            // No slot is active on the title screen; the save slot screen picks one.
            SaveSystem.DeselectSlot();
            EnsureLoadingReferences();
            if (saveSlotsScreen != null)
            {
                saveSlotsScreen.onSlotReady = () => StartCoroutine(LoadMetaArea());
                saveSlotsScreen.onBack = OnBackToTitle;
            }
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
            if (saveSlotsScreen == null)
            {
                Debug.LogError("[MainMenuManager] Save Slots Screen is not assigned.", this);
            }
            CursorLockManager.SetUnlock("MainMenu_" + GetInstanceID(), true);
            if (prewarmInBackgroundOnStart && prewarmVariants != null && !prewarmVariants.isWarmedUp)
            {
                StartCoroutine(BackgroundPrewarmRoutine());
            }

            ShowScreen(titleScreen);
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
            if (settingsScreen) settingsScreen.SetActive(false);
            if (loadingScreen) loadingScreen.SetActive(false);
            if (saveSlotsScreen) saveSlotsScreen.gameObject.SetActive(false);

            if (screen) screen.SetActive(true);
        }

        public void OnPlayClicked()
        {
            forceTutorial = false;
            ShowScreen(saveSlotsScreen != null ? saveSlotsScreen.gameObject : null);
        }

        /// <summary>Title screen Replay Tutorial: pick a slot, then play the tutorial again without touching its completed flag.</summary>
        public void OnReplayTutorialClicked()
        {
            forceTutorial = true;
            ShowScreen(saveSlotsScreen != null ? saveSlotsScreen.gameObject : null);
        }

        private bool forceTutorial;

        public void OnSettingsClicked()
        {
            ShowScreen(settingsScreen);
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

        private IEnumerator LoadMetaArea()
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
            string sceneToLoad = string.IsNullOrEmpty(metaAreaSceneName) ? "Bladehold Meta Area Scene" : metaAreaSceneName;
            // First launch (or Replay Tutorial) goes to the tutorial instead of the hub.
            if (forceTutorial || !SaveSystem.Load().tutorialCompleted)
            {
                TutorialConfigSO tutorial = TutorialConfigSO.Load();
                if (tutorial != null && !string.IsNullOrEmpty(tutorial.firstSceneName))
                {
                    sceneToLoad = tutorial.firstSceneName;
                    TutorialRun.Begin();
                }
            }

            var meta = AreaDatabase.GetMetadata(sceneToLoad);
            string enteringName = !string.IsNullOrEmpty(meta?.displayName) ? meta.displayName : sceneToLoad;
            if (enteringTitleText != null)
            {
                enteringTitleText.text = $"Entering {enteringName}";
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
                if (loadingText)
                {
                    loadingText.text = enteringTitleText != null
                        ? $"Loading... {(overallProgress * 100):F0}%"
                        : $"Entering {enteringName}... {(overallProgress * 100):F0}%";
                }
                yield return null;
            }

            if (logoLoadingFill) logoLoadingFill.fillAmount = 1f;
            if (loadingBar) loadingBar.value = 1f;
        }
    }
}
