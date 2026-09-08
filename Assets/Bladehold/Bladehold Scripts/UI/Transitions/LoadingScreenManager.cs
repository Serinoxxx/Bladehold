using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        public static LoadingScreenManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<LoadingScreenManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("LoadingScreenManager");
                        _instance = go.AddComponent<LoadingScreenManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("UI References")]
        [Tooltip("Optional LoadingScreen prefab to instantiate if no instance exists in the scene.")]
        [SerializeField] private GameObject loadingScreenPrefab;

        [Tooltip("Direct reference to an active LoadingScreenUI instance.")]
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

            EnsureLoadingUI();
        }

        /// <summary>
        ///     Finds, instantiates, or builds an active LoadingScreenUI.
        /// </summary>
        private void EnsureLoadingUI()
        {
            if (activeLoadingUI != null) return;

            // 1. Try finding in active scene
            activeLoadingUI = Object.FindAnyObjectByType<LoadingScreenUI>(FindObjectsInactive.Include);
            if (activeLoadingUI != null)
            {
                DontDestroyOnLoad(activeLoadingUI.transform.root.gameObject);
                activeLoadingUI.gameObject.SetActive(false);
                return;
            }

            // 2. Try instantiating configured prefab
            if (loadingScreenPrefab != null)
            {
                GameObject instance = Instantiate(loadingScreenPrefab);
                instance.name = "LoadingScreen_Instance";
                DontDestroyOnLoad(instance);
                activeLoadingUI = instance.GetComponentInChildren<LoadingScreenUI>(true);
                if (activeLoadingUI == null)
                {
                    activeLoadingUI = instance.AddComponent<LoadingScreenUI>();
                }
                activeLoadingUI.gameObject.SetActive(false);
                return;
            }

            // 3. Fallback: Build a lightweight UI canvas dynamically
            activeLoadingUI = CreateFallbackLoadingUI();
        }

        /// <summary>
        ///     Constructs a clean dark-fantasy styled loading screen canvas dynamically as a fallback.
        /// </summary>
        private LoadingScreenUI CreateFallbackLoadingUI()
        {
            GameObject canvasGo = new GameObject("LoadingScreen_Canvas");
            DontDestroyOnLoad(canvasGo);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            CanvasGroup group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // Background panel
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.05f, 0.98f);

            // Container for centered layout
            GameObject container = new GameObject("ContentContainer", typeof(RectTransform));
            container.transform.SetParent(canvasGo.transform, false);
            RectTransform contRt = container.GetComponent<RectTransform>();
            contRt.anchorMin = new Vector2(0.15f, 0.1f);
            contRt.anchorMax = new Vector2(0.85f, 0.9f);
            contRt.sizeDelta = Vector2.zero;

            // Title: "Entering [Scene Name]"
            GameObject titleGo = new GameObject("EnteringTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(container.transform, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.7f);
            titleRt.anchorMax = new Vector2(1f, 0.88f);
            titleRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.fontSize = 44;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(0.95f, 0.85f, 0.55f); // Parchment gold

            // Subtitle
            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(container.transform, false);
            RectTransform subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 0.62f);
            subRt.anchorMax = new Vector2(1f, 0.7f);
            subRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI subTxt = subGo.GetComponent<TextMeshProUGUI>();
            subTxt.alignment = TextAlignmentOptions.Center;
            subTxt.fontSize = 26;
            subTxt.fontStyle = FontStyles.Italic;
            subTxt.color = new Color(0.75f, 0.75f, 0.75f);

            // Description / Lore
            GameObject descGo = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGo.transform.SetParent(container.transform, false);
            RectTransform descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.1f, 0.35f);
            descRt.anchorMax = new Vector2(0.9f, 0.58f);
            descRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI descTxt = descGo.GetComponent<TextMeshProUGUI>();
            descTxt.alignment = TextAlignmentOptions.Center;
            descTxt.fontSize = 22;
            descTxt.enableWordWrapping = true;
            descTxt.color = new Color(0.85f, 0.85f, 0.88f);

            // Progress Slider
            GameObject sliderGo = new GameObject("LoadingBar", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(container.transform, false);
            RectTransform sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.2f, 0.2f);
            sliderRt.anchorMax = new Vector2(0.8f, 0.23f);
            sliderRt.sizeDelta = Vector2.zero;
            Slider slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Slider Background
            GameObject sBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            sBg.transform.SetParent(sliderGo.transform, false);
            RectTransform sBgRt = sBg.GetComponent<RectTransform>();
            sBgRt.anchorMin = Vector2.zero;
            sBgRt.anchorMax = Vector2.one;
            sBgRt.sizeDelta = Vector2.zero;
            Image sBgImg = sBg.GetComponent<Image>();
            sBgImg.color = new Color(0.15f, 0.15f, 0.18f);

            // Slider Fill Area & Fill
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            Image fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.85f, 0.65f, 0.2f); // Golden amber
            slider.fillRect = fillRt;

            // Loading / Percentage text
            GameObject loadTextGo = new GameObject("LoadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
            loadTextGo.transform.SetParent(container.transform, false);
            RectTransform loadTextRt = loadTextGo.GetComponent<RectTransform>();
            loadTextRt.anchorMin = new Vector2(0f, 0.12f);
            loadTextRt.anchorMax = new Vector2(1f, 0.18f);
            loadTextRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI loadTxt = loadTextGo.GetComponent<TextMeshProUGUI>();
            loadTxt.alignment = TextAlignmentOptions.Center;
            loadTxt.fontSize = 20;
            loadTxt.color = new Color(0.7f, 0.7f, 0.7f);

            // Wire view
            LoadingScreenUI ui = canvasGo.AddComponent<LoadingScreenUI>();
            ui.canvasGroup = group;
            ui.enteringTitleText = titleTxt;
            ui.subtitleText = subTxt;
            ui.descriptionText = descTxt;
            ui.loadingBar = slider;
            ui.loadingText = loadTxt;

            canvasGo.SetActive(false);
            return ui;
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

            EnsureLoadingUI();

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
