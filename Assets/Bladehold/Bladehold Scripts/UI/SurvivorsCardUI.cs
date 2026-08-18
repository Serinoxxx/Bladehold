using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Attached to the UI Card prefab for Survivors mode level-up card selection.
///     Encapsulates rendering skill info (icon, title, description, level badge)
///     and handling card click events.
/// </summary>
public class SurvivorsCardUI : MonoBehaviour
{
    [Header("Card UI References")]
    [Tooltip("Primary button component on the card.")]
    [SerializeField] private Button selectButton;

    [Tooltip("Icon image component.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Title text component.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Description text component.")]
    [SerializeField] private TextMeshProUGUI descText;

    [Tooltip("Level badge text component.")]
    [SerializeField] private TextMeshProUGUI levelText;

    private Action onClickedCallback;

    private void Awake()
    {
        AutoWireReferences();
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleClick);
        }
    }

    private void OnValidate()
    {
        AutoWireReferences();
    }

    /// <summary>
    /// Auto-wires component references based on standard Card prefab child object names.
    /// </summary>
    public void AutoWireReferences()
    {
        if (selectButton == null) selectButton = GetComponent<Button>();
        if (titleText == null) titleText = transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (levelText == null) levelText = transform.Find("LevelBadge")?.GetComponent<TextMeshProUGUI>();
        if (descText == null) descText = transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
        if (iconImage == null) iconImage = transform.Find("Icon")?.GetComponent<Image>();
    }

    /// <summary>
    /// Populates the card with skill node data, level badge info, icon, and click handler.
    /// </summary>
    public void SetData(SkillNode node, int currentLevel, Sprite icon, Action onClicked)
    {
        onClickedCallback = onClicked;

        int nextLevel = currentLevel + 1;

        if (titleText != null)
        {
            titleText.text = node != null ? node.LocalizedDisplayName : "";
        }

        if (descText != null && node != null)
        {
            string body = currentLevel > 0 && !string.IsNullOrEmpty(node.LocalizedUpgradeText)
                ? node.LocalizedUpgradeText
                : node.LocalizedDescription;
            descText.text = body;
        }

        if (levelText != null && node != null)
        {
            levelText.text = currentLevel > 0
                ? $"Level {currentLevel} -> {nextLevel} (Max {node.maxLevel})"
                : $"Unlock Level 1 (Max {node.maxLevel})";
        }

        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Enables or disables button interactability (e.g. for the 0.5s anti-accidental-click delay).
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (selectButton != null)
        {
            selectButton.interactable = interactable;
        }
    }

    private void HandleClick()
    {
        onClickedCallback?.Invoke();
    }
}
