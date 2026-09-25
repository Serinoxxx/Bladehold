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

        private bool isLoading = false;
        public bool IsLoading => isLoading;

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

            if (activeLoadingUI != null)
            {
                activeLoadingUI.SetAreaInfo(metadata);
                activeLoadingUI.SetProgress(0f, "Preparing realm...");
                yield return activeLoadingUI.FadeIn(fadeDuration);
            }

            float loadStartTime = Time.unscaledTime;

            // 1. Optional progressive shader prewarm (reusing MainMenuManager logic)
            if (prewarmVariants != null && !prewarmVariants.isWarmedUp && prewarmVariants.variantCount > 0)
            {
                int totalVariants = prewarmVariants.variantCount;
                if (activeLoadingUI != null)
                {
                    activeLoadingUI.SetProgress(0f, "Prewarming Shaders...");
                }

                while (!prewarmVariants.isWarmedUp && prewarmVariants.warmedUpVariantCount < totalVariants)
                {
                    prewarmVariants.WarmUpProgressively(variantsPerFrame);
                    float shaderProgress = Mathf.Clamp01((float)prewarmVariants.warmedUpVariantCount / totalVariants);
                    float shaderFill = shaderProgress * 0.35f;

                    if (activeLoadingUI != null)
                    {
                        activeLoadingUI.SetProgress(shaderFill, $"Prewarming Shaders... {(shaderProgress * 100f):F0}%");
                    }
                    yield return null;
                }
            }

            // 2. Asynchronous scene loading
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[LoadingScreenManager] Failed to load scene '{sceneName}'! Check Build Settings.");
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
                activeLoadingUI.SetProgress(1f, "Entering " + (!string.IsNullOrEmpty(metadata?.displayName) ? metadata.displayName : "Realm"));
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

            // 5. Fade out and clean up
            if (activeLoadingUI != null)
            {
                yield return activeLoadingUI.FadeOut(fadeDuration);
            }

            isLoading = false;
        }
    }
}
