using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bladehold.UI
{
    /// <summary>
    ///     Persistent scene transition manager.
    ///     Reuses the loading pipeline from MainMenuManager (shader prewarming, progressive fill)
    ///     and displays "Entering [DisplayName]" with rich area metadata during scene switches.
    /// </summary>
    public class LoadingScreenManager : MonoBehaviour
    {
        private static LoadingScreenManager _instance;

        /// <summary>Resources path of the authored manager + loading canvas prefab.</summary>
        private const string PrefabResourcePath = "LoadingScreenManager";

        /// <summary>
        ///     The persistent manager. Spawned on first use from <c>Resources/LoadingScreenManager.prefab</c>;
        ///     null (with an error) if that prefab is missing, so callers fall back to a plain scene load.
        /// </summary>
        public static LoadingScreenManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<LoadingScreenManager>();
                }
                if (_instance == null)
                {
                    LoadingScreenManager prefab = Resources.Load<LoadingScreenManager>(PrefabResourcePath);
                    if (prefab == null)
                    {
                        Debug.LogError("[LoadingScreenManager] Resources/" + PrefabResourcePath + ".prefab is missing.");
                        return null;
                    }
                    // Awake registers _instance and marks it DontDestroyOnLoad.
                    Instantiate(prefab).name = prefab.name;
                }
                return _instance;
            }
        }

        [Header("UI References")]
        [Tooltip("The loading canvas, a child of this prefab.")]
        [SerializeField] private LoadingScreenUI activeLoadingUI;

        [Header("Timing")]
        [Tooltip("Fade in/out duration in seconds.")]
        [SerializeField] private float fadeDuration = 0.35f;

        [Tooltip("Minimum duration in seconds to keep the loading screen visible so the player can read lore.")]
        [SerializeField] private float minDisplayDuration = 1.0f;

        [Header("Shader Prewarming (Optional)")]
        [Tooltip("ShaderVariantCollection to progressively warm up during load (identical to MainMenuManager).")]
        [SerializeField] private ShaderVariantCollection prewarmVariants;
        [SerializeField] private int variantsPerFrame = 25;

        [Tooltip("Longest the fade-out waits on scene-start holds (HoldFadeOut) before revealing the scene anyway.")]
        [SerializeField] private float maxFadeOutHoldSeconds = 8f;

        private bool isLoading = false;
        public bool IsLoading => isLoading;

        /// <summary>True while a transition is on screen, without spawning the manager like <see cref="Instance" /> does.</summary>
        public static bool IsTransitioning => _instance != null && _instance.isLoading;

        private static int fadeOutHolds;

        /// <summary>
        ///     Keeps the loading screen up after the new scene activates until every hold is released
        ///     (or <c>maxFadeOutHoldSeconds</c> passes), so scene-start work like <see cref="EnemyPrewarmer" />
        ///     runs out of sight. Take it in Awake, which runs before the activation finishes.
        /// </summary>
        public static void HoldFadeOut() => fadeOutHolds++;

        public static void ReleaseFadeOut() => fadeOutHolds = Mathf.Max(0, fadeOutHolds - 1);

        /// <summary>Raised when a transition begins, before the loading screen fades in (the music fades out here).</summary>
        public static event System.Action OnTransitionStarted;

        /// <summary>Raised just before the loading screen fades out to reveal the new scene.</summary>
        public static event System.Action OnSceneRevealed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            fadeOutHolds = 0;
            OnTransitionStarted = null;
            OnSceneRevealed = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (activeLoadingUI == null)
            {
                Debug.LogError("[LoadingScreenManager] activeLoadingUI is not assigned; scenes will load without a loading screen.", this);
                return;
            }
            activeLoadingUI.gameObject.SetActive(false);
        }

        /// <summary>
        ///     Loads an area using an AreaDefinitionSO.
        /// </summary>
        public void LoadArea(AreaDefinitionSO areaDef)
        {
            if (areaDef == null)
            {
                Debug.LogError("[LoadingScreenManager] Null AreaDefinitionSO provided!");
                return;
            }

            LoadScene(areaDef.sceneName, AreaMetadata.FromSO(areaDef));
        }

        /// <summary>
        ///     Loads a scene with optional explicit metadata.
        ///     If metadata is null, AreaDatabase resolves the scene name automatically.
        /// </summary>
        public void LoadScene(string sceneName, AreaMetadata metadata = null)
        {
            if (isLoading)
            {
                Debug.LogWarning($"[LoadingScreenManager] Load already in progress for a scene. Ignoring request to load '{sceneName}'.");
                return;
            }

            if (metadata == null)
            {
                metadata = AreaDatabase.GetMetadata(sceneName);
            }

            StartCoroutine(TransitionRoutine(sceneName, metadata));
        }

        /// <summary>
        ///     Loads a scene with explicit display text.
        /// </summary>
        public void LoadScene(string sceneName, string displayName, string subtitle = null, string description = null, Sprite previewSprite = null)
        {
            AreaMetadata meta = new AreaMetadata(sceneName, 0, displayName, subtitle, description, previewSprite);
            LoadScene(sceneName, meta);
        }

        private IEnumerator TransitionRoutine(string sceneName, AreaMetadata metadata)
        {
            isLoading = true;
            Time.timeScale = 1f;
            OnTransitionStarted?.Invoke();

            if (activeLoadingUI != null)
            {
                activeLoadingUI.SetAreaInfo(metadata);
                activeLoadingUI.SetProgress(0f, Loc.Get("loading.preparing", "Preparing the realm"));
                yield return activeLoadingUI.FadeIn(fadeDuration);
            }

            float loadStartTime = Time.unscaledTime;

            // 1. Optional progressive shader prewarm (reusing MainMenuManager logic)
            if (prewarmVariants != null && !prewarmVariants.isWarmedUp && prewarmVariants.variantCount > 0)
            {
                int totalVariants = prewarmVariants.variantCount;
                string shaderStatus = Loc.Get("loading.shaders", "Preparing shaders");
                if (activeLoadingUI != null)
                {
                    activeLoadingUI.SetProgress(0f, shaderStatus);
                }

                while (!prewarmVariants.isWarmedUp && prewarmVariants.warmedUpVariantCount < totalVariants)
                {
                    prewarmVariants.WarmUpProgressively(variantsPerFrame);
                    float shaderProgress = Mathf.Clamp01((float)prewarmVariants.warmedUpVariantCount / totalVariants);
                    float shaderFill = shaderProgress * 0.35f;

                    if (activeLoadingUI != null)
                    {
                        activeLoadingUI.SetProgress(shaderFill, shaderStatus);
                    }
                    yield return null;
                }
            }

            // 2. Asynchronous scene loading
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[LoadingScreenManager] Failed to load scene '{sceneName}'! Check Build Settings.");
                OnSceneRevealed?.Invoke();
                if (activeLoadingUI != null)
                {
                    yield return activeLoadingUI.FadeOut(fadeDuration);
                }
                isLoading = false;
                yield break;
            }

            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                float sceneProgress = Mathf.Clamp01(op.progress / 0.9f);
                float overallProgress = (prewarmVariants != null && prewarmVariants.variantCount > 0)
                    ? 0.35f + (sceneProgress * 0.65f)
                    : sceneProgress;

                if (activeLoadingUI != null)
                {
                    activeLoadingUI.SetProgress(overallProgress);
                }
                yield return null;
            }

            if (activeLoadingUI != null)
            {
                activeLoadingUI.SetProgress(1f, Loc.Get("loading.ready", "Ready"));
                yield return activeLoadingUI.WaitForProgressDisplay(1f);
            }

            // 3. Minimum display duration to ensure player can read lore and prevent visual flashing
            float elapsed = Time.unscaledTime - loadStartTime;
            if (elapsed < minDisplayDuration)
            {
                yield return new WaitForSecondsRealtime(minDisplayDuration - elapsed);
            }

            // 4. Activate scene
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                yield return null;
            }

            // Allow scene start frames to settle
            yield return null;
            yield return null;

            float holdStart = Time.unscaledTime;
            while (fadeOutHolds > 0 && Time.unscaledTime - holdStart < maxFadeOutHoldSeconds)
            {
                yield return null;
            }

            // 5. Fade out and clean up
            OnSceneRevealed?.Invoke();
            if (activeLoadingUI != null)
            {
                yield return activeLoadingUI.FadeOut(fadeDuration);
            }

            isLoading = false;
        }
    }
}
