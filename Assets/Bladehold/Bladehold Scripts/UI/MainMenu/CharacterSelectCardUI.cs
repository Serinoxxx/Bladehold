using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Represents an individual character class card on the Main Menu Character Select Screen.
    ///     Reacts to hover events using authored MMF_Player feedbacks, handles smooth scale lerping,
    ///     manages selection border and sorting, and reports click/hover events to the controller.
    /// </summary>
    public class CharacterSelectCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Class Configuration")]
        [Tooltip("Identifier persisted in SaveData (e.g. swordsman, berserker, mage).")]
        [SerializeField] private string classId = "swordsman";

        [Tooltip("Player-facing class name (e.g. Ranger, Berserker, Mage).")]
        [SerializeField] private string className = "Ranger";

        [Tooltip("Character individual name (e.g. Galius, Bronin, Casteria).")]
        [SerializeField] private string characterName = "Galius";

        [Tooltip("Custom description for this character blurb.")]
        [TextArea(3, 6)]
        [SerializeField] private string classDescription;

        [Tooltip("Optional ClassDefinitionSO reference for fallback description and data.")]
        [SerializeField] private ClassDefinitionSO classDefinition;

        [Serializable]
        public class KeySkillInfo
        {
            [Tooltip("Optional skill ID to resolve from the class's SkillTreeSO.")]
            public string skillId;
            [Tooltip("Display name of the skill shown in the tooltip.")]
            public string skillTitle;
            [TextArea(2, 4)]
            [Tooltip("Detailed description of the skill shown in the tooltip.")]
            public string skillDescription;
            [Tooltip("Icon sprite for the preview badge.")]
            public Sprite icon;

            public KeySkillInfo() { }

            public KeySkillInfo(string id, string title, string description, Sprite iconSprite)
            {
                skillId = id;
                skillTitle = title;
                skillDescription = description;
                icon = iconSprite;
            }
        }

        [Tooltip("Detailed Key Skills displayed in the preview badges and hover tooltips.")]
        [SerializeField] private List<KeySkillInfo> keySkills = new List<KeySkillInfo>();

        [Tooltip("Legacy icon sprites fallback for preview badges.")]
        [SerializeField] private Sprite[] keySkillIcons;

        [Header("UI References")]
        [SerializeField] private GameObject selectionBorder;
        [SerializeField] private Canvas cardCanvas;
        [SerializeField] private Button cardButton;

        [Header("Feedbacks")]
        [SerializeField] private MMF_Player hoverEnterFeedback;
        [SerializeField] private MMF_Player hoverExitFeedback;
        [SerializeField] private MMF_Player selectFeedback;

        [Header("Color Tint")]
        [Tooltip("The card's background image component whose color is tinted.")]
        [SerializeField] private Image cardBackgroundImage;

        [Tooltip("Color when the card is unselected (dimmed/muted).")]
        [SerializeField] private Color normalColor = new Color(0.585f, 0.562f, 0.488f, 1.0f);

        [Tooltip("Color when the card is selected (lighter / illuminated).")]
        [SerializeField] private Color selectedColor = Color.white;

        [Tooltip("Speed at which the card color lerps between states.")]
        [SerializeField] private float colorLerpSpeed = 10f;

        [Header("Scaling & Pulse")]
        [SerializeField] private bool useSmoothScale = true;
        [SerializeField] private float selectedScale = 1.20f;
        [SerializeField] private float normalScale = 1.00f;
        [SerializeField] private float scaleLerpSpeed = 12f;

        [Tooltip("Pulse the scale of the selected card gently over time.")]
        [SerializeField] private bool pulseSelectedScale = true;

        [Tooltip("Speed of the scale pulsing cycle (radians per second).")]
        [SerializeField] private float pulseSpeed = 2.0f;

        [Tooltip("Amplitude of the scale pulse offset.")]
        [SerializeField] private float pulseAmplitude = 0.025f;

        private bool isSelected;
        private Vector3 targetScale = Vector3.one;

        public event Action<CharacterSelectCardUI> OnCardClicked;
        public event Action<CharacterSelectCardUI> OnCardHovered;
        public event Action<CharacterSelectCardUI> OnCardHoverExited;

        public string ClassId => classId;
        public string ClassName => className;
        public string CharacterName => characterName;
        public IReadOnlyList<KeySkillInfo> KeySkills => keySkills;
        public Sprite[] KeySkillIcons => keySkillIcons;
        public bool IsSelected => isSelected;

        public string ClassDescription
        {
            get
            {
                if (!string.IsNullOrEmpty(classDescription))
                {
                    return classDescription;
                }
                if (classDefinition != null)
                {
                    return classDefinition.LocalizedDescription;
                }
                return string.Empty;
            }
        }

        private void Awake()
        {
            AutoWireReferences();

            ForceUnscaledTime(hoverEnterFeedback);
            ForceUnscaledTime(hoverExitFeedback);
            ForceUnscaledTime(selectFeedback);

            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(HandleButtonClicked);
                cardButton.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (cardButton != null)
            {
                cardButton.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        private void Update()
        {
            // 1. Smooth Color Tint
            if (cardBackgroundImage != null)
            {
                Color targetColor = isSelected ? selectedColor : normalColor;
                if (cardBackgroundImage.color != targetColor)
                {
                    cardBackgroundImage.color = Color.Lerp(cardBackgroundImage.color, targetColor, Time.unscaledDeltaTime * colorLerpSpeed);
                }
            }

            // 2. Scale Lerping & Selected Pulsing
            if (useSmoothScale)
            {
                float baseScale = isSelected ? selectedScale : normalScale;
                float pulseOffset = (isSelected && pulseSelectedScale)
                    ? Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude
                    : 0f;

                Vector3 currentTarget = Vector3.one * (baseScale + pulseOffset);
                transform.localScale = Vector3.Lerp(transform.localScale, currentTarget, Time.unscaledDeltaTime * scaleLerpSpeed);
            }
        }

        private void AutoWireReferences()
        {
            if (cardBackgroundImage == null)
            {
                cardBackgroundImage = GetComponent<Image>();
            }

            if (cardCanvas == null)
            {
                cardCanvas = GetComponent<Canvas>();
            }

            if (cardButton == null)
            {
                cardButton = GetComponent<Button>();
            }

            if (selectionBorder == null)
            {
                var borderT = transform.Find("SelectionBorder");
                if (borderT != null)
                {
                    selectionBorder = borderT.gameObject;
                }
            }

            if (hoverEnterFeedback == null)
            {
                var t = transform.Find("HoverEnterFeedback");
                if (t != null) hoverEnterFeedback = t.GetComponent<MMF_Player>();
            }

            if (hoverExitFeedback == null)
            {
                var t = transform.Find("HoverExitFeedback");
                if (t != null) hoverExitFeedback = t.GetComponent<MMF_Player>();
            }

            if (selectFeedback == null)
            {
                var t = transform.Find("SelectFeedback");
                if (t != null) selectFeedback = t.GetComponent<MMF_Player>();
            }
        }

        private static void ForceUnscaledTime(MMF_Player player)
        {
            if (player == null) return;
            player.ForceTimescaleMode = true;
            player.ForcedTimescaleMode = TimescaleModes.Unscaled;
            player.PlayerTimescaleMode = TimescaleModes.Unscaled;
        }

        public void SetSelected(bool selected, bool immediate = false)
        {
            isSelected = selected;
            targetScale = selected ? Vector3.one * selectedScale : Vector3.one * normalScale;

            if (immediate)
            {
                transform.localScale = targetScale;
                if (cardBackgroundImage != null)
                {
                    cardBackgroundImage.color = selected ? selectedColor : normalColor;
                }
            }

            if (selectionBorder != null)
            {
                selectionBorder.SetActive(selected);
            }

            if (cardCanvas != null)
            {
                cardCanvas.overrideSorting = selected;
                cardCanvas.sortingOrder = selected ? 10 : 0;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SafePlayFeedbacks(hoverEnterFeedback);
            OnCardHovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SafePlayFeedbacks(hoverExitFeedback);
            OnCardHoverExited?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerSelect();
        }

        private void HandleButtonClicked()
        {
            TriggerSelect();
        }

        private void TriggerSelect()
        {
            SafePlayFeedbacks(selectFeedback);
            OnCardClicked?.Invoke(this);
        }

        private static void SafePlayFeedbacks(MMF_Player player)
        {
            if (player == null) return;
            try
            {
                player.PlayFeedbacks();
            }
            catch (Exception ex)
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning($"[CharacterSelectCardUI] Feedback exception: {ex.Message}");
                }
            }
        }
    }
}
