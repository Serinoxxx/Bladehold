using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     View component for the scene transition loading screen.
    ///     Reuses the visual structure from MainMenuManager (logoLoadingFill, loadingBar, loadingText)
    ///     and extends it with rich area meta information ("Entering [DisplayName]", subtitle, lore blurb).
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        [Header("Main Menu Visual Compatibility")]
        [Tooltip("Optional image fill for crest/logo (identical to MainMenuManager).")]
        public Image logoLoadingFill;

        [Tooltip("Progress bar slider (identical to MainMenuManager).")]
        public Slider loadingBar;

        [Tooltip("General loading status text (identical to MainMenuManager).")]
        public TextMeshProUGUI loadingText;

        [Header("Area Meta Information")]
        [Tooltip("Title text displaying 'Entering [DisplayName]' (e.g. 'Entering Bladehold Fortress').")]
        public TextMeshProUGUI enteringTitleText;

        [Tooltip("Subtitle or region tagline (e.g. 'The Inner Gate').")]
        public TextMeshProUGUI subtitleText;

        [Tooltip("Lore blurb or objective description.")]
        public TextMeshProUGUI descriptionText;

        [Tooltip("Optional preview artwork or wallpaper image.")]
        public Image previewImage;

        [Header("Transition")]
        [Tooltip("CanvasGroup used for smooth alpha fading.")]
        public CanvasGroup canvasGroup;

        private string currentAreaDisplayName = "";

        private void Awake()
        {
            EnsureReferences();
        }

        public void EnsureReferences()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (loadingBar == null)
            {
                loadingBar = GetComponentInChildren<Slider>(true);
            }

            if (logoLoadingFill == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img != null && img.gameObject.name.Equals("LogoLoadingFill", System.StringComparison.OrdinalIgnoreCase))
                    {
                        logoLoadingFill = img;
                        break;
                    }
                }
            }

            if (loadingText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var txt in texts)
                {
                    if (txt != null && txt.gameObject.name.Equals("LoadingText", System.StringComparison.OrdinalIgnoreCase))
                    {
                        loadingText = txt;
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     Populates the loading screen with destination area metadata.
        /// </summary>
        public void SetAreaInfo(AreaMetadata meta)
        {
            if (meta == null) return;

            currentAreaDisplayName = !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : "Unknown Realm";

            // 1. Entering [Scene Name] header
            string enteringHeader = $"Entering {currentAreaDisplayName}";
            if (enteringTitleText != null)
            {
                enteringTitleText.text = enteringHeader;
            }
            else if (loadingText != null)
            {
                loadingText.text = enteringHeader;
            }

            // 2. Subtitle
            if (subtitleText != null)
            {
                subtitleText.text = meta.subtitle ?? "";
                subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(meta.subtitle));
            }

            // 3. Description / Lore
            if (descriptionText != null)
            {
                descriptionText.text = meta.description ?? "";
                descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(meta.description));
            }

            // 4. Preview image
            if (previewImage != null)
            {
                if (meta.previewSprite != null)
                {
                    previewImage.sprite = meta.previewSprite;
                    previewImage.gameObject.SetActive(true);
                }
                else
                {
                    previewImage.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        ///     Updates loading progress bar and percentage text.
        /// </summary>
        public void SetProgress(float progress, string customStatus = null)
        {
            float clamped = Mathf.Clamp01(progress);

            if (logoLoadingFill != null)
            {
                logoLoadingFill.fillAmount = clamped;
            }

            if (loadingBar != null)
            {
                loadingBar.value = clamped;
            }

            if (loadingText != null)
            {
                if (!string.IsNullOrEmpty(customStatus))
                {
                    loadingText.text = customStatus;
                }
                else if (enteringTitleText != null)
                {
                    loadingText.text = $"Loading... {(clamped * 100f):F0}%";
                }
                else
                {
                    loadingText.text = $"Entering {currentAreaDisplayName}... {(clamped * 100f):F0}%";
                }
            }
        }

        public IEnumerator FadeIn(float duration)
        {
            EnsureReferences();
            gameObject.SetActive(true);

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                if (duration <= 0f)
                {
                    canvasGroup.alpha = 1f;
                    yield break;
                }

                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }
        }

        public IEnumerator FadeOut(float duration)
        {
            EnsureReferences();

            if (canvasGroup != null)
            {
                if (duration <= 0f)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.blocksRaycasts = false;
                    gameObject.SetActive(false);
                    yield break;
                }

                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }
    }
}
