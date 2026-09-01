using System;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Represents an individual stage tile card on the Main Menu Level Select Screen.
    ///     Features a scenic level preview with gradient, parchment title header, duration timer,
    ///     locked overlay, smooth unscaled lerp to 120% scale on selection, and MMF_Player hover feedbacks.
    /// </summary>
    public class LevelSelectCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float SelectedScale = 1.20f;
        private const float NormalScale = 1.00f;
        private const float ScaleLerpSpeed = 12f;

        [Header("UI References")]
        [SerializeField] private Image previewImage;
        [SerializeField] private Image gradientOverlay;
        [SerializeField] private Image parchmentHeader;
        [SerializeField] private TMP_Text stageNumberText;
        [SerializeField] private TMP_Text stageTitleText;
        [SerializeField] private Image timerIcon;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TMP_Text lockStatusText;
        [SerializeField] private Image selectionBorder;

        [Header("Sorting")]
        [SerializeField] private Canvas cardCanvas;

        [Header("Feedbacks")]
        [SerializeField] private MMF_Player hoverEnterFeedback;
        [SerializeField] private MMF_Player hoverExitFeedback;
        [SerializeField] private MMF_Player selectFeedback;

        [Header("State")]
        private int stageNumber;
        private string stageName;
        private string stageDescription;
        private string duration;
        private bool isUnlocked;
        private bool isSelected;
        private Vector3 targetScale = Vector3.one;

        private Action<LevelSelectCardUI> onClickCallback;
        private Action<LevelSelectCardUI> onHoverCallback;

        public int StageNumber => stageNumber;
        public string StageName => stageName;
        public string StageDescription => stageDescription;
        public string Duration => duration;
        public bool IsUnlocked => isUnlocked;
        public bool IsSelected => isSelected;

        private void Awake()
        {
            if (cardCanvas == null)
            {
                cardCanvas = GetComponent<Canvas>();
            }

            ForceUnscaledTime(hoverEnterFeedback);
            ForceUnscaledTime(hoverExitFeedback);
            ForceUnscaledTime(selectFeedback);
        }

        private void Update()
        {
            if (transform.localScale != targetScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * ScaleLerpSpeed);
                if (Vector3.Distance(transform.localScale, targetScale) < 0.001f)
                {
                    transform.localScale = targetScale;
                }
            }
        }

        private static void ForceUnscaledTime(MMF_Player player)
        {
            if (player == null) return;
            player.ForceTimescaleMode = true;
            player.ForcedTimescaleMode = TimescaleModes.Unscaled;
            player.PlayerTimescaleMode = TimescaleModes.Unscaled;
        }

        public void Setup(
            int number,
            string name,
            string desc,
            string durationStr,
            bool unlocked,
            bool selected,
            Sprite previewSprite,
            Action<LevelSelectCardUI> onClick,
            Action<LevelSelectCardUI> onHover)
        {
            stageNumber = number;
            stageName = name;
            stageDescription = desc;
            duration = durationStr;
            isUnlocked = unlocked;
            isSelected = selected;
            onClickCallback = onClick;
            onHoverCallback = onHover;

            if (stageNumberText != null)
            {
                stageNumberText.text = $"STAGE {number}";
            }

            if (stageTitleText != null)
            {
                stageTitleText.text = name;
            }

            if (timerText != null)
            {
                timerText.text = durationStr;
            }

            if (previewImage != null && previewSprite != null)
            {
                previewImage.sprite = previewSprite;
            }

            if (lockOverlay != null)
            {
                lockOverlay.SetActive(!isUnlocked);
            }

            if (lockStatusText != null)
            {
                lockStatusText.text = isUnlocked ? "UNLOCKED" : $"LOCKED\n<size=24><color=#CCCCCC>Survive Stage {number - 1}</color></size>";
            }

            SetSelected(selected, immediate: true);
        }

        public void SetSelected(bool selected, bool immediate = false)
        {
            if (!isUnlocked && selected)
            {
                selected = false;
            }

            isSelected = selected;
            targetScale = selected ? Vector3.one * SelectedScale : Vector3.one * NormalScale;

            if (immediate)
            {
                transform.localScale = targetScale;
            }

            if (selectionBorder != null)
            {
                selectionBorder.gameObject.SetActive(selected);
            }

            if (cardCanvas != null)
            {
                cardCanvas.overrideSorting = selected;
                cardCanvas.sortingOrder = selected ? 10 : 0;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isUnlocked) return;

            if (hoverEnterFeedback != null)
            {
                hoverEnterFeedback.PlayFeedbacks();
            }
            onHoverCallback?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isUnlocked) return;

            if (hoverExitFeedback != null)
            {
                hoverExitFeedback.PlayFeedbacks();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isUnlocked) return;

            if (selectFeedback != null)
            {
                selectFeedback.PlayFeedbacks();
            }
            onClickCallback?.Invoke(this);
        }
    }
}
