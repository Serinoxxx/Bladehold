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

        [Header("Reference Style Layout Elements")]
        [Tooltip("Displays the class display name at the top of the card.")]
        [SerializeField] private TMPro.TMP_Text classTitleLabel;

        [Tooltip("Displays the archetype/role subtitle under the portrait (e.g. Steadfast Defender).")]
        [SerializeField] private TMPro.TMP_Text roleSubtitleLabel;
        [SerializeField] private string roleSubtitle = "Steadfast Defender";

        [Tooltip("Displays the health number under the class title (e.g. 716/716).")]
        [SerializeField] private TMPro.TMP_Text healthTextLabel;
        [SerializeField] private string healthValueString = "716/716";

        [Tooltip("Placeholder hero portrait image from Synty.")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Sprite portraitSprite;

        [Tooltip("Optional 'SELECTED' pill badge at top of the card.")]
        [SerializeField] private GameObject selectedBadge;

        [Tooltip("Glowing cyan/gold border shown when selected.")]
        [SerializeField] private GameObject selectedGlowBorder;

        [Tooltip("Standard bronze border shown when unselected.")]
        [SerializeField] private GameObject normalBorder;

        [Tooltip("Soft glow / aura effect behind the selected hero card.")]
        [SerializeField] private GameObject selectedBackGlow;

        [Tooltip("The ornate 'CLASS DESCRIPTION' plaque shown on the selected card.")]
        [SerializeField] private GameObject descriptionPanel;
        [SerializeField] private TMPro.TMP_Text descriptionLabel;

        [Tooltip("The 3 key skill preview badges embedded directly in this card.")]
        [SerializeField] private KeySkillBadgeUI[] cardSkillBadges;

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
        [SerializeField] private float selectedScale = 1.00f;
        [SerializeField] private float normalScale = 1.00f;
        [SerializeField] private float scaleLerpSpeed = 12f;
        [SerializeField] private Vector2 selectedSizeDelta = new Vector2(810f, 1320f);
        [SerializeField] private Vector2 normalSizeDelta = new Vector2(760f, 1140f);

        [Tooltip("Pulse the scale of the selected card gently over time.")]
        [SerializeField] private bool pulseSelectedScale = true;

        [Tooltip("Speed of the scale pulsing cycle (radians per second).")]
        [SerializeField] private float pulseSpeed = 2.0f;

        [Tooltip("Amplitude of the scale pulse offset.")]
        [SerializeField] private float pulseAmplitude = 0.025f;

        private bool isSelected;
        private Vector3 targetScale = Vector3.one;
        private Vector2 targetSizeDelta = new Vector2(790f, 1180f);

        public event Action<CharacterSelectCardUI> OnCardClicked;
        public event Action<CharacterSelectCardUI> OnCardHovered;
        public event Action<CharacterSelectCardUI> OnCardHoverExited;

        public string ClassId => classId;
        public string ClassName => className;
        public string CharacterName => characterName;
        public string HealthValueString => healthValueString;
        public Sprite PortraitSprite => portraitSprite;
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

                var rt = transform as RectTransform;
                if (rt != null && rt.sizeDelta != targetSizeDelta)
                {
                    rt.sizeDelta = Vector2.Lerp(rt.sizeDelta, targetSizeDelta, Time.unscaledDeltaTime * scaleLerpSpeed);
                }
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

            if (classTitleLabel == null)
            {
                var t = transform.Find("ClassTitleText") ?? transform.Find("ParchmentHeader/ClassTitleText");
                if (t != null) classTitleLabel = t.GetComponent<TMPro.TMP_Text>();
            }

            if (roleSubtitleLabel == null)
            {
                var t = transform.Find("RoleSubtitleText");
                if (t != null) roleSubtitleLabel = t.GetComponent<TMPro.TMP_Text>();
            }

            if (healthTextLabel == null)
            {
                var t = transform.Find("HealthBar/HealthText") ?? transform.Find("HealthBar/Text");
                if (t != null) healthTextLabel = t.GetComponent<TMPro.TMP_Text>();
            }

            if (portraitImage == null)
            {
                var t = transform.Find("Portrait") ?? transform.Find("PortraitImage");
                if (t != null) portraitImage = t.GetComponent<Image>();
            }

            if (selectedBadge == null)
            {
                var t = transform.Find("SelectedBadge");
                if (t != null) selectedBadge = t.gameObject;
            }

            if (selectedGlowBorder == null)
            {
                var t = transform.Find("SelectedGlowBorder");
                if (t != null) selectedGlowBorder = t.gameObject;
            }

            if (normalBorder == null)
            {
                var t = transform.Find("NormalBorder");
                if (t != null) normalBorder = t.gameObject;
            }

            if (selectedBackGlow == null)
            {
                var t = transform.Find("SelectedBackGlow");
                if (t != null) selectedBackGlow = t.gameObject;
            }

            if (descriptionPanel == null)
            {
                var t = transform.Find("DescriptionPanel");
                if (t != null) descriptionPanel = t.gameObject;
            }

            if (descriptionLabel == null && descriptionPanel != null)
            {
                var t = descriptionPanel.transform.Find("DescriptionText") ?? descriptionPanel.transform.Find("Text");
                if (t != null) descriptionLabel = t.GetComponent<TMPro.TMP_Text>();
            }

            if (cardSkillBadges == null || cardSkillBadges.Length == 0)
            {
                var skillsContainer = transform.Find("SkillsPanel") ?? transform.Find("Skills");
                if (skillsContainer != null)
                {
                    cardSkillBadges = skillsContainer.GetComponentsInChildren<KeySkillBadgeUI>(true);
                }
            }
        }

        private void Start()
        {
            RefreshCardVisuals();
        }

        /// <summary>
        /// Populates this card's visual elements, text, portrait, and skills dynamically from a ClassDefinitionSO.
        /// </summary>
        public void PopulateFromDefinition(ClassDefinitionSO def, SkillTooltip tooltip = null)
        {
            if (def == null) return;

            AutoWireReferences();

            classDefinition = def;
            classId = def.id;
            className = !string.IsNullOrEmpty(def.displayName) ? def.displayName : def.id;
            characterName = def.CharacterName;
            roleSubtitle = characterName;
            healthValueString = !string.IsNullOrEmpty(def.healthDisplay) ? def.healthDisplay : "100/100";
            portraitSprite = def.portrait;
            classDescription = !string.IsNullOrEmpty(def.description) ? def.description : def.LocalizedDescription;

            keySkills.Clear();
            if (def.keySkills != null && def.keySkills.Count > 0)
            {
                foreach (var entry in def.keySkills)
                {
                    keySkills.Add(new KeySkillInfo(entry.skillId, entry.skillTitle, entry.skillDescription, entry.icon));
                }
            }
            else if (def.keySkillIds != null && def.skillTree != null)
            {
                foreach (string sId in def.keySkillIds)
                {
                    var node = def.skillTree.GetById(sId);
                    if (node != null)
                    {
                        Sprite icon = def.skillTree.GetIcon(node.iconName);
                        keySkills.Add(new KeySkillInfo(node.id, node.displayName, node.description, icon));
                    }
                }
            }

            RefreshCardVisuals(tooltip);
        }

        public void RefreshCardVisuals(SkillTooltip externalTooltip = null)
        {
            if (classTitleLabel != null)
            {
                classTitleLabel.text = !string.IsNullOrEmpty(className) ? className.ToUpper() : "";
            }

            if (roleSubtitleLabel != null)
            {
                roleSubtitleLabel.text = roleSubtitle;
            }

            if (healthTextLabel != null)
            {
                healthTextLabel.text = healthValueString;
            }

            if (portraitImage != null && portraitSprite != null)
            {
                portraitImage.sprite = portraitSprite;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = ClassDescription;
            }

            if (cardSkillBadges != null && cardSkillBadges.Length > 0)
            {
                var tooltip = externalTooltip != null ? externalTooltip : FindObjectOfType<SkillTooltip>(true);
                for (int i = 0; i < cardSkillBadges.Length; i++)
                {
                    if (cardSkillBadges[i] == null) continue;

                    if (keySkills != null && i < keySkills.Count)
                    {
                        cardSkillBadges[i].gameObject.SetActive(true);
                        cardSkillBadges[i].Setup(keySkills[i].skillTitle, keySkills[i].skillDescription, keySkills[i].icon, tooltip);
                    }
                    else if (keySkillIcons != null && i < keySkillIcons.Length && keySkillIcons[i] != null)
                    {
                        cardSkillBadges[i].gameObject.SetActive(true);
                        cardSkillBadges[i].Setup("", "", keySkillIcons[i], tooltip);
                    }
                    else
                    {
                        cardSkillBadges[i].gameObject.SetActive(false);
                    }
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

        public void SetSelected(bool selected, bool immediate = false)
        {
            isSelected = selected;
            targetScale = selected ? Vector3.one * selectedScale : Vector3.one * normalScale;
            targetSizeDelta = selected ? selectedSizeDelta : normalSizeDelta;

            var rt = transform as RectTransform;
            if (immediate)
            {
                transform.localScale = targetScale;
                if (rt != null) rt.sizeDelta = targetSizeDelta;
                if (cardBackgroundImage != null)
                {
                    cardBackgroundImage.color = selected ? selectedColor : normalColor;
                }
            }
            else if (!useSmoothScale && rt != null)
            {
                rt.sizeDelta = targetSizeDelta;
            }

            if (selectionBorder != null)
            {
                selectionBorder.SetActive(selected);
            }

            if (selectedBadge != null)
            {
                selectedBadge.SetActive(selected);
            }

            if (selectedGlowBorder != null)
            {
                selectedGlowBorder.SetActive(selected);
            }

            if (selectedBackGlow != null)
            {
                selectedBackGlow.SetActive(selected);
            }

            if (normalBorder != null)
            {
                normalBorder.SetActive(!selected);
            }

            if (descriptionPanel != null)
            {
                descriptionPanel.SetActive(selected);
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
