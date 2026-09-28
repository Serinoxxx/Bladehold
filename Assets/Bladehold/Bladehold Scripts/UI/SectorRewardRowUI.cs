using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     One "label ........ value" line in the victory screen's Sector Rewards list. The row is an authored
///     template child in <c>DeathScreen.prefab</c>; <see cref="DeathScreen" /> clones it once per reward.
///     The icon slot keeps its width when there's no sprite, so the columns stay aligned.
/// </summary>
public class SectorRewardRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text value;

    private void Start()
    {
        if (label == null || value == null)
        {
            Debug.LogError("[SectorRewardRowUI] label/value text is not assigned.", this);
        }
    }

    public void Set(string labelText, string valueText, Sprite iconSprite, Color iconTint)
    {
        if (label != null) label.text = labelText;
        if (value != null) value.text = valueText;
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.color = iconTint;
            icon.enabled = iconSprite != null;
        }
    }
}
