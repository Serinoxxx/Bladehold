using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Bladehold.UI
{
    /// <summary>
    ///     The boot scene (build index 0): title screen with Start, Replay Tutorial, Settings and Quit. Start
    ///     (and Replay Tutorial) opens the <see cref="SaveSlotsScreen" />; once a slot is picked the shared
    ///     <see cref="LoadingScreenManager" /> loads the Meta Area hub, where every run begins, or the
    ///     tutorial's first scene until the slot's <see cref="SaveData.tutorialCompleted" /> is set. Shaders
    ///     prewarm in the background while the title screen is up.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Screens")]
        public GameObject titleScreen;
        public GameObject settingsScreen;
        public SaveSlotsScreen saveSlotsScreen;

        [Header("Loading")]
        [Tooltip("Scene Start loads: the Meta Area hub.")]
        public string metaAreaSceneName = "Bladehold Meta Area Scene";

        [Header("Shader Prewarming")]
        [Tooltip("ShaderVariantCollection asset to progressively prewarm in the background on the title screen.")]
        [SerializeField] private ShaderVariantCollection prewarmVariants;
        [Tooltip("How many shader variants to compile per frame when warming up.")]
        [SerializeField] private int variantsPerFrame = 25;
        [Tooltip("If true, starts prewarming variants gently in the background as soon as the Main Menu loads.")]
        [SerializeField] private bool prewarmInBackgroundOnStart = true;

        private void Awake()
        {
            // No slot is active on the title screen; the save slot screen picks one.
            SaveSystem.DeselectSlot();
            if (saveSlotsScreen != null)
            {
                saveSlotsScreen.onSlotReady = LoadMetaArea;
                saveSlotsScreen.onBack = OnBackToTitle;
            }
        }

        private void Start()
        {
            if (saveSlotsScreen == null)
            {
                Debug.LogError("[MainMenuManager] Save Slots Screen is not assigned.", this);
            }
            CursorLockManager.SetUnlock("MainMenu_" + GetInstanceID(), true);
            ResetLeftoverGameState();
            if (prewarmInBackgroundOnStart && prewarmVariants != null && !prewarmVariants.isWarmedUp)
            {
                StartCoroutine(BackgroundPrewarmRoutine());
            }

            ShowScreen(titleScreen);
        }

        /// <summary>
        ///     Safety net for coming back from a game (a tester saw a black title screen with only the logo
        ///     after quitting to the menu; cause not reproduced yet). Anything a battle left behind that would
        ///     freeze or cover the title screen is reset here, and logged so a repro names the culprit.
        /// </summary>
        private void ResetLeftoverGameState()
        {
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                Debug.LogWarning($"[MainMenuManager] Time.timeScale was {Time.timeScale} on the title screen; resetting to 1.", this);
                Time.timeScale = 1f;
            }
            MoreMountains.Feedbacks.MMTimeScaleEvent.Reset();
            StartCoroutine(EnsureLoadingScreenGoneRoutine());
        }

        private IEnumerator EnsureLoadingScreenGoneRoutine()
        {
            // Give a normal transition time to finish its own fade-out first.
            yield return new WaitForSecondsRealtime(3f);
            if (LoadingScreenManager.HideIfStuck())
            {
                Debug.LogError("[MainMenuManager] The loading screen was still showing on the title screen with no load running; force-hid it.", this);
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
            if (settingsScreen) settingsScreen.SetActive(false);
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

        private void LoadMetaArea()
        {
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

            // The shared transition screen finishes any shader prewarm the background pass didn't get through.
            LoadingScreenManager loader = LoadingScreenManager.Instance;
            if (loader != null)
            {
                loader.LoadScene(sceneToLoad);
            }
            else
            {
                SceneManager.LoadScene(sceneToLoad);
            }
        }
    }
}
