using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Represents an individual Key Skill preview badge on the Character Select Screen.
    ///     Displays the skill icon and pops up the SkillTooltip on cursor hover.
    /// </summary>
    public class KeySkillBadgeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("The icon image component representing the skill.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Optional text label displaying the skill title under the icon.")]
        [SerializeField] private TMPro.TMP_Text nameLabel;

        [Tooltip("The shared tooltip instance to show on hover.")]
        [SerializeField] private SkillTooltip tooltip;

        private string skillTitle;
        private string skillDescription;

        public Image IconImage => iconImage;
        public TMPro.TMP_Text NameLabel => nameLabel;
        public string SkillTitle => skillTitle;
        public string SkillDescription => skillDescription;

        private void Awake()
        {
            if (iconImage == null)
            {
                var inner = transform.Find("Image");
                if (inner != null)
                {
                    iconImage = inner.GetComponent<Image>();
                }
                else
                {
                    iconImage = GetComponent<Image>();
                }
            }

            if (nameLabel == null)
            {
                var labelT = transform.Find("SkillName") ?? transform.Find("Text");
                if (labelT != null)
                {
                    nameLabel = labelT.GetComponent<TMPro.TMP_Text>();
                }
            }

            if (tooltip == null)
            {
                tooltip = FindObjectOfType<SkillTooltip>(true);
            }
        }

        public void Setup(string title, string description, Sprite icon, SkillTooltip tooltipRef = null)
        {
            skillTitle = title;
            skillDescription = description;
            if (tooltipRef != null)
            {
                tooltip = tooltipRef;
            }

            if (nameLabel != null)
            {
                nameLabel.text = title;
            }

            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
        }

        public void SetTooltip(SkillTooltip tooltipRef)
        {
            tooltip = tooltipRef;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null && !string.IsNullOrEmpty(skillTitle))
            {
                tooltip.ShowDirect(skillTitle, skillDescription, "");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
        }

        private void OnDisable()
        {
            if (tooltip != null && tooltip.gameObject.activeSelf)
            {
                tooltip.Hide();
            }
        }
    }
}
