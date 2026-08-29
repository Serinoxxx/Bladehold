using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Controls the Main Menu Character Select Screen.
    ///     Manages the hero cards, responds to card hover and selection events,
    ///     updates the character description and key skill badges dynamically,
    ///     synchronizes with the 3D CharacterRotunda if present, and advances
    ///     the player to the level select screen upon confirmation.
    /// </summary>
    public class CharacterSelectScreenUI : MonoBehaviour
    {
        [Header("Cards")]
        [SerializeField] private List<CharacterSelectCardUI> cards = new List<CharacterSelectCardUI>();

        [Header("Description Panel")]
        [Tooltip("The text component displaying the selected hero's description.")]
        [SerializeField] private TMP_Text descriptionText;

        [Tooltip("Optional label displaying the selected class title (e.g. Ranger).")]
        [SerializeField] private TMP_Text classTitleText;

        [Tooltip("Optional label displaying the character name (e.g. Galius).")]
        [SerializeField] private TMP_Text characterNameText;

        [Header("Key Skills & Tooltip")]
        [Tooltip("The shared tooltip component for displaying skill details on hover.")]
        [SerializeField] private SkillTooltip tooltip;

        [Tooltip("Badges in the Key Skills section displaying ability icons and triggering tooltips.")]
        [SerializeField] private KeySkillBadgeUI[] keySkillBadges;

        [Tooltip("If true, hovering over a card previews its description and key skills without selecting it.")]
        [SerializeField] private bool previewOnHover = false;

        [Header("Action Buttons")]
        [Tooltip("Button that confirms character selection and moves to level select.")]
        [SerializeField] private Button confirmButton;

        [Tooltip("Button that returns to the main menu title screen.")]
        [SerializeField] private Button backButton;

        [Header("Integration")]
        [SerializeField] private MainMenuManager mainMenuManager;
        [SerializeField] private CharacterRotunda rotunda;
        [SerializeField] private bool syncRotunda = true;

        private CharacterSelectCardUI currentSelectedCard;

        public CharacterSelectCardUI SelectedCard => currentSelectedCard;

        private void Awake()
        {
            AutoWireReferences();

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
                confirmButton.onClick.AddListener(HandleConfirmClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
                backButton.onClick.AddListener(HandleBackClicked);
            }

            foreach (var card in cards)
            {
                if (card == null) continue;
                card.OnCardClicked += HandleCardClicked;
                card.OnCardHovered += HandleCardHovered;
                card.OnCardHoverExited += HandleCardHoverExited;
            }
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
            }

            foreach (var card in cards)
            {
                if (card == null) continue;
                card.OnCardClicked -= HandleCardClicked;
                card.OnCardHovered -= HandleCardHovered;
                card.OnCardHoverExited -= HandleCardHoverExited;
            }
        }

        private void OnEnable()
        {
            string savedClassId = "swordsman";
            SaveData data = SaveSystem.Load();
            if (data != null && !string.IsNullOrEmpty(data.playerClassId))
            {
                savedClassId = data.playerClassId;
            }

            CharacterSelectCardUI targetCard = null;
            foreach (var card in cards)
            {
                if (card != null && string.Equals(card.ClassId, savedClassId, System.StringComparison.OrdinalIgnoreCase))
                {
                    targetCard = card;
                    break;
                }
            }

            if (targetCard == null && cards.Count > 0)
            {
                targetCard = cards[0];
            }

            if (targetCard != null)
            {
                SelectCard(targetCard, immediate: true);
            }
        }

        private void AutoWireReferences()
        {
            if (mainMenuManager == null)
            {
                mainMenuManager = GetComponentInParent<MainMenuManager>() ?? FindObjectOfType<MainMenuManager>();
            }

            if (rotunda == null)
            {
                rotunda = FindObjectOfType<CharacterRotunda>();
            }

            if (cards.Count == 0)
            {
                var container = transform.Find("CardsContainer");
                if (container != null)
                {
                    cards.AddRange(container.GetComponentsInChildren<CharacterSelectCardUI>(true));
                }
            }

            if (descriptionText == null)
            {
                var descT = transform.Find("BottomDescPanel/SelectedStageTitle");
                if (descT != null)
                {
                    descriptionText = descT.GetComponent<TMP_Text>();
                }
            }

            if (tooltip == null)
            {
                tooltip = FindObjectOfType<SkillTooltip>(true);
            }

            if (keySkillBadges == null || keySkillBadges.Length == 0)
            {
                var badgesContainer = transform.Find("BottomDescPanel/Skills Panel") ?? transform.Find("BottomDescPanel/GameObject");
                if (badgesContainer != null)
                {
                    var badgeList = new List<KeySkillBadgeUI>();
                    for (int i = 0; i < badgesContainer.childCount; i++)
                    {
                        var badge = badgesContainer.GetChild(i);
                        var badgeUI = badge.GetComponent<KeySkillBadgeUI>();
                        if (badgeUI == null)
                        {
                            badgeUI = badge.gameObject.AddComponent<KeySkillBadgeUI>();
                        }
                        badgeUI.SetTooltip(tooltip);
                        badgeList.Add(badgeUI);
                    }
                    keySkillBadges = badgeList.ToArray();
                }
            }

            if (confirmButton == null)
            {
                var btn = transform.Find("ActionButtons/PlayStageButton");
                if (btn != null) confirmButton = btn.GetComponent<Button>();
            }

            if (backButton == null)
            {
                var btn = transform.Find("ActionButtons/BackButton");
                if (btn != null) backButton = btn.GetComponent<Button>();
            }
        }

        private void OnDisable()
        {
            if (tooltip != null && tooltip.gameObject.activeSelf)
            {
                tooltip.Hide();
            }
        }

        public void SelectCard(CharacterSelectCardUI card, bool immediate = false)
        {
            if (card == null) return;

            currentSelectedCard = card;

            foreach (var c in cards)
            {
                if (c != null)
                {
                    c.SetSelected(c == card, immediate);
                }
            }

            UpdateDescriptionPanel(card);

            if (syncRotunda && rotunda != null)
            {
                int index = cards.IndexOf(card);
                if (index >= 0)
                {
                    rotunda.Select(index);
                }
            }

            PlayerClassController.SetSavedClass(card.ClassId);
        }

        private void UpdateDescriptionPanel(CharacterSelectCardUI card)
        {
            if (card == null) return;

            if (descriptionText != null)
            {
                descriptionText.text = card.ClassDescription;
            }

            if (classTitleText != null)
            {
                classTitleText.text = card.ClassName;
            }

            if (characterNameText != null)
            {
                characterNameText.text = card.CharacterName;
            }

            if (keySkillBadges != null)
            {
                for (int i = 0; i < keySkillBadges.Length; i++)
                {
                    if (keySkillBadges[i] == null) continue;

                    if (card.KeySkills != null && i < card.KeySkills.Count)
                    {
                        var skill = card.KeySkills[i];
                        keySkillBadges[i].gameObject.SetActive(true);
                        keySkillBadges[i].Setup(skill.skillTitle, skill.skillDescription, skill.icon, tooltip);
                    }
                    else if (card.KeySkillIcons != null && i < card.KeySkillIcons.Length && card.KeySkillIcons[i] != null)
                    {
                        keySkillBadges[i].gameObject.SetActive(true);
                        keySkillBadges[i].Setup("", "", card.KeySkillIcons[i], tooltip);
                    }
                    else
                    {
                        keySkillBadges[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void HandleCardClicked(CharacterSelectCardUI card)
        {
            SelectCard(card);
        }

        private void HandleCardHovered(CharacterSelectCardUI card)
        {
            if (previewOnHover && card != null)
            {
                UpdateDescriptionPanel(card);
            }
        }

        private void HandleCardHoverExited(CharacterSelectCardUI card)
        {
            if (previewOnHover && currentSelectedCard != null)
            {
                UpdateDescriptionPanel(currentSelectedCard);
            }
        }

        private void HandleConfirmClicked()
        {
            if (currentSelectedCard != null)
            {
                PlayerClassController.SetSavedClass(currentSelectedCard.ClassId);
            }

            if (mainMenuManager != null)
            {
                mainMenuManager.OnCharacterSelected();
            }
        }

        private void HandleBackClicked()
        {
            if (mainMenuManager != null)
            {
                mainMenuManager.OnBackToTitle();
            }
        }
    }
}
